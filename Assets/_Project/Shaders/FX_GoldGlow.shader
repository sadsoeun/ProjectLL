// Camera-facing procedural gold glow for the golden-leaf pickup: halo, hot core, long vertical cross flare,
// radial rays and a thin ring. Unlit, HDR-capable, no textures. Output is premultiplied (rgb is added, alpha
// slightly darkens what is behind) so it still reads over bright meadow backgrounds and glows on dark ones.
// Render with a unit Quad; world size comes from the object scale. Animation is driven by _Time only.
Shader "MyGame/FX/GoldGlow"
{
    Properties
    {
        [HDR] _Color("Gold", Color) = (1.0, 0.72, 0.22, 1)
        [HDR] _CoreColor("Core", Color) = (1.0, 0.97, 0.80, 1)
        _Intensity("Intensity", Range(0, 4)) = 1.6
        _HaloAlpha("Halo Tint Alpha", Range(0, 1)) = 0.45
        _FlareLength("Flare Length (vertical)", Range(0.1, 1)) = 0.95
        _RayCount("Ray Count", Range(6, 40)) = 22
        _RingRadius("Ring Radius", Range(0.2, 0.9)) = 0.46
        _PulseSpeed("Pulse Speed", Float) = 2.2
        _PulseAmount("Pulse Amount", Range(0, 0.5)) = 0.14
        _ViewOffset("Toward Camera (m)", Float) = -0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                float _Intensity;
                float _HaloAlpha;
                float _FlareLength;
                float _RayCount;
                float _RingRadius;
                float _PulseSpeed;
                float _PulseAmount;
                float _ViewOffset;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 centerWS = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float sx = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float sy = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                o.phase = frac(centerWS.x * 0.173 + centerWS.y * 0.311 + centerWS.z * 0.071) * 6.2831;
                float breathe = 1.0 + _PulseAmount * 0.5 * sin(_Time.y * _PulseSpeed + o.phase);
                float3 viewPos = TransformWorldToView(centerWS);
                viewPos.xy += input.positionOS.xy * float2(sx, sy) * breathe;
                viewPos.z += _ViewOffset; // view space looks down -z, so +z is toward the camera
                o.positionCS = TransformWViewToHClip(viewPos);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                if (r >= 1.0) return half4(0, 0, 0, 0);
                float t = _Time.y;
                float pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed + i.phase);
                float twinkle = 0.78 + 0.22 * sin(t * 7.3 + i.phase * 2.0) * sin(t * 3.1 + i.phase);
                float edge = 1.0 - smoothstep(0.7, 1.0, r); // everything dies before the quad border

                // soft halo and hot core
                float halo = exp(-r * r * 12.0) * 0.32 + exp(-r * 3.4) * 0.12;
                float core = exp(-r * r * 300.0) * 1.5 + exp(-r * r * 60.0) * 0.3;

                // cross flare: vertical much longer than horizontal
                float v = exp(-abs(p.x) * 90.0) * exp(-abs(p.y) / (0.28 * _FlareLength));
                float h = exp(-abs(p.y) * 110.0) * exp(-abs(p.x) / 0.14) * 0.55;
                float flare = (v + h) * twinkle;

                // radial rays (two layers, slowly counter-rotating)
                float a = atan2(p.y, p.x);
                float r1 = pow(saturate(0.5 + 0.5 * cos(a * _RayCount + t * 0.35)), 18.0);
                float r2 = pow(saturate(0.5 + 0.5 * cos(a * (_RayCount * 0.6) - t * 0.25 + 1.7)), 24.0);
                float rays = (r1 * 0.7 + r2 * 0.5) * 0.65 * exp(-r * 4.0) * (0.8 + 0.2 * twinkle);

                // thin ring with a faint doubled lower edge
                float rr = _RingRadius * (1.0 + 0.03 * sin(t * _PulseSpeed + i.phase));
                float ring = exp(-pow((r - rr) / 0.011, 2.0)) * 0.55;
                float r2d = length(p - float2(0.0, 0.025));
                ring += exp(-pow((r2d - rr) / 0.009, 2.0)) * 0.3 * smoothstep(0.1, -0.6, p.y);

                float soft = (halo + rays * 0.9 + ring) * pulse;
                float hot = (core + flare) * pulse;
                half3 rgb = _Color.rgb * soft + _CoreColor.rgb * hot;
                rgb *= _Intensity * edge;
                half alpha = saturate((halo * 0.6 + ring * 0.5 + rays * 0.25) * _HaloAlpha * 1.6) * edge;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
