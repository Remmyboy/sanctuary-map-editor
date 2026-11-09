// Editor display of Sanctuary props: albedo with alpha cutout (foliage),
// lit by the same globals as the terrain (SunDirection, SunColor,
// SunAmbience, LightingMultiplier - set by ScmapEditor.UpdateLighting), GPU
// instancing so a map's tens of thousands of props draw in a few calls.
// In Resources so builds always include it. Not the game's prop shader: no
// normal/mask maps, no wind.
Shader "Sanctuary/Prop"
{
	Properties
	{
		_MainTex ("Albedo", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
		[Toggle] _AlphaTest ("Alpha test", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma target 3.5
			#include "UnityCG.cginc"

			float4 SunDirection;
			float4 SunColor;
			float4 SunAmbience;
			float LightingMultiplier;

			sampler2D _MainTex;
			fixed4 _Color;
			half _Cutoff;
			half _AlphaTest;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
			};

			v2f vert (appdata_base v)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;
				o.normal = UnityObjectToWorldNormal(v.normal);
				return o;
			}

			fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
			{
				fixed4 c = tex2D(_MainTex, i.uv) * _Color;
				if (_AlphaTest > 0.5)
					clip(c.a - _Cutoff);
				float3 n = normalize(i.normal) * (facing > 0 ? 1 : -1);
				float ndl = saturate(dot(n, normalize(SunDirection.xyz)));
				float mult = LightingMultiplier > 0 ? LightingMultiplier : 1;
				float3 light = SunColor.rgb * ndl * mult + SunAmbience.rgb;
				return fixed4(c.rgb * light, 1);
			}
			ENDCG
		}
	}
}
