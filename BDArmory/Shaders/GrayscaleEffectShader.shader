Shader "Hidden/Grayscale Effect" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	_RampTex("Base (RGB)", 2D) = "grayscaleRamp" {}
	_RedPower("Red Power", Range(0.001,1.0)) = 1.0
		_RedDelta("Red Delta", Range(0.001,1.0)) = 1.0
	}

		SubShader{
		Pass{
		ZTest Always Cull Off ZWrite Off
		Fog{ Mode off }

		CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma fragmentoption ARB_precision_hint_fastest 
#include "UnityCG.cginc"

		uniform sampler2D _MainTex;
	uniform sampler2D _RampTex;
	uniform half _RampOffset;
	uniform float _RedPower;
	uniform float _RedDelta;

	fixed4 frag(v2f_img i) : COLOR
	{
		fixed4 original = tex2D(_MainTex, i.uv);
	fixed grayscale = Luminance(original.rgb);
	half2 remap = half2 (grayscale + _RampOffset, .5);
	fixed4 output = tex2D(_RampTex, remap);

	half avg = original.r + original.g + original.b;
	avg *= 0.333;
	half4 nC = half4(avg,avg,avg,original.a);

	half avg2 = original.g + original.b;
	avg2 *= 0.5;

	if (original.r > _RedPower && (original.r - avg2) > _RedDelta)
	{
		nC.rgb = half3(original.r,avg2,avg2);
	}

	output = fixed4(nC.r,nC.g,nC.b,original.a);

	return output;
	}
		ENDCG

	}
	}

		Fallback off

}