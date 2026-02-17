Shader "Iris/NatureBox"
{
    Properties
    {
        [Header(Time)]
        _TimeOfDay ("Time of Day (0 midnight, 0.5 noon)", Range(0, 1)) = 0.5

        [Header(Sky)]
        _SunSize ("Sun Moon Sharpness", Range(0.90, 1.0)) = 0.97
        _SunGlow ("Sun Glow", Range(0, 1)) = 0.4

        [Header(Clouds)]
        _CloudSpeed ("Cloud Speed", Float) = 0.015
        _CloudScale ("Cloud Scale", Float) = 2.5
        _CloudDensity ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudSharpness ("Cloud Edge Sharpness", Range(0.01, 0.5)) = 0.15

        [Header(Terrain)]
        _MountainScale ("Mountain Scale", Float) = 2.5
        _MountainHeight ("Mountain Height", Range(0, 0.35)) = 0.2
        _TreelineHeight ("Treeline Height", Range(0, 0.15)) = 0.07

        [Header(Atmosphere)]
        _HorizonFog ("Horizon Fog", Range(0, 1)) = 0.35
        _StarDensity ("Star Density", Range(0.95, 1.0)) = 0.985

        [Header(Retro)]
        [Tooltip("Target vertical resolution. 0=native, 240=N64/PS1, 480=PS2/Dreamcast")]
        _PixelDensity ("Pixel Density", Float) = 0
        [Tooltip("Color bit depth. 0=off, 16=N64, 32=PS1, 256=PS2")]
        _ColorDepth ("Color Depth (per channel)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Front
        ZWrite Off

        Pass
        {
            Name "NatureBox"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Properties ───────────────────────────────────────────

            CBUFFER_START(UnityPerMaterial)
                float _TimeOfDay;
                float _SunSize;
                float _SunGlow;
                float _CloudSpeed;
                float _CloudScale;
                float _CloudDensity;
                float _CloudSharpness;
                float _MountainScale;
                float _MountainHeight;
                float _TreelineHeight;
                float _HorizonFog;
                float _StarDensity;
                float _PixelDensity;
                float _ColorDepth;
            CBUFFER_END

            // ── Structs ──────────────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // ── Noise ────────────────────────────────────────────────
            // Hash & value noise matching ParallaxWindow.shader style

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep interp

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractional Brownian Motion — 4 octaves (terrain)
            float fbm4(float2 p)
            {
                float v = 0.0;
                v += 0.5000 * noise2D(p); p *= 2.01;
                v += 0.2500 * noise2D(p); p *= 2.02;
                v += 0.1250 * noise2D(p); p *= 2.01;
                v += 0.0625 * noise2D(p);
                return v / 0.9375;
            }

            // FBM — 5 octaves (clouds, finer detail)
            float fbm5(float2 p)
            {
                float v = 0.0;
                v += 0.5000 * noise2D(p); p *= 2.01;
                v += 0.2500 * noise2D(p); p *= 2.02;
                v += 0.1250 * noise2D(p); p *= 2.01;
                v += 0.0625 * noise2D(p); p *= 2.03;
                v += 0.03125 * noise2D(p);
                return v / 0.96875;
            }

            // ── Color palette ────────────────────────────────────────
            // t: 0 = midnight, 0.25 = 6am (dawn), 0.5 = noon, 0.75 = 6pm (dusk)
            // Maps directly to GameClock.NormalizedTimeOfDay.

            float3 lerpPalette(float t, float3 midnight, float3 dawn, float3 noon, float3 dusk)
            {
                t = frac(t);
                if (t < 0.25) return lerp(midnight, dawn, t * 4.0);
                if (t < 0.50) return lerp(dawn, noon, (t - 0.25) * 4.0);
                if (t < 0.75) return lerp(noon, dusk, (t - 0.50) * 4.0);
                return lerp(dusk, midnight, (t - 0.75) * 4.0);
            }

            float3 zenithColor(float t)
            {
                return lerpPalette(t,
                    float3(0.01, 0.01, 0.06),   // midnight — near-black
                    float3(0.25, 0.18, 0.45),   // dawn — lavender
                    float3(0.35, 0.55, 0.85),   // noon — soft blue
                    float3(0.22, 0.12, 0.38)    // dusk — purple
                );
            }

            float3 horizColor(float t)
            {
                return lerpPalette(t,
                    float3(0.03, 0.03, 0.10),   // midnight — dark blue
                    float3(0.85, 0.45, 0.25),   // dawn — warm orange
                    float3(0.65, 0.75, 0.90),   // noon — pale blue
                    float3(0.80, 0.30, 0.18)    // dusk — deep orange
                );
            }

            float3 sunCol(float t)
            {
                return lerpPalette(t,
                    float3(0.50, 0.55, 0.70),   // midnight — moonlight silver
                    float3(1.00, 0.65, 0.25),   // dawn — gold
                    float3(1.00, 0.95, 0.80),   // noon — bright white
                    float3(1.00, 0.35, 0.12)    // dusk — deep orange
                );
            }

            // ── Sun / moon direction ─────────────────────────────────
            // Full revolution: rises ~6am (t=0.25), peaks noon (t=0.5), sets ~6pm (t=0.75).
            // Below horizon at night → becomes the moon conceptually.

            float3 sunDirection(float t)
            {
                float a = t * 6.28318;
                float y = -cos(a);           // peaks at t=0.5 (noon)
                float x = sin(a);            // east at dawn, west at dusk
                return normalize(float3(x, y * 0.75 + 0.05, 0.25));
            }

            // ── Star field ───────────────────────────────────────────

            float starField(float2 p, float density)
            {
                float2 cell = floor(p * 150.0);
                float h = hash21(cell);
                float star = step(density, h);
                star *= 0.5 + 0.5 * sin(_Time.y * (1.5 + h * 3.0) + h * 50.0);
                return star;
            }

            // ── Vertex ───────────────────────────────────────────────

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                // Direction from camera to fragment — skybox-like projection.
                // When _PixelDensity > 0, snap screen position to a coarser grid
                // and reconstruct the view direction from that snapped coordinate.
                float3 viewDir;

                if (_PixelDensity > 0)
                {
                    float2 screenUV = IN.positionCS.xy / _ScreenParams.xy;
                    float aspect = _ScreenParams.x / _ScreenParams.y;
                    float2 targetRes = float2(_PixelDensity * aspect, _PixelDensity);
                    float2 snappedUV = (floor(screenUV * targetRes) + 0.5) / targetRes;

                    // Reconstruct world ray from snapped screen coordinate
                    float2 ndc = snappedUV * 2.0 - 1.0;
                    float4 clipPos = float4(ndc, 0.0, 1.0);
                    float4 wp = mul(UNITY_MATRIX_I_VP, clipPos);
                    viewDir = normalize(wp.xyz / wp.w - _WorldSpaceCameraPos);
                }
                else
                {
                    viewDir = normalize(IN.positionWS - _WorldSpaceCameraPos);
                }

                float elevation = viewDir.y;
                float2 horiz = viewDir.xz / max(length(viewDir.xz), 0.001);

                float t = _TimeOfDay;

                // Night factor (0 = day, 1 = deep night)
                float nightFade = 1.0 - smoothstep(0.20, 0.30, t) + smoothstep(0.72, 0.82, t);
                nightFade = saturate(nightFade);

                float3 sunDir = sunDirection(t);
                float sunDot = dot(viewDir, sunDir);
                float3 sCol = sunCol(t);

                // ── 1. Sky gradient ──────────────────────────────────
                float skyBlend = saturate(elevation * 1.8 + 0.5);
                skyBlend *= skyBlend; // ease-in toward zenith

                float3 zenith = zenithColor(t);
                float3 hCol = horizColor(t);
                float3 col = lerp(hCol, zenith, skyBlend);

                // ── 2. Sun / moon disc + glow ────────────────────────
                float disc = smoothstep(_SunSize, _SunSize + 0.008, sunDot);
                float glow = pow(saturate(sunDot), 6.0) * _SunGlow;
                col += sCol * (disc * 1.5 + glow);

                // ── 3. Stars (night only, above horizon) ─────────────
                if (nightFade > 0.01 && elevation > 0.05)
                {
                    float2 starUV = horiz * 3.0 + float2(0, elevation * 5.0);
                    float s = starField(starUV, _StarDensity);
                    col += s * nightFade * smoothstep(0.05, 0.20, elevation) * 0.7;
                }

                // ── 4. Clouds ────────────────────────────────────────
                if (elevation > -0.1)
                {
                    // Dome projection: flatten xz by inverse of elevation
                    float2 cloudUV = viewDir.xz / (elevation + 0.35);
                    cloudUV *= _CloudScale;
                    cloudUV.x += _Time.y * _CloudSpeed;
                    cloudUV.y += _Time.y * _CloudSpeed * 0.3;

                    float cn = fbm5(cloudUV);
                    float threshold = 0.50 - _CloudDensity * 0.35;
                    float cloudMask = smoothstep(threshold, threshold + _CloudSharpness, cn);
                    cloudMask *= smoothstep(-0.10, 0.10, elevation); // fade at horizon

                    // Bright day clouds, dim night clouds
                    float3 cloudCol = lerp(float3(0.92, 0.90, 0.85),
                                           float3(0.06, 0.06, 0.10), nightFade);
                    // Sun highlight on cloud edges
                    cloudCol += sCol * pow(saturate(sunDot), 3.0) * 0.15 * (1.0 - nightFade);

                    col = lerp(col, cloudCol, cloudMask * 0.88);
                }

                // ── 5. Far mountains (blue-hazed silhouette) ─────────
                float mountainN = fbm4(horiz * _MountainScale);
                float mountainLine = mountainN * _MountainHeight;

                if (elevation < mountainLine + 0.015)
                {
                    float mountainMask = smoothstep(mountainLine + 0.008,
                                                    mountainLine - 0.004, elevation);

                    float3 mountainCol = lerp(
                        float3(0.12, 0.14, 0.22),   // lower: blue-gray
                        float3(0.06, 0.08, 0.14),   // upper: darker
                        smoothstep(0.0, _MountainHeight, elevation)
                    );
                    mountainCol *= lerp(1.0, 0.25, nightFade);

                    col = lerp(col, mountainCol, mountainMask);
                }

                // ── 6. Near treeline ─────────────────────────────────
                float treeN = fbm4(horiz * _MountainScale * 3.5 + float2(17.3, 42.1));
                float treeLine = treeN * _TreelineHeight;

                if (elevation < treeLine + 0.008)
                {
                    float treeMask = smoothstep(treeLine + 0.004,
                                                treeLine - 0.002, elevation);

                    // High-freq noise for individual tree spikes
                    float treeDetail = noise2D(horiz * _MountainScale * 20.0);
                    treeMask *= smoothstep(0.3, 0.5, treeDetail + 0.3);

                    float3 treeCol = float3(0.015, 0.035, 0.018);
                    treeCol *= lerp(1.0, 0.15, nightFade);

                    col = lerp(col, treeCol, treeMask);
                }

                // ── 7. Ground (below horizon) ────────────────────────
                if (elevation < 0.0)
                {
                    float groundBlend = smoothstep(0.0, -0.12, elevation);
                    float3 groundCol = float3(0.025, 0.04, 0.025);
                    groundCol *= lerp(1.0, 0.15, nightFade);
                    col = lerp(col, groundCol, groundBlend);
                }

                // ── 8. Horizon fog band ──────────────────────────────
                float fogBand = exp(-abs(elevation) * 12.0) * _HorizonFog;
                float3 fogCol = lerp(hCol, sCol, 0.25) * lerp(1.0, 0.3, nightFade);
                col = lerp(col, fogCol, fogBand);

                col = saturate(col);

                // ── 9. Color quantization (retro color depth) ────────
                if (_ColorDepth > 0)
                    col = floor(col * _ColorDepth + 0.5) / _ColorDepth;

                return half4(col, 1.0);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
