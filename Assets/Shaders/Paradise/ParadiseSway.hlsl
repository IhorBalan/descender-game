// Shared by the Paradise sprite shaders (Sprite Lit Sway, Sprite Unlit Haze): wind, the
// hero's push and distance haze, so a plant in the play layer and a tree three parallax
// layers back move and fade by the same rules.
//
// Contract: include this AFTER Core2D.hlsl and AFTER the shader's UnityPerMaterial
// CBUFFER, which must declare _SwayAmount, _SwaySpeed, _SwayScale, _SwayHeight,
// _SwayUseObjectY, _SwayHang, _SwayFlutter, _SwayFlutterSpeed, _SwayLag, _PushRadius,
// _PushStrength, _HazeColor and _HazeAmount. The functions read them directly so every
// pass of a shader shares one code path.
//
// How it moves. The wind is one field in world space (ParadiseWind, mirrored in C# by
// Descender.World.Paradise.GardenWind): a breeze plus gusts carried downwind, so a gust
// visibly travels along a row of plants. Each sprite is one plant: its pivot (the object
// origin; the SRP Batcher keeps one matrix per sprite) gives it its own stiffness,
// frequency and phase, so neighbours never move in step. A plant answers the wind like
// a damped spring: the gust it bends to is the wind over the last second weighed by a
// spring's impulse response (lag, a little overshoot, settle), it rocks at its own natural
// frequency with the turbulence, and its leaves flutter faster at the tips. Standing
// plants (pivot at the root) bend as a stalk, stiff at the base; hanging pieces (pivot at
// the top, _SwayHang = 1) swing as a pendulum from where they hang, with the motion
// lagging down their length and a ripple running down the strands or the cloth.
//
// Sprites here are full-rect quads, four vertices each, so the vertex stage can only
// shear them. The curve between the corners (the stalk's bend, the strand's ripple, the
// leaves' flutter) is done in the fragment stage by offsetting the texture lookup by
// what the shape wants minus what the vertex stage already did (ParadiseSwayUV), which
// is exact at the corners and needs no extra vertices. Hero push: see ParadisePush.
#ifndef DESCENDER_PARADISE_SWAY_INCLUDED
#define DESCENDER_PARADISE_SWAY_INCLUDED

// Globals, outside the material CBUFFER so the SRP Batcher still batches. Never set means
// all zero: no wind (z = 0) and no push.
// Set every frame by Descender.World.Paradise.GardenWind: x = base breeze, y = gust
// strength at the camera, z = direction (+1 blowing right, -1 left), w = gust travel phase
// (world units the gusts have travelled).
float4 _GardenWind;
// Set every frame by Descender.Fx.HeroWindPush: xy = the hero's chest, w = 1 with a hero.
float4 _HeroPos;
// The hero's last positions, newest first: xy = chest, z = age in seconds, w = push (0..1).
#define PARADISE_HERO_TRAIL 12
float4 _HeroTrail[PARADISE_HERO_TRAIL];

// The interpolators ParadiseSwayVertex fills and ParadiseSwayUV reads: put
// PARADISE_SWAY_OUTPUTS in the Varyings struct and PARADISE_SWAY_COPY(o) after it is filled.
#define PARADISE_SWAY_OUTPUTS float4 sway0 : TEXCOORD4; float4 sway1 : TEXCOORD5; float sway2 : TEXCOORD6;
#define PARADISE_SWAY_COPY(o) o.sway0 = sway0; o.sway1 = sway1; o.sway2 = sway2;

// Must match GardenWind.GustSpeed.
#define PARADISE_GUST_SPEED 4.5
#define PARADISE_TAU 6.2831853

// The gusts, 0 to 1, as a pattern carried downwind (u = x in the frame moving with them).
// Mirrors GardenWind.Gust.
float ParadiseGust(float u, float phase)
{
    float n = 0.5 * sin(u * 0.21) + 0.3 * sin(u * 0.127 + 1.7) + 0.2 * sin(u * 0.067 + 4.1);
    float envelope = 0.7 + 0.3 * sin(phase * 0.023 + u * 0.011 + 0.6);
    return smoothstep(0.1, 0.8, n) * envelope;
}

// The wind's strength (unsigned, about 0 to 1.5) at x for a gust phase. Mirrors
// GardenWind.Strength; the direction is _GardenWind.z.
float ParadiseWind(float x, float phase)
{
    float breathing = 0.85 + 0.15 * sin(phase * 0.041 + x * 0.017);
    return _GardenWind.x * breathing + ParadiseGust(x - _GardenWind.z * phase, phase);
}

