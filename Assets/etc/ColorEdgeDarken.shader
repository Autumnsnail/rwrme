Shader "RWRME/Color Edge Darken"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Width (UV)", Range(0.001, 0.2)) = 0.04
        _EdgeDarken ("Edge Darken", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _EdgeWidth;
            float _EdgeDarken;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = min(i.uv, 1.0 - i.uv);
                float dist = min(d.x, d.y);
                float edge = dist < _EdgeWidth ? 1.0 : 0.0;
                float shade = 1.0 - edge * _EdgeDarken;
                return fixed4(_Color.rgb * shade, _Color.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
