Shader "Custom/Billboard"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [Toggle] _OnlyVertical ("Only Vertical", float) = 1
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1) // 发光颜色
        _EmissionStrength ("Emission Strength", Float) = 1.0 // 发光强度
        _Size ("Size", Float) = 1.0 // 尺寸控制
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" 
               "QUEUE"="Transparent" 
               "DisableBatching"="True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature _ONLYVERTICAL_ON

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _EmissionStrength;
            float _Size; // 控制尺寸

            v2f vert(appdata v)
            {
                v2f o;

                // 相机到物体的方向向量
                float3 CameraToObject = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1));
                float3 forward = CameraToObject;

                #if _ONLYVERTICAL_ON
                    forward.y = 0;
                #endif

                forward = normalize(forward);

                // 构建正交基
                float3 up = abs(forward.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 right = normalize(cross(forward, up));
                up = normalize(cross(right, forward));

                // 按照尺寸调整顶点位置
                float3 vertex = v.vertex.x * right + v.vertex.y * up;

                // 应用尺寸控制
                vertex *= _Size; // 乘以 _Size 进行缩放

                o.vertex = UnityObjectToClipPos(float4(vertex, 1));
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 获取纹理颜色并乘上常规颜色
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // 计算发光效果
                fixed4 emission = _EmissionColor * _EmissionStrength;

                // 结合颜色和发光效果
                return col + emission;
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}