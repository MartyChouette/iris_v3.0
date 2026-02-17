Shader "Iris/Fullscreen/Duotone"
{
    Properties
    {
        _ColorDark  ("Dark Color",  Color) = (0, 0, 0, 1)
        _ColorLight ("Light Color", Color) = (1, 1, 1, 1)
        _Threshold  ("Threshold",   Range(0, 1)) = 0.5
        _Softness   ("Softness",    Range(0, 0.5)) = 0.0
        _Blend      ("Blend",       Range(0, 1)) = 0.0
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
            Name "Duotone"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDark;
                half4 _ColorLight;
                half  _Threshold;
                half  _Softness;
                half  _Blend;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                half4 screen = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // Early out when effect is fully off
                if (_Blend < 0.001)
                    return screen;

                // Perceptual luminance (Rec. 709)
                half lum = dot(screen.rgb, half3(0.2126, 0.7152, 0.0722));

                // Hard threshold (_Softness == 0) or smooth blend
                half t = (_Softness > 0.001)
                    ? smoothstep(_Threshold - _Softness, _Threshold + _Softness, lum)
                    : step(_Threshold, lum);

                half3 duotone = lerp(_ColorDark.rgb, _ColorLight.rgb, t);

                // Blend between original and duotone
                half3 result = lerp(screen.rgb, duotone, _Blend);
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
