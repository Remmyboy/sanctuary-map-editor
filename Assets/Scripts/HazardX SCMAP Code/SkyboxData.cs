//********************************
// 
// * Procedural skybox from v60 scmap file for FAF Map Editor 
// * Copyright ozonexo3 2017
//
//********************************

using System.Collections;
using UnityEngine;

[System.Serializable]
public class SkyboxData
{
	public SkyboxValues Data;

	#region SkyboxData
	[System.Serializable]
	public class SkyboxValues
	{
		[Header("Sky dome")]
		public Vector3 Position = new Vector3(256, 0, 256);
		public float Scale = 2343;
		public float SubtractHeight = 1.256637f;
		//public Vector3 Scale = new Vector3(-35.45f, 1171.581f, 1.256637f); // X - horizonBegin, Y - Scale of sky dome, Z - subtract from vertex position.y
		public int SubdivisionsAxis = 16;
		public int SubdivisionsHeight = 6;

		[Header("Horizon")]
		public float HorizonHeight = 0;
		public float ZenithHeight = 174.5082f; // horizonEnd
		[ColorUsage(false, true)]
		public Color HorizonColor = new Color(0, 0, 0, 0);
		[ColorUsage(false, true)]
		public Color ZenithColor = new Color(0, 0, 0, 0);

		[Header("Decals atlas")]
		public float DecalGlowMultiplier = 0.1f; // Always 0.1
		public string Albedo = "/textures/environment/Decal_test_Albedo001.dds";
		public string Glow = "/textures/environment/Decal_test_Glow001.dds";
		public Planet[] Planets = new Planet[0];

		[Header("Cumulus?")]
		public Color32 MidRgbColor; // 3 bytes, always 0 == black?


		[Header("Cirrus")]
		public float CirrusMultiplier = 1.8f;
		[ColorUsage(false, true)]
		public Color CirrusColor = Color.white;
		public string CirrusTexture = "/textures/environment/cirrus000.dds";
		public Cirrus[] CirrusLayers;

		public float Clouds7; // Always 0

		[System.Serializable]
		public struct Planet
		{
			public Vector3 Position;
			public float Rotation; // Used in shader to rotate vertexes around center
			public Vector2 Scale;
			public Vector4 Uv;
		}

		[System.Serializable]
		public struct Cirrus
		{
			public Vector2 frequency;
			public float Speed;
			public Vector2 Direction;
		}

		public SkyboxValues()
		{

		}

		public SkyboxValues(SkyboxValues from)
		{
			CopyFrom(from);

		}

		public void CopyFrom(SkyboxValues from)
		{
			Position = from.Position;
			Scale = from.Scale;
			SubtractHeight = from.SubtractHeight;
			SubdivisionsAxis = from.SubdivisionsAxis;
			SubdivisionsHeight = from.SubdivisionsHeight;

			HorizonHeight = from.HorizonHeight;
			ZenithHeight = from.ZenithHeight;
			HorizonColor = from.HorizonColor;
			ZenithColor = from.ZenithColor;
			DecalGlowMultiplier = from.DecalGlowMultiplier;

			Albedo = from.Albedo;
			Glow = from.Glow;

			Planets = new Planet[from.Planets.Length];
			for (int i = 0; i < from.Planets.Length; i++)
			{
				Planets[i] = from.Planets[i];
			}

			CirrusMultiplier = from.CirrusMultiplier;
			CirrusColor = from.CirrusColor;

			CirrusTexture = from.CirrusTexture;

			CirrusLayers = from.CirrusLayers;
			Clouds7 = from.Clouds7;

			UpdateSize();
		}

		public void UpdateSize()
		{
			Position = ScmapEditor.WorldPosToScmap( MapLuaParser.Current.MapCenterPoint);
			Scale = Mathf.Max(ScmapEditor.Current.map.Width, ScmapEditor.Current.map.Height) * 2.288245f;

			ScmapEditor.Current.Skybox.LoadSkybox();
		}
	}
	#endregion

	#region Read/Write

	#endregion
}