// A cheap hash of the plant's pivot, 0..1, stable as long as the plant does not move.
float ParadiseHash(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

// The plant's stiffness, from its seed: _SwayAmount varied by up to 30% (times _SwayScale).
float ParadiseAmount(float seed)
{
    return _SwayAmount * (1.0 + _SwayScale * 0.3 * (frac(seed * 7.13 + 0.37) * 2.0 - 1.0));
}

// How far up the plant a point is, 0 at the root and 1 at the tip. UV.y by default;
// object-space y (pivot at the root) over _SwayHeight units for atlased sprites, whose
// UVs only cover part of the texture.
float ParadiseHeight(float2 uv, float rel)
{
    return saturate(lerp(uv.y, rel / max(_SwayHeight, 0.001), _SwayUseObjectY));
}

// The wind the plant bends to: the wind at its root over the last 1.2 of its periods,
// weighed by a damped spring's impulse response (damping 0.3). As the gusts travel, the
// wind a moment ago is the wind a little upwind now. Normalised so a steady wind gives
// itself back; a sudden gust overshoots by about a third before it settles.
float ParadiseSpringWind(float x, float omega)
{
    float period = PARADISE_TAU / omega;
    float sum = 0.0;
    float weights = 0.0;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        float tau = (k + 0.5) * 0.3 * period;
        float h = exp(-0.3 * omega * tau) * sin(omega * tau);
        sum += h * ParadiseWind(x, _GardenWind.w - PARADISE_GUST_SPEED * tau);
        weights += h;
    }
    return sum / weights;
}

// The hero's push at p, in units at the tip, signed (away from him). The trail of where
// he has been goes through a damped spring at omega: while he stands in a plant it is
// held apart; once he has passed, the samples where he was still in it fade through the
// spring's response, so the plant swings back past upright and settles. Taller than wide,
// so a plant reaching above his head is still brushed.
float ParadisePush(float2 p, float omega)
{
    float radius = max(_PushRadius, 0.001);
    float sum = 0.0;
    float weights = 0.0;
    [unroll]
    for (int k = 0; k < PARADISE_HERO_TRAIL; k++)
    {
        float4 s = _HeroTrail[k];
        float age = s.z;
        float window = saturate(1.0 - age / 0.9);
        float h = exp(-0.3 * omega * age) * sin(omega * age + 0.25) * window * window;
        float2 d = p - s.xy;
        float falloff = saturate(1.0 - length(d * float2(1.0, 0.6)) / radius);
        falloff = falloff * falloff * (3.0 - 2.0 * falloff);
        float side = clamp(d.x / (radius * 0.3), -1.0, 1.0);
        sum += h * falloff * side * s.w;
        weights += h;
    }
    float push = weights > 0.001 ? sum / weights : 0.0;
    return clamp(push, -1.4, 1.4) * _PushStrength;
}

// The shape a plant takes, as a world-space offset of the point at rest position r,
// height rel above its pivot (negative below it) and UV uv. params is per plant, computed
// once in the vertex stage: x = lean (standing: units at the tip, push included;
// hanging: radians), y = the wind's strength there now, z = the plant's seed, w = its
// natural frequency (rad/s). The fragment stage calls this again to draw the curve.
float2 ParadiseShape(float4 params, float2 r, float rel, float2 uv)
{
    float t = _Time.y;
    float wind = params.y;
    float seed = params.z;
    float omega = params.w;
    float phase = seed * PARADISE_TAU;
    // Leaves and ripples are faster than the sway, but never faster than real foliage.
    float omegaFast = min(omega * _SwayFlutterSpeed, 12.0);
    float2 offset;

    UNITY_BRANCH
    if (_SwayHang < 0.5)
    {
        // Standing: a stalk curving from a stiff base, tip dipping so it keeps its length.
        float h = ParadiseHeight(uv, rel);
        float dx = params.x * h * h;
        float dy = -min(0.5 * dx * dx / max(rel, 0.25), 0.5 * abs(dx));
        // Leaves flutter at the tips, more as the wind rises, each patch in its own phase.
        float leaf = _SwayFlutter * (0.2 + wind) * smoothstep(0.2, 1.0, h);
        float fx = 0.6 * sin(omegaFast * t + 4.7 * r.x + 3.1 * r.y + phase)
                 + 0.4 * sin(1.37 * omegaFast * t - 3.9 * r.x + 5.3 * r.y + 2.3 * phase);
        float fy = 0.45 * sin(1.13 * omegaFast * t + 3.3 * r.x - 4.1 * r.y + 1.7 * phase);
        offset = float2(dx, dy) + leaf * float2(fx, fy);
    }
    else
    {
        // Hanging: a pendulum from the top. Its angle at a distance s down the piece is
        // the wind's lean plus a swing whose phase lags by _SwayLag per unit down, so the
        // free end trails the top; integrating the angle down the length gives x.
        float s = max(-rel, 0.0);
        float amount = ParadiseAmount(seed);
        float swing = amount * (0.15 + 0.45 * wind);
        float lag = max(_SwayLag, 0.01);
        float p = omega * t + phase;
        float x = params.x * s + swing * (cos(p - lag * s) - cos(p)) / lag;
        // A ripple running down the strands or the cloth, out of step across its width.
        float ripple = _SwayFlutter * (0.15 + wind);
        float pr = omegaFast * t + phase * 3.7 + 2.7 * r.x;
        x += ripple * (cos(pr - 2.6 * s) - cos(pr)) / 2.6 * saturate(s * 0.7);
        // The free end rises a little as it swings out (it keeps its length).
        float y = -min(0.5 * x * x / max(s, 0.15), 0.5 * abs(x));
        offset = float2(x, y);
    }
    return offset;
}

