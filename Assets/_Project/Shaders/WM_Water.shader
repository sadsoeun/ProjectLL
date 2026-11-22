// Stylised transparent water.
//  Sea  : gentle vertex waves, depth tint, shoreline foam and drifting ripple highlights (needs the camera depth texture);
//         steep faces (waterfalls without flow UVs) get streaks falling in world space.
//  Flow : rivers / waterfalls, streaks scroll along +V of the mesh UVs.
//  Foam : surf rings around islands, pulse and break up over time.
Shader "MyGame/Ambient/Water"
{
    Properties
    {
        [KeywordEnum(Sea, Flow, Foam)] _Mode("Mode", Float) = 0
        _BaseColor("Deep Color", Color) = (0.15, 0.71, 0.92, 0.82)
        _ShallowColor("Shallow Color", Color) = (0.45, 0.93, 0.95, 0.55)
        _DepthFade("Depth Fade (m)", Range(0.05, 10)) = 2.5
        _WaveHeight("Wave Height (m)", Range(0, 0.5)) = 0.06
        _WaveSpeed("Wave Speed", Range(0, 4)) = 1
        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        _FoamWidth("Shore Foam Width (m)", Range(0, 3)) = 0.6
        _RippleColor("Ripple Color", Color) = (1, 1, 1, 0.5)
        _RippleScale("Ripple Scale", Range(0.05, 4)) = 0.45
        _RippleSpeed("Ripple Speed", Range(0, 4)) = 0.8
        _FlowSpeed("Flow Speed", Range(0, 4)) = 1.2
        _FlowScale("Flow Streak Scale (u, v)", Vector) = (3, 1.5, 0, 0)
        _PulseSpeed("Foam Pulse Speed", Range(0, 6)) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WMAmbientCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShallowColor;
            float _DepthFade;
            float _WaveHeight;
            float _WaveSpeed;
            half4 _FoamColor;
            float _FoamWidth;
            half4 _RippleColor;
            float _RippleScale;
            float _RippleSpeed;
            float _FlowSpeed;
            float4 _FlowScale;
            float _PulseSpeed;
        CBUFFER_END

        float WMWave(float2 p)
        {
            float t = _Time.y * _WaveSpeed;
            return sin(p.x * 0.35 + t * 1.1) * 0.5
                 + sin(p.y * 0.42 - t * 0.9 + p.x * 0.1) * 0.35
                 + sin((p.x + p.y) * 0.8 + t * 1.7) * 0.15;
        }

        void WMDisplace(inout float3 positionWS, inout float3 normalWS, float3 positionOS, float4 color, float2 uv)
        {
        #if defined(_MODE_SEA)
            const float e = 0.15;
            float h = WMWave(positionWS.xz);
            float dx = (WMWave(positionWS.xz + float2(e, 0)) - h) / e;
            float dz = (WMWave(positionWS.xz + float2(0, e)) - h) / e;
            float up = saturate((normalWS.y - 0.7) / 0.3); // only level surfaces wave; waterfall faces keep their normals
            positionWS.y += h * _WaveHeight * up;
            normalWS = normalize(lerp(normalWS, float3(-dx * _WaveHeight, 1.0, -dz * _WaveHeight), up));
        #elif defined(_MODE_FOAM)
            // rings breathe in and out around their own centre
            float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
            float s = sin(_Time.y * _PulseSpeed + dot(pivot.xz, float2(0.31, 0.47)));
            positionWS.xz += (positionWS.xz - pivot.xz) * 0.015 * s;
            positionWS.y += 0.02 * s;
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma shader_feature_local _MODE_SEA _MODE_FLOW _MODE_FOAM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define WM_PASS_FORWARD
            #include "WMAmbientPasses.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Water thickness behind this pixel in metres (large when nothing opaque is behind it).
            float WMThickness(WMVaryings input)
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                return max(0.0, sceneEye - surfaceEye);
            }

            half WMSunGlint(float3 positionWS, half3 normalWS)
            {
                Light sun = GetMainLight();
                half3 h = normalize(sun.direction + GetWorldSpaceNormalizeViewDir(positionWS));
                return smoothstep(0.985, 0.995, saturate(dot(normalWS, h)));
            }

            void WMSurface(WMVaryings input, out half3 albedo, out half alpha, out half3 emission, inout half3 normalWS)
            {
                float t = _Time.y;
                float3 p = input.positionWS;
            #if defined(_MODE_SEA)
                float thickness = WMThickness(input);
                half deep = saturate(thickness / _DepthFade);
                albedo = lerp(_ShallowColor.rgb, _BaseColor.rgb, deep);
                alpha = lerp(_ShallowColor.a, _BaseColor.a, deep);

                // shore foam: bands that roll toward the beach, broken up by noise
                float edge = 1.0 - saturate(thickness / max(_FoamWidth, 1e-3));
                float bands = frac(edge * 2.0 + t * 0.35);
                float n = WMFbm(p.xz * 2.2 + t * 0.25);
                // anti-aliased crest edge (a hard step() shimmered into blocky pixels at map zoom)
                float crestSignal = n * 0.6 + bands * 0.4;
                float crestThreshold = 0.98 - edge;
                float crestWidth = max(fwidth(crestSignal - crestThreshold), 0.015);
                half crest = smoothstep(crestThreshold - crestWidth, crestThreshold + crestWidth, crestSignal);
                half foam = saturate(crest * edge * 1.5 + smoothstep(0.85, 1.0, edge));

                // drifting caustic web: thin cell borders, broken into patches by noise so it never reads as a grid
                float edgeDist = WMVoronoiEdge(p.xz * _RippleScale + float2(t * 0.05, t * 0.03), t * _RippleSpeed);
                half web = 1.0 - smoothstep(0.015, 0.06, edgeDist);
                half patches = smoothstep(0.5, 0.85, WMFbm(p.xz * 0.18 + float2(-t * 0.03, t * 0.02)));
                half ripple = web * patches * _RippleColor.a * (0.3 + 0.7 * deep);

                // steep faces (waterfalls) get world-space streaks falling straight down instead of ripples
                half steep = 1.0 - smoothstep(0.35, 0.7, abs(normalWS.y));
                float across = p.x + p.z;
                float fall = WMFbm(float2(across * 2.5, p.y * 0.7 + t * _FlowSpeed * 1.5));
                float fall2 = WMValueNoise(float2(across * 5.0, p.y * 0.35 + t * _FlowSpeed * 2.2));
                half streak = smoothstep(0.5, 0.68, fall * 0.7 + fall2 * 0.4) * steep;
                ripple *= 1.0 - steep;

                albedo = lerp(albedo, _FoamColor.rgb, saturate(foam + streak * 0.8));
                alpha = lerp(alpha, _FoamColor.a, saturate(foam + streak * 0.6));
                emission = _RippleColor.rgb * ripple + WMSunGlint(p, normalWS) * 0.8 * (1.0 - steep);
            #elif defined(_MODE_FLOW)
                float2 uv = input.uv * _FlowScale.xy;
                float s = WMFbm(float2(uv.x, uv.y - t * _FlowSpeed));
                float s2 = WMValueNoise(float2(uv.x * 2.3, uv.y * 0.6 - t * _FlowSpeed * 1.6));
                half streak = smoothstep(0.55, 0.7, s * 0.7 + s2 * 0.4);
                albedo = lerp(_BaseColor.rgb, _FoamColor.rgb, streak);
                alpha = lerp(_BaseColor.a, _FoamColor.a, streak);
                emission = _RippleColor.rgb * streak * _RippleColor.a * 0.5;
            #else
                float phase = dot(input.pivotWS.xz, float2(0.31, 0.47));
                half pulse = 0.6 + 0.4 * sin(t * _PulseSpeed + phase);
                // uv.x = distance (m) to the nearest ring boundary, uv.y = the ring's max distance (0 on meshes without the bake)
                float d = input.uv.x;
                half hasDist = step(1e-4, input.uv.y);
                half edgeFade = lerp(1.0, smoothstep(0.0, 0.45, d), hasDist);
                // lacy foam: a soft core band plus drifting strands that thin out toward the open water
                float lace = WMFbm(p.xz * 1.6 + float2(t * 0.12, -t * 0.09));
                float strands = WMValueNoise(p.xz * 4.5 + float2(-t * 0.25, t * 0.2));
                float signal = lace * 0.65 + strands * 0.35 + (1.0 - saturate(d / max(input.uv.y, 0.5))) * 0.25 * hasDist;
                float w = max(fwidth(signal), 0.02);
                half breakup = smoothstep(0.42 - w, 0.58 + w, signal);
                albedo = _BaseColor.rgb;
                alpha = _BaseColor.a * pulse * edgeFade * lerp(0.25, 1.0, breakup);
                emission = half3(0, 0, 0);
            #endif
            }
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
