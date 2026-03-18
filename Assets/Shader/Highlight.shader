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

            // Prevent additive overdraw on complex geometry:
            // first fragment at each pixel writes 200, subsequent fragments are discarded.
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
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            float4 _HighlightSnapResolution;
            float  _HighlightJitter;
            float  _HighlightNormalOffset;
            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _HighlightColor;
                half4  _RimColor;
                half   _RimPower;
                half   _PulseSpeed;
                half   _PulseAmount;
            CBUFFER_END

            float hashVert(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 norm = normalize(input.normalOS);
                float offset = _HighlightNormalOffset > 0 ? _HighlightNormalOffset : 0.001;
                float3 pos = input.positionOS.xyz + norm * offset;

                if (_HighlightJitter > 0)
                {
                    float jit = hashVert(input.positionOS.xyz + _Time.y * 3.0) * 2.0 - 1.0;
                    pos += norm * jit * _HighlightJitter;
                }

                VertexPositionInputs posInputs = GetVertexPositionInputs(pos);
                float4 clipPos = posInputs.positionCS;

                float2 snapRes = _HighlightSnapResolution.xy;
                if (snapRes.x <= 0) snapRes = _VertexSnapResolution.xy;
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
