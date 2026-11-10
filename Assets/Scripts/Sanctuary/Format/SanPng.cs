// Reads a PNG's first channel at full precision: Unity's LoadImage turns a
// 16-bit greyscale heightmap into 8 bits (measured 2026-11-10: a round trip
// lost up to one 8-bit step, ~2 m on a 512 m map). Non-interlaced PNGs of
// colour type 0 (grey), 2 (RGB), 4 (grey+alpha) and 6 (RGBA), 8 or 16 bits.
// No UnityEngine dependency.
using System;
using System.IO;
using System.IO.Compression;

namespace Sanctuary.MapFormat
{
	public static class SanPng
	{
		/// First channel of every pixel, 0..1, row 0 = the image's TOP row.
		public static float[,] ReadFirstChannel(byte[] png, out int width, out int height)
		{
			if (png.Length < 33 || png[1] != 'P' || png[2] != 'N' || png[3] != 'G') throw new InvalidDataException("not a PNG");
			int pos = 8, depth = 0, colour = 0, interlace = 0;
			width = height = 0;
			var idat = new MemoryStream();
			while (pos + 8 <= png.Length)
			{
				int len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
				string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
				int data = pos + 8;
				if (type == "IHDR")
				{
					width = (png[data] << 24) | (png[data + 1] << 16) | (png[data + 2] << 8) | png[data + 3];
					height = (png[data + 4] << 24) | (png[data + 5] << 16) | (png[data + 6] << 8) | png[data + 7];
					depth = png[data + 8]; colour = png[data + 9]; interlace = png[data + 12];
				}
				else if (type == "IDAT") idat.Write(png, data, len);
				else if (type == "IEND") break;
				pos = data + len + 4;
			}
			if (interlace != 0) throw new InvalidDataException("interlaced PNGs aren't supported");
			if (depth != 8 && depth != 16) throw new InvalidDataException($"{depth}-bit PNGs aren't supported");
			int channels = colour == 0 ? 1 : colour == 2 ? 3 : colour == 4 ? 2 : colour == 6 ? 4 : throw new InvalidDataException($"PNG colour type {colour} isn't supported");
			int bpp = channels * depth / 8, stride = width * bpp;

			var raw = new byte[(stride + 1) * height];
			idat.Position = 2; // zlib header
			using (var z = new DeflateStream(idat, CompressionMode.Decompress))
			{
				int got = 0, n;
				while (got < raw.Length && (n = z.Read(raw, got, raw.Length - got)) > 0) got += n;
				if (got < raw.Length) throw new InvalidDataException("PNG data is short");
			}

			var prev = new byte[stride];
			var cur = new byte[stride];
			var result = new float[height, width];
			for (int y = 0; y < height; y++)
			{
				int f = raw[y * (stride + 1)];
				Buffer.BlockCopy(raw, y * (stride + 1) + 1, cur, 0, stride);
				for (int i = 0; i < stride; i++)
				{
					int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
					int add = f == 0 ? 0 : f == 1 ? a : f == 2 ? b : f == 3 ? (a + b) >> 1 : Paeth(a, b, c);
					cur[i] = (byte)(cur[i] + add);
				}
				for (int x = 0; x < width; x++)
				{
					int o = x * bpp;
					result[y, x] = depth == 16 ? ((cur[o] << 8) | cur[o + 1]) / 65535f : cur[o] / 255f;
				}
				var t = prev; prev = cur; cur = t;
			}
			return result;
		}

		static int Paeth(int a, int b, int c)
		{
			int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
			return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
		}
	}
}
