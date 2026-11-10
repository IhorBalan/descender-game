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

        /// <summary>The burst's flash of light, and how long it takes to die away.</summary>
        public const float FlareIntensity = 2.6f;
        public const float FlareRadius = 3.5f;
        public const float FlareTime = 0.35f;

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
        private Light2D flare;
        private LineRenderer stalk;
        private Vector2 anchor;
        private Phase phase = Phase.Hang;
        private float since;
        private float angle;
        private float offset;
        private Sprite[] effectFrames;
        private float effectSince;
        private bool effectOn;
        private float flareSince = -1f;

        public bool IsPainted => art != null;

        /// <summary>The sprite the fruit shows now, or null for the circle.</summary>
        public Sprite Showing => art != null ? art.sprite : null;

        /// <summary>The frame of a burst or a pop playing now, or null when none is.</summary>
        public Sprite Playing => effectOn ? effectNow.sprite : null;

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
            flare = MakeLight(effectRoot, "Flare", 0f, FlareRadius);
            flare.enabled = false;

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

        public void Hanging()
        {
            Enter(Phase.Hang);
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

        /// <summary>It burst on the ground at `ground` (the point under its middle).</summary>
        public void Burst(Vector2 ground)
        {
            Enter(Phase.Gone);
            visual.gameObject.SetActive(false);
            Play(look != null ? look.Burst : null, ground);
            effectRoot.position = ground;
            flareSince = Time.time;
            flare.enabled = true;
        }

        /// <summary>A strike popped it at `at` (its middle).</summary>
        public void Popped(Vector2 at)
        {
            Enter(Phase.Gone);
            visual.gameObject.SetActive(false);
            Play(look != null ? look.Pop : null, at);
        }

        public void Growing()
        {
            Enter(Phase.Grow);
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
                case Phase.Grow:
                    ShowGrowing(Mathf.Clamp01(t / LanternFruit.GrowTime));
                    break;
            }
            sway.localRotation = Quaternion.Euler(0f, 0f, angle);
            glow.enabled = phase != Phase.Gone;
            UpdateEffect();
            UpdateFlare();
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

        private void Play(Sprite[] frames, Vector2 at)
        {
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
            effectRoot.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.08f, 1f - (1f - done) * (1f - done));
        }

        private void UpdateFlare()
        {
            if (flareSince < 0f)
            {
                return;
            }
            var t = (Time.time - flareSince) / FlareTime;
            if (t >= 1f)
            {
                flare.enabled = false;
                flareSince = -1f;
                return;
            }
            flare.intensity = FlareIntensity * (1f - t) * (1f - t);
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
            Gone,
            Grow,
        }
    }
}
