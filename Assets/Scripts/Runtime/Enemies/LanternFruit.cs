using Descender.Combat;
using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// A lantern fruit: a glowing fruit on a stalk under a branch of the Orchard. When the
    /// hero walks beneath it, it shivers and brightens (the warning), lets go and falls; it
    /// bursts where it lands or on him, hurting him if he is in the burst, and grows back on
    /// its stalk a few seconds later. A strike before it bursts pops it harmlessly. It never
    /// dies for good and counts as no kill. This is what it does; <see cref="LanternFruitView"/>
    /// draws it, painted from a <see cref="LanternFruitLook"/> or as a circle. The object's
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
        private LanternFruitView view;
        private readonly Collider2D[] hits = new Collider2D[4];
        private readonly RaycastHit2D[] casts = new RaycastHit2D[4];
        private ContactFilter2D ground;
        private Vector2 size = new Vector2(0.75f, 0.75f);
        private Vector2 home;
        private State state = State.Hang;
        private float stateLeft;
        private float cooldown;

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

        /// <summary>How fast it falls now, in pixels a second.</summary>
        public float FallSpeed { get; private set; }

        /// <summary>How many times it has burst (landed), and how many times a strike popped it.</summary>
        public int Bursts { get; private set; }
        public int Pops { get; private set; }

        /// <summary>What draws it.</summary>
        public LanternFruitView View => view;

        /// <summary>Whether it is drawn from a painted look rather than the placeholder.</summary>
        public bool IsPainted => view != null && view.IsPainted;

        /// <summary>The sprite the fruit shows now, or null for the placeholder.</summary>
        public Sprite Showing => view != null ? view.Showing : null;

        /// <summary>The frame of a burst or a pop playing now, or null when none is.</summary>
        public Sprite Playing => view != null ? view.Playing : null;

        /// <summary>How much of the ripe look shows over the hanging one, 0 to 1.</summary>
        public float Ripeness => view != null ? view.Ripeness : 0f;

        /// <summary>Sets its size in units and its look (null for the placeholder); call once, before its first frame.</summary>
        public void Configure(Vector2 fruitSize, LanternFruitLook painted = null)
        {
            size = fruitSize;
            box.size = size;
            if (view == null)
            {
                view = gameObject.AddComponent<LanternFruitView>();
                view.Build(this, size, painted);
            }
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
        }

        private void Start()
        {
            if (view == null)
            {
                Configure(size);
            }
            home = transform.position;
            // The stalk runs up to the branch above, or a tile up when there is none.
            var top = view.StemPoint;
            var hit = Physics2D.Raycast(top, Vector2.up, 20f, Layers.GroundMask);
            view.TieTo(hit.collider != null ? hit.point : top + Vector2.up);
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            stateLeft -= dt;
            cooldown = Mathf.Max(cooldown - dt, 0f);
            switch (state)
            {
                case State.Hang:
                    if (cooldown <= 0f && HeroBeneath())
                    {
                        StartRipen();
                    }
                    break;
                case State.Ripen:
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
                    if (stateLeft <= 0f)
                    {
                        HangAgain();
                    }
                    break;
            }
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
            view.Ripening();
            Audio.Sfx.Play("creak", -6f);
        }

        private void LetGo()
        {
            state = State.Fall;
            FallSpeed = 0f;
            view.Falling();
        }

        /// <summary>Falls by its own gravity; bursts on him, or on the first ground under it.</summary>
        private void Fall(float dt)
        {
            FallSpeed = Mathf.Min(FallSpeed + Gravity * dt, MaxFallSpeed);
            var drop = FallSpeed / Ppu * dt;
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
            // Drawn from the ground under it.
            view.Burst(at - new Vector2(0f, size.y / 2f));
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
            view.Popped(at);
            return true;
        }

        /// <summary>Gone from his attacks until it grows back.</summary>
        private void Vanish()
        {
            state = State.Gone;
            stateLeft = RegrowTime;
            FallSpeed = 0f;
            box.enabled = false;
        }

        /// <summary>It comes back on its stalk, small, and swells to full size.</summary>
        private void StartGrow()
        {
            state = State.Grow;
            stateLeft = GrowTime;
            body.position = home;
            transform.position = home;
            view.Growing();
        }

        private void HangAgain()
        {
            state = State.Hang;
            cooldown = Cooldown;
            box.enabled = true;
            view.Hanging();
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
