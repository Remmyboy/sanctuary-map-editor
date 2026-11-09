// preview.png for a Sanctuary map: what the lobby shows (LobbyManager reads
// <map folder>/preview.png and nothing else). Like the developers' own: a
// top-down view of the playable area, north up, with a numbered badge on each
// army's spawn.
using System;
using System.IO;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryPreview
	{
		/// The shipped maps' previews are 400 px; converted ones 512.
		public const int DefaultSize = 400;

		/// Badge colours by slot, read off the developers' previews at their
		/// spawn positions (Tools~/sample-badges.ps1 on The Forge, Two Step
		/// Shuffle and White Desert; brightest sample of each).
		static readonly Color32[] SlotColor =
		{
			new Color32(76, 150, 118, 255),  // 1 green
			new Color32(167, 22, 22, 255),   // 2 red
			new Color32(244, 228, 165, 255), // 3 cream
			new Color32(47, 91, 146, 255),   // 4 blue
			new Color32(248, 168, 0, 255),   // 5 orange
			new Color32(73, 124, 170, 255),  // 6 light blue
			new Color32(178, 168, 178, 255), // 7 lilac
			new Color32(196, 196, 196, 255), // 8 grey-white
		};

		// 3x5 digits, row-major from the top.
		static readonly string[] Digit35 =
		{
			"111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
			"111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
		};

		/// Renders and writes the preview. Returns the path written.
		public static string Write(SanMapFolder folder, int size = 0) =>
			WriteTo(folder, Path.Combine(folder.Directory, "preview.png"), size);

		public static string WriteTo(SanMapFolder folder, string path, int size = 0)
		{
			var m = folder.Map;
			if (size <= 0) size = ExistingSize(path);

			// Region: PlayableArea, else the whole terrain. Sanctuary metres,
			// z north; editor world = metres / 10, world z = (sanZ - Size) / 10.
			int terrain = SanctuaryMapLoader.Size;
			var area = m.PlayableArea ?? new SanArea { X = 0, Y = 0, Width = terrain, Height = terrain };
			float side = (float)Math.Max(area.Width, area.Height);
			float cx = (float)(area.X + area.Width / 2), cz = (float)(area.Y + area.Height / 2);
			var center = new Vector3(cx / 10f, 0, (cz - terrain) / 10f);
			var ter = ScmapEditor.Current.Teren;
			float top = ter.terrainData.size.y + 1f;

			// Frame at the mean ground height of the area (water surface where
			// it is higher), so the edges line up with the playable area.
			float sum = 0; int n = 0;
			float water = ScmapEditor.Current.map.Water.HasWater ? ScmapEditor.Current.map.Water.Elevation / 10f : 0f;
			for (int gy = 0; gy < 16; gy++)
				for (int gx = 0; gx < 16; gx++)
				{
					var p = new Vector3((float)(area.X + (gx + 0.5) * area.Width / 16) / 10f, 0, ((float)(area.Y + (gy + 0.5) * area.Height / 16) - terrain) / 10f);
					sum += Mathf.Max(ter.SampleHeight(p) + ter.transform.position.y, water); n++;
				}

			// Rendered at 2x and box-filtered down: at 1x the strata's detail
			// (sand ripples, heather patches) went to mush next to the game's.
			const int ss = 2;
			var tex = ScmapEditor.Current.PreviewRenderer.RenderPreviewOrtho(center, side / 10f, size * ss, size * ss, top, sum / n);
			var px = Downsample(tex.GetPixels32(), size * ss, ss);
			int i = 0;
			foreach (var army in m.ArmyNames)
			{
				var spawn = m.FindSpawn(army);
				if (spawn == null) continue;
				// Texture row 0 is the bottom (south) edge.
				int x = (int)Math.Round((spawn.Position.X - (cx - side / 2)) / side * size);
				int y = (int)Math.Round((spawn.Position.Z - (cz - side / 2)) / side * size);
				DrawBadge(px, size, x, y, i);
				i++;
			}

			var rgb = new Texture2D(size, size, TextureFormat.RGB24, false);
			rgb.SetPixels32(px);
			rgb.Apply(false);
			File.WriteAllBytes(path, rgb.EncodeToPNG());
			UnityEngine.Object.Destroy(rgb);
			UnityEngine.Object.Destroy(tex);
			return path;
		}

		static Color32[] Downsample(Color32[] src, int srcSize, int f)
		{
			int dst = srcSize / f;
			var o = new Color32[dst * dst];
			int n = f * f;
			for (int y = 0; y < dst; y++)
				for (int x = 0; x < dst; x++)
				{
					int r = 0, g = 0, b = 0;
					for (int dy = 0; dy < f; dy++)
						for (int dx = 0; dx < f; dx++)
						{
							var c = src[(y * f + dy) * srcSize + x * f + dx];
							r += c.r; g += c.g; b += c.b;
						}
					o[y * dst + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
				}
			return o;
		}

		static int ExistingSize(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					using (var f = File.OpenRead(path))
					{
						var h = new byte[24];
						if (f.Read(h, 0, 24) == 24 && h[1] == 'P' && h[2] == 'N' && h[3] == 'G')
						{
							int w = (h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19];
							if (w >= 64 && w <= 2048) return w;
						}
					}
				}
			}
			catch (Exception) { }
			return DefaultSize;
		}

		/// A disc in the slot colour with a darkened rim and the slot number,
		/// about 3.5% of the preview across, as on the developers' previews.
		static void DrawBadge(Color32[] px, int res, int cx, int cy, int slot)
		{
			int r = Math.Max(5, (int)Math.Round(res * 0.035));
			int s = Math.Max(1, r / 4);
			Color32 c = SlotColor[slot % SlotColor.Length];
			for (int dy = -r - 1; dy <= r + 1; dy++)
				for (int dx = -r - 1; dx <= r + 1; dx++)
				{
					int x = cx + dx, y = cy + dy;
					if (x < 0 || y < 0 || x >= res || y >= res) continue;
					int d2 = dx * dx + dy * dy;
					if (d2 > (r + 1) * (r + 1)) continue;
					int o = y * res + x;
					if (d2 > r * r) { var p = px[o]; px[o] = new Color32((byte)(p.r / 3), (byte)(p.g / 3), (byte)(p.b / 3), 255); }
					else px[o] = c;
				}

			string t = (slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
			int gw = 3 * s, gap = s, total = t.Length * gw + (t.Length - 1) * gap;
			int x0 = cx - total / 2, yTop = cy + 5 * s / 2;
			var ink = new Color32(20, 20, 26, 255);
			for (int k = 0; k < t.Length; k++)
			{
				string g = Digit35[t[k] - '0'];
				for (int gy = 0; gy < 5; gy++)
					for (int gx = 0; gx < 3; gx++)
					{
						if (g[gy * 3 + gx] != '1') continue;
						for (int py = 0; py < s; py++)
							for (int qx = 0; qx < s; qx++)
							{
								int x = x0 + k * (gw + gap) + gx * s + qx, y = yTop - gy * s - py;
								if (x >= 0 && y >= 0 && x < res && y < res) px[y * res + x] = ink;
							}
					}
			}
		}
	}
}
