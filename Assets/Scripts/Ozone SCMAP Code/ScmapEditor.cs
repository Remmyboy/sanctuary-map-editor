// ***************************************************************************************
// * SCmap editor
// * Set Unity objects and scripts using data loaded from Scm
// ***************************************************************************************

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityStandardAssets.ImageEffects;
//using UnityEngine.PostProcessing;

public partial class ScmapEditor : MonoBehaviour
{

	public static ScmapEditor Current;

	[Header("Connections")]
	public Camera Cam;
	public Terrain Teren;
	[System.NonSerialized]
	public TerrainData Data;
	public Transform WaterLevel;
	public Light Sun;
	public ProceduralSkybox Skybox;
	public Material TerrainMaterial;
	public Material WaterMaterial;
	public Cubemap DefaultWaterSky;
	//public PostProcessingProfile PostProcessing;
	public BloomOptimized BloomOpt;
	public BloomOptimized BloomOptPreview;
	public PreviewTex PreviewRenderer;
	public TextAsset DefaultSkybox;

	[Header("Loaded variables")]
	public TerrainTexture[] Textures; // Loaded textures
	public Map map; // Loaded Scmap data
	//public SkyboxData.SkyboxValues DefaultSkyboxData;
	public Cubemap CurrentEnvironmentCubemap;



	public const float MapHeightScale = 2048;


	// Stratum Layer
	[System.Serializable]
	public class TerrainTexture
	{
		public Texture2D Albedo;
		public Texture2D Normal;
		public Vector2 Tilling = Vector2.one;
		//Scmap Data
		public string AlbedoPath;
		public string NormalPath;
		public float AlbedoScale;
		public float NormalScale;
	}


	void Awake()
	{
		Current = this;
		Data = Teren.terrainData;
	}


	void Start()
	{

		Grid = false;
		UpdateGrid();
		heightsLength = 10;
		heights = new float[10, 10];
		RestartTerrainAsset();
	}

	public void UpdateLighting()
	{
		Vector3 SunDIr = new Vector3(-map.SunDirection.x, -map.SunDirection.y, map.SunDirection.z);
		Sun.transform.rotation = Quaternion.LookRotation(SunDIr);

		/*BloomModel.Settings Bs = PostProcessing.bloom.settings;
		Bs.bloom.intensity = map.Bloom * 10;
		PostProcessing.bloom.settings = Bs;*/

		BloomOpt.intensity = map.Bloom * 4;
		BloomOptPreview.intensity = map.Bloom * 4;

		RenderSettings.fog = false; // We need to disable the unity default fog
		RenderSettings.fogColor = new Color(map.FogColor.x, map.FogColor.y, map.FogColor.z, 1);
		RenderSettings.fogStartDistance = map.FogStart * 0.1f;
		RenderSettings.fogEndDistance = map.FogEnd * 0.1f;

        Shader.SetGlobalVector("ShadowFillColor", map.ShadowFillColor);
        Shader.SetGlobalFloat("LightingMultiplier", map.LightingMultiplier);
        Shader.SetGlobalVector("SunDirection", map.SunDirection);
        Shader.SetGlobalVector("SunAmbience", map.SunAmbience);
        Shader.SetGlobalVector("SunColor", map.SunColor);
        Shader.SetGlobalVector("SpecularColor", map.SpecularColor);
	}


