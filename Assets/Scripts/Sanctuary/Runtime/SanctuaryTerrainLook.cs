// Inputs the game's terrain shader gets that FA's did not, fed to the editor's
// terrain shader as globals (FaTerrainShader, TerrainAlbedoXP):
//   tint_colors.tga  colour multiply over the terrain, 0.5 neutral, alpha its
//                    strength (MapManager sets it as _TerrainTintTexture)
//   full-range masks Sanctuary's stratum masks blend 0..1; FA's XP path
//                    threw away everything under 0.5
// Calibrated against the game's own preview.png (Tools~/preview-error.ps1);
// overridable on the command line: -tintalpha, -tintgamma, -maskfull, -notint.
using System;
using System.Globalization;
using Sanctuary.MapFormat;
using UnityEngine;

namespace Sanctuary
{
	public static class SanctuaryTerrainLook
	{
		public static float TintAlpha = 2f;        // strength = alpha * this (alpha 0.5 = full)
		public static float TintGamma = 1f / 2.2f;  // linear factor applied in gamma space
		public static bool MaskFull = true;
		public static bool TintOn = true;

		/// The tint as shown, north in row 0 like the stratum masks.
		public static Texture2D Tint { get; private set; }

		static readonly int IdTint = Shader.PropertyToID("_SanTint");

		public static void Load(SanMapFolder folder)
		{
			ReadArgs();
			TintDirty = false;
			try
			{
				var tga = folder.GetTexture("tint_colors");
				if (Tint != null) UnityEngine.Object.Destroy(Tint);
				// No mipmaps: TgaToTexture fills level 0 only.
				Tint = tga == null ? null : SanGamedata.TgaToTexture(tga, true, false, northUp: true);
				if (Tint != null) { Tint.wrapMode = TextureWrapMode.Clamp; Tint.name = "SanctuaryTint"; }
			}
			catch (Exception e)
			{
				Debug.LogWarning("Sanctuary tint_colors not shown: " + e.Message);
				Tint = null;
			}
			Apply();
		}

		/// Tint painted since load or the last save.
		public static bool TintDirty { get; private set; }
		public static void MarkTintSaved() => TintDirty = false;

		/// For undo: the tint as it is now.
		public static Action SnapshotTint()
		{
			if (Tint == null) return () => { };
			var copy = Tint.GetPixels32();
			var tex = Tint;
			bool wasDirty = TintDirty;
			return () =>
			{
				if (tex != Tint) return; // another map since
				tex.SetPixels32(copy);
				tex.Apply(false);
				TintDirty = true;
			};
		}

		/// Moves the tint within `radius` metres of map (x, z) towards
		/// `target` (0.5 neutral) by up to `strength` at the centre, fading
		/// to nothing at the edge. Painted texels get full tint strength.
		public static void PaintTint(float x, float z, float radius, float strength, Color target)
		{
			if (Tint == null) return;
			int w = Tint.width, h = Tint.height;
			float size = SanctuaryMapLoader.Size;
			float cx = x / size * w, cy = (size - z) / size * h; // row 0 = north
			float rr = radius / size * w;
			int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rr)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + rr));
			int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - rr)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + rr));
			if (x1 < x0 || y1 < y0) return;
			int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
			var px = Tint.GetPixels32(); // whole: GetPixels32 has no block overload
			// Full strength for painted texels: alpha 1/TintAlpha.
			byte fullA = (byte)Mathf.Clamp(Mathf.RoundToInt(255f / Mathf.Max(1f, TintAlpha)), 0, 255);
			var t = (Color32)target;
			for (int py = y0; py <= y1; py++)
				for (int qx = x0; qx <= x1; qx++)
				{
					float d = Mathf.Sqrt((qx - cx) * (qx - cx) + (py - cy) * (py - cy)) / Mathf.Max(0.5f, rr);
					if (d >= 1f) continue;
					float f = strength * (1f - d * d) * (1f - d * d);
					int i = py * w + qx;
					var c = px[i];
					c.r = (byte)Mathf.RoundToInt(Mathf.Lerp(c.r, t.r, f));
					c.g = (byte)Mathf.RoundToInt(Mathf.Lerp(c.g, t.g, f));
					c.b = (byte)Mathf.RoundToInt(Mathf.Lerp(c.b, t.b, f));
					c.a = (byte)Mathf.RoundToInt(Mathf.Lerp(c.a, Mathf.Max(c.a, fullA), Mathf.Min(1f, f * 4f)));
					px[i] = c;
				}
			Tint.SetPixels32(px);
			Tint.Apply(false);
			TintDirty = true;
		}

		/// Pushes the current values to the shader; call after editing Tint.
		public static void Apply()
		{
			Shader.SetGlobalTexture(IdTint, Tint != null ? (Texture)Tint : Texture2D.grayTexture);
			Shader.SetGlobalFloat("_SanTintOn", TintOn && Tint != null ? 1f : 0f);
			Shader.SetGlobalFloat("_SanTintAlpha", TintAlpha);
			Shader.SetGlobalFloat("_SanTintGamma", TintGamma);
			Shader.SetGlobalFloat("_SanMaskFull", MaskFull ? 1f : 0f);
		}

		static void ReadArgs()
		{
			var a = Environment.GetCommandLineArgs();
			for (int i = 0; i < a.Length; i++)
			{
				string v = i + 1 < a.Length ? a[i + 1] : "";
				if (a[i] == "-tintalpha") float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out TintAlpha);
				if (a[i] == "-tintgamma") float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out TintGamma);
				if (a[i] == "-maskfull") MaskFull = v != "0";
				if (a[i] == "-notint") TintOn = false;
			}
		}
	}
}
