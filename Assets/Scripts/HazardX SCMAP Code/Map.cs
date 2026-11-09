// ***************************************************************************************
// * SCMAP Loader
// * Copyright Unknown
// * Filename: Map.cs
// * Source: http://www.hazardx.com/details.php?file=82
// ***************************************************************************************

using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
[System.Serializable, PreferBinarySerialization]
#endif
public class Map
{

	// Map
	private const int MAP_MAGIC = 0x1a70614d;

	private const int MAP_VERSION = 2;


	public string Filename;

	//    public Bitmap PreviewBitmap;

	public GetGamedataFile.HeaderClass PreviewTextHeader;
	public GetGamedataFile.HeaderClass TextureMapHeader;
	public GetGamedataFile.HeaderClass TextureMap2Header;
	public GetGamedataFile.HeaderClass NormalmapHeader;
	public GetGamedataFile.HeaderClass WatermapHeader;

	public Texture2D PreviewTex;
	[HideInInspector]
	public ushort[] HeightmapData = new ushort[0];
	public short HeightMin;
	public short HeightMax;

	public short HeightDiff;

	public WaterShader Water = new WaterShader();
	//texturemap for the "Strata" layers
	public Texture2D TexturemapTex;
	public Texture2D TexturemapTex2;
	public Texture2D NormalmapTex;
	public Texture2D WatermapTex;
	public Texture2D WaterDataTexture;
	[HideInInspector]
	public byte[] WaterFoamMask = new byte[0];
	[HideInInspector]
	public byte[] WaterFlatnessMask = new byte[0];

	[HideInInspector]
	public byte[] WaterDepthBiasMask = new byte[0];
	public int Width = 0;
	public int Height = 0;

	public string TexPathBackground;
	public string TexPathSkyCubemap;
	public string[] EnvCubemapsName;
	public string[] EnvCubemapsFile;
	public SkyboxData AdditionalSkyboxData = new SkyboxData();

	[HideInInspector]
	public byte[] TerrainTypeData;

	//[HideInInspector]
	public List<Layer> Layers = new List<Layer>();
	public int VersionMinor = 0;

	public int VersionMajor = 0;
	public float HeightScale;

	public string TerrainShader;
	public float LightingMultiplier;
	public Vector3 SunDirection;
	public Vector3 SunAmbience;
	public Vector3 SunColor;
	public Vector3 ShadowFillColor;
	public Vector4 SpecularColor;

	public float Bloom;
	public Vector3 FogColor;
	public float FogStart;
	public float FogEnd;
	public int Unknown10;
	public int Unknown11;
	public float Unknown12;

	public byte Unknown15;
	public Single Unknown16;
	public Single Unknown17;

	public short Unknown13;
	public int Unknown7;

	public int Unknown8;
	public float Unknown14;

	//Minimap Cartographic View Colors (Not in Hazard's Original Code)
	public int MinimapContourInterval;
	public Color32 MinimapDeepWaterColor;
	public Color32 MinimapShoreColor;
	public Color32 MinimapLandStartColor;
	public Color32 MinimapLandEndColor;
	public Color32 MinimapContourColor; //Not sure about this one


	//<Merge Conflict>
	public void Initialize()
	{
		PreviewTex = new Texture2D(0, 0);
		TexturemapTex = new Texture2D(0, 0);
		TexturemapTex2 = new Texture2D(0, 0);
		NormalmapTex = new Texture2D(0, 0);
		WatermapTex = new Texture2D(0, 0);
		WaterDataTexture = new Texture2D(0, 0);

		TerrainTypeData = new byte[Height * Width];
		//		TerrainTypeData = new List<byte>();
		HeightmapData = new ushort[(Height + 1) * (Width + 1)];
		WaterDepthBiasMask = new byte[(Height * Width) / 4];
		WaterFlatnessMask = new byte[(Height * Width) / 4];
		WaterFoamMask = new byte[(Height * Width) / 4];
		for (int i = 0; i <= WaterDepthBiasMask.Length - 1; i++)
		{
			WaterDepthBiasMask[i] = 127;
			WaterFlatnessMask[i] = 255;
			WaterFoamMask[i] = 0;
		}

		//Version
		VersionMajor = 2;
		VersionMinor = 60;

		HeightScale = 0.0078125f;


		//Unknown Values

		Unknown8 = 0;
		Unknown10 = -1091567891;
		Unknown11 = 2;
		Unknown12 = 0;
		Unknown13 = 0;
		Unknown14 = 0;
		Unknown15 = 0;

		//Minimap Colors (Default)
		MinimapContourInterval = 24;
		MinimapDeepWaterColor = new Color(71, 140, 181);
		MinimapContourColor = new Color(112, 112, 112);
		MinimapShoreColor = new Color(140, 201, 224);
		MinimapLandStartColor = new Color(117, 99, 107);
		MinimapLandEndColor = new Color(206, 206, 176);
	}


