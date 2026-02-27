Shader "Iris/CurvedWorld"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _GridColor ("Grid Color", Color) = (0.3, 0.3, 0.3, 0.4)
        _GridScale ("Grid Scale", Float) = 2.0
        _GridThickness ("Grid Thickness", Range(0.001, 0.1)) = 0.02
        _CurveStrength ("Curve Strength", Float) = 0.003
        _CurveOffset ("Curve Start Distance", Float) = 8.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "CurvedWorldGrid"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float distFromCam : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _GridColor;
                float _GridScale;
                float _GridThickness;
                float _CurveStrength;
                float _CurveOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Transform to world space
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // Calculate distance from camera on the XZ plane
                float3 camPos = GetCameraPositionWS();
                float2 diff = worldPos.xz - camPos.xz;
                float dist = length(diff);

                // Apply downward curve beyond the offset distance
                float effectiveDist = max(0.0, dist - _CurveOffset);
                worldPos.y -= effectiveDist * effectiveDist * _CurveStrength;

                // Transform curved world pos to clip space
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldPos = worldPos;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.distFromCam = dist;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // World-space grid lines
                float2 gridUV = input.worldPos.xz * _GridScale;
                float2 gridDeriv = fwidth(gridUV);
                float2 gridAA = smoothstep(_GridThickness - gridDeriv, _GridThickness + gridDeriv, abs(frac(gridUV - 0.5) - 0.5));
                float gridMask = 1.0 - min(gridAA.x, gridAA.y);

                // Base color with texture
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = texColor * _BaseColor;

                // Blend grid on top
                half4 color = lerp(baseColor, _GridColor, gridMask * _GridColor.a);

                // Fade out at distance for clean horizon
                float fadeDist = _CurveOffset + 15.0;
                float fade = 1.0 - saturate((input.distFromCam - _CurveOffset) / fadeDist);
                color.a *= fade;

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
