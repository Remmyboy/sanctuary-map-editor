// A fixed, known edit for checking the save path end to end (-testedit on
// the command line). Tools~/SanFormatTests --diff then confirms exactly these
// changes reached the files and nothing else moved:
//   heightmap   a 40x40-cell square at the centre raised by 5 m
//   stratums    a 16x16-pixel square at the centre set to layer 1 only
//   markers     the first spawn moved 8 m east
using MapLua;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryTestEdit
	{
		public const int HeightBox = 40;
		public const float RaiseMetres = 5f;
		public const int MaskBox = 16;
		public const float MoveEastMetres = 8f;

		/// -testmarkers: a new spawn and a new alloy spot at the map centre
		/// (through the Markers tool's own constructor and naming), and the
		/// first existing alloy spot deleted.
		public static void ApplyMarkers()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			var list = MapLuaParser.Current.SaveLuaFile.Data.MasterChains[0].Markers;
			float c = SanctuaryMapLoader.Size / 2f;
			var spawn = new SaveLua.Marker(SaveLua.Marker.MarkerTypes.BlankMarker);
			spawn.position = OnGround(c, c);
			list.Add(spawn);
			var alloy = new SaveLua.Marker(SaveLua.Marker.MarkerTypes.Mass);
			alloy.position = OnGround(c + 20, c);
			list.Add(alloy);
			var gone = list.Find(m => m.MarkerType == SaveLua.Marker.MarkerTypes.Mass && m != alloy);
			if (gone != null) list.Remove(gone);
			Debug.Log($"Sanctuary test markers: added {spawn.Name}, {alloy.Name}; removed {gone?.Name}");
		}

		/// -testlayers: layer 2 gets Winter's dirty_ground (with its sibling
		/// normal/mask and the shipped maps' tint for it), layer 1's scale
		/// doubles, and the texture picker opens on layer 2 for a screenshot.
		public static void ApplyLayers()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			SanctuaryLayers.SetTexture(2, "Environment/Winter/Stratum/dirty_ground_albedo");
			var ed = ScmapEditor.Current;
			ed.Textures[1].AlbedoScale *= 2;
			ed.SetTextures(1);
			SanctuaryTexturePicker.Open(2);
			Debug.Log($"Sanctuary test layers: layer 2 -> dirty_ground, layer 1 scale -> {ed.Textures[1].AlbedoScale}");
		}

		/// -testprops: 25 Highlands trees painted in a 30 m circle at the map
		/// centre, every prop within 40 m of a point 120 m east erased, the
		/// Props panel opened, and the camera brought down to look at the
		/// painted trees for a screenshot.
		public static void ApplyProps()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			float c = SanctuaryMapLoader.Size / 2f;
			int placed = SanctuaryProps.Paint("Environment/01_Highlands/Props/edbm0149/edbm0149.santp", c, c, 30, 25, new System.Random(1));
			int erased = SanctuaryProps.Erase(c + 120, c, 40);
			var world = new Vector3(c / 10f, 0, (c - SanctuaryMapLoader.Size) / 10f);
			world.y = ScmapEditor.Current.Teren.SampleHeight(world);
			CameraControler.FocusCamera(world, Quaternion.Euler(35, 0, 0), 3);
			Debug.Log($"Sanctuary test props: painted {placed}, erased {erased}; {SanctuaryProps.Count} props now");
		}

		/// -testplace: the Markers tool as a click uses it: Markers tab, the
		/// Spawn (then Alloy) button, the placement ghost moved to a point
		/// 40 m (then 60 m) east of the centre, and the placement's own Place
		/// (what a left click calls). Logs whether each marker was created,
		/// is shown, and where, then aims the camera at them.
		public static void ApplyPlace()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			var edit = MapLuaParser.Current.EditMenu;
			edit.ButtonFunction("Markers");
			var markers = MapLuaParser.Current.SaveLuaFile.Data.MasterChains[0].Markers;
			float c = SanctuaryMapLoader.Size / 2f;
			var place = typeof(PlacementManager).GetMethod("Place", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			foreach (var (button, east) in new[] { (0, 40f), (1, 60f) })
			{
				int before = markers.Count;
				EditMap.NewMarkersInfo.Current.SelectCreateNew(button);
				var pm = PlacementManager.Current;
				var world = ScmapEditor.ScmapPosToWorld(new Vector3(c + east, 0, c));
				world.y = ScmapEditor.Current.Teren.SampleHeight(world);
				string error = null;
				if (pm == null || pm.PlacementObject == null) error = "no placement object";
				else
				{
					pm.PlacementObject.transform.position = world;
					try { place.Invoke(pm, null); } catch (System.Exception e) { error = (e.InnerException ?? e).ToString(); }
				}
				var added = markers.Count > before ? markers[markers.Count - 1] : null;
				var obj = added?.MarkerObj;
				var r = obj != null ? obj.GetComponentInChildren<Renderer>(true) : null;
				Debug.Log($"Sanctuary test place (button {button}): {(added == null ? "NOT created" : $"created {added.Name} ({added.MarkerType}) at {added.position}")}; " +
					$"object {(obj == null ? "none" : obj.gameObject.activeInHierarchy ? "shown" : "hidden")}, renderer {(r == null ? "none" : r.enabled && r.gameObject.activeInHierarchy ? "on" : "off")}, layer {(obj != null ? LayerMask.LayerToName(obj.gameObject.layer) : "-")}" +
					(r != null ? $", mesh {obj.Mf.sharedMesh?.name} {r.bounds.size}, material {r.sharedMaterial?.name} shader {r.sharedMaterial?.shader?.name} supported {r.sharedMaterial?.shader?.isSupported}, at world {r.bounds.center}, on screen {Camera.main.WorldToScreenPoint(r.bounds.center)}" : "") + (error != null ? "; error " + error : ""));
				EditMap.NewMarkersInfo.Current.SelectCreateNew(button); // back to selection
			}
			var focus = ScmapEditor.ScmapPosToWorld(new Vector3(c + 50, 0, c));
			focus.y = ScmapEditor.Current.Teren.SampleHeight(focus);
			CameraControler.FocusCamera(focus, Quaternion.Euler(50, 0, 0), 4);
		}

		/// -testtools: the props Select tool, a paint stroke, a texture pick
		/// and a drawn playable area, each through the code the mouse calls,
		/// each undone and redone through the editor's Undo, with every
		/// result checked and logged as PASS/FAIL. Leaves three edits for a
		/// save to show: playable area 200-824 m on both axes, the first
		/// prop moved 20 m east, the second prop deleted.
		public static void ApplyTools()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			int fails = 0;
			void Check(string what, bool ok) { if (!ok) fails++; Debug.Log($"Sanctuary tools test: {(ok ? "PASS" : "FAIL")} {what}"); }
			var undo = global::Undo.Current;
			float c = SanctuaryMapLoader.Size / 2f;

			// Paint, then undo: count and saved state come back.
			int n0 = SanctuaryProps.Count;
			UndoHistory.HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			int painted = SanctuaryProps.Paint("Environment/01_Highlands/Props/edbm0149/edbm0149.santp", c, c, 30, 12, new System.Random(2));
			Check($"paint adds {painted}", painted > 0 && SanctuaryProps.Count == n0 + painted && SanctuaryProps.Dirty);
			undo.DoUndo();
			Check("undo paint restores the count and the clean state", SanctuaryProps.Count == n0 && !SanctuaryProps.Dirty);
			undo.DoRedo();
			Check("redo paint", SanctuaryProps.Count == n0 + painted && SanctuaryProps.Dirty);
			undo.DoUndo();

			// Select: a click on a prop picks that prop (or one standing on
			// the same spot: props come in clumps).
			var groups = SanctuaryProps.Groups();
			var first = groups[0].Transforms[0];
			string type = groups[0].BlueprintPath;
			bool got = SanctuaryProps.Pick((float)first.Position.X, (float)first.Position.Z, 3f, out var pickedType, out var picked);
			Check($"pick at a prop finds a prop {(got ? System.Math.Sqrt((picked.Position.X - first.Position.X) * (picked.Position.X - first.Position.X) + (picked.Position.Z - first.Position.Z) * (picked.Position.Z - first.Position.Z)) : -1):0.00} m from the click",
				got && System.Math.Abs(picked.Position.X - first.Position.X) < 0.01 && System.Math.Abs(picked.Position.Z - first.Position.Z) < 0.01);
			Check("pick far from any prop finds none", !SanctuaryProps.Pick(-500, -500, 3f, out _, out _));

			// Turn 90 degrees, undo, redo, undo.
			float h0 = SanctuaryPropsPanel.Heading(first);
			UndoHistory.HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			var turned = SanctuaryPropsPanel.Turn(first, 90);
			SanctuaryProps.Replace(type, first, turned);
			Check($"turn 90: heading {h0:0} -> {SanctuaryPropsPanel.Heading(turned):0}", Mathf.Abs(Mathf.DeltaAngle(h0 + 90, SanctuaryPropsPanel.Heading(turned))) < 0.5f);
			undo.DoUndo();
			Check("undo turn puts the original back", SanctuaryProps.Contains(type, first) && !SanctuaryProps.Contains(type, turned) && !SanctuaryProps.Dirty);
			undo.DoRedo();
			Check("redo turn", SanctuaryProps.Contains(type, turned));
			undo.DoUndo();

			// Scale.
			UndoHistory.HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			var bigger = SanctuaryPropsPanel.Scaled(first, 1.1);
			SanctuaryProps.Replace(type, first, bigger);
			Check($"scale x1.1: {first.Scale.Y:0.000} -> {bigger.Scale.Y:0.000}", System.Math.Abs(bigger.Scale.Y - first.Scale.Y * 1.1) < 0.001);
			undo.DoUndo();
			Check("undo scale", SanctuaryProps.Contains(type, first));

			// Texture pick on layer 3, undone: the layer's JSON is exactly as loaded.
			var layers = SanctuaryMapLoader.Folder.Map.StratumLayers;
			string json0 = layers[3].ToString();
			UndoHistory.HistorySanctuaryState.Register(() => SanctuaryLayers.Snapshot(3));
			SanctuaryLayers.SetTexture(3, "Environment/Winter/Stratum/dirty_ground_albedo");
			Check("texture pick changes layer 3", layers[3].ToString() != json0);
			undo.DoUndo();
			Check("undo texture pick restores layer 3 exactly", layers[3].ToString() == json0 && ScmapEditor.Current.Textures[3].AlbedoPath != null && !ScmapEditor.Current.Textures[3].AlbedoPath.Contains("Winter"));
			undo.DoRedo();
			Check("redo texture pick", layers[3].ToString() != json0);
			undo.DoUndo();
			UndoHistory.HistorySanctuaryState.Register(() => SanctuaryLayers.Snapshot(3));
			SanctuaryLayers.SetTint(3, new Color(0.5f, 0.4f, 0.3f));
			undo.DoUndo();
			Check("undo tint restores layer 3 exactly", layers[3].ToString() == json0);

			// Playable area drawn from (200, 200) to (824, 824), undone, redone (kept).
			var area0 = MapLuaParser.Current.SaveLuaFile.Data.areas[0].rectangle;
			UndoHistory.HistorySanctuaryState.Register(global::AreaInfo.CaptureAreas);
			global::AreaInfo.SetPlayableArea(new Vector2(824, 200), new Vector2(200, 824));
			var r = MapLuaParser.Current.SaveLuaFile.Data.areas[0].rectangle;
			float size = SanctuaryMapLoader.Size;
			Check($"draw area: {r}", r.x == 200 && r.width == 824 && r.y == size - 824 && r.height == size - 200);
			undo.DoUndo();
			Check("undo area", MapLuaParser.Current.SaveLuaFile.Data.areas[0].rectangle == area0);
			undo.DoRedo();
			Check("redo area", MapLuaParser.Current.SaveLuaFile.Data.areas[0].rectangle == r);

			// Kept for the save: first prop 20 m east (on the ground), second prop deleted.
			UndoHistory.HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			var moved = SanctuaryPropsPanel.Moved(first, first.Position.X + 20, first.Position.Z);
			SanctuaryProps.Replace(type, first, moved);
			var second = groups[0].Transforms[1];
			UndoHistory.HistorySanctuaryState.Register(SanctuaryProps.Snapshot);
			SanctuaryProps.Remove(groups[0].BlueprintPath, second);
			Check($"kept: {groups[0].BlueprintPath} #0 moved to x {moved.Position.X:0.###} y {moved.Position.Y:0.###}, #1 deleted; {SanctuaryProps.Count} props", SanctuaryProps.Count == n0 - 1 && SanctuaryProps.Dirty);
			Debug.Log($"Sanctuary tools test: {fails} failed");
		}

		/// FA map coordinates (x, metres up, z from north) on the terrain, as the
		/// Markers tool places them.
		static Vector3 OnGround(float x, float z)
		{
			var world = ScmapEditor.ScmapPosToWorld(new Vector3(x, 0, z));
			world.y = ScmapEditor.Current.Teren.SampleHeight(world) + ScmapEditor.Current.Teren.transform.position.y;
			return ScmapEditor.WorldPosToScmap(world);
		}

		public static void Apply()
		{
			if (!SanctuaryMapLoader.IsActive) return;
			var ed = ScmapEditor.Current;

			var data = ed.Teren.terrainData;
			int c = data.heightmapResolution / 2 - HeightBox / 2;
			var h = data.GetHeights(c, c, HeightBox, HeightBox);
			float add = RaiseMetres / SanctuaryMapLoader.Folder.Map.Height;
			for (int y = 0; y < HeightBox; y++)
				for (int x = 0; x < HeightBox; x++)
					h[y, x] = Mathf.Clamp01(h[y, x] + add);
			data.SetHeights(c, c, h);

			var t1 = ed.map.TexturemapTex;
			var t2 = ed.map.TexturemapTex2;
			int mx = t1.width / 2 - MaskBox / 2, my = t1.height / 2 - MaskBox / 2;
			var a = new Color[MaskBox * MaskBox];
			var b = new Color[MaskBox * MaskBox];
			for (int i = 0; i < a.Length; i++) { a[i] = new Color(1, 0, 0, 0); b[i] = new Color(0, 0, 0, 0); }
			t1.SetPixels(mx, my, MaskBox, MaskBox, a); t1.Apply(false);
			t2.SetPixels(mx, my, MaskBox, MaskBox, b); t2.Apply(false);

			foreach (var mk in MapLuaParser.Current.SaveLuaFile.Data.MasterChains[0].Markers)
			{
				if (mk.MarkerType != SaveLua.Marker.MarkerTypes.BlankMarker) continue;
				var p = mk.position;
				p.x += MoveEastMetres;
				mk.position = p;
				Debug.Log($"Sanctuary test edit: moved {mk.Name} {MoveEastMetres} m east");
				break;
			}
			Debug.Log($"Sanctuary test edit: raised {HeightBox}x{HeightBox} cells at ({c},{c}) by {RaiseMetres} m; painted {MaskBox}x{MaskBox} px at ({mx},{my}) layer 1");
		}
	}
}
