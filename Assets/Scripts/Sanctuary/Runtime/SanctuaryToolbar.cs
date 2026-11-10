// A strip of Sanctuary tools beside the Props button, each from the game's
// own (unshipped) map editor:
//   Pathability     the game's 30-degree rule as a red wash (MapUtils)
//   Lighting/water  the fields MapEditorLighting / MapEditorWater edit
//   Tint            paints tint_colors (MapEditorToolbox.TintBrushTask)
//   Masks           a layer's mask from or to a PNG (MapEditorTextures.ImportMask)
//   Decals          lists, adds and erases decals, shown as rings
// IMGUI, like the props panel: the editor's own UI is a prefab scene.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SFB;
using UndoHistory;
using UnityEngine;

namespace Sanctuary
{
	public class SanctuaryToolbar : MonoBehaviour
	{
		static SanctuaryToolbar current;
		enum Win { None, Lighting, Tint, Masks, Decals }
		Win open;
		Rect window = new Rect(Screen.width - 400, 34, 380, 560);
		static Rect Bar => new Rect(Screen.width / 2f + 80, 3, 330, 22);
		bool overUi;

		public static void Ensure()
		{
			if (current == null) current = new GameObject("SanctuaryToolbar").AddComponent<SanctuaryToolbar>();
			current.fieldText.Clear();
			current.tintDirtyAtLoad = false;
			if (SanctuaryPathability.Shown) SanctuaryPathability.Show(true);
		}

		bool tintDirtyAtLoad;

		/// For screenshots: opens a window by name (Lighting, Tint, Masks, Decals).
		internal static void OpenWindow(string name)
		{
			if (current != null && Enum.TryParse(name, true, out Win w)) current.open = w;
		}

		void OnGUI()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			GUILayout.BeginArea(Bar);
			GUILayout.BeginHorizontal();
			// Narrow enough to clear the cursor readout at the top right.
			bool path = GUILayout.Toggle(SanctuaryPathability.Shown, "Pathing", "Button", GUILayout.Width(60));
			if (path != SanctuaryPathability.Shown) SanctuaryPathability.Show(path);
			Tab(Win.Lighting, "Light/water", 80);
			Tab(Win.Tint, "Tint" + (SanctuaryTerrainLook.TintDirty ? "*" : ""), 44);
			Tab(Win.Masks, "Masks", 50);
			Tab(Win.Decals, $"Decals {SanctuaryDecals.Count}", 74);
			GUILayout.EndHorizontal();
			GUILayout.EndArea();

			if (open != Win.None)
			{
				window.x = Mathf.Clamp(window.x, 0, Screen.width - window.width);
				window.y = Mathf.Clamp(window.y, 0, Screen.height - 40);
				window = GUI.Window(0x5A9E, window, Draw, open == Win.Lighting ? "Lighting and water" : open.ToString());
			}
		}

		void Tab(Win w, string label, float width)
		{
			bool on = GUILayout.Toggle(open == w, label, "Button", GUILayout.Width(width));
			if (on && open != w) { open = w; scroll = Vector2.zero; }
			else if (!on && open == w) open = Win.None;
		}

		Vector2 scroll;

		void Draw(int id)
		{
			switch (open)
			{
				case Win.Lighting: DrawLighting(); break;
				case Win.Tint: DrawTint(); break;
				case Win.Masks: DrawMasks(); break;
				case Win.Decals: DrawDecals(); break;
			}
			GUI.DragWindow();
		}

		void Update()
		{
			var edit = MapLuaParser.Current?.EditMenu;
			if (edit == null || !SanctuaryMapLoader.IsActive) { HideRings(); brush?.Hide(); return; }
			if (brush == null) brush = new SanctuaryOutline("toolbar brush", new Color(0.4f, 1f, 0.6f, 0.9f));

			var m = Input.mousePosition;
			var gui = new Vector2(m.x, Screen.height - m.y);
			bool inside = Bar.Contains(gui) || (open != Win.None && window.Contains(gui));
			if (inside) { edit.MauseOnGameplay = false; overUi = true; }
			else if (overUi)
			{
				overUi = false;
				var es = UnityEngine.EventSystems.EventSystem.current;
				edit.MauseOnGameplay = es == null || !es.IsPointerOverGameObject();
			}

			// Pathability follows terrain edits: recomputed when a stroke ends.
			if (SanctuaryPathability.Shown && Input.GetMouseButtonUp(0) && !inside && edit.GetCategoryId() == 1) SanctuaryPathability.Recompute();

			UpdateRings();
			bool onMap = edit.MauseOnGameplay && !inside;
			if (open == Win.Tint && tintMode != 0) UpdateTintBrush(onMap);
			else if (open == Win.Decals && decalMode != 0) UpdateDecalBrush(onMap);
			else brush.Hide();
		}

