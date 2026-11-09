// Sanctuary's game files: every *.sanpack under Sanctuary_Data/Gamedata is a
// plain zip. The game's texture lookup ignores the extension a map names and
// tries .dds first (a stratum layer names "..._albedo.tga"; the pack holds
// "..._albedo.dds"), so lookups here do the same. "map/..." paths resolve in
// the open map's own folder.
using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanGamedata
	{
		static readonly string[] ProbeOrder = { ".dds", ".png", ".tga", ".jpg", ".bmp" };

		// key: lower-case path without extension -> extension -> entry
		static Dictionary<string, Dictionary<string, KeyValuePair<ZipFile, ZipEntry>>> index;
		static readonly List<ZipFile> open = new List<ZipFile>();
		static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

		public static int EntryCount { get; private set; }

		public static void EnsureLoaded()
		{
			if (index != null) return;
			index = new Dictionary<string, Dictionary<string, KeyValuePair<ZipFile, ZipEntry>>>(StringComparer.OrdinalIgnoreCase);
			string dir = SanctuaryPaths.GamedataDir;
			if (dir == null || !Directory.Exists(dir))
			{
				Debug.LogWarning("Sanctuary install not found; game textures will be blank. Set it in preferences.");
				return;
			}
			foreach (var pack in Directory.GetFiles(dir, "*.sanpack"))
			{
				try
				{
					var zf = new ZipFile(File.OpenRead(pack));
					open.Add(zf);
					foreach (ZipEntry e in zf)
					{
						if (!e.IsFile) continue;
						string name = e.Name.Replace('\\', '/').TrimStart('/');
						string ext = Path.GetExtension(name).ToLowerInvariant();
						string key = name.Substring(0, name.Length - ext.Length);
						if (!index.TryGetValue(key, out var byExt)) index[key] = byExt = new Dictionary<string, KeyValuePair<ZipFile, ZipEntry>>();
						byExt[ext] = new KeyValuePair<ZipFile, ZipEntry>(zf, e);
						EntryCount++;
					}
				}
				catch (Exception ex)
				{
					Debug.LogError("Cannot read " + pack + "\n" + ex);
				}
			}
			Debug.Log($"Sanctuary gamedata: {EntryCount} files from {open.Count} packs in {dir}");
		}

		/// Bytes of a game or map file, and the extension actually found.
		public static byte[] ReadBytes(string path, out string foundExt)
		{
			foundExt = null;
			if (string.IsNullOrEmpty(path)) return null;
			path = path.Replace('\\', '/').TrimStart('/');
			string stem = path.Substring(0, path.Length - Path.GetExtension(path).Length);

			if (path.StartsWith("map/", StringComparison.OrdinalIgnoreCase))
			{
				var map = SanctuaryMapLoader.Folder;
				if (map == null) return null;
				string full = Path.Combine(map.Directory, stem.Substring(4));
				foreach (var ext in ProbeOrder)
					if (File.Exists(full + ext)) { foundExt = ext; return File.ReadAllBytes(full + ext); }
				return null;
			}

			EnsureLoaded();
			if (!index.TryGetValue(stem, out var byExt)) return null;
			foreach (var ext in ProbeOrder)
			{
				if (!byExt.TryGetValue(ext, out var entry)) continue;
				foundExt = ext;
				var bytes = new byte[entry.Value.Size];
				using (var s = entry.Key.GetInputStream(entry.Value))
				{
					int read = 0;
					while (read < bytes.Length)
					{
						int n = s.Read(bytes, read, bytes.Length - read);
						if (n <= 0) break;
						read += n;
					}
				}
				return bytes;
			}
			return null;
		}

		/// A texture by the path a .sanmap names, or null when it cannot be found.
		public static Texture2D LoadTexture(string path, bool linear)
		{
			if (string.IsNullOrEmpty(path)) return null;
			string key = (linear ? "lin:" : "srgb:") + path;
			if (cache.TryGetValue(key, out var t) && t != null) return t;

			byte[] b = ReadBytes(path, out string ext);
			if (b == null)
			{
				Debug.LogWarning("Sanctuary texture not found: " + path);
				return null;
			}

			try
			{
				if (ext == ".dds") t = DecodeDds(b, path, linear);
				else if (ext == ".tga") t = TgaToTexture(SanTga.Read(b, path), linear, true);
				else
				{
					t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
					t.LoadImage(b);
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Cannot decode " + path + "\n" + e);
				return null;
			}
			t.name = Path.GetFileName(path);
			t.wrapMode = TextureWrapMode.Repeat;
			cache[key] = t;
			return t;
		}

		/// DDS with a DX10 header (BC7 on most Sanctuary strata, which the FA
		/// loader does not know) is decoded here; legacy FourCC files go to the
		/// FA loader.
		static Texture2D DecodeDds(byte[] b, string name, bool linear)
		{
			if (b.Length < 148 || b[84] != 'D' || b[85] != 'X' || b[86] != '1' || b[87] != '0')
				return GetGamedataFile.TextureFromDdsBytes(b, name);

			int height = BitConverter.ToInt32(b, 12);
			int width = BitConverter.ToInt32(b, 16);
			int mips = Math.Max(1, BitConverter.ToInt32(b, 28));
			int dxgi = BitConverter.ToInt32(b, 128);
			TextureFormat fmt;
			switch (dxgi)
			{
				case 98: case 99: fmt = TextureFormat.BC7; break;
				case 71: case 72: fmt = TextureFormat.DXT1; break;
				case 77: case 78: fmt = TextureFormat.DXT5; break;
				case 80: fmt = TextureFormat.BC4; break;
				case 83: fmt = TextureFormat.BC5; break;
				case 95: fmt = TextureFormat.BC6H; break;
				case 28: case 29: fmt = TextureFormat.RGBA32; break;
				default: throw new NotSupportedException($"DXGI format {dxgi}");
			}
			const int header = 148;
			var data = new byte[b.Length - header];
			Buffer.BlockCopy(b, header, data, 0, data.Length);
			var t = new Texture2D(width, height, fmt, mips, linear);
			try { t.LoadRawTextureData(data); }
			catch (Exception)
			{
				// Fewer mips stored than declared, or trailing bytes: load the top level only.
				t = new Texture2D(width, height, fmt, 1, linear);
				int top = TopLevelSize(fmt, width, height);
				var first = new byte[top];
				Buffer.BlockCopy(data, 0, first, 0, Math.Min(top, data.Length));
				t.LoadRawTextureData(first);
			}
			t.Apply(false, false);
			t.anisoLevel = 4;
			t.filterMode = FilterMode.Trilinear;
			return t;
		}

		static int TopLevelSize(TextureFormat f, int w, int h)
		{
			int blocks = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4);
			switch (f)
			{
				case TextureFormat.DXT1: case TextureFormat.BC4: return blocks * 8;
				case TextureFormat.RGBA32: return w * h * 4;
				default: return blocks * 16;
			}
		}

		/// A Unity texture from a map TGA. Unity row 0 is the bottom row,
		/// which is what SanTga's accessors count from.
		/// `northUp` puts the north edge in texture row 0, which is what the
		/// FA terrain shader samples (v = -10 z / L); the game's own row 0 is
		/// the south edge.
		public static Texture2D TgaToTexture(SanTga tga, bool linear, bool mipmaps, bool northUp = false)
		{
			int w = tga.Width, h = tga.Height;
			var rgba = new byte[w * h * 4];
			for (int y = 0; y < h; y++)
			{
				int src = tga.Offset(0, northUp ? h - 1 - y : y);
				int dst = y * w * 4;
				for (int x = 0; x < w; x++, src += 4, dst += 4)
				{
					rgba[dst] = tga.Pixels[src + 2];
					rgba[dst + 1] = tga.Pixels[src + 1];
					rgba[dst + 2] = tga.Pixels[src];
					rgba[dst + 3] = tga.Pixels[src + 3];
				}
			}
			var t = new Texture2D(w, h, TextureFormat.RGBA32, mipmaps, linear);
			t.LoadRawTextureData(rgba);
			t.Apply(mipmaps);
			return t;
		}

		/// The reverse, for saving painted masks back into the map's TGA.
		public static void TextureToTga(Texture2D tex, SanTga tga, bool northUp = false)
		{
			if (tex.width != tga.Width || tex.height != tga.Height)
				throw new ArgumentException($"texture {tex.width}x{tex.height} does not match TGA {tga.Width}x{tga.Height}");
			var px = tex.GetPixels32();
			int w = tga.Width;
			for (int y = 0; y < tga.Height; y++)
			{
				int dst = tga.Offset(0, northUp ? tga.Height - 1 - y : y);
				int src = y * w;
				for (int x = 0; x < w; x++, src++, dst += 4)
				{
					tga.Pixels[dst] = px[src].b;
					tga.Pixels[dst + 1] = px[src].g;
					tga.Pixels[dst + 2] = px[src].r;
					tga.Pixels[dst + 3] = px[src].a;
				}
			}
		}
	}
}
