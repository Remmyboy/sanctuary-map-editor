// A new, blank Sanctuary map built on another map's look. No UnityEngine:
// the dotnet harness can create maps too.
//
// What the template gives: stratum layers, lighting, fog, sky, water style,
// wind - every field the new map does not set itself. What is new: a flat
// heightmap, empty splat masks (the base layer shows everywhere), neutral
// tint maps, a ring of spawns, no resources, props or decals.
//
// Layout follows the developers' maps: the terrain is twice the playable
// area (capped at 4096 m), with the PlayableArea centred, because the engine
// mirrors the terrain past its edge and a map that fills its terrain shows
// reflected copies of itself (docs/modding-field-notes.md, "Maps").
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Sanctuary.MapFormat
{
	public sealed class NewMapOptions
	{
		public string Name;
		/// Playable area, metres. Rounded to what the terrain allows.
		public int PlayableWidth = 512, PlayableLength = 512;
		public int Armies = 2;
		public double GroundHeight = 30;
		public bool Water;
		public double WaterLevel = 20;
		/// Metres a raw height of 65536 reaches; the shipped maps use 128-1600.
		public int VerticalScale = 512;
	}

	public static class SanMapFactory
	{
		public static int TerrainSizeFor(int playable)
		{
			int s = 256;
			while (s < playable * 2 && s < 4096) s *= 2;
			return s;
		}

		/// Writes a new map into `targetDir` (which must not exist or be
		/// empty) and returns it, opened.
		public static SanMapFolder Create(string targetDir, SanMapFolder template, NewMapOptions o)
		{
			if (Directory.Exists(targetDir) && Directory.GetFileSystemEntries(targetDir).Length > 0)
				throw new IOException("not empty: " + targetDir);
			if (o.Armies < 1 || o.Armies > 16) throw new ArgumentOutOfRangeException(nameof(o.Armies));
			if (o.GroundHeight < 0 || o.GroundHeight >= o.VerticalScale) throw new ArgumentOutOfRangeException(nameof(o.GroundHeight));

			int terrain = TerrainSizeFor(Math.Max(o.PlayableWidth, o.PlayableLength));
			int pw = Math.Min(o.PlayableWidth, terrain), pl = Math.Min(o.PlayableLength, terrain);
			double ax = (terrain - pw) / 2.0, az = (terrain - pl) / 2.0;
			int res = terrain + 1;
			string folderName = Path.GetFileName(Path.GetFullPath(targetDir).TrimEnd('\\', '/'));

			var json = (JObject)template.Map.Json.DeepClone();
			json["name"] = string.IsNullOrWhiteSpace(o.Name) ? folderName.Replace('_', ' ') : o.Name;
			json["credits"] = "";
			json["mapVersion"] = 1;
			json["width"] = terrain;
			json["length"] = terrain;
			json["height"] = o.VerticalScale;
			json["heightmapResolution"] = res;
			json.Remove("layerResolution");
			json["hasWater"] = o.Water;
			json["waterLevel"] = o.Water ? o.WaterLevel : 0.0;
			json["props"] = new JArray();
			json["decals"] = new JArray();
			json["chains"] = new JObject();
			json["areas"] = new JObject
			{
				["PlayableArea"] = new JObject { ["x"] = ax, ["y"] = az, ["width"] = (double)pw, ["height"] = (double)pl },
			};

			// Armies: the template's first army's settings for each slot.
			var proto = (json["armies"] as JObject)?.Properties().FirstOrDefault()?.Value as JObject
				?? new JObject { ["faction"] = 0, ["alloys"] = 500.0, ["energy"] = 500.0 };
			var armies = new JObject();
			var spawns = new JObject();
			double cx = terrain / 2.0, cz = terrain / 2.0, ring = Math.Min(pw, pl) * 0.35;
			for (int i = 0; i < o.Armies; i++)
			{
				string key = "Army_" + (i + 1);
				var a = (JObject)proto.DeepClone();
				a["groups"] = new JObject();
				armies[key] = a;
				double ang = 2 * Math.PI * i / o.Armies;
				spawns[key] = Transform(Math.Round(cx + Math.Sin(ang) * ring), o.GroundHeight, Math.Round(cz + Math.Cos(ang) * ring));
			}
			json["armies"] = armies;

			string resourceType = "Alloys";
			var tm = json["markers"] as JObject;
			if (tm != null)
				foreach (var p in tm.Properties())
					if (p.Value["resource"]?.Value<bool>() == true) { resourceType = p.Name; break; }
			json["markers"] = new JObject
			{
				["Spawn"] = new JObject { ["resource"] = false, ["transforms"] = spawns },
				[resourceType] = new JObject { ["resource"] = true, ["transforms"] = new JObject() },
			};

			// Files.
			string tex = Path.Combine(targetDir, SanMapFolder.TexturesDir);
			Directory.CreateDirectory(tex);
			var doc = new SanMapDocument(json);
			doc.Save(Path.Combine(targetDir, folderName + SanMapDocument.Extension));

			var hm = new SanHeightmap(res);
			ushort ground = (ushort)Math.Min(65535, Math.Round(o.GroundHeight / o.VerticalScale * SanHeightmap.RawScale));
			for (int i = 0; i < hm.Raw.Length; i++) hm.Raw[i] = ground;
			hm.Write(Path.Combine(tex, "heightmap.raw"));

			// Masks: one texel per metre (the shipped 2048 m maps use 2048 or
			// 4096); tint maps as on the shipped maps, at most 2048.
			int maskRes = Math.Min(4096, terrain);
			Fill(SanTga.Create(maskRes, maskRes), 0, 0, 0, 0).Write(Path.Combine(tex, "stratums_1_4.tga"));
			Fill(SanTga.Create(maskRes, maskRes), 0, 0, 0, 0).Write(Path.Combine(tex, "stratums_5_8.tga"));
			int tintRes = Math.Min(2048, terrain);
			Fill(SanTga.Create(tintRes, tintRes), 127, 127, 127, 128).Write(Path.Combine(tex, "tint_colors.tga"));
			Fill(SanTga.Create(tintRes, tintRes), 127, 127, 255, 255).Write(Path.Combine(tex, "tint_geometry.tga"));

			// Map-local textures the template's layers name ("map/...").
			foreach (var layer in (json["stratumLayers"] as JArray ?? new JArray()).OfType<JObject>())
				foreach (var slot in new[] { "albedo", "normal", "mask" })
				{
					string p = (layer[slot] as JObject)?["path"]?.Value<string>();
					if (p == null || !p.StartsWith("map/", StringComparison.OrdinalIgnoreCase)) continue;
					string rel = p.Substring(4).Replace('/', Path.DirectorySeparatorChar);
					string stem = Path.Combine(template.Directory, Path.ChangeExtension(rel, null));
					foreach (var ext in new[] { ".dds", ".png", ".tga", ".jpg", ".bmp" })
					{
						string src = stem + ext;
						if (!File.Exists(src)) continue;
						string dst = Path.Combine(targetDir, Path.ChangeExtension(rel, ext));
						Directory.CreateDirectory(Path.GetDirectoryName(dst));
						if (!File.Exists(dst)) File.Copy(src, dst);
					}
				}

			return SanMapFolder.Open(targetDir);
		}

		static SanTga Fill(SanTga t, byte r, byte g, byte b, byte a)
		{
			var px = t.Pixels;
			for (int i = 0; i < px.Length; i += 4) { px[i] = b; px[i + 1] = g; px[i + 2] = r; px[i + 3] = a; }
			return t;
		}

		static JObject Transform(double x, double y, double z) => new JObject
		{
			["position"] = new JObject { ["x"] = x, ["y"] = y, ["z"] = z },
			["rotation"] = new JObject { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0, ["w"] = 1.0 },
			["scale"] = new JObject { ["x"] = 1.0, ["y"] = 1.0, ["z"] = 1.0 },
		};
	}
}
