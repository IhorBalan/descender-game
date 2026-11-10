// Ansel's outline: drawn by a copy of his body (HeroLight keeps it posed as he is) that sits
// just behind every part of him, so only what lies outside his figure shows: a thin dark ink
// line hugging the edge, which reads on the pale stone and sky of Paradise, and beyond it a
// soft halo of light, which reads on dark ground. Unlit: the lights must not pick it out.
// The parts are painted with empty pixels around them (tools/hero_ansel/fit_parts.py, PAD)
// so there is room for both; widths are in texels of the part, so they hold at any zoom.
// _Texel (1/width, 1/height of the part's texture) comes per renderer from HeroLight.
Shader "Descender/Hero Silhouette"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _InkColor("Ink", Color) = (0.13, 0.08, 0.07, 0.88)
        _InkWidth("Ink Width (texels)", Float) = 3
        _InkPixels("Ink Width At Least (screen pixels)", Float) = 1.3
        _HaloColor("Halo", Color) = (1, 0.86, 0.56, 0.3)
        _HaloWidth("Halo Width (texels)", Float) = 16
        _HaloPixels("Halo Width At Least (screen pixels)", Float) = 5
        _Room("Room Outside The Paint (texels, under fit_parts.py PAD)", Float) = 36
        _Glow("Halo Strength (HeroLight)", Float) = 1
        _Texel("Texel (1/w, 1/h, w, h; HeroLight)", Vector) = (0.004, 0.004, 256, 256)

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _InkColor;
                float _InkWidth;
                float _InkPixels;
                half4 _HaloColor;
                float _HaloWidth;
                float _HaloPixels;
                float _Room;
                float _Glow;
                float4 _Texel;
            CBUFFER_END

            #pragma vertex SilhouetteVertex
            #pragma fragment SilhouetteFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            Varyings SilhouetteVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half Coverage(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            // The most of the figure found on a ring of twelve points around uv; radius in uv.
            half RingMax(float2 uv, float2 radius)
            {
                half most = 0;
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * (6.2831853 / 12.0);
                    most = max(most, Coverage(uv + float2(cos(angle), sin(angle)) * radius));
                }
                return most;
            }

            // A width in uv: so many texels, but never thinner than so many pixels on screen
            // (when he is drawn small), and never past the empty room around the paint.
            float2 Width(float texels, float pixels, float2 uvPerPixel)
            {
                float2 width = max(texels * _Texel.xy, pixels * uvPerPixel);
                return min(width, _Room * _Texel.xy);
            }

            half4 SilhouetteFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half inside = Coverage(uv);
                float2 uvPerPixel = fwidth(uv);
                half ink = max(inside, RingMax(uv, Width(_InkWidth, _InkPixels, uvPerPixel)));
                // The halo: how much of the figure is within reach, falling off with distance.
                float2 reach = Width(_HaloWidth, _HaloPixels, uvPerPixel);
                half halo =
                    RingMax(uv, reach * 0.3) * 0.4
                    + RingMax(uv, reach * 0.55) * 0.3
                    + RingMax(uv, reach * 0.8) * 0.2
                    + RingMax(uv, reach) * 0.1;
                halo = halo * halo;

                half inkAlpha = ink * _InkColor.a;
                half haloAlpha = halo * _HaloColor.a * _Glow;
                half alpha = inkAlpha + haloAlpha * (1 - inkAlpha);
                half3 rgb = (_InkColor.rgb * inkAlpha + _HaloColor.rgb * haloAlpha * (1 - inkAlpha)) / max(alpha, 1e-4);
                return half4(rgb, saturate(alpha) * input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
