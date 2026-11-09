// Textures/heightmap.raw: resolution x resolution little-endian uint16, no
// header. Mirrors the game's EM.Gamedata.Load.ReadRaw / Save.WriteRaw as
// SanMap calls them (flipVertically: true, scale 1, Bit16, Windows order):
//
//   unity[res-1-row, col] = raw[row*res + col] / 65536
//   raw = clamp(round(unity * 65536), 0, 65535)
//
// so file row 0 is Unity heightmap row res-1 - the far (+z) edge. Unity's
// normalised height times the terrain's size.y (the .sanmap `height` field)
// gives metres. Raw values are kept as loaded, so an unedited heightmap saves
// byte for byte.
using System;
using System.IO;

namespace Sanctuary.MapFormat
{
	public sealed class SanHeightmap
	{
		public const float RawScale = 65536f; // the game's own divisor, not 65535

		public int Resolution { get; }
		/// Raw samples in file order (row 0 = Unity row Resolution-1).
		public ushort[] Raw { get; }

		public SanHeightmap(int resolution)
		{
			CheckResolution(resolution);
			Resolution = resolution;
			Raw = new ushort[resolution * resolution];
		}

		SanHeightmap(int resolution, ushort[] raw)
		{
			Resolution = resolution;
			Raw = raw;
		}

		/// Unity rounds a heightmap resolution to 2^n+1 without complaint;
		/// any other value loads as a corner of the map. Refuse it here.
		public static bool IsValidResolution(int r)
		{
			if (r < 33 || r > 4097) return false;
			int n = r - 1;
			return (n & (n - 1)) == 0;
		}

		static void CheckResolution(int r)
		{
			if (!IsValidResolution(r))
				throw new ArgumentException($"heightmap resolution {r} is not 2^n+1 between 33 and 4097; Unity would silently resize it");
		}

		public static SanHeightmap Read(string path, int resolution)
		{
			CheckResolution(resolution);
			byte[] b = File.ReadAllBytes(path);
			long want = (long)resolution * resolution * 2;
			if (b.Length != want)
			{
				int fileRes = (int)Math.Round(Math.Sqrt(b.Length / 2.0));
				throw new InvalidDataException($"{path}: {b.Length} bytes is a {fileRes}^2 heightmap, the .sanmap says {resolution} (needs {want} bytes)");
			}
			var raw = new ushort[resolution * resolution];
			for (int i = 0; i < raw.Length; i++)
				raw[i] = (ushort)(b[i * 2] | (b[i * 2 + 1] << 8));
			return new SanHeightmap(resolution, raw);
		}

		public void Write(string path)
		{
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			var b = new byte[Raw.Length * 2];
			for (int i = 0; i < Raw.Length; i++)
			{
				b[i * 2] = (byte)(Raw[i] & 0xff);
				b[i * 2 + 1] = (byte)(Raw[i] >> 8);
			}
			File.WriteAllBytes(path, b);
		}

		/// Raw sample at Unity heightmap coordinates (x = column, y = row from
		/// the near (-z) edge).
		public ushort GetRaw(int x, int unityRow) => Raw[(Resolution - 1 - unityRow) * Resolution + x];

		public void SetRaw(int x, int unityRow, ushort v) => Raw[(Resolution - 1 - unityRow) * Resolution + x] = v;

		/// The array TerrainData.SetHeights takes, exactly as the game builds it.
		public float[,] ToUnityHeights()
		{
			int r = Resolution;
			var h = new float[r, r];
			for (int row = 0; row < r; row++)
			{
				int u = r - 1 - row;
				int o = row * r;
				for (int c = 0; c < r; c++)
					h[u, c] = Raw[o + c] / RawScale;
			}
			return h;
		}

		/// Takes TerrainData.GetHeights output; rounds as the game's writer does.
		public void FromUnityHeights(float[,] h)
		{
			int r = Resolution;
			if (h.GetLength(0) != r || h.GetLength(1) != r)
				throw new ArgumentException($"heights are {h.GetLength(1)}x{h.GetLength(0)}, heightmap is {r}x{r}");
			for (int row = 0; row < r; row++)
			{
				int u = r - 1 - row;
				int o = row * r;
				for (int c = 0; c < r; c++)
				{
					// Mathf.RoundToInt: float product, banker's rounding.
					float p = h[u, c] * 1f * RawScale;
					double v = Math.Round((double)p);
					Raw[o + c] = (ushort)(v < 0 ? 0 : v > 65535 ? 65535 : v);
				}
			}
		}

		/// Metres above the terrain's base for a raw value, given the .sanmap `height`.
		public static double RawToMetres(ushort raw, double mapHeight) => raw / (double)RawScale * mapHeight;
	}
}
