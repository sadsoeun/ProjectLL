// Toon-lit (or flat Blinn-Phong when Toon Mode is off) surface that sways in the global wind (foliage, grass, flowers, banners, sails).
// Sway weight = vertex colour R (0 anchored .. 1 free). Without vertex colours, enable Height Weight to derive it from object-space height.
Shader "MyGame/Ambient/Wind Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (UV0)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color (tint)", Color) = (1, 1, 1, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle] _ToonMode("Toon Shading", Float) = 1
        [IntRange] _ShadeSteps("Shade Steps", Range(2, 4)) = 3
        _ShadeSmoothness("Shade Smoothness", Range(0, 0.25)) = 0.03
        _ShadowColor("Shadow Tint", Color) = (0.72, 0.7, 0.84, 1)
        [MaterialToggle] _ShadowFromAlbedo("Shadow From Albedo (HSV)", Float) = 0
        _ShadowHueShift("Shadow Hue Shift", Range(-0.25, 0.25)) = -0.02
        _ShadowSaturation("Shadow Saturation", Range(0, 2)) = 1.2
        _ShadowValue("Shadow Value", Range(0, 1)) = 0.75
        [HDR] _RimColor("Rim Color", Color) = (0.3, 0.32, 0.38, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3
        _WindStrength("Wind Strength (m)", Range(0, 1)) = 0.12
        _WindSpeed("Wind Speed", Range(0, 6)) = 1.6
        _Flutter("Flutter", Range(0, 1)) = 0.25
        [Toggle] _HeightWeight("Height Weight (no vertex colours)", Float) = 0
        _HeightRange("Height Range (start, end)", Vector) = (0.3, 3, 0, 0)
        [Header(Tiling Breakup)] _MacroStrength("Macro Colour Strength", Range(0, 1)) = 0
        _MacroScale("Macro Patch Size (m)", Range(0.5, 20)) = 5
        _TileVariation("Per-Module Brightness Variation", Range(0, 0.2)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WMAmbientCommon.hlsl"
        #include "WMToon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            WM_TOON_PROPS
            float _WindStrength;
            float _WindSpeed;
            float _Flutter;
            float _HeightWeight;
            float4 _HeightRange;
        CBUFFER_END

        void WMDisplace(inout float3 positionWS, inout float3 normalWS, float3 positionOS, float4 color, float2 uv)
        {
            float weight = _HeightWeight > 0.5 ? smoothstep(_HeightRange.x, _HeightRange.y, positionOS.y) : color.r;
            float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
            positionWS += WMWindOffset(positionWS, pivotWS, weight, _WindStrength, _WindSpeed, _Flutter);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define WM_PASS_FORWARD
            #define WM_TOON
            #include "WMAmbientPasses.hlsl"

            void WMSurface(WMVaryings input, out half3 albedo, out half alpha, out half3 emission, inout half3 normalWS)
            {
                albedo = WMMacroVary(WMSampleAlbedo(input.uv), input.positionWS, input.pivotWS, _MacroStrength, _MacroScale, _TileVariation);
                alpha = 1.0;
                emission = WMSampleEmission(input.uv);
            }
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define WM_PASS_SHADOW
            #include "WMAmbientPasses.hlsl"
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]

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
            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define WM_PASS_DEPTHNORMALS
            #define SCREEN_OUTLINE_BASE_MASK 0.5   // foliage: soft screen outline (silhouettes only)
            #include "WMAmbientPasses.hlsl"
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
