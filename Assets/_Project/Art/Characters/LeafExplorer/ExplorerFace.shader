// Ink-only eye / mouth overlay for the Leaf Explorer face decal patches (LeafExplorer_Face mesh).
// The skin under the eyes / mouth is NOT painted here: LeafExplorerFace swaps the body albedo for a copy with the eyes / mouth erased
// (same texture, same Toon2Tone lighting), so only lines and the mouth interior are drawn by this shader.
// Patch id comes from UV1.x (0 eye L, 1 eye R, 2 mouth).
// _Eye: 0 open (nothing drawn), 1 closed, 2 happy ^ ^, 3 hurt > <, 4 sad. _Blink: 0 open, ~0.5 half-closed (drawn lid + sclera + iris), 1 closed arc. _Wink: right eye happy arc only.
// _Mouth: 0 painted smile (nothing drawn), 1 small o, 2 open smile with tongue, 3 grimace, 4 flat, 5 cat w.
// Lighting uses the same Toon_Shade() as MyGame/Toon/2Tone.
Shader "LeafAndLeap/ExplorerFace"
{
    Properties
    {
        _LineColor("Lash / line colour", Color) = (0.20, 0.09, 0.08, 1)
        _MouthColor("Mouth inner colour", Color) = (0.42, 0.10, 0.13, 1)
        _TongueColor("Tongue colour", Color) = (0.93, 0.42, 0.46, 1)
        _SkinColor("Skin colour (unused, kept for old materials)", Color) = (0.99, 0.92, 0.88, 1)
        _Eye("Eye expression", Float) = 0
        _Mouth("Mouth expression", Float) = 0
        _Blink("Blink 0..1", Range(0, 1)) = 0
        _Wink("Wink (right eye happy arc)", Range(0, 1)) = 0
        _ClosedLineWidth("Closed / wink / smile eye line width (patch units)", Range(0.02, 0.2)) = 0.10

        // ---- Toon2Tone-compatible values (same names / layout as MyGame/Toon/2Tone)
        [MainTexture] _BaseMap("Base Map (unused)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _LitColor("Lit Color", Color) = (1, 1, 1, 1)
        _ShadowColor("Shadow Color (tint)", Color) = (1, 0.93, 0.93, 1)
        _Threshold("Threshold (N.L)", Range(-1, 1)) = 0
        _BandSoftness("Band Softness", Range(0, 0.3)) = 0.01
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 1
        _ShadowHueShift("Shadow Hue Shift", Range(-0.25, 0.25)) = -0.03
        _ShadowSaturation("Shadow Saturation", Range(0, 2)) = 1.2
        _ShadowValue("Shadow Value", Range(0, 1)) = 0.78
        _MainLightColorInfluence("Main Light Color Influence", Range(0, 1)) = 0.5
        _IndirectStrength("Indirect (Ambient) Strength", Range(0, 1)) = 0.2
        _ShadowDepthBias("Extra Shadow Depth Bias", Range(0, 0.2)) = 0
        _ShadowNormalBias("Extra Shadow Normal Bias", Range(0, 0.2)) = 0
        _ShadeMapStrength("Shade Map Offset Strength", Range(0, 1)) = 1
        _ShadeMaskLit("Shade Map B: Lit Amount", Range(0, 1)) = 0
        _FaceForward("Face Forward", Vector) = (0, 0, 1, 0)
        _FaceUp("Face Up", Vector) = (0, 1, 0, 0)
        _FaceVectorSpace("Face Vector Space", Float) = 0
        _FaceNormalBlend("Face N.L Blend", Range(0, 1)) = 0
        _FaceSDFFlip("Face SDF Flip", Float) = 0
        _RimColor("Rim Color", Color) = (0.5, 0.5, 0.5, 1)
        _RimWidth("Rim Width", Range(0, 1)) = 0.3
        _RimLitOnly("Rim Lit Side Only", Range(0, 1)) = 1
        _SpecularColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _SpecularSize("Specular Size", Range(0, 0.5)) = 0.05
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _OutlineColor("Outline Color", Color) = (0.4, 0.4, 0.4, 1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 1.5
        _OutlineAlbedoMix("Outline Albedo Mix", Range(0, 1)) = 1
        _OutlineDistanceClamp("Outline Constant-Width Distance", Range(0.5, 50)) = 8
        _AdditionalLightStrength("Additional Light Strength", Range(0, 1)) = 1
        _AdditionalLightTint("Additional Light Tint", Range(0, 1)) = 0.5
        _AdditionalLightThreshold("Additional Light Threshold", Range(0.01, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Name "ExplorerFace"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1      // the patches hug the skin: without a bias the line edges z-fight into ragged steps
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Assets/_Project/Shaders/Toon2Tone/Includes/ToonInput.hlsl"
            #include "Assets/_Project/Shaders/Toon2Tone/Includes/ToonLighting.hlsl"
            half4 _SkinColor; half4 _LineColor; half4 _MouthColor; half4 _TongueColor;
            float _Eye; float _Mouth; float _Blink; float _Wink; float _ClosedLineWidth;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 patch:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float patchId:TEXCOORD3; half fogFactor:TEXCOORD4; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS; o.positionWS = pos.positionWS;
                o.normalWS = (half3)TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv; o.patchId = v.patch.x;
                o.fogFactor = (half)ComputeFogFactor(pos.positionCS.z);
                return o;
            }
            float Stroke(float v, float curve, float t) { return 1.0 - smoothstep(t * 0.72, t, abs(v - curve)); }
            // eye ink: u = 0 inner corner .. 1 outer corner, v up
            float EyeInk(float2 uv, float kind, out float lens, out float iris, out float pupil)
            {
                lens = 0; iris = 0; pupil = 0;
                float u = uv.x, v = uv.y;
                float x = (u - 0.5) * 2.0;
                float inRange = step(0.08, u) * step(u, 0.96);
                float ink = 0;
                float lw = _ClosedLineWidth;   // thick painted-lash stroke shared by closed / smile / wink eyes (tapered toward the inner corner)
                if (kind < 1.5)            // closed: ONE lid line (gentle downward arc, thicker toward the outer corner) + two lash flicks at the outer end
                {
                    float curve = 0.50 + 0.15 * x * x;
                    float w = lw * lerp(0.60, 1.0, smoothstep(0.10, 0.85, u));
                    float taper = smoothstep(0.07, 0.22, u) * (1.0 - smoothstep(0.90, 0.97, u));
                    ink = Stroke(v, curve, max(w * taper, 0.004)) * inRange * smoothstep(0.0, 0.2, taper);   // the ends thin out to a point instead of fading (a faded end reads as a grey smudge)
                    float fu = step(0.76, u) * (1.0 - smoothstep(0.93, 0.99, u));
                    ink += fu * Stroke(v, curve - (u - 0.76) * 0.70, lw * 0.62);                                      // upper flick: leaves the line outward and down
                    ink += step(0.80, u) * (1.0 - smoothstep(0.90, 0.96, u)) * Stroke(v, curve - 0.04 - (u - 0.80) * 1.35, lw * 0.52);   // second, shorter flick
                }
                else if (kind < 2.5)       // happy ^ ^ (also the wink)
                {
                    float curve = 0.58 - 0.27 * x * x;
                    float w = lw * 1.05 * lerp(0.60, 1.0, smoothstep(0.10, 0.85, u));
                    float taper = smoothstep(0.07, 0.22, u) * (1.0 - smoothstep(0.90, 0.97, u));
                    ink = Stroke(v, curve, max(w * taper, 0.004)) * inRange * smoothstep(0.0, 0.2, taper);   // the ends thin out to a point instead of fading (a faded end reads as a grey smudge)
                    ink += step(0.80, u) * (1.0 - smoothstep(0.92, 0.98, u)) * Stroke(v, curve - (u - 0.80) * 0.9, lw * 0.62);
                    ink += step(0.84, u) * (1.0 - smoothstep(0.92, 0.98, u)) * Stroke(v, curve - 0.05 - (u - 0.84) * 1.5, lw * 0.50);
                }
                else if (kind < 3.5)       // hurt > <
                {
                    float s1 = Stroke(v, lerp(0.52, 0.78, saturate((u - 0.22) / 0.7)), 0.06);
                    float s2 = Stroke(v, lerp(0.52, 0.28, saturate((u - 0.22) / 0.7)), 0.06);
                    ink = max(s1, s2) * step(0.2, u) * step(u, 0.95);
                }
                else                       // sad: drooping closed line
                {
                    float curve = 0.42 + 0.11 * x * x - 0.16 * (u - 0.5);
                    ink = Stroke(v, curve, 0.05) * inRange;
                }
                return saturate(ink);
            }
            // mouth: (outline, interior, tongue)
            float3 MouthShape(float2 uv, float kind)
            {
                float x = (uv.x - 0.5) * 2.0, v = uv.y;
                float outline = 0, inner = 0, tongue = 0;
                if (kind < 1.5)            // small o
                {
                    float2 d = float2(x * 0.78, (v - 0.46) * 1.55);
                    float r = length(d);
                    outline = 1 - smoothstep(0.17, 0.20, r);
                    inner = 1 - smoothstep(0.115, 0.145, r);
                }
                else if (kind < 2.5)       // open smile: flat-ish top lip, round bottom, thin dark outline, tongue hint
                {
                    float ax = x / 0.38;                                  // -1..1 across the mouth
                    float inside = step(abs(ax), 1.0);
                    float top = 0.60 + 0.04 * ax * ax;                    // corners lift a little
                    float depth = 0.27 * sqrt(saturate(1.0 - ax * ax));
                    float bottom = top - depth;
                    float aa = 0.018;
                    float body = inside * smoothstep(bottom - aa, bottom + aa, v) * (1.0 - smoothstep(top - aa, top + aa, v));
                    float edge = body * (1.0 - smoothstep(0.0, 0.085, min(v - bottom, top - v)));
                    float sides = body * step(0.88, abs(ax));
                    outline = saturate(max(edge, sides));
                    float2 td = float2(ax * 0.95, (v - (bottom + depth * 0.22)) / max(0.02, depth * 0.28));
                    tongue = (1.0 - smoothstep(0.80, 1.0, length(td))) * body * (1.0 - outline);
                    inner = body * (1.0 - outline) * (1.0 - tongue);
                }
                else if (kind < 3.5)       // grimace: wavy line
                {
                    float curve = 0.46 + 0.07 * sin(x * 9.0);
                    outline = Stroke(v, curve, 0.05) * step(abs(x), 0.6);
                }
                else if (kind < 4.5)       // flat
                {
                    outline = Stroke(v, 0.46, 0.04) * step(abs(x), 0.4);
                }
                else                       // cat w
                {
                    float c = 0.50 - 0.14 * abs(sin(x * 3.1));
                    outline = Stroke(v, c, 0.045) * step(abs(x), 0.66);
                }
                return saturate(float3(outline, inner, tongue));
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv; int pid = (int)(i.patchId + 0.5);
                float edgeDist = min(min(uv.x, 1 - uv.x), min(uv.y, 1 - uv.y));
                float inkFade = smoothstep(0.0, 0.08, edgeDist);
                half3 albedo = _LineColor.rgb; float alpha = 0;
                if (pid < 2)
                {
                    float2 e = uv;
                    if (pid == 1) e.x = 1 - e.x;
                    float kind = round(_Eye);
                    if (_Wink > 0.5) { if (pid == 0) discard; kind = 2; }
                    else if (kind < 0.5) { if (_Blink < 0.5) discard; kind = 1; }
                    float lens, iris, pupil;
                    float ink = EyeInk(e, kind, lens, iris, pupil);
                    half3 body = lerp(half3(0.96h, 0.92h, 0.91h), half3(0.16h, 0.48h, 0.57h), iris);
                    body = lerp(body, half3(0.07h, 0.09h, 0.14h), pupil);
                    albedo = lerp(_LineColor.rgb, body, saturate(lens * (1.0 - ink)));
                    alpha = saturate(max(ink, lens)) * inkFade;
                }
                else
                {
                    float kind = round(_Mouth);
                    if (kind < 0.5) discard;
                    float3 m = MouthShape(uv, kind);
                    albedo = _LineColor.rgb;
                    albedo = lerp(albedo, _MouthColor.rgb, saturate(m.y));
                    albedo = lerp(albedo, _TongueColor.rgb, saturate(m.z));
                    alpha = saturate(max(m.x, max(m.y, m.z))) * inkFade;
                }
                if (alpha < 0.01) discard;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = normalize((float3)i.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                inputData.shadowMask = half4(1, 1, 1, 1);
                half3 color = Toon_Shade(inputData, albedo, uv, half3(0.0h, 1.0h, 1.0h));
                color = MixFog(color, inputData.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
