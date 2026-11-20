// A tree of Tiles V2 whose crown moves in the wind (TilesV2Things.Crowned): URP's
// Sprite-Lit-Default (Universal2D, NormalsRendering and UniversalForward passes, the same
// includes and keywords) with its picture bent before it is read, every frame. Its trunk
// stands; above _Rest, more the higher, the tree leans with a wind that runs across the
// world in slow swells and now and then a gust, and its crown rustles, each part of it a
// little on its own. The bend is smooth, not stepped in texels or frames, so it moves at
// whatever rate the screen shows (60, 120 a second); _Snap brings back whole texels. The
// quad is grown by _Room texels on either side and at the top so a leaning crown is never
// cut off, and what lies outside the picture is clear. The normal-map pass bends the same
// way, so the light lines up with the colour.
Shader "Descender/Tiles V2/Crown Lit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Picture)]
        _TexelUnits("World Units A Texel", Float) = 0.03125
        _Room("Room Grown On Either Side And At The Top (texels)", Float) = 10
        _Rest("Height The Trunk Stands Still To (UV)", Range(0, 1)) = 0.22

        [Header(Wind)]
        _Sway("Lean In The Swells (texels at the top)", Float) = 3
        _Gust("Lean In A Gust (texels at the top)", Float) = 3.2
        _Rustle("The Crown's Rustle (texels)", Float) = 1.3
        _Speed("Speed Of The Wind", Float) = 1
        _Snap("Whole Texels (0 smooth .. 1 stepped)", Range(0, 1)) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
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
        ZWrite [_ZWrite]

        // One CBUFFER layout for all passes, as the SRP Batcher requires.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _TexelUnits;
            float _Room;
            float _Rest;
            float _Sway;
            float _Gust;
            float _Rustle;
            float _Speed;
            float _Snap;
        CBUFFER_END

        #define TILESV2_CROWN_OUTPUTS float crownX : TEXCOORD6;

        // The quad grown by _Room texels on either side and at the top (never at the foot):
        // its corner at `uv` moved out in the picture's own space, before it is flipped, and
        // its UV moved out the same, past the picture. `size` is the picture in texels.
        void TilesV2CrownGrow(inout float3 positionOS, inout float2 uv, float2 size)
        {
            float2 side = float2(uv.x < 0.5 ? -1.0 : 1.0, uv.y < 0.5 ? 0.0 : 1.0);
            positionOS.xy += side * _Room * _TexelUnits;
            uv += side * _Room / max(size, 1.0);
        }

        // How far the picture is moved at `uv` (texels, x and y): `worldX` is where this part
        // of it stands across the world, so the wind reaches it in its turn.
        float2 TilesV2CrownShift(float2 uv, float worldX, float2 size)
        {
            float up = saturate((uv.y - _Rest) / max(1.0 - _Rest, 1e-3));
            float bend = up * up * (3.0 - 2.0 * up);
            float t = _Time.y * _Speed;
            float along = worldX * 0.11;
            // Slow swells, two of them out of step, so the swing never quite repeats.
            float swell = 0.62 * sin(t * 0.83 - along) + 0.38 * sin(t * 1.37 - along * 1.6 + 1.1);
            // A gust now and then, sweeping through a stretch of the world at a time.
            float gust = pow(saturate(0.5 + 0.5 * sin(t * 0.23 - worldX * 0.05)), 4.0);
            float lean = (_Sway * swell + _Gust * gust) * bend;
            // The crown's rustle: its parts, some ten texels across, each moving a little on
            // its own, more the higher.
            float2 at = uv * size;
            float rustle = _Rustle * up;
            float rx = sin(t * 2.3 + at.x * 0.21 + at.y * 0.13) * sin(t * 1.6 + at.y * 0.19 - at.x * 0.07);
            float ry = sin(t * 2.9 + at.x * 0.17 - at.y * 0.23) * sin(t * 1.9 - at.x * 0.11 + at.y * 0.09);
            // A crown leaning far drops a touch, as a bent bough does.
            float2 shift = float2(lean + rustle * rx, rustle * ry * 0.5 - abs(lean) * 0.08);
            return lerp(shift, round(shift), _Snap);
        }

        // Where the picture is read from at `uv`, bent; its colour's alpha is cleared where
        // that falls outside the picture (its texture repeats).
        float2 TilesV2CrownUV(float2 uv, float worldX, float2 size, inout half4 color)
        {
            float2 read = uv - TilesV2CrownShift(uv, worldX, size) / max(size, 1.0);
            float2 inside = step(0.0, read) * step(read, 1.0);
            color.a *= inside.x * inside.y;
            return read;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
                TILESV2_CROWN_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            float2 PictureSize()
            {
                uint w, h;
                _MainTex.GetDimensions(w, h);
                return float2(w, h);
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                TilesV2CrownGrow(input.positionOS, input.uv, PictureSize());
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.crownX = TransformObjectToWorld(input.positionOS).x;
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                input.uv = TilesV2CrownUV(input.uv, input.crownX, PictureSize(), input.color);
                return CommonLitFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
                TILESV2_CROWN_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            float2 PictureSize()
            {
                uint w, h;
                _MainTex.GetDimensions(w, h);
                return float2(w, h);
            }

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                TilesV2CrownGrow(input.positionOS, input.uv, PictureSize());
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.crownX = TransformObjectToWorld(input.positionOS).x;
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // Setup instancing for SpriteFlip is used in NormalsRenderingShared
                SetUpSpriteInstanceProperties();

                input.uv = TilesV2CrownUV(input.uv, input.crownX, PictureSize(), input.color);
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

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
                TILESV2_CROWN_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            float2 PictureSize()
            {
                uint w, h;
                _MainTex.GetDimensions(w, h);
                return float2(w, h);
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                TilesV2CrownGrow(input.positionOS, input.uv, PictureSize());
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.crownX = TransformObjectToWorld(input.positionOS).x;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                input.uv = TilesV2CrownUV(input.uv, input.crownX, PictureSize(), input.color);
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
