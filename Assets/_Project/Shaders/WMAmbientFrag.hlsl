#ifndef WM_AMBIENT_FRAG_INCLUDED
#define WM_AMBIENT_FRAG_INCLUDED

// Fragment programs for the ambient shaders; include after WMAmbientPasses.hlsl and (forward pass) after defining WMSurface.

#if defined(WM_PASS_FORWARD)
#if defined(WM_TOON)
// Cel lighting: main light (shadowed, half-Lambert) -> stepped ramp -> shadow-colour tint; additional lights add stepped light.
half3 WMToonLighting(InputData inputData, half3 albedo, half3 emission, half alpha)
{
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.alpha = alpha;
    surface.occlusion = 1.0;

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surface);
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    half3 N = inputData.normalWS;
    half steps = clamp(round(_ShadeSteps), 2.0h, 4.0h);

    half x = saturate(dot(N, mainLight.direction) * 0.5h + 0.5h) * mainLight.shadowAttenuation;
    half ramp = WMToonRamp(x, steps, _ShadeSmoothness);
    // shadow side: flat tint of the albedo (_ShadowFromAlbedo 0) or an HSV-shifted albedo (1), then the shadow tint
    half3 shadowAlbedo = lerp(albedo, WMShadowAlbedo(albedo, _ShadowHueShift, _ShadowSaturation, _ShadowValue), _ShadowFromAlbedo) * _ShadowColor.rgb;
    half3 toned = lerp(shadowAlbedo, albedo, ramp);
    half3 ambient = inputData.bakedGI * aoFactor.indirectAmbientOcclusion;
    half3 radiance = ambient + mainLight.color * (mainLight.distanceAttenuation * ramp * aoFactor.directAmbientOcclusion);

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        radiance += light.color * (light.distanceAttenuation * WMToonRamp(saturate(dot(N, light.direction)) * light.shadowAttenuation, steps, _ShadeSmoothness));
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        radiance += light.color * (light.distanceAttenuation * WMToonRamp(saturate(dot(N, light.direction)) * light.shadowAttenuation, steps, _ShadeSmoothness));
    LIGHT_LOOP_END
    #endif

    // soft rim, strongest on the lit side
    half fres = 1.0h - saturate(dot(N, inputData.viewDirectionWS));
    half rim = pow(max(fres, 1e-4h), max(_RimPower, 0.01h)) * lerp(0.35h, 1.0h, ramp);
    half3 rimColor = _RimColor.rgb * rim * mainLight.color;

    return toned * radiance + rimColor + emission;
}
#endif

half4 WMFrag(WMVaryings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
    half3 albedo; half alpha; half3 emission;
    WMSurface(input, albedo, alpha, emission, normalWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.bakedGI = SampleSH(normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    half4 color;
    #if defined(WM_TOON)
    if (_ToonMode > 0.5h)
    {
        color = half4(WMToonLighting(inputData, albedo, emission, alpha), alpha);
    }
    else
    #endif
    {
        SurfaceData surface = (SurfaceData)0;
        surface.albedo = albedo;
        surface.alpha = alpha;
        surface.emission = emission;
        surface.occlusion = 1.0;
        surface.specular = half3(0, 0, 0);
        surface.smoothness = 0.0;
        color = UniversalFragmentBlinnPhong(inputData, surface);
    }
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = alpha;
    return color;
}
#elif defined(WM_PASS_DEPTHNORMALS)
#include "ScreenOutlineMask.hlsl"
// Alpha carries the screen-outline mask (see ScreenOutlineMask.hlsl); xyz stays the URP normal encoding.
half4 WMFrag(WMVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 oct = PackNormalOctQuadEncode(normalize(input.normalWS));
        return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), ScreenOutlineMask());
    #else
        return half4(NormalizeNormalPerPixel(input.normalWS), ScreenOutlineMask());
    #endif
}
#else
half4 WMFrag(WMVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    return input.positionCS.z;
}
#endif

#endif
