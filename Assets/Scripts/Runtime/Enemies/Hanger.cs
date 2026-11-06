using Descender.Combat;
using Descender.Economy;
using Descender.Fx;
using Descender.Pickups;
using Descender.Player;
using Descender.World;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The hanged thing: a frozen traitor on a rope under the ceilings of Treachery.
    /// It hangs where the placement puts its feet, swaying, the rope drawn up to the
    /// ceiling. When the hero walks beneath it the rope creaks, then it lets go and
    /// drops on him, lies where it lands for a moment (when it can be hit), and the
    /// rope hauls it back up. Killed, it falls for good. Origin at the feet. Port of
    /// scripts/enemies/hanger.gd.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Hanger : MonoBehaviour, IHittable, IEnemy, IPlacementReceiver
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float Gravity = 700f;
        public const float MaxFallSpeed = 300f;
        public const float HurtTime = 0.2f;
        public static readonly Vector2 Knockback = new Vector2(60f, 30f);
        public const float DropRangeX = 18f;
        public const float DropRangeY = 220f;
        public const float CreakTime = 0.35f;
        public const float RestTime = 1.4f;
        public const float ClimbTime = 1.2f;
        public const float DropCooldown = 1f;
        public const float CorpseTime = 1f;
        public static readonly Vector2 Knot = new Vector2(0f, 58f);
        public const int DropDamage = 2;
        public const int ContactDamage = 1;
        public static readonly Color DeathSparks = new Color(0.75f, 0.85f, 1.0f);
        public static readonly Color DeathPuff = new Color(0.8f, 0.88f, 1.0f);
        public const float DeathHitStop = 0.06f;
        public static readonly Color TintCreak = new Color(1.0f, 0.8f, 0.8f);
        public static readonly Color TintHurt = new Color(1.0f, 0.5f, 0.5f);
        public const float DropChance = 0.5f;
        public static readonly Color RopeColor = new Color(0.45f, 0.35f, 0.25f);
        public static readonly Vector2 BodySize = new Vector2(16f, 44f);

        [SerializeField]
        private SpriteAnimator sprite;

        [SerializeField]
        private int maxHealth = 3;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private LineRenderer rope;
        private readonly Collider2D[] hits = new Collider2D[4];
        private State state = State.Hang;
        private int health;
        private Vector2 home;
        private Vector2 anchor;
        private float time;
        private float hurtLeft;
        private float stateLeft;
        private float cooldown;
        private bool grounded;
        private bool dead;
        private Coroutine climb;
        private EnemyHealthBar healthBar;

        /// <summary>What it leaks and carried: a traitor's light, and a bone now and then.</summary>
        public static readonly LootTable Loot = new LootTable
        {
            LightMin = 7,
            LightMax = 10,
            BoneChance = 0.12f,
            FlaskChance = 0.04f,
            Xp = 8,
        };

        public event System.Action<IEnemy> Died;

        public string PlacementId { get; set; } = "";
        public bool IsDead => dead;
        public int Health => health;
        public Vector2 Home => home;
        public bool IsHanging => state == State.Hang;
        public bool IsDropping => state == State.Drop;
        public bool IsResting => state == State.Rest;

        public void Configure(WorldPlacement placement)
        {
            PlacementId = placement.Id;
        }

        /// <summary>Falling below this y (units) removes it; the level sets it.</summary>
        public float DeathY { get; set; } = float.NegativeInfinity;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteAnimator>();
            }
            if (sprite == null)
            {
                sprite = SpriteAnimator.CreateChild(transform);
            }
            EnemyPhysics.Prepare(body, box);
            box.size = BodySize / Ppu;
            box.offset = new Vector2(0f, BodySize.y / 2f / Ppu);
            health = maxHealth;
            home = transform.position;
            time = Random.value * Mathf.PI * 2f;
            healthBar = EnemyHealthBar.Create(transform, new Vector2(0f, 70f));
            rope = new GameObject("Rope").AddComponent<LineRenderer>();
            rope.transform.SetParent(transform, false);
            rope.useWorldSpace = true;
            rope.positionCount = 2;
            rope.startWidth = 1f / Ppu;
            rope.endWidth = 1f / Ppu;
            rope.sharedMaterial = Shapes.Unlit;
            rope.startColor = RopeColor;
            rope.endColor = RopeColor;
            rope.sortingOrder = -1;
        }

        private void Start()
        {
            sprite.Play("idle");
            // The rope runs to the ceiling above the knot, or a few tiles up when nothing is there.
            var from = home + Knot / Ppu;
            var hit = Physics2D.Raycast(from, Vector2.up, 100f, Layers.WorldMask);
            anchor = hit.collider != null ? hit.point : from + new Vector2(0f, 48f / Ppu);
            DrawRope();
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            time += dt;
            stateLeft -= dt;
            cooldown = Mathf.Max(cooldown - dt, 0f);
            grounded = EnemyPhysics.IsGrounded(box, hits);
            var velocity = body.linearVelocity * Ppu;
            if (dead)
            {
                Fall(ref velocity, dt);
                body.linearVelocity = velocity / Ppu;
                return;
            }
            if (hurtLeft > 0f)
            {
                hurtLeft -= dt;
                if (hurtLeft <= 0f)
                {
                    sprite.Color = Color.white;
                    ShowState();
                }
            }
            switch (state)
            {
                case State.Hang:
                    velocity = Vector2.zero;
                    // A small sway on the rope.
                    sprite.transform.localPosition = new Vector3(Mathf.Sin(time * 1.3f) * 1.5f / Ppu, 0f, 0f);
                    if (cooldown <= 0f && HeroBeneath())
                    {
                        StartCreak();
                    }
                    break;
                case State.Creak:
                    velocity = Vector2.zero;
                    if (stateLeft <= 0f)
                    {
                        LetGo(ref velocity);
                    }
                    break;
                case State.Drop:
                    Fall(ref velocity, dt);
                    break;
                case State.Rest:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, 200f * dt);
                    Fall(ref velocity, dt);
                    if (stateLeft <= 0f)
                    {
                        StartClimb();
                    }
                    break;
                case State.Climb:
                    velocity = Vector2.zero;
                    if (stateLeft <= 0f)
                    {
                        HangAgain();
                    }
                    break;
            }
            body.linearVelocity = velocity / Ppu;
            if (state == State.Drop && grounded && stateLeft < -0.05f)
            {
                Land();
            }
            if (transform.position.y < DeathY)
            {
                Destroy(gameObject);
                return;
            }
            DrawRope();
            HurtHero();
        }

        private void Fall(ref Vector2 velocity, float dt)
        {
            if (!grounded)
            {
                velocity.y = Mathf.Max(velocity.y - Gravity * dt, -MaxFallSpeed);
            }
            else if (velocity.y < 0f)
            {
                velocity.y = 0f;
            }
        }

        /// <summary>True when the hero is alive, right under it and not too far down.</summary>
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

        /// <summary>The warning: the rope creaks, the eyes open, it twitches.</summary>
        private void StartCreak()
        {
            state = State.Creak;
            stateLeft = CreakTime;
            sprite.transform.localPosition = Vector3.zero;
            sprite.Pose("attack", 0);
            sprite.Color = TintCreak;
            Audio.Sfx.Play("creak", -4f);
        }

        private void LetGo(ref Vector2 velocity)
        {
            state = State.Drop;
            stateLeft = 0f;
            sprite.Color = Color.white;
            sprite.Restart("attack");
            velocity = Vector2.zero;
            grounded = false;
        }

        /// <summary>It hits the floor, and lies there.</summary>
        private void Land()
        {
            state = State.Rest;
            stateLeft = RestTime;
            body.linearVelocity = Vector2.zero;
            Feedback.Shake(0.3f);
            Feedback.Puff(transform.position, DeathPuff);
            Audio.Sfx.Play("land", -2f);
            sprite.Pose("death", 2);
        }

        /// <summary>The rope hauls it back up to where it hung.</summary>
        private void StartClimb()
        {
            state = State.Climb;
            stateLeft = ClimbTime;
            sprite.Pose("hurt", 1);
            sprite.Color = Color.white;
            body.simulated = false;
            StopClimb();
            climb = StartCoroutine(Tween.Move(transform, (Vector3)home, ClimbTime));
        }

        private void StopClimb()
        {
            if (climb != null)
            {
                StopCoroutine(climb);
                climb = null;
            }
        }

        private void HangAgain()
        {
            state = State.Hang;
            cooldown = DropCooldown;
            StopClimb();
            transform.position = home;
            body.position = home;
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            sprite.Play("idle");
        }

        /// <summary>The frame the state holds once a flinch is over.</summary>
        private void ShowState()
        {
            switch (state)
            {
                case State.Rest:
                    sprite.Pose("death", 2);
                    break;
                case State.Climb:
                    sprite.Pose("hurt", 1);
                    break;
                case State.Drop:
                case State.Creak:
                    sprite.Play("attack");
                    break;
                default:
                    sprite.Play("idle");
                    break;
            }
        }

        private void DrawRope()
        {
            if (rope == null)
            {
                return;
            }
            rope.SetPosition(0, new Vector3(anchor.x, anchor.y, 0f));
            var knot = (Vector2)transform.position + Knot / Ppu + (Vector2)sprite.transform.localPosition;
            rope.SetPosition(1, new Vector3(knot.x, knot.y, 0f));
        }

        private void HurtHero()
        {
            var damage = state == State.Drop ? DropDamage : ContactDamage;
            var centre = (Vector2)transform.position + new Vector2(0f, BodySize.y / 2f / Ppu);
            EnemyPhysics.HurtHero(centre, (BodySize + new Vector2(4f, 0f)) / Ppu, damage, transform.position, hits);
        }

        public bool Hit(int damage, Vector2 from)
        {
            if (dead)
            {
                return false;
            }
            health -= damage;
            healthBar.ShowHealth(health, maxHealth);
            Audio.Sfx.Play("hit");
            Feedback.Flash(this, 0.1f);
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, 22f / Ppu));
            if (health <= 0)
            {
                Die();
                return true;
            }
            // A hit while it rests pushes it along the floor; one while it creaks drops it.
            if (state == State.Rest)
            {
                var dir = transform.position.x >= from.x ? 1f : -1f;
                body.linearVelocity = new Vector2(Knockback.x * dir, Knockback.y) / Ppu;
            }
            if (state == State.Creak)
            {
                var velocity = Vector2.zero;
                LetGo(ref velocity);
                body.linearVelocity = velocity;
                return true;
            }
            hurtLeft = HurtTime;
            sprite.Color = TintHurt;
            sprite.Restart("hurt");
            return true;
        }

        /// <summary>Death: the rope breaks, time stops for a blink, and it falls for good into a heap, then fades.</summary>
        private void Die()
        {
            dead = true;
            StopClimb();
            body.simulated = true;
            Audio.Sfx.Play("enemy_die");
            Feedback.HitStop(DeathHitStop);
            Feedback.Shake(0.3f);
            var centre = (Vector2)transform.position + new Vector2(0f, 22f / Ppu);
            Feedback.Spark(centre, DeathSparks);
            Feedback.Puff((Vector2)transform.position + new Vector2(0f, 16f / Ppu), DeathPuff);
            healthBar.Hide();
            sprite.Color = Color.white;
            sprite.transform.localPosition = Vector3.zero;
            rope.enabled = false;
            Kills.Record(PlacementId);
            Died?.Invoke(this);
            StartCoroutine(EnemyDeath.Play(sprite, 1, CorpseTime, () => Destroy(gameObject)));
            Pickups.Loot.Drop(centre, Loot);
            if (Random.value < DropChance)
            {
                HealMote.Spawn(centre);
            }
        }

        private enum State
        {
            Hang,
            Creak,
            Drop,
            Rest,
            Climb,
        }
    }
}