		// ---- lighting and water -----------------------------------------

		static readonly (string key, string label, float min, float max)[] LightFields =
		{
			("sunRA", "Sun direction (degrees round)", -360, 360),
			("sunDA", "Sun height (degrees up)", 0, 90),
			("sunIntensity", "Sun intensity (lux)", 0, 1e6f),
			("sunTemperature", "Sun colour temperature (K)", 1000, 20000),
			("skylightIntensity", "Sky light intensity", 0, 1e6f),
			("skylightTemperature", "Sky light temperature (K)", 1000, 20000),
			("exposure", "Exposure", -20, 30),
			("exposureCompensation", "Exposure compensation", -20, 20),
			("skyboxExposure", "Skybox exposure", -20, 30),
			("skyboxRotation", "Skybox rotation (degrees)", -360, 360),
			("fogAttenuationDistance", "Fog attenuation distance", 0, 1e5f),
			("fogBaseHeight", "Fog base height", -1000, 10000),
			("fogMaximumHeight", "Fog maximum height", -1000, 10000),
			("fogMaximumDistance", "Fog maximum distance", 0, 1e5f),
			("fogAnisotropy", "Fog anisotropy", -1, 1),
		};

		static readonly (string key, string label, float min, float max)[] WaterFields =
		{
			("waterDepth", "Water depth (m, colour fade)", 0, 1000),
			("waterWindSpeed", "Water wind speed (0-1)", 0, 1),
			("waterWindDirection", "Water wind direction (degrees)", -360, 360),
			("waterShoreDepthOffset", "Shore depth offset", -100, 100),
			("waterShoreDepthStrength", "Shore depth strength", 0, 100),
			("waterShoreDistanceOffset", "Shore distance offset", -100, 100),
			("waterShoreDistanceStrength", "Shore distance strength", 0, 100),
		};

		readonly Dictionary<string, string> fieldText = new Dictionary<string, string>();
		string lastUndoKey; float lastUndoTime;

		void DrawLighting()
		{
			var doc = SanctuaryMapLoader.Folder.Map;
			scroll = GUILayout.BeginScrollView(scroll);
			GUILayout.Label("Sun and sky");
			foreach (var f in LightFields) Field(doc.Json, f.key, f.label, f.min, f.max, true);
			ColourField(doc.Json, "sunTint", "Sun tint");
			ColourField(doc.Json, "skylightTint", "Sky light tint");
			GUILayout.Space(8);
			GUILayout.Label("Water (level and on/off are on the Map tab)");
			foreach (var f in WaterFields) Field(doc.Json, f.key, f.label, f.min, f.max, false);
			GUILayout.EndScrollView();
			GUILayout.Label("The editor shows the sun's direction and colours; the rest\nis saved for the game, which draws its own lighting.");
		}

		void Field(JObject json, string key, string label, float min, float max, bool lighting)
		{
			if (json[key] == null) return;
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(220));
			if (!fieldText.TryGetValue(key, out var text)) text = ((double)json[key]).ToString("0.###", CultureInfo.InvariantCulture);
			string next = GUILayout.TextField(text, GUILayout.Width(110));
			GUILayout.EndHorizontal();
			fieldText[key] = next;
			if (next == text) return;
			if (!double.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return;
			v = Math.Max(min, Math.Min(max, v));
			if (key == "waterDepth") v = Math.Min(v, (double?)json["waterLevel"] ?? v);
			if (Math.Abs(v - (double)json[key]) < 1e-9) return;
			Changing(json, key);
			json[key] = v;
			if (lighting) SanctuaryMapLoader.RefreshLighting();
		}

