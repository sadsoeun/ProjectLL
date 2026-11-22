// Molten lava: glowing flow under drifting dark crust plates, slow heave and a breathing glow. World-space, needs no UVs.
Shader "MyGame/Ambient/Lava"
{
    Properties
    {
        _BaseColor("Hot Color", Color) = (1, 0.35, 0.07, 1)
        [HDR] _EmissionColor("Glow", Color) = (1.6, 0.46, 0, 1)
        _CrustColor("Crust Color", Color) = (0.16, 0.06, 0.05, 1)
        _CrustAmount("Crust Amount", Range(0, 1)) = 0.45
        _Scale("Pattern Scale", Range(0.02, 2)) = 0.35
        _FlowDir("Flow Direction (xz)", Vector) = (0.6, 0.3, 0, 0)
        _FlowSpeed("Flow Speed", Range(0, 2)) = 0.25
        _PulseSpeed("Glow Pulse Speed", Range(0, 6)) = 1.2
        _HeaveHeight("Heave Height (m)", Range(0, 0.3)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WMAmbientCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _CrustColor;
            float _CrustAmount;
            float _Scale;
            float4 _FlowDir;
            float _FlowSpeed;
            float _PulseSpeed;
            float _HeaveHeight;
        CBUFFER_END

        void WMDisplace(inout float3 positionWS, inout float3 normalWS, float3 positionOS, float4 color, float2 uv)
        {
            positionWS.y += sin(_Time.y * 0.8 + positionWS.x * 0.4 + positionWS.z * 0.3) * _HeaveHeight;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define WM_PASS_FORWARD
            #include "WMAmbientPasses.hlsl"

            void WMSurface(WMVaryings input, out half3 albedo, out half alpha, out half3 emission, inout half3 normalWS)
            {
                float t = _Time.y;
                float2 flow = normalize(_FlowDir.xy + 1e-4) * t * _FlowSpeed;
                float2 p = input.positionWS.xz * _Scale;
                // domain-warped noise makes the molten veins swirl
                float2 warp = float2(WMFbm(p + flow * 0.5), WMFbm(p + 5.2 - flow * 0.3));
                float heat = WMFbm(p * 1.6 + warp * 1.5 - flow);
                float plates = WMVoronoi(p * 1.3 + flow * 0.8, t * 0.15);
                half crust = smoothstep(_CrustAmount - 0.08, _CrustAmount + 0.08, plates * 0.8 + (1.0 - heat) * 0.45);
                half pulse = 0.85 + 0.15 * sin(t * _PulseSpeed + heat * 6.0);

                albedo = lerp(_BaseColor.rgb, _CrustColor.rgb, crust);
                alpha = 1.0;
                emission = _EmissionColor.rgb * (1.0 - crust) * pulse * (0.7 + heat * 0.8);
            }
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile_instancing
            #define WM_PASS_DEPTH
            #include "WMAmbientPasses.hlsl"
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define WM_PASS_DEPTHNORMALS
            #include "WMAmbientPasses.hlsl"
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
