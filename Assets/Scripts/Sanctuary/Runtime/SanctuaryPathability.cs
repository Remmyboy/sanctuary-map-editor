// Where ground units can go, by the game's own rule (EM.GameUtils.MapUtils.
// IsValidTerrain): a heightmap cell is blocked if it, or any of its 8
// neighbours, has a normal more than 30 degrees from up, or it lies within 2
// cells of the edge. The normal is the game's CalculateNormalUnsafe:
// normalize(2(hL-hR), 4, 2(hD-hU)), heights in metres, one cell apart.
// Shown as a red wash on the terrain (FaTerrainShader, _SanPath).
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryPathability
	{
		public static bool Shown { get; private set; }
		public static int BlockedCells { get; private set; }
		public static int Cells { get; private set; }

		static Texture2D tex;
		// tan(30 deg): the normal is past 30 degrees when the horizontal part
		// of (2dx, 4, 2dz) exceeds 4 tan 30.
		const float Limit = 4f * 0.57735027f;

		public static void Show(bool on)
		{
			Shown = on;
			if (on) Recompute();
			Shader.SetGlobalFloat("_SanPathOn", on && tex != null ? 1f : 0f);
		}

		/// Rebuilds the overlay from the terrain as it is now.
		public static void Recompute()
		{
			var ed = ScmapEditor.Current;
			var doc = SanctuaryMapLoader.Folder?.Map;
			if (ed == null || ed.Teren == null || doc == null) return;
			var td = ed.Teren.terrainData;
			int n = td.heightmapResolution;
			float metres = doc.Height; // normalised heights x the map's height
			var h = td.GetHeights(0, 0, n, n); // [row = south to north, col = west to east]

			// Steep cells first, then the game's 3x3 neighbourhood test.
			var steep = new bool[n * n];
			for (int z = 1; z < n - 1; z++)
				for (int x = 1; x < n - 1; x++)
				{
					float gx = 2f * (h[z, x - 1] - h[z, x + 1]) * metres;
					float gz = 2f * (h[z - 1, x] - h[z + 1, x]) * metres;
					steep[z * n + x] = gx * gx + gz * gz > Limit * Limit;
				}

			var px = new byte[n * n];
			int blocked = 0;
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					bool bad = x < 2 || z < 2 || x > n - 3 || z > n - 3;
					for (int dz = -1; dz <= 1 && !bad; dz++)
						for (int dx = -1; dx <= 1 && !bad; dx++)
							bad = steep[(z + dz) * n + x + dx];
					// Row 0 = north, like the stratum masks the shader samples alongside.
					px[(n - 1 - z) * n + x] = bad ? (byte)255 : (byte)0;
					if (bad) blocked++;
				}

			if (tex == null || tex.width != n)
			{
				if (tex != null) Object.Destroy(tex);
				tex = new Texture2D(n, n, TextureFormat.R8, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SanctuaryPathability" };
			}
			tex.SetPixelData(px, 0);
			tex.Apply(false);
			Shader.SetGlobalTexture("_SanPath", tex);
			Shader.SetGlobalFloat("_SanPathOn", Shown ? 1f : 0f);
			BlockedCells = blocked;
			Cells = n * n;
			Debug.Log($"Sanctuary pathability: {blocked * 100f / (n * n):0.0}% of {n}x{n} cells blocked (slope over 30 degrees or map edge)");
		}
	}
}