	/// Builds the terrain, water and materials from `map`. Shared by the FA
	/// .scmap loader above and Sanctuary.SanctuaryMapLoader.
	public void ApplyLoadedMap()
	{
		UpdateLighting();

		if (map.VersionMinor >= 60 && !Sanctuary.SanctuaryMapLoader.IsActive)
		{
			map.AdditionalSkyboxData.Data.UpdateSize();
		}
		else
		{
			LoadDefaultSkybox();
		}



		//Shader
        MapLuaParser.Current.EditMenu.MapInfoMenu.ShaderName.SetValue(map.TerrainShader);
        ToogleShader();
	
		// Set Variables
		int xRes = MapLuaParser.Current.ScenarioLuaFile.Data.Size[0];
		int zRes = MapLuaParser.Current.ScenarioLuaFile.Data.Size[1];
		float HalfxRes = xRes / 10f;
		float HalfzRes = zRes / 10f;


		TerrainMaterial.SetTexture("_TerrainNormal", map.UncompressedNormalmapTex);
		WaterMaterial.SetTexture("UtilitySamplerC", map.UncompressedWatermapTex);
		Shader.SetGlobalFloat("_WaterScaleX", xRes);
		Shader.SetGlobalFloat("_WaterScaleZ", xRes);

		//*****************************************
		// ***** Set Terrain proportives
		//*****************************************

		LoadHeights();


		// Load Stratum Textures Paths
		LoadStratumScdTextures();
		MapLuaParser.Current.InfoPopup.Show(true, "Loading map...");


		WaterLevel.transform.localScale = new Vector3(HalfxRes, 1, HalfzRes);
		TerrainMaterial.SetFloat("TerrainScale", (float) 1.0 / xRes);
        Shader.SetGlobalFloat("_GridScale", HalfxRes);
		TerrainMaterial.SetTexture("UtilitySamplerC", map.UncompressedWatermapTex);


		// Environment reflections: the editor's default sky (the FA map's
		// cubemaps came from Forged Alliance's files).
		Shader.SetGlobalTexture("environmentSampler", DefaultWaterSky);

		SetWaterTextures();

		SetWater();

		Teren.gameObject.layer = 8;

		SetTextures();


		if (Slope)
		{
			ToogleSlope(Slope);
		}
	}

	public void LoadHeights()
	{
		if (Teren) DestroyImmediate(Teren.gameObject);

		Teren = Terrain.CreateTerrainGameObject(Data).GetComponent<Terrain>();
		Teren.gameObject.name = "TERRAIN";
#if UNITY_2019_2_OR_NEWER

#else
		Teren.materialType = Terrain.MaterialType.Custom;
#endif
		Teren.materialTemplate = TerrainMaterial;
		Teren.heightmapPixelError = 2f;
		Teren.basemapDistance = 10000;
		Teren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		Teren.drawTreesAndFoliage = false;
		Teren.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

		int xRes = MapLuaParser.Current.ScenarioLuaFile.Data.Size[0];
		int zRes = MapLuaParser.Current.ScenarioLuaFile.Data.Size[1];
		float yRes = (float)map.HeightScale;

		float HalfxRes = xRes / 10f;
		float HalfzRes = zRes / 10f;

		Data.heightmapResolution = (int)(xRes + 1);
		TerrainHeight = 1f / yRes;
		TerrainHeight *= 0.1f;
		TerrainHeight *= 2;

		if (Sanctuary.SanctuaryMapLoader.IsActive)
		{
			// The .sanmap `height` is the metres a raw value of 65536 reaches;
			// the game feeds raw/65536 straight into Unity. Same here, at the
			// editor's 1/10 scale, so the full 16-bit range is kept.
			TerrainHeight = Sanctuary.SanctuaryMapLoader.Folder.Map.Height / 10f;
			Data.size = new Vector3(HalfxRes, TerrainHeight, HalfzRes);
			Data.RefreshPrototypes();
			Teren.Flush();
			Teren.UpdateGIMaterials();
			SyncHeightmap();
			Teren.transform.localPosition = new Vector3(0, 0, -HalfzRes);

			heightsLength = xRes + 1;
			heights = Sanctuary.SanctuaryMapLoader.Folder.Heightmap.ToUnityHeights();
			ApplyHeightmap(false);
			// What Unity actually stores, so saving can tell edited cells
			// from untouched ones without depending on its internal precision.
			Sanctuary.SanctuaryMapLoader.TerrainAtLoad = Data.GetHeights(0, 0, Data.heightmapResolution, Data.heightmapResolution);

			GenerateControlTex.StopAllTasks();
			GenerateControlTex.GenerateNormal();
			GenerateControlTex.GenerateWater();
			return;
		}

		Data.size = new Vector3(
			HalfxRes,
			TerrainHeight,
			HalfzRes
			);

		Data.RefreshPrototypes();
		Teren.Flush();
		Teren.UpdateGIMaterials();
		SyncHeightmap();

		Teren.transform.localPosition = new Vector3(0, 0, -HalfzRes);

		heightsLength = (int)Mathf.Max((map.Height + 1), (map.Width + 1));
		heights = new float[heightsLength, heightsLength];

		float HeightWidthMultiply = (map.Height / (float)map.Width);

		int y = 0;
		int x = 0;
		int localY = 0;

		for (y = 0; y < heightsLength; y++)
		{
			for (x = 0; x < heightsLength; x++)
			{
				localY = (int)(((heightsLength - 1) - y) * HeightWidthMultiply);

				//heights[y, x] = (float)((((double)map.GetHeight(x, localY)) / HeightResize));
				heights[y, x] = (float)(map.GetHeight(x, localY) / HeightResize); // 65536.0 / 2.0 // 32768.0

				if (HeightWidthMultiply == 0.5f && y > 0 && y % 2f == 0)
				{
					heights[y - 1, x] = Mathf.Lerp(heights[y, x], heights[y - 2, x], 0.5f);
				}
			}
		}

		// Set terrain heights from heights array
		ApplyHeightmap(false);

		GenerateControlTex.StopAllTasks();
		GenerateControlTex.GenerateNormal();
		GenerateControlTex.GenerateWater();
	}



#region Water

