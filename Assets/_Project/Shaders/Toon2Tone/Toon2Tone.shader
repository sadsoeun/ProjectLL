// 2-tone anime cel shader for characters / monsters (URP 17, Unity 6).
// Lit side = albedo x LitColor x (light tint); shadow side = per-pixel HSV-shifted albedo x ShadowColor.
// N.L, realtime shadow and additional lights all go through one hard step -> exactly two colour fields.
// Configure with ToonShaderGUI (or ToonShaderGUI.SetupMaterial(material) from scripts).
Shader "MyGame/Toon/2Tone"
{
    Properties
    {
        // ---- Surface
        [Enum(Opaque, 0, Transparent, 1)] _Surface("Surface Type", Float) = 0
        [ToggleUI] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [ToggleUI] _CastShadows("Cast Shadows", Float) = 1
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1
        [IntRange] _QueueOffset("Queue Offset", Range(-50, 50)) = 0
        _DepthOffsetFactor("Depth Offset Factor", Range(-2, 2)) = 0
        _DepthOffsetUnits("Depth Offset Units", Range(-50, 50)) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1

        // ---- Base
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        // ---- 2-Tone
        _LitColor("Lit Color", Color) = (1, 1, 1, 1)
        _ShadowColor("Shadow Color (tint)", Color) = (1, 1, 1, 1)
        _Threshold("Threshold (N.L)", Range(-1, 1)) = 0
        _BandSoftness("Band Softness", Range(0, 0.3)) = 0.01
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 1
        _ShadowHueShift("Shadow Hue Shift", Range(-0.25, 0.25)) = -0.02
        _ShadowSaturation("Shadow Saturation", Range(0, 2)) = 1.25
        _ShadowValue("Shadow Value", Range(0, 1)) = 0.65
        _MainLightColorInfluence("Main Light Color Influence", Range(0, 1)) = 0.5
        _IndirectStrength("Indirect (Ambient) Strength", Range(0, 1)) = 0.2

        // ---- Shadows (per-material extra bias, metres, on top of URP bias)
        _ShadowDepthBias("Extra Shadow Depth Bias", Range(0, 0.2)) = 0
        _ShadowNormalBias("Extra Shadow Normal Bias", Range(0, 0.2)) = 0

        // ---- Shade mask (Linear): R threshold offset (0.5 = none), G rim mask, B spec mask
        [NoScaleOffset] _ShadeMap("Shade Map (R offset, G rim, B spec)", 2D) = "gray" {}
        _ShadeMapStrength("Shade Map Offset Strength", Range(0, 1)) = 1
        _ShadeMaskLit("Shade Map B: Lit Amount (opt-in, 0 = off)", Range(0, 1)) = 0
        _OutlineMaskG("Shade Map G: Outline Suppress (opt-in, 0 = off)", Range(0, 1)) = 0
        _OutlineVertexMask("Vertex colour R scales the outline (opt-in, 0 = off)", Range(0, 1)) = 0

        // ---- Face
        [ToggleUI] _FaceShading("Face Shading", Float) = 0
        _FaceForward("Face Forward", Vector) = (0, 0, 1, 0)
        _FaceUp("Face Up", Vector) = (0, 1, 0, 0)
        [Enum(Object, 0, World, 1)] _FaceVectorSpace("Face Vector Space", Float) = 0
        _FaceNormalBlend("Face N.L Blend", Range(0, 1)) = 0
        [NoScaleOffset] _FaceSDF("Face SDF (R)", 2D) = "black" {}
        [ToggleUI] _FaceSDFFlip("Face SDF Flip", Float) = 0

        // ---- Rim
        [ToggleUI] _RimOn("Rim", Float) = 0
        [HDR] _RimColor("Rim Color", Color) = (0.5, 0.5, 0.5, 1)
        _RimWidth("Rim Width", Range(0, 1)) = 0.3
        _RimLitOnly("Rim Lit Side Only", Range(0, 1)) = 1

        // ---- Specular
        [ToggleUI] _SpecularOn("Specular", Float) = 0
        [HDR] _SpecularColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _SpecularSize("Specular Size", Range(0, 0.5)) = 0.05

        // ---- Emission
        [ToggleUI] _EmissionOn("Emission", Float) = 0
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)

        // ---- Outline (inverted hull, SRPDefaultUnlit pass)
        [ToggleUI] _OutlineOn("Outline", Float) = 0
        _OutlineColor("Outline Color", Color) = (0.4, 0.4, 0.4, 1)
        _OutlineWidth("Outline Width (px @1080p)", Range(0, 10)) = 1.5
        _OutlineAlbedoMix("Outline Albedo Mix", Range(0, 1)) = 1
        _OutlineDistanceClamp("Outline Constant-Width Distance", Range(0.5, 50)) = 8
        _OutlineOffsetFactor("Outline Depth Offset Factor (opt-in)", Range(0, 10)) = 0
        _OutlineOffsetUnits("Outline Depth Offset Units (opt-in)", Range(0, 500)) = 0

        // ---- Additional lights
        _AdditionalLightStrength("Additional Light Strength", Range(0, 1)) = 1
        _AdditionalLightTint("Additional Light Tint", Range(0, 1)) = 0.5
        _AdditionalLightThreshold("Additional Light Threshold", Range(0.01, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Includes/ToonInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            Offset [_DepthOffsetFactor], [_DepthOffsetUnits]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonForwardVertex
            #pragma fragment ToonForwardFragment

            // material keywords
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SHADEMAP
            #pragma shader_feature_local_fragment _FACE_SHADING
            #pragma shader_feature_local_fragment _FACE_SDF
            #pragma shader_feature_local_fragment _RIM_ON
            #pragma shader_feature_local_fragment _SPECULAR_ON
            #pragma shader_feature_local_fragment _EMISSION

            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #define TOON_PASS_FORWARD
            #include "Includes/ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend One Zero
            Offset [_OutlineOffsetFactor], [_OutlineOffsetUnits]   // 0,0 = unchanged; pushes the hull back to hide specks on thin / folded meshes

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonOutlineVertex
            #pragma fragment ToonOutlineFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #define TOON_PASS_OUTLINE
            #include "Includes/ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonShadowVertex
            #pragma fragment ToonShadowFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #define TOON_PASS_SHADOWCASTER
            #include "Includes/ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonDepthVertex
            #pragma fragment ToonDepthFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #define TOON_PASS_DEPTHONLY
            #include "Includes/ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonDepthNormalsVertex
            #pragma fragment ToonDepthNormalsFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #define TOON_PASS_DEPTHNORMALS
            #include "Includes/ToonPasses.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "ToonShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
