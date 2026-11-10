using Descender.Fx;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.Enemies
{
    /// <summary>
    /// The lantern fruit's burst, beyond its painted frames: a white-gold flash about as big
    /// as the burst that hurts, a faint shockwave ring sweeping out past it, a spray of
    /// glowing motes that drift up, peel shards and seeds thrown out and falling, and a flare
    /// of light that leaves an ember glow on the ground. Made once per fruit and played
    /// where it bursts; everything it draws comes from code, nothing from an asset.
    /// </summary>
    public sealed class LanternFruitBurst : MonoBehaviour
    {
        /// <summary>Seconds the flash and the ring take, and the flare and the ember after it.</summary>
        public const float FlashTime = 0.3f;
        public const float RingTime = 0.5f;
        public const float FlareTime = 0.55f;
        public const float EmberTime = 1.6f;

        /// <summary>The flash grows to this many units across; about the hurting burst's width.</summary>
        public const float FlashSize = 3.2f;

        /// <summary>A column of light shoots up this many units from where it burst, in this many seconds.</summary>
        public const float PillarHeight = 5f;
        public const float PillarTime = 0.45f;

        /// <summary>The ring sweeps out to this many units across.</summary>
        public const float RingSize = 4.5f;

        public const float FlareIntensity = 5f;
        public const float FlareRadius = 6f;
        public const float EmberIntensity = 0.9f;
        public const float EmberRadius = 2.2f;

        public const int Motes = 70;
        public const int Shards = 18;
        public const int Seeds = 8;

        public static readonly Color FlashColor = new Color(1f, 0.97f, 0.85f);
        public static readonly Color RingColor = new Color(1f, 0.88f, 0.55f);
        public static readonly Color MoteColor = new Color(1f, 0.9f, 0.55f);
        public static readonly Color ShardColor = new Color(0.98f, 0.9f, 0.72f);
        public static readonly Color SeedColor = new Color(0.95f, 0.7f, 0.3f);

        private static Texture2D discTexture;
        private static Texture2D ringTexture;
        private static Texture2D shardTexture;
        private static Sprite discSprite;
        private static Texture2D flashTexture;
        private static Sprite flashSprite;
        private static Texture2D pillarTexture;
        private static Sprite pillarSprite;
        private static Sprite ringSprite;
        private static Material moteMaterial;
        private static Material shardMaterial;

        private SpriteRenderer flash;
        private SpriteRenderer ring;
        private SpriteRenderer pillar;
        private Light2D flare;
        private ParticleSystem motes;
        private ParticleSystem shards;
        private ParticleSystem seeds;
        private float since = -1f;

        /// <summary>Whether any part of it is still showing.</summary>
        public bool IsPlaying => since >= 0f || Particles > 0;

        /// <summary>The motes, shards and seeds in the air now.</summary>
        public int Particles =>
            (motes != null ? motes.particleCount : 0)
            + (shards != null ? shards.particleCount : 0)
            + (seeds != null ? seeds.particleCount : 0);

        /// <summary>Whether the flash is showing.</summary>
        public bool Flashing => flash != null && flash.enabled;

        /// <summary>Makes its parts under `parent`; call once.</summary>
        public static LanternFruitBurst Make(Transform parent)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(parent, false);
            var burst = go.AddComponent<LanternFruitBurst>();
            burst.Build();
            return burst;
        }

        private void Build()
        {
            Prepare();
            flash = MakeSprite(flashSprite, "Flash", 6, 0.5f);
            // Behind the ground it lands on, so it reads as a shockwave along the floor.
            ring = MakeSprite(ringSprite, "Ring", -1, 0.05f);
            pillar = MakeSprite(pillarSprite, "Pillar", 5, 0f);
            var lightObject = new GameObject("Flare");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            flare = lightObject.AddComponent<Light2D>();
            flare.lightType = Light2D.LightType.Point;
            flare.color = LanternFruit.Glow;
            flare.pointLightInnerRadius = 0f;
            flare.pointLightOuterRadius = FlareRadius;
            flare.falloffIntensity = 0.55f;
            flare.shadowsEnabled = false;
            flare.enabled = false;

            // Motes: sprayed up, slowed by the air, drifting and flickering as they fade.
            motes = MakeSystem("Motes", moteMaterial, 7);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(MoteColor, Color.white);
            main.gravityModifier = -0.08f;
            Cone(motes, 75f, 0.3f);
            var drag = motes.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 1.2f;
            drag.dampen = 0.12f;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.2f;
            Fade(motes, 0.6f);

            // Peel shards: thrown out spinning, and falling.
            shards = MakeSystem("Shards", shardMaterial, 6);
            main = shards.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = ShardColor;
            main.gravityModifier = 1.6f;
            Cone(shards, 65f, 0.25f);
            var spin = shards.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            Fade(shards, 0.0f);
            Land(shards);

            // Seeds: small and heavy, falling fast.
            seeds = MakeSystem("Seeds", moteMaterial, 6);
            main = seeds.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
            main.startColor = SeedColor;
            main.gravityModifier = 2.4f;
            Cone(seeds, 55f, 0.2f);
            Fade(seeds, 0.2f);
            Land(seeds);
            Hide();
        }

        /// <summary>Plays it with its foot at `ground`.</summary>
        public void Play(Vector2 ground)
        {
            transform.position = ground;
            since = Time.time;
            flash.enabled = true;
            ring.enabled = true;
            pillar.enabled = true;
            flare.enabled = true;
            motes.Emit(Motes);
            shards.Emit(Shards);
            seeds.Emit(Seeds);
            Animate(0f);
        }

        private void Update()
        {
            if (since >= 0f)
            {
                Animate(Time.time - since);
            }
        }

        private void Animate(float t)
        {
            // The flash: a quick swell to the burst's size, gone as fast.
            var f = Mathf.Clamp01(t / FlashTime);
            flash.enabled = f < 1f;
            // Up to its size in the first third of its time, then fading as it keeps swelling.
            var swell = OutQuad(Mathf.Clamp01(f * 3f));
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, FlashSize, swell) * (1f + 0.15f * f);
            flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, 1f - f * f * f);

            // The ring sweeps out past it, fainter as it goes, a little flattened on the ground.
            var r = Mathf.Clamp01(t / RingTime);
            ring.enabled = r < 1f;
            var across = Mathf.Lerp(0.3f, RingSize, OutCubic(r));
            ring.transform.localScale = new Vector3(across, across * 0.45f, 1f);
            ring.color = new Color(RingColor.r, RingColor.g, RingColor.b, 0.9f * (1f - r));

            // The column of light: up fast from the ground, thinning and fading as it goes.
            var p = Mathf.Clamp01(t / PillarTime);
            pillar.enabled = p < 1f;
            pillar.transform.localScale = new Vector3(
                Mathf.Lerp(3f, 1.4f, p),
                Mathf.Lerp(0.5f, PillarHeight, OutCubic(p)),
                1f
            );
            pillar.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, (1f - p));

            // The flare, then an ember glow that dies slowly on the ground.
            if (t < FlareTime)
            {
                var k = 1f - t / FlareTime;
                flare.intensity = Mathf.Max(FlareIntensity * k * k, EmberIntensity);
                flare.pointLightOuterRadius = Mathf.Lerp(EmberRadius, FlareRadius, k);
            }
            else if (t < FlareTime + EmberTime)
            {
                var e = (t - FlareTime) / EmberTime;
                flare.intensity = EmberIntensity * (1f - e) * (1f - e);
                flare.pointLightOuterRadius = EmberRadius;
            }
            else
            {
                Hide();
            }
        }

        private void Hide()
        {
            since = -1f;
            flash.enabled = false;
            ring.enabled = false;
            pillar.enabled = false;
            flare.enabled = false;
        }

        private SpriteRenderer MakeSprite(Sprite sprite, string name, int order, float above)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, above, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = Shapes.Unlit;
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private ParticleSystem MakeSystem(string name, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return system;
        }

        /// <summary>A spray upward within `angle` degrees of straight up, from a small circle.</summary>
        private static void Cone(ParticleSystem system, float angle, float radius)
        {
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            // A cone points along its own z: turn it to point up.
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>It lands on the ground and the ledges, bouncing a little and sliding to rest.</summary>
        private static void Land(ParticleSystem system)
        {
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = Combat.Layers.GroundMask;
            collision.bounce = 0.3f;
            collision.dampen = 0.5f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;
        }

        /// <summary>Full until `hold` of its life, then fading out and shrinking a little.</summary>
        private static void Fade(ParticleSystem system, float hold)
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(hold)),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            colour.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
        }

        private static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        private static float OutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>The soft disc, the ring and the shard, drawn once into small textures.</summary>
        private static void Prepare()
        {
            if (discTexture == null)
            {
                discTexture = Draw(64, 64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 2f));
                discSprite = Sprite.Create(discTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            }
            if (flashTexture == null)
            {
                // A bright solid core with a soft edge: the flash must read, not only glow.
                flashTexture = Draw(
                    96,
                    96,
                    (x, y) =>
                    {
                        var d = Mathf.Sqrt(x * x + y * y);
                        return d < 0.35f ? 1f : Mathf.Pow(Mathf.Clamp01((1f - d) / 0.65f), 1.6f);
                    }
                );
                flashSprite = Sprite.Create(flashTexture, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 96f);
            }
            if (pillarTexture == null)
            {
                // A soft vertical streak, brightest at its foot; its pivot is the ground.
                pillarTexture = Draw(
                    32,
                    128,
                    (x, y) => Mathf.Pow(1f - Mathf.Abs(x), 1.2f) * Mathf.Pow(1f - (y + 1f) / 2f, 0.6f)
                );
                pillarSprite = Sprite.Create(pillarTexture, new Rect(0, 0, 32, 128), new Vector2(0.5f, 0f), 128f);
            }
            if (ringTexture == null)
            {
                ringTexture = Draw(
                    128,
                    128,
                    (x, y) =>
                    {
                        var d = (Mathf.Sqrt(x * x + y * y) - 0.82f) / 0.09f;
                        return Mathf.Exp(-d * d);
                    }
                );
                ringSprite = Sprite.Create(ringTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
            }
            if (shardTexture == null)
            {
                // A curled sliver of peel: a soft lens, brighter along its middle.
                shardTexture = Draw(32, 64, (x, y) => Mathf.Clamp01((1f - (Mathf.Abs(x) * 2.2f + y * y)) * 3f));
            }
            if (moteMaterial == null)
            {
                moteMaterial = new Material(Shapes.Unlit) { name = "Lantern fruit motes", mainTexture = discTexture };
            }
            if (shardMaterial == null)
            {
                shardMaterial = new Material(Shapes.Unlit)
                {
                    name = "Lantern fruit shards",
                    mainTexture = shardTexture,
                };
            }
        }

        /// <summary>A white texture whose alpha is `alpha` at x, y from -1 to 1 across it.</summary>
        private static Texture2D Draw(int width, int height, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width * 2f - 1f;
                    var v = (y + 0.5f) / height * 2f - 1f;
                    var a = (byte)(Mathf.Clamp01(alpha(u, v)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
