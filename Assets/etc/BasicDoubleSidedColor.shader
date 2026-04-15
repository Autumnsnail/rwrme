Shader "RWRME/Basic Double-Sided Color"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _BackCheckerScale ("Back Checker Scale", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            fixed4 _Color;
            float _BackCheckerScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                if (facing > 0) return _Color;

                float2 p = i.worldPos.xz * max(_BackCheckerScale, 1e-6);
                float2 cell = floor(p);
                float checker = fmod(cell.x + cell.y, 2.0);

                fixed4 colA = fixed4(0, 0, 0, 1);
                fixed4 colB = fixed4(1, 0, 1, 1);
                return lerp(colA, colB, checker);
            }
            ENDCG
        }
    }
    FallBack Off
}
