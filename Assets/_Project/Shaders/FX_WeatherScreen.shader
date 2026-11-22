// Themed stage screen effect (meadow / desert / coast / lava fortress): wind-blown particles (3 parallax layers),
// wind streaks + gust haze, a themed screen border and optional heat shimmer.
// One full-screen pass, procedural (no textures), one scene sample. Driven by the WeatherScreenFx volume component.
// Border styles (_WxStyle): 0 light leak, 1 dust, 2 droplets, 3 scorch.
Shader "Hidden/MyGame/WeatherScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "WeatherScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _WxIntensity;
            float  _WxStyle;
            float4 _PartParams;    // x amount, y size, z speed, w glow (0 opaque .. 1 additive)
            float4 _PartColor;
            float4 _PartColorB;
            float4 _PartMotion;    // xy own drift (screen heights / s), z flutter, w stretch
            float4 _WindParams;    // xy direction (normalized), z strength, w gust strength
            float4 _HazeColor;     // rgb, a = amount
            float  _StreakAmount;
            float4 _BorderParams;  // x width, y opacity, z distortion, w detail
            float4 _BorderTint;
            float4 _BorderGlow;
            float4 _HeatParams;    // x amplitude (uv), y scale, z speed, w splash amount (droplets style)

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

            // Cellular noise: x = distance to the nearest point, y = F2 - F1 (thin lines = cracks).
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

            // Distance to the screen border in screen heights; the bottom edge (ground, pits) counts as further away
            // so its band is thinner and landing spots stay readable. Smooth min: deeper band in the corners.
            float EdgeDistance(float2 uv, float aspect)
            {
                float edgeX = min(uv.x, 1.0 - uv.x) * aspect;
                float edgeY = min(uv.y * 1.8, 1.0 - uv.y);
                const float k = 0.05;
                return -k * log(exp(-edgeX / k) + exp(-edgeY / k));
            }

            // One particle per grid cell (if present). sp: aspect-corrected screen pos, disp: accumulated movement,
            // mdir: movement direction (stretch axis). Returns x = coverage, y = per-particle random.
            float2 ParticleLayer(float2 sp, float2 disp, float scale, float density, float radius, float stretch,
                                 float softness, float2 mdir, float flutter, float t, float seed)
            {
                float2 p = (sp - disp) * scale + seed * 7.31;
                p.x += sin(p.y * 0.35 + t * 1.7 + seed) * (0.15 + 0.45 * flutter);   // sway
                float2 cell = floor(p);
                float2 f = frac(p);
                float present = step(Hash12(cell + seed * 3.1), density);
                float2 h = Hash22(cell + seed * 13.7);
                float2 v = f - (0.25 + 0.5 * h);
                float2 perp = float2(-mdir.y, mdir.x);
                float along = dot(v, mdir) / (1.0 + stretch + 0.9 * flutter);   // petals / leaves are longer than wide
                // Flutter: the particle turns edge-on now and then (a tumbling petal).
                float flip = lerp(1.0, 0.3 + 0.7 * abs(sin(t * (2.0 + 2.0 * h.x) + h.y * 6.28)), flutter);
                float across = dot(v, perp) / flip;
                float d = length(float2(along, across));
                float r = radius * (0.6 + 0.8 * h.y);
                return float2(present * smoothstep(r, r * lerp(softness, 0.8, flutter), d), h.x);   // tumbling shapes stay crisp
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

            // Small water splashes jumping up from the bottom edge now and then (3 emitters x 4 drops).
            float SplashDrops(float2 sp, float aspect, float t)
            {
                float acc = 0.0;
                [unroll] for (int e = 0; e < 3; e++)
                {
                    float period = 2.6 + 0.9 * e;
                    float ph = t / period + e * 0.37;
                    float cyc = floor(ph);
                    float tau = frac(ph) * period;   // seconds since this burst
                    float2 origin = float2(Hash12(float2(cyc, e + 1.7)) * aspect, -0.02);
                    [unroll] for (int k = 0; k < 4; k++)
                    {
                        float2 h = Hash22(float2(cyc * 7.0 + k, e * 3.1 + 0.5));
                        float2 v0 = float2((h.x - 0.5) * 0.35, 0.45 + 0.3 * h.y);
                        float2 pos = origin + v0 * tau - float2(0.0, 0.9) * tau * tau;
                        float r = 0.006 + 0.006 * h.y;
                        acc += smoothstep(r, r * 0.4, length(sp - pos));
                    }
                }
                return saturate(acc);
            }

            float3 BlendParticle(float3 col, float2 layer, float opacity, float t)
            {
                float3 pc = lerp(_PartColor.rgb, _PartColorB.rgb, layer.y);
                float glow = _PartParams.w;
                float flicker = 0.65 + 0.35 * sin(t * 7.0 + layer.y * 40.0);
                float a = layer.x * opacity;
                float3 opaque = lerp(col, pc, a);
                float3 additive = col + pc * a * flicker * 1.6;
                return lerp(opaque, additive, glow);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float I = _WxIntensity;
                float t = _Time.y;   // scaled time: the weather freezes with the perfect-dodge time stop
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 sp = uv * float2(aspect, 1.0);
                float style = _WxStyle;

                float2 wdir = _WindParams.xy;
                float windStrength = _WindParams.z;
                float gustStrength = _WindParams.w;
                float g = Gust(t);
                float gust = g * gustStrength;
                float speed = _PartParams.z;
                float drift = windStrength * speed * (t + gustStrength * 1.5 * GustIntegral(t));
                float2 windDisp = wdir * drift * 0.6;   // screen heights

                // --------------------------------------------- border band (computed first: refraction offset)
                float w = _BorderParams.x;
                float detail = _BorderParams.w;
                float d = EdgeDistance(uv, aspect);
                // Dust creeps with the wind, the other borders sit still on the "lens".
                float2 bandScroll = style > 0.5 && style < 1.5 ? windDisp * 0.12 : float2(0.0, 0.0);
                float n = Fbm(sp * 6.0 - bandScroll);
                float n2 = Fbm(sp * 21.0 + 5.0 - bandScroll * 2.0);
                float band = 1.0 - smoothstep(w * 0.2, w, d + (n - 0.5) * w * 0.9 + (n2 - 0.5) * w * 0.3);
                band *= I;

                float2 duv = (float2(n2, n) - 0.5) * 2.0 * _BorderParams.z * band;

                // Water drops on the lens (style 2): a grid of drops, denser toward the edge, slowly running down.
                float dropMask = 0.0, dropHi = 0.0;
                float2 dropN = float2(0.0, 0.0);
                [branch] if (style > 1.5 && style < 2.5 && d < w * 1.6)
                {
                    float2 p = sp * 13.0;
                    p.y += t * 0.03 * (0.3 + Hash12(float2(floor(p.x), 1.3)));
                    float2 cell = floor(p);
                    float2 f = frac(p);
                    float2 h = Hash22(cell + 4.7);
                    // Presence from the cell centre so a drop is never cut by the band outline.
                    float2 cuv = (cell + 0.5) / 13.0 / float2(aspect, 1.0);
                    float cellBand = 1.0 - smoothstep(w * 0.15, w * 1.35, EdgeDistance(cuv, aspect));
                    float present = step(Hash12(cell + 9.2), cellBand * detail);
                    float2 v = f - (0.3 + 0.4 * h);
                    v.y *= 0.8;
                    float r = 0.12 + 0.16 * h.x;
                    dropMask = present * smoothstep(r, r * 0.75, length(v)) * I;
                    dropN = v / r;
                    dropHi = present * smoothstep(r * 0.4, r * 0.1, length(v - float2(-0.3, 0.38) * r)) * I;
                    duv -= dropN * dropMask * _BorderParams.z;   // a drop shows the scene flipped, like a lens
                }

                // Heat shimmer: rising waves, weaker toward the top of the screen.
                [branch] if (_HeatParams.x > 0.0)
                {
                    float hs = _HeatParams.y;
                    float rise = t * _HeatParams.z;
                    float hx = ValueNoise(float2(sp.x * hs, sp.y * hs * 0.6 - rise * 2.0));
                    float hy = ValueNoise(float2(sp.x * hs * 1.7 + 3.1, sp.y * hs - rise * 2.6));
                    duv += (float2(hx, hy) - 0.5) * 2.0 * _HeatParams.x * I * (1.0 - 0.5 * uv.y);
                }

                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + duv).rgb;

                // --------------------------------------------- wind: gust haze + streaks
                float haze = Fbm(sp * 1.8 - windDisp * 0.9);
                haze = smoothstep(0.5, 0.95, haze);
                col = lerp(col, _HazeColor.rgb, haze * _HazeColor.a * (0.2 + 0.8 * gust) * I);

                float streak = WindStreaks(sp, wdir, t, gust) * _StreakAmount * (0.35 + 0.65 * g);
                col = lerp(col, _HazeColor.rgb, saturate(streak * 0.8) * I);

                // --------------------------------------------- particles, far -> near
                float amount = _PartParams.x;
                float size = _PartParams.y;
                float flutter = _PartMotion.z;
                float2 own = _PartMotion.xy * speed * t;                       // falling / rising
                float2 vel = wdir * windStrength * 0.6 + _PartMotion.xy;
                float2 mdir = vel / max(length(vel), 1e-4);
                float stretchBase = _PartMotion.w * (0.5 + 1.6 * gust);
                float behindBorder = 1.0 - band * 0.7;

                [branch] if (amount > 0.001)
                {
                float2 far  = ParticleLayer(sp, windDisp * 0.55 + own * 0.6, 20.0, amount * 0.9,  0.10 * size,  stretchBase * 0.6, 0.30, mdir, flutter, t, 1.0);
                float2 mid  = ParticleLayer(sp, windDisp * 1.0  + own * 1.0, 10.0, amount * 0.65, 0.085 * size, stretchBase * 1.0, 0.25, mdir, flutter, t, 2.0);
                float2 near = ParticleLayer(sp, windDisp * 1.7  + own * 1.6,  4.5, amount * 0.3,  0.07 * size,  stretchBase * 1.5, 0.0,  mdir, flutter, t, 3.0);

                col = BlendParticle(col, far,  0.5  * I * behindBorder, t);
                col = BlendParticle(col, mid,  0.85 * I * behindBorder, t);
                col = BlendParticle(col, near, 0.55 * I * behindBorder, t);
                }

                [branch] if (style > 1.5 && style < 2.5 && _HeatParams.w > 0.0 && uv.y < 0.3)
                {
                    float splash = SplashDrops(sp, aspect, t) * _HeatParams.w * I;
                    col = lerp(col, _BorderGlow.rgb * 0.92 + col * 0.12, splash);
                }

                // --------------------------------------------- border layer on top (on the "lens")
                float opacity = _BorderParams.y;
                [branch] if (style < 0.5)
                {
                    // Light leak: leaf shade moving in the breeze + warm sun glow from the top-left corner.
                    float dapple = smoothstep(0.42, 0.62, Fbm(sp * 4.5 + windDisp * 0.25 + float2(sin(t * 0.6), cos(t * 0.45)) * 0.08));
                    col = lerp(col, col * _BorderTint.rgb, saturate(band * opacity * (0.35 + 0.65 * dapple)));
                    float sun = saturate(1.0 - length((uv - float2(0.0, 1.0)) * float2(aspect, 1.0)) / 1.25);
                    sun = sun * sun * sun;
                    col += _BorderGlow.rgb * sun * detail * 0.4 * I * (0.85 + 0.15 * sin(t * 0.8));
                }
                else if (style < 1.5)
                {
                    // Dust: sand piling up at the edges, with coarse grains.
                    float grain = smoothstep(0.7, 0.9, ValueNoise(sp * 150.0 - bandScroll * 150.0)) * detail;
                    float3 dustCol = _BorderTint.rgb * (0.82 + 0.3 * n2) + col * 0.2;
                    col = lerp(col, dustCol, saturate(band * opacity));
                    col = lerp(col, _BorderGlow.rgb, grain * smoothstep(0.1, 0.8, band) * 0.35);
                }
                else if (style < 2.5)
                {
                    // Droplets: wet sheen in the band, drops with a dark rim and a highlight.
                    col = lerp(col, col * _BorderTint.rgb, saturate(band * opacity));
                    float rim = smoothstep(0.55, 1.0, length(dropN));
                    col = lerp(col, col * 1.06 + 0.02, dropMask);
                    col *= 1.0 - dropMask * rim * 0.4;
                    col += _BorderGlow.rgb * dropHi * 0.75;
                }
                else
                {
                    // Scorch: charred edge, glowing cracks, a hot inner rim.
                    float3 charCol = _BorderTint.rgb * (0.7 + 0.5 * n2) + col * 0.15;
                    col = lerp(col, charCol, saturate(band * opacity));
                    [branch] if (band > 0.002)
                    {
                        float2 cr = Voronoi(sp * 9.0 + n * 1.2);
                        float crack = 1.0 - smoothstep(0.0, 0.06, cr.y);
                        float pulse = 0.6 + 0.4 * sin(t * 1.7 + n * 9.0);
                        col += _BorderGlow.rgb * crack * smoothstep(0.3, 0.95, band) * detail * pulse * 0.9;
                    }
                    col += _BorderGlow.rgb * band * (1.0 - band) * 4.0 * 0.12 * (0.7 + 0.3 * g);
                }

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
