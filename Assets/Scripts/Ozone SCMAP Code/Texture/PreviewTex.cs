using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class PreviewTex : MonoBehaviour {

	public Camera Cam;
	public Color Empty;
	public LayerMask Layers;
	public LayerMask FullLayers;

	public RenderTexture DefaultRenderTex;

	static bool RenderingPreview = false;
	public static bool IsPreview
	{
		get
		{
			return RenderingPreview;
		}
	}

	public static void ForcePreviewMode(bool on)
	{
		RenderingPreview = on;
	}
	
	/// Orthographic top-down render of a square region (editor world units),
	/// for Sanctuary previews: they cover the playable area only, and an
	/// orthographic view keeps spawn badges on their spawns whatever the
	/// terrain height.
	/// (A true orthographic camera renders this terrain shader black, so this
	/// is a 5-degree telephoto from far above: within ~2 m of orthographic at
	/// the edge of a 1 km area with 40 m of relief.)
	public Texture2D RenderPreviewOrtho(Vector3 center, float sizeWorld, int Width, int Height, float topY, float planeY)
	{
		const float Fov = 5f;
		float wasFov = Cam.fieldOfView;
		float wasFar = Cam.farClipPlane;
		float wasNear = Cam.nearClipPlane;
		float distance = sizeWorld * 0.5f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
		Cam.fieldOfView = Fov;
		Cam.nearClipPlane = Mathf.Max(0.3f, distance - topY - 10f);
		Cam.farClipPlane = distance + planeY + 100f;
		try
		{
			return RenderPreview(0, Width, Height, false, false, new Vector3(center.x, planeY + distance, center.z));
		}
		finally
		{
			Cam.fieldOfView = wasFov;
			Cam.farClipPlane = wasFar;
			Cam.nearClipPlane = wasNear;
		}
	}

	public Texture2D RenderPreview(float HeightOffset = 0, int Width = 256, int Height = 256, bool Flip = true, bool RenderEverything = false, Vector3? CameraPosition = null) {
		RenderingPreview = true;

		//Sbool Slope = ScmapEditor.Current.Slope;
		//bool Grid = ScmapEditor.Current.Grid;

		//if (Slope)
		//ScmapEditor.Current.ToogleSlope(false);
		//if(Grid)
		//ScmapEditor.Current.ToogleGrid(false);

		if (RenderEverything)
			Cam.cullingMask = FullLayers;
		else
			Cam.cullingMask = Layers;

		ScmapEditor.Current.TerrainMaterial.EnableKeyword("PREVIEW_ON");

		Vector3 CamPos = MapLuaParser.Current.MapCenterPoint;
		float Size = Mathf.Max(ScmapEditor.Current.map.Width, ScmapEditor.Current.map.Height) * 0.1f;
		float distance = Size * (2.0f * Mathf.Tan(0.5f * Cam.fieldOfView * Mathf.Deg2Rad));

		distance = (Size) * 0.5f / Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

		CamPos.y = distance + HeightOffset;
		transform.position = CameraPosition ?? CamPos;

		//Cam.targetTexture.width = Width;
		//Cam.targetTexture.height = Height;

		if(DefaultRenderTex.width != Width || DefaultRenderTex.height != Height)
		{
			Cam.targetTexture = new RenderTexture(Width, Height, DefaultRenderTex.depth, DefaultRenderTex.format);

		}

		RenderTexture currentActiveRT = RenderTexture.active;
		RenderTexture.active = Cam.targetTexture;

		float LastLodBias = QualitySettings.lodBias;
		QualitySettings.lodBias = 100000;
		// -->
		Cam.Render();
		// <--
		QualitySettings.lodBias = LastLodBias;


		// Texture
		Texture2D PreviewRender = new Texture2D(Width, Height, TextureFormat.RGBA32, false);

		Color[] Colors = new Color[Width * Height];
		for(int i = 0; i < Colors.Length; i++)
		{
			Colors[i] = Empty;
		}
		PreviewRender.SetPixels(Colors);
		PreviewRender.Apply(false);
		PreviewRender.ReadPixels(new Rect(0, 0, PreviewRender.width, PreviewRender.height), 0, 0, false);
		PreviewRender.Apply(false);

		RenderTexture.active = currentActiveRT;

		if (Cam.targetTexture != DefaultRenderTex)
		{
			Destroy(Cam.targetTexture);
			Cam.targetTexture = DefaultRenderTex;
		}

		//if(Slope)
		//ScmapEditor.Current.ToogleSlope(Slope);
		//if(Grid)
		//ScmapEditor.Current.ToogleGrid(Grid);
		ScmapEditor.Current.TerrainMaterial.DisableKeyword("PREVIEW_ON");

		RenderingPreview = false;

		if (Flip)
			return TextureFlip.FlipTextureVertical(PreviewRender, false);
		else
			return PreviewRender;

	}


}
