using Descender.Fx;
using Descender.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.Enemies
{
    /// <summary>
    /// How a <see cref="LanternFruit"/> looks, animated every rendered frame rather than at
    /// the physics step. Hanging it sways on its stem and its light breathes; ripening it
    /// shivers, swells and burns brighter as the ripe look comes in; falling it stretches
    /// with its speed; its burst and its pop cross-fade from frame to frame where they
    /// happened, with a flare of light for the burst; growing back it swells from the stem
    /// with a little overshoot. Painted from a <see cref="LanternFruitLook"/>, or a circle.
    /// The fruit tells it what happens; it changes nothing but what is drawn.
    /// </summary>
    public sealed class LanternFruitView : MonoBehaviour
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;

        /// <summary>The sway on its stem while it hangs: degrees either way, and radians a second.</summary>
        public const float SwayDegrees = 2.5f;
        public const float SwaySpeed = 1.3f;

        /// <summary>Ripening: how much bigger it swells, and how far it shivers in pixels.</summary>
        public const float Swell = 0.12f;
        public const float ShiverPixels = 1.2f;

        /// <summary>Falling at full speed it is this much longer (and half that much thinner).</summary>
        public const float FallStretch = 0.22f;

        /// <summary>Squashed on landing: this much wider, and this much flatter.</summary>
        public const float SquashWide = 0.4f;
        public const float SquashFlat = 0.4f;

        /// <summary>Its shadow at its darkest, and its width at its smallest and largest, in fruit widths.</summary>
        public const float ShadowAlpha = 0.5f;
        public const float ShadowSmall = 0.8f;
        public const float ShadowLarge = 1.7f;
        public static readonly Color ShadowColor = new Color(0.1f, 0.05f, 0.14f);

        /// <summary>The painted burst plays this much bigger than the fruit, growing to the second size.</summary>
        public const float BurstScale = 1.9f;
        public const float BurstScaleEnd = 2.3f;

        /// <summary>The size it starts growing back from, painted (its own frames are small) and as a circle.</summary>
        public const float GrowFromPainted = 0.7f;
        public const float GrowFromCircle = 0.2f;

        private LanternFruit fruit;
        private LanternFruitLook look;
        private Vector2 size;
        private Transform sway;
        private Transform visual;
        private Shape shape;
        private SpriteRenderer art;
        private SpriteRenderer artNext;
        private SpriteRenderer ripeArt;
        private Transform effectRoot;
        private SpriteRenderer effectNow;
        private SpriteRenderer effectNext;
        private Light2D glow;
        private LanternFruitBurst burst;
        private float effectScale = 1f;
        private LineRenderer stalk;
        private SpriteRenderer shadow;
        private bool hasGround;
        private float groundY;
        private float fallHeight = 1f;
        private float shadowNow;
        private Vector2 anchor;
        private Phase phase = Phase.Hang;
        private float since;
        private float angle;
        private float offset;
        private Sprite[] effectFrames;
        private float effectSince;
        private bool effectOn;

        public bool IsPainted => art != null;

        /// <summary>The sprite the fruit shows now, or null for the circle.</summary>
        public Sprite Showing => art != null ? art.sprite : null;

        /// <summary>The frame of a burst or a pop playing now, or null when none is.</summary>
        public Sprite Playing => effectOn ? effectNow.sprite : null;

        /// <summary>The flash, the ring, the motes, shards and seeds and the flare of its burst.</summary>
        public LanternFruitBurst BurstEffect => burst;

        /// <summary>How dark and wide its shadow on the ground is, 0 (none) to 1 (it is about to land).</summary>
        public float Shadow => shadowNow;

        /// <summary>Where its shadow lies, in the world.</summary>
        public Vector2 ShadowPoint => shadow != null ? (Vector2)shadow.transform.position : Vector2.zero;

        /// <summary>How far it is squashed, 0 to 1.</summary>
        public float Squash { get; private set; }

        /// <summary>How far it has ripened, 0 to 1: how much of the ripe look shows.</summary>
        public float Ripeness { get; private set; }

        /// <summary>Units from the fruit's middle up to where the stalk meets it.</summary>
        public float StemTop => look != null ? look.StemTop : size.y / 2f;

        /// <summary>The top of its stem in the world, where the stalk to the branch ends.</summary>
        public Vector2 StemPoint => sway != null ? (Vector2)sway.position : (Vector2)transform.position;

        /// <summary>Makes the drawing under the fruit: a painted one when `painted` has its hanging sprite, else a circle.</summary>
        public void Build(LanternFruit owner, Vector2 fruitSize, LanternFruitLook painted)
        {
            fruit = owner;
            size = fruitSize;
            look = painted != null && painted.Hang != null ? painted : null;
            offset = Random.value * Mathf.PI * 2f;

            // It sways about the top of its stem, so the stalk's end stays put.
            sway = new GameObject("Sway").transform;
            sway.SetParent(transform, false);
            sway.localPosition = new Vector3(0f, StemTop, 0f);
            visual = new GameObject("Visual").transform;
            visual.SetParent(sway, false);
            visual.localPosition = new Vector3(0f, -StemTop, 0f);
            if (look != null)
            {
                art = MakeRenderer(visual, "Art", look.Hang, 2);
                artNext = MakeRenderer(visual, "Art next", null, 3);
                artNext.enabled = false;
                ripeArt = MakeRenderer(visual, "Ripe", look.Ripe, 4);
                ripeArt.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                shape = Shape.Make(visual, "Fruit", ShapeKind.Circle, Vector2.zero, size, LanternFruit.Glow, "", 2);
            }

            effectRoot = new GameObject("Effect").transform;
            effectRoot.SetParent(transform, false);
            effectNow = MakeRenderer(effectRoot, "Frame", null, 3);
            effectNext = MakeRenderer(effectRoot, "Frame next", null, 4);
            effectNow.enabled = false;
            effectNext.enabled = false;
            burst = LanternFruitBurst.Make(transform);

            shadow = MakeRenderer(transform, "Shadow", LanternFruitBurst.SoftDisc, 1);
            shadow.enabled = false;

            glow = MakeLight(transform, "Glow", LanternFruit.LightIntensity, LanternFruit.LightRadius);

            stalk = new GameObject("Stalk").AddComponent<LineRenderer>();
            stalk.transform.SetParent(transform, false);
            stalk.useWorldSpace = true;
            stalk.positionCount = 2;
            stalk.startWidth = 1f / Ppu;
            stalk.endWidth = 1f / Ppu;
            stalk.sharedMaterial = Shapes.Unlit;
            stalk.startColor = LanternFruit.StalkColor;
            stalk.endColor = LanternFruit.StalkColor;
            stalk.sortingOrder = 1;
            anchor = StemPoint + Vector2.up;
            Hanging();
        }

        /// <summary>Where the stalk is tied to the branch.</summary>
        public void TieTo(Vector2 branch)
        {
            anchor = branch;
        }

        /// <summary>The ground beneath it is at `y`: its shadow lies there while it ripens and falls.</summary>
        public void ShadowOn(float y)
        {
            hasGround = true;
            groundY = y;
            fallHeight = Mathf.Max(transform.position.y - size.y / 2f - y, 0.1f);
        }

        /// <summary>It landed: it flattens, its foot on the ground.</summary>
        public void Squashing()
        {
            Enter(Phase.Squash);
        }

        public void Hanging()
        {
            Enter(Phase.Hang);
            Squash = 0f;
            visual.gameObject.SetActive(true);
            visual.localPosition = new Vector3(0f, -StemTop, 0f);
            sway.localScale = Vector3.one;
            if (art != null)
            {
                art.sprite = look.Hang;
                artNext.enabled = false;
            }
            SetRipe(0f);
        }

        public void Ripening()
        {
            Enter(Phase.Ripen);
        }

        public void Falling()
        {
            Enter(Phase.Fall);
            SetRipe(1f);
            visual.localPosition = new Vector3(0f, -StemTop, 0f);
        }

        /// <summary>It burst at `ground` (the point under its middle): on the ground when `landed`, else on him.</summary>
        public void Burst(Vector2 ground, bool landed = true)
        {
            Enter(Phase.Gone);
            visual.gameObject.SetActive(false);
            Play(look != null ? look.Burst : null, ground, BurstScale);
            burst.Play(ground, landed);
        }

        /// <summary>A strike popped it at `at` (its middle).</summary>
        public void Popped(Vector2 at)
        {
            Enter(Phase.Gone);
            visual.gameObject.SetActive(false);
            Play(look != null ? look.Pop : null, at, 1f);
        }

        public void Growing()
        {
            Enter(Phase.Grow);
            Squash = 0f;
            visual.gameObject.SetActive(true);
            visual.localPosition = new Vector3(0f, -StemTop, 0f);
            visual.localScale = Vector3.one;
            SetRipe(0f);
            if (art != null && look.Grow.Length > 0)
            {
                art.sprite = look.Grow[0];
            }
            ShowGrowing(0f);
        }

        private void Enter(Phase next)
        {
            phase = next;
            since = Time.time;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var t = Time.time - since;
            switch (phase)
            {
                case Phase.Hang:
                    angle = Mathf.Sin(Time.time * SwaySpeed + offset) * SwayDegrees;
                    visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one, Ease(dt, 10f));
                    glow.intensity = LanternFruit.LightIntensity * (0.85f + 0.15f * Mathf.Sin(Time.time * 2f + offset));
                    glow.pointLightOuterRadius = LanternFruit.LightRadius;
                    break;
                case Phase.Ripen:
                    var ripe = Mathf.Clamp01(t / LanternFruit.RipenTime);
                    SetRipe(ripe);
                    // The sway stills, and a shiver grows in its place.
                    angle = Mathf.Lerp(angle, 0f, Ease(dt, 8f));
                    var shiver = Mathf.Sin(Time.time * 55f) * ShiverPixels / Ppu * (0.4f + 0.6f * ripe);
                    visual.localPosition = new Vector3(shiver, -StemTop, 0f);
                    visual.localScale = Vector3.one * (1f + Swell * ripe * ripe);
                    glow.intensity = LanternFruit.LightIntensity * (1f + 1.2f * ripe);
                    glow.pointLightOuterRadius = LanternFruit.LightRadius * (1f + 0.3f * ripe);
                    break;
                case Phase.Fall:
                    angle = Mathf.Lerp(angle, 0f, Ease(dt, 14f));
                    var speed = Mathf.Clamp01(fruit.FallSpeed / LanternFruit.MaxFallSpeed);
                    var stretch = new Vector3(
                        1f + Swell - 0.5f * FallStretch * speed,
                        1f + Swell + FallStretch * speed,
                        1f
                    );
                    visual.localScale = stretch;
                    break;
                case Phase.Squash:
                    var flat = 1f - Mathf.Clamp01(t / LanternFruit.SquashTime);
                    Squash = 1f - flat * flat;
                    var tall = 1f + Swell - SquashFlat * Squash;
                    visual.localScale = new Vector3(1f + Swell + SquashWide * Squash, tall, 1f);
                    // Flattened about its foot, not its middle.
                    visual.localPosition = new Vector3(0f, -StemTop - (1f - tall) * size.y / 2f, 0f);
                    break;
                case Phase.Grow:
                    ShowGrowing(Mathf.Clamp01(t / LanternFruit.GrowTime));
                    break;
            }
            sway.localRotation = Quaternion.Euler(0f, 0f, angle);
            glow.enabled = phase != Phase.Gone;
            UpdateEffect();
            UpdateShadow(dt);
            DrawStalk();
        }

        /// <summary>Growing back: its frames cross-fading in turn, and a swell from the stem with a little overshoot.</summary>
        private void ShowGrowing(float grown)
        {
            var from = art != null ? GrowFromPainted : GrowFromCircle;
            sway.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, BackOut(grown));
            glow.intensity = LanternFruit.LightIntensity * grown;
            angle = Mathf.Sin(Time.time * SwaySpeed + offset) * SwayDegrees * grown;
            if (art == null || look.Grow.Length == 0)
            {
                return;
            }
            var frames = look.Grow;
            var at = grown * frames.Length;
            var frame = Mathf.Min((int)at, frames.Length - 1);
            art.sprite = frames[frame];
            // The next frame comes in over this one; the last gives way to the hanging fruit.
            var next = frame + 1 < frames.Length ? frames[frame + 1] : look.Hang;
            artNext.sprite = next;
            artNext.enabled = true;
            artNext.color = new Color(1f, 1f, 1f, Mathf.Clamp01(at - frame));
        }

        private void Play(Sprite[] frames, Vector2 at, float scale)
        {
            effectScale = scale;
            if (frames == null || frames.Length == 0)
            {
                return;
            }
            effectRoot.position = at;
            effectFrames = frames;
            effectSince = Time.time;
            effectOn = true;
            effectNow.sprite = frames[0];
            effectNow.color = Color.white;
            effectNow.enabled = true;
            UpdateEffect();
        }

        /// <summary>A burst or a pop: each frame cross-fades into the next, the whole grows a little, and the last fades out.</summary>
        private void UpdateEffect()
        {
            if (!effectOn)
            {
                return;
            }
            var at = (Time.time - effectSince) * look.FramesPerSecond;
            var frame = (int)at;
            if (frame >= effectFrames.Length)
            {
                effectOn = false;
                effectNow.enabled = false;
                effectNext.enabled = false;
                return;
            }
            var blend = at - frame;
            effectNow.sprite = effectFrames[frame];
            var last = frame + 1 >= effectFrames.Length;
            effectNext.enabled = !last;
            if (!last)
            {
                effectNext.sprite = effectFrames[frame + 1];
                effectNext.color = new Color(1f, 1f, 1f, blend);
            }
            effectNow.color = new Color(1f, 1f, 1f, last ? 1f - blend : 1f);
            var done = at / effectFrames.Length;
            var grow = effectScale > 1f ? BurstScaleEnd / BurstScale : 1.16f;
            var eased = 1f - (1f - done) * (1f - done);
            effectRoot.localScale = Vector3.one * effectScale * Mathf.Lerp(0.92f, 0.92f * grow, eased);
        }

        /// <summary>
        /// Its shadow on the ground: faint as it ripens, darker and wider as it falls nearer,
        /// so where it will land shows before it does; gone fast once it has.
        /// </summary>
        private void UpdateShadow(float dt)
        {
            if (!hasGround)
            {
                return;
            }
            var target = 0f;
            var follow = false;
            switch (phase)
            {
                case Phase.Ripen:
                    target = 0.35f * Ripeness;
                    break;
                case Phase.Fall:
                    var height = transform.position.y - size.y / 2f - groundY;
                    target = Mathf.Lerp(0.35f, 1f, 1f - Mathf.Clamp01(height / fallHeight));
                    follow = true;
                    break;
                case Phase.Squash:
                    target = 1f;
                    follow = true;
                    break;
            }
            // It follows the fall at once, and eases in and out otherwise.
            shadowNow = follow ? target : Mathf.Lerp(shadowNow, target, Ease(dt, 14f));
            shadow.enabled = shadowNow > 0.01f;
            if (!shadow.enabled)
            {
                return;
            }
            var width = size.x * Mathf.Lerp(ShadowSmall, ShadowLarge, shadowNow);
            shadow.transform.position = new Vector3(transform.position.x, groundY + 0.02f, 0f);
            shadow.transform.localScale = new Vector3(width, width * 0.25f, 1f);
            shadow.color = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, ShadowAlpha * shadowNow);
        }

        private void SetRipe(float ripe)
        {
            Ripeness = ripe;
            if (ripeArt != null)
            {
                ripeArt.color = new Color(1f, 1f, 1f, ripe);
            }
            else if (shape != null)
            {
                shape.Body.color = Color.Lerp(LanternFruit.Glow, LanternFruit.Ripe, ripe);
            }
        }

        private void DrawStalk()
        {
            // The stalk stays on the branch: it shows while the fruit hangs on it and while it grows back.
            var on = phase == Phase.Hang || phase == Phase.Ripen || phase == Phase.Grow;
            stalk.enabled = on;
            if (!on)
            {
                return;
            }
            var end = StemPoint;
            stalk.SetPosition(0, new Vector3(anchor.x, anchor.y, 0f));
            stalk.SetPosition(1, new Vector3(end.x, end.y, 0f));
        }

        /// <summary>The share of the way a smoothing at `rate` a second covers in `dt`, whatever the frame rate.</summary>
        private static float Ease(float dt, float rate) => 1f - Mathf.Exp(-rate * dt);

        /// <summary>An ease that overshoots a little and settles: 0 at 0, 1 at 1.</summary>
        private static float BackOut(float t)
        {
            const float s = 1.70158f;
            var u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }

        private static SpriteRenderer MakeRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            // A lantern shows its own light, whatever lights the scene.
            renderer.sharedMaterial = Shapes.Unlit;
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Light2D MakeLight(Transform parent, string name, float intensity, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = LanternFruit.Glow;
            light.intensity = intensity;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.6f;
            light.shadowsEnabled = false;
            return light;
        }

        private enum Phase
        {
            Hang,
            Ripen,
            Fall,
            Squash,
            Gone,
            Grow,
        }
    }
}
