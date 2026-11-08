// Particles off a Paradise waterfall (WaterfallSpray.cs): splash droplets as soft round
// dots (_Round 1, no texture) stretched along their flight by the particle renderer, and
// mist puffs from a soft texture (_Round 0). Vertex colour carries the particle's colour
// and fade. Unlit, alpha blended, never above white.
Shader "Descender/Water Spray"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Round ("Round Dot (1) Or Texture (0)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
            float4 _MainTex_ST;
            float4 _Color;
            float _Round;

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
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float dot_ = saturate(1.0 - dot(c, c));
                dot_ *= dot_;
                float4 tex = tex2D(_MainTex, i.uv);
                float4 col = i.color * tex;
                col.a *= lerp(1.0, dot_, _Round);
                return col;
            }
            ENDCG
        }
    }
}
