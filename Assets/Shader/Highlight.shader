Shader "Iris/Highlight"
{
    Properties
    {
        _HighlightColor ("Highlight Color", Color) = (1, 0.95, 0.85, 0.25)
        _RimColor       ("Rim Color", Color) = (1, 0.95, 0.85, 0.5)
        _RimPower       ("Rim Power", Range(0.5, 8.0)) = 2.5
        _PulseSpeed     ("Pulse Speed", Range(0, 8)) = 2.0
        _PulseAmount    ("Pulse Amount", Range(0, 0.5)) = 0.1
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
            Name "Highlight"
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
                half4  _HighlightColor;
                half4  _RimColor;
                half   _RimPower;
                half   _PulseSpeed;
                half   _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float4 clipPos = posInputs.positionCS;

                // PSX vertex snapping (matches PSXLit)
                float2 snapRes = _VertexSnapResolution.xy;
                if (snapRes.x > 0 && snapRes.y > 0)
                {
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5)
                               / snapRes * clipPos.w;
                }

                output.positionCS = clipPos;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half NdotV = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));

                // Rim glow at edges (stronger at grazing angles)
                half rim = pow(1.0 - NdotV, _RimPower);

                // Flat fill across the whole surface (visible on front faces too)
                half fill = _HighlightColor.a;

                // Gentle pulse
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // Combine: flat fill + rim boost, all pulsing
                half alpha = saturate(fill + rim * _RimColor.a) * pulse;

                // Blend the two colors: fill uses HighlightColor, rim adds RimColor
                half3 col = lerp(_HighlightColor.rgb, _RimColor.rgb, rim);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
