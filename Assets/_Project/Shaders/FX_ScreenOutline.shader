// Subtle screen-space outline for background geometry (depth + normals edge detection), driven by the ScreenOutline
// volume component. One full-screen pass before transparents, so water, particles, the frost screen FX and UI are drawn
// on top of it and never outlined.
//  - Depth edges: second difference of 1/z (affine in screen space on any plane), so coplanar tile seams and grazing
//    ground never produce lines; one-sided (only the nearer pixel darkens) -> 1 px silhouettes.
//  - Normal edges: angle between neighbouring normals above a threshold; skipped where the neighbour is in front.
//  - Mask in _CameraNormalsTexture.a (ScreenOutlineMask.hlsl): characters are excluded, foliage only gets silhouettes.
//  - Colour: darkens the scene colour (multiply by a tint), fades out with distance.
Shader "Hidden/MyGame/ScreenOutline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ScreenOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _OUTLINE_DEBUG

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineParams;   // x intensity, y offset (px), z depth threshold, w normal threshold (1 - cos)
            float4 _OutlineParams2;  // x depth weight, y normal weight, z foliage normal scale, w softness
            float4 _OutlineFade;     // x fade start (m), y fade end (m), z near fade end (m), w max darkening
            float4 _OutlineColor;    // rgb multiply tint applied at full edge strength

            struct Sample
            {
                float q;        // depth metric: 1/z (perspective) or -z (orthographic); larger = nearer
                float eye;      // linear eye depth
                float3 n;       // world normal (zero where nothing wrote normals)
                float mask;     // ScreenOutlineMask
            };

            Sample Fetch(int2 px)
            {
                px = clamp(px, int2(0, 0), int2(_ScreenSize.xy) - 1);
                Sample s;
                float raw = LoadSceneDepth(px);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        raw = 1.0 - raw;
                    #endif
                    s.eye = lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                    s.q = -s.eye;
                }
                else
                {
                    s.eye = LinearEyeDepth(raw, _ZBufferParams);
                    s.q = 1.0 / max(s.eye, 1e-4);
                }
                float4 nt = LOAD_TEXTURE2D_X(_CameraNormalsTexture, px);
                s.n = nt.xyz;
                s.mask = nt.w;
                return s;
            }

            // Convexity of the centre against two opposite neighbours, relative to the centre depth (0 on planes).
            float DepthTerm(Sample c, Sample a, Sample b)
            {
                return (c.q - 0.5 * (a.q + b.q)) / abs(c.q);
            }

            // Crease between the centre and one neighbour; 0 when either has no normal or the neighbour is in front.
            float NormalTerm(Sample c, Sample o)
            {
                if (dot(c.n, c.n) < 0.25 || dot(o.n, o.n) < 0.25)
                    return 0.0;
                if (o.eye < c.eye * 0.97)
                    return 0.0;
                return 1.0 - dot(normalize(c.n), normalize(o.n));
            }

            float Ramp(float v, float threshold, float softness)
            {
                return saturate((v - threshold) / max(threshold * softness, 1e-5));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, _BlitMipLevel);

                int2 px = int2(uv * _ScreenSize.xy);
                int o = max(1, (int)round(_OutlineParams.y));

                Sample c = Fetch(px);
                // Sky / empty background: nothing to outline on this pixel (the line belongs to the object side).
                if (c.eye >= _ProjectionParams.z * 0.98)
                    return scene;

                Sample l = Fetch(px + int2(-o, 0));
                Sample r = Fetch(px + int2( o, 0));
                Sample d = Fetch(px + int2(0, -o));
                Sample u = Fetch(px + int2(0,  o));

                // Characters (and their immediate surroundings) keep only their own hull outline.
                float excluded = max(max(c.mask, max(l.mask, r.mask)), max(u.mask, d.mask));
                if (excluded > 0.75)
                    return scene;

                float softness = _OutlineParams2.w;
                float depthEdge = max(DepthTerm(c, l, r), DepthTerm(c, d, u));
                depthEdge = Ramp(depthEdge, _OutlineParams.z, softness) * _OutlineParams2.x;

                float nEdge = max(NormalTerm(c, r), NormalTerm(c, u));
                float foliage = (c.mask > 0.25 || r.mask > 0.25 || u.mask > 0.25) ? _OutlineParams2.z : 1.0;
                nEdge = Ramp(nEdge, _OutlineParams.w, softness) * _OutlineParams2.y * foliage;

                float edge = saturate(max(depthEdge, nEdge));

                // Distance fade (far background stays clean) and a short near fade (objects brushing the lens).
                float fade = 1.0 - smoothstep(_OutlineFade.x, _OutlineFade.y, c.eye);
                fade *= smoothstep(0.0, max(_OutlineFade.z, 1e-3), c.eye);
                edge *= fade * _OutlineParams.x;

                #if defined(_OUTLINE_DEBUG)
                    return half4(edge.xxx, 1.0);
                #endif

                half3 darkened = scene.rgb * _OutlineColor.rgb;
                scene.rgb = lerp(scene.rgb, darkened, (half)min(edge, _OutlineFade.w));
                return scene;
            }
            ENDHLSL
        }
    }
}