	public Map()
	{

	}

	const double HeightResize = 128.0 * 256.0; //512 * 40;
	public Map(int _Width, int _Height, int InitialHeight, bool _Water, int WaterLevel, int DepthLevel, int AbyssLevel)
	{
		Width = _Width;
		Height = _Height;

		int multiplier = 1;
		if (Width >= 1000 || Height >= 1000) {
			multiplier = 1;
		}

		if (Width >= 2000 || Height >= 2000) {
			multiplier = 1;
		}

		PreviewTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
		TexturemapTex = new Texture2D(multiplier * Width, multiplier * Height, TextureFormat.RGBA32, false);
		TexturemapTex2 = new Texture2D(multiplier * Width, multiplier * Height, TextureFormat.RGBA32, false);
		NormalmapTex = new Texture2D(Width, Height, TextureFormat.DXT5, false);
		WatermapTex = new Texture2D(Width, Height, TextureFormat.DXT5, false);
		UncompressedWatermapTex = new Texture2D(WatermapTex.width, WatermapTex.height, TextureFormat.RGBA32, false);
		UncompressedNormalmapTex = new Texture2D(NormalmapTex.width, NormalmapTex.height, TextureFormat.RGBA32, false);
		WaterDataTexture = new Texture2D(Width, Height, TextureFormat.RGB24, false);

		Color SplatTextureColor = new Color(0, 0, 0, 0);
		Color[] Pixels = new Color[multiplier * multiplier * Width * Height];
		for (int i = 0; i < Pixels.Length; i++)
			Pixels[i] = SplatTextureColor;

		TexturemapTex.SetPixels(Pixels);
		TexturemapTex2.SetPixels(Pixels);

		TexturemapTex.Apply();
		TexturemapTex2.Apply();

		Pixels = new Color[Width * Height];
		Color Bump = new Color(0, 0, 1, 1);
		for (int i = 0; i < Pixels.Length; i++)
			Pixels[i] = Bump;

		UncompressedNormalmapTex.SetPixels(Pixels);



		PreviewTextHeader = DefaultScmapHeaders.Current.PreviewTextHeader;
		TextureMapHeader = DefaultScmapHeaders.Current.TextureMapHeader;
		TextureMap2Header = DefaultScmapHeaders.Current.TextureMap2Header;
		NormalmapHeader = DefaultScmapHeaders.Current.NormalmapHeader;
		WatermapHeader = DefaultScmapHeaders.Current.WatermapHeader;

		TerrainTypeData = new byte[Height * Width];
		//		TerrainTypeData = new List<byte>();
		HeightmapData = new ushort[(Height + 1) * (Width + 1)];
		WaterDepthBiasMask = new byte[(Height * Width) / 4];
		WaterFlatnessMask = new byte[(Height * Width) / 4];
		WaterFoamMask = new byte[(Height * Width) / 4];
		for (int i = 0; i < WaterDepthBiasMask.Length; i++)
		{
			WaterDepthBiasMask[i] = 127;
			WaterFlatnessMask[i] = 255;
			WaterFoamMask[i] = 0;
		}


		float HeightConversion = InitialHeight / 25.6f;
		HeightConversion /= 10f;


		ushort InitialHeightValue = (ushort)((HeightConversion * HeightResize) + 0.5f);
		Debug.Log(InitialHeightValue);

		for (int i = 0; i < HeightmapData.Length; i++)
		{
			HeightmapData[i] = InitialHeightValue;
		}


		//Version
		VersionMajor = 2;
		VersionMinor = 60;

		HeightScale = 0.0078125f;

		TexPathBackground = "/textures/environment/blackbackground.dds";
		TexPathSkyCubemap = "/textures/environment/defaultenvcube.dds";
		EnvCubemapsName = new string[1];
		EnvCubemapsFile = new string[1];
		EnvCubemapsName[0] = "<default>";
		EnvCubemapsFile[0] = "/textures/environment/defaultenvcube.dds";

		Bloom = 0.03f;

		TerrainShader = "TTerrainXP";
		LightingMultiplier = 1.3f;
		SunDirection = new Vector3(-0.616f, 0.559f, -0.554f).normalized;
		SunAmbience = new Vector3(0.41f, 0.44f, 0.55f);
		SunColor = new Vector3(0.95f, 0.84f, 0.76f);
		ShadowFillColor = Vector3.zero;
		SpecularColor = new Vector4(0.1f, 0.1f, 0.1f, 0.1f);


		FogColor = new Vector3(0.37f, 0.50f, 0.61f);
		FogStart = 0;
		FogEnd = 800;
		Unknown10 = -1091567891;
		Unknown11 = 2;
		Unknown12 = 0;
		Unknown7 = 13153;
		Unknown8 = 4;
		Unknown14 = -8.3f;
		Unknown15 = 0;
		Layers = new List<Layer>();

		{
			// 0
			Layer NewLayer = new Layer();
			NewLayer.PathTexture = "env/paradise/layers/sand000_albedo.dds";
			NewLayer.PathNormalmap = "env/paradise/layers/sand000_normals.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 1
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 2
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 3
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 4
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 5
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 6
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 7
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 8
			NewLayer = new Layer();
			NewLayer.PathTexture = "";
			NewLayer.PathNormalmap = "env/tundra/layers/tund_none_normal.dds";
			NewLayer.ScaleTexture = 4;
			NewLayer.ScaleNormalmap = 4;
			Layers.Add(NewLayer);

			// 9
			NewLayer = new Layer();
			NewLayer.PathTexture = "env/lava/layers/macrotexture000_albedo.dds";
			NewLayer.PathNormalmap = "";
			NewLayer.ScaleTexture = 180;
			NewLayer.ScaleNormalmap = 1;
			Layers.Add(NewLayer);
		}


		Water = new WaterShader();
		Water.Defaults();

		Water.HasWater = _Water;
		Water.Elevation = WaterLevel;
		Water.ElevationDeep = DepthLevel;
		Water.ElevationAbyss = AbyssLevel;
		//Unknown Values

		Unknown8 = 0;
		Unknown10 = -1091567891;
		Unknown11 = 2;
		Unknown12 = 0;
		Unknown13 = 0;
		Unknown14 = 0;
		Unknown15 = 0;

		//Minimap Colors (Default)
		MinimapContourInterval = 24;
		MinimapDeepWaterColor = new Color(71, 140, 181);
		MinimapContourColor = new Color(112, 112, 112);
		MinimapShoreColor = new Color(140, 201, 224);
		MinimapLandStartColor = new Color(117, 99, 107);
		MinimapLandEndColor = new Color(206, 206, 176);
	}



