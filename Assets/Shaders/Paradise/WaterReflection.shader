// The surface of a pool at the foot of a Paradise waterfall: a horizontal sheet of water seen
// at a grazing angle, so a thin band hanging below the waterline, straight along the far edge
// and curved along the near one. It mirrors what stands above it from the frame URP captures
// after the Default sorting layer (_CameraSortingLayerTexture), which is why it has to be
// drawn on a later layer. Rings spread from where the fall lands and bend the reflection; far
// away the sheet is nearly a mirror and nearer the viewer it shows more of its own colour.
//
// On a SpriteRenderer whose top edge is the waterline (WaterQuad.cs sets _Size and _Seed;
// _Shape.xy is the pool's reach left and right of the quad's centre, in units).
Shader "Descender/Water Reflection"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused (the quad's sprite)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WaterColor ("Water", Color) = (0.2, 0.42, 0.52, 1)
        _FoamColor ("Foam", Color) = (0.93, 0.97, 1, 1)
        _Size ("Quad Size (units, per pool)", Vector) = (8, 0.7, 0, 0)
        _Shape ("Reach Left, Reach Right (units)", Vector) = (4, 4, 0, 0)
        _Seed ("Seed (per pool)", Float) = 0
        _Stretch ("Units Mirrored Per Unit Of Surface", Float) = 3.5
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

            sampler2D _CameraSortingLayerTexture;
            float4 _Color;
            float4 _WaterColor;
            float4 _FoamColor;
            float4 _Size;
            float4 _Shape;
            float _Seed;
            float _Stretch;

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
                float4 mirror : TEXCOORD1;
                float4 unit : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float below = (1.0 - v.texcoord.y) * _Size.y;
                // A point this far below the waterline mirrors one _Stretch times as far above it.
                float3 mirrored = float3(world.x, world.y + below * (1.0 + _Stretch), world.z);
                o.mirror = ComputeScreenPos(UnityWorldToClipPos(float4(mirrored, 1.0)));
                // A unit along x, in screen coordinates, for the ripples.
                o.unit = ComputeScreenPos(UnityWorldToClipPos(float4(mirrored + float3(1.0, 0.0, 0.0), 1.0))) - o.mirror;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(_Size.xy, 0.1);
                float x = (i.uv.x - 0.5) * size.x;      // units from the fall
                float below = (1.0 - i.uv.y) * size.y;  // units below the waterline
                float near = saturate(below / size.y);  // 0 at the far edge, 1 at the nearest
                float t = _Time.y;
                float seed = _Seed;

                // The outline: an ellipse cut by the waterline, wandering a little along its near edge.
                float reach = max(x < 0.0 ? _Shape.x : _Shape.y, 0.2);
                float across = saturate(abs(x) / reach);
                float wander = 0.82 + 0.18 * WaterFbm(float2(x * 0.7 + seed, seed * 0.31));
                float edge = size.y * pow(saturate(1.0 - across * across), 0.7) * wander;
                float inside = smoothstep(edge, edge - 0.05, below) * smoothstep(0.0, 0.025, below);
                if (inside <= 0.0)
                {
                    return 0;
                }

                // Foreshortened, so a ring of radius r reaches r / 4.5 toward the viewer.
                float r = length(float2(x, below * 4.5));
                float rings = 0.0;
                float crest = 0.0;
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    float phase = frac(t * 0.3 + k / 3.0 + seed * 0.137);
                    float d = (r - (0.5 + phase * 5.0)) / (0.16 + phase * 0.3);
                    float ring = exp(-d * d) * (1.0 - phase) * smoothstep(0.0, 0.1, phase);
                    rings += ring * d;
                    crest += ring;
                }

                // The sheet's own small waves, finer toward the far edge where they crowd together.
                float2 wave = float2(x * 2.2 + seed, below * 16.0 - t * 0.9);
                float waves = WaterFbm(wave) - 0.5;
                float swing = waves * (0.05 + 0.2 * near) + rings * 0.12;
                float2 uv = (i.mirror.xy + i.unit.xy * swing) / i.mirror.w;
                float3 mirrored = tex2D(_CameraSortingLayerTexture, uv).rgb;

                // Grazing light mirrors almost wholly; nearer the viewer the water shows through.
                float3 rgb = lerp(mirrored, _WaterColor.rgb, lerp(0.14, 0.5, near));
                float light = smoothstep(0.1, 0.34, waves + 0.12 * crest);
                rgb += light * 0.1;
                float broken = smoothstep(0.34, 0.7, WaterNoise(float2(x * 2.4 + seed, r * 0.6 - t * 0.3)));
                rgb = lerp(rgb, _FoamColor.rgb, saturate(crest * broken * 0.75));

                // A bright line where it meets the far bank, a shaded rim at its near edge.
                float waterline = exp(-pow((below - 0.03) / 0.03, 2.0)) * smoothstep(1.0, 0.75, across);
                rgb = lerp(rgb, _FoamColor.rgb, waterline * 0.32);
                rgb *= 1.0 - 0.3 * smoothstep(edge - 0.1, edge - 0.015, below);
                float glint = WaterGlint(float2(x * 4.0 + seed, below * 22.0), t, 0.2);
                rgb += glint * 0.45;

                return float4(rgb, inside * lerp(1.0, 0.86, near)) * _Color * i.color;
            }
            ENDCG
        }
    }
}
