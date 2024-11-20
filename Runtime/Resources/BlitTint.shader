// used by Scopa Wad Creator to resize + palettize textures
// based on https://github.com/oxysoft/RetroSuite3D/blob/master/Assets/Shaders/Custom/RetroPixelMax.shader
Shader "Hidden/BlitTint" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
	 	_MainTex ("", 2D) = "white" {}
	}

	SubShader {
		Lighting Off
		ZTest Always
		Cull Off
		ZWrite Off
		Fog { Mode Off }

	 	Pass {
	  		CGPROGRAM
	  		#pragma vertex vert_img
	  		#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
	  		#include "UnityCG.cginc"

            uniform fixed4 _Color;
	  		uniform sampler2D _MainTex;

	  		fixed4 frag (v2f_img i) : COLOR
	  		{
				#if !UNITY_COLORSPACE_GAMMA
	   			return pow(tex2D(_MainTex, i.uv), 2.2) * _Color;
				#else
				return tex2D(_MainTex, i.uv) * _Color;
				#endif
	  		}
	  		ENDCG
	 	}
	}

	FallBack "Unlit"
}