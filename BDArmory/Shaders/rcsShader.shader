// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/rcsShader" {

	Properties{
		_RCSCOLOR("RCSCOLOR", Color) = (1, 1, 1, 1)
		_LIGHTDIR("LIGHTDIR", Vector) = (0, 0, 1)
	}

		SubShader{

			Pass{

			CGPROGRAM

			// DEFINES
			#pragma vertex RCSVertexShader
			#pragma fragment RCSFragmentShader

			// INCLUDES
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#include "UnityStandardBRDF.cginc"


			// uniforms from properties
			float4 _RCSCOLOR;
			float3 _LIGHTDIR;

			struct VertexData {
				float4 position : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct Interpolators {
				float4 position : SV_POSITION;
				float3 normal : TEXCOORD1;
			};



			// Main Vertex Program
			Interpolators RCSVertexShader(VertexData v) {
				Interpolators i;
				i.position = UnityObjectToClipPos(v.position);
				i.normal = UnityObjectToWorldNormal(v.normal);
				//i.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return i;
			}



			// Main Fragment Program
			float4 RCSFragmentShader(Interpolators i) : SV_TARGET{
				float3 lightDir = _WorldSpaceLightPos0.xyz;
				float3 lightColor = _RCSCOLOR.rgb;
				float3 reflectionDir = reflect(-_LIGHTDIR, i.normal);
				return DotClamped(_LIGHTDIR, reflectionDir);
			}


			ENDCG

			} //PASS
		} //SUBSHADER

}
