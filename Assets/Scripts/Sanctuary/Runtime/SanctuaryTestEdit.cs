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
