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
        
        // �ȸ��߲���
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
                float3 worldPos : TEXCOORD1;  // ������������������
            };
            
            sampler2D _BaseTex, _Layer1, _Layer2, _Layer3, _Layer4, _Mask;
            float4 _BaseTex_ST;
            
            // �ȸ��߲���
            float _ContourInterval;
            float _ContourWidth;
            float4 _ContourColor;
            float _ContourIntensity;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // ת��������ռ�
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // ԭ�в��ʻ���߼�
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
                
                // ====== �ȸ��߼����߼� ======
                // 1. ����߶ȣ���Ч������� �� �ȸ����ܶ�Ϊԭ���� 2 ��
                float height = i.worldPos.y;
                float interval = _ContourInterval * 0.5;
                
                // 2. ������ȸ��ߣ�y = k * interval���ľ���
                float remainder = fmod(height, interval);
                if (remainder < 0) remainder += interval;
                float distanceToContour = min(remainder, interval - remainder);
                float contourLine = step(distanceToContour, _ContourWidth);
                
                // 3. ���ơ������桹����Ϳ��ˮƽ/����ˮƽ���� height ���������ڼ������䣬
                //    remainder ȫƬ��ͬ��step ��Ϊ 1 �� �����ɫ��ֻ�ڸ߶�����Ļ���пɼ��仯�����ߣ����桢�۱ߵȣ���
                float heightVariation = fwidth(height);
                float allowContour = smoothstep(0.0, max(_ContourWidth * 0.05, 1e-6), heightVariation);
                float contourFactor = contourLine * allowContour;
                
                // 4. ��ϵȸ�����ɫ
                result = lerp(result, _ContourColor, contourFactor * _ContourIntensity);
                
                return result;
            }
            ENDCG
        }
    }
}