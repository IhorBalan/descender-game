// Falling water for Paradise's waterfalls (docs/design/paradise.md, section 5): the
// waterfall texture scrolled down in two layers at different speeds and scales, so the
// streaks never line up, with a soft fade at the sides, a brighter core and a lighter
// top where the water leaves the lip. Unlit and slightly above 1 in the core, so bloom
// gives it a sunlit sheen. Drawn on a SpriteRenderer stretched to the fall's size (not
// Tiled: the fades span the whole fall); Waterfall.cs sets _Tiles from the fall's height
// so the streaks keep their size. The texture must repeat vertically (Wrap Mode: Repeat).
Shader "Descender/Waterfall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Water Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.92, 0.97, 1, 0.9)
        _Speed ("Fall Speed (texture heights per second)", Float) = 1.2
        _Brightness ("Core Brightness (HDR)", Float) = 1.25
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _TopFade ("Top Fade", Range(0, 0.5)) = 0.06
        _BottomFade ("Bottom Fade (into mist or clouds)", Range(0, 0.6)) = 0.25
        _Tiles ("Texture Repeats Down The Fall", Float) = 4
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

            sampler2D _MainTex;
            float4 _Color;
            float _Speed;
            float _Brightness;
            float _EdgeSoftness;
            float _TopFade;
            float _BottomFade;
            float _Tiles;

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
                float2 local : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                // 0..1 across and down the whole fall, whatever the sprite's tiling.
                o.local = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                // Two layers of the same streaks, scrolled at different speeds and offsets.
                float2 a = float2(i.uv.x, i.uv.y * _Tiles + t);
                float2 b = float2(i.uv.x * 1.37 + 0.31, i.uv.y * _Tiles * 0.71 + t * 1.6);
                float4 s1 = tex2D(_MainTex, a);
                float4 s2 = tex2D(_MainTex, b);
                float4 c = lerp(s1, s2, 0.45);

                // Soft sides, a brighter core, fades at the lip and into the mist below.
                float side = smoothstep(0.0, _EdgeSoftness, i.local.x) * smoothstep(0.0, _EdgeSoftness, 1.0 - i.local.x);
                float core = 1.0 + (_Brightness - 1.0) * (1.0 - abs(i.local.x * 2.0 - 1.0));
                float top = smoothstep(1.0, 1.0 - max(_TopFade, 0.001), i.local.y);
                float bottom = smoothstep(0.0, max(_BottomFade, 0.001), i.local.y);

                float4 col = c * _Color * i.color;
                col.rgb *= core;
                col.a *= side * top * bottom;
                return col;
            }
            ENDCG
        }
    }
}
