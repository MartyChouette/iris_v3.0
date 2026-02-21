Shader "Iris/Fullscreen/PSXPost"
{
    Properties
    {
        _ColorDepth     ("Color Depth (levels per channel)", Float) = 32
        _DitherIntensity ("Dither Intensity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PSXPost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ColorDepth;
                half  _DitherIntensity;
            CBUFFER_END

            // ── Bayer 4×4 ordered dither matrix (normalized to 0–1) ──
            static const float BayerMatrix[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

            half4 Frag(Varyings input) : SV_Target
            {
                half4 screen = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                float levels = max(_ColorDepth, 2.0);

                // ── Ordered dithering ──
                // Screen-space pixel coords mod 4 index into Bayer matrix
                float2 pixelPos = input.texcoord * _ScreenParams.xy;
                int2 ditherCoord = int2(fmod(pixelPos, 4.0));
                int idx = ditherCoord.y * 4 + ditherCoord.x;
                float threshold = BayerMatrix[idx] - 0.5; // center around 0

                float3 c = screen.rgb;
                c += threshold * (1.0 / levels) * _DitherIntensity;

                // ── Color depth reduction (posterization) ──
                c = floor(c * levels + 0.5) / levels;

                return half4(saturate(c), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
