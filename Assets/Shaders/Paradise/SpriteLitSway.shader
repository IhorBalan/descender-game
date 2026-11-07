// Paradise plants and parallax layers that take 2D light: URP's Sprite-Lit-Default
// (Universal2D, NormalsRendering and UniversalForward passes, same includes and
// keywords) with the garden's wind, the hero's push and distance haze added in
// ParadiseSway.hlsl. In every pass the sway moves the vertices before the URP helpers
// run and bends the texture lookup between them (the stalk's curve, the strands'
// ripple), so the normal-map pass lines up with the lit colour. Haze is applied after
// lighting: it stands for light scattered in the air between us and the layer.
Shader "Descender/Sprite Lit Sway"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Wind sway)]
        _SwayAmount("Bend Per Unit Wind (units at the tip, or radians when hanging)", Float) = 0.12
        _SwaySpeed("Natural Frequency (rad per second)", Float) = 4
        _SwayScale("Variation Between Plants (0-1)", Float) = 0.7
        [ToggleUI] _SwayUseObjectY("Height From Object Y (else UV.y)", Float) = 0
        _SwayHeight("Height In Units (object Y mode)", Float) = 1
        [ToggleUI] _SwayHang("Hangs From Its Pivot (pendulum)", Float) = 0
        _SwayFlutter("Flutter (leaf tips, or ripple radians when hanging)", Float) = 0.012
        _SwayFlutterSpeed("Flutter Speed (times the natural frequency)", Float) = 2.5
        _SwayLag("Swing Lag Down The Length (rad per unit, hanging)", Float) = 0.4

        [Header(Hero push)]
        _PushRadius("Push Radius (units)", Float) = 1.5
        _PushStrength("Push Strength (units at the tip)", Float) = 0.35

        [Header(Distance haze)]
        _HazeColor("Haze Color", Color) = (1, 1, 1, 1)
        _HazeAmount("Haze Amount", Range(0, 1)) = 0

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
            float _SwayAmount;
            float _SwaySpeed;
            float _SwayScale;
            float _SwayUseObjectY;
            float _SwayHeight;
            float _SwayHang;
            float _SwayFlutter;
            float _SwayFlutterSpeed;
            float _SwayLag;
            float _PushRadius;
            float _PushStrength;
            half4 _HazeColor;
            half _HazeAmount;
        CBUFFER_END

        #include "Assets/Shaders/Paradise/ParadiseSway.hlsl"
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
                PARADISE_SWAY_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                UNITY_SETUP_INSTANCE_ID(input); // the object matrix is per instance when instanced
                float4 sway0, sway1;
                float sway2;
                ParadiseSwayVertex(input.positionOS, input.uv, sway0, sway1, sway2);

                Varyings o = CommonLitVertex(input);
                PARADISE_SWAY_COPY(o);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                input.uv = ParadiseSwayUV(input.uv, input.sway0, input.sway1, input.sway2);
                half4 color = CommonLitFragment(input, input.color);
                color.rgb = ParadiseHaze(color.rgb);
                return color;
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
                PARADISE_SWAY_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                UNITY_SETUP_INSTANCE_ID(input); // the object matrix is per instance when instanced
                float4 sway0, sway1;
                float sway2;
                ParadiseSwayVertex(input.positionOS, input.uv, sway0, sway1, sway2);

                Varyings o = CommonNormalsVertex(input);
                PARADISE_SWAY_COPY(o);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // Setup instancing for SpriteFlip is used in NormalsRenderingShared
                SetUpSpriteInstanceProperties();

                input.uv = ParadiseSwayUV(input.uv, input.sway0, input.sway1, input.sway2);
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
                PARADISE_SWAY_OUTPUTS
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
                UNITY_SETUP_INSTANCE_ID(input); // the object matrix is per instance when instanced
                float4 sway0, sway1;
                float sway2;
                ParadiseSwayVertex(input.positionOS, input.uv, sway0, sway1, sway2);

                Varyings o = CommonUnlitVertex(input);
                PARADISE_SWAY_COPY(o);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                input.uv = ParadiseSwayUV(input.uv, input.sway0, input.sway1, input.sway2);
                half4 color = CommonUnlitFragment(input, input.color);
                color.rgb = ParadiseHaze(color.rgb);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
