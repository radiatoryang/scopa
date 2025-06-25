// used by Scopa Wad Creator to resize + palettize textures
// based on https://github.com/oxysoft/RetroSuite3D/blob/master/Assets/Shaders/Custom/RetroPixelMax.shader
Shader "Hidden/PalettizeBlit" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
	 	_MainTex ("", 2D) = "white" {}
		_AlphaIsTransparency ("Alpha Is Transparency", Integer) = 0
		_Gamma ("", Float) = 1.0
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
			uniform half _AlphaIsTransparency;
			uniform half _Gamma;

			half3 RGBtoHSV(half3 arg1)
			{
				half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				half4 P = lerp(half4(arg1.bg, K.wz), half4(arg1.gb, K.xy), step(arg1.b, arg1.g));
				half4 Q = lerp(half4(P.xyw, arg1.r), half4(arg1.r, P.yzx), step(P.x, arg1.r));
				half D = Q.x - min(Q.w, Q.y);
				half E = 1e-10;
				return half3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);
			}
			
			half3 HSVtoRGB(half3 arg1)
			{
				half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
				half3 P = abs(frac(arg1.xxx + K.xyz) * 6.0 - K.www);
				return arg1.z * lerp(K.xxx, saturate(P - K.xxx), arg1.y);
			}

	  		float4 frag (v2f_img i) : COLOR
	  		{
				float4 col = float4 (1,1,1,1);
				float dist = 10000000.0;

				float4 original = pow(tex2D(_MainTex, i.uv), _Gamma);

				if (_AlphaIsTransparency == 1 && original.a < 0.9) {
					col.a = 1;
					return col;
				}

				original *= _Color;
				half3 hsvA = RGBtoHSV(original.rgb);

	   			for (int i = 0; i < 255; i++) { // ignore index 255, which is reserved for transparency
					// via https://stackoverflow.com/questions/35113979/calculate-distance-between-colors-in-hsv-space

					half3 hsvB = RGBtoHSV(_Colors[i].rgb);
					half dh = min(abs(hsvB.x-hsvA.x), 1.0-abs(hsvB.x-hsvA.x));
					half ds = (hsvB.y-hsvA.y);
					half dv = (hsvB.z-hsvA.z);
					half d = sqrt(dh*dh+ds*ds+dv*dv);

					// half d = distance(original.rgb, _Colors[i].rgb);

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