// A map is a folder: Maps/<Folder>/<Folder>.sanmap, preview.png, and
// Textures/ holding heightmap.raw and four 32-bit TGAs. The game's loader
// (SanMap.LoadMapTextures) fails the whole map if any of the TGAs is missing,
// so Save always writes all four.
//
//   stratums_1_4.tga  RGBA = splat weights of layers 1..4
//   stratums_5_8.tga  RGBA = splat weights of layers 5..8
//   tint_colors.tga   colour tint over the terrain
//   tint_geometry.tga terrain types and normal offsets (the game names it
//                     "normals offset map")
using System;
using System.Collections.Generic;
using System.IO;

namespace Sanctuary.MapFormat
{
	public sealed class SanMapFolder
	{
		public const string TexturesDir = "Textures";
		public static readonly string[] TextureNames = { "stratums_1_4", "stratums_5_8", "tint_colors", "tint_geometry" };

		public string Directory { get; private set; }
		public string FolderName => Path.GetFileName(Directory);
		public string SanmapPath => Path.Combine(Directory, FolderName + SanMapDocument.Extension);
		public string TexturesPath => Path.Combine(Directory, TexturesDir);

		public SanMapDocument Map { get; private set; }

		SanHeightmap heightmap;
		readonly Dictionary<string, SanTga> textures = new Dictionary<string, SanTga>();

		SanMapFolder() { }

		/// Accepts the map folder or the .sanmap inside it.
		public static SanMapFolder Open(string path)
		{
			path = Path.GetFullPath(path);
			string dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
			var f = new SanMapFolder { Directory = dir };
			string sanmap = File.Exists(path) && path.EndsWith(SanMapDocument.Extension, StringComparison.OrdinalIgnoreCase)
				? path : f.SanmapPath;
			if (!File.Exists(sanmap))
			{
				// The game keys a map by its folder, but tolerate a single
				// differently named .sanmap.
				var found = System.IO.Directory.GetFiles(dir, "*" + SanMapDocument.Extension);
				if (found.Length != 1) throw new FileNotFoundException($"no {f.FolderName}{SanMapDocument.Extension} in {dir}");
				sanmap = found[0];
			}
			f.Map = SanMapDocument.Load(sanmap);
			return f;
		}

		/// Heightmap, read on first use.
		public SanHeightmap Heightmap
		{
			get
			{
				if (heightmap == null)
					heightmap = SanHeightmap.Read(Path.Combine(TexturesPath, "heightmap.raw"), Map.HeightmapResolution);
				return heightmap;
			}
			set => heightmap = value;
		}

		/// One of TextureNames, read on first use (each can be 64 MB).
		public SanTga GetTexture(string name)
		{
			if (!textures.TryGetValue(name, out var t))
			{
				string p = Path.Combine(TexturesPath, name + ".tga");
				t = SanTga.Read(p);
				textures[name] = t;
			}
			return t;
		}

		public void SetTexture(string name, SanTga tga) => textures[name] = tga;

		public bool IsTextureLoaded(string name) => textures.ContainsKey(name);
		public bool IsHeightmapLoaded => heightmap != null;

		/// Writes the folder to `targetDir` (default: where it came from). Only
		/// parts that were loaded or set are rewritten; when saving somewhere
		/// new, untouched files are copied across as they are.
		public void Save(string targetDir = null)
		{
			string dst = Path.GetFullPath(targetDir ?? Directory);
			string src = Directory;
			string name = Path.GetFileName(dst);
			string tex = Path.Combine(dst, TexturesDir);
			System.IO.Directory.CreateDirectory(tex);

			Map.Save(Path.Combine(dst, name + SanMapDocument.Extension));

			bool moved = !string.Equals(src, dst, StringComparison.OrdinalIgnoreCase);
			if (heightmap != null) heightmap.Write(Path.Combine(tex, "heightmap.raw"));
			else if (moved) CopyIfExists(Path.Combine(src, TexturesDir, "heightmap.raw"), Path.Combine(tex, "heightmap.raw"));

			foreach (var n in TextureNames)
			{
				if (textures.TryGetValue(n, out var t)) t.Write(Path.Combine(tex, n + ".tga"));
				else if (moved) CopyIfExists(Path.Combine(src, TexturesDir, n + ".tga"), Path.Combine(tex, n + ".tga"));
			}

			if (moved)
			{
				// Map-local textures referenced as map/Textures/..., and the preview.
				foreach (var f in System.IO.Directory.GetFiles(Path.Combine(src, TexturesDir)))
				{
					string d = Path.Combine(tex, Path.GetFileName(f));
					if (!File.Exists(d)) File.Copy(f, d);
				}
				CopyIfExists(Path.Combine(src, "preview.png"), Path.Combine(dst, "preview.png"));
				Directory = dst;
			}
		}

		static void CopyIfExists(string from, string to)
		{
			if (File.Exists(from)) File.Copy(from, to, true);
		}

		/// Faults the game accepts silently and that break a map. Each entry
		/// is one human-readable problem; entries starting "warning:" are
		/// oddities a shipped map also has. Empty means none found.
		public List<string> Validate()
		{
			var problems = new List<string>();
			var m = Map;
			int res = m.HeightmapResolution;
			if (!SanHeightmap.IsValidResolution(res))
				problems.Add($"heightmapResolution {res} is not 2^n+1; Unity resizes it and the heights land in one corner");
			else if (m.Width != res - 1 || m.Length != res - 1)
				// The shipped Two_Step_Shuffle has width 1023 on a 1025 heightmap
				// and plays; the terrain is just stretched by the difference.
				problems.Add($"warning: width/length {m.Width}x{m.Length} do not match heightmapResolution {res} (expected {res - 1}); the terrain stretches to fit");

			var armies = m.ArmyNames;
			if (armies.Count == 0) problems.Add("no armies: the lobby will offer zero player slots");
			foreach (var a in armies)
				if (m.FindSpawn(a) == null && !a.StartsWith("NEUTRAL", StringComparison.OrdinalIgnoreCase))
					problems.Add($"army {a} has no Spawn marker");

			var areas = m.Json["areas"] as Newtonsoft.Json.Linq.JObject;
			if (areas != null && areas.Count > 0 && m.PlayableArea == null)
				problems.Add("areas has no entry named exactly \"PlayableArea\"; the engine ignores other names and plays on the whole terrain");

			var layers = m.StratumLayers;
			if (layers == null || layers.Count != SanMapDocument.StratumCount)
				problems.Add($"stratumLayers has {layers?.Count ?? 0} entries, the game expects {SanMapDocument.StratumCount}");

			foreach (var n in TextureNames)
				if (!textures.ContainsKey(n) && !File.Exists(Path.Combine(TexturesPath, n + ".tga")))
					problems.Add($"Textures/{n}.tga is missing; the game refuses to load the map without it");
			if (heightmap == null && !File.Exists(Path.Combine(TexturesPath, "heightmap.raw")))
				problems.Add("Textures/heightmap.raw is missing");

			return problems;
		}
	}
}