	public int HeightmapId(int x, int y)
	{
		return (x + y * (Width + 1));
	}

	public ushort GetHeight(int x, int y)
	{
		return HeightmapData[HeightmapId(x, y)];
	}
	public void SetHeight(int x, int y, ushort value)
	{
		//Debug.Log("change value " + value);
		HeightmapData[HeightmapId(x, y)] = value;
	}

	public byte GetTerrainTypeValue(int x, int y)
	{
		return TerrainTypeData[(y * Width) + x];
	}
	public void SetTerrainTypeValue(int x, int y, byte value)
	{
		TerrainTypeData[(y * Width) + x] = value;
	}


	public void Clear()
	{
		//    if (PreviewBitmap != null) { PreviewBitmap.Dispose(); PreviewBitmap = null; }
		if (PreviewTex != null) { PreviewTex = null; }
		if (TexturemapTex != null) { TexturemapTex = null; }
		if (TexturemapTex2 != null) { TexturemapTex2 = null; }
		if (NormalmapTex != null) { NormalmapTex = null; }

		HeightmapData = new ushort[0];

		Width = 0;
		Height = 0;

		TexPathBackground = "";
		TexPathSkyCubemap = "";
		EnvCubemapsName = new string[0];
		EnvCubemapsFile = new string[0];

		Water.Clear();

		WaterFoamMask = new byte[0];
		WaterFlatnessMask = new byte[0];
		WaterDepthBiasMask = new byte[0];
		Layers.Clear();
	}


	// The .scmap reader and writer (HazardX) are gone: maps are Sanctuary
	// .sanmap, read and written by Sanctuary.SanctuaryMapLoader / Saver.
	public Texture2D UncompressedWatermapTex;
	public Texture2D UncompressedNormalmapTex;

	public static Color Int32ToColor(int data, bool floatRange = true)
	{
		int r = (data) & 0xFF;
		int g = (data >> 8) & 0xFF;
		int b = (data >> 16) & 0xFF;
		int a = (data >> 24) & 0xFF;
		if (floatRange)
			return new Color(r, g, b, a);
		else
			return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
	}

	/*private void CopyStream(System.IO.Stream Source, System.IO.Stream Target)
    {
        byte[] buffer = new byte[2049];
        int read = 0;
        do
        {
            read = Source.Read(buffer, 0, buffer.Length);
            if (read > 0)
                Target.Write(buffer, 0, read);
        } while ((read > 0));
    }*/


}