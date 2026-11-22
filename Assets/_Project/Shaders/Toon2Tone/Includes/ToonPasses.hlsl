#ifndef TOON2TONE_PASSES_INCLUDED
#define TOON2TONE_PASSES_INCLUDED

// Pass programs for "MyGame/Toon/2Tone". Select with one of:
//   TOON_PASS_FORWARD, TOON_PASS_OUTLINE, TOON_PASS_SHADOWCASTER, TOON_PASS_DEPTHONLY, TOON_PASS_DEPTHNORMALS
// ToonInput.hlsl (CBUFFER) is already included by the shader's HLSLINCLUDE block.

// =============================================================================================
#if defined(TOON_PASS_FORWARD)
// =============================================================================================
#include "ToonLighting.hlsl"

struct ToonVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3  normalWS   : TEXCOORD2;
    half   fogFactor  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonVaryings ToonForwardVertex(ToonAttributes input)
{
    ToonVaryings output = (ToonVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = pos.positionCS;
    output.positionWS = pos.positionWS;
    output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.fogFactor = (half)ComputeFogFactor(pos.positionCS.z);
    return output;
}

half4 ToonForwardFragment(ToonVaryings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 baseColor = ToonSampleBase(input.uv);

    float3 normalWS = normalize((float3)input.normalWS);
    normalWS = IS_FRONT_VFACE(facing, normalWS, -normalWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    inputData.shadowMask = half4(1, 1, 1, 1);

    // ShadeMap (Linear): R = threshold offset (0.5 neutral), G = rim mask, B = spec mask
    #if defined(_SHADEMAP)
        half4 sm = SAMPLE_TEXTURE2D(_ShadeMap, sampler_ShadeMap, input.uv);
        half3 shade = half3((sm.r - 0.5h) * 2.0h * _ShadeMapStrength, sm.g, sm.b);
    #else
        half3 shade = half3(0.0h, 1.0h, 1.0h);
    #endif

    half3 color = Toon_Shade(inputData, baseColor.rgb, input.uv, shade);

    #if defined(_EMISSION)
        color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
    #endif

    color = MixFog(color, inputData.fogCoord);

    #if defined(_SURFACE_TYPE_TRANSPARENT)
        return half4(color, baseColor.a);
    #else
        return half4(color, 1.0h);
    #endif
}

// =============================================================================================
#elif defined(TOON_PASS_OUTLINE)
// =============================================================================================
// Inverted hull. Width is in "pixels at 1080p" and constant on screen up to _OutlineDistanceClamp
// (view depth, metres); beyond that it shrinks with distance (constant world thickness).

struct ToonOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half   fogFactor  : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonOutlineVaryings ToonOutlineVertex(ToonAttributes input)
{
    ToonOutlineVaryings output = (ToonOutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(positionWS);

    float2 nCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
    float2 dirPx = nCS * _ScreenParams.xy;                    // direction in pixel space
    float lenPx = length(dirPx);
    float2 dir = (lenPx > 1.0e-6) ? dirPx / lenPx : float2(0.0, 0.0);
    float2 ndcPerPx = 2.0 / max(_ScreenParams.xy, float2(1.0, 1.0));
    float px = max(_OutlineWidth, 0.0) * (_ScreenParams.y / 1080.0);
    // opt-in: thinner outline where the ShadeMap blue (lit) mask is set, e.g. face skin
    if (_OutlineVertexMask > 0.0h)
        px *= lerp(1.0, saturate(input.color.r), _OutlineVertexMask);   // painted-linework regions (eyes / mouth) carry 0
    if (_ShadeMaskLit > 0.0h || _OutlineMaskG > 0.0h)
    {
        half4 outlineMask = SAMPLE_TEXTURE2D_LOD(_ShadeMap, sampler_ShadeMap, TRANSFORM_TEX(input.texcoord, _BaseMap), 0);
        px *= lerp(1.0, 0.3, saturate(outlineMask.b * _ShadeMaskLit));
        px *= 1.0 - saturate(outlineMask.g * _OutlineMaskG);   // eye / mouth region: the face linework is painted, no hull line
    }
    float depthScale = min(positionCS.w, max(_OutlineDistanceClamp, 0.01));
    positionCS.xy += dir * ndcPerPx * px * depthScale;
    // zero-width hull vertices (painted-linework regions, vertex colour R = 0) would sit exactly on the surface and z-fight with it, leaving pale eye-shaped patches /
    // dotted lines from the albedo-mixed hull colour: push them to the far plane so the hull can never pass the depth test there.
    if (_OutlineVertexMask > 0.0h && input.color.r < 0.02h)
        positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * positionCS.w;

    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.fogFactor = (half)ComputeFogFactor(positionCS.z);
    return output;
}

half4 ToonOutlineFragment(ToonOutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 baseColor = ToonSampleBase(input.uv);
    half3 color = lerp(_OutlineColor.rgb, baseColor.rgb * _OutlineColor.rgb, _OutlineAlbedoMix);
    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0h);
}

// =============================================================================================
#elif defined(TOON_PASS_SHADOWCASTER)
// =============================================================================================
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct ToonShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

ToonShadowVaryings ToonShadowVertex(ToonAttributes input)
{
    ToonShadowVaryings output = (ToonShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

    // URP bias (pipeline asset / light settings) first, then per-material extra bias in metres.
    positionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
    float invNdotL = 1.0 - saturate(dot(lightDirectionWS, normalWS));
    positionWS -= lightDirectionWS * _ShadowDepthBias;          // push away from the light
    positionWS -= normalWS * (_ShadowNormalBias * invNdotL);     // inset along the normal

    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(positionWS));
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    return output;
}

half4 ToonShadowFragment(ToonShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ToonSampleAlphaClipOnly(input.uv);
    return 0;
}

// =============================================================================================
#elif defined(TOON_PASS_DEPTHONLY)
// =============================================================================================
struct ToonDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonDepthVaryings ToonDepthVertex(ToonAttributes input)
{
    ToonDepthVaryings output = (ToonDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    return output;
}

half ToonDepthFragment(ToonDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ToonSampleAlphaClipOnly(input.uv);
    return input.positionCS.z;
}

// =============================================================================================
#elif defined(TOON_PASS_DEPTHNORMALS)
// =============================================================================================
struct ToonDepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half3  normalWS   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonDepthNormalsVaryings ToonDepthNormalsVertex(ToonAttributes input)
{
    ToonDepthNormalsVaryings output = (ToonDepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
    return output;
}

#include "../../ScreenOutlineMask.hlsl"
// Alpha carries the screen-outline mask (see Shaders/ScreenOutlineMask.hlsl).
void ToonDepthNormalsFragment(ToonDepthNormalsVaryings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ToonSampleAlphaClipOnly(input.uv);

    #if defined(_GBUFFER_NORMALS_OCT)
        float3 n = normalize((float3)input.normalWS);
        float2 oct = PackNormalOctQuadEncode(n);
        outNormalWS = half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), ScreenOutlineMask());
    #else
        outNormalWS = half4(NormalizeNormalPerPixel((float3)input.normalWS), ScreenOutlineMask());
    #endif

    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif

#endif // TOON2TONE_PASSES_INCLUDED
