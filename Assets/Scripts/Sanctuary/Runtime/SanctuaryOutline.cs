// Lines laid on the terrain for the Sanctuary tools: the props brush circle,
// the selected prop's ring, the playable area being drawn. One LineRenderer
// per outline, points in Sanctuary metres, heights sampled from the terrain.
using UnityEngine;

namespace Sanctuary
{
	public class SanctuaryOutline
	{
		static Material material;
		readonly LineRenderer line;

		public SanctuaryOutline(string name, Color color)
		{
			if (material == null) material = new Material(Shader.Find("Sanctuary/Line"));
			var go = new GameObject("Sanctuary outline " + name);
			Object.DontDestroyOnLoad(go);
			line = go.AddComponent<LineRenderer>();
			line.sharedMaterial = material;
			line.useWorldSpace = true;
			line.loop = true;
			line.startColor = line.endColor = color;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			line.enabled = false;
		}

		public void Hide() { if (line != null) line.enabled = false; }

		/// A circle of `radius` metres around Sanctuary (x, z).
		public void Circle(float x, float z, float radius)
		{
			const int n = 72;
			line.positionCount = n;
			for (int i = 0; i < n; i++)
			{
				float a = i * Mathf.PI * 2 / n;
				line.SetPosition(i, OnGround(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius));
			}
			Fit();
			line.enabled = true;
		}

		/// A rectangle between Sanctuary corners (x0, z0) and (x1, z1),
		/// following the ground along each side.
		public void Rect(float x0, float z0, float x1, float z1)
		{
			const int perSide = 32;
			line.positionCount = perSide * 4;
			var c = new[] { new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1) };
			for (int s = 0; s < 4; s++)
				for (int i = 0; i < perSide; i++)
				{
					var p = Vector2.Lerp(c[s], c[(s + 1) % 4], i / (float)perSide);
					line.SetPosition(s * perSide + i, OnGround(p.x, p.y));
				}
			Fit();
			line.enabled = true;
		}

		// Constant width on screen: scale with the camera's distance.
		void Fit()
		{
			var cam = Camera.main;
			if (cam == null || line.positionCount == 0) return;
			float d = Vector3.Distance(cam.transform.position, line.GetPosition(0));
			line.widthMultiplier = Mathf.Max(0.002f, d * 0.004f);
		}

		static Vector3 OnGround(float x, float z)
		{
			float size = SanctuaryMapLoader.Size;
			x = Mathf.Clamp(x, 0, size); z = Mathf.Clamp(z, 0, size);
			var terrain = ScmapEditor.Current.Teren;
			var w = new Vector3(x / 10f, 0, (z - size) / 10f);
			w.y = terrain.SampleHeight(w) + terrain.transform.position.y + 0.05f;
			return w;
		}
	}
}
