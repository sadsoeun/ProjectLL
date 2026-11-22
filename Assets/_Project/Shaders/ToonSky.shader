// Stylised sky backdrop for side-scrolling stages: vertical gradient (zenith -> horizon), a soft sun glow and
// cel-shaded drifting clouds (two fbm layers, stepped edges + a shadow band). Drawn on a large quad far behind the
// level (StageLayoutBuilder "Sky"), unlit, no fog (it IS the far colour), no depth write.
Shader "MyGame/Sky/ToonSky"
{
    Properties
    {
        _ZenithColor("Zenith", Color) = (0.24, 0.42, 0.74, 1)
        _HorizonColor("Horizon", Color) = (0.66, 0.78, 0.92, 1)
        _HorizonHeight("Horizon (0-1 of the quad)", Range(0, 1)) = 0.12
        _GradientPower("Gradient Power", Range(0.3, 3)) = 0.9
        _SunColor("Sun Glow", Color) = (1, 0.95, 0.85, 1)
        _SunPos("Sun Position (uv)", Vector) = (0.72, 0.78, 0, 0)
        _SunSize("Sun Glow Size", Range(0.01, 0.6)) = 0.22
        _CloudColor("Cloud Light", Color) = (0.98, 0.99, 1, 1)
        _CloudShade("Cloud Shade", Color) = (0.74, 0.8, 0.92, 1)
        _CloudScale("Cloud Scale (x, y)", Vector) = (9, 3.2, 0, 0)
        _CloudCover("Cloud Coverage", Range(0, 1)) = 0.48
        _CloudBand("Cloud Band (min y, max y)", Vector) = (0.25, 0.9, 0, 0)
        _CloudSpeed("Cloud Drift", Float) = 0.004
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+500" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _SunColor, _CloudColor, _CloudShade;
                float4 _SunPos, _CloudScale, _CloudBand;
                float _HorizonHeight, _GradientPower, _SunSize, _CloudCover, _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldXY : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = v.uv;
                o.worldXY = p.positionWS.xy;
                return o;
            }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 4; k++) { s += a * noise(p); p = p * 2.03 + 17.1; a *= 0.42; }
                return s / 0.86;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float h = saturate((uv.y - _HorizonHeight) / max(1e-3, 1 - _HorizonHeight));
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(h, _GradientPower));

                // soft sun glow (aspect-corrected by the cloud scale ratio)
                float2 d = (uv - _SunPos.xy) * float2(_CloudScale.x / _CloudScale.y, 1);
                float glow = saturate(1 - length(d) / _SunSize);
                col += _SunColor.rgb * glow * glow * 0.35;

                // clouds: world-anchored so they parallax with the quad, slow drift
                float2 cp = float2(i.worldXY.x * 0.01, uv.y) * _CloudScale.xy + float2(_Time.y * _CloudSpeed * _CloudScale.x, 0);
                float band = smoothstep(_CloudBand.x, _CloudBand.x + 0.12, uv.y) * (1 - smoothstep(_CloudBand.y - 0.15, _CloudBand.y, uv.y));
                float n = fbm(cp) * band;
                // puffy tops: bias coverage upward in the middle of each cloud row
                n += 0.12 * sin(cp.y * 3.14159);
                float n2 = fbm(cp + float2(0.05, -0.35)) * band + 0.12 * sin((cp.y - 0.35) * 3.14159); // sample below -> shaded underside
                float t = 1 - _CloudCover;
                float body = smoothstep(t, t + 0.03, n);                 // cel edge
                float shade = 1 - smoothstep(t + 0.05, t + 0.09, n2 * band); // underside band
                half3 cloud = lerp(_CloudColor.rgb, _CloudShade.rgb, shade * 0.85);
                // far, smaller wisps near the horizon
                float w = fbm(cp * float2(2.2, 3.5) + 40) * smoothstep(_HorizonHeight, _HorizonHeight + 0.1, uv.y) * (1 - smoothstep(_HorizonHeight + 0.15, _HorizonHeight + 0.35, uv.y));
                col = lerp(col, lerp(_HorizonColor.rgb, _CloudColor.rgb, 0.6), smoothstep(0.55, 0.58, w) * 0.6);
                col = lerp(col, cloud, body);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
