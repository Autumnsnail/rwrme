Shader "Custom/MultiLayerMask" {
    Properties {
        _MainTex ("Base Color (RGB)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _SpecularMask ("Specular Mask", 2D) = "white" {}
        _SpecularScale ("Specular Scale", Float) = 1.0
        _Specular ("Specular Color", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(8.0, 256)) = 20
        
        // 图层纹理
        _Layer1Tex ("Layer 1 Tex", 2D) = "white" {}
        _Layer1Mask ("Layer 1 Mask", 2D) = "white" {}
        _Layer2Tex ("Layer 2 Tex", 2D) = "white" {}
        _Layer2Mask ("Layer 2 Mask", 2D) = "white" {}
        _Layer3Tex ("Layer 3 Tex", 2D) = "white" {}
        _Layer3Mask ("Layer 3 Mask", 2D) = "white" {}
        _Layer4Tex ("Layer 4 Tex", 2D) = "white" {}
        _Layer4Mask ("Layer 4 Mask", 2D) = "white" {}
        _Layer5Tex ("Layer 5 Tex", 2D) = "white" {}
        _Layer5Mask ("Layer 5 Mask", 2D) = "white" {}
        
        _LayerCount ("Active Layer Count", Range(0, 5)) = 5
    }
    SubShader {
        Pass {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            // 基础纹理
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            sampler2D _SpecularMask;
            float _SpecularScale;
            fixed4 _Specular;
            float _Gloss;
            
            // 图层纹理数组
            sampler2D _Layer1Tex, _Layer2Tex, _Layer3Tex, _Layer4Tex, _Layer5Tex;
            sampler2D _Layer1Mask, _Layer2Mask, _Layer3Mask, _Layer4Mask, _Layer5Mask;
            int _LayerCount;

            struct a2v {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 texcoord : TEXCOORD0;
            };
            
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 lightDir : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert (a2v v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                TANGENT_SPACE_ROTATION;
                o.lightDir = mul(rotation, ObjSpaceLightDir(v.vertex)).xyz;
                o.viewDir = mul(rotation, ObjSpaceViewDir(v.vertex)).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. 初始化基础颜色
                fixed3 albedo = tex2D(_MainTex, i.uv).rgb;
                
                // 2. 使用循环处理所有图层
                for (int layer = 0; layer < _LayerCount; layer++) {
                    fixed3 layerColor;
                    fixed maskValue;
                    
                    // 根据图层索引选择对应的纹理
                    if (layer == 0) {
                        layerColor = tex2D(_Layer1Tex, i.uv).rgb;
                        maskValue = tex2D(_Layer1Mask, i.uv).r;
                    } else if (layer == 1) {
                        layerColor = tex2D(_Layer2Tex, i.uv).rgb;
                        maskValue = tex2D(_Layer2Mask, i.uv).r;
                    } else if (layer == 2) {
                        layerColor = tex2D(_Layer3Tex, i.uv).rgb;
                        maskValue = tex2D(_Layer3Mask, i.uv).r;
                    } else if (layer == 3) {
                        layerColor = tex2D(_Layer4Tex, i.uv).rgb;
                        maskValue = tex2D(_Layer4Mask, i.uv).r;
                    } else if (layer == 4) {
                        layerColor = tex2D(_Layer5Tex, i.uv).rgb;
                        maskValue = tex2D(_Layer5Mask, i.uv).r;
                    }
                    
                    // 混合图层
                    albedo = lerp(albedo, layerColor, maskValue);
                }
                
                // 3. 原有的光照计算
                fixed3 tangentNormal = UnpackNormal(tex2D(_BumpMap, i.uv));
                fixed3 tangentLightDir = normalize(i.lightDir);
                fixed3 diffuse = _LightColor0.rgb * albedo * max(0, dot(tangentNormal, tangentLightDir));

                fixed specularMask = tex2D(_SpecularMask, i.uv).r;
                fixed3 halfDir = normalize(tangentLightDir + normalize(i.viewDir));
                fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(tangentNormal, halfDir)), _Gloss) * specularMask * _SpecularScale;

                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * albedo;
                return fixed4(ambient + diffuse , 1.0);
            }
            ENDCG
        }
    }
    FallBack "Specular"
}