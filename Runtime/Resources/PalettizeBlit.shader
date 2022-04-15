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

            uniform fixed4 _Color;
			uniform fixed4 _Colors[256];
	  		uniform sampler2D _MainTex;

	  		fixed4 frag (v2f_img i) : COLOR
	  		{
	   			fixed4 original = tex2D(_MainTex, i.uv) * _Color;

	   			fixed4 col = fixed4 (0,0,0,0);
	   			fixed dist = 10000000.0;

	   			for (int i = 0; i < 255; i++) { // ignore index 255, which is reserved for transparency
	   				fixed4 c = _Colors[i];
	   				fixed d = distance(original, c);

	   				if (d < dist) {
	   					dist = d;
	   					col = fixed4(i/255.0, i/255.0, i/255.0, 1);
	   				}
	   			}

				return col;
	  		}
	  		ENDCG
	 	}
	}

	FallBack "Unlit"
}