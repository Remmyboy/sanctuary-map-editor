// Writes the editor's state back into the open Sanctuary map.
//
// Only what the editor edits is rewritten; everything else in the .sanmap
// (lighting, fog, sky, props, decals, unit groups) passes through untouched.
// Values the editor did not change keep their original bytes: unedited
// heightmap cells keep their 16-bit value, unpainted mask pixels their bytes,
// and unmoved markers their original doubles - float32 rounding never
// creeps into a file just because it was opened and saved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapLua;
using Newtonsoft.Json.Linq;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryMapSaver
	{
		public sealed class Report
		{
			public int HeightCellsChanged;
			public int MarkersWritten;
			public List<string> SkippedMarkers = new List<string>();
			public string BackupDir;
			public string SavedTo;
			public string Preview;
			public List<string> Problems = new List<string>();
			public override string ToString() =>
				$"saved {SavedTo}: {HeightCellsChanged} heightmap cells changed, {MarkersWritten} markers" +
				(SkippedMarkers.Count > 0 ? $", {SkippedMarkers.Count} FA-only markers not saved ({string.Join(", ", SkippedMarkers.Take(5))})" : "") +
				(BackupDir != null ? $"; backup in {BackupDir}" : "") +
				(Preview != null ? "; preview.png updated" : "");
		}

		public static Report LastReport { get; private set; }

		/// Saves into the map's own folder, or into `targetDir` (a new map
		/// folder; the folder name becomes the map's file name).
		public static IEnumerator Save(string targetDir = null)
		{
			var parser = MapLuaParser.Current;
			var folder = SanctuaryMapLoader.Folder;
			if (folder == null) yield break;

			MapLuaParser.SavingMapProcess = true;
			parser.InfoPopup.Show(true, "Saving Sanctuary map...");
			yield return null;
			while (Markers.MarkersControler.IsUpdating) yield return null;

			var report = new Report();
			try
			{
				bool inPlace = targetDir == null ||
					string.Equals(Path.GetFullPath(targetDir).TrimEnd('\\', '/'), folder.Directory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
				if (inPlace) report.BackupDir = Backup(folder);

				WriteHeights(folder, report);
				WriteMasks(folder);
				WriteScenario(folder);
				WriteMarkers(folder, report);
				WriteArea(folder);
				WriteWater(folder);

				if (!inPlace) RenameForNewFolder(folder, targetDir);
				folder.Save(inPlace ? null : targetDir);
				if (!inPlace) FollowNewFolder(folder);
				report.SavedTo = folder.Directory;
				report.Problems = folder.Validate();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				report.Problems.Add("save failed: " + e.Message);
			}

			// The lobby's picture. Rendered after the files are written, so
			// a render problem can never cost the save.
			if (report.SavedTo != null)
			{
				for (int f = 0; f < 3; f++) yield return new WaitForEndOfFrame();
				try { report.Preview = SanctuaryPreview.Write(folder); }
				catch (Exception e) { Debug.LogException(e); report.Problems.Add("preview.png not updated: " + e.Message); }
			}

			LastReport = report;
			MapLuaParser.SavingMapProcess = false;
			parser.InfoPopup.Show(false);
			string msg = "Sanctuary " + report;
			if (report.Problems.Count > 0) msg += "\n" + string.Join("\n", report.Problems);
			Debug.Log(msg);
			GenericInfoPopup.ShowInfo(msg);
		}

		// ---- save as -----------------------------------------------------

		/// A copy saved under a new folder would otherwise show in the lobby
		/// with the original's name. If the name was not edited, take it from
		/// the new folder ("My_Map" -> "My Map").
		static void RenameForNewFolder(SanMapFolder folder, string targetDir)
		{
			var sc = MapLuaParser.Current.ScenarioLuaFile.Data;
			string oldName = folder.Map.Name;
			if (!string.IsNullOrEmpty(sc.name) && sc.name != oldName) return; // edited by hand: WriteScenario keeps it
			string fresh = Path.GetFileName(Path.GetFullPath(targetDir).TrimEnd('\\', '/')).Replace('_', ' ');
			folder.Map.Name = fresh;
			sc.name = fresh;
		}

		/// After Save As the editor works on the new copy.
		static void FollowNewFolder(SanMapFolder folder)
		{
			var parser = MapLuaParser.Current;
			parser.FolderName = folder.FolderName;
			parser.ScenarioFileName = folder.FolderName;
			parser.FolderParentPath = Path.GetDirectoryName(folder.Directory).Replace('\\', '/') + "/";
			WindowStateSever.WindowStateSaver.ChangeWindowName(folder.FolderName);
		}

		/// Folder names become map keys: no spaces, nothing the file system
		/// or the game's path handling would trip on.
		public static string SafeFolderName(string name)
		{
			var chars = name.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray();
			string s = new string(chars);
			while (s.Contains("__")) s = s.Replace("__", "_");
			return s.Trim('_');
		}

		// ---- backup ------------------------------------------------------

		/// Copies the files an in-place save rewrites to
		/// %LOCALAPPDATA%\SanctuaryMapEditor\Backups\<map>\<time>\.
		static string Backup(SanMapFolder folder)
		{
			string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"SanctuaryMapEditor", "Backups", folder.FolderName, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
			Directory.CreateDirectory(Path.Combine(root, SanMapFolder.TexturesDir));
			File.Copy(folder.SanmapPath, Path.Combine(root, Path.GetFileName(folder.SanmapPath)), true);
			foreach (var n in new[] { "heightmap.raw", "stratums_1_4.tga", "stratums_5_8.tga" })
			{
				string src = Path.Combine(folder.TexturesPath, n);
				if (File.Exists(src)) File.Copy(src, Path.Combine(root, SanMapFolder.TexturesDir, n), true);
			}
			return root;
		}

		// ---- terrain -----------------------------------------------------

		static void WriteHeights(SanMapFolder folder, Report report)
		{
			var data = ScmapEditor.Current.Teren.terrainData;
			int res = data.heightmapResolution;
			var hm = folder.Heightmap;
			if (hm.Resolution != res) throw new InvalidOperationException($"terrain is {res}, heightmap {hm.Resolution}");
			var now = data.GetHeights(0, 0, res, res);
			var then = SanctuaryMapLoader.TerrainAtLoad;
			int changed = 0;
			for (int y = 0; y < res; y++)
				for (int x = 0; x < res; x++)
				{
					float v = now[y, x];
					if (then != null && then[y, x] == v) continue;
					// Same rounding as the game's writer (Mathf.RoundToInt).
					int raw = Mathf.Clamp(Mathf.RoundToInt(v * SanHeightmap.RawScale), 0, 65535);
					if (hm.GetRaw(x, y) != raw) { hm.SetRaw(x, y, (ushort)raw); changed++; }
				}
			report.HeightCellsChanged = changed;
			SanctuaryMapLoader.TerrainAtLoad = now;
		}

		static void WriteMasks(SanMapFolder folder)
		{
			var map = ScmapEditor.Current.map;
			SanGamedata.TextureToTga(map.TexturemapTex, folder.GetTexture("stratums_1_4"), northUp: true);
			SanGamedata.TextureToTga(map.TexturemapTex2, folder.GetTexture("stratums_5_8"), northUp: true);
		}

		// ---- scenario ----------------------------------------------------

		static void WriteScenario(SanMapFolder folder)
		{
			var doc = folder.Map;
			var sc = MapLuaParser.Current.ScenarioLuaFile.Data;
			if (!string.IsNullOrEmpty(sc.name) && sc.name != doc.Name) doc.Name = sc.name;
			if (sc.description != null && sc.description != (doc.Credits ?? "")) doc.Credits = sc.description;

			// Armies in the editor's order; existing entries keep their fields.
			var names = new List<string>();
			foreach (var conf in sc.Configurations.Take(1))
			{
				foreach (var team in conf.Teams) names.AddRange(team.Armys.Select(a => a.Name));
				names.AddRange(conf.ExtraArmys.Select(a => a.Name));
			}
			var old = doc.Json["armies"] as JObject ?? new JObject();
			var armies = new JObject();
			foreach (var n in names.Distinct())
				armies[n] = old[n] ?? new JObject { ["faction"] = 0, ["alloys"] = 500.0, ["energy"] = 500.0, ["groups"] = new JObject() };
			if (!JToken.DeepEquals(armies, old)) doc.Json["armies"] = armies;
		}

		// ---- markers -----------------------------------------------------

		static void WriteMarkers(SanMapFolder folder, Report report)
		{
			var doc = folder.Map;
			int size = SanctuaryMapLoader.Size;
			string resourceType = doc.MarkerTypes.FirstOrDefault(doc.IsResourceMarkerType) ?? "Alloys";

			// Original positions, to keep exact doubles for markers not moved.
			var original = new Dictionary<string, SanTransform>();
			foreach (var t in doc.MarkerTypes)
				foreach (var kv in doc.GetMarkers(t))
					original[t + "\n" + kv.Key] = kv.Value;

			var byType = new Dictionary<string, List<KeyValuePair<string, SanTransform>>>();
			foreach (var t in doc.MarkerTypes) byType[t] = new List<KeyValuePair<string, SanTransform>>();

			foreach (var mk in MapLuaParser.Current.SaveLuaFile.Data.MasterChains[0].Markers)
			{
				string type;
				if (!SanctuaryMapLoader.MarkerSourceType.TryGetValue(mk.Name, out type))
				{
					if (mk.MarkerType == SaveLua.Marker.MarkerTypes.Mass || mk.MarkerType == SaveLua.Marker.MarkerTypes.Hydrocarbon) type = resourceType;
					else if (mk.MarkerType == SaveLua.Marker.MarkerTypes.BlankMarker) type = "Spawn";
					else { report.SkippedMarkers.Add(mk.Name); continue; }
				}
				if (!byType.ContainsKey(type)) byType[type] = new List<KeyValuePair<string, SanTransform>>();

				SanTransform t;
				if (original.TryGetValue(type + "\n" + mk.Name, out var o) &&
					(float)o.Position.X == mk.position.x && (float)o.Position.Y == mk.position.y && (float)(size - o.Position.Z) == mk.position.z)
					t = o;
				else
					t = new SanTransform
					{
						// Per axis: an axis the move did not touch keeps its original double.
						Position = new SanVec3(
							o != null && (float)o.Position.X == mk.position.x ? o.Position.X : mk.position.x,
							o != null && (float)o.Position.Y == mk.position.y ? o.Position.Y : mk.position.y,
							o != null && (float)(size - o.Position.Z) == mk.position.z ? o.Position.Z : size - (double)mk.position.z),
						Rotation = o?.Rotation ?? SanQuat.Identity,
						Scale = o?.Scale ?? new SanVec3(1, 1, 1),
					};
				byType[type].Add(new KeyValuePair<string, SanTransform>(mk.Name, t));
				report.MarkersWritten++;
			}

			foreach (var kv in byType)
			{
				bool resource = doc.MarkerTypes.Contains(kv.Key) ? doc.IsResourceMarkerType(kv.Key) : kv.Key == resourceType;
				var before = doc.Json["markers"]?[kv.Key]?.DeepClone();
				doc.SetMarkers(kv.Key, resource, kv.Value);
				// SetMarkers rebuilds the objects; if nothing changed, restore
				// the original token so key order and number formatting stay.
				if (before != null && JToken.DeepEquals(before, doc.Json["markers"][kv.Key]))
					doc.Json["markers"][kv.Key] = before;
			}
		}

		// ---- area and water ----------------------------------------------

		static void WriteArea(SanMapFolder folder)
		{
			var doc = folder.Map;
			var areas = MapLuaParser.Current.SaveLuaFile.Data.areas;
			if (areas == null || areas.Length == 0) return;
			// The editor keeps FA corner pairs (x0, y0, x1, y1), y from the north.
			Rect r = areas[0].rectangle;
			int size = SanctuaryMapLoader.Size;
			var old = doc.PlayableArea;
			if (old != null &&
				(float)old.X == r.x && (float)(size - (old.Y + old.Height)) == r.y &&
				(float)(old.X + old.Width) == r.width && (float)(size - old.Y) == r.height)
				return;
			doc.PlayableArea = new SanArea { X = r.x, Y = size - (double)r.height, Width = r.width - r.x, Height = r.height - r.y };
		}

		static void WriteWater(SanMapFolder folder)
		{
			var doc = folder.Map;
			var w = ScmapEditor.Current.map.Water;
			if (doc.HasWater != w.HasWater) doc.HasWater = w.HasWater;
			if ((float)doc.WaterLevel != w.Elevation) doc.WaterLevel = w.Elevation;
		}
	}
}
