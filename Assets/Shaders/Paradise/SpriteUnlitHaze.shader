// Far parallax layers of Paradise (silhouettes flattened into the sky): URP's
// Sprite-Unlit-Default (same single pass, includes and keywords) with distance haze and
// an optional slow sway from ParadiseSway.hlsl. Unlit on purpose: 2D lights should not
// pick out shapes that are meant to sit in the haze, and it saves the light lookups on
// the biggest sprites on screen. Sway and push are off by default (amount 0).
Shader "Descender/Sprite Unlit Haze"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Distance haze)]
        _HazeColor("Haze Color", Color) = (1, 1, 1, 1)
        _HazeAmount("Haze Amount", Range(0, 1)) = 0.5

        [Header(Wind sway off by default)]
        _SwayAmount("Bend Per Unit Wind (units at the tip, or radians when hanging)", Float) = 0
        _SwaySpeed("Natural Frequency (rad per second)", Float) = 0.6
        _SwayScale("Variation Between Plants (0-1)", Float) = 0.3
        [ToggleUI] _SwayUseObjectY("Height From Object Y (else UV.y)", Float) = 0
        _SwayHeight("Height In Units (object Y mode)", Float) = 1
        [ToggleUI] _SwayHang("Hangs From Its Pivot (pendulum)", Float) = 0
        _SwayFlutter("Flutter (leaf tips, or ripple radians when hanging)", Float) = 0
        _SwayFlutterSpeed("Flutter Speed (times the natural frequency)", Float) = 2.5
        _SwayLag("Swing Lag Down The Length (rad per unit, hanging)", Float) = 0.4

        [Header(Hero push off by default)]
        _PushRadius("Push Radius (units)", Float) = 1.5
        _PushStrength("Push Strength (units at the tip)", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap("Pixel snap", Float) = 0
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

        Pass
        {
            HLSLPROGRAM
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

                // Skip the round trip through world space when the layer is still,
                // which is the default and most far layers.
                float4 sway0 = 0.0;
                float4 sway1 = 0.0;
                float sway2 = 0.0;
                UNITY_BRANCH
                if (_SwayAmount != 0.0 || _PushStrength != 0.0)
                {
                    UNITY_SETUP_INSTANCE_ID(input); // the object matrix is per instance when instanced
                    ParadiseSwayVertex(input.positionOS, input.uv, sway0, sway1, sway2);
                }

                Varyings o = CommonUnlitVertex(input);
                PARADISE_SWAY_COPY(o);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // The derivatives are taken on every pixel; the branch is uniform per material.
                float2 swayedUV = ParadiseSwayUV(input.uv, input.sway0, input.sway1, input.sway2);
                if (_SwayAmount != 0.0 || _PushStrength != 0.0)
                {
                    input.uv = swayedUV;
                }
                half4 color = CommonUnlitFragment(input, input.color);
                color.rgb = ParadiseHaze(color.rgb);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
