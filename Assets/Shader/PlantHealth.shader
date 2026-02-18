Shader "Iris/Plant"
{
    Properties
    {
        _Health ("Health", Range(0, 1)) = 1.0
        _HealthyColor ("Healthy Color", Color) = (0.2, 0.5, 0.2, 1)
        _WiltColor ("Wilt Color", Color) = (0.55, 0.35, 0.15, 1)
        _DeadColor ("Dead Color", Color) = (0.4, 0.4, 0.4, 1)
        _DroopAmount ("Droop Amount", Range(0, 0.1)) = 0.05
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
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half  _Health;
                half4 _HealthyColor;
                half4 _WiltColor;
                half4 _DeadColor;
                half  _DroopAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;

                // Leaf droop: vertices far from object-space center XZ droop Y
                float distFromCenter = length(posOS.xz);
                float droop = distFromCenter * (1.0 - _Health) * _DroopAmount * 10.0;
                posOS.y -= droop;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Two-step color interpolation: dead→wilt→healthy
                half h = _Health;
                half3 lowColor = lerp(_DeadColor.rgb, _WiltColor.rgb, saturate(h * 2.0));
                half3 finalColor = lerp(lowColor, _HealthyColor.rgb, saturate(h * 2.0 - 1.0));

                // Simple Lambert lighting
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * NdotL;
                half3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;

                half3 lit = finalColor * (diffuse + ambient + 0.15);

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
                half  _Health;
                half4 _HealthyColor;
                half4 _WiltColor;
                half4 _DeadColor;
                half  _DroopAmount;
            CBUFFER_END

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;
                float distFromCenter = length(posOS.xz);
                float droop = distFromCenter * (1.0 - _Health) * _DroopAmount * 10.0;
                posOS.y -= droop;

                float3 posWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));

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
