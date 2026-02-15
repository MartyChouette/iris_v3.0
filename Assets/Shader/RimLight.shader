Shader "Iris/RimLight"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0.6)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0.0, 3.0)) = 1.0
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

            CBUFFER_START(UnityPerMaterial)
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
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
