Shader "StellarDriveMods/SunRays"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+300" }

        Pass
        {
            Name "SunRays"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_FLOAT(_CloudsShadowMap);
            float4 _CloudsShadowMapInverseRotationWs;
            float4 _CloudsShadowMapPositionRelativeToPlanetWs;
            float4 _CloudsShadowMapExtentsWs;

            CBUFFER_START(UnityPerMaterial)
                float4 _RayColor;
                float4 _SunDirection;
                float4 _PlanetCenter;
                float4 _StreakU;
                float4 _StreakV;
                float _StreakSize;
                float _Reference;
                float _CloudRadius, _GroundRadius, _Range, _HeightScale, _MapSize, _Softness, _Steps, _Anisotropy;
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

            float3 Rotate(float4 q, float3 v)
            {
                float3 t = 2 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            float FarHit(float3 origin, float3 dir, float radius)
            {
                float len = length(origin);
                float b = dot(origin, dir);
                float c = (len - radius) * (len + radius);
                float h = b * b - c;
                if (h < 0) return -1;
                return -b + sqrt(h);
            }

            float NearHit(float3 origin, float3 dir, float radius)
            {
                float len = length(origin);
                float b = dot(origin, dir);
                float c = (len - radius) * (len + radius);
                float h = b * b - c;
                if (h < 0) return -1;
                return -b - sqrt(h);
            }

            float TexelLit(int2 texel, float depth)
            {
                if (texel.x < 0 || texel.y < 0 || texel.x >= (int)_MapSize || texel.y >= (int)_MapSize) return 0;
                float stored = LOAD_TEXTURE2D(_CloudsShadowMap, texel).r;
                if (stored > 1.5) return 1;
                return saturate((stored * _CloudsShadowMapExtentsWs.z - depth) / _Softness + 0.5);
            }

            float Lit(float3 relative)
            {
                float3 local = Rotate(_CloudsShadowMapInverseRotationWs, relative - _CloudsShadowMapPositionRelativeToPlanetWs.xyz);
                float2 uv = local.xy / (2 * _CloudsShadowMapExtentsWs.xy) + 0.5;
                if (any(uv < 0) || any(uv > 1)) return 0;
                float2 texel = uv * _MapSize - 0.5;
                int2 base = (int2)floor(texel);
                float2 f = texel - base;
                float a = TexelLit(base, local.z);
                float b = TexelLit(base + int2(1, 0), local.z);
                float c = TexelLit(base + int2(0, 1), local.z);
                float d = TexelLit(base + int2(1, 1), local.z);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            float Value(float2 p)
            {
                float2 f = frac(p);
                float2 cell = fmod(floor(p), 1024);
                float2 u = f * f * (3 - 2 * f);
                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Streaks(float3 p)
            {
                float2 across = float2(dot(p, _StreakU.xyz), dot(p, _StreakV.xyz)) / _StreakSize;
                float n = 0.6 * Value(across) + 0.4 * Value(across * 3.1 + 17);
                return saturate((n - 0.32) * 2.8) * 1.5;
            }

            float Noise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
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
                if (raw <= 1e-6) dist = _Range;
#else
                if (raw >= 1 - 1e-6) dist = _Range;
#endif
                float3 origin = _WorldSpaceCameraPos - _PlanetCenter.xyz;
                float end = min(dist, _Range);
                float ceiling = FarHit(origin, dir, _CloudRadius);
                if (ceiling > 0) end = min(end, ceiling);
                if (length(origin) > _GroundRadius)
                {
                    float ground = NearHit(origin, dir, _GroundRadius);
                    if (ground > 0) end = min(end, ground);
                }
                if (end <= 1) return 0;

                int steps = (int)_Steps;
                float jitter = Noise(i.positionCS.xy);
                float sum = 0;
                for (int s = 0; s < steps; s++)
                {
                    float x = (s + jitter) / steps;
                    float t = end * x * x;
                    float dt = 2 * x * end / steps;
                    float3 p = origin + dir * t;
                    float height = max(length(p) - _GroundRadius, 0);
                    float lit = Lit(p);
                    if (lit > 0.001) sum += lit * Streaks(p) * exp(-height / _HeightScale - t / _Range) * dt;
                }

                float g = _Anisotropy;
                float cosine = dot(dir, _SunDirection.xyz);
                float forward = (1 - g * g) / pow(abs(1 + g * g - 2 * g * cosine), 1.5);
                float phase = 0.2 + 0.8 * forward / (1 + g) * (1 - g);
                return half4(_RayColor.rgb * (1 - exp(-phase * sum / _Reference)), 1);
            }
            ENDHLSL
        }
    }
}