		void ColourField(JObject json, string key, string label)
		{
			if (!(json[key] is JObject c)) return;
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(110));
			foreach (var ch in new[] { "r", "g", "b" })
			{
				string k = key + "." + ch;
				if (!fieldText.TryGetValue(k, out var text)) text = ((double)c[ch]).ToString("0.###", CultureInfo.InvariantCulture);
				string next = GUILayout.TextField(text, GUILayout.Width(70));
				fieldText[k] = next;
				if (next != text && double.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 0 && Math.Abs(v - (double)c[ch]) > 1e-9)
				{
					Changing(json, key);
					c[ch] = Math.Min(v, 10);
					SanctuaryMapLoader.RefreshLighting();
				}
			}
			GUILayout.EndHorizontal();
		}

		/// One undo step per field per burst of typing.
		void Changing(JObject json, string key)
		{
			if (key == lastUndoKey && Time.unscaledTime - lastUndoTime < 2f) { lastUndoTime = Time.unscaledTime; return; }
			lastUndoKey = key; lastUndoTime = Time.unscaledTime;
			var old = json[key].DeepClone();
			HistorySanctuaryState.Register(() =>
			{
				var was = old.DeepClone();
				return () =>
				{
					json[key] = was.DeepClone();
					fieldText.Remove(key);
					foreach (var ch in new[] { "r", "g", "b" }) fieldText.Remove(key + "." + ch);
					SanctuaryMapLoader.RefreshLighting();
				};
			});
		}

		// ---- tint brush ---------------------------------------------------

		int tintMode; // 0 off, 1 paint, 2 back to neutral
		float tintRadius = 40f, tintStrength = 0.3f;
		Color tintColour = new Color(0.5f, 0.5f, 0.5f);
		float nextStamp;
		SanctuaryOutline brush;

		void DrawTint()
		{
			if (SanctuaryTerrainLook.Tint == null) { GUILayout.Label("This map has no tint_colors.tga."); return; }
			GUILayout.Label("Colours the ground: 0.5 leaves it as it is, higher\nbrightens that channel, lower darkens it.");
			tintMode = GUILayout.Toolbar(tintMode, new[] { "Off", "Paint", "Back to neutral" });
			GUILayout.Label($"Brush radius {tintRadius:0} m   ([ and ] change it)");
			tintRadius = GUILayout.HorizontalSlider(tintRadius, 2f, 400f);
			GUILayout.Label($"Strength {tintStrength:0.00}");
			tintStrength = GUILayout.HorizontalSlider(tintStrength, 0.02f, 1f);
			GUILayout.Label($"Colour  R {tintColour.r:0.00}  G {tintColour.g:0.00}  B {tintColour.b:0.00}");
			tintColour.r = GUILayout.HorizontalSlider(tintColour.r, 0f, 1f);
			tintColour.g = GUILayout.HorizontalSlider(tintColour.g, 0f, 1f);
			tintColour.b = GUILayout.HorizontalSlider(tintColour.b, 0f, 1f);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Neutral")) tintColour = new Color(0.5f, 0.5f, 0.5f);
			if (GUILayout.Button("Darker")) tintColour = new Color(0.4f, 0.4f, 0.4f);
			if (GUILayout.Button("Lighter")) tintColour = new Color(0.6f, 0.6f, 0.6f);
			if (GUILayout.Button("Warmer")) tintColour = new Color(0.58f, 0.5f, 0.42f);
			if (GUILayout.Button("Cooler")) tintColour = new Color(0.44f, 0.5f, 0.58f);
			GUILayout.EndHorizontal();
			var swatch = GUILayoutUtility.GetRect(40, 18, GUILayout.ExpandWidth(true));
			var old = GUI.color; GUI.color = new Color(tintColour.r * 2 * 0.5f, tintColour.g * 2 * 0.5f, tintColour.b * 2 * 0.5f); GUI.DrawTexture(swatch, Texture2D.whiteTexture); GUI.color = old;
			GUILayout.Label("Click or drag on the map. Ctrl+Z undoes a stroke.");
		}

		void UpdateTintBrush(bool onMap)
		{
			if (!CameraControler.IsInputFieldFocused())
			{
				if (Input.GetKeyDown(KeyCode.LeftBracket)) tintRadius = Mathf.Max(2f, tintRadius / 1.25f);
				if (Input.GetKeyDown(KeyCode.RightBracket)) tintRadius = Mathf.Min(400f, tintRadius * 1.25f);
			}
			if (!onMap || !SanctuaryPropsPanel.MouseOnMap(out var at)) { brush.Hide(); return; }
			brush.Circle(at.x, at.y, tintRadius);
			if (!Input.GetMouseButton(0)) return;
			if (Input.GetMouseButtonDown(0)) { HistorySanctuaryState.Register(SanctuaryTerrainLook.SnapshotTint); nextStamp = 0; }
			if (Time.unscaledTime < nextStamp) return;
			nextStamp = Time.unscaledTime + 0.05f;
			var target = tintMode == 2 ? new Color(0.5f, 0.5f, 0.5f) : tintColour;
			SanctuaryTerrainLook.PaintTint(at.x, at.y, tintRadius, tintStrength, target);
		}

		// ---- layer masks ------------------------------------------------

		int maskLayer = 1;

		void DrawMasks()
		{
			GUILayout.Label("A texture layer's mask (where it shows) from or to a\ngreyscale PNG, north at the top, the map's size or\nresampled to it. White = the layer fully shown.");
			GUILayout.Label("Layer");
			maskLayer = 1 + GUILayout.Toolbar(maskLayer - 1, new[] { "1", "2", "3", "4", "5", "6", "7", "8" });
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Import PNG...")) ImportMask(maskLayer);
			if (GUILayout.Button("Export PNG...")) ExportMask(maskLayer);
			GUILayout.EndHorizontal();
			GUILayout.Space(6);
			GUILayout.Label("To change the order of layers, use the up and down\narrows on the Textures tab: the masks move with them.");
		}

		static Texture2D MaskTexture(int layer, out int channel)
		{
			var map = ScmapEditor.Current.map;
			channel = (layer - 1) % 4;
			return layer <= 4 ? map.TexturemapTex : map.TexturemapTex2;
		}

		void ImportMask(int layer)
		{
			var paths = StandaloneFileBrowser.OpenFilePanel("Import mask for layer " + layer, "", new[] { new ExtensionFilter("PNG", "png") }, false);
			if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0])) return;
			GenericInfoPopup.ShowInfo(ImportMaskFrom(layer, paths[0])
				? $"Layer {layer} mask imported from {Path.GetFileName(paths[0])}" : "Couldn't read that PNG.");
		}

		internal static bool ImportMaskFrom(int layer, string path)
		{
			var tex = MaskTexture(layer, out int ch);
			var img = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
			if (!img.LoadImage(File.ReadAllBytes(path))) { Destroy(img); return false; }
			HistorySanctuaryState.Register(() => SnapshotTexture(tex));
			int w = tex.width, h = tex.height;
			var px = tex.GetPixels32();
			for (int row = 0; row < h; row++)
				for (int x = 0; x < w; x++)
				{
					// Mask row 0 is north; the PNG's top (north) is its texture's last row.
					float v = img.width == w && img.height == h ? img.GetPixel(x, h - 1 - row).grayscale
						: img.GetPixelBilinear((x + 0.5f) / w, 1f - (row + 0.5f) / h).grayscale;
					var c = px[row * w + x];
					byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
					if (ch == 0) c.r = b; else if (ch == 1) c.g = b; else if (ch == 2) c.b = b; else c.a = b;
					px[row * w + x] = c;
				}
			tex.SetPixels32(px);
			tex.Apply(false);
			Destroy(img);
			return true;
		}

		void ExportMask(int layer)
		{
			var path = StandaloneFileBrowser.SaveFilePanel("Export mask of layer " + layer, "", $"layer{layer}_mask", new[] { new ExtensionFilter("PNG", "png") });
			if (string.IsNullOrEmpty(path)) return;
			ExportMaskTo(layer, path);
			GenericInfoPopup.ShowInfo($"Layer {layer} mask exported to {Path.GetFileName(path)}");
		}

		internal static void ExportMaskTo(int layer, string path)
		{
			var tex = MaskTexture(layer, out int ch);
			int w = tex.width, h = tex.height;
			var src = tex.GetPixels32();
			var outPx = new Color32[w * h];
			for (int row = 0; row < h; row++)
				for (int x = 0; x < w; x++)
				{
					var c = src[row * w + x];
					byte v = ch == 0 ? c.r : ch == 1 ? c.g : ch == 2 ? c.b : c.a;
					outPx[(h - 1 - row) * w + x] = new Color32(v, v, v, 255);
				}
			var img = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
			img.SetPixels32(outPx);
			img.Apply(false);
			File.WriteAllBytes(path, img.EncodeToPNG());
			Destroy(img);
		}

		static Action SnapshotTexture(Texture2D tex)
		{
			var copy = tex.GetPixels32();
			return () => { tex.SetPixels32(copy); tex.Apply(false); };
		}

		// ---- decals -------------------------------------------------------

		int decalMode; // 0 off, 1 add, 2 erase
		string decalType;
		float decalSize = 8f, decalEraseRadius = 6f;
		bool showRings = true, eraseOnlyType;
		string filter = "";
		List<string> catalog;
		readonly List<SanctuaryOutline> rings = new List<SanctuaryOutline>();
		int ringStamp = -1;

		void DrawDecals()
		{
			GUILayout.Label("The editor can't draw decals; each shows as a ring.\nAlloy-spot decals follow their alloy markers on save.");
			showRings = GUILayout.Toggle(showRings, "Show decal rings");
			foreach (var (type, count) in SanctuaryDecals.Types().ToList())
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Toggle(decalType == type, $"{Path.GetFileNameWithoutExtension(type)}  ({count})", "Button")) decalType = type;
				GUILayout.EndHorizontal();
			}
			decalMode = GUILayout.Toolbar(decalMode, new[] { "Off", "Add", "Erase" });
			if (decalMode == 1)
			{
				GUILayout.Label($"Size {decalSize:0.0} m");
				decalSize = GUILayout.HorizontalSlider(decalSize, 1f, 100f);
			}
			else if (decalMode == 2)
			{
				GUILayout.Label($"Erase radius {decalEraseRadius:0} m   ([ and ] change it)");
				decalEraseRadius = GUILayout.HorizontalSlider(decalEraseRadius, 1f, 200f);
				eraseOnlyType = GUILayout.Toggle(eraseOnlyType, "Only the type picked");
			}
			GUILayout.Label("Pick another decal type:");
			filter = GUILayout.TextField(filter);
			if (catalog == null) catalog = SanctuaryDecals.Catalog();
			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(170));
			foreach (var c in catalog)
			{
				if (filter.Length > 0 && c.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
				if (GUILayout.Toggle(string.Equals(c, decalType, StringComparison.OrdinalIgnoreCase), c.Replace("Environment/", ""), "Button")) decalType = c;
			}
			GUILayout.EndScrollView();
			GUILayout.Label(decalType == null ? "Pick a type to add." : "Picked: " + Path.GetFileNameWithoutExtension(decalType));
		}

		void UpdateDecalBrush(bool onMap)
		{
			if (!CameraControler.IsInputFieldFocused() && decalMode == 2)
			{
				if (Input.GetKeyDown(KeyCode.LeftBracket)) decalEraseRadius = Mathf.Max(1f, decalEraseRadius / 1.25f);
				if (Input.GetKeyDown(KeyCode.RightBracket)) decalEraseRadius = Mathf.Min(200f, decalEraseRadius * 1.25f);
			}
			if (!onMap || !SanctuaryPropsPanel.MouseOnMap(out var at)) { brush.Hide(); return; }
			brush.Circle(at.x, at.y, decalMode == 1 ? decalSize / 2f : decalEraseRadius);
			if (!Input.GetMouseButtonDown(0) && !(decalMode == 2 && Input.GetMouseButton(0))) return;
			if (decalMode == 1)
			{
				if (decalType == null) return;
				HistorySanctuaryState.Register(SanctuaryDecals.Snapshot);
				SanctuaryDecals.Add(decalType, at.x, at.y, decalSize, UnityEngine.Random.Range(0f, 360f));
			}
			else
			{
				if (Input.GetMouseButtonDown(0)) HistorySanctuaryState.Register(SanctuaryDecals.Snapshot);
				SanctuaryDecals.Erase(at.x, at.y, decalEraseRadius, eraseOnlyType ? decalType : null);
			}
			ringStamp = -1;
		}

		void UpdateRings()
		{
			bool want = showRings && open == Win.Decals;
			if (!want) { HideRings(); ringStamp = -1; return; }
			// Redrawn when the count changes, and twice a second for undo.
			int stamp = SanctuaryDecals.Count;
			if (stamp == ringStamp && Time.frameCount % 30 != 0) return;
			ringStamp = stamp;
			var all = SanctuaryDecals.All().Take(600).ToList();
			while (rings.Count < all.Count) rings.Add(new SanctuaryOutline("decal ring", new Color(1f, 0.55f, 0.9f, 0.9f)));
			for (int i = 0; i < rings.Count; i++)
			{
				if (i < all.Count) rings[i].Circle(all[i].x, all[i].z, Mathf.Max(1f, all[i].size / 2f));
				else rings[i].Hide();
			}
		}

		void HideRings() { foreach (var r in rings) r.Hide(); }
	}
}
