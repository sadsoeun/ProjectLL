#ifndef WM_AMBIENT_PASSES_INCLUDED
#define WM_AMBIENT_PASSES_INCLUDED

// Generic pass bodies for the ambient shaders. Before including, a shader defines its UnityPerMaterial CBUFFER and:
//   void WMDisplace(inout float3 positionWS, inout float3 normalWS, float3 positionOS, float4 color, float2 uv)
//   void WMSurface(WMVaryings input, out half3 albedo, out half alpha, out half3 emission, inout half3 normalWS)   (forward pass only, after this include)
// then includes WMAmbientFrag.hlsl. The pass is selected with WM_PASS_FORWARD / WM_PASS_SHADOW / WM_PASS_DEPTH / WM_PASS_DEPTHNORMALS.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(WM_PASS_FORWARD)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif
#if defined(WM_PASS_SHADOW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
    float3 _LightDirection;
    float3 _LightPosition;
#endif

struct WMAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 color      : COLOR;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WMVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3  normalWS   : TEXCOORD1;
    float2 uv         : TEXCOORD2;
    float4 color      : TEXCOORD3;
    half   fogFactor  : TEXCOORD4;
    float3 pivotWS    : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

WMVaryings WMVert(WMAttributes input)
{
    WMVaryings output = (WMVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    WMDisplace(positionWS, normalWS, input.positionOS.xyz, input.color, input.uv);

    output.positionWS = positionWS;
    output.normalWS = normalWS;
    output.uv = input.uv;
    output.color = input.color;
    output.pivotWS = TransformObjectToWorld(float3(0, 0, 0));

#if defined(WM_PASS_SHADOW)
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
#else
    output.positionCS = TransformWorldToHClip(positionWS);
#endif
#if defined(WM_PASS_FORWARD)
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
#endif
    return output;
}

#endif
