using Descender.Combat;
using Descender.Fx;
using Descender.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.Enemies
{
    /// <summary>
    /// A lantern fruit: a glowing fruit on a stalk under a branch of the Orchard. When the
    /// hero walks beneath it, it shivers and brightens (the warning), lets go and falls; it
    /// bursts where it lands or on him, hurting him if he is in the burst, and grows back on
    /// its stalk a few seconds later. A strike before it bursts pops it harmlessly. It never
    /// dies for good and counts as no kill. Drawn from a <see cref="LanternFruitLook"/> when it
    /// has one, else as a placeholder circle, with a small light either way. The object's
    /// position is the fruit's middle; <see cref="Configure"/> sets its size and look.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LanternFruit : MonoBehaviour, IHittable
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;

        /// <summary>The hero sets it off within this many pixels to either side of its middle.</summary>
        public const float DropRangeX = 20f;

        /// <summary>And no further below it than this, in pixels.</summary>
        public const float DropRangeY = 200f;

        /// <summary>Seconds it shivers before it lets go.</summary>
        public const float RipenTime = 0.45f;

        /// <summary>Pixels a second squared, and the fastest it falls.</summary>
        public const float Gravity = 700f;
        public const float MaxFallSpeed = 320f;

        /// <summary>Damage when it lands on him, and when he is in its burst.</summary>
        public const int FallDamage = 1;
        public const int BurstDamage = 1;

        /// <summary>The burst's box, in pixels, centred where it bursts.</summary>
        public static readonly Vector2 BurstSize = new Vector2(40f, 24f);

        /// <summary>Seconds from a burst or a pop until it starts to grow back, and how long growing takes.</summary>
        public const float RegrowTime = 4f;
        public const float GrowTime = 0.6f;

        /// <summary>Seconds after growing back before it can fall again.</summary>
        public const float Cooldown = 0.5f;

        public static readonly Color Glow = new Color(1f, 0.85f, 0.45f);
        public static readonly Color Ripe = new Color(1f, 0.97f, 0.85f);
        public static readonly Color StalkColor = new Color(0.35f, 0.55f, 0.35f);
        public static readonly Color BurstColor = new Color(1f, 0.9f, 0.55f);
        public const float LightRadius = 2.5f;
        public const float LightIntensity = 0.7f;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private Transform visual;
        private Shape shape;
        private LanternFruitLook look;
        private SpriteRenderer art;
        private SpriteRenderer ripeArt;
        private SpriteRenderer effect;
        private Sprite[] effectFrames;
        private float effectTime;
        private Light2D glow;
        private LineRenderer stalk;
        private readonly Collider2D[] hits = new Collider2D[4];
        private readonly RaycastHit2D[] casts = new RaycastHit2D[4];
        private ContactFilter2D ground;
        private Vector2 size = new Vector2(0.75f, 0.75f);
        private Vector2 home;
        private Vector2 anchor;
        private State state = State.Hang;
        private float stateLeft;
        private float cooldown;
        private float fallSpeed;
        private float time;

        /// <summary>Hanging still on its stalk, waiting for him to pass beneath.</summary>
        public bool IsHanging => state == State.Hang;

        /// <summary>Shivering: the warning before it lets go.</summary>
        public bool IsRipening => state == State.Ripen;

        public bool IsFalling => state == State.Fall;

        /// <summary>Burst or popped, and not yet growing back.</summary>
        public bool IsGone => state == State.Gone;

        public bool IsGrowing => state == State.Grow;

        /// <summary>Where it hangs, its middle, in units.</summary>
        public Vector2 Home => home;

        /// <summary>How many times it has burst (landed), and how many times a strike popped it.</summary>
        public int Bursts { get; private set; }
        public int Pops { get; private set; }

        /// <summary>Whether it is drawn from a painted look rather than the placeholder.</summary>
        public bool IsPainted => art != null;

        /// <summary>The sprite the fruit shows now, or null for the placeholder.</summary>
        public Sprite Showing => art != null ? art.sprite : null;

        /// <summary>The frame of a burst or a pop playing now, or null when none is.</summary>
        public Sprite Playing => effect != null && effect.enabled ? effect.sprite : null;

        /// <summary>How much of the ripe look shows over the hanging one, 0 to 1.</summary>
        public float Ripeness => ripeArt != null ? ripeArt.color.a : 0f;

        /// <summary>Sets its size in units and its look (null for the placeholder); call before its first physics step.</summary>
        public void Configure(Vector2 fruitSize, LanternFruitLook painted = null)
        {
            size = fruitSize;
            if (box != null)
            {
                box.size = size;
            }
            if (shape != null)
            {
                shape.Set(ShapeKind.Circle, size, Glow, "", 2);
            }
            if (painted != null && painted.Hang != null && art == null)
            {
                UseLook(painted);
            }
        }

        /// <summary>The painted fruit in place of the circle: the hanging sprite, the ripe one over it, and a player for the burst and the pop.</summary>
        private void UseLook(LanternFruitLook painted)
        {
            look = painted;
            shape.gameObject.SetActive(false);
            art = MakeRenderer(visual, "Art", look.Hang, 2);
            ripeArt = MakeRenderer(visual, "Ripe", look.Ripe, 3);
            ripeArt.color = new Color(1f, 1f, 1f, 0f);
            effect = MakeRenderer(transform, "Effect", null, 3);
            effect.enabled = false;
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

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            gameObject.layer = Layers.Enemy;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            // Solid like every enemy's, so the attacks' queries find it; the enemies' layer
            // passes the hero, and a kinematic body is not pushed by the world.
            box.isTrigger = false;
            box.size = size;
            ground = Layers.Solid(Layers.GroundMask);
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            shape = Shape.Make(visual, "Fruit", ShapeKind.Circle, Vector2.zero, size, Glow, "", 2);

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(transform, false);
            glow = lightObject.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Glow;
            glow.intensity = LightIntensity;
            glow.pointLightInnerRadius = 0f;
            glow.pointLightOuterRadius = LightRadius;
            glow.falloffIntensity = 0.6f;
            glow.shadowsEnabled = false;

            stalk = new GameObject("Stalk").AddComponent<LineRenderer>();
            stalk.transform.SetParent(transform, false);
            stalk.useWorldSpace = true;
            stalk.positionCount = 2;
            stalk.startWidth = 1f / Ppu;
            stalk.endWidth = 1f / Ppu;
            stalk.sharedMaterial = Shapes.Unlit;
            stalk.startColor = StalkColor;
            stalk.endColor = StalkColor;
            stalk.sortingOrder = 1;
            time = Random.value * Mathf.PI * 2f;
        }

        private void Start()
        {
            home = transform.position;
            // The stalk runs up to the branch above, or a tile up when there is none.
            var top = home + new Vector2(0f, StemTop);
            var hit = Physics2D.Raycast(top, Vector2.up, 20f, Layers.GroundMask);
            anchor = hit.collider != null ? hit.point : top + Vector2.up;
            DrawStalk();
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            time += dt;
            stateLeft -= dt;
            cooldown = Mathf.Max(cooldown - dt, 0f);
            switch (state)
            {
                case State.Hang:
                    // A slow glow, breathing.
                    glow.intensity = LightIntensity * (0.85f + 0.15f * Mathf.Sin(time * 2f));
                    if (cooldown <= 0f && HeroBeneath())
                    {
                        StartRipen();
                    }
                    break;
                case State.Ripen:
                    // The warning: it shivers on its stalk and burns brighter.
                    var shiver = Mathf.Sin(time * 60f) * 1.5f / Ppu;
                    visual.localPosition = new Vector3(shiver, 0f, 0f);
                    var ripeness = 1f - Mathf.Clamp01(stateLeft / RipenTime);
                    SetRipeness(ripeness);
                    glow.intensity = LightIntensity * (1f + ripeness);
                    if (stateLeft <= 0f)
                    {
                        LetGo();
                    }
                    break;
                case State.Fall:
                    Fall(dt);
                    break;
                case State.Gone:
                    if (stateLeft <= 0f)
                    {
                        StartGrow();
                    }
                    break;
                case State.Grow:
                    var grown = 1f - Mathf.Clamp01(stateLeft / GrowTime);
                    ShowGrowing(grown);
                    glow.intensity = LightIntensity * grown;
                    if (stateLeft <= 0f)
                    {
                        HangAgain();
                    }
                    break;
            }
            DrawStalk();
        }

        private void Update()
        {
            // The burst and the pop play in frames where they happened, and then hide.
            if (effect == null || !effect.enabled)
            {
                return;
            }
            effectTime += Time.deltaTime;
            var frame = (int)(effectTime * look.FramesPerSecond);
            if (frame >= effectFrames.Length)
            {
                effect.enabled = false;
                return;
            }
            effect.sprite = effectFrames[frame];
        }

        /// <summary>Units from its middle to where the stalk meets it: the painted stem's top, or the circle's.</summary>
        private float StemTop => look != null ? look.StemTop : size.y / 2f;

        private void SetRipeness(float ripeness)
        {
            if (ripeArt != null)
            {
                ripeArt.color = new Color(1f, 1f, 1f, ripeness);
            }
            else
            {
                shape.Body.color = Color.Lerp(Glow, Ripe, ripeness);
            }
        }

        /// <summary>Growing back: the painted frames in turn, or the circle swelling.</summary>
        private void ShowGrowing(float grown)
        {
            if (art != null && look.Grow.Length > 0)
            {
                var frame = Mathf.Min((int)(grown * look.Grow.Length), look.Grow.Length - 1);
                art.sprite = look.Grow[frame];
                visual.localScale = Vector3.one;
            }
            else
            {
                visual.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, grown);
            }
        }

        private void PlayEffect(Sprite[] frames, Vector2 at)
        {
            if (effect == null || frames == null || frames.Length == 0)
            {
                return;
            }
            effect.transform.position = at;
            effectFrames = frames;
            effectTime = 0f;
            effect.sprite = frames[0];
            effect.enabled = true;
        }

        /// <summary>True when the hero is alive, under it and not too far down.</summary>
        private bool HeroBeneath()
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero == null)
            {
                return false;
            }
            var offset = ((Vector2)hero.transform.position - (Vector2)transform.position) * Ppu;
            return Mathf.Abs(offset.x) <= DropRangeX && offset.y < 0f && offset.y >= -DropRangeY;
        }

        private void StartRipen()
        {
            state = State.Ripen;
            stateLeft = RipenTime;
            Audio.Sfx.Play("creak", -6f);
        }

        private void LetGo()
        {
            state = State.Fall;
            fallSpeed = 0f;
            visual.localPosition = Vector3.zero;
            SetRipeness(1f);
        }

        /// <summary>Falls by its own gravity; bursts on him, or on the first ground under it.</summary>
        private void Fall(float dt)
        {
            fallSpeed = Mathf.Min(fallSpeed + Gravity * dt, MaxFallSpeed);
            var drop = fallSpeed / Ppu * dt;
            var at = body.position;
            // Cast the whole step so a fast fall never passes through a thin ledge.
            var count = Physics2D.BoxCast(at, size, 0f, Vector2.down, ground, casts, drop);
            var landed = false;
            for (var i = 0; i < count; i++)
            {
                if (casts[i].normal.y > 0.5f && casts[i].distance < drop)
                {
                    drop = casts[i].distance;
                    landed = true;
                }
            }
            var next = at + Vector2.down * drop;
            body.MovePosition(next);
            if (EnemyPhysics.HurtHero(next, size, FallDamage, next, hits))
            {
                Burst(next);
                return;
            }
            if (landed)
            {
                Burst(next);
            }
        }

        /// <summary>It bursts in a puff of light that hurts the hero if he is in it.</summary>
        private void Burst(Vector2 at)
        {
            Bursts++;
            EnemyPhysics.HurtHero(at, BurstSize / Ppu, BurstDamage, at, hits);
            Feedback.Puff(at, BurstColor);
            Feedback.Spark(at, BurstColor);
            Feedback.Shake(0.15f);
            Audio.Sfx.Play("enemy_die", -4f);
            Vanish();
            // The burst's pivot is the ground under it.
            PlayEffect(look != null ? look.Burst : null, at - new Vector2(0f, size.y / 2f));
        }

        public bool Hit(int damage, Vector2 from)
        {
            if (state == State.Gone || state == State.Grow)
            {
                return false;
            }
            // A strike pops it where it is: a spark, and no burst to hurt him.
            Pops++;
            var at = body.position;
            Feedback.Spark(at, Glow);
            Audio.Sfx.Play("hit");
            Vanish();
            PlayEffect(look != null ? look.Pop : null, at);
            return true;
        }

        /// <summary>Gone from sight and from his attacks until it grows back.</summary>
        private void Vanish()
        {
            state = State.Gone;
            stateLeft = RegrowTime;
            fallSpeed = 0f;
            visual.gameObject.SetActive(false);
            glow.enabled = false;
            box.enabled = false;
        }

        /// <summary>It comes back on its stalk, small, and swells to full size.</summary>
        private void StartGrow()
        {
            state = State.Grow;
            stateLeft = GrowTime;
            body.position = home;
            transform.position = home;
            visual.localPosition = Vector3.zero;
            SetRipeness(0f);
            ShowGrowing(0f);
            visual.gameObject.SetActive(true);
            glow.enabled = true;
            glow.intensity = 0f;
        }

        private void HangAgain()
        {
            state = State.Hang;
            cooldown = Cooldown;
            visual.localScale = Vector3.one;
            if (art != null)
            {
                art.sprite = look.Hang;
            }
            box.enabled = true;
        }

        private void DrawStalk()
        {
            if (stalk == null)
            {
                return;
            }
            // The stalk stays on the branch: it shows while the fruit hangs and while it grows back.
            var hanging = state == State.Hang || state == State.Ripen || state == State.Grow;
            stalk.enabled = hanging;
            if (!hanging)
            {
                return;
            }
            var fruitTop = (Vector2)visual.position + new Vector2(0f, StemTop * visual.localScale.y);
            stalk.SetPosition(0, new Vector3(anchor.x, anchor.y, 0f));
            stalk.SetPosition(1, new Vector3(fruitTop.x, fruitTop.y, 0f));
        }

        /// <summary>The fruit's states, in the order it goes through them.</summary>
        private enum State
        {
            Hang,
            Ripen,
            Fall,
            Gone,
            Grow,
        }
    }
}
