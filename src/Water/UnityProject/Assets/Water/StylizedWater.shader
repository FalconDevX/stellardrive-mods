Shader "StellarDriveMods/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.36, 0.66, 0.62, 0.5)
        _MidColor ("Mid", Color) = (0.13, 0.47, 0.55, 0.84)
        _DeepColor ("Deep", Color) = (0.05, 0.26, 0.38, 0.97)
        _MidDepth ("Mid depth (m)", Float) = 1.3
        _DeepDepth ("Deep depth (m)", Float) = 4.5
        _WaveHeight ("Wave height (m)", Float) = 0.14
        _WaveSpeed ("Wave speed", Float) = 1
        _CausticsTex ("Caustics", 2D) = "black" {}
        _CausticsScale ("Caustics scale (1/m)", Float) = 0.12
        _CausticsSpeed ("Caustics speed", Float) = 0.025
        _CausticsStrength ("Caustics strength", Float) = 0.5
        _FoamTex ("Foam", 2D) = "black" {}
        _FoamScale ("Foam scale (1/m)", Float) = 0.18
        _FoamDistance ("Shore foam width (m)", Float) = 1.6
        _FoamColor ("Foam", Color) = (0.96, 0.98, 0.95, 1)
        _NormalTex ("Ripples", 2D) = "bump" {}
        _NormalScale ("Ripples scale (1/m)", Float) = 0.08
        _NormalStrength ("Ripples strength", Float) = 0.35
        _PatchScale ("Patches scale (1/m)", Float) = 0.012
        _PatchStrength ("Patches strength", Float) = 0.22
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _FresnelColor ("Fresnel / sky", Color) = (0.62, 0.86, 0.95, 1)
        _UseSceneDepth ("Use scene depth", Float) = 1
        _Exposure ("Exposure (day/night, set by mod)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _MidColor, _DeepColor, _FoamColor, _FresnelColor;
                float _MidDepth, _DeepDepth;
                float _CausticsScale, _CausticsSpeed, _CausticsStrength;
                float _FoamScale, _FoamDistance;
                float _NormalScale, _NormalStrength, _Smoothness, _UseSceneDepth, _Exposure;
                float _WaveHeight, _WaveSpeed;
                float _PatchScale, _PatchStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float depth : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float fog : TEXCOORD5;
                float2 slope : TEXCOORD6;
            };

            float3 Waves(float2 p, float t)
            {
                float3 r = 0;
                float2 d[3] = { float2(0.8, 0.6), float2(-0.45, 0.89), float2(0.96, -0.28) };
                float l[3] = { 16.0, 10.0, 6.5 };
                float a[3] = { 0.55, 0.3, 0.15 };
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float w = 6.2831853 / l[k];
                    float phase = dot(d[k], p) * w + t * sqrt(9.8 * w) * 0.7;
                    r.x += a[k] * sin(phase);
                    r.yz += a[k] * w * cos(phase) * d[k];
                }
                return r;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float fade = saturate(v.uv2.x / 3.0);
                float3 wave = Waves(v.uv, _Time.y * _WaveSpeed) * _WaveHeight * fade;
                float3 displaced = v.positionOS.xyz + normalize(v.normalOS) * wave.x;
                VertexPositionInputs p = GetVertexPositionInputs(displaced);
                o.slope = wave.yz;
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.depth = v.uv2.x;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Band(float v, float edge) { return smoothstep(edge - 0.04, edge + 0.04, v); }

            float2 Rotate(float2 p, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float Mask(float2 p)
            {
                return SAMPLE_TEXTURE2D_LOD(_FoamTex, sampler_FoamTex, p, 6).r;
            }

            float3 NormalVaried(float2 uv, float mask)
            {
                float3 a = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv));
                float2 uvB = Rotate(uv, 2.17) + float2(0.37, 0.71);
                float3 b = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvB));
                b.xy = Rotate(b.xy, -2.17);
                return lerp(a, b, mask);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float3 up = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));

                float depth = max(i.depth, 0);
                float edgeDepth = depth;
                if (_UseSceneDepth > 0.5)
                {
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float surface = i.screenPos.w;
                    float viewThickness = scene - surface;
                    if (viewThickness > 0)
                    {
                        float vertical = viewThickness * saturate(dot(viewDir, up) + 0.05);
                        edgeDepth = min(edgeDepth > 0 ? edgeDepth : 1e4, vertical);
                        depth = min(depth, vertical * 1.5 + 0.2);
                    }
                }

                float camDist = length(_WorldSpaceCameraPos - i.positionWS);
                float far = saturate((camDist - 25) / 160);
                float m1 = smoothstep(0.08, 0.38, Mask(i.uv * 0.0071 + float2(t * 0.0011, -t * 0.0007)));
                float m2 = smoothstep(0.08, 0.38, Mask(Rotate(i.uv, 0.9) * 0.0053 + float2(0.31, t * 0.0009)));
                float2 uvN1 = i.uv * _NormalScale + float2(t * 0.012, t * 0.007);
                float2 uvN2 = Rotate(i.uv, 1.13) * _NormalScale * 1.73 + float2(-t * 0.009, t * 0.013);
                float3 n1 = NormalVaried(uvN1, m1);
                float3 n2 = NormalVaried(uvN2, m2);
                n2.xy = Rotate(n2.xy, -1.13);
                float2 uvN3 = Rotate(i.uv, -0.61) * _NormalScale * 0.23 + float2(t * 0.004, -t * 0.003);
                float3 n3 = NormalVaried(uvN3, 1 - m1);
                n3.xy = Rotate(n3.xy, 0.61);
                float2 uvN4 = Rotate(i.uv, 2.45) * _NormalScale * 0.071 + float2(-t * 0.0021, t * 0.0016);
                float3 n4 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvN4));
                n4.xy = Rotate(n4.xy, -2.45);
                float gust = lerp(0.65, 1.35, m2 * (1 - m1 * 0.5));
                float2 ripple = ((n1.xy + n2.xy) * (1 - far * 0.75) + n3.xy * 1.4 + n4.xy * lerp(0.6, 1.6, far)) * _NormalStrength * gust - i.slope;
                float3 tangent = normalize(cross(up, abs(up.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 bitangent = cross(up, tangent);
                float3 normal = normalize(up + tangent * ripple.x + bitangent * ripple.y);

                if (dot(viewDir, up) < 0)
                {
                    Light sunBelow = GetMainLight();
                    half3 ambientBelow = SampleSH(up);
                    float shimmer = saturate((abs(ripple.x) + abs(ripple.y)) * 1.5);
                    half4 under;
                    under.rgb = _ShallowColor.rgb * (sunBelow.color * 0.7 + ambientBelow * 0.8) + (sunBelow.color + ambientBelow) * shimmer * 0.3;
                    float grazing = 1 - saturate(-dot(viewDir, up) * 2.5);
                    under.a = lerp(0.55 + shimmer * 0.2, 0.9, grazing * grazing);
                    under.rgb *= _Exposure;
                    under.rgb = MixFog(under.rgb, i.fog);
                    return under;
                }

                half4 col = lerp(_ShallowColor, _MidColor, saturate(depth / _MidDepth));
                col = lerp(col, _DeepColor, saturate((depth - _MidDepth) / max(_DeepDepth - _MidDepth, 0.01)));

                float2 cuv = i.uv * _CausticsScale + ripple * 0.15;
                float c1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cuv + float2(t, t * 0.6) * _CausticsSpeed).r;
                float c2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, Rotate(cuv, 1.87) * 1.43 + float2(-t * 0.7, t) * _CausticsSpeed).r;
                float caustics = pow(saturate(min(c1, c2) * 1.8), 1.5) + max(c1, c2) * 0.18;
                caustics *= _CausticsStrength * saturate(1.2 - depth / (_DeepDepth * 1.4)) * lerp(0.45, 1.25, m1) * (1 - far * 0.6);
                col.rgb += caustics * half3(0.85, 1.0, 0.95);

                float p1 = SAMPLE_TEXTURE2D_LOD(_FoamTex, sampler_FoamTex, i.uv * _PatchScale + float2(t * 0.003, t * 0.002), 5).r;
                float p2 = SAMPLE_TEXTURE2D_LOD(_FoamTex, sampler_FoamTex, i.uv * _PatchScale * 0.37 + float2(-t * 0.002, t * 0.0025), 5).r;
                float patches = saturate((p1 + p2) * 0.5 / 0.45);
                col.rgb *= 1 + (patches - 0.5) * 2 * _PatchStrength;

                Light sun = GetMainLight();
                float ndl = saturate(dot(normal, sun.direction)) * 0.5 + 0.5;
                half3 ambient = SampleSH(normal);
                half3 light = sun.color * ndl * 0.62 + ambient * 0.45;
                col.rgb *= light / max(1, max(light.r, max(light.g, light.b)));
                float fresnel = pow(1 - saturate(dot(normal, viewDir)), 5);
                half3 sky = _FresnelColor.rgb * saturate(sun.color * 0.4 + ambient * 0.6);
                col.rgb = lerp(col.rgb, sky, fresnel * 0.35);
                col.a = lerp(col.a, 1, fresnel * 0.5);
                float3 h = normalize(sun.direction + viewDir);
                float2 detail = (n1.xy + n2.xy) * _NormalStrength * 1.6 + ripple * 0.5;
                float3 glintNormal = normalize(up + tangent * detail.x + bitangent * detail.y);
                float sunUp = saturate(dot(sun.direction, up) * 6 + 0.1);
                float glint = pow(saturate(dot(glintNormal, h)), lerp(900, 140, far)) * lerp(2.2, 0.5, far);
                float sheen = pow(saturate(dot(normal, h)), 24) * 0.05;
                col.rgb += sun.color * sunUp * (saturate(glint) * 0.8 + sheen) * _Smoothness;

                float foamTex = lerp(SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, i.uv * _FoamScale + float2(t * 0.01, -t * 0.008)).r,
                    SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, Rotate(i.uv, 1.41) * _FoamScale * 0.83 + float2(-t * 0.007, t * 0.009)).r, m2);
                float shore = 1 - saturate(edgeDepth / _FoamDistance);
                float wavesBand = Band(frac(edgeDepth / (_FoamDistance * 0.9) + t * 0.18) * shore, 0.55);
                float foam = max(Band(shore * (0.55 + foamTex * 0.9), 0.7), wavesBand * Band(foamTex, 0.35));
                col.rgb = lerp(col.rgb, _FoamColor.rgb * (sun.color * 0.6 + ambient * 0.6), foam);
                col.a = max(col.a, foam);
                col.a *= smoothstep(0.0, 0.45, edgeDepth);

                col.rgb *= _Exposure;
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
