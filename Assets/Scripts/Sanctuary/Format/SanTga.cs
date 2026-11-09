// Uncompressed 32-bit TGA, as Sanctuary maps store their stratum masks and
// tint maps. No UnityEngine dependency: this compiles in the dotnet test
// harness (Tools~/SanFormatTests) as well as in the editor.
//
// The game ships two header variants and reads both:
//   descriptor 0x08 - bottom-left origin, 8 alpha bits (stock maps)
//   descriptor 0x28 - top-left origin (maps from the SCFA converter)
// and tint_geometry.tga declares 0 alpha bits while still storing 32 bpp.
// Stock files also carry a 26-byte TGA 2.0 footer. A loaded file keeps its
// header and trailer bytes verbatim, so saving an unedited file reproduces it
// byte for byte.
using System;
using System.IO;

namespace Sanctuary.MapFormat
{
	public sealed class SanTga
	{
		public const int HeaderSize = 18;
		/// Descriptor for files this code creates: bottom-left origin, 8 alpha
		/// bits - the same as the developers' own maps, and the same row order
		/// as a Unity texture, so no flip is needed either way.
		public const byte NewFileDescriptor = 0x08;

		public int Width { get; private set; }
		public int Height { get; private set; }

		/// BGRA bytes, rows in file order. Use the pixel accessors rather than
		/// indexing this directly unless you have checked TopLeftOrigin.
		public byte[] Pixels { get; private set; }

		byte[] header;   // 18 bytes + image id
		byte[] trailer;  // anything after the pixels (TGA 2.0 footer)

		public bool TopLeftOrigin => (header[17] & 0x20) != 0;
		public byte Descriptor => header[17];

		public static SanTga Create(int width, int height)
		{
			if (width <= 0 || height <= 0 || width > 65535 || height > 65535)
				throw new ArgumentOutOfRangeException(nameof(width), $"TGA size {width}x{height} out of range");
			var h = new byte[HeaderSize];
			h[2] = 2; // uncompressed true-colour
			h[12] = (byte)(width & 0xff); h[13] = (byte)(width >> 8);
			h[14] = (byte)(height & 0xff); h[15] = (byte)(height >> 8);
			h[16] = 32;
			h[17] = NewFileDescriptor;
			return new SanTga { Width = width, Height = height, header = h, trailer = Array.Empty<byte>(), Pixels = new byte[width * height * 4] };
		}

		public static SanTga Read(string path) => Read(File.ReadAllBytes(path), path);

		public static SanTga Read(byte[] b, string nameForErrors = "tga")
		{
			if (b.Length < HeaderSize) throw new InvalidDataException($"{nameForErrors}: {b.Length} bytes is shorter than a TGA header");
			int idLen = b[0];
			if (b[1] != 0) throw new InvalidDataException($"{nameForErrors}: colour-mapped TGA is not a map texture");
			if (b[2] != 2) throw new InvalidDataException($"{nameForErrors}: TGA image type {b[2]}; Sanctuary maps use type 2 (uncompressed true-colour)");
			int w = b[12] | (b[13] << 8);
			int h = b[14] | (b[15] << 8);
			if (b[16] != 32) throw new InvalidDataException($"{nameForErrors}: {b[16]} bpp; Sanctuary map textures are 32 bpp");
			int start = HeaderSize + idLen;
			long len = (long)w * h * 4;
			if (start + len > b.Length) throw new InvalidDataException($"{nameForErrors}: {w}x{h}x4 pixels need {start + len} bytes, file has {b.Length}");

			var t = new SanTga { Width = w, Height = h };
			t.header = new byte[start];
			Buffer.BlockCopy(b, 0, t.header, 0, start);
			t.Pixels = new byte[len];
			Buffer.BlockCopy(b, start, t.Pixels, 0, (int)len);
			int rest = b.Length - start - (int)len;
			t.trailer = new byte[rest];
			Buffer.BlockCopy(b, start + (int)len, t.trailer, 0, rest);
			return t;
		}

		public void Write(string path)
		{
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			using (var fs = File.Create(path))
			{
				fs.Write(header, 0, header.Length);
				fs.Write(Pixels, 0, Pixels.Length);
				fs.Write(trailer, 0, trailer.Length);
			}
		}

		/// Byte offset of pixel (x, y), with y counted from the BOTTOM row -
		/// Unity's texture convention - whatever origin the file declares.
		public int Offset(int x, int yFromBottom)
		{
			int row = TopLeftOrigin ? Height - 1 - yFromBottom : yFromBottom;
			return (row * Width + x) * 4;
		}

		/// Channel value 0..255; channel 0=R, 1=G, 2=B, 3=A.
		public byte Get(int x, int yFromBottom, int channel)
		{
			return Pixels[Offset(x, yFromBottom) + BgraIndex(channel)];
		}

		public void Set(int x, int yFromBottom, int channel, byte value)
		{
			Pixels[Offset(x, yFromBottom) + BgraIndex(channel)] = value;
		}

		static int BgraIndex(int rgbaChannel)
		{
			switch (rgbaChannel)
			{
				case 0: return 2;
				case 1: return 1;
				case 2: return 0;
				case 3: return 3;
				default: throw new ArgumentOutOfRangeException(nameof(rgbaChannel));
			}
		}
	}
}
