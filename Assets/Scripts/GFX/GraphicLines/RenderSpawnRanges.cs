using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Selection;

public class RenderSpawnRanges : MonoBehaviour
{

	static RenderSpawnRanges Instance;

	private void Awake()
	{
		Instance = this;
	}

	public Material lineMaterial;

	public Color BuildRangeColor;
	public Color AttackRangeColor;

	public float[] UtilityRanges;
	public float[] AtackRanges;

	public void OnRenderObject()
	{
		if (PreviewTex.IsPreview || !MapLuaParser.IsMapLoaded)
			return;

		if (!Markers.MarkersControler.Current.MarkerLayersSettings.SpawnRanges)
			return;

		lineMaterial.SetPass(0);

		var scenario = MapLuaParser.Current.ScenarioLuaFile.Data;
		for (int c = 0; c < scenario.Configurations.Length; c++)
		{
			for(int t = 0; t < scenario.Configurations[c].Teams.Length; t++)
			{
				for(int a = 0; a < scenario.Configurations[c].Teams[t].Armys.Count; a++)
				{
					MapLua.SaveLua.Marker ArmyMarker = MapLua.SaveLua.GetMarker(scenario.Configurations[c].Teams[t].Armys[a].Name);

					if (ArmyMarker != null && ArmyMarker.MarkerObj != null && ArmyMarker.MarkerType == MapLua.SaveLua.Marker.MarkerTypes.BlankMarker)
					{
						Vector3 pos = ArmyMarker.MarkerObj.transform.localPosition;
						for (int i = 0; i < UtilityRanges.Length; i++)
						{
							DrawDottedCircle(pos, UtilityRanges[i], BuildRangeColor);
						}
						for (int i = 0; i < AtackRanges.Length; i++)
						{
							DrawDottedCircle(pos, AtackRanges[i], AttackRangeColor);
						}
					}
				}
			}
		}
	}

	static readonly float DoublePI = 2 * Mathf.PI;

	/// Dotted ring on the ground (moved here from the FA unit-range renderer).
	public static void DrawDottedCircle(Vector3 center, float radius, Color col)
	{
		if (radius <= 0)
			return;
		GL.PushMatrix();
		GL.Begin(GL.LINES);
		GL.Color(col);
		int steps = Mathf.RoundToInt((DoublePI * radius) * 10f);
		if (steps < 12) steps = 12;
		else if (steps % 4 != 0) steps += 4 - (steps % 4);
		float step = Mathf.Clamp((2f * Mathf.PI) / steps, 0.005f, 0.1f);
		for (float theta = 0.0f; theta < DoublePI; theta += step)
			GL.Vertex3(Mathf.Cos(theta) * radius + center.x, center.y, Mathf.Sin(theta) * radius + center.z);
		GL.End();
		GL.PopMatrix();
	}
}
