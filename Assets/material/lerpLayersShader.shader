Shader "Custom/lerpLayersShader"
{
    Properties
    {
        _BaseTex ("Base Texture", 2D) = "white" {}
        
        _Layer1 ("Layer 1", 2D) = "white" {}
        _Layer2 ("Layer 2", 2D) = "white" {}
        _Layer3 ("Layer 3", 2D) = "white" {}
        _Layer4 ("Layer 4", 2D) = "white" {}
        
        _Mask ("Mask 1", 2D) = "white" {}
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
                float4 vertex:POSITION;
                float2 uv:TEXCOORD0;
            };
            struct v2f
            {
                float2 uv:TEXCOORD0;
                float4 vertex:SV_POSITION;
            };
            sampler2D _BaseTex,_Layer1,_Layer2,_Layer3,_Layer4;
            sampler2D _Mask;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
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
                result = lerp(layer4,result , mask4);
                return result;
            }
            
            ENDCG
        }
    }
}
