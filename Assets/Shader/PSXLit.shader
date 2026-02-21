Shader "Iris/PSXLit"
{
    Properties
    {
        [Header(Texture)]
        _MainTex ("Albedo", 2D) = "white" {}
        _Color   ("Tint",   Color) = (1, 1, 1, 1)

        [Header(PSX Effects)]
        _VertexSnapResolution ("Vertex Snap Resolution", Vector) = (160, 120, 0, 0)
        _AffineIntensity      ("Affine Intensity", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward Lit Pass ──────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
                float2 uvCorrect   : TEXCOORD3; // plain UV — hardware perspective-corrects
                float3 uvAffine    : TEXCOORD4; // xy = uv * w, z = w — divide to get affine
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float4 _VertexSnapResolution; // xy used
                half   _AffineIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float4 clipPos = posInputs.positionCS;

                // ── Vertex snapping: snap XY to screen-space grid ──
                float2 snapRes = _VertexSnapResolution.xy;
                if (snapRes.x > 0 && snapRes.y > 0)
                {
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5)
                               / snapRes * clipPos.w;
                }

                output.positionCS  = clipPos;
                output.positionWS  = posInputs.positionWS;
                output.normalWS    = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor   = ComputeFogFactor(clipPos.z);

                float2 uv = TRANSFORM_TEX(input.uv, _MainTex);

                // Perspective-correct UV: pass plain — hardware interpolates correctly
                output.uvCorrect = uv;

                // Affine UV trick: multiply by clip w before interpolation.
                // In fragment, dividing xy/z cancels the hardware perspective correction,
                // yielding screen-space linear (affine) interpolation.
                output.uvAffine = float3(uv * clipPos.w, clipPos.w);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Reconstruct both UV modes
                float2 correctUV = input.uvCorrect;
                float2 affineUV  = input.uvAffine.xy / input.uvAffine.z;

                // Blend between perspective-correct and affine
                float2 finalUV = lerp(correctUV, affineUV, _AffineIntensity);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);
                half4 albedo = tex * _Color;

                // Simple Lambert lighting
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * NdotL;
                half3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;

                half3 lit = albedo.rgb * (diffuse + ambient + 0.15);
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        // ── ShadowCaster Pass ─────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float4 _VertexSnapResolution;
                half   _AffineIntensity;
            CBUFFER_END

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