	/// Stratum textures and masks of the open Sanctuary map (the FA env.scd
	/// loader that lived here is gone).
	public void LoadStratumScdTextures(bool Loading = true)
	{
		if (Loading && Sanctuary.SanctuaryMapLoader.IsActive)
			Sanctuary.SanctuaryMapLoader.ApplyStratumTextures();
	}

	/// Water display textures: a generated depth ramp and calm water (the
	/// game's water shading is not reproduced).
	public void SetWaterTextures()
	{
		Shader.SetGlobalTexture("WaterRampSampler", Sanctuary.SanctuaryMapLoader.WaterRamp());
		WaterMaterial.SetTexture("SkySampler", DefaultWaterSky);
		for (int n = 0; n < 4; n++)
			WaterMaterial.SetTexture("NormalSampler" + n, Sanctuary.SanctuaryMapLoader.CalmWaveNormal());
		WaterMaterial.SetVector("normalRepeatRate", Vector4.one);
	}
	public void SetWater()
	{
		WaterLevel.gameObject.SetActive(map.Water.HasWater);
		WaterLevel.transform.position = Vector3.up * (map.Water.Elevation / 10.0f);

		WaterMaterial.SetVector("waterColor", map.Water.SurfaceColor);
		WaterMaterial.SetVector("SunColor", map.Water.SunColor);
        WaterMaterial.SetVector("waterLerp", map.Water.ColorLerp);
		WaterMaterial.SetVector("SunDirection", map.Water.SunDirection);
		WaterMaterial.SetFloat("SunReflectionAmount", map.Water.SunReflection);
		WaterMaterial.SetFloat("SunShininess", map.Water.SunShininess);
		WaterMaterial.SetFloat("skyreflectionAmount", map.Water.SkyReflection);
        WaterMaterial.SetFloat("refractionScale", map.Water.RefractionScale);

		/*
		for (int w = 0; w < map.WaveGenerators.Count; w++)
		{
			map.WaveGenerators[w].Position.y = map.Water.Elevation;
		}
		*/

		Shader.SetGlobalFloat("WaterElevation", map.Water.Elevation / 10.0f);
		Shader.SetGlobalInt("_Water", map.Water.HasWater ? 1 : 0);
	}

#endregion

#region Textures
	public void SetTextures(int Layer = -1)
	{
		if (Layer < 0)
		{
			TerrainMaterial.SetTexture("UtilitySamplerA", map.TexturemapTex);
			if (Textures[5].Albedo || Textures[6].Albedo || Textures[7].Albedo || Textures[8].Albedo)
				TerrainMaterial.SetTexture("UtilitySamplerB", map.TexturemapTex2);
			
			TerrainMaterial.SetFloat("Stratum0AlbedoTile", map.Width / Textures[1].AlbedoScale);
            TerrainMaterial.SetFloat("Stratum1AlbedoTile", map.Width / Textures[2].AlbedoScale);
            TerrainMaterial.SetFloat("Stratum2AlbedoTile", map.Width / Textures[3].AlbedoScale);
            TerrainMaterial.SetFloat("Stratum3AlbedoTile", map.Width / Textures[4].AlbedoScale);
            TerrainMaterial.SetFloat("Stratum4AlbedoTile", map.Width / Textures[5].AlbedoScale);
            TerrainMaterial.SetFloat("Stratum5AlbedoTile", map.Width / Textures[6].AlbedoScale);
            TerrainMaterial.SetFloat("Stratum6AlbedoTile", map.Width / Textures[7].AlbedoScale);

            TerrainMaterial.SetFloat("Stratum0NormalTile", map.Width / Textures[1].NormalScale);
            TerrainMaterial.SetFloat("Stratum1NormalTile", map.Width / Textures[2].NormalScale);
            TerrainMaterial.SetFloat("Stratum2NormalTile", map.Width / Textures[3].NormalScale);
            TerrainMaterial.SetFloat("Stratum3NormalTile", map.Width / Textures[4].NormalScale);
            TerrainMaterial.SetFloat("Stratum4NormalTile", map.Width / Textures[5].NormalScale);
            TerrainMaterial.SetFloat("Stratum5NormalTile", map.Width / Textures[6].NormalScale);
            TerrainMaterial.SetFloat("Stratum6NormalTile", map.Width / Textures[7].NormalScale);
            
            GenerateArrays();
		}

		if (Layer <= 0)
		{
			TerrainMaterial.SetFloat("LowerAlbedoTile", map.Width / Textures[0].AlbedoScale);
			TerrainMaterial.SetFloat("LowerNormalTile", map.Width / Textures[0].NormalScale);
			TerrainMaterial.SetTexture("LowerAlbedoSampler", Textures[0].Albedo);
			TerrainMaterial.SetTexture("LowerNormalSampler", Textures[0].Normal);
		}

		if (Layer >= 1 && Layer <= 7)
		{
			string IdStrig = (Layer - 1).ToString();
			TerrainMaterial.SetFloat("Stratum" + IdStrig + "AlbedoTile", map.Width / Textures[Layer].AlbedoScale);
			TerrainMaterial.SetFloat("Stratum" + IdStrig + "NormalTile", map.Width / Textures[Layer].NormalScale);
			GenerateArrays();
		}
		
		if (Layer == 8 || Layer < 0)
		{
			TerrainMaterial.SetFloat("Stratum7AlbedoTile", map.Width / Textures[8].AlbedoScale);
			TerrainMaterial.SetFloat("Stratum7NormalTile", map.Width / Textures[8].NormalScale);
			TerrainMaterial.SetTexture("Stratum7AlbedoSampler", Textures[8].Albedo);
			TerrainMaterial.SetTexture("Stratum7NormalSampler", Textures[8].Normal);
		}

		if (Layer == 9 || Layer < 0)
		{
			TerrainMaterial.SetFloat("UpperAlbedoTile", map.Width / Textures[9].AlbedoScale);
			TerrainMaterial.SetTexture("UpperAlbedoSampler", Textures[9].Albedo);
		}
	}

