// Opens a Sanctuary map folder in the editor.
//
// The editor's state is Supreme Commander shaped (ScenarioLua, SaveLua markers,
// a HazardX Map). Rather than rewrite every tool, the loader fills that state
// from the .sanmap the way NewMap fills it for a blank map, and keeps the
// SanMapFolder alongside so saving can write back everything the editor does
// not model.
//
// Coordinates. Sanctuary world: x east, z north, origin at the south-west
// corner, metres. Editor: FA map coordinates (x east, z measured from the
// NORTH edge, metres), shown at 1/10 scale with world z = -mapZ/10. So
// faZ = length - sanZ, heights are metres in both, and the heightmap rows line
// up as they are (file row 0 is the north edge in both formats).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MapLua;
using Newtonsoft.Json.Linq;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryMapLoader
	{
		/// The open Sanctuary map, or null when the editor holds an FA map.
		public static SanMapFolder Folder { get; private set; }
		public static bool IsActive => Folder != null;

		/// Terrain extent in metres the editor uses (heightmap resolution - 1).
		public static int Size { get; private set; }

		/// Marker name -> whether it came from a resource type, for saving.
		public static readonly Dictionary<string, string> MarkerSourceType = new Dictionary<string, string>();

		public static void Clear()
		{
			Folder = null;
			MarkerSourceType.Clear();
		}

		public static bool IsSanmapPath(string path) =>
			!string.IsNullOrEmpty(path) &&
			(path.EndsWith(SanMapDocument.Extension, StringComparison.OrdinalIgnoreCase) ||
			 (Directory.Exists(path) && Directory.GetFiles(path, "*" + SanMapDocument.Extension).Length > 0));

		public static double SanZToFa(double z) => Size - z;

		// ---- load -------------------------------------------------------

		public static IEnumerator Load(string path)
		{
			var parser = MapLuaParser.Current;
			while (MapLuaParser.SavingMapProcess) yield return null;

			SanMapFolder folder;
			try { folder = SanMapFolder.Open(path); }
			catch (Exception e)
			{
				parser.ReturnLoadingWithError("Cannot open Sanctuary map:\n" + path + "\n" + e.Message);
				yield break;
			}
			var m = folder.Map;
			int res = m.HeightmapResolution;
			if (!SanHeightmap.IsValidResolution(res))
			{
				parser.ReturnLoadingWithError($"{folder.FolderName}: heightmap resolution {res} is not 2^n+1");
				yield break;
			}

			MapLuaParser.LoadingMapProcess = true;
			ScmapEditor.Current.UnloadMap();
			parser.ResetUI();
			parser.SaveLuaFile.Unload();
			Undo.Current.Clear();

			Clear();
			Folder = folder;
			Size = res - 1;

			parser.FolderName = folder.FolderName;
			parser.ScenarioFileName = folder.FolderName;
			parser.FolderParentPath = Path.GetDirectoryName(folder.Directory).Replace('\\', '/') + "/";

			parser.InfoPopup.Show(true, "Loading map...\n(" + folder.FolderName + ")");
			parser.EditMenu.gameObject.SetActive(true);
			parser.Background.SetActive(false);
			yield return null;

			// ---- scenario: name, size, armies --------------------------
			var sc = new ScenarioLua.ScenarioInfo();
			sc.Configurations = new ScenarioLua.Configuration[1];
			sc.Configurations[0] = new ScenarioLua.Configuration { name = "standard" };
			sc.Configurations[0].Teams = new ScenarioLua.Team[1];
			sc.Configurations[0].Teams[0] = new ScenarioLua.Team { name = "FFA", Armys = new List<ScenarioLua.Army>() };
			sc.Configurations[0].ExtraArmys = new List<ScenarioLua.Army>();
			sc.Configurations[0].factions = new ScenarioLua.Factions[0];
			sc.starts = true;
			sc.name = m.Name ?? folder.FolderName;
			sc.description = m.Credits ?? "";
			sc.type = "skirmish";
			sc.Size = new[] { Size, Size };
			sc.map = folder.SanmapPath.Replace('\\', '/');
			sc.save = sc.map;
			sc.script = sc.map;
			sc.map_version = m.MapVersion;
			foreach (var a in m.ArmyNames)
			{
				var list = a.StartsWith("NEUTRAL", StringComparison.OrdinalIgnoreCase)
					? sc.Configurations[0].ExtraArmys
					: sc.Configurations[0].Teams[0].Armys;
				list.Add(new ScenarioLua.Army(a));
			}
			parser.ScenarioLuaFile.Data = sc;

			parser.MapCenterPoint = new Vector3(Size / 20f, 0, -Size / 20f);
			CameraControler.Current.MapSize = Size;
			CameraControler.Current.RestartCam();

			// ---- terrain -----------------------------------------------
			parser.InfoPopup.Show(true, "Loading map...\n( heightmap " + res + " )");
			yield return null;
			SanHeightmap heights;
			try { heights = folder.Heightmap; }
			catch (Exception e)
			{
				Fail(parser, "heightmap: " + e.Message);
				yield break;
			}

			ReadCalibrationArgs();
			var map = new Map(Size, Size, 0, m.HasWater, 0, 0, 0);
			map.TerrainShader = "TTerrainXP";
			map.Water.HasWater = m.HasWater;
			map.Water.Elevation = (float)m.WaterLevel;
			map.Water.ElevationDeep = (float)(m.WaterLevel - m.WaterDepth);
			map.Water.ElevationAbyss = (float)(m.WaterLevel - 2 * m.WaterDepth);
			// Depth tint for the FA water shader: lerp toward this colour by
			// depth, clamped to ColorLerp.
			map.Water.SurfaceColor = new Vector3(0.07f, 0.2f, 0.26f);
			map.Water.ColorLerp = new Vector2(0.25f, 0.85f);
			ApplyLighting(m, map);
			ScmapEditor.Current.map = map;

			parser.InfoPopup.Show(true, "Loading map...\n( textures )");
			yield return null;
			ScmapEditor.Current.ApplyLoadedMap();
			CameraControler.Current.RestartCam();
			yield return null;
			var ter = ScmapEditor.Current.Teren;
			Debug.Log($"Sanctuary terrain: pos {ter.transform.position} size {ter.terrainData.size} res {ter.terrainData.heightmapResolution}; camera {Camera.main?.transform.position} looking {Camera.main?.transform.forward}");

			// ---- markers and playable area ------------------------------
			parser.SaveLuaFile.Data = new SaveLua.Scenario();
			var markers = parser.SaveLuaFile.Data.MasterChains[0].Markers = new List<SaveLua.Marker>();
			var armySet = new HashSet<string>(m.ArmyNames, StringComparer.OrdinalIgnoreCase);
			foreach (var type in m.MarkerTypes)
			{
				bool resource = m.IsResourceMarkerType(type);
				foreach (var kv in m.GetMarkers(type))
				{
					SaveLua.Marker.MarkerTypes mt;
					if (resource) mt = SaveLua.Marker.MarkerTypes.Mass;
					else if (string.Equals(type, "Spawn", StringComparison.OrdinalIgnoreCase)) mt = SaveLua.Marker.MarkerTypes.BlankMarker;
					else mt = SaveLua.Marker.MarkerTypes.BlankMarker;

					// Spawn names follow their army's spelling so the editor
					// pairs them.
					string name = kv.Key;
					if (mt == SaveLua.Marker.MarkerTypes.BlankMarker)
						foreach (var a in m.ArmyNames)
							if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) { name = a; break; }

					var marker = new SaveLua.Marker(mt, name);
					var p = kv.Value.Position;
					marker.position = new Vector3((float)p.X, (float)p.Y, (float)SanZToFa(p.Z));
					markers.Add(marker);
					MarkerSourceType[marker.Name] = type;
				}
			}

			var area = m.PlayableArea;
			if (area != null)
			{
				parser.SaveLuaFile.Data.areas = new SaveLua.Areas[1];
				parser.SaveLuaFile.Data.areas[0] = new SaveLua.Areas
				{
					Name = SanMapDocument.PlayableAreaKey,
					// FA areas are corner pairs (x0, y0, x1, y1), y from the north edge.
					rectangle = new Rect((float)area.X, (float)SanZToFa(area.Y + area.Height),
						(float)(area.X + area.Width), (float)SanZToFa(area.Y)),
				};
			}
			else parser.SaveLuaFile.Data.areas = new SaveLua.Areas[0];

			Markers.MarkersControler.LoadMarkers();
			yield return null;

			// ---- done ---------------------------------------------------
			var problems = folder.Validate();
			parser.InfoPopup.Show(false);
			WindowStateSever.WindowStateSaver.ChangeWindowName(folder.FolderName);
			MapLuaParser.LoadingMapProcess = false;
			RenderMarkersConnections.Current.UpdateConnections();
			parser.EditMenu.Categorys[0].GetComponent<EditMap.MapInfo>().UpdateFields();
			parser.UpdateArea();
			LogMarkerFit();

			string msg = $"Sanctuary map loaded: {folder.FolderName}\n{m.ArmyNames.Count} armies, {markers.Count} markers, {Size} m, {m.Height} m tall";
			if (problems.Count > 0) msg += "\n" + string.Join("\n", problems);
			Debug.Log(msg);
			GenericInfoPopup.ShowInfo(msg);
			MapLuaParser.OnMapLoaded?.Invoke();
		}

		static void Fail(MapLuaParser parser, string error)
		{
			Clear();
			MapLuaParser.LoadingMapProcess = false;
			parser.InfoPopup.Show(false);
			parser.ResetUI();
			parser.ReturnLoadingWithError(error);
		}

		/// Sun direction from the map's azimuth (sunRA) and altitude (sunDA),
		/// both degrees. The editor's lighting model is FA's, so only the sun
		/// direction and a neutral colour carry over; full Sanctuary lighting
		/// is phase 4.
		static void ApplyLighting(SanMapDocument m, Map map)
		{
			float ra = (float)m.Json.Value<double?>("sunRA").GetValueOrDefault(0);
			float da = (float)m.Json.Value<double?>("sunDA").GetValueOrDefault(45);
			Vector3 toSun = -(Quaternion.Euler(da, ra, 0) * Vector3.forward);
			if (toSun.y < 0.1f) toSun.y = 0.1f;
			map.SunDirection = toSun.normalized;

			// Neutral light: FA's defaults are a warm sun with a 1.3x
			// multiplier, which turned Sanctuary greens yellow. Colour comes
			// from the baked diffuseRemap instead (BakeDiffuseRemap).
			// Sun and sky colours come from the map's own colour temperatures
			// (the game lights with physical temperatures; the blue sky light
			// is most of why its previews read cool).
			Color sun = Kelvin((float)m.Json.Value<double?>("sunTemperature").GetValueOrDefault(6500)) * Tint(m, "sunTint");
			Color sky = SkyColor(m);
			map.SunColor = new Vector3(sun.r, sun.g, sun.b) * SunLevel;
			map.SunAmbience = new Vector3(sky.r, sky.g, sky.b) * AmbientLevel;
			map.LightingMultiplier = 1f;
			map.ShadowFillColor = Vector3.zero;
			map.SpecularColor = Vector4.zero;
		}

		/// Display calibration, set by matching editor screenshots to the
		/// game's own preview.png (Tools~/compare-preview.ps1). Overridable
		/// on the command line: -remapgain, -ambient.
		public static float RemapGain = 2f;
		public static float AmbientLevel = 0.6f;
		public static float SunLevel = 0.8f;
		/// The game's height and distance fog give its overhead views a cool
		/// haze; the FA shader's fog uniforms are never set by the editor, so
		/// a fixed share of sky-coloured haze is baked into the displayed
		/// layers instead.
		public static float HazeLevel = 0.25f;
		public static float HazeBright = 0.7f;

		/// Ambient colour. Every shipped developer map has skylightIntensity
		/// 0 (White Desert's 1500 K sky would otherwise paint it orange): its
		/// ambient comes from the skybox, approximated as 10000 K daylight
		/// sky. A map that turns the sky light on gets its own colour.
		static Color SkyColor(SanMapDocument m)
		{
			double intensity = m.Json.Value<double?>("skylightIntensity").GetValueOrDefault(0);
			if (intensity <= 0) return Kelvin(10000f);
			return Kelvin((float)m.Json.Value<double?>("skylightTemperature").GetValueOrDefault(9000)) * Tint(m, "skylightTint");
		}

		static Color Tint(SanMapDocument m, string key)
		{
			var t = m.Json[key] as JObject;
			if (t == null) return Color.white;
			return new Color(t.Value<float?>("r") ?? 1, t.Value<float?>("g") ?? 1, t.Value<float?>("b") ?? 1, 1);
		}

		/// Black-body colour, normalised so the brightest channel is 1
		/// (Tanner Helland's fit, good to a few percent over 1000-40000 K).
		public static Color Kelvin(float k)
		{
			float t = Mathf.Clamp(k, 1000f, 40000f) / 100f;
			float r = t <= 66 ? 255 : 329.698727446f * Mathf.Pow(t - 60, -0.1332047592f);
			float g = t <= 66 ? 99.4708025861f * Mathf.Log(t) - 161.1195681661f : 288.1221695283f * Mathf.Pow(t - 60, -0.0755148492f);
			float b = t >= 66 ? 255 : t <= 19 ? 0 : 138.5177312231f * Mathf.Log(t - 10) - 305.0447927307f;
			var c = new Color(Mathf.Clamp(r, 0, 255) / 255f, Mathf.Clamp(g, 0, 255) / 255f, Mathf.Clamp(b, 0, 255) / 255f);
			float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
			return c / max;
		}

		static void ReadCalibrationArgs()
		{
			var a = Environment.GetCommandLineArgs();
			for (int i = 0; i < a.Length - 1; i++)
			{
				if (a[i] == "-remapgain") float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out RemapGain);
				if (a[i] == "-ambient") float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out AmbientLevel);
				if (a[i] == "-sunlevel") float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out SunLevel);
				if (a[i] == "-haze") float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out HazeLevel);
				if (a[i] == "-hazebright") float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out HazeBright);
			}
		}

		static Texture2D calmWave;

		/// Flat wave normal with zero alpha. The FA water shader turns the
		/// summed alpha of its four wave textures into white crests, so the
		/// usual flat normal (alpha 0.5) paints the whole sea white.
		public static Texture2D CalmWaveNormal()
		{
			if (calmWave != null) return calmWave;
			calmWave = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "SanctuaryCalmWave" };
			var px = new Color32[16];
			for (int i = 0; i < 16; i++) px[i] = new Color32(128, 128, 255, 0);
			calmWave.SetPixels32(px);
			calmWave.Apply();
			return calmWave;
		}

		static Texture2D waterRamp;

		/// Shore-to-deep colour ramp for the FA water shader (u = depth).
		/// Colours are picked to read like the game's sea in its previews;
		/// the real water shading is phase 4.
		public static Texture2D WaterRamp()
		{
			if (waterRamp != null) return waterRamp;
			var shallow = new Color(0.36f, 0.55f, 0.56f, 0.35f);
			var mid = new Color(0.17f, 0.36f, 0.42f, 0.75f);
			var deep = new Color(0.08f, 0.20f, 0.28f, 0.95f);
			waterRamp = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SanctuaryWaterRamp" };
			for (int x = 0; x < 256; x++)
			{
				float t = x / 255f;
				waterRamp.SetPixel(x, 0, t < 0.4f ? Color.Lerp(shallow, mid, t / 0.4f) : Color.Lerp(mid, deep, (t - 0.4f) / 0.6f));
			}
			waterRamp.Apply();
			return waterRamp;
		}

		// ---- stratum textures -------------------------------------------

		static Texture2D clear;

		/// Called by ScmapEditor.LoadStratumScdTextures in place of the FA
		/// gamedata path. Sanctuary has 9 layers (0 = base, 1-8 masked); the
		/// editor has 10 (9 = FA's "upper" macro layer), which stays empty.
		public static void ApplyStratumTextures()
		{
			var ed = ScmapEditor.Current;
			var m = Folder.Map;
			var map = ed.map;

			while (map.Layers.Count < ed.Textures.Length) map.Layers.Add(new Layer());

			for (int i = 0; i < ed.Textures.Length; i++)
			{
				var tex = ed.Textures[i];
				if (i >= SanMapDocument.StratumCount)
				{
					if (clear == null)
					{
						clear = new Texture2D(4, 4, TextureFormat.RGBA32, false);
						var px = new Color32[16];
						clear.SetPixels32(px);
						clear.Apply();
					}
					tex.Albedo = clear;
					tex.Normal = GetGamedataFile.emptyNormalTexture;
					tex.AlbedoPath = tex.NormalPath = "";
					tex.AlbedoScale = tex.NormalScale = 1000;
					map.Layers[i].PathTexture = map.Layers[i].PathNormalmap = "";
					continue;
				}

				string albedo = m.GetStratumTexture(i, "albedo");
				string normal = m.GetStratumTexture(i, "normal");
				var layer = m.StratumLayers[i];
				float tile = 10f;
				var ts = layer?["tileSize"];
				if (ts != null && ts["x"] != null) tile = Mathf.Max(0.5f, ts.Value<float>("x"));

				tex.AlbedoPath = albedo ?? "";
				tex.NormalPath = normal ?? "";
				tex.AlbedoScale = tile;
				tex.NormalScale = tile;
				tex.Albedo = SanGamedata.LoadTexture(albedo, false) ?? Texture2D.grayTexture;
				tex.Normal = SanGamedata.LoadTexture(normal, true) ?? GetGamedataFile.emptyNormalTexture;
				if (tex.Albedo.mipmapCount <= 1 && tex.Albedo.width > 4) tex.Albedo = GetGamedataFile.ConvertWithMipmaps(tex.Albedo);

				map.Layers[i].PathTexture = tex.AlbedoPath;
				map.Layers[i].PathNormalmap = tex.NormalPath;
				map.Layers[i].ScaleTexture = tile;
				map.Layers[i].ScaleNormalmap = tile;
			}

			BakeDiffuseRemap(ed, m);

			// Splat masks: stratums_1_4 RGBA = layers 1-4, stratums_5_8 = 5-8,
			// the same packing as FA's two texture maps.
			try
			{
				map.TexturemapTex = SanGamedata.TgaToTexture(Folder.GetTexture("stratums_1_4"), true, false);
				map.TexturemapTex2 = SanGamedata.TgaToTexture(Folder.GetTexture("stratums_5_8"), true, false);
				map.TexturemapTex.wrapMode = map.TexturemapTex2.wrapMode = TextureWrapMode.Clamp;
			}
			catch (Exception e)
			{
				Debug.LogError("Stratum masks: " + e.Message);
			}
		}

		/// The game draws each stratum as albedo x diffuseRemap: the remap
		/// carries most of a layer's colour (the Forge's sand texture is near
		/// white, its remap a dark tan). The FA shader has no such input, so
		/// the product is baked into the displayed texture. The game's
		/// exposure then brightens the whole result; here one gain per map
		/// keeps the mean brightness of the layers as it was, which keeps
		/// every layer's colour relative to the others. The textures written
		/// back on save are untouched: only the copies on screen change.
		static void BakeDiffuseRemap(ScmapEditor ed, SanMapDocument m)
		{
			int n = Mathf.Min(SanMapDocument.StratumCount, ed.Textures.Length);
			var remaps = new Color[n];
			var means = new Color[n];
			var use = new bool[n];
			double lumBefore = 0, lumAfter = 0;
			for (int i = 0; i < n; i++)
			{
				var tex = ed.Textures[i].Albedo;
				var r = m.StratumLayers?[i]?["diffuseRemap"] as JObject;
				if (tex == null || r == null || string.IsNullOrEmpty(m.GetStratumTexture(i, "albedo")) || !tex.isReadable) continue;
				remaps[i] = new Color(r.Value<float>("r"), r.Value<float>("g"), r.Value<float>("b"), 1);
				means[i] = MeanColor(tex);
				use[i] = true;
				lumBefore += Lum(means[i]);
				lumAfter += Lum(means[i] * remaps[i]);
			}
			if (lumAfter <= 0) return;
			// One gain for every map: normalising per map made light maps
			// (Two Step Shuffle) 1.7x too bright against the game's preview.
			float gain = RemapGain;
			Color haze = SkyColor(m) * HazeBright;
			float h = Mathf.Clamp01(HazeLevel), keep = 1f - h;

			for (int i = 0; i < n; i++)
			{
				if (!use[i]) continue;
				var src = ed.Textures[i].Albedo;
				Color k = remaps[i] * gain;
				var px = src.GetPixels(0);
				for (int p = 0; p < px.Length; p++)
				{
					var c = px[p];
					px[p] = new Color(
						Mathf.Clamp01(c.r * k.r * keep + haze.r * h),
						Mathf.Clamp01(c.g * k.g * keep + haze.g * h),
						Mathf.Clamp01(c.b * k.b * keep + haze.b * h), c.a);
				}
				var baked = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true, false)
				{
					name = src.name + " (remap)", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear,
				};
				baked.SetPixels(px, 0);
				baked.Apply(true);
				ed.Textures[i].Albedo = baked;
			}
			Debug.Log($"Sanctuary diffuseRemap baked into {System.Linq.Enumerable.Count(use, u => u)} layers, gain {gain:0.00}");
		}

		static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

		static Color MeanColor(Texture2D t)
		{
			int mip = 0;
			while (mip < t.mipmapCount - 1 && (t.width >> mip) > 64) mip++;
			var px = t.GetPixels(mip);
			Color s = Color.clear;
			foreach (var c in px) s += c;
			return s / Mathf.Max(1, px.Length);
		}

		/// Spawn markers should sit on the terrain; a large gap means the
		/// coordinate mapping is wrong. Logged so it can be checked from the
		/// player log without looking at the screen.
		static void LogMarkerFit()
		{
			var markers = MapLuaParser.Current.SaveLuaFile.Data.MasterChains[0].Markers;
			float worst = 0; string worstName = "";
			foreach (var mk in markers)
			{
				Vector3 world = ScmapEditor.ScmapPosToWorld(mk.position);
				float terrain = ScmapEditor.Current.Teren.SampleHeight(world) + ScmapEditor.Current.Teren.transform.position.y;
				float d = Mathf.Abs(terrain - world.y) * 10f;
				if (d > worst) { worst = d; worstName = mk.Name; }
			}
			Debug.Log($"Sanctuary marker fit: worst {worst:0.00} m ({worstName}) over {markers.Count} markers");
		}
	}
}
