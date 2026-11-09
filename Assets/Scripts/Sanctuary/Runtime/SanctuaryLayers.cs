// Stratum layer editing for Sanctuary maps: swap a layer's texture for one of
// the game's, change its tint (diffuseRemap). Changes go straight into the
// open .sanmap's stratumLayers, which the saver writes as they stand.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryLayers
	{
		/// The game's stratum albedos, "Environment/<biome>/Stratum/<name>_albedo",
		/// grouped by biome folder.
		public static SortedDictionary<string, List<string>> Albedos()
		{
			var by = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var stem in SanGamedata.Stems())
			{
				if (!stem.EndsWith("_albedo", StringComparison.OrdinalIgnoreCase)) continue;
				var parts = stem.Split('/');
				if (parts.Length < 4 || !parts[0].Equals("Environment", StringComparison.OrdinalIgnoreCase)
					|| !parts[2].Equals("Stratum", StringComparison.OrdinalIgnoreCase)) continue;
				if (!by.TryGetValue(parts[1], out var list)) by[parts[1]] = list = new List<string>();
				list.Add(stem);
			}
			foreach (var l in by.Values) l.Sort(StringComparer.OrdinalIgnoreCase);
			return by;
		}

		static Dictionary<string, Color> knownTints;

		/// The tint the shipped maps most often give a texture: read from the
		/// stratumLayers of every map in the game's Maps folder (the first
		/// 200 KB of each file holds them; the scan takes ~50 ms).
		public static Color? KnownTint(string albedoStem)
		{
			if (knownTints == null)
			{
				knownTints = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
				var votes = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
				var re = new Regex("\"albedo\":\\s*\\{\\s*\"path\":\\s*\"([^\"]*)\"[\\s\\S]*?\"diffuseRemap\":\\s*\\{\\s*\"r\":\\s*([-\\d.eE]+),\\s*\"g\":\\s*([-\\d.eE]+),\\s*\"b\":\\s*([-\\d.eE]+)");
				string maps = SanctuaryPaths.MapsDir;
				if (maps != null && Directory.Exists(maps))
					foreach (var dir in Directory.GetDirectories(maps))
					{
						var f = Path.Combine(dir, Path.GetFileName(dir) + SanMapDocument.Extension);
						if (!File.Exists(f)) continue;
						string head;
						using (var s = File.OpenRead(f))
						{
							var buf = new byte[200000];
							int n = s.Read(buf, 0, buf.Length);
							head = System.Text.Encoding.UTF8.GetString(buf, 0, n);
						}
						int at = head.IndexOf("\"stratumLayers\"", StringComparison.Ordinal);
						if (at < 0) continue;
						var m = re.Match(head, at);
						for (int k = 0; m.Success && k < SanMapDocument.StratumCount; k++, m = m.NextMatch())
						{
							string stem = StemOf(m.Groups[1].Value);
							if (stem.Length == 0 || stem.StartsWith("map/", StringComparison.OrdinalIgnoreCase)) continue;
							string key = $"{m.Groups[2].Value}|{m.Groups[3].Value}|{m.Groups[4].Value}";
							if (!votes.TryGetValue(stem, out var v)) votes[stem] = v = new Dictionary<string, int>();
							v[key] = v.TryGetValue(key, out int c) ? c + 1 : 1;
						}
					}
				foreach (var kv in votes)
				{
					var best = kv.Value.OrderByDescending(x => x.Value).First().Key.Split('|');
					float P(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
					knownTints[kv.Key] = new Color(P(best[0]), P(best[1]), P(best[2]), 1);
				}
			}
			return knownTints.TryGetValue(StemOf(albedoStem), out var col) ? col : (Color?)null;
		}

		public static string StemOf(string path) =>
			string.IsNullOrEmpty(path) ? "" : path.Substring(0, path.Length - Path.GetExtension(path).Length);

		static string Sibling(string albedoStem, params string[] suffixes)
		{
			string baseStem = albedoStem.Substring(0, albedoStem.Length - "_albedo".Length);
			foreach (var s in suffixes)
				if (SanGamedata.Exists(baseStem + s)) return baseStem + s + ".dds";
			return null;
		}

		/// Gives layer `i` (0 = base, 1-8) the game texture `albedoStem`, with
		/// its normal and mask siblings when the pack has them, and the tint
		/// the shipped maps use for it (else the layer keeps its tint).
		public static void SetTexture(int i, string albedoStem)
		{
			var ed = ScmapEditor.Current;
			var layer = Layer(i);
			if (layer == null) return;
			SetPath(layer, "albedo", albedoStem + ".dds");
			string normal = Sibling(albedoStem, "_normal", "_normals");
			if (normal != null) SetPath(layer, "normal", normal);
			string mask = Sibling(albedoStem, "_mask", "_masks");
			if (mask != null) SetPath(layer, "mask", mask);
			var tint = KnownTint(albedoStem);
			if (tint.HasValue) SetTintJson(layer, tint.Value);

			ed.Textures[i].AlbedoPath = albedoStem + ".dds";
			ed.Textures[i].Albedo = SanGamedata.LoadTexture(albedoStem + ".dds", false) ?? Texture2D.grayTexture;
			if (normal != null)
			{
				ed.Textures[i].NormalPath = normal;
				ed.Textures[i].Normal = SanGamedata.LoadTexture(normal, true) ?? GetGamedataFile.emptyNormalTexture;
			}
			RefreshLayer(i);
			Debug.Log($"Sanctuary layer {i}: {albedoStem} (normal {normal ?? "kept"}, mask {mask ?? "kept"}, tint {(tint.HasValue ? tint.Value.ToString() : "kept")})");
		}

		public static Color GetTint(int i)
		{
			var r = Layer(i)?["diffuseRemap"] as JObject;
			return r == null ? Color.gray : new Color(r.Value<float>("r"), r.Value<float>("g"), r.Value<float>("b"), r.Value<float?>("a") ?? 1);
		}

		public static void SetTint(int i, Color c)
		{
			var layer = Layer(i);
			if (layer == null) return;
			SetTintJson(layer, c);
			// Rebake from the raw texture, not the already-baked display copy.
			var ed = ScmapEditor.Current;
			ed.Textures[i].Albedo = SanGamedata.LoadTexture(ed.Textures[i].AlbedoPath, false) ?? ed.Textures[i].Albedo;
			RefreshLayer(i);
		}

		static void RefreshLayer(int i)
		{
			var ed = ScmapEditor.Current;
			var tex = ed.Textures[i].Albedo;
			if (tex != null && tex.mipmapCount <= 1 && tex.width > 4) ed.Textures[i].Albedo = GetGamedataFile.ConvertWithMipmaps(tex);
			SanctuaryMapLoader.BakeLayer(i);
			ed.map.Layers[i].PathTexture = ed.Textures[i].AlbedoPath;
			ed.map.Layers[i].PathNormalmap = ed.Textures[i].NormalPath;
			ed.SetTextures(i);
		}

		static JObject Layer(int i)
		{
			var layers = SanctuaryMapLoader.Folder?.Map.StratumLayers;
			return layers != null && i >= 0 && i < layers.Count ? layers[i] as JObject : null;
		}

		static void SetPath(JObject layer, string slot, string path)
		{
			if (!(layer[slot] is JObject o)) layer[slot] = o = new JObject();
			o["path"] = path;
		}

		static void SetTintJson(JObject layer, Color c)
		{
			var old = layer["diffuseRemap"] as JObject;
			double a = old?.Value<double?>("a") ?? 1.0;
			layer["diffuseRemap"] = new JObject { ["r"] = Math.Round(c.r, 4), ["g"] = Math.Round(c.g, 4), ["b"] = Math.Round(c.b, 4), ["a"] = a };
		}
	}

	/// Texture picker window (IMGUI, built at runtime: no scene changes).
	public class SanctuaryTexturePicker : MonoBehaviour
	{
		static SanctuaryTexturePicker current;
		int layer;
		Rect window = new Rect(340, 80, 560, 640);
		Vector2 scroll;
		string biome;
		SortedDictionary<string, List<string>> albedos;
		readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
		Color tint;

		public static void Open(int layer)
		{
			if (!SanctuaryMapLoader.IsActive || layer < 0 || layer >= SanMapDocument.StratumCount) return;
			if (current == null) current = new GameObject("SanctuaryTexturePicker").AddComponent<SanctuaryTexturePicker>();
			current.layer = layer;
			current.albedos = SanctuaryLayers.Albedos();
			current.tint = SanctuaryLayers.GetTint(layer);
			if (current.biome == null || !current.albedos.ContainsKey(current.biome)) current.biome = current.albedos.Keys.FirstOrDefault();
			current.enabled = true;
		}

		bool overWindow;

		/// The editor learns the mouse is over its 3D view from UI pointer
		/// events, which IMGUI windows do not take part in: without this a
		/// click in the picker would also paint the terrain underneath.
		void Update()
		{
			var mouse = Input.mousePosition;
			bool inside = window.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
			var edit = MapLuaParser.Current?.EditMenu;
			if (edit == null) return;
			if (inside) { edit.MauseOnGameplay = false; overWindow = true; }
			else if (overWindow)
			{
				overWindow = false;
				var es = UnityEngine.EventSystems.EventSystem.current;
				edit.MauseOnGameplay = es == null || !es.IsPointerOverGameObject();
			}
		}

		void OnDisable()
		{
			if (overWindow && MapLuaParser.Current?.EditMenu != null) MapLuaParser.Current.EditMenu.MauseOnGameplay = true;
			overWindow = false;
		}

		void OnGUI()
		{
			GUI.depth = -100;
			window = GUI.Window(0x5A9C, window, Draw, $"Layer {(layer == 0 ? "Lower (base)" : layer.ToString())} texture");
		}

		void Draw(int id)
		{
			GUILayout.BeginHorizontal();
			foreach (var b in albedos.Keys)
				if (GUILayout.Toggle(b == biome, b.Replace('_', ' '), "Button")) biome = b;
			GUILayout.EndHorizontal();

			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(440));
			if (biome != null && albedos.TryGetValue(biome, out var list))
			{
				const int perRow = 4;
				for (int r = 0; r < list.Count; r += perRow)
				{
					GUILayout.BeginHorizontal();
					for (int c = r; c < Math.Min(r + perRow, list.Count); c++)
					{
						string stem = list[c];
						GUILayout.BeginVertical(GUILayout.Width(126));
						if (GUILayout.Button(Thumb(stem), GUILayout.Width(120), GUILayout.Height(120)))
						{
							SanctuaryLayers.SetTexture(layer, stem);
							tint = SanctuaryLayers.GetTint(layer);
						}
						GUILayout.Label(System.IO.Path.GetFileName(stem).Replace("_albedo", ""), GUILayout.Width(120));
						GUILayout.EndVertical();
					}
					GUILayout.EndHorizontal();
				}
			}
			GUILayout.EndScrollView();

			GUILayout.Label("Tint (diffuseRemap, linear): R " + tint.r.ToString("0.00") + "  G " + tint.g.ToString("0.00") + "  B " + tint.b.ToString("0.00"));
			tint.r = GUILayout.HorizontalSlider(tint.r, 0f, 1.5f);
			tint.g = GUILayout.HorizontalSlider(tint.g, 0f, 1.5f);
			tint.b = GUILayout.HorizontalSlider(tint.b, 0f, 1.5f);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Apply tint")) SanctuaryLayers.SetTint(layer, tint);
			if (GUILayout.Button("Close")) enabled = false;
			GUILayout.EndHorizontal();
			GUI.DragWindow();
		}

		/// A 120 px preview, cut from the texture's small mip so 60 textures
		/// don't decode at full size.
		Texture2D Thumb(string stem)
		{
			if (thumbs.TryGetValue(stem, out var t)) return t;
			var full = SanGamedata.LoadTexture(stem + ".dds", false, cache: false);
			Texture2D small = null;
			if (full != null)
			{
				var rt = RenderTexture.GetTemporary(120, 120, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
				Graphics.Blit(full, rt);
				var prev = RenderTexture.active;
				RenderTexture.active = rt;
				small = new Texture2D(120, 120, TextureFormat.RGB24, false);
				small.ReadPixels(new Rect(0, 0, 120, 120), 0, 0);
				small.Apply();
				RenderTexture.active = prev;
				RenderTexture.ReleaseTemporary(rt);
			}
			if (full != null) Destroy(full);
			thumbs[stem] = small;
			return small;
		}
	}
}
