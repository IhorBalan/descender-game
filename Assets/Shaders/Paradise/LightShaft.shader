// A soft shaft of sun through the canopy (Paradise zones 2 and 3): unlit and added on
// top of what is under it, like Sprite Glow. Made for a SpriteRenderer (UV.y up the
// shaft; tint with the renderer colour; use a full-rect sprite, e.g. a white square,
// for the procedural shape). The shape is a procedural soft beam that widens toward the
// bottom, or the sprite itself when Use Ray Texture is on (greyscale times alpha). Streaks
// of hash noise drift down it for shimmer, so no noise texture is needed. Fades out at
// the top and bottom so the quad's edges never show. Colour is HDR: push it above 1 and
// the bloom picks the shaft up.
Shader "Descender/Light Shaft"
{
    Properties
    {
        [PerRendererData] _MainTex("Ray Texture (optional)", 2D) = "white" {}
        [ToggleUI] _UseRayTexture("Use Ray Texture (else procedural)", Float) = 0

        [HDR] _Color("Color", Color) = (1, 0.95, 0.8, 1)
        _Intensity("Intensity", Range(0, 4)) = 0.6

        [Header(Shape)]
        _TopWidth("Top Width (fraction of bottom)", Range(0.05, 1.5)) = 0.6
        _EdgeSoftness("Edge Softness", Range(0.01, 1)) = 0.8
        _FadeTop("Fade Top (fraction of height)", Range(0.001, 1)) = 0.25
        _FadeBottom("Fade Bottom (fraction of height)", Range(0.001, 1)) = 0.45

        [Header(Shimmer)]
        _ScrollSpeed("Scroll Speed (noise cells per second)", Float) = 0.25
        _NoiseScale("Noise Scale (across, along)", Vector) = (5, 1.5, 0, 0)
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.55
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0.15
        _PulseSpeed("Pulse Speed (rad per second)", Float) = 0.7
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // Additive colour; leave the target's alpha alone.
        Blend One One, Zero One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex ShaftVertex
            #pragma fragment ShaftFragment
            #pragma multi_compile_instancing

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _UseRayTexture;
                half4 _Color;
                half _Intensity;
                float _TopWidth;
                float _EdgeSoftness;
                float _FadeTop;
                float _FadeBottom;
                float _ScrollSpeed;
                float4 _NoiseScale;
                float _NoiseStrength;
                float _PulseAmount;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                // World x: a per-shaft seed that survives sprite batching, so two
                // shafts side by side shimmer differently.
                float seed : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float ShaftHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ShaftValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = ShaftHash(i);
                float b = ShaftHash(i + float2(1.0, 0.0));
                float c = ShaftHash(i + float2(0.0, 1.0));
                float d = ShaftHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings ShaftVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.seed = TransformObjectToWorld(input.positionOS).x;
                // SpriteRenderer.color arrives in unity_SpriteColor (SRP Batcher/instancing), as in Sprite-Unlit-Default.
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 ShaftFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Procedural beam: soft across, narrower at the top so it spreads as it falls.
                float width = max(lerp(1.0, _TopWidth, uv.y), 0.001);
                float x = (uv.x - 0.5) * 2.0 / width;
                float across = saturate(1.0 - abs(x));
                float procedural = smoothstep(0.0, max(_EdgeSoftness, 0.001), across);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float fromTexture = dot(tex.rgb, half3(0.3333, 0.3333, 0.3333)) * tex.a;
                float shape = lerp(procedural, fromTexture, _UseRayTexture);

                float fade = smoothstep(0.0, max(_FadeBottom, 0.001), uv.y)
                           * smoothstep(0.0, max(_FadeTop, 0.001), 1.0 - uv.y);

                // Streaks: stretched along the shaft, drifting down (+t moves features down).
                float t = _Time.y * _ScrollSpeed;
                float2 p = float2(x * _NoiseScale.x + input.seed * 0.37, uv.y * _NoiseScale.y + t);
                float n = ShaftValueNoise(p) * 0.65
                        + ShaftValueNoise(p * 2.03 + float2(17.1, t * 0.6)) * 0.35;
                float shimmer = lerp(1.0, saturate(n * 1.6), _NoiseStrength);
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed + input.seed * 1.7);

                float amount = shape * fade * shimmer * pulse * _Intensity * _Color.a * input.color.a;
                return half4(_Color.rgb * input.color.rgb * amount, 0.0);
            }
            ENDHLSL
        }
    }
}