	// We need an array texture to reduce the amount of samplers in the shader,
	// but we exclude stratum 7 as it might hold very large textures.
	void GenerateArrays()
	{
		int ArraySize = 4;
		int ArrayMipMapCount = 3;
		for (int i = 0; i < 7; i++)
		{
			Texture2D stratumTexture = Textures[i + 1].Albedo;
			ArraySize = FindArraySize(stratumTexture, ArraySize);
			ArrayMipMapCount = FindArrayMipMapCount(stratumTexture, ArrayMipMapCount);
		}

		Texture2DArray AlbedoArray = new Texture2DArray(ArraySize, ArraySize, 7, TextureFormat.RGBA32, true);

		for (int i = 0; i < 7; i++)
		{
			Texture2D stratumAlbedo = Textures[i + 1].Albedo;
			if(!stratumAlbedo.isReadable)
			{
				if(stratumAlbedo != Texture2D.whiteTexture)
					Debug.LogWarning("Assigned albedo texture is not readable! " + Textures[i + 1].AlbedoPath);
				continue;
			}

			if (stratumAlbedo.width <= 4 && stratumAlbedo.height <= 4)
				continue;

			stratumAlbedo = ScaleStratumTextureIfNeeded(stratumAlbedo, ArraySize, ArrayMipMapCount);

			for (int m = 0; m < ArrayMipMapCount; m++)
			{
				AlbedoArray.SetPixels(stratumAlbedo.GetPixels(m), i, m);
			}
		}

		AlbedoArray.filterMode = FilterMode.Bilinear;
		AlbedoArray.anisoLevel = 4;
		AlbedoArray.Apply(false);
		TerrainMaterial.SetTexture("_StratumAlbedoArray", AlbedoArray);

		ArraySize = 4;
		ArrayMipMapCount = 3;
		for (int i = 0; i < 7; i++)
		{
			Texture2D stratumTexture = Textures[i + 1].Normal;
			ArraySize = FindArraySize(stratumTexture, ArraySize);
			ArrayMipMapCount = FindArrayMipMapCount(stratumTexture, ArrayMipMapCount);
		}

		Texture2DArray NormalArray = new Texture2DArray(ArraySize, ArraySize, 7, TextureFormat.RGBA32, true);

		for (int i = 0; i < 7; i++)
		{
			Texture2D stratumNormal = Textures[i + 1].Normal;
			if (!stratumNormal.isReadable)
			{
				Debug.LogWarning("Assigned normal texture is not readable! " + Textures[i + 1].NormalPath);
				continue;
			}

			if (stratumNormal == null)
				continue;

			stratumNormal = ScaleStratumTextureIfNeeded(stratumNormal, ArraySize, ArrayMipMapCount);

			for (int m = 0; m < stratumNormal.mipmapCount; m++)
			{
				NormalArray.SetPixels(stratumNormal.GetPixels(m), i, m);
			}
		}

		NormalArray.filterMode = FilterMode.Bilinear;
		NormalArray.anisoLevel = 2;
		NormalArray.Apply(false);
		TerrainMaterial.SetTexture("_StratumNormalArray", NormalArray);
	}

