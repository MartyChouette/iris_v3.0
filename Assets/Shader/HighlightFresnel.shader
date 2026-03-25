Shader "Iris/HighlightFresnel"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.95, 0.85, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.05)) = 0.006
        _FresnelPower ("Fresnel Power (edge fade)", Range(0.5, 8.0)) = 2.0
        _FresnelMin   ("Fresnel Min Alpha", Range(0, 1)) = 0.3
        _PulseSpeed   ("Pulse Speed", Range(0, 8)) = 2.0
        _PulseAmount  ("Pulse Amount", Range(0, 0.5)) = 0.1
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
            Name "FresnelOutline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

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

            float4 _HighlightSnapResolution;
            float  _HighlightJitter;
            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                half   _OutlineWidth;
                half   _FresnelPower;
                half   _FresnelMin;
                half   _PulseSpeed;
                half   _PulseAmount;
            CBUFFER_END

            float hashVert(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 norm = normalize(input.normalOS);
                float3 extruded = input.positionOS.xyz + norm * _OutlineWidth;

                if (_HighlightJitter > 0)
                {
                    float jit = hashVert(input.positionOS.xyz + _Time.y * 3.0) * 2.0 - 1.0;
                    extruded += norm * jit * _HighlightJitter;
                }

                VertexPositionInputs posInputs = GetVertexPositionInputs(extruded);
                float4 clipPos = posInputs.positionCS;

                float2 snapRes = _HighlightSnapResolution.xy;
                if (snapRes.x <= 0) snapRes = _VertexSnapResolution.xy;
                if (snapRes.x > 0 && snapRes.y > 0)
                {
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5)
                               / snapRes * clipPos.w;
                }

                output.positionCS = clipPos;
                // Use original (non-extruded) normal/view for fresnel
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Fresnel: brighter at edges (silhouette), fades toward center
                half NdotV = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                // Front-face cull is on, so we see back faces — invert the dot
                NdotV = 1.0 - NdotV;
                half fresnel = pow(1.0 - NdotV, _FresnelPower);
                half alpha = lerp(_FresnelMin, 1.0, fresnel);

                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                return half4(_OutlineColor.rgb, _OutlineColor.a * alpha * pulse);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
