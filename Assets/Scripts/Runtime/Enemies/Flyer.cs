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
    /// A flying enemy that hovers around its post and chases the hero when he comes
    /// close, hurting on contact. With <see cref="Dive"/> it keeps above the hero
    /// instead, stops, turns red (the attack strip) and drops through him, then climbs
    /// back for the next pass. The body (watcher, shade, siren) is a prefab whose
    /// <see cref="FlyerProfile"/> holds the numbers. Origin at the sprite's centre.
    /// Port of scripts/enemies/flyer.gd.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Flyer : MonoBehaviour, IHittable, IEnemy, IPlacementReceiver
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float Accel = 220f;
        public const float HurtTime = 0.25f;
        public const float KnockbackSpeed = 120f;
        public const float DeathDrop = 12f;
        public const float CorpseTime = 0.6f;
        public static readonly Color DeathSparks = new Color(1.0f, 0.45f, 0.35f);
        public const float DeathHitStop = 0.06f;
        public static readonly Vector2 DivePerch = new Vector2(0f, 64f);
        public const float AimTime = 0.5f;
        public const float DiveSpeed = 190f;
        public const float DiveTime = 0.45f;
        public const float ClimbTime = 0.8f;
        public const float DiveCooldown = 1.5f;
        public static readonly Color TintAim = new Color(1.0f, 0.7f, 0.7f);
        public static readonly Color TintHurt = new Color(1.0f, 0.5f, 0.5f);

        [SerializeField]
        private FlyerProfile profile;

        [SerializeField]
        private SpriteAnimator sprite;

        [SerializeField]
        private int contactDamage = 1;

        [Tooltip("Collision size in level pixels about the centre.")]
        [SerializeField]
        private Vector2 bodySize = new Vector2(16f, 16f);

        [Tooltip("The dive habit: keep above the hero and drop on him.")]
        [SerializeField]
        private bool dive;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private readonly Collider2D[] hits = new Collider2D[4];
        private State state = State.Hover;
        private int health;
        private Vector2 home;
        private float time;
        private float hurtLeft;
        private float stateLeft;
        private float diveCooldown;
        private bool touchedWorld;
        private bool dead;
        private EnemyHealthBar healthBar;

        public event System.Action<IEnemy> Died;

        public FlyerProfile Profile => profile;
        public string PlacementId { get; set; } = "";
        public bool IsDead => dead;
        public int Health => health;
        public Vector2 Home => home;
        public bool IsDiving => state == State.Dive;
        public bool IsAiming => state == State.Aim;

        /// <summary>What the map says about this one: its id for the kill list, and whether it dives.</summary>
        public void Configure(WorldPlacement placement)
        {
            PlacementId = placement.Id;
            Dive = placement.Behaviour == "dive";
        }

        public bool Dive
        {
            get => dive;
            set => dive = value;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            if (profile == null)
            {
                Debug.LogError($"Flyer: no profile set on {name}; using defaults.");
                profile = ScriptableObject.CreateInstance<FlyerProfile>();
            }
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteAnimator>();
            }
            if (sprite == null)
            {
                sprite = SpriteAnimator.CreateChild(transform);
            }
            EnemyPhysics.Prepare(body, box, false);
            box.size = bodySize / Ppu;
            box.offset = Vector2.zero;
            health = profile.health;
            home = transform.position;
            time = Random.value * Mathf.PI * 2f;
            if (transform.localScale.x < 0f)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
            }
            healthBar = EnemyHealthBar.Create(transform, new Vector2(0f, 14f));
        }

        private void Start()
        {
            sprite.Play("idle");
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.layer == Layers.World || collision.gameObject.layer == Layers.Ledge)
            {
                touchedWorld = true;
            }
        }

        private void FixedUpdate()
        {
            if (dead)
            {
                return;
            }
            var dt = Time.fixedDeltaTime;
            time += dt;
            stateLeft -= dt;
            diveCooldown = Mathf.Max(diveCooldown - dt, 0f);
            var velocity = body.linearVelocity * Ppu;
            if (hurtLeft > 0f)
            {
                hurtLeft -= dt;
                velocity = Vector2.MoveTowards(velocity, Vector2.zero, Accel * dt);
                if (hurtLeft <= 0f)
                {
                    sprite.Color = Color.white;
                    sprite.Play("idle");
                }
            }
            else
            {
                switch (state)
                {
                    case State.Hover:
                        StepHover(ref velocity, dt);
                        break;
                    case State.Aim:
                        velocity = Vector2.MoveTowards(velocity, Vector2.zero, Accel * 2f * dt);
                        if (stateLeft <= 0f)
                        {
                            StartDive(ref velocity);
                        }
                        break;
                    case State.Dive:
                        if (stateLeft <= 0f || touchedWorld)
                        {
                            StartClimb();
                        }
                        break;
                    case State.Climb:
                        var wanted = Vector2.ClampMagnitude((home - (Vector2)transform.position) * Ppu, profile.chase);
                        velocity = Vector2.MoveTowards(velocity, wanted, Accel * dt);
                        if (stateLeft <= 0f)
                        {
                            state = State.Hover;
                        }
                        break;
                }
            }
            touchedWorld = false;
            body.linearVelocity = velocity / Ppu;
            HurtHero();
        }

        /// <summary>Drifts about its post; near the hero, it chases him (or, diving, keeps above him and drops).</summary>
        private void StepHover(ref Vector2 velocity, float dt)
        {
            var target = home + new Vector2(Mathf.Sin(time * 1.6f) * 24f, Mathf.Sin(time * 2.7f) * 6f) / Ppu;
            var speed = profile.hover;
            var hero = NearHero();
            if (hero != null)
            {
                speed = profile.chase;
                if (dive)
                {
                    target = (Vector2)hero.transform.position + DivePerch / Ppu;
                    if (diveCooldown <= 0f && Vector2.Distance(transform.position, target) < 12f / Ppu)
                    {
                        StartAim();
                        return;
                    }
                }
                else
                {
                    target = (Vector2)hero.transform.position + new Vector2(0f, 16f / Ppu);
                }
            }
            var wanted = Vector2.ClampMagnitude((target - (Vector2)transform.position) * Ppu, speed);
            velocity = Vector2.MoveTowards(velocity, wanted, Accel * dt);
        }

        private PlayerController NearHero()
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero == null)
            {
                return null;
            }
            var distance = Vector2.Distance(transform.position, hero.transform.position) * Ppu;
            return distance < profile.aggro ? hero : null;
        }

        /// <summary>The eye turns red and fixes on the hero: the warning.</summary>
        private void StartAim()
        {
            state = State.Aim;
            stateLeft = AimTime;
            sprite.Restart("attack");
            sprite.Color = TintAim;
        }

        /// <summary>Straight at where the hero stands now.</summary>
        private void StartDive(ref Vector2 velocity)
        {
            state = State.Dive;
            stateLeft = DiveTime;
            sprite.Color = Color.white;
            var hero = EnemyPhysics.LiveHero();
            var at = hero != null ? (Vector2)hero.transform.position + new Vector2(0f, 8f / Ppu) : home;
            var dir = (at - (Vector2)transform.position).normalized;
            if (dir == Vector2.zero)
            {
                dir = Vector2.down;
            }
            velocity = dir * DiveSpeed;
            touchedWorld = false;
            Audio.Sfx.Play("dash", -8f);
        }

        private void StartClimb()
        {
            state = State.Climb;
            stateLeft = ClimbTime;
            diveCooldown = DiveCooldown;
            sprite.Play("idle");
        }

        private void HurtHero()
        {
            EnemyPhysics.HurtHero(transform.position, bodySize / Ppu, contactDamage, transform.position, hits);
        }

        public bool Hit(int damage, Vector2 from)
        {
            if (dead)
            {
                return false;
            }
            health -= damage;
            healthBar.ShowHealth(health, profile.health);
            Audio.Sfx.Play("hit");
            Feedback.Flash(this, 0.1f);
            Feedback.Spark(transform.position);
            // A hit knocks it out of its aim.
            if (state == State.Aim)
            {
                StartClimb();
            }
            hurtLeft = HurtTime;
            sprite.Color = TintHurt;
            sprite.Restart("hurt");
            var away = ((Vector2)transform.position - from).normalized;
            if (away == Vector2.zero)
            {
                away = Vector2.up;
            }
            body.linearVelocity = away * KnockbackSpeed / Ppu;
            if (health <= 0)
            {
                Die();
            }
            return true;
        }

        /// <summary>Death: a blink of stopped time; it sinks a little as the death strip plays in a puff of light, then fades.</summary>
        private void Die()
        {
            dead = true;
            Audio.Sfx.Play("enemy_die");
            Feedback.HitStop(DeathHitStop);
            Feedback.Shake(0.3f);
            Feedback.Spark(transform.position, DeathSparks);
            Feedback.Puff(transform.position, profile.puff);
            box.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            healthBar.Hide();
            sprite.Color = Color.white;
            Kills.Record(PlacementId);
            Died?.Invoke(this);
            StartCoroutine(
                Tween.Move(sprite.transform, sprite.transform.position + new Vector3(0f, -DeathDrop / Ppu, 0f), 0.5f)
            );
            StartCoroutine(EnemyDeath.Play(sprite, 1, CorpseTime, () => Destroy(gameObject)));
            Loot.Drop(transform.position, profile.loot);
            if (Random.value < profile.dropChance + Inventory.Bonus("mote_chance"))
            {
                HealMote.Spawn(transform.position);
            }
        }

        private enum State
        {
            Hover,
            Aim,
            Dive,
            Climb,
        }
    }
}
