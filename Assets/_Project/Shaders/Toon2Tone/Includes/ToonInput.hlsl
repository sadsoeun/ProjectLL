#ifndef TOON2TONE_INPUT_INCLUDED
#define TOON2TONE_INPUT_INCLUDED

// Shared material inputs for "MyGame/Toon/2Tone".
// Every pass includes this file through the shader's HLSLINCLUDE block, so the UnityPerMaterial
// constant buffer layout is identical in all passes (SRP Batcher requirement).
// Textures/samplers live outside the CBUFFER (also an SRP Batcher requirement).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;

    // 2-tone core
    half4  _LitColor;
    half4  _ShadowColor;
    half   _Threshold;
    half   _BandSoftness;
    half   _ShadowStrength;
    half   _ShadowHueShift;
    half   _ShadowSaturation;
    half   _ShadowValue;
    half   _MainLightColorInfluence;
    half   _IndirectStrength;

    // shadows
    float  _ShadowDepthBias;
    float  _ShadowNormalBias;

    // shade mask
    half   _ShadeMapStrength;

    // face
    float4 _FaceForward;
    float4 _FaceUp;
    half   _FaceVectorSpace;
    half   _FaceNormalBlend;
    half   _FaceSDFFlip;

    // rim / specular / emission
    half4  _RimColor;
    half   _RimWidth;
    half   _RimLitOnly;
    half4  _SpecularColor;
    half   _SpecularSize;
    half4  _EmissionColor;

    // outline
    half4  _OutlineColor;
    float  _OutlineWidth;
    half   _OutlineAlbedoMix;
    float  _OutlineDistanceClamp;

    // additional lights
    half   _AdditionalLightStrength;
    half   _AdditionalLightTint;
    half   _AdditionalLightThreshold;

    // opt-in: ShadeMap blue channel pulls the 2-tone result toward the lit tone (e.g. anime face skin); 0 = off
    half   _ShadeMaskLit;
    half   _OutlineVertexMask; // opt-in: vertex colour red scales the outline width (0 = off)
    half   _OutlineMaskG;   // opt-in: ShadeMap green channel removes the outline (painted facial linework)
CBUFFER_END

TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
TEXTURE2D(_ShadeMap);     SAMPLER(sampler_ShadeMap);
TEXTURE2D(_FaceSDF);      SAMPLER(sampler_FaceSDF);
TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);

struct ToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Albedo (rgb) and alpha, alpha clip applied when _ALPHATEST_ON.
half4 ToonSampleBase(float2 uv)
{
    half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
    #if defined(_ALPHATEST_ON)
        clip(c.a - _Cutoff);
    #endif
    return c;
}

half ToonSampleAlphaClipOnly(float2 uv)
{
    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
    #if defined(_ALPHATEST_ON)
        clip(a - _Cutoff);
    #endif
    return a;
}

#endif // TOON2TONE_INPUT_INCLUDED
