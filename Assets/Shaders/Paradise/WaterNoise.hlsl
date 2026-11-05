// Shared by Paradise's water shaders (Waterfall, Water Foam, Water Mist): cheap value
// noise and fBm, periodic every 289 cells so coordinates that grow with time (the
// water's own flow, read in seconds since it left the lip) keep their precision and
// never show a seam: the hash only ever sees whole numbers below 289.
#ifndef DESCENDER_WATER_NOISE_INCLUDED
#define DESCENDER_WATER_NOISE_INCLUDED

// Dave Hoskins' hash without sine, on a cell wrapped into 0..288.
float WaterHash(float2 cell)
{
    cell -= floor(cell / 289.0) * 289.0;
    float3 p = frac(cell.xyx * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// Value noise, 0..1, with a quintic fade so its slope is smooth (no grid creases).
float WaterNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = p - i;
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = WaterHash(i);
    float b = WaterHash(i + float2(1.0, 0.0));
    float c = WaterHash(i + float2(0.0, 1.0));
    float d = WaterHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Three octaves, 0..1. The octaves are offset, not rotated, which is enough at this size.
float WaterFbm(float2 p)
{
    float v = 0.5 * WaterNoise(p);
    v += 0.25 * WaterNoise(p * 2.03 + float2(17.1, 3.7));
    v += 0.125 * WaterNoise(p * 4.01 + float2(5.3, 31.7));
    return v / 0.875;
}

// Glints: one sparkle per cell at most, at a random spot, twinkling on its own clock.
// `p` in cells, `t` seconds; returns 0..1.
float WaterGlint(float2 p, float t, float density)
{
    float2 cell = floor(p);
    float h = WaterHash(cell + 71.0);
    float2 spot = float2(WaterHash(cell + 13.0), WaterHash(cell + 29.0)) * 0.6 + 0.2;
    float2 d = p - cell - spot;
    float point_ = saturate(1.0 - dot(d, d) * 90.0);
    float twinkle = saturate(sin(t * (3.0 + h * 5.0) + h * 40.0) * 2.0 - 1.0);
    return point_ * point_ * twinkle * step(1.0 - density, h);
}

#endif
