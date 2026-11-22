#ifndef WM_AMBIENT_COMMON_INCLUDED
#define WM_AMBIENT_COMMON_INCLUDED

// Shared helpers for the ambient (water / wind / lava) shaders.
// Global wind is driven by AmbientWind (Scripts/Ambient): xy = direction on the XZ plane, z = strength scale, w = gust 0..1.
float4 _WM_Wind;

float4 WMGetWind()
{
    float4 w = _WM_Wind;
    if (dot(w.xy, w.xy) < 1e-4)
        w = float4(0.8, 0.6, 1.0, 0.0); // AmbientWind not in the scene: gentle default breeze
    w.xy = normalize(w.xy);
    return w;
}

float WMHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 WMHash22(float2 p)
{
    float n = WMHash21(p);
    return float2(n, WMHash21(p + n + 17.17));
}

float WMValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = WMHash21(i);
    float b = WMHash21(i + float2(1, 0));
    float c = WMHash21(i + float2(0, 1));
    float d = WMHash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float WMFbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += a * WMValueNoise(p);
        p = p * 2.03 + 11.7;
        a *= 0.5;
    }
    return v;
}

// Distance to the nearest animated cell point (cellular noise); the cell points orbit over time.
float WMVoronoi(float2 p, float time)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float best = 8.0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float2 g = float2(x, y);
        float2 o = WMHash22(i + g);
        o = 0.5 + 0.5 * sin(time + 6.2831 * o);
        float2 r = g + o - f;
        best = min(best, dot(r, r));
    }
    return sqrt(best);
}

// Distance to the nearest cell border (F2 - F1 of animated cellular noise): small values trace a caustic-like web.
float WMVoronoiEdge(float2 p, float time)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float f1 = 8.0, f2 = 8.0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float2 g = float2(x, y);
        float2 o = WMHash22(i + g);
        o = 0.5 + 0.4 * sin(time + 6.2831 * o);
        float d = length(g + o - f);
        if (d < f1) { f2 = f1; f1 = d; }
        else if (d < f2) { f2 = d; }
    }
    return f2 - f1;
}

// Sway offset for a vertex. weight 0 = anchored, 1 = free; pivotWS gives every object its own phase.
float3 WMWindOffset(float3 positionWS, float3 pivotWS, float weight, float strength, float speed, float flutter)
{
    float4 wind = WMGetWind();
    float t = _Time.y * speed;
    float phase = dot(pivotWS.xz, float2(0.37, 0.23));
    float sway = sin(t + phase) * 0.7 + sin(t * 2.1 + phase * 1.3) * 0.3;
    sway = sway * 0.5 + 0.5 + wind.w * 0.6;               // mostly leaning downwind, gusts push further
    // flutter phase varies slowly across the mesh so neighbouring vertices move together (no jelly ripple)
    float flick = sin(_Time.y * speed * 4.0 + dot(positionWS, float3(0.5, 0.4, 0.6))) * flutter;
    float3 dir = float3(wind.x, 0.0, wind.y);
    float3 side = float3(-wind.y, 0.0, wind.x);
    float amount = strength * wind.z * weight * weight; // bend curve: the lower canopy stays put, the top leans
    float3 offset = dir * sway * amount + (side * flick + float3(0, flick * 0.5, 0)) * amount;
    offset.y -= sway * sway * amount * 0.25;              // bending also lowers the tip a little
    return offset;
}

#endif
