Shader "Iris/ParallaxWindow"
{
    Properties
    {
        _Cubemap ("Exterior Cubemap", CUBE) = "" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _RoomDepth ("Room Depth", Range(0.1, 10)) = 2.0
        _WindowEmission ("Window Emission", Range(0, 2)) = 0.0
        _EmissionColor ("Emission Color", Color) = (1, 0.9, 0.7, 1)
        _RainIntensity ("Rain Intensity", Range(0, 1)) = 0.0
        _DropletScale ("Droplet Scale", Range(1, 20)) = 8.0
        _StreakSpeed ("Streak Speed", Range(0.1, 3)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 tangentViewDir : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
            };

            TEXTURECUBE(_Cubemap);
            SAMPLER(sampler_Cubemap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half  _RoomDepth;
                half  _WindowEmission;
                half4 _EmissionColor;
                half  _RainIntensity;
                half  _DropletScale;
                half  _StreakSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;

                // Build TBN matrix for tangent-space view direction
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * input.tangentOS.w;

                output.normalWS = normalWS;
                output.tangentWS = tangentWS;
                output.bitangentWS = bitangentWS;

                // View direction in tangent space
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.tangentViewDir = float3(
                    dot(viewDirWS, tangentWS),
                    dot(viewDirWS, bitangentWS),
                    dot(viewDirWS, normalWS)
                );

                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            // ── Procedural hash for rain droplets ──────────────────────
            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ── Rain droplet layer ─────────────────────────────────────
            float2 rainLayer(float2 uv, float scale, float speed, float time)
            {
                float2 st = uv * scale;
                float2 id = floor(st);
                float2 gv = frac(st) - 0.5;

                float2 offset = float2(0, 0);
                float t = time * speed;

                float2 rnd = hash22(id);
                float x = (rnd.x - 0.5) * 0.6;
                float y = -t + rnd.y * 6.28;
                y = frac(y) * -0.7 + 0.35;

                float2 dropPos = gv - float2(x, y);
                float d = length(dropPos);
                float drop = smoothstep(0.08, 0.02, d);

                offset += drop * dropPos * 3.0;
                return offset;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // ── Rain distortion ──────────────────────────────
                float2 rainOffset = float2(0, 0);
                if (_RainIntensity > 0.001)
                {
                    float t = _Time.y;
                    // Large slow droplets
                    rainOffset += rainLayer(uv, _DropletScale, _StreakSpeed * 0.3, t) * 0.8;
                    // Vertical streaks
                    rainOffset += rainLayer(uv + 0.5, _DropletScale * 2.0, _StreakSpeed, t) * 0.4;
                    // Small splatter
                    rainOffset += rainLayer(uv + 1.7, _DropletScale * 3.0, _StreakSpeed * 0.5, t) * 0.2;

                    rainOffset *= _RainIntensity;
                }

                float2 distortedUV = uv + rainOffset * 0.02;

                // ── Interior mapping (parallax cubemap) ──────────
                // Map UV to centered box coordinates (-0.5 to 0.5)
                float3 viewDir = normalize(input.tangentViewDir);
                float2 centeredUV = distortedUV - 0.5;

                // Ray: origin at UV on front face, direction from tangent-space view
                // Virtual box extends _RoomDepth behind the window
                float3 rayDir = float3(viewDir.x, viewDir.y, max(viewDir.z, 0.001));
                float depthT = _RoomDepth / rayDir.z;

                float3 hitPoint = float3(centeredUV, 0) + rayDir * depthT;

                // Transform hit point from tangent space to world space for cubemap lookup
                float3 worldDir = hitPoint.x * input.tangentWS
                                + hitPoint.y * input.bitangentWS
                                + hitPoint.z * input.normalWS;

                half4 cubeSample = SAMPLE_TEXTURECUBE(_Cubemap, sampler_Cubemap, worldDir);
                half3 color = cubeSample.rgb * _Tint.rgb;

                // ── Window emission (warm glow) ──────────────────
                color += _EmissionColor.rgb * _WindowEmission * 0.5;

                // ── Rain streak darkening on glass ───────────────
                float rainDarken = 1.0 - _RainIntensity * 0.15;
                color *= rainDarken;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