// The per-vertex work. Moves positionOS and fills the three interpolators the fragment
// stage needs for ParadiseSwayUV: sway0 = (rest position xy, the offset the vertex got
// from the shape), sway1 = the plant's params (see ParadiseShape), sway2 = rel.
void ParadiseSwayVertex(inout float3 positionOS, float2 uv, out float4 sway0, out float4 sway1, out float sway2)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 pivot = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float rel = positionWS.y - pivot.y;

    float seed = ParadiseHash(pivot.xy);
    float omega = max(_SwaySpeed, 0.05) * (1.0 + _SwayScale * 0.35 * (frac(seed * 3.71 + 0.11) * 2.0 - 1.0));
    float amount = ParadiseAmount(seed);
    float on = abs(_GardenWind.z);
    float wind = ParadiseWind(pivot.x, _GardenWind.w) * on;
    float lean = _GardenWind.z * amount * ParadiseSpringWind(pivot.x, omega);

    // The hero's push springs a little faster than the plant sways: it is a knock, not the wind.
    float push = ParadisePush(positionWS.xy, clamp(omega * 1.2, 4.0, 10.0));
    float2 extra = 0.0;
    if (_SwayHang < 0.5)
    {
        // The plant rocking at its own frequency with the turbulence, its swings uneven.
        float phase = seed * PARADISE_TAU;
        float t = _Time.y;
        float rock = (0.15 + 0.55 * wind)
                   * (0.65 * sin(omega * t + phase) + 0.35 * sin(1.71 * omega * t + 2.1 * phase + 1.3))
                   * (0.75 + 0.25 * sin(0.29 * omega * t + 3.0 * phase));
        lean += 0.3 * amount * rock * on + push;
    }
    else
    {
        // Brushed aside along its length; the pendulum swing is in the shape.
        extra.x = push * saturate(-rel / 1.2);
    }

    float4 params = float4(lean, wind, seed, omega);
    float2 shape = ParadiseShape(params, positionWS.xy, rel, uv);
    sway0 = float4(positionWS.xy, shape);
    sway1 = params;
    sway2 = rel;
    positionWS.xy += shape + extra;
    positionOS = TransformWorldToObject(positionWS);
}

// The texture coordinate a fragment should show so the sprite takes the curved shape,
// not just the vertices' shear: the point at rest position r (interpolated) should sit
// where ParadiseShape puts it, but the vertex stage only moved it by the interpolated
// offset; the difference, mapped from world units to UV through the sprite's own
// rest-pose derivatives, is the lookup's correction. Exact at the vertices.
float2 ParadiseSwayUV(float2 uv, float4 sway0, float4 sway1, float sway2)
{
    float2 r = sway0.xy;
    float2 shape = ParadiseShape(sway1, r, sway2, uv);
    float2 miss = clamp(sway0.zw - shape, -0.3, 0.3);

    float2 uvX = ddx(uv);
    float2 uvY = ddy(uv);
    float2 rX = ddx(r);
    float2 rY = ddy(r);
    float det = rX.x * rY.y - rY.x * rX.y;
    if (abs(det) < 1e-12)
    {
        return uv;
    }
    // d(uv)/d(world) = [uvX uvY] * inverse([rX rY]).
    float2 col0 = (uvX * rY.y - uvY * rX.y) / det;
    float2 col1 = (uvY * rX.x - uvX * rY.x) / det;
    return uv + col0 * miss.x + col1 * miss.y;
}

// Atmospheric perspective: pull the final colour toward the zone's sky/haze colour.
half3 ParadiseHaze(half3 color)
{
    return lerp(color, _HazeColor.rgb, _HazeAmount);
}

#endif // DESCENDER_PARADISE_SWAY_INCLUDED
