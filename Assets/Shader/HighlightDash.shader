Shader "Iris/HighlightDash"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.95, 0.85, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.05)) = 0.008
        _DashFreq     ("Dash Frequency", Range(2, 60)) = 20.0
        _DashRatio    ("Dash Ratio (solid portion)", Range(0.1, 0.9)) = 0.5
        _ScrollSpeed  ("Scroll Speed", Range(0, 8)) = 2.0
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
            Name "DashedOutline"
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
                float3 positionOS : TEXCOORD0;
            };

            float4 _HighlightSnapResolution;
            float  _HighlightJitter;
            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                half   _OutlineWidth;
                half   _DashFreq;
                half   _DashRatio;
                half   _ScrollSpeed;
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
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Scrolling dashed pattern along object-space position
                float pattern = (input.positionOS.x + input.positionOS.y + input.positionOS.z) * _DashFreq + _Time.y * _ScrollSpeed;
                float dash = step(frac(pattern), _DashRatio);

                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                return half4(_OutlineColor.rgb, _OutlineColor.a * pulse * dash);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
