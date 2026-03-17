Shader "Iris/HighlightOverlay"
{
    Properties
    {
        _OverlayColor ("Overlay Color", Color) = (1, 0.95, 0.85, 0.15)
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
            Name "Overlay"
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            // Depth bias to prevent z-fighting with base mesh
            Offset -1, -1

            // Prevent overdraw on complex geometry
            Stencil
            {
                Ref 200
                Comp NotEqual
                Pass Replace
            }

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
            };

            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _OverlayColor;
                half   _PulseSpeed;
                half   _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Slight normal offset to prevent z-fighting
                float3 pos = input.positionOS.xyz + normalize(input.normalOS) * 0.001;
                VertexPositionInputs posInputs = GetVertexPositionInputs(pos);
                float4 clipPos = posInputs.positionCS;

                // PSX vertex snapping
                float2 snapRes = _VertexSnapResolution.xy;
                if (snapRes.x > 0 && snapRes.y > 0)
                {
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5)
                               / snapRes * clipPos.w;
                }

                output.positionCS = clipPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                return half4(_OverlayColor.rgb, _OverlayColor.a * pulse);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
