Shader "StellarDriveMods/UnderwaterFog"
{
    Properties
    {
        _FogColor ("Fog color", Color) = (0.08, 0.32, 0.36, 1)
        _Density ("Density (1/m)", Float) = 0.25
        _CamDepth ("Camera depth below surface (m)", Float) = 1
        _Up ("Surface up (world)", Vector) = (0, 1, 0, 0)
        _Exposure ("Exposure", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+400" }

        Pass
        {
            Name "UnderwaterFog"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density, _CamDepth, _Exposure;
                float4 _Up;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.positionOS.xy * 2, 0.5, 1);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float raw = SampleSceneDepth(uv);
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 ray = world - _WorldSpaceCameraPos;
                float dist = length(ray);
                float3 dir = ray / max(dist, 1e-4);
#if UNITY_REVERSED_Z
                if (raw <= 1e-6) dist = 1e5;
#else
                if (raw >= 1 - 1e-6) dist = 1e5;
#endif
                float3 up = normalize(_Up.xyz);
                float rise = dot(dir, up);
                float toSurface = rise > 1e-3 ? max(_CamDepth, 0) / rise : 1e5;
                float fogDist = dist;
                if (toSurface < dist)
                {
                    fogDist = toSurface * lerp(0.35, 0.12, saturate(rise));
                }
                float fog = 1 - exp(-fogDist * _Density);

                Light sun = GetMainLight();
                half3 ambient = SampleSH(up);
                half3 light = saturate(sun.color * 0.55 + ambient * 0.6);
                float dim = exp(-max(_CamDepth, 0) * 0.06);
                half3 col = _FogColor.rgb * light * _Exposure * (0.45 + 0.55 * dim);
                col *= lerp(0.8, 1.15, saturate(rise * 0.5 + 0.5));
                return half4(col, saturate(fog));
            }
            ENDHLSL
        }
    }
}
