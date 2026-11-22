// World map node halo: procedural additive rings on a flat quad (UV 0..1, centre 0.5). No texture.
// Outer thin ring + dashed rune ring + soft glow + one expanding ripple (_Ripple 0..1, driven by WorldMapNode).
Shader "MyGame/WorldMap/NodeRing"
{
    Properties
    {
        [HDR] _BaseColor("Colour", Color) = (0.3, 0.75, 1, 1)
        _Intensity("Intensity", Range(0, 4)) = 1
        _Ripple("Ripple Phase", Range(0, 1)) = 0
        _InnerRadius("Inner Radius (ripple start)", Range(0, 1)) = 0.62
        _Dashes("Dash Count", Float) = 24
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha // premultiplied: stays visible on bright paths, unlike additive
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Intensity;
                float _Ripple;
                float _InnerRadius;
                float _Dashes;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Soft band centred on c with half-width w.
            half Band(float d, float c, float w)
            {
                float fw = max(fwidth(d), 1e-4);
                return saturate(1.0 - (abs(d - c) - w) / fw) * saturate(1.0 - abs(d - c) / (w + 2.0 * fw));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                float d = length(p) * 2.0;
                float a = atan2(p.y, p.x) / (2.0 * PI) + 0.5;

                half outer = Band(d, 0.90, 0.012);
                float dash = frac(a * _Dashes);
                half dashMask = smoothstep(0.08, 0.16, dash) * (1.0 - smoothstep(0.52, 0.60, dash));
                half runes = Band(d, 0.78, 0.018) * dashMask;
                half glow = exp(-pow((d - 0.80) / 0.11, 2.0)) * 0.22;

                float rr = lerp(_InnerRadius, 0.98, _Ripple);
                half ripple = Band(d, rr, 0.02) * (1.0 - _Ripple) * smoothstep(0.0, 0.08, _Ripple);

                half m = outer * 0.9 + runes * 0.75 + glow + ripple;
                m *= 1.0 - smoothstep(0.97, 1.0, d); // never reach the quad edge
                half alpha = saturate(m * _Intensity * _BaseColor.a * 0.85);
                // Core of each line drifts toward white so the ring reads crisp on any ground colour.
                half3 col = lerp(_BaseColor.rgb, half3(1, 1, 1), saturate(m * 0.35)) * (1.0 + 0.25 * _Intensity);
                col = MixFog(col, input.fogFactor);
                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
