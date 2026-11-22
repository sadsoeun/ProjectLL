// Tiny procedural four-point gold star for particles (pickup sparkles and collect burst). Additive, unlit,
// HDR tint x particle vertex colour. No texture needed.
Shader "MyGame/FX/GoldSparkle"
{
    Properties
    {
        [HDR] _Color("Tint", Color) = (1.0, 0.8, 0.3, 1)
        _Sharpness("Star Sharpness", Range(1, 12)) = 5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+11" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Sharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color * _Color;
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = abs(i.uv * 2.0 - 1.0);
                float r = length(p);
                // four-point star plus a small hot core and soft glow
                float star = saturate(1.0 - (sqrt(p.x) + sqrt(p.y)));
                star = pow(star, 1.0 + 8.0 / _Sharpness) * 3.0;
                float core = exp(-r * r * 22.0) * 1.2;
                float glow = exp(-r * r * 5.0) * 0.25;
                float edge = 1.0 - smoothstep(0.75, 1.0, r);
                float v = saturate(star + core + glow) * edge;
                half4 c = i.color;
                return half4(c.rgb * (1.0 + core * 0.8), c.a * v);
            }
            ENDHLSL
        }
    }
}
