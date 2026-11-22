#ifndef TOON2TONE_LIGHTING_INCLUDED
#define TOON2TONE_LIGHTING_INCLUDED

// 2-tone cel lighting for "MyGame/Toon/2Tone". Include after ToonInput.hlsl (forward pass only).
//
// Order of operations (main light) - every darkening term goes through ONE hard step, then ONE select:
//   x        = N.L (or face term) + shadeMap.R offset
//   bandNL   = step(x - _Threshold)                       (fwidth-AA'd narrow transition + _BandSoftness)
//   bandSh   = step(shadowAttenuation - 0.5)              (realtime shadow hard-stepped the same way)
//   lit      = min(bandNL, bandSh) * lightLayerVisible    (min -> no double darkening, never a 3rd tone)
//   lit      = max(lit, additionalLightMask * _AdditionalLightStrength)
//   color    = lerp(shadowTone, litTone, lit)             (the only 2-tone select)
//   color   *= flat ambient factor                        (uniform per object -> no gradient)
//   color   += rim / specular (optional, hard-stepped)    ; emission added by the pass
// Light colour / distance attenuation never multiply the gradient directly, so they cannot add bands.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---------------------------------------------------------------------------------------------
// HSV helpers (float for precision on mobile).
// ---------------------------------------------------------------------------------------------
float3 Toon_RGBToHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    const float e = 1.0e-5;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 Toon_HSVToRGB(float3 c)
{
    float3 p = abs(frac(c.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * lerp(float3(1.0, 1.0, 1.0), saturate(p - 1.0), c.y);
}

// Shadow-side albedo derived per pixel from the lit albedo, so one material matches any palette.
// HSV is evaluated in sRGB (perceptual) space: _ShadowValue 0.65 means "65% of the painted brightness".
half3 Toon_ShadowAlbedo(half3 albedoLinear)
{
    float3 c = saturate((float3)albedoLinear);
    #if !defined(UNITY_COLORSPACE_GAMMA)
        c = LinearToSRGB(c);
    #endif
    float3 hsv = Toon_RGBToHSV(c);
    hsv.x = frac(hsv.x + _ShadowHueShift + 1.0);
    hsv.y = saturate(hsv.y * _ShadowSaturation);
    hsv.z = hsv.z * _ShadowValue;
    c = Toon_HSVToRGB(hsv);
    #if !defined(UNITY_COLORSPACE_GAMMA)
        c = SRGBToLinear(c);
    #endif
    return (half3)c * _ShadowColor.rgb;
}

// ---------------------------------------------------------------------------------------------
// Hard step with narrow anti-aliasing. Returns 0 (shadow) .. 1 (lit).
// ---------------------------------------------------------------------------------------------
half Toon_StepAA(half x, half softness)
{
    half w = max(fwidth(x), 1.0e-4h) * 0.75h + softness;
    return smoothstep(-w, w, x);
}

// Loop-safe version (no derivatives inside dynamic loops): caller supplies the AA width.
half Toon_StepW(half x, half w)
{
    w = max(w, 1.0e-4h);
    return smoothstep(-w, w, x);
}

// ---------------------------------------------------------------------------------------------
// Face shading: light angle around the head instead of per-pixel N.L.
// ---------------------------------------------------------------------------------------------
void Toon_FaceBasis(out float3 F, out float3 U, out float3 R)
{
    float3 f = _FaceForward.xyz;
    float3 u = _FaceUp.xyz;
    if (_FaceVectorSpace < 0.5)
    {
        f = TransformObjectToWorldDir(f);
        u = TransformObjectToWorldDir(u);
    }
    U = SafeNormalize(u);
    F = SafeNormalize(f - U * dot(f, U)); // orthogonalise forward against up
    R = cross(U, F);                       // Unity left-handed: right = up x forward
}

// Returns a signed term in the same domain as N.L (positive = lit); compared with _Threshold.
half Toon_FaceTerm(float3 N, float3 L, float2 uv)
{
    float3 F, U, R;
    Toon_FaceBasis(F, U, R);

    // light projected onto the head's horizontal plane
    float3 Lp = L - U * dot(L, U);
    float lenLp = length(Lp);
    Lp = (lenLp > 1.0e-4) ? Lp / lenLp : F; // light straight above/below -> treat as frontal

    float fwd = dot(F, Lp); // 1 front, -1 behind

    #if defined(_FACE_SDF)
        // SDF convention: texture is authored for light coming from the character's RIGHT side (+R).
        // For light from the left, U is mirrored. _FaceSDFFlip swaps the convention.
        float side = dot(R, Lp);
        float mirror = (side < 0.0) ? 1.0 : 0.0;
        mirror = (_FaceSDFFlip > 0.5) ? 1.0 - mirror : mirror;
        float2 sdfUV = float2(lerp(uv.x, 1.0 - uv.x, mirror), uv.y);
        half sdf = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, sdfUV).r;
        half lightAtten = 0.5h - 0.5h * (half)fwd; // 0 frontal .. 1 behind
        half faceX = sdf - lightAtten;
    #else
        // No SDF: one coherent value for the whole face (no nose/eye blotches).
        half faceX = (half)fwd;
    #endif

    return lerp(faceX, (half)dot(N, L), _FaceNormalBlend);
}

// ---------------------------------------------------------------------------------------------
// Additional light: hard-stepped N.L and hard-stepped "reach" (distance x shadow x intensity).
// Contribution is a MASK that promotes shadow -> lit, plus the strongest light's normalized hue.
// It never adds brightness, so it cannot create a 3rd band (documented compromise).
// ---------------------------------------------------------------------------------------------
void Toon_AccumulateAdditionalLight(Light light, float3 N, half offset, half aaW,
                                    inout half mask, inout half3 tint, inout half best)
{
    half band = Toon_StepW((half)dot(N, light.direction) + offset - _Threshold, aaW + _BandSoftness);
    half peak = max(light.color.r, max(light.color.g, light.color.b));
    half intensity = peak * light.distanceAttenuation * light.shadowAttenuation;
    half t = max(_AdditionalLightThreshold, 1.0e-3h);
    half reach = smoothstep(t * 0.95h, t * 1.05h, intensity);
    half m = band * reach;
    half score = m * intensity;
    half sel = step(best, score) * step(1.0e-4h, score);
    tint = lerp(tint, light.color / max(peak, 1.0e-4h), sel);
    best = max(best, score);
    mask = max(mask, m);
}

// Flat ambient: average of SH at up and down -> one colour per object, no gradient.
half3 Toon_FlatAmbient()
{
    return (SampleSH(half3(0.0h, 1.0h, 0.0h)) + SampleSH(half3(0.0h, -1.0h, 0.0h))) * 0.5h;
}

// shade.x = threshold offset (-1..1), shade.y = rim mask, shade.z = spec mask
half3 Toon_Shade(InputData inputData, half3 albedo, float2 uv, half3 shade)
{
    float3 N = inputData.normalWS;
    float3 V = inputData.viewDirectionWS;

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
    float3 L = mainLight.direction;

    // ---- 1. light term
    #if defined(_FACE_SHADING)
        half x = Toon_FaceTerm(N, L, uv);
    #else
        half x = (half)dot(N, L);
    #endif
    x += shade.x;

    // ---- 2. hard steps, combined with min BEFORE the select
    half bandNL = Toon_StepAA(x - _Threshold, _BandSoftness);
    half bandSh = Toon_StepAA(mainLight.shadowAttenuation - 0.5h, _BandSoftness);
    half lit = min(bandNL, bandSh) * step(0.5h, mainLight.distanceAttenuation);
    lit = lerp(lit, 1.0h, saturate(shade.z) * _ShadeMaskLit);   // opt-in lit mask (face); no-op while _ShadeMaskLit = 0

    // ---- 3. tones
    half peak = max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b));
    half3 lightTint = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color / max(peak, 1.0h), _MainLightColorInfluence);
    half3 litTone = albedo * _LitColor.rgb * lightTint;
    half3 shadowTone = lerp(litTone, Toon_ShadowAlbedo(albedo), _ShadowStrength);

    // ---- 4. additional lights (mask only)
    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    {
        half aaW = max(length(fwidth(N)), 1.0e-4) * 0.5h;
        half addMask = 0.0h;
        half3 addTint = half3(1.0h, 1.0h, 1.0h);
        half best = 0.0h;
        uint pixelLightCount = GetAdditionalLightsCount();

        #if USE_CLUSTER_LIGHT_LOOP
        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
            Toon_AccumulateAdditionalLight(light, N, shade.x, aaW, addMask, addTint, best);
        }
        #endif

        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
            Toon_AccumulateAdditionalLight(light, N, shade.x, aaW, addMask, addTint, best);
        LIGHT_LOOP_END

        litTone *= lerp(half3(1.0h, 1.0h, 1.0h), addTint, addMask * _AdditionalLightTint);
        lit = max(lit, addMask * _AdditionalLightStrength);
    }
    #endif

    // ---- 5. the single 2-tone select + flat ambient
    half3 color = lerp(shadowTone, litTone, lit);
    color *= lerp(half3(1.0h, 1.0h, 1.0h), Toon_FlatAmbient(), _IndirectStrength);

    // ---- 6. optional layers (hard-stepped, never replace the 2 tones)
    #if defined(_RIM_ON)
    {
        half rimRaw = 1.0h - saturate((half)dot(N, V));
        half rim = Toon_StepAA(rimRaw - (1.0h - _RimWidth), _BandSoftness);
        rim *= shade.y * lerp(1.0h, lit, _RimLitOnly);
        color += _RimColor.rgb * rim;
    }
    #endif

    #if defined(_SPECULAR_ON)
    {
        float3 H = SafeNormalize(L + V);
        half ndh = saturate((half)dot(N, H));
        half spec = Toon_StepAA(ndh - (1.0h - _SpecularSize), _BandSoftness);
        spec *= shade.z * lit;
        color += _SpecularColor.rgb * lightTint * spec;
    }
    #endif

    return color;
}

#endif // TOON2TONE_LIGHTING_INCLUDED
