Shader "Custom/Dither"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _DitherPattern ("Dither Pattern", 2D) = "white" {}
        _DitherScale ("Dither Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf StandardDithered fullforwardshadows addshadow
        #pragma target 3.0

        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex;
        float _DitherScale;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        // Bayer matrix 4x4
        float DitherBayer4x4(float2 screenPos)
        {
            float4x4 bayerMatrix = {
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5
            };
            
            int x = int(fmod(screenPos.x, 4));
            int y = int(fmod(screenPos.y, 4));
            
            return bayerMatrix[y][x] / 16.0;
        }

        inline half4 LightingStandardDithered(SurfaceOutputStandard s, half3 viewDir, UnityGI gi)
        {
            return LightingStandard(s, viewDir, gi);
        }

        void LightingStandardDithered_GI(
            SurfaceOutputStandard s,
            UnityGIInput data,
            inout UnityGI gi)
        {
            LightingStandard_GI(s, data, gi);
            
            // Apply dithering to the shadow/attenuation
            float2 screenPos = data.worldPos.xy * _DitherScale;
            float ditherValue = DitherBayer4x4(screenPos);
            
            // Convert smooth shadow to dithered binary shadow
            float shadow = gi.light.color.r; // Use light intensity as shadow proxy
            float atten = data.atten;
            
            if (atten < ditherValue)
            {
                gi.light.color *= 0.0;
            }
            else
            {
                gi.light.color *= 1.0;
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
