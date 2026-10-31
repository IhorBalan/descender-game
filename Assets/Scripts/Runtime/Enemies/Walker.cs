using System.Collections;
using Descender.Combat;
using Descender.Economy;
using Descender.Fx;
using Descender.Pickups;
using Descender.Player;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// A ground enemy that patrols a platform, turning at edges and walls, and hurts
    /// the hero on contact. When the hero stands close in front of it, it stops and
    /// swings: a wind-up, a short strike with a hitbox in front, then a recovery. A
    /// hit during the wind-up or strike cancels the attack (unless it wears armour).
    /// A <see cref="WalkerBehaviour"/> from the placement adds one habit: chase,
    /// ambush, pounce, hurl, slam or lunge. The body (imp, angel guard, brute, slug,
    /// hoarder, burned, hound) is a prefab whose <see cref="WalkerProfile"/> holds the
    /// numbers. Origin at the feet. Port of scripts/enemies/walker.gd.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Walker : MonoBehaviour, IHittable, IEnemy
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float Gravity = 700f;
        public const float MaxFallSpeed = 260f;
        public const float HurtTime = 0.2f;
        public static readonly Vector2 Knockback = new Vector2(110f, 70f);
        public const float AttackHeightTolerance = 24f;
        public static readonly Color TintWindup = new Color(1.0f, 0.75f, 0.55f);
        public static readonly Color TintHurt = new Color(1.0f, 0.5f, 0.5f);
        public const float CorpseTime = 1f;
        public static readonly Color DeathSparks = new Color(1.0f, 0.45f, 0.35f);
        public const float DeathHitStop = 0.06f;

        // Chasing
        public const float ChaseRange = 120f;
        public const float ChaseHeightTolerance = 40f;
        public const float ChaseGiveUp = 1.4f;
        public const float ChaseSpeed = 1.8f;

        // Ambush
        public const float AmbushRange = 40f;
        public const float AmbushHaste = 0.5f;
        public const float MarkTime = 0.6f;
        public static readonly Color MarkColor = new Color(1.0f, 0.35f, 0.3f);

        // Pounce
        public const float PounceMin = 36f;
        public const float PounceMax = 96f;
        public const float PounceWindup = 0.4f;
        public static readonly Vector2 PounceVelocity = new Vector2(150f, 190f);
        public const float PounceTimeMax = 1f;
        public const float PounceRecover = 0.4f;
        public const float PounceCooldown = 1.6f;
        public static readonly Vector3 CrouchScale = new Vector3(1.2f, 0.8f, 1f);

        // Lunge
        public const float LungeSight = 200f;
        public const float LungeSpeed = 2.4f;
        public const float LungeMin = 28f;
        public const float LungeMax = 72f;
        public const float LungeWindup = 0.25f;
        public static readonly Vector2 LungeVelocity = new Vector2(210f, 90f);
        public const float LungeCooldown = 1.2f;

        // Hurl
        public const float HurlRange = 150f;
        public const float HurlHeightTolerance = 48f;
        public const float HurlRetreat = 48f;
        public const float HurlWindup = 0.5f;
        public const float HurlRecover = 0.5f;
        public const float HurlCooldown = 1.8f;
        public static readonly Vector2 HurlHand = new Vector2(10f, 14f);

        // Slam
        public const float SlamMax = 110f;
        public const float SlamWindup = 0.7f;
        public const float SlamRecover = 0.9f;
        public const float SlamCooldown = 2.5f;
        public static readonly Color TintSlam = new Color(1.0f, 0.6f, 0.45f);

        /// <summary>Lines an enemy may shout as it winds up, by bark set.</summary>
        public static readonly string[] FallenBarks =
        {
            "Up! He said up!",
            "The Lowest talks to nobody.",
            "Half-angel! He said you'd come!",
            "He says a name. To nobody.",
        };
        public static readonly string[] BruteBarks = { "Hrr.", "Down. Stay down." };
        public const float BarkChance = 0.5f;
        public const float BarkTime = 1.3f;
        public static readonly Color BarkColor = new Color(1.0f, 0.85f, 0.8f);

        [SerializeField]
        private WalkerProfile profile;

        [SerializeField]
        private SpriteAnimator sprite;

        [SerializeField]
        private int contactDamage = 1;

        [SerializeField]
        private WalkerBehaviour behaviour = WalkerBehaviour.None;

        [Tooltip("A key of the bark sets (fallen, brute): the enemy shouts one of its lines as it winds up, half the time.")]
        [SerializeField]
        private string barks = "";

        [Tooltip("A resting demon of the last camp: patrols, never swings and does no contact damage.")]
        [SerializeField]
        private bool passive;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private readonly Collider2D[] hits = new Collider2D[4];
        private State state = State.Patrol;
        private Attack attack = Attack.Swing;
        private int health;
        private int direction = -1;
        private float hurtLeft;
        private float stateLeft;
        private float attackCooldown;
        private float specialCooldown;
        private bool awake;
        private bool dead;
        private bool grounded;
        private EnemyHealthBar healthBar;
        private EnemyMark mark;
        private Coroutine crouch;

        public event System.Action<IEnemy> Died;

        /// <summary>Text appeared over the head: the ambush mark or a bark, with its colour and time.</summary>
        public event System.Action<string, Color, float> Marked;

        public WalkerProfile Profile => profile;
        public string PlacementId { get; set; } = "";
        public bool IsDead => dead;
        public bool IsAlive => !dead;
        public int Health => health;
        public int Direction => direction;
        public WalkerBehaviour Behaviour => behaviour;
        public bool IsAttacking => state == State.Windup || state == State.Strike || state == State.Leap || state == State.Recover;
        public bool IsLurking => state == State.Lurk;
        public bool IsChasing => state == State.Chase;

        /// <summary>Falling below this y (units) removes the enemy; the level sets it.</summary>
        public float DeathY { get; set; } = float.NegativeInfinity;

        public bool Passive
        {
            get => passive;
            set => passive = value;
        }

        public string Barks
        {
            get => barks;
            set => barks = value ?? "";
        }

        /// <summary>The text over the head right now, or "".</summary>
        public string MarkText => mark != null ? mark.Text : "";

        /// <summary>Sets the habit from a placement's name ("chase", "ambush"...); "" or an unknown name is plain patrolling.</summary>
        public void SetBehaviour(string godotName)
        {
            behaviour = Parse(godotName);
            if (behaviour == WalkerBehaviour.Ambush && !passive && !dead)
            {
                state = State.Lurk;
                sprite.Play("idle");
            }
        }

        public void SetBehaviour(WalkerBehaviour value) => SetBehaviour(value.ToString().ToLowerInvariant());

        public static WalkerBehaviour Parse(string godotName)
        {
            switch ((godotName ?? "").Trim().ToLowerInvariant())
            {
                case "chase":
                    return WalkerBehaviour.Chase;
                case "ambush":
                    return WalkerBehaviour.Ambush;
                case "pounce":
                    return WalkerBehaviour.Pounce;
                case "hurl":
                    return WalkerBehaviour.Hurl;
                case "slam":
                    return WalkerBehaviour.Slam;
                case "lunge":
                    return WalkerBehaviour.Lunge;
                default:
                    return WalkerBehaviour.None;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            if (profile == null)
            {
                Debug.LogError($"Walker: no profile set on {name}; using defaults.");
                profile = ScriptableObject.CreateInstance<WalkerProfile>();
            }
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteAnimator>();
            }
            if (sprite == null)
            {
                sprite = SpriteAnimator.CreateChild(transform);
            }
            EnemyPhysics.Prepare(body, box);
            var size = profile.body;
            box.size = size / Ppu;
            box.offset = new Vector2(0f, size.y / 2f / Ppu);
            health = profile.health;
            // A placement flips the whole object to turn the body; read it back as the direction.
            if (transform.localScale.x < 0f)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
                direction = 1;
            }
            healthBar = EnemyHealthBar.Create(transform, new Vector2(0f, size.y + 6f));
        }

        private void Start()
        {
            sprite.Play(profile.walkAnim);
            Face(direction);
            if (behaviour == WalkerBehaviour.Ambush && !passive)
            {
                state = State.Lurk;
                sprite.Play("idle");
            }
        }

        private void FixedUpdate()
        {
            if (dead)
            {
                return;
            }
            var dt = Time.fixedDeltaTime;
            grounded = EnemyPhysics.IsGrounded(box, hits);
            var velocity = body.linearVelocity * Ppu;
            if (!grounded)
            {
                velocity.y = Mathf.Max(velocity.y - Gravity * dt, -MaxFallSpeed);
            }
            else if (velocity.y < 0f)
            {
                velocity.y = 0f;
            }
            attackCooldown = Mathf.Max(attackCooldown - dt, 0f);
            specialCooldown = Mathf.Max(specialCooldown - dt, 0f);
            if (hurtLeft > 0f)
            {
                hurtLeft -= dt;
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, 300f * dt);
                if (hurtLeft <= 0f)
                {
                    Tint(Color.white);
                    sprite.Play(profile.walkAnim);
                }
            }
            else
            {
                switch (state)
                {
                    case State.Patrol:
                        StepPatrol(ref velocity);
                        break;
                    case State.Chase:
                        StepChase(ref velocity);
                        break;
                    case State.Lurk:
                        StepLurk(ref velocity);
                        break;
                    case State.Leap:
                        StepLeap(ref velocity, dt);
                        break;
                    default:
                        StepAttack(ref velocity, dt);
                        break;
                }
            }
            body.linearVelocity = velocity / Ppu;
            if (hurtLeft <= 0f && state == State.Patrol && EnemyPhysics.IsOnWall(box, direction, hits))
            {
                Face(-direction);
            }
            if (transform.position.y < DeathY)
            {
                Destroy(gameObject);
                return;
            }
            HurtHero();
        }

        private void StepPatrol(ref Vector2 velocity)
        {
            if (TryAttacks(ref velocity))
            {
                return;
            }
            if (Chasing && HeroWithin(ChaseRangeNow, ChaseHeightTolerance))
            {
                state = State.Chase;
                return;
            }
            var hero = EnemyPhysics.LiveHero();
            if (behaviour == WalkerBehaviour.Hurl && hero != null && HeroWithin(HurlRetreat, HurlHeightTolerance))
            {
                // Too close to throw: back off, but not over the edge.
                Face(hero.transform.position.x >= transform.position.x ? -1 : 1);
                velocity.x = LedgeAhead ? direction * profile.speed : 0f;
                return;
            }
            if (grounded && !EnemyPhysics.FloorAhead(box, direction, hits))
            {
                Face(-direction);
            }
            velocity.x = direction * profile.speed;
        }

        /// <summary>Walks at the hero, stopping at the end of the ledge, until he is gone.</summary>
        private void StepChase(ref Vector2 velocity)
        {
            if (TryAttacks(ref velocity))
            {
                return;
            }
            var hero = EnemyPhysics.LiveHero();
            if (hero == null || !HeroWithin(ChaseRangeNow * ChaseGiveUp, ChaseHeightTolerance))
            {
                state = State.Patrol;
                return;
            }
            Face(hero.transform.position.x >= transform.position.x ? 1 : -1);
            var haste = behaviour == WalkerBehaviour.Lunge ? LungeSpeed : ChaseSpeed;
            velocity.x = LedgeAhead ? direction * profile.speed * haste : 0f;
        }

        /// <summary>Waits as if idle; the hero coming close springs the swing.</summary>
        private void StepLurk(ref Vector2 velocity)
        {
            velocity.x = 0f;
            if (attackCooldown > 0f || !HeroWithin(AmbushRange, AttackHeightTolerance))
            {
                return;
            }
            awake = true;
            ShowMark("!", MarkColor, MarkTime);
            StartWindup(Attack.Swing, profile.windup * AmbushHaste, ref velocity);
        }

        /// <summary>Starts whichever attack the hero's distance calls for. True if one did.</summary>
        private bool TryAttacks(ref Vector2 velocity)
        {
            var hero = EnemyPhysics.LiveHero();
            if (passive || hero == null)
            {
                return false;
            }
            if (attackCooldown <= 0f && HeroWithin(profile.attackRange, AttackHeightTolerance))
            {
                StartWindup(Attack.Swing, profile.windup, ref velocity);
                return true;
            }
            if (specialCooldown > 0f || !SpecialWanted(hero))
            {
                return false;
            }
            switch (behaviour)
            {
                case WalkerBehaviour.Pounce:
                    StartWindup(Attack.Pounce, PounceWindup, ref velocity);
                    break;
                case WalkerBehaviour.Hurl:
                    StartWindup(Attack.Hurl, HurlWindup, ref velocity);
                    break;
                case WalkerBehaviour.Slam:
                    StartWindup(Attack.Slam, SlamWindup, ref velocity);
                    break;
                case WalkerBehaviour.Lunge:
                    StartWindup(Attack.Pounce, LungeWindup, ref velocity);
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>Whether the hero stands where the behaviour's own attack wants him (offsets in pixels, Y up).</summary>
        private bool SpecialWanted(PlayerController hero)
        {
            var offset = ((Vector2)hero.transform.position - (Vector2)transform.position) * Ppu;
            var dx = Mathf.Abs(offset.x);
            switch (behaviour)
            {
                case WalkerBehaviour.Pounce:
                    // A hero a few tiles away, level or a little above.
                    return dx >= PounceMin && dx <= PounceMax && offset.y < 48f && offset.y >= -8f;
                case WalkerBehaviour.Hurl:
                    return dx > HurlRetreat && dx <= HurlRange && Mathf.Abs(offset.y) <= HurlHeightTolerance;
                case WalkerBehaviour.Slam:
                    return dx > profile.attackRange && dx <= SlamMax && Mathf.Abs(offset.y) <= AttackHeightTolerance;
                case WalkerBehaviour.Lunge:
                    return dx >= LungeMin && dx <= LungeMax && offset.y < 24f && offset.y >= -8f;
                default:
                    return false;
            }
        }

        private bool Chasing =>
            behaviour == WalkerBehaviour.Chase
            || behaviour == WalkerBehaviour.Lunge
            || (behaviour == WalkerBehaviour.Ambush && awake);

        private float ChaseRangeNow => behaviour == WalkerBehaviour.Lunge ? LungeSight : ChaseRange;

        /// <summary>True when the hero is alive and in this box (pixels) around the enemy.</summary>
        private bool HeroWithin(float rangeX, float rangeY)
        {
            var hero = EnemyPhysics.LiveHero();
            if (passive || hero == null)
            {
                return false;
            }
            var offset = ((Vector2)hero.transform.position - (Vector2)transform.position) * Ppu;
            return Mathf.Abs(offset.x) <= rangeX && Mathf.Abs(offset.y) <= rangeY;
        }

        private bool LedgeAhead => !grounded || EnemyPhysics.FloorAhead(box, direction, hits);

        private void StartWindup(Attack kind, float duration, ref Vector2 velocity)
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero != null)
            {
                Face(hero.transform.position.x >= transform.position.x ? 1 : -1);
            }
            velocity.x = 0f;
            attack = kind;
            state = State.Windup;
            stateLeft = duration;
            sprite.Pose("attack", 0);
            switch (kind)
            {
                case Attack.Swing:
                    Tint(TintWindup);
                    MaybeBark();
                    break;
                case Attack.Pounce:
                    Tint(TintWindup);
                    if (sprite != null)
                    {
                        StopCrouch();
                        crouch = StartCoroutine(Tween.Scale(sprite.transform, Vector3.one, CrouchScale, duration));
                    }
                    break;
                case Attack.Hurl:
                    Tint(TintWindup);
                    Feedback.Spark(HandPosition, EnemyShot.BoltColor);
                    break;
                case Attack.Slam:
                    // The arms go up (the strip's raised frame) and stay there.
                    sprite.Pose("attack", 1);
                    Tint(TintSlam);
                    MaybeBark();
                    break;
            }
        }

        private void MaybeBark()
        {
            string[] lines;
            switch (barks)
            {
                case "fallen":
                    lines = FallenBarks;
                    break;
                case "brute":
                    lines = BruteBarks;
                    break;
                default:
                    return;
            }
            if (Random.value >= BarkChance)
            {
                return;
            }
            ShowMark(lines[Random.Range(0, lines.Length)], BarkColor, BarkTime);
        }

        private void ShowMark(string text, Color color, float duration)
        {
            if (mark == null)
            {
                mark = EnemyMark.Create(transform, new Vector2(0f, profile.body.y + 14f));
            }
            mark.Show(text, color, duration);
            Marked?.Invoke(text, color, duration);
        }

        private void StepAttack(ref Vector2 velocity, float dt)
        {
            velocity.x = 0f;
            stateLeft -= dt;
            if (state == State.Strike)
            {
                StrikeHero();
            }
            if (stateLeft > 0f)
            {
                return;
            }
            switch (state)
            {
                case State.Windup:
                    Release(ref velocity);
                    break;
                case State.Strike:
                    state = State.Recover;
                    stateLeft = profile.recover;
                    sprite.Play("idle");
                    break;
                case State.Recover:
                    EndAttack();
                    break;
            }
        }

        /// <summary>The wind-up is over: the attack happens.</summary>
        private void Release(ref Vector2 velocity)
        {
            Tint(Color.white);
            sprite.Restart("attack");
            switch (attack)
            {
                case Attack.Swing:
                    state = State.Strike;
                    stateLeft = profile.strike;
                    break;
                case Attack.Pounce:
                    Unsquash();
                    state = State.Leap;
                    stateLeft = PounceTimeMax;
                    var spring = behaviour == WalkerBehaviour.Lunge ? LungeVelocity : PounceVelocity;
                    velocity = new Vector2(spring.x * direction, spring.y);
                    grounded = false;
                    Audio.Sfx.Play("jump", -6f);
                    break;
                case Attack.Hurl:
                    Throw();
                    state = State.Recover;
                    stateLeft = HurlRecover;
                    break;
                case Attack.Slam:
                    Slam();
                    state = State.Recover;
                    stateLeft = SlamRecover;
                    break;
            }
        }

        /// <summary>Airborne with the claws out; landing ends it.</summary>
        private void StepLeap(ref Vector2 velocity, float dt)
        {
            stateLeft -= dt;
            StrikeHero();
            if ((grounded && stateLeft < PounceTimeMax - 0.1f) || stateLeft <= 0f)
            {
                velocity.x = 0f;
                state = State.Recover;
                stateLeft = PounceRecover;
                sprite.Play("idle");
            }
        }

        private Vector2 ReachCentre =>
            (Vector2)transform.position
            + new Vector2((profile.body.x / 2f + profile.reach.x / 2f) * direction, profile.reach.y / 2f) / Ppu;

        private void StrikeHero()
        {
            EnemyPhysics.HurtHero(ReachCentre, profile.reach / Ppu, profile.attackDamage, transform.position, hits);
        }

        private void Throw()
        {
            var hero = EnemyPhysics.LiveHero();
            var from = HandPosition;
            var aim = hero != null ? (Vector2)hero.transform.position + new Vector2(0f, 12f / Ppu) - from : Vector2.zero;
            EnemyShot.Spawn(EnemyShot.Kind.Bolt, from, aim.sqrMagnitude > 0f ? aim : new Vector2(direction, 0f));
            Audio.Sfx.Play("swing", -4f);
        }

        /// <summary>The ground shakes and a wave runs along the floor the way it faces.</summary>
        private void Slam()
        {
            var halfWidth = profile.body.x / 2f;
            var at = (Vector2)transform.position + new Vector2(direction * (halfWidth + 6f) / Ppu, 0f);
            EnemyShot.Spawn(EnemyShot.Kind.Wave, at, new Vector2(direction, 0f));
            Feedback.Shake(0.35f);
            Audio.Sfx.Play("land", -2f);
            for (var i = 0; i < 3; i++)
            {
                Feedback.Spark(
                    (Vector2)transform.position + new Vector2(direction * (halfWidth + i * 8f), 2f) / Ppu,
                    EnemyShot.WaveDust
                );
            }
        }

        private Vector2 HandPosition => (Vector2)transform.position + new Vector2(HurlHand.x * direction, HurlHand.y) / Ppu;

        private void StopCrouch()
        {
            if (crouch != null)
            {
                StopCoroutine(crouch);
                crouch = null;
            }
        }

        private void Unsquash()
        {
            StopCrouch();
            if (sprite != null)
            {
                sprite.transform.localScale = Vector3.one;
            }
        }

        /// <summary>Back to patrolling (or lurking), with the cooldowns running.</summary>
        private void EndAttack()
        {
            state = behaviour == WalkerBehaviour.Ambush && !awake ? State.Lurk : State.Patrol;
            Unsquash();
            switch (attack)
            {
                case Attack.Swing:
                    attackCooldown = profile.cooldown;
                    break;
                case Attack.Pounce:
                    specialCooldown = behaviour == WalkerBehaviour.Lunge ? LungeCooldown : PounceCooldown;
                    break;
                case Attack.Hurl:
                    specialCooldown = HurlCooldown;
                    break;
                case Attack.Slam:
                    specialCooldown = SlamCooldown;
                    break;
            }
            Tint(Color.white);
            sprite.Play(state == State.Lurk ? "idle" : profile.walkAnim);
        }

        private void Face(int dir)
        {
            direction = dir;
            if (sprite != null)
            {
                sprite.FlipX = (dir > 0) == profile.facesLeft;
            }
        }

        private void Tint(Color color)
        {
            if (sprite != null)
            {
                sprite.Color = color;
            }
        }

        private void HurtHero()
        {
            if (passive)
            {
                return;
            }
            var size = (profile.body + new Vector2(4f, 0f)) / Ppu;
            var centre = (Vector2)transform.position + new Vector2(0f, profile.body.y / 2f / Ppu);
            EnemyPhysics.HurtHero(centre, size, contactDamage, transform.position, hits);
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
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, profile.body.y / 2f / Ppu));
            // A hit wakes an ambusher.
            awake = true;
            if (health <= 0)
            {
                Die();
                return true;
            }
            var attacking = state != State.Patrol && state != State.Chase && state != State.Lurk;
            if (attacking && profile.armour)
            {
                // Armour: the blow lands, the attack goes on.
                return true;
            }
            if (attacking)
            {
                EndAttack();
            }
            hurtLeft = HurtTime;
            Tint(TintHurt);
            sprite.Restart("hurt");
            var dir = transform.position.x >= from.x ? 1f : -1f;
            var push = profile.armour ? 0.5f : 1f;
            body.linearVelocity = new Vector2(Knockback.x * dir * push, Knockback.y * push) / Ppu;
            return true;
        }

        /// <summary>Death: a blink of stopped time, the death strip in a puff of dust with sparks, and the drops.</summary>
        private void Die()
        {
            dead = true;
            Audio.Sfx.Play("enemy_die");
            Feedback.HitStop(DeathHitStop);
            var size = profile.body;
            // A heavier body shakes the ground more.
            Feedback.Shake(0.25f + size.y / 250f);
            var centre = (Vector2)transform.position + new Vector2(0f, size.y / 2f / Ppu);
            Feedback.Spark(centre, DeathSparks);
            Feedback.Puff((Vector2)transform.position + new Vector2(0f, size.y / 4f / Ppu), profile.puff);
            box.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            healthBar.Hide();
            if (mark != null)
            {
                mark.Hide();
            }
            Unsquash();
            Tint(Color.white);
            Kills.Record(PlacementId);
            Died?.Invoke(this);
            // The body falls away from the hero, who is on the side it faces.
            StartCoroutine(EnemyDeath.Play(sprite, -direction, CorpseTime, () => Destroy(gameObject)));
            Drop(centre);
        }

        private void Drop(Vector2 centre)
        {
            Loot.Drop(centre, profile.loot);
            if (Random.value < profile.powerChance)
            {
                PowerUp.Spawn((Power)(int)profile.power, transform.position);
                return;
            }
            // A Cold Coin worn makes the dying give up more motes.
            if (Random.value >= profile.dropChance + Inventory.Bonus("mote_chance"))
            {
                return;
            }
            HealMote.Spawn(centre);
        }

        private enum State
        {
            Patrol,
            Chase,
            Lurk,
            Windup,
            Strike,
            Leap,
            Recover,
        }

        private enum Attack
        {
            Swing,
            Pounce,
            Hurl,
            Slam,
        }
    }
}
