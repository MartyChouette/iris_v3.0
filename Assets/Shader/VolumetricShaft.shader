Shader "Iris/VolumetricShaft"
{
    Properties
    {
        _ShaftColor ("Shaft Color", Color) = (1, 0.95, 0.85, 0.15)
        _FadeStart  ("Fade Start", Float) = 0.0
        _FadeEnd    ("Fade End", Float) = 1.0
        _NoiseScale ("Noise Scale", Float) = 3.0
        _NoiseSpeed ("Noise Speed", Float) = 0.3
        _NoiseAmount("Noise Amount", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VolumetricShaft"

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _ShaftColor;
                float  _FadeStart;
                float  _FadeEnd;
                float  _NoiseScale;
                float  _NoiseSpeed;
                half   _NoiseAmount;
            CBUFFER_END

            // Simple hash noise
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;
                output.worldPos = posInputs.positionWS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // UV.y = 0 at window, 1 at far end of shaft
                float dist = saturate((input.uv.y - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));

                // Fade: strong near window, fades out
                float fade = 1.0 - dist;
                fade = fade * fade; // quadratic falloff for softer look

                // Animated noise for dusty/volumetric feel
                float2 noiseUV = input.worldPos.xz * _NoiseScale + _Time.y * _NoiseSpeed;
                float noise = valueNoise(noiseUV);
                noise = lerp(1.0, noise, _NoiseAmount);

                float alpha = _ShaftColor.a * fade * noise;

                half3 color = _ShaftColor.rgb;
                color = MixFog(color, input.fogFactor);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
