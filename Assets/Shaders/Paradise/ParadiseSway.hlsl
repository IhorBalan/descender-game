// Shared by the Paradise sprite shaders (Sprite Lit Sway, Sprite Unlit Haze): wind sway,
// the hero's push and distance haze, so a plant in the play layer and a tree three
// parallax layers back move and fade by the same rules.
//
// Contract: include this AFTER Core2D.hlsl and AFTER the shader's UnityPerMaterial
// CBUFFER, which must declare _SwayAmount, _SwaySpeed, _SwayScale, _SwayHeight,
// _SwayUseObjectY, _PushRadius, _PushStrength, _HazeColor and _HazeAmount. The
// functions read them directly so every pass of a shader shares one code path.
#ifndef DESCENDER_PARADISE_SWAY_INCLUDED
#define DESCENDER_PARADISE_SWAY_INCLUDED

// Global, set every frame by Descender.Fx.HeroWindPush: xy = the hero's chest in world
// units, w = 1 while there is a hero (0 = no push). Outside the material CBUFFER so the
// SRP Batcher still batches; never set means all zero, so no push.
float4 _HeroPos;

// How far up the plant a vertex is, 0 at the root and 1 at the tip, squared so the
// stem bends like a stalk rather than sliding sideways. UV.y is the default: it does
// not care where the pivot is and survives sprite batching (vertices pre-transformed
// to world space). Object-space y (pivot at the root) is for atlased sprites, whose
// UVs only cover part of the texture: height = _SwayHeight units above the pivot.
float ParadiseSwayWeight(float2 uv, float3 positionOS)
{
    float h = lerp(uv.y, positionOS.y / max(_SwayHeight, 0.001), _SwayUseObjectY);
    h = saturate(h);
    return h * h;
}

// World-space offset for one vertex. The wind is three detuned sines whose phase comes
// from world position, so neighbouring plants never move in step; the tip lags the base
// a little (the y term) which reads as a gust travelling up the stalk. The push leans
// the plant away from the hero, smoothly through zero when he is right on it, and dips
// the tip so the stalk seems to keep its length.
float3 ParadiseSwayOffsetWS(float3 positionWS, float weight)
{
    float t = _Time.y * _SwaySpeed;
    float phase = (positionWS.x + positionWS.y * 0.35) * _SwayScale;
    float wave = sin(t + phase) * 0.6
               + sin(t * 2.31 + phase * 1.73) * 0.15
               + sin(t * 0.43 + phase * 0.61) * 0.25;
    float lean = wave * _SwayAmount * weight;

    float radius = max(_PushRadius, 0.001);
    float2 d = positionWS.xy - _HeroPos.xy;
    // Taller than wide: a plant reaching above the hero's head is still brushed.
    float falloff = saturate(1.0 - length(d * float2(1.0, 0.6)) / radius);
    falloff = falloff * falloff * (3.0 - 2.0 * falloff);
    float side = clamp(d.x / (radius * 0.3), -1.0, 1.0);
    float push = falloff * _PushStrength * weight * _HeroPos.w;
    lean += side * push;

    return float3(lean, -abs(lean) * 0.3, 0.0);
}

// Object-space position after sway and push. Does the work in world space (phase and
// hero distance live there) and goes back, so CommonLitVertex and friends stay unchanged.
float3 ParadiseApplySway(float3 positionOS, float2 uv)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    positionWS += ParadiseSwayOffsetWS(positionWS, ParadiseSwayWeight(uv, positionOS));
    return TransformWorldToObject(positionWS);
}

// Atmospheric perspective: pull the final colour toward the zone's sky/haze colour.
half3 ParadiseHaze(half3 color)
{
    return lerp(color, _HazeColor.rgb, _HazeAmount);
}

#endif // DESCENDER_PARADISE_SWAY_INCLUDED
