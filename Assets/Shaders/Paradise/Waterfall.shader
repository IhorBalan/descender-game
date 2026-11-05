// Falling water for Paradise's waterfalls (docs/design/paradise.md, section 5), on a
// SpriteRenderer stretched from the lip to the foot (Waterfall.cs sets the fall's size).
//
// The water is read where it came from, not where it is: each pixel asks when the water
// now passing it left the lip (e = time - time to fall this far, under gravity from a lip
// speed that varies across the width), and the streaks, wobble and breakup are all
// functions of (across, e). So the sheet accelerates and its streaks stretch as they
// fall, neighbouring columns slide past each other, and nothing ever tears or shears
// over time. Down the fall: a smooth glassy curl at the lip with a band of sunlight on
// it, water turning white as it aerates, a sheet that spreads and frays at its edges,
// and strands with spray between them toward the foot. Falls into the clouds
// (_Dissolve 1) thin into mist instead of ending. Unlit; kept below 1 except for small
// glints, so it never outshines the play layer. The texture repeats vertically.
Shader "Descender/Waterfall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Water Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.92, 0.97, 1, 0.9)
        _Brightness ("Brightness", Float) = 0.96
        _Speed ("Speed Multiplier (per fall)", Float) = 1
        _LipSpeed ("Speed At The Lip (units/s)", Float) = 1.6
        _Gravity ("Gravity (units/s^2)", Float) = 5.5
        _Repeat ("Units Of Streak Per Texture Repeat At The Lip", Float) = 2.2
        _Height ("Fall Height (units, per fall)", Float) = 10
        _Width ("Fall Width (units, per fall)", Float) = 1.6
        _Seed ("Seed (per fall)", Float) = 0
        _Dissolve ("Into The Clouds (0/1, per fall)", Float) = 0
        _DissolveLength ("Units Over Which It Thins Into Cloud (per fall)", Float) = 12
        _Curl ("Lip Curl (units the far side drops before it falls)", Float) = 0.45
        _DeepColor ("Deep Water", Color) = (0.36, 0.56, 0.68, 1)
        _GlassColor ("Glassy Water At The Lip", Color) = (0.56, 0.78, 0.84, 1)
        _FoamColor ("Aerated Water", Color) = (0.93, 0.97, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/Shaders/Paradise/WaterNoise.hlsl"

            sampler2D _MainTex;
            float4 _Color;
            float _Brightness;
            float _Speed;
            float _LipSpeed;
            float _Gravity;
            float _Repeat;
            float _Height;
            float _Width;
            float _Seed;
            float _Dissolve;
            float _DissolveLength;
            float _Curl;
            float4 _DeepColor;
            float4 _GlassColor;
            float4 _FoamColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                return o;
            }

            float Band(float x, float centre, float width)
            {
                float k = (x - centre) / width;
                return exp(-k * k);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float h = max(_Height, 0.5);
                float w = max(_Width, 0.3);
                float d = (1.0 - i.uv.y) * h;           // units below the lip
                float p = saturate(d / h);              // 0 at the lip, 1 at the foot
                float xu = (i.uv.x - 0.5) * w;          // units from the centre line
                float t = _Time.y;
                float seed = _Seed;

                // When the water here left the lip. The lip speed varies across the width,
                // so the columns fall at different rates and the streaks slide past each other.
                float column = WaterNoise(float2(xu * 1.7 + seed, seed * 0.37));
                float v0 = max(_LipSpeed * _Speed * (0.78 + 0.44 * column), 0.1);
                float g = max(_Gravity, 0.1);
                float vel = sqrt(v0 * v0 + 2.0 * g * d);
                float e = t - (vel - v0) / g;

                // The sheet sways and spreads as it falls, more the further it has gone.
                float wob = (WaterFbm(float2(xu * 0.8 + seed, e * 0.85)) - 0.5) * (0.03 + 0.28 * p);
                float spread = lerp(0.78, 1.0, sqrt(p));
                float xr = (xu - wob) / (0.5 * w * spread);   // -1..1 across the sheet
                float ax = abs(xr);

                // Streaks: the texture in two layers, read in the water's own time, so they
                // are short at the lip and stretch out as the water speeds up.
                float rate = _LipSpeed / max(_Repeat, 0.1);
                float4 sa = tex2D(_MainTex, float2(0.5 + xr * 0.42, e * rate + seed));
                float4 sb = tex2D(_MainTex, float2(0.5 - xr * 0.3, e * rate * 1.9 + 0.37 + seed));
                // The painted streaks only span a narrow band of brightness: stretch it to 0..1.
                float streak = saturate((dot(lerp(sa.rgb, sb.rgb, 0.45), float3(0.3, 0.59, 0.11)) - 0.77) / 0.17);
                float body = saturate(lerp(sa.a, sb.a, 0.45) / 0.6);

                // The lip's edge: the water spills from the spout on the left and rounds over,
                // so the far side of the sheet starts lower; `dl` is the depth below that edge.
                float side = saturate(xr * 0.5 + 0.5);
                float dl = d - _Curl * side * side;

                // Aeration: fine churn that whitens the water as it falls.
                float churn = WaterFbm(float2(xu * 3.2 / spread + seed * 3.0, e * 3.1));
                float glass = 1.0 - smoothstep(0.1, 0.8, dl);
                float aerate = smoothstep(0.3, 3.5, d);
                float core = 1.0 - ax * ax;
                float white = saturate(
                    0.12 + streak * lerp(0.5, 0.85, aerate) + (churn - 0.45) * 1.1 * aerate + 0.2 * core * aerate
                );
                // Fine striations, the threads a sheet of falling water is made of.
                float fine = WaterNoise(float2(xu * 15.0 / spread + seed, e * 4.5));
                white = saturate(white + (fine - 0.5) * 0.35 * aerate);
                white = white * white * (3.0 - 2.0 * white);
                float3 rgb = lerp(_DeepColor.rgb, _FoamColor.rgb, white);
                float alpha = lerp(0.74, 0.97, white);

                // The glassy curl at the lip: smooth, clearer water with a band of sun on it,
                // a darker underside just below, and a soft second reflection.
                float smoothStreak = lerp(0.5, streak, 0.4);
                float3 glassRgb = _GlassColor.rgb * lerp(0.82, 1.15, smoothStreak);
                rgb = lerp(rgb, glassRgb, glass);
                alpha = lerp(alpha, 0.8, glass);
                float shimmer = 0.65 + 0.35 * WaterNoise(float2(xu * 3.5 - t * 0.7, seed));
                float catchLight = Band(dl, 0.12, 0.06) * (1.0 - 0.55 * ax * ax) * shimmer;
                rgb = lerp(rgb, float3(1.0, 0.99, 0.95), saturate(catchLight * 0.8));
                rgb += Band(dl, 0.6, 0.25) * 0.12 * shimmer;
                rgb *= 1.0 - 0.12 * Band(dl, 0.28, 0.07);
                alpha = saturate(alpha + catchLight * 0.3);

                // Glints where the streaks catch the sun, riding down with the water.
                float glint = WaterGlint(float2(xr * 4.0 + seed, e * 5.0), t, 0.18) * (1.0 - glass * 0.5);
                rgb += glint * 0.3 * saturate(white + 0.3);

                // Edges: thinner, clearer water that frays, each side on its own noise.
                float ragged = WaterFbm(float2(e * 2.3 + seed, xr > 0.0 ? 3.0 : 9.0));
                float inner = lerp(0.82, 0.62 - 0.2 * ragged, sqrt(p));
                float edge = smoothstep(1.0, inner, ax);

                // Strands: toward the foot the sheet breaks along the flow into strands
                // (elongated by the water's speed, since they live in its time), with spray
                // between them. Into the clouds it breaks sooner and thins away entirely.
                float strandNoise = WaterFbm(float2(xu * 2.6 / spread + seed, e * 1.25));
                // How far into the stretch where it thins into cloud, 0..1 (0 for ground falls).
                float into = saturate((d - (h - max(_DissolveLength, 0.5))) / max(_DissolveLength, 0.5)) * _Dissolve;
                // Ground falls fray over their last few units only, and keep most of their body.
                float fray = smoothstep(h - min(5.0, h * 0.45), h, d);
                float breakup = lerp(fray * 0.5, into * 1.05, _Dissolve);
                breakup += smoothstep(0.55, 1.0, ax) * (0.12 + 0.25 * p);
                float strands = smoothstep(breakup - 0.1, breakup + 0.1, strandNoise);
                float spray =
                    max(fray * (1.0 - _Dissolve), into) * WaterFbm(float2(xu * 1.4 + seed, d * 0.9 - t * 0.8)) * (1.0 - strands);
                spray *= smoothstep(1.25, 0.6, ax) * lerp(0.35, 0.5, _Dissolve);
                rgb = lerp(rgb, _FoamColor.rgb, saturate(spray * 1.5) * (1.0 - strands));

                // Ends: a short fade at the lip under the spout; into foam at the foot, or
                // into the clouds as mist, the water whitening and thinning on the way.
                float top = smoothstep(0.0, 0.05, dl);
                float footFade = smoothstep(h, h - 0.35, d);
                float cloud = WaterFbm(float2(xu * 0.9 + seed, d * 0.35 - t * 0.25));
                float mistFade = smoothstep(1.0, 0.35, into + (cloud - 0.5) * 0.4);
                float foot = lerp(footFade, mistFade, _Dissolve);
                rgb = lerp(rgb, _FoamColor.rgb, smoothstep(0.0, 0.8, into) * 0.6);

                float a = body * alpha * edge * strands + spray * edge;
                a = saturate(a) * top * foot;
                float4 col = float4(rgb * _Brightness, a) * _Color * i.color;
                return col;
            }
            ENDCG
        }
    }
}
