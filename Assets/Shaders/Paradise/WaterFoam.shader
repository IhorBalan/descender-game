// The foot of a Paradise waterfall that lands on ground: a shallow pool seen almost edge
// on, with a heap of churning foam under the fall that boils upward and rolls outward,
// foam rings that spread across the pool and break up as they go, a glassy waterline,
// and a few glints of sun. All procedural, on a SpriteRenderer stretched to the pool
// (WaterQuad.cs sets _Size and _Seed; _Shape.x is the fall's half width and _Shape.y
// the ground's height above the quad's bottom, both in units). Unlit and low: it sits
// on a walkable deck, so it stays under the hero's knees and lets the deck show through.
Shader "Descender/Water Foam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused (the quad's sprite)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _FoamColor ("Foam", Color) = (0.93, 0.96, 1, 1)
        _ShadeColor ("Foam In Shadow", Color) = (0.5, 0.62, 0.72, 1)
        _WaterColor ("Pool Water", Color) = (0.42, 0.62, 0.72, 1)
        _Size ("Quad Size (units, per pool)", Vector) = (5, 2, 0, 0)
        _Shape ("Fall Half Width, Ground Height (units)", Vector) = (0.8, 0.6, 0, 0)
        _Seed ("Seed (per pool)", Float) = 0
        _HeapHeight ("Foam Heap Height (units)", Float) = 0.7
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

            float4 _Color;
            float4 _FoamColor;
            float4 _ShadeColor;
            float4 _WaterColor;
            float4 _Size;
            float4 _Shape;
            float _Seed;
            float _HeapHeight;

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

            // How deep inside the churning foam a point is: positive inside, rising toward its
            // core; the boiling noise rolls outward on each side and rises in crowns.
            float FoamDensity(float2 q, float ax, float side, float heapTop, float t, float seed)
            {
                float2 roll = float2(ax * 3.0 - t * 1.2 + seed, q.y * 2.6 - t * 1.0);
                float outward = lerp(WaterFbm(roll), WaterFbm(roll + float2(41.3, 0.0)), side);
                float crowns = WaterFbm(float2(q.x * 3.4 + seed * 1.7, q.y * 1.3 - t * 1.8));
                float boil = lerp(outward, crowns, 0.45);
                return (heapTop - q.y) / 0.35 + (boil - 0.5) * 2.2;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(_Size.xy, 0.1);
                float hw = max(_Shape.x, 0.2);
                // Units, from where the fall meets the ground.
                float2 q = float2((i.uv.x - 0.5) * size.x, i.uv.y * size.y - _Shape.y);
                float ax = abs(q.x);
                float t = _Time.y;
                float seed = _Seed;
                // Clear air above the heap and beyond the pool: skip the noise there.
                if (ax > hw * 4.0 || q.y > _HeapHeight * 1.6 + 0.3 || q.y < -0.35)
                {
                    return 0;
                }

                // The heap: white water churning where the fall lands, tallest under it, its
                // outline breathing and rolling outward, its surface boiling up in crowns.
                // Each side rolls on its own noise (blended under the fall), so they never mirror.
                float side = smoothstep(-0.4, 0.4, q.x);
                float swell = lerp(
                    WaterFbm(float2(ax * 1.5 - t * 0.6 + seed, t * 0.3)),
                    WaterFbm(float2(ax * 1.5 - t * 0.6 + seed + 41.3, t * 0.3)),
                    side
                );
                float heapTop = _HeapHeight * (0.35 + 0.75 * exp(-ax * ax / (hw * hw * 1.2))) * (0.6 + 0.8 * swell);
                float d0 = FoamDensity(q, ax, side, heapTop, t, seed);
                // Lit from above: brighter where the foam thins upward, shaded in its folds.
                float d1 = FoamDensity(q + float2(0.0, 0.07), ax, side, heapTop, t, seed);
                float reach = hw * (2.1 + 0.5 * swell);
                float heap = smoothstep(-0.1, 0.9, d0) * smoothstep(-0.3, -0.06, q.y) * smoothstep(reach, reach * 0.35, ax);
                float bubbles = WaterFbm(float2(ax * 9.0 - t * 1.7, q.y * 9.0 - t * 2.3 + seed));
                float lit = saturate(0.5 + (d0 - d1) * 0.9);
                float3 foamRgb = lerp(_ShadeColor.rgb, _FoamColor.rgb, saturate(lit * 0.85 + 0.1 + (bubbles - 0.5) * 0.5));
                float foam = heap * (0.75 + 0.25 * smoothstep(0.3, 0.7, bubbles));

                // The pool: a thin sheet of water along the ground, a bright waterline.
                float poolReach = hw * 3.6;
                float pool = smoothstep(poolReach, poolReach * 0.6, ax) * smoothstep(-0.16, -0.02, q.y)
                    * smoothstep(0.07, 0.0, q.y);
                float waterline = exp(-pow(q.y / 0.025, 2.0)) * smoothstep(poolReach, poolReach * 0.5, ax);

                // Rings: foam pushed out across the pool, seen edge on as flat ellipses that
                // widen, thin and break up as they go.
                float rings = 0.0;
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    float phase = frac(t * 0.33 + k / 3.0 + seed * 0.137);
                    float radius = hw * (0.8 + 3.0 * phase);
                    float r = length(float2(q.x, q.y / 0.11));
                    float width = 0.05 + 0.12 * phase;
                    float ring = exp(-pow((r - radius) / width, 2.0));
                    float broken = smoothstep(0.3, 0.7, WaterNoise(float2(q.x / radius * 3.0 + k * 11.0 + seed, k * 5.0 + floor(t * 0.33 + k / 3.0 + seed * 0.137) * 3.1)));
                    float fade = (1.0 - phase) * smoothstep(0.0, 0.12, phase);
                    rings += ring * broken * fade;
                }
                rings = saturate(rings) * smoothstep(0.12, -0.02, q.y);

                // Glints on the waterline and the wet foam.
                float glint = WaterGlint(float2(q.x * 5.0 + seed, q.y * 9.0), t, 0.22) * saturate(pool + heap) * lit;

                float3 rgb = _WaterColor.rgb;
                float a = pool * 0.55 + waterline * 0.4;
                rgb = lerp(rgb, _FoamColor.rgb, saturate(rings + waterline * 0.6));
                a = max(a, rings * 0.8);
                rgb = lerp(rgb, foamRgb, saturate(foam * 1.4));
                a = max(a, foam * 0.95);
                rgb += glint * 0.35;
                a = saturate(a + glint * 0.3);
                return float4(rgb, a) * _Color * i.color;
            }
            ENDCG
        }
    }
}
