Shader "Iris/RimLight"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0.6)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0.0, 3.0)) = 1.0
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RimLight"
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            // Global PSX properties (set by PSXRenderController)
            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half   _GlitchIntensity;
            CBUFFER_END

            // Hash functions (match PSXLitGlitch exactly)
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float4 clipPos = posInputs.positionCS;

                // PSX vertex snapping (matches PSXLit / PSXLitGlitch)
                float2 snapRes = _VertexSnapResolution.xy;
                if (snapRes.x > 0 && snapRes.y > 0)
                {
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5)
                               / snapRes * clipPos.w;
                }

                // Vertex jitter (matches PSXLitGlitch exactly)
                if (_GlitchIntensity > 0.001)
                {
                    float timeSeed = floor(_Time.y * 6.0);
                    float vertexSeed = Hash21(input.positionOS.xz + timeSeed);

                    float glitchChance = _GlitchIntensity * 0.6;
                    if (vertexSeed < glitchChance)
                    {
                        float jitterX = (Hash11(vertexSeed * 127.1 + timeSeed) - 0.5) * 2.0;
                        float jitterY = (Hash11(vertexSeed * 269.5 + timeSeed) - 0.5) * 2.0;

                        float strength = _GlitchIntensity * 0.08 * clipPos.w;
                        clipPos.xy += float2(jitterX, jitterY) * strength;
                    }
                }

                output.positionCS = clipPos;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half NdotV = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                return half4(_RimColor.rgb, rim * _RimColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
