// Spray mist off a Paradise waterfall: a plume that billows up from where the water
// breaks, widening and thinning as it rises and leaning with the breeze. Domain-warped
// noise carried upward, its features growing with height, so it rolls and swells rather
// than scrolling. On a SpriteRenderer stretched to the plume (WaterQuad.cs sets _Size and
// _Seed; _Shape.x is the source's half width, _Shape.y its height above the quad's bottom
// _Shape.z how high the mist rises before it is gone and _Shape.w the source's offset from
// the quad's middle, all in units). Into the clouds,
// the same plume is larger and denser, so the fall thins into it. Soft and translucent:
// it veils, it never hides the play layer.
Shader "Descender/Water Mist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused (the quad's sprite)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.99, 0.97, 1)
        _Size ("Quad Size (units, per plume)", Vector) = (6, 6, 0, 0)
        _Shape ("Source Half Width, Source Height, Rise, Source Offset From Middle (units)", Vector) = (1, 0.5, 4, 0)
        _Seed ("Seed (per plume)", Float) = 0
        _RiseSpeed ("Rise Speed (units/s)", Float) = 0.45
        _Wind ("Sideways Drift (units/s)", Float) = 0.18
        _Density ("Density", Range(0, 2)) = 1
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
            float4 _Size;
            float4 _Shape;
            float _Seed;
            float _RiseSpeed;
            float _Wind;
            float _Density;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(_Size.xy, 0.1);
                float source = max(_Shape.x, 0.2);
                float rise = max(_Shape.z, 0.5);
                // Units, from the middle of the source.
                float2 q = float2((i.uv.x - 0.5) * size.x - _Shape.w, i.uv.y * size.y - _Shape.y);
                float up = max(q.y, 0.0);
                float t = _Time.y;
                float seed = _Seed;

                // The plume leans downwind the higher it gets, and widens as it rises.
                float lean = _Wind * up / max(_RiseSpeed, 0.05) * 0.5;
                float width = source + up * 0.75;
                float across = (q.x - lean) / width;

                // Most of the quad is clear air: skip the noise there.
                float shapeRough = exp(-across * across * 2.2) * (1.0 - smoothstep(0.2, 1.0, up / rise));
                if (shapeRough < 0.003 || q.y < -0.6)
                {
                    return 0;
                }

                // Billows: noise carried up with the mist, scaled by height so each puff
                // swells as it climbs, warped by a second noise so it rolls.
                float grow = 1.0 / (1.0 + up * 0.35);
                float2 c = float2((q.x - lean) * 0.8 * grow, (q.y - t * _RiseSpeed) * 0.8 * grow) + seed;
                float2 warp = float2(WaterFbm(c * 0.7 + float2(t * 0.05, 3.1)), WaterFbm(c * 0.7 + float2(7.7, -t * 0.04)));
                float billow = WaterFbm(c + (warp - 0.5) * 1.6);
                float wisps = WaterFbm(c * 2.3 + warp * 0.8 + 11.0);

                // Shape: thick at the source, thinning with height and toward the sides,
                // a soft start just below the source.
                float shape = exp(-across * across * 2.2);
                shape *= smoothstep(-0.6, 0.15, q.y);
                shape *= 1.0 - smoothstep(0.2, 1.0, up / rise);
                float density = shape * smoothstep(0.28, 0.8, billow * 0.75 + wisps * 0.25 + shape * 0.22);
                density *= _Density;

                // Lit from above: the tops of the billows a little brighter.
                float3 rgb = _Color.rgb * lerp(0.88, 1.0, saturate(billow * 1.3 - 0.1));
                return float4(rgb, saturate(density) * _Color.a) * float4(i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}