	private static int FindArraySize(Texture2D stratumTexture, int ArraySize)
	{
		if (stratumTexture == null)
			return ArraySize;
		if (stratumTexture.width > ArraySize)
		{
			ArraySize = stratumTexture.width;
		}
		if (stratumTexture.height > ArraySize)
		{
			ArraySize = stratumTexture.height;
		}

		return ArraySize;
	}

	private static int FindArrayMipMapCount(Texture2D stratumTexture, int ArrayMipMapCount)
	{
		if (stratumTexture.mipmapCount > ArrayMipMapCount)
		{
			ArrayMipMapCount = stratumTexture.mipmapCount;
		}

		if (stratumTexture.mipmapCount > ArrayMipMapCount)
		{
			ArrayMipMapCount = stratumTexture.mipmapCount;
		}

		return ArrayMipMapCount;
	}

	/// A copy of `stratumTexture` at ArraySize square with a full mip chain,
	/// ready for Texture2DArray.SetPixels. Rebuilding the chain with
	/// Apply(true) instead of copying the source's own levels works for any
	/// mix of sizes and mip counts (the old copy indexed past the source's
	/// levels when a layer was small, e.g. a 4x4 placeholder).
	private static Texture2D ScaleStratumTextureIfNeeded(Texture2D stratumTexture, int ArraySize, int ArrayMipMapCount)
	{
		int fullChain = 1 + (int)Mathf.Log(ArraySize, 2);
		if (stratumTexture.width == ArraySize && stratumTexture.height == ArraySize && stratumTexture.mipmapCount == fullChain)
			return stratumTexture;

		Texture2D scaled = stratumTexture.width == ArraySize && stratumTexture.height == ArraySize
			? stratumTexture
			: TextureScale.Bilinear(stratumTexture, ArraySize, ArraySize);
		var result = new Texture2D(ArraySize, ArraySize, TextureFormat.RGBA32, true, false);
		result.SetPixels(scaled.GetPixels(0), 0);
		result.Apply(true);
		return result;
	}

	public void UpdateScales(int id)
	{
		if (id == 0)
		{
			TerrainMaterial.SetFloat("LowerAlbedoTile", map.Width / Textures[0].AlbedoScale);
			TerrainMaterial.SetFloat("LowerNormalTile", map.Width / Textures[0].NormalScale);
		}
		else if (id == 9)
		{
			TerrainMaterial.SetFloat("UpperAlbedoTile", map.Width / Textures[9].AlbedoScale);
			TerrainMaterial.SetFloat("UpperNormalTile", map.Width / Textures[9].NormalScale);
		}
		else
		{
			string IdStrig = (id - 1).ToString();
			TerrainMaterial.SetFloat("Stratum" + IdStrig + "AlbedoTile", map.Width / Textures[id].AlbedoScale);
			TerrainMaterial.SetFloat("Stratum" + IdStrig + "NormalTile", map.Width / Textures[id].NormalScale);
		}
	}
#endregion

#region Saving
	public static float TerrainHeight = 12.5f;
	//const double HeightResize = 128.0 * 256.0; //512 * 40;
	public const double HeightResize = 32768.0; //512 * 40;
	//public const double RoundingError = 0.5;
	public const float MaxElevation = 256;

#endregion

#region Clean
	public void RestartTerrainAsset()
	{
		int xRes = (int)(256 + 1);
		int zRes = (int)(256 + 1);
		int yRes = (int)(ScmapEditor.MaxElevation);
		heightsLength = xRes;
		heights = new float[xRes, zRes];

		// Set Terrain proportives
		Data.heightmapResolution = xRes;
		Data.size = new Vector3(
			256 / 10.0f,
			yRes / 10.0f,
			256 / 10.0f
			);

		if (map != null) WaterLevel.transform.localScale = new Vector3(map.Width * 0.1f, 1f, map.Height * 0.1f);
		if (Teren) Teren.transform.localPosition = new Vector3(-xRes / 20.0f, 1, -zRes / 20.0f);

		// Modify heights array data
		for (int y = 0; y < zRes; y++)
		{
			for (int x = 0; x < xRes; x++)
			{
				heights[x, y] = 0;
			}
		}

		// Set terrain heights from heights array
		Data.SetHeights(0, 0, heights);
	}

	public void LoadDefaultSkybox()
	{
		map.AdditionalSkyboxData = UnityEngine.JsonUtility.FromJson<SkyboxData>(DefaultSkybox.text);
		map.AdditionalSkyboxData.Data.UpdateSize();
	}

	public void UnloadMap()
	{
		Markers.MarkersControler.UnloadMarkers();
		GenerateControlTex.StopAllTasks();
	}
#endregion

#region Converters
	/// <summary>
	/// Convert Scmap position to editor world position
	/// </summary>
	/// <param name="MapPos"></param>
	/// <returns></returns>
	public static Vector3 ScmapPosToWorld(Vector3 MapPos)
	{
		Vector3 ToReturn = MapPos;

		// Position
		ToReturn.x = MapPos.x / 10f;
		ToReturn.z = -MapPos.z / 10f;

		// Height
		ToReturn.y = 1 * (MapPos.y / 10);

		return ToReturn;
	}

	public static float ScmapPosToWorld(float val)
	{
		return val / 10f;
	}

	/// <summary>
	/// Convert Editor world position to Scmap position
	/// </summary>
	/// <param name="MapPos"></param>
	/// <returns></returns>
	public static Vector3 WorldPosToScmap(Vector3 MapPos)
	{
		Vector3 ToReturn = MapPos;

		//Position
		ToReturn.x = MapPos.x * 10;
		ToReturn.z = MapPos.z * -10f;

		// Height
		ToReturn.y = MapPos.y * 10;

		return ToReturn;
	}

	public static Vector3 SnapToSmallGridCenter(Vector3 Pos)
	{

		Pos.x += 0.05f;
		Pos.z -= 0.05f;

		Pos.x *= 20;
		Pos.x = (int)(Pos.x + 0.0f);
		Pos.x /= 20.0f;

		Pos.z *= 20;
		Pos.z = (int)(Pos.z + 0.0f);
		Pos.z /= 20.0f;

		return Pos;
	}

	public static Vector3 SnapToSmallGrid(Vector3 Pos)
	{
		Pos.x *= 20;
		Pos.x = (int)(Pos.x + 0.0f);
		Pos.x /= 20.0f;

		Pos.z *= 20;
		Pos.z = (int)(Pos.z + 0.0f);
		Pos.z /= 20.0f;

		return Pos;
	}

	public static Vector3 SnapMarker(Vector3 Pos, int ID)
	{
		bool Water = ID == 1 || ID == 2 || ID == 3;

		return SnapToGridCenter(Pos, true, !Water);

	}

	public static Vector3 SnapToGridCenter(Vector3 Pos, bool SampleHeight = false, bool MinimumWaterLevel = false)
	{
		Pos.x += 0.1f;
		//Pos.z += 0.1f;

		Pos.x *= 10;
		Pos.x = (int)(Pos.x);
		Pos.x /= 10.0f;

		Pos.z *= 10;
		Pos.z = (int)(Pos.z);
		Pos.z /= 10.0f;

		Pos.x -= 0.05f;
		Pos.z -= 0.05f;

		if (SampleHeight)
			Pos.y = Current.Teren.SampleHeight(Pos);
		if (MinimumWaterLevel)
			Pos.y = Mathf.Clamp(Pos.y, GetWaterLevel(), 10000);
		return Pos;
	}

	public static Vector3 SnapToTerrain(Vector3 Pos, bool MinimumWaterLevel = false)
	{
		Pos.y = Current.Teren.SampleHeight(Pos);
		if (MinimumWaterLevel)
			Pos.y = Mathf.Clamp(Pos.y, GetWaterLevel(), 10000);
		return Pos;
	}

	public static Vector3 ClampToWater(Vector3 Pos)
	{
		if (Current.map.Water.HasWater)
			Pos.y = Mathf.Clamp(Pos.y, GetWaterLevel(), 10000);
		return Pos;
	}

	public static Vector3 SnapToGrid(Vector3 Pos)
	{
		Pos.x *= 10;
		Pos.x = (int)(Pos.x);
		Pos.x /= 10.0f;

		Pos.z *= 10;
		Pos.z = (int)(Pos.z);
		Pos.z /= 10.0f;

		return Pos;
	}

	public static float GetWaterLevel()
	{
		if (!Current.map.Water.HasWater)
			return 0;
		return Current.WaterLevel.localPosition.y;
	}

#endregion

#region Rendering
	[HideInInspector]
	public bool Grid;
	[HideInInspector]
	public GridTypes GridType;
	[HideInInspector]
	public bool Slope;

	public Texture[] GridTextures;

	public enum GridTypes
	{
		Build, General, AI
	}

	public bool ToogleCurrent()
	{
		Grid = !Grid;
		UpdateGrid();
		return Grid;
	}

	public void ToogleCenterLines(bool To)
	{
		TerrainMaterial.SetInteger("_Lines", To ? 1 : 0);
	}

	public void ToogleBuildGrid(bool To)
	{
		if (To)
			GridType = GridTypes.Build;
		Grid = To;
		UpdateGrid();
	}

	public void ToogleGeneraldGrid(bool To)
	{
		if (To)
			GridType = GridTypes.General;
		Grid = To;
		UpdateGrid();
	}

	public void ToogleAIGrid(bool To)
	{
		if (To)
			GridType = GridTypes.AI;
		Grid = To;
		UpdateGrid();
	}

	void UpdateGrid()
	{
		TerrainMaterial.SetTexture("_GridTexture", GridTextures[0]);
		TerrainMaterial.SetInteger("_Grid", Grid ? 1 : 0);
		TerrainMaterial.SetInteger("_GridType", (int) GridType);
	}

	public void ToggleTexturesView(bool on)
	{
		Shader.SetGlobalInt("_HideTextures", on ? 1 : 0);
	}
	
	public void ToggleNormalsView(bool on)
	{
		Shader.SetGlobalInt("_ShowNormals", on ? 1 : 0);
	}

	public void ToggleRoughnessView(bool on)
	{
		Shader.SetGlobalInt("_ShowRoughness", on ? 1 : 0);
	}
	
	public void ToggleAOView(bool on)
	{
		Shader.SetGlobalInt("_ShowAO", on ? 1 : 0);
	}
	
	public void ToggleMaskView(bool on)
	{
		Shader.SetGlobalInt("_ShowMask", on ? 1 : 0);
	}

	public void ToogleSlope(bool To)
	{
		Slope = To;
		TerrainMaterial.SetInteger("_Slope", Slope ? 1 : 0);
		if (To)
		{
			GenerateControlTex.Current.GenerateSlopeTexture();

		}

	}

