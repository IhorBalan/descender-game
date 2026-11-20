// What grows on the floors of Tiles V2 and bends (TilesV2Life's brush): URP's
// Sprite-Lit-Default (Universal2D, NormalsRendering and UniversalForward passes, the same
// includes and keywords) with its picture bent before it is read. Its foot stays where it
// grows; above it, more the higher, it leans with a wind that runs across the world in
// swells and gusts, each blade stirring on its own, and with the hero's push (_Push, set
// on each renderer by TilesV2Life.Bend). A tip that leans far sinks a little, as a bent
// blade does. The bend moves whole texels, so the picture stays crisp pixel art, and the
// normal-map pass bends the same way, so the light lines up with the colour.
Shader "Descender/Tiles V2/Brush Lit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Picture)]
        _Size("Picture Size In Texels (x, y)", Vector) = (108, 60, 0, 0)
        _Foot("Foot (UV height it grows from)", Range(0, 1)) = 0.1

        [Header(Wind)]
        _Wind("Lean In The Wind (texels at the tip)", Float) = 1.4
        _Gust("Lean In A Gust (texels at the tip)", Float) = 2.2
        _Flutter("Each Blade's Stir (texels at the tip)", Float) = 0.8

        [Header(Hero push)]
        _Push("Push (-1 .. 1, set per renderer)", Float) = 0
        _PushReach("Lean At A Full Push (texels at the tip)", Float) = 9
        _Sink("How Much A Leaning Tip Sinks (0 .. 1)", Range(0, 1)) = 0.35

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
            float4 _Size;
            float _Foot;
            float _Wind;
            float _Gust;
            float _Flutter;
            float _Push;
            float _PushReach;
            float _Sink;
        CBUFFER_END

        #define TILESV2_BRUSH_OUTPUTS float brushX : TEXCOORD6;

        // Where the picture is read from at `uv`, bent: `worldX` is where this part of it
        // stands across the world, so the wind reaches it in its turn.
        float2 TilesV2BrushUV(float2 uv, float worldX)
        {
            float2 size = max(_Size.xy, 1.0);
            // The texel this fragment shows, so a texel moves as one.
            float2 texel = (floor(uv * size) + 0.5) / size;
            float up = saturate((texel.y - _Foot) / max(1.0 - _Foot, 1e-3));
            float bend = pow(up, 1.6);
            float t = _Time.y;
            // A swell that runs across the world downwind, mostly leaning it over, now and then
            // letting it swing back a little.
            float swell = 0.35 + 0.65 * sin(t * 1.25 - worldX * 0.45);
            // A gust now and then, sweeping through a stretch at a time.
            float gust = pow(saturate(0.5 + 0.5 * sin(t * 0.29 - worldX * 0.07)), 4.0);
            // Each blade's own stir: by the column it stands in, quick and small.
            float column = floor(worldX * 32.0);
            float flutter = sin(t * 5.1 + column * 1.7) * 0.6 + sin(t * 8.3 + column * 2.9) * 0.4;
            float lean = _Wind * swell + _Gust * gust + _Flutter * flutter * up + _Push * _PushReach;
            float shift = round(lean * bend);
            float sink = round(abs(lean * bend) * _Sink * up);
            return float2(uv.x - shift / size.x, uv.y + sink / size.y);
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
                TILESV2_BRUSH_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.brushX = TransformObjectToWorld(input.positionOS).x;
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                input.uv = TilesV2BrushUV(input.uv, input.brushX);
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
                TILESV2_BRUSH_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.brushX = TransformObjectToWorld(input.positionOS).x;
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // Setup instancing for SpriteFlip is used in NormalsRenderingShared
                SetUpSpriteInstanceProperties();

                input.uv = TilesV2BrushUV(input.uv, input.brushX);
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
                TILESV2_BRUSH_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.brushX = TransformObjectToWorld(input.positionOS).x;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                input.uv = TilesV2BrushUV(input.uv, input.brushX);
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
