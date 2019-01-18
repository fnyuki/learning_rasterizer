Shader "Hidden/Framework"
{
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			float4 _Size;
			StructuredBuffer<float4> _Buffer;

			fixed4 frag (v2f i) : SV_Target
			{
				i.uv.y = 1.0 - i.uv.y;
				const float2 uvOffset = float2(1.0 / _Size.x, 1.0 / _Size.y);
				const float2 uv = i.uv - uvOffset;
				return _Buffer.Load(ceil(uv.x * _Size.x) + ceil(uv.y * _Size.y) * _Size.x);
			}
			ENDCG
		}
	}
}