	public void ToogleShader()
	{
		foreach (var localKeyword in TerrainMaterial.shader.keywordSpace.keywords)
		{
			TerrainMaterial.DisableKeyword(localKeyword);
		}
		TerrainMaterial.EnableKeyword(MapLuaParser.Current.EditMenu.MapInfoMenu.ShaderName.text);
		
		int shaderId;
		switch (MapLuaParser.Current.EditMenu.MapInfoMenu.ShaderName.text)
		{
			case "TTerrain":
				shaderId = -10;
				break;
			case "TTerrainXP":
				shaderId = -20;
				break;
			case "TTerrainXPExt":
				shaderId = 20;
				break;
			case "Terrain000":
				shaderId = 0;
				break;
			case "Terrain050":
				shaderId = 50;
				break;
			case "Terrain100":
				shaderId = 100;
				break;
			case "Terrain150":
				shaderId = 150;
				break;
			case "Terrain101":
				shaderId = 101;
				break;
			case "Terrain151":
				shaderId = 151;
				break;
			case "Terrain102":
				shaderId = 102;
				break;
			case "Terrain152":
				shaderId = 152;
				break;
			case "Terrain100B":
				shaderId = 110;
				break;
			case "Terrain150B":
				shaderId = 160;
				break;
			case "Terrain101B":
				shaderId = 111;
				break;
			case "Terrain151B":
				shaderId = 161;
				break;
			case "Terrain102B":
				shaderId = 112;
				break;
			case "Terrain152B":
				shaderId = 162;
				break;
			case "Terrain200":
				shaderId = 200;
				break;
			case "Terrain250":
				shaderId = 250;
				break;
			case "Terrain201":
				shaderId = 201;
				break;
			case "Terrain251":
				shaderId = 251;
				break;
			case "Terrain202":
				shaderId = 202;
				break;
			case "Terrain252":
				shaderId = 252;
				break;
			case "Terrain200B":
				shaderId = 210;
				break;
			case "Terrain250B":
				shaderId = 260;
				break;
			case "Terrain201B":
				shaderId = 211;
				break;
			case "Terrain251B":
				shaderId = 261;
				break;
			case "Terrain202B":
				shaderId = 212;
				break;
			case "Terrain252B":
				shaderId = 262;
				break;
			default:
				shaderId = -1;
				break;
		}

		if (shaderId >= 0)
        {
            Textures[8].AlbedoScale = 10000;  // Use terrain info texture
            Textures[8].NormalScale = 10000;  // Use terrain normal texture
            SetTextures(8);
            MapLuaParser.Current.EditMenu.TexturesMenu.ShaderTools.interactable = true;
            MapLuaParser.Current.EditMenu.TexturesMenu.ShaderTools.alpha = 1;
            if (shaderId >= 100)
            {
                Textures[9].AlbedoScale = 10000;  // Use PBR rendering on decals
            }
        }
		else
		{
			MapLuaParser.Current.EditMenu.TexturesMenu.ShaderTools.interactable = false;
			MapLuaParser.Current.EditMenu.TexturesMenu.ShaderTools.alpha = 0.7f;
		}
		
		if (shaderId >= 200)
		{
			MapLuaParser.Current.EditMenu.TexturesMenu.ShaderSettings.gameObject.SetActive(true);
			// arguments are: blurriness, contrast increase, cos(30), sin(30)
			map.SpecularColor = new Vector4(map.SpecularColor.x, 0.6f, 0.866f, 0.5f);
			Shader.SetGlobalVector("SpecularColor", map.SpecularColor);
		}
		else if (shaderId >= 100)
		{
			MapLuaParser.Current.EditMenu.TexturesMenu.ShaderSettings.gameObject.SetActive(false);
		}
		else if (shaderId == -10)  //TTerrain
		{
			MapLuaParser.Current.EditMenu.TexturesMenu.ShaderSettings.gameObject.SetActive(false);
		}
		else
		{
			MapLuaParser.Current.EditMenu.TexturesMenu.ShaderSettings.gameObject.SetActive(false);
		}
		
		Shader.SetGlobalInt("_ShaderID", shaderId);
	}
#endregion
}
