// Ansel's cloak, woven with light: URP's Sprite-Lit-Default (Universal2D, NormalsRendering
// and UniversalForward passes, same includes and keywords), with light added after the
// scene's lighting so it glows even in shade:
//   - filaments: thin threads of light that run down the cloth from the collar to the hem,
//     drifting as the cloth falls (the drift is _Flow, which HeroLight advances faster as
//     he runs and dashes);
//   - the hem and the torn edges burn brightest, the light cooling from gold to pale blue
//     toward the hem;
//   - a surge: a band of light running from the collar down to the hem (_Surge, its place
//     from 0 at the collar to 1 at the hem), sent on every cut, dash and landing and, more
//     softly, every few seconds as he breathes;
//   - motes that twinkle in the weave.
// How bright all of it is, is _Energy (HeroLight: his speed, his moves, a hit that dims it).
// The cloth itself is drawn a little deeper (_BaseTint) so the light has something to stand
// on. UV y is 0 at the hem and 1 at the collar.
Shader "Descender/Hero Cloak Light"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        [Header(Cloth)]
        _BaseTint("Cloth Tint", Color) = (0.72, 0.66, 0.72, 1)

        [Header(Light)]
        _LightColor("Light (collar)", Color) = (1, 0.74, 0.32, 1)
        _TipColor("Light (hem)", Color) = (0.62, 0.9, 1, 1)
        _Filaments("Filaments", Float) = 1.6
        _Hem("Hem Glow", Float) = 0.5
        _Edge("Torn Edge Glow", Float) = 0.9
        _EdgeWidth("Edge Width (texels)", Float) = 5
        _Sparkle("Sparkle", Float) = 1.4
        _Reach("How Far Up The Light Reaches (0 hem only, 1 all of it)", Range(0, 1)) = 0.9

        [Header(Driven by HeroLight)]
        _Energy("Energy", Float) = 1
        _Flow("Flow", Float) = 0
        _Surge("Surge Place (collar 0, hem 1, off below 0)", Float) = -1
        _SurgePower("Surge Power", Float) = 0
        _Texel("Texel (1/w, 1/h, w, h)", Vector) = (0.004, 0.004, 256, 256)

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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _BaseTint;
            half4 _LightColor;
            half4 _TipColor;
            float _Filaments;
            float _Hem;
            float _Edge;
            float _EdgeWidth;
            float _Sparkle;
            float _Reach;
            float _Energy;
            float _Flow;
            float _Surge;
            float _SurgePower;
            float4 _Texel;
        CBUFFER_END

        float CloakHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float CloakNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(CloakHash(i), CloakHash(i + float2(1, 0)), u.x),
                lerp(CloakHash(i + float2(0, 1)), CloakHash(i + float2(1, 1)), u.x),
                u.y
            );
        }

        // The light woven into the cloth at uv; alpha is the cloth there, minEdge the least
        // cloth found a few texels around (so the torn edges can be told).
        half3 CloakLight(float2 uv, half alpha, half minEdge)
        {
            float down = 1.0 - uv.y; // 0 at the collar, 1 at the hem
            // Texel space, so the weave is round whatever the texture's shape.
            float2 p = uv * _Texel.zw / 160.0;

            // Threads: ridges of a noise stretched down the fall of the cloth, warped a little
            // so they wander, scrolling down as the cloth flows.
            float2 q = float2(p.x * 3.2, p.y * 0.9 + _Flow);
            q.x += (CloakNoise(q * 0.6 + float2(0, _Flow * 0.35)) - 0.5) * 1.6;
            float n = CloakNoise(q) * 0.65 + CloakNoise(q * 2.3 + 7.1) * 0.35;
            float ridge = 1.0 - abs(n * 2.0 - 1.0);
            float threads = pow(ridge, 9.0);

            float reach = smoothstep(1.0 - _Reach - 0.15, 1.0, down);
            float hem = pow(smoothstep(0.45, 1.0, down), 2.0);
            float edge = saturate((alpha - minEdge) * 1.6) * lerp(0.35, 1.0, down);

            float surge = 0;
            if (_Surge >= 0)
            {
                float d = (down - _Surge) / 0.09;
                surge = exp(-d * d) * _SurgePower * (0.3 + threads * 1.3 + edge * 0.6);
            }

            // Motes: a few cells of a grid wink on and off.
            float2 cell = floor(p * 9.0);
            float h = CloakHash(cell);
            float2 inCell = frac(p * 9.0) - 0.5;
            float wink = pow(saturate(sin(_Time.y * (1.5 + h * 3.0) + h * 40.0)), 16.0);
            float sparkle = step(0.84, h) * wink * saturate(1.0 - length(inCell) * 3.2) * reach;

            float glow =
                threads * _Filaments * reach
                + hem * _Hem
                + edge * _Edge
                + sparkle * _Sparkle
                + surge;
            half3 color = lerp(_LightColor.rgb, _TipColor.rgb, saturate(down * down * 1.2));
            return color * glow * _Energy * alpha;
        }

        half CloakMinEdge(TEXTURE2D_PARAM(tex, samp), float2 uv)
        {
            half least = 1;
            [unroll]
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (6.2831853 / 8.0);
                least = min(least, SAMPLE_TEXTURE2D(tex, samp, uv + float2(cos(angle), sin(angle)) * _EdgeWidth * _Texel.xy).a);
            }
            return least;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

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
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonLitFragment(input, input.color * half4(_BaseTint.rgb, 1));
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half least = CloakMinEdge(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv);
                color.rgb += CloakLight(input.uv, alpha, least) * input.color.a;
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
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                SetUpSpriteInstanceProperties();
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
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color * half4(_BaseTint.rgb, 1));
                half least = CloakMinEdge(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv);
                color.rgb += CloakLight(input.uv, color.a / max(input.color.a, 1e-4), least) * input.color.a;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
