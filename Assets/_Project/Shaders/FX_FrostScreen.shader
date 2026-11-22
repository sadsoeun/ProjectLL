// Frost-stage screen effect: blowing snow (3 parallax layers), wind streaks + gust haze, frozen screen border.
// One full-screen pass, procedural (no textures), one scene sample. Driven by the FrostScreenFx volume component.
Shader "Hidden/MyGame/FrostScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "FrostScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _FrostIntensity;
            float4 _SnowParams;   // x amount, y flake size, z speed
            float4 _SnowColor;
            float4 _WindParams;   // xy direction (normalized), z strength, w gust strength
            float  _StreakAmount;
            float4 _FrostParams;  // x width, y opacity, z distortion
            float4 _FrostTint;

            // ------------------------------------------------------------ noise
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash12(i);
                float b = Hash12(i + float2(1, 0));
                float c = Hash12(i + float2(0, 1));
                float d = Hash12(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    v += amp * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    amp *= 0.5;
                }
                return v / 0.875;
            }

            // Cellular noise: x = distance to the nearest point, y = F2 - F1 (thin bright lines = crystal edges).
            float2 Voronoi(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float f1 = 8.0, f2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y);
                    float2 r = o + Hash22(i + o) - f;
                    float dd = dot(r, r);
                    if (dd < f1) { f2 = f1; f1 = dd; }
                    else if (dd < f2) { f2 = dd; }
                }
                f1 = sqrt(f1);
                f2 = sqrt(f2);
                return float2(f1, f2 - f1);
            }

            // ------------------------------------------------------------ wind
            // Gust level g(t) in [0, 1] and its integral G(t), so displacement stays continuous while speed changes.
            float Gust(float t)         { return 0.5 + 0.35 * sin(0.9 * t) + 0.15 * sin(2.3 * t + 1.0); }
            float GustIntegral(float t) { return 0.5 * t - 0.35 * cos(0.9 * t) / 0.9 - 0.15 * cos(2.3 * t + 1.0) / 2.3; }

            // One flake per grid cell (if present). sp: aspect-corrected screen pos, disp: accumulated drift.
            float SnowLayer(float2 sp, float2 disp, float scale, float density, float radius, float stretch,
                            float softness, float2 wdir, float t, float seed)
            {
                float2 p = (sp - disp) * scale + seed * 7.31;
                p.x += sin(p.y * 0.35 + t * 1.7 + seed) * 0.3;     // gentle sway
                float2 cell = floor(p);
                float2 f = frac(p);
                float present = step(Hash12(cell + seed * 3.1), density);
                float2 h = Hash22(cell + seed * 13.7);
                float2 v = f - (0.22 + 0.56 * h);
                float2 perp = float2(-wdir.y, wdir.x);
                float along = dot(v, wdir) / (1.0 + stretch);   // stretched along the wind = motion streak
                float across = dot(v, perp);
                float d = length(float2(along, across));
                float r = radius * (0.6 + 0.8 * h.y);
                return present * smoothstep(r, r * softness, d);
            }

            // Fast thin lines travelling with the wind.
            float WindStreaks(float2 sp, float2 wdir, float t, float gust)
            {
                float2 perp = float2(-wdir.y, wdir.x);
                float a = dot(sp, wdir);
                float c = dot(sp, perp) * 30.0;
                float row = floor(c);
                float fr = frac(c);
                float hr = Hash12(float2(row, 3.7));
                float x = (a - t * (1.8 + 1.4 * hr) * (0.6 + gust)) * 1.3 + hr * 11.0;
                float seg = floor(x);
                float sf = frac(x);
                float on = step(0.84, Hash12(float2(row, seg)));
                float len = 0.45 + 0.45 * Hash12(float2(seg, row));
                float shape = smoothstep(0.0, len * 0.35, sf) * smoothstep(len, len * 0.45, sf);
                float thin = 1.0 - smoothstep(0.0, 0.06, abs(fr - 0.5 - (hr - 0.5) * 0.6));
                return on * shape * thin;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float I = _FrostIntensity;
                float t = _Time.y;   // scaled time: snow freezes with the perfect-dodge time stop
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 sp = uv * float2(aspect, 1.0);

                float2 wdir = _WindParams.xy;
                float windStrength = _WindParams.z;
                float gustStrength = _WindParams.w;
                float g = Gust(t);
                float gust = g * gustStrength;

                // --------------------------------------------- frozen border (computed first: refraction offset)
                float edgeX = min(uv.x, 1.0 - uv.x) * aspect;
                // The bottom edge (ground, pits) gets a thinner band than the top, so landing spots stay readable.
                float edgeY = min(uv.y * 1.8, 1.0 - uv.y);
                const float k = 0.05;
                float d = -k * log(exp(-edgeX / k) + exp(-edgeY / k));   // smooth min: deeper frost in corners
                float n = Fbm(sp * 6.0);
                float n2 = Fbm(sp * 21.0 + 5.0);
                float w = _FrostParams.x;
                float frost = 1.0 - smoothstep(w * 0.2, w, d + (n - 0.5) * w * 0.9 + (n2 - 0.5) * w * 0.3);
                frost *= I;

                float2 duv = (float2(n2, n) - 0.5) * 2.0 * _FrostParams.z * frost;
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + duv).rgb;

                // Cold cast that deepens toward the border.
                col *= lerp(float3(1, 1, 1), _FrostTint.rgb, saturate(frost * 1.6) * 0.35);

                // --------------------------------------------- wind: gust haze + streaks
                float drift = windStrength * _SnowParams.z * (t + gustStrength * 1.5 * GustIntegral(t));
                float2 windDisp = wdir * drift * 0.6;                 // screen heights
                float2 fall = float2(0.0, -0.22 * _SnowParams.z * t);

                float haze = Fbm(sp * 1.8 - windDisp * 0.9);
                haze = smoothstep(0.5, 0.95, haze);
                col = lerp(col, _SnowColor.rgb, haze * (0.02 + 0.1 * gust) * I);

                float streak = WindStreaks(sp, wdir, t, gust) * _StreakAmount * (0.35 + 0.65 * g);
                col = lerp(col, _SnowColor.rgb, saturate(streak * 0.8) * I);

                // --------------------------------------------- snow, far -> near
                float amount = _SnowParams.x;
                float size = _SnowParams.y;
                float stretchBase = windStrength * (0.5 + 1.6 * gust);
                float behindFrost = 1.0 - frost * 0.7;

                float far  = SnowLayer(sp, windDisp * 0.55 + fall * 0.6, 20.0, amount * 0.9,  0.10 * size, stretchBase * 0.6, 0.30, wdir, t, 1.0);
                float mid  = SnowLayer(sp, windDisp * 1.0  + fall * 1.0, 10.0, amount * 0.65, 0.085 * size, stretchBase * 1.0, 0.25, wdir, t, 2.0);
                float near = SnowLayer(sp, windDisp * 1.7  + fall * 1.6,  4.5, amount * 0.3,  0.07 * size, stretchBase * 1.5, 0.0,  wdir, t, 3.0);

                col = lerp(col, _SnowColor.rgb, far  * 0.5  * I * behindFrost);
                col = lerp(col, _SnowColor.rgb, mid  * 0.85 * I * behindFrost);
                col = lerp(col, _SnowColor.rgb, near * 0.55 * I * behindFrost);

                // --------------------------------------------- frost layer on top (on the "lens")
                [branch] if (frost > 0.002)
                {
                    // Ice crystals: large cells with fine sub-cells, sharp bright edges, only inside the band.
                    float2 cA = Voronoi(sp * 11.0 + n * 1.5);
                    float2 cB = Voronoi(sp * 30.0 + 3.7);
                    float crystal = (1.0 - smoothstep(0.0, 0.05, cA.y)) * 0.8 + (1.0 - smoothstep(0.0, 0.04, cB.y)) * 0.45;
                    float facet = cA.x * 0.35;   // shaded facets inside each crystal
                    float3 frostCol = _FrostTint.rgb * (0.78 + 0.25 * n2 + facet) + col * 0.25;
                    col = lerp(col, frostCol, saturate(frost * _FrostParams.y));
                    col += _FrostTint.rgb * crystal * smoothstep(0.25, 0.9, frost) * 0.32;
                }

                float2 sc = floor(sp * 240.0);
                float sparkle = step(0.992, Hash12(sc)) * (0.5 + 0.5 * sin(t * 3.0 + Hash12(sc + 9.1) * 6.28));
                col += sparkle * smoothstep(0.2, 0.8, frost) * 0.6;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
