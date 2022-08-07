// used by Scopa Wad Creator to resize + palettize textures
// based on https://github.com/oxysoft/RetroSuite3D/blob/master/Assets/Shaders/Custom/RetroPixelMax.shader
Shader "Hidden/PalettizeBlit" {
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

            uniform float4 _Color;
			uniform float4 _Colors[256];
	  		uniform sampler2D _MainTex;

	  		float4 frag (v2f_img i) : COLOR
	  		{
	   			float4 original = tex2D(_MainTex, i.uv) * 2 * _Color;

	   			float4 col = float4 (1,1,1,1);
	   			float dist = 10000000.0;

	   			for (int i = 0; i < 255; i++) { // ignore index 255, which is reserved for transparency
	   				float d = distance(original, _Colors[i]);

	   				if (d < dist) {
	   					dist = d;
						col = _Colors[i];
						col.a = i / 255.0;
	   				}
	   			}

				return col;
	  		}

	  		ENDCG
	 	}
	}

	FallBack "Unlit"
}