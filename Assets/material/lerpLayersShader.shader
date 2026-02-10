Shader "Custom/lerpLayersShader_WithContour"
{
    Properties
    {
        _BaseTex ("Base Texture", 2D) = "white" {}
        _Layer1 ("Layer 1", 2D) = "white" {}
        _Layer2 ("Layer 2", 2D) = "white" {}
        _Layer3 ("Layer 3", 2D) = "white" {}
        _Layer4 ("Layer 4", 2D) = "white" {}
        _Mask ("Mask 1", 2D) = "white" {}
        
        // 等高线参数
        _ContourInterval ("Contour Interval", Range(0.1, 50)) = 5.0
        _ContourWidth ("Contour Width", Range(0.001, 0.1)) = 0.01
        _ContourColor ("Contour Color", Color) = (0, 0, 0, 1)
        _ContourIntensity ("Contour Intensity", Range(0, 1)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
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
                float3 worldPos : TEXCOORD1;  // 新增：传递世界坐标
            };
            
            sampler2D _BaseTex, _Layer1, _Layer2, _Layer3, _Layer4, _Mask;
            float4 _BaseTex_ST;
            
            // 等高线参数
            float _ContourInterval;
            float _ContourWidth;
            float4 _ContourColor;
            float _ContourIntensity;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // 转换到世界空间
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 原有材质混合逻辑
                fixed4 baseColor = tex2D(_BaseTex, i.uv);
                fixed4 layer1 = tex2D(_Layer1, i.uv);
                fixed4 layer2 = tex2D(_Layer2, i.uv);
                fixed4 layer3 = tex2D(_Layer3, i.uv);
                fixed4 layer4 = tex2D(_Layer4, i.uv);
                
                fixed mask1 = tex2D(_Mask, i.uv).r;
                fixed mask2 = tex2D(_Mask, i.uv).g;
                fixed mask3 = tex2D(_Mask, i.uv).b;
                fixed mask4 = tex2D(_Mask, i.uv).a;
                
                fixed4 result = baseColor;
                result = lerp(result, layer1, mask1);
                result = lerp(result, layer2, mask2);
                result = lerp(result, layer3, mask3);
                result = lerp(layer4, result, mask4);
                
                // ====== 等高线计算逻辑 ======
                // 1. 获取当前片段的世界高度
                float height = i.worldPos.y;
                
                // 2. 计算到最近等高线的距离（取模运算）
                float remainder = fmod(height, _ContourInterval);
                
                // 3. 处理负高度的取模（确保始终为正）
                if (remainder < 0) remainder += _ContourInterval;
                
                // 4. 判断是否在等高线宽度范围内（两端都检查）
                float distanceToContour = min(remainder, _ContourInterval - remainder);
                float contourFactor = step(distanceToContour, _ContourWidth);
                
                // 5. 混合等高线颜色
                if (contourFactor > 0.5 && _ContourIntensity > 0)
                {
                    result = lerp(result, _ContourColor, _ContourIntensity * contourFactor);
                }
                
                return result;
            }
            ENDCG
        }
    }
}