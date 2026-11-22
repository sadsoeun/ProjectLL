#ifndef WM_TOON_INCLUDED
#define WM_TOON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl" // LinearToSRGB / SRGBToLinear

// Toon (cel) shading shared by MyGame/Toon/Lit and MyGame/Ambient/Wind Lit.
// Usage: include inside HLSLINCLUDE after Core.hlsl, put WM_TOON_PROPS inside the UnityPerMaterial CBUFFER,
// define WM_TOON in the ForwardLit pass before including WMAmbientFrag.hlsl.

TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

// Every property used by the toon path. Wind Lit appends its own wind fields.
#define WM_TOON_PROPS \
    float4 _BaseMap_ST; \
    half4 _BaseColor; \
    half4 _EmissionColor; \
    half4 _ShadowColor; \
    half4 _RimColor; \
    half _RimPower; \
    half _ShadeSteps; \
    half _ShadeSmoothness; \
    half _ToonMode; \
    half _MacroStrength; \
    half _MacroScale; \
    half _TileVariation; \
    half _ShadowFromAlbedo; \
    half _ShadowHueShift; \
    half _ShadowSaturation; \
    half _ShadowValue; \
    float _Cull;

// Macros (not functions): the UnityPerMaterial CBUFFER is declared after this include.
#define WMSampleAlbedo(uv) (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, (uv) * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * _BaseColor.rgb)
#define WMSampleEmission(uv) (SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, (uv) * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * _EmissionColor.rgb)

// 0..1 light amount -> stepped value in [0,1] with (steps) bands; w = half width of the soft edge between bands.
half WMToonRamp(half x, half steps, half w)
{
    half sum = 0.0;
    w = max(w, 0.0005h);
    [unroll] for (int i = 1; i < 4; i++)
    {
        half edge = i / steps;
        half on = (i < steps - 0.5h) ? 1.0h : 0.0h;
        sum += on * smoothstep(edge - w, edge + w, x);
    }
    return sum / max(steps - 1.0h, 1.0h);
}

// Shadow-side albedo derived from the painted colour (same idea as MyGame/Toon/2Tone): HSV shift in sRGB space.
// Written as a macro-free function taking the parameters, because the material CBUFFER is declared after this include.
half3 WMShadowAlbedo(half3 albedoLinear, half hueShift, half satMul, half valMul)
{
    float3 c = saturate((float3)albedoLinear);
    #if !defined(UNITY_COLORSPACE_GAMMA)
        c = LinearToSRGB(c);
    #endif
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float3 hsv = float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1.0e-5)), d / (q.x + 1.0e-5), q.x);
    hsv.x = frac(hsv.x + hueShift + 1.0);
    hsv.y = saturate(hsv.y * satMul);
    hsv.z *= valMul;
    float3 r = abs(frac(hsv.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    c = hsv.z * lerp(float3(1, 1, 1), saturate(r - 1.0), hsv.y);
    #if !defined(UNITY_COLORSPACE_GAMMA)
        c = SRGBToLinear(c);
    #endif
    return (half3)c;
}

// Breaks visible tiling on repeated modules: soft world-space colour patches (two octaves of value noise over the
// stage XY plane, depth folded in) plus a small per-object brightness offset hashed from the pivot.
// strength 0 and tileVar 0 return albedo unchanged. Uses WMHash21 / WMValueNoise from WMAmbientCommon.hlsl.
half3 WMMacroVary(half3 albedo, float3 positionWS, float3 pivotWS, half strength, half scale, half tileVar)
{
    float2 p = float2(positionWS.x + positionWS.z * 0.37, positionWS.y) / max(scale, 0.01);
    half n = (half)(WMValueNoise(p) * 0.65 + WMValueNoise(p * 2.31 + 17.3) * 0.35);
    n = smoothstep(0.2h, 0.8h, n); // broader painted patches, fewer mid tones
    half3 cool = half3(0.86h, 0.88h, 0.96h);
    half3 warm = half3(1.08h, 1.03h, 0.94h);
    half3 macro = lerp(half3(1, 1, 1), lerp(cool, warm, n), strength);
    half tile = 1.0h + ((half)WMHash21(floor(pivotWS.xy * 2.0 + 0.25) + pivotWS.z) - 0.5h) * 2.0h * tileVar;
    return albedo * macro * tile;
}

#endif
