// dotnet run -c Release -- [mapsDir] [--textures N|all] [--map Name]
//
// Every check compares against the files the game itself loads:
//   json      load + save is semantically identical (JToken.DeepEquals)
//   heightmap read + write is byte-identical; Unity heights round-trip exactly
//   textures  read + write of each TGA is byte-identical (first N maps)
//   folder    SanMapFolder.Save to a scratch dir reproduces every file
//   validate  what SanMapFolder.Validate says about each shipped map
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Sanctuary.MapFormat;

static class Program
{
	const string DefaultMaps = @"C:\Program Files (x86)\Steam\steamapps\common\Sanctuary Shattered Sun Playtest\engine\Sanctuary_Data\Maps";

	static int failures;

	static int Main(string[] args)
	{
		string mapsDir = DefaultMaps;
		int textureMaps = 4;
		string only = null;
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == "--textures") { string v = args[++i]; textureMaps = v == "all" ? int.MaxValue : int.Parse(v); }
			else if (args[i] == "--map") only = args[++i];
			else mapsDir = args[i];
		}

		var dirs = Directory.GetDirectories(mapsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
			.Where(d => only == null || string.Equals(Path.GetFileName(d), only, StringComparison.OrdinalIgnoreCase))
			.Where(d => Directory.GetFiles(d, "*.sanmap").Length > 0).ToList();
		Console.WriteLine($"{dirs.Count} maps in {mapsDir}");
		string scratch = Path.Combine(Path.GetTempPath(), "SanFormatTests");
		var sw = Stopwatch.StartNew();
		var problemCounts = new SortedDictionary<string, List<string>>();
		int texDone = 0, folderDone = 0;

		foreach (var dir in dirs)
		{
			string name = Path.GetFileName(dir);
			try
			{
				var f = SanMapFolder.Open(dir);
				var original = JToken.Parse(File.ReadAllText(f.SanmapPath));

				// json
				var reparsed = JToken.Parse(f.Map.ToJsonString());
				if (!JToken.DeepEquals(original, reparsed)) Fail(name, "json round trip differs: " + FirstDiff(original, reparsed, "$"));

				// accessors
				var armies = f.Map.ArmyNames;
				int spawns = f.Map.GetMarkers("Spawn").Count;
				int alloys = f.Map.MarkerTypes.Where(f.Map.IsResourceMarkerType).Sum(t => f.Map.GetMarkers(t).Count);

				// heightmap
				string hm = Path.Combine(f.TexturesPath, "heightmap.raw");
				var h = f.Heightmap;
				string hmOut = Path.Combine(scratch, "hm.raw");
				Directory.CreateDirectory(scratch);
				h.Write(hmOut);
				if (!SameBytes(hm, hmOut)) Fail(name, "heightmap.raw write differs from the file read");
				var copy = (ushort[])h.Raw.Clone();
				h.FromUnityHeights(h.ToUnityHeights());
				if (!copy.SequenceEqual(h.Raw)) Fail(name, "heightmap Unity-heights round trip changed values");

				// marker heights agree with the terrain (catches a flipped z)
				double worst = 0, worstAny = 0;
				string worstAnyName = "";
				foreach (var type in f.Map.MarkerTypes)
					foreach (var kv in f.Map.GetMarkers(type))
					{
						double terrain = TerrainHeight(h, f.Map, kv.Value.Position.X, kv.Value.Position.Z);
						double d = Math.Abs(terrain - kv.Value.Position.Y);
						if (type == "Spawn") worst = Math.Max(worst, d);
						if (d > worstAny) { worstAny = d; worstAnyName = $"{type}/{kv.Key} y={kv.Value.Position.Y:0.##} terrain={terrain:0.##}"; }
					}
				if (only != null) Console.WriteLine($"  worst marker of any type: {worstAny:0.00} m ({worstAnyName})");

				// textures
				string texNote = "";
				if (texDone < textureMaps)
				{
					foreach (var n in SanMapFolder.TextureNames)
					{
						string p = Path.Combine(f.TexturesPath, n + ".tga");
						if (!File.Exists(p)) continue;
						var t = SanTga.Read(p);
						string o = Path.Combine(scratch, n + ".tga");
						t.Write(o);
						if (!SameBytes(p, o)) Fail(name, $"{n}.tga write differs from the file read");
						texNote += $" {n}:{t.Width}x{t.Height}/0x{t.Descriptor:x2}";
					}
					texDone++;
				}

				// folder save to a new place, unedited: every file identical
				if (folderDone < 2)
				{
					string dst = Path.Combine(scratch, "folder", name);
					if (Directory.Exists(dst)) Directory.Delete(dst, true);
					var g = SanMapFolder.Open(dir);
					g.Save(dst);
					foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
					{
						string rel = Path.GetRelativePath(dir, file);
						string other = Path.Combine(dst, rel);
						if (!File.Exists(other)) Fail(name, $"folder save dropped {rel}");
						else if (!rel.EndsWith(".sanmap") && !SameBytes(file, other)) Fail(name, $"folder save changed {rel}");
					}
					var resaved = JToken.Parse(File.ReadAllText(Path.Combine(dst, name + ".sanmap")));
					((JObject)original)["fileVersion"] = 3;
					if (!JToken.DeepEquals(original, resaved)) Fail(name, "folder save changed the .sanmap content: " + FirstDiff(original, resaved, "$"));
					Directory.Delete(dst, true);
					folderDone++;
				}

				foreach (var p in f.Validate())
				{
					string key = System.Text.RegularExpressions.Regex.Replace(p, @"\d+|ARMY_\w+|Army_\w+", "#");
					if (!problemCounts.TryGetValue(key, out var l)) problemCounts[key] = l = new List<string>();
					l.Add(name);
				}

				Console.WriteLine($"  ok  {name,-36} res {h.Resolution,4}  armies {armies.Count,2} spawns {spawns,2} alloys {alloys,3} props {f.Map.PropInstanceCount,6}  spawn-vs-terrain {worst,5:0.00} m{texNote}");
			}
			catch (Exception e)
			{
				Fail(name, e.GetType().Name + ": " + e.Message);
			}
		}

		Console.WriteLine();
		Console.WriteLine("Validate() findings on the shipped maps:");
		if (problemCounts.Count == 0) Console.WriteLine("  none");
		foreach (var kv in problemCounts)
			Console.WriteLine($"  {kv.Value.Count,3} x {kv.Key}   e.g. {string.Join(", ", kv.Value.Take(3))}");

		Console.WriteLine();
		Console.WriteLine($"{dirs.Count} maps, {texDone} with textures checked, {failures} failure(s), {sw.Elapsed.TotalSeconds:0}s");
		return failures == 0 ? 0 : 1;
	}

	/// Bilinear terrain height in metres at world (x, z): Unity terrain at the
	/// origin, x along columns, z along Unity rows.
	static double TerrainHeight(SanHeightmap h, SanMapDocument m, double x, double z)
	{
		int r = h.Resolution;
		double fx = Math.Clamp(x / m.Width * (r - 1), 0, r - 1);
		double fz = Math.Clamp(z / m.Length * (r - 1), 0, r - 1);
		int x0 = (int)fx, z0 = (int)fz;
		int x1 = Math.Min(x0 + 1, r - 1), z1 = Math.Min(z0 + 1, r - 1);
		double tx = fx - x0, tz = fz - z0;
		double a = h.GetRaw(x0, z0) * (1 - tx) + h.GetRaw(x1, z0) * tx;
		double b = h.GetRaw(x0, z1) * (1 - tx) + h.GetRaw(x1, z1) * tx;
		return (a * (1 - tz) + b * tz) / SanHeightmap.RawScale * m.Height;
	}

	static bool SameBytes(string a, string b)
	{
		var fa = new FileInfo(a); var fb = new FileInfo(b);
		if (fa.Length != fb.Length) return false;
		const int N = 1 << 20;
		using var sa = fa.OpenRead(); using var sb = fb.OpenRead();
		var ba = new byte[N]; var bb = new byte[N];
		int n;
		while ((n = sa.Read(ba, 0, N)) > 0)
		{
			int m = 0;
			while (m < n) m += sb.Read(bb, m, n - m);
			if (!ba.AsSpan(0, n).SequenceEqual(bb.AsSpan(0, n))) return false;
		}
		return true;
	}

	static string FirstDiff(JToken a, JToken b, string path)
	{
		if (a.Type != b.Type) return $"{path}: {a.Type} vs {b.Type}";
		if (a is JObject oa && b is JObject ob)
		{
			foreach (var p in oa.Properties())
			{
				if (!ob.ContainsKey(p.Name)) return $"{path}.{p.Name} missing";
				if (!JToken.DeepEquals(p.Value, ob[p.Name])) return FirstDiff(p.Value, ob[p.Name], path + "." + p.Name);
			}
			foreach (var p in ob.Properties()) if (!oa.ContainsKey(p.Name)) return $"{path}.{p.Name} added";
		}
		if (a is JArray aa && b is JArray ab)
		{
			if (aa.Count != ab.Count) return $"{path}: {aa.Count} vs {ab.Count} items";
			for (int i = 0; i < aa.Count; i++) if (!JToken.DeepEquals(aa[i], ab[i])) return FirstDiff(aa[i], ab[i], $"{path}[{i}]");
		}
		return $"{path}: {a} vs {b}";
	}

	static void Fail(string map, string msg)
	{
		failures++;
		Console.WriteLine($"  FAIL {map}: {msg}");
	}
}
