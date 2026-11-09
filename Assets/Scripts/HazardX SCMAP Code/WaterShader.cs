// ***************************************************************************************
// * SCMAP Loader
// * Copyright Unknown
// * Filename: WaterShader.cs
// * Source: http://www.hazardx.com/details.php?file=82
// ***************************************************************************************


using System;
using UnityEngine;

[System.Serializable]
public class WaterShader
{

	public bool HasWater;
    public float Elevation;
    public float ElevationDeep;
    public float ElevationAbyss;


    public Vector3 SurfaceColor;
    public Vector2 ColorLerp;
    public float RefractionScale;
    public float FresnelBias;
    public float FresnelPower;

    public float UnitReflection;
    public float SkyReflection;

    public float SunShininess;
    public float SunStrength;
    public Vector3 SunDirection;
    public Vector3 SunColor;
    public float SunReflection;
    public float SunGlow;

    public string TexPathCubemap;
    public string TexPathWaterRamp;

    public WaveTexture[] WaveTextures;

    public void Clear()
    {
        TexPathCubemap = "";
        TexPathWaterRamp = "";
        WaveTextures = new WaveTexture[4];
    }

    public void Defaults()
    {
        HasWater = true;

        Elevation = 30f;
        ElevationDeep = 25f;
        ElevationAbyss = 15f;

        SurfaceColor = new Vector3(0.2f, 0.2f, 0.2f);
        ColorLerp = new Vector2(0.064f, 0.064f);
        RefractionScale = 0.38f;
        FresnelBias = 0f;
        FresnelPower = 0f;
        UnitReflection = 0.5f;
        SkyReflection = 1f;
        SunShininess = 80f;
        SunStrength = 0f;
        SunDirection = new Vector3(0.09954818f, -0.9626309f, 0.2518569f);
        SunColor = new Vector3(0.52f, 0.47f, 0.35f);
        SunReflection = 1f;
        SunGlow = 0f;

        TexPathCubemap = "/textures/environment/skycube_evergreen01a.dds";
        TexPathWaterRamp = "/textures/engine/waterramp.dds";

        WaveTextures = new WaveTexture[4];
        WaveTextures[0] = new WaveTexture();
        WaveTextures[0].NormalMovement = new Vector2(0f, -0.012f);
        WaveTextures[0].NormalRepeat = 0.05f;
        WaveTextures[0].TexPath = "/textures/engine/waves1_400m.dds";
        WaveTextures[1] = new WaveTexture();
        WaveTextures[1].NormalMovement = new Vector2(-0.00130236f, 0.00738606f);
        WaveTextures[1].NormalRepeat = 0.167f;
        WaveTextures[1].TexPath = "/textures/engine/waves1_120m.dds";
        WaveTextures[2] = new WaveTexture();
        WaveTextures[2].NormalMovement = new Vector2(-0.003f, 0.00519615f);
        WaveTextures[2].NormalRepeat = 0.5f;
        WaveTextures[2].TexPath = "/textures/engine/waves1_40m.dds";
        WaveTextures[3] = new WaveTexture();
        WaveTextures[3].NormalMovement = new Vector2(-0.00105f, -0.006f);
        WaveTextures[3].NormalRepeat = 0.8f;
        WaveTextures[3].TexPath = "/textures/engine/waves1_120m.dds";
    }
    
}