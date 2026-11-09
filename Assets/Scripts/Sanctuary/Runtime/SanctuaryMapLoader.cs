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
