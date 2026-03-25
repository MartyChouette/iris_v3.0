Shader "Iris/HighlightDouble"
{
    Properties
    {
        _OutlineColor  ("Outer Color", Color) = (1, 0.95, 0.85, 1)
        _InnerColor    ("Inner Color", Color) = (1, 0.8, 0.6, 0.5)
        _OutlineWidth  ("Outer Width", Range(0.001, 0.05)) = 0.010
        _InnerWidth    ("Inner Width", Range(0.001, 0.03)) = 0.004
        _PulseSpeed    ("Pulse Speed", Range(0, 8)) = 2.0
        _PulseAmount   ("Pulse Amount", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Pass 1: Outer (thicker) outline ──
        Pass
        {
            Name "OuterOutline"
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
            };

            float4 _HighlightSnapResolution;
            float  _HighlightJitter;
            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                half4  _InnerColor;
                half   _OutlineWidth;
                half   _InnerWidth;
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
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5) / snapRes * clipPos.w;

                output.positionCS = clipPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                return half4(_OutlineColor.rgb, _OutlineColor.a * pulse);
            }
            ENDHLSL
        }

        // ── Pass 2: Inner (thinner, different color) outline ──
        Pass
        {
            Name "InnerOutline"
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
            };

            float4 _HighlightSnapResolution;
            float  _HighlightJitter;
            float4 _VertexSnapResolution;

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                half4  _InnerColor;
                half   _OutlineWidth;
                half   _InnerWidth;
                half   _PulseSpeed;
                half   _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 norm = normalize(input.normalOS);
                float3 extruded = input.positionOS.xyz + norm * _InnerWidth;

                VertexPositionInputs posInputs = GetVertexPositionInputs(extruded);
                float4 clipPos = posInputs.positionCS;

                float2 snapRes = _HighlightSnapResolution.xy;
                if (snapRes.x <= 0) snapRes = _VertexSnapResolution.xy;
                if (snapRes.x > 0 && snapRes.y > 0)
                    clipPos.xy = floor(clipPos.xy / clipPos.w * snapRes + 0.5) / snapRes * clipPos.w;

                output.positionCS = clipPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed * 1.5) * _PulseAmount * 0.5;
                return half4(_InnerColor.rgb, _InnerColor.a * pulse);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
