using Descender.Combat;
using Descender.Fx;
using Descender.Pickups;
using Descender.Player;
using Descender.World;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// A boss: a big enemy that waits dormant in its room until the hero walks into
    /// the <see cref="Arena"/>, raises <see cref="Engaged"/>, and once the level calls
    /// <see cref="StartFight"/> fights in two phases until it is dead. It walks at the
    /// hero and swings when close, charges when he keeps his distance, and has one
    /// move of its own by body (brutes slam, angels leap and volley, imp kings summon);
    /// from half health it hastens and leaps after every charge. The last bosses turn
    /// twice (rampage, court, mirror). Hits never stagger it. One Boss prefab; the
    /// <see cref="BossProfile"/> picks the body. Origin at the feet. Port of
    /// scripts/enemies/boss.gd; the fight around it (doors, bar, dialogue) is the level's.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Boss : MonoBehaviour, IHittable, IEnemy, IPlacementReceiver
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float Gravity = 700f;
        public const float MaxFallSpeed = 300f;
        public const float HurtTime = 0.15f;
        public const float CorpseTime = 1.5f;
        public static readonly Color DeathSparks = new Color(1.0f, 0.85f, 0.5f);
        public static readonly Color DeathPuff = new Color(1.0f, 0.9f, 0.6f);
        public static readonly Color TintWindup = new Color(1.0f, 0.75f, 0.55f);
        public static readonly Color TintCharge = new Color(1.0f, 0.6f, 0.5f);
        public static readonly Color TintHurt = new Color(1.0f, 0.5f, 0.5f);
        public const float PhaseTwoAt = 0.5f;
        public const float PhaseTwoHaste = 0.7f;
        public const float AttackHeightTolerance = 32f;
        public const float ChargeRange = 90f;
        public const float ChargeTimeMax = 1.4f;
        public const float LeapVelocityY = 260f;
        public const float LeapTime = 0.9f;
        public const float SlamRange = 130f;
        public const float SlamWindup = 0.7f;
        public const float SlamRecover = 0.8f;
        public const float SlamCooldown = 3f;
        public static readonly Color TintSlam = new Color(1.0f, 0.6f, 0.45f);
        public const float VolleyMin = 56f;
        public const float VolleyMax = 220f;
        public const float VolleyVelocityY = 230f;
        public const float VolleyTime = 1.2f;
        public const float VolleyCooldown = 3.5f;
        public const float VolleySpread = 22f;
        public static readonly Color VolleyColor = new Color(1.0f, 0.95f, 0.7f);
        public static readonly Color VolleyCore = new Color(1.0f, 1.0f, 0.9f);
        public const int SummonCount = 2;
        public const float SummonSpacing = 48f;
        public const float PhaseThreeAt = 0.25f;
        public const float CastMin = 56f;
        public const float CastMax = 240f;
        public const float CastWindup = 0.35f;
        public const float CastRecover = 0.4f;
        public const float CastCooldown = 2.2f;
        public static readonly Color TintCast = new Color(1.0f, 0.95f, 0.75f);
        public const float SmiteRange = 110f;
        public const float SmiteVelocityY = 300f;
        public const float SmiteRiseTime = 0.55f;
        public const float SmiteFallTime = 1.5f;
        public const float SmiteRecover = 0.7f;
        public const float SmiteCooldown = 4f;
        public const int RampageBounces = 1;

        [SerializeField]
        private BossProfile profile;

        [SerializeField]
        private SpriteAnimator sprite;

        [Tooltip("The strips of every body a boss can wear, by character; the profile picks one.")]
        [SerializeField]
        private Wardrobe[] wardrobe = new Wardrobe[0];

        [Tooltip("Every boss's numbers, so a placement can pick one by its id.")]
        [SerializeField]
        private BossProfile[] profiles = new BossProfile[0];

        [Tooltip("The imp the imp kings call; a Walker prefab.")]
        [SerializeField]
        private Walker impPrefab;

        [Tooltip("The room the boss guards, in units: the hero walking into it starts the fight.")]
        [SerializeField]
        private Rect arena = new Rect(-8f, -4f, 16f, 8f);

        private Rigidbody2D body;
        private BoxCollider2D box;
        private readonly Collider2D[] hits = new Collider2D[4];
        private State state = State.Dormant;
        private int health;
        private int maxHealth = 1;
        private int direction = -1;
        private float stateLeft;
        private float hurtLeft;
        private float attackCooldown;
        private float chargeCooldown;
        private float moveCooldown;
        private float thirdCooldown;
        private float smiteCooldown;
        private bool leapAfterCharge;
        private bool volleyThrown;
        private bool phaseTwo;
        private bool phaseThree;
        private int bouncesLeft;
        private bool grounded;
        private bool dead;
        private bool engagedRaised;

        public event System.Action<Boss> Engaged;
        public event System.Action<int, int> HealthChanged;
        public event System.Action<Boss> Defeated;
        public event System.Action<IEnemy> Died;

        public BossProfile Profile => profile;
        public string Id => profile != null ? profile.name : "";
        public string DisplayName => profile != null ? profile.displayName : "";
        public string PlacementId { get; set; } = "";
        public bool IsDead => dead;
        public int Health => health;
        public int MaxHealth => maxHealth;
        public int Direction => direction;
        public bool IsFighting => state != State.Dormant && state != State.Dead;
        public bool IsDormant => state == State.Dormant;

        /// <summary>The room the boss guards, in units, about the scene origin.</summary>
        public Rect Arena
        {
            get => arena;
            set => arena = value;
        }

        /// <summary>The body's own move: slam, volley or summon, by character.</summary>
        public string Move
        {
            get
            {
                switch (profile != null ? profile.character : "")
                {
                    case "brute":
                        return "slam";
                    case "angel_guard":
                        return "volley";
                    case "imp":
                        return "summon";
                    default:
                        return "";
                }
            }
        }

        public string Third => profile != null ? profile.third : "";
        public bool InPhaseTwo => health <= Mathf.CeilToInt(maxHealth * PhaseTwoAt);
        public bool InPhaseThree => Third.Length > 0 && health <= Mathf.CeilToInt(maxHealth * PhaseThreeAt);

        /// <summary>The story flag a dead boss sets.</summary>
        public static string FlagFor(string id) => BossIds.FlagFor(id);

        /// <summary>The profile id for a boss's name as the map tool labels it ("Seraph Warden" -> "seraph_warden").</summary>
        public static string IdFor(string label)
        {
            var id = (label ?? "").ToLowerInvariant().Trim();
            id = id.Replace(":", "").Replace(",", "").Replace("'", "").Replace(".", "");
            return id.Replace(' ', '_');
        }

        /// <summary>The map's placement: the boss id is its character; the arena is the room about it.</summary>
        public void Configure(WorldPlacement placement)
        {
            PlacementId = placement.Id;
            var found = ProfileFor(placement.Character);
            if (found != null)
            {
                SetProfile(found);
            }
            else
            {
                Debug.LogWarning($"Boss: no profile named '{placement.Character}' on {name}.");
            }
            var centre = (Vector2)transform.position + new Vector2(0f, arena.height / 2f);
            arena = new Rect(centre.x - arena.width / 2f, centre.y - arena.height / 2f, arena.width, arena.height);
        }

        /// <summary>The profile asset of a boss id, from the ones the prefab carries.</summary>
        public BossProfile ProfileFor(string id)
        {
            foreach (var candidate in profiles)
            {
                if (candidate != null && candidate.name == id)
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Gives the boss its body before it wakes (the placement does this after Instantiate).</summary>
        public void SetProfile(BossProfile value)
        {
            profile = value;
            if (body != null)
            {
                ApplyProfile();
            }
        }

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
            if (profile == null)
            {
                Debug.LogError($"Boss: no profile set on {name}; using defaults.");
                profile = ScriptableObject.CreateInstance<BossProfile>();
            }
            if (transform.localScale.x < 0f)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
                direction = 1;
            }
            ApplyProfile();
        }

        private void ApplyProfile()
        {
            foreach (var outfit in wardrobe)
            {
                if (outfit.character == profile.character && outfit.strips != null && outfit.strips.Length > 0)
                {
                    sprite.SetStrips(outfit.strips);
                    break;
                }
            }
            maxHealth = Mathf.Max(profile.health, 1);
            health = maxHealth;
            box.size = profile.body / Ppu;
            box.offset = new Vector2(0f, profile.body.y / 2f / Ppu);
            sprite.transform.localScale = Vector3.one * profile.scale;
            Tint(Color.white);
            sprite.Play("idle");
            Face(direction);
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
            chargeCooldown = Mathf.Max(chargeCooldown - dt, 0f);
            moveCooldown = Mathf.Max(moveCooldown - dt, 0f);
            thirdCooldown = Mathf.Max(thirdCooldown - dt, 0f);
            smiteCooldown = Mathf.Max(smiteCooldown - dt, 0f);
            stateLeft -= dt;
            if (hurtLeft > 0f)
            {
                hurtLeft -= dt;
                if (hurtLeft <= 0f)
                {
                    Tint(Color.white);
                }
            }
            switch (state)
            {
                case State.Dormant:
                    velocity.x = 0f;
                    if (!engagedRaised && HeroInArena())
                    {
                        engagedRaised = true;
                        Engaged?.Invoke(this);
                    }
                    break;
                case State.Approach:
                    StepApproach(ref velocity);
                    break;
                case State.Windup:
                case State.Strike:
                case State.Recover:
                    StepSwing(ref velocity);
                    break;
                case State.ChargeWindup:
                    velocity.x = 0f;
                    if (stateLeft <= 0f)
                    {
                        StartCharge();
                    }
                    break;
                case State.Charge:
                    StepCharge(ref velocity);
                    break;
                case State.Leap:
                    if ((grounded && stateLeft < LeapTime - 0.1f) || stateLeft <= 0f)
                    {
                        velocity.x = 0f;
                        SetState(State.Approach, 0f);
                    }
                    break;
                case State.SlamWindup:
                    velocity.x = 0f;
                    if (stateLeft <= 0f)
                    {
                        Slam();
                    }
                    break;
                case State.SlamRecover:
                case State.CastRecover:
                case State.SmiteRecover:
                    velocity.x = 0f;
                    if (stateLeft <= 0f)
                    {
                        SetState(State.Approach, 0f);
                        sprite.Play(profile.walkAnim);
                    }
                    break;
                case State.Volley:
                    StepVolley(ref velocity);
                    break;
                case State.CastWindup:
                    velocity.x = 0f;
                    if (stateLeft <= 0f)
                    {
                        Cast();
                    }
                    break;
                case State.SmiteRise:
                    if (stateLeft <= 0f || velocity.y <= 0f)
                    {
                        SmiteFall(ref velocity);
                    }
                    break;
                case State.SmiteFall:
                    StepSmiteFall(ref velocity);
                    break;
            }
            body.linearVelocity = velocity / Ppu;
            if (IsFighting)
            {
                HurtHero();
            }
        }

        /// <summary>Called by the level once the intro is over: the boss wakes up and comes.</summary>
        public void StartFight()
        {
            if (state != State.Dormant || dead)
            {
                return;
            }
            SetState(State.Approach, 0f);
            sprite.Play(profile.walkAnim);
        }

        private float Haste => InPhaseTwo ? PhaseTwoHaste : 1f;

        private bool HeroInArena()
        {
            var hero = EnemyPhysics.LiveHero();
            return hero != null && arena.Contains((Vector2)hero.transform.position);
        }

        private void StepApproach(ref Vector2 velocity)
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero == null)
            {
                velocity.x = 0f;
                return;
            }
            var offset = ((Vector2)hero.transform.position - (Vector2)transform.position) * Ppu;
            Face(offset.x >= 0f ? 1 : -1);
            if (Mathf.Abs(offset.x) <= profile.attackRange)
            {
                velocity.x = 0f;
                if (attackCooldown <= 0f && Mathf.Abs(offset.y) <= AttackHeightTolerance)
                {
                    StartWindup();
                }
                return;
            }
            if (moveCooldown <= 0f && MoveWanted(offset))
            {
                StartMove(ref velocity);
                return;
            }
            if (InPhaseThree && ThirdWanted(offset))
            {
                StartThird(offset, ref velocity);
                return;
            }
            if (Mathf.Abs(offset.x) >= ChargeRange && chargeCooldown <= 0f)
            {
                StartChargeWindup();
                return;
            }
            velocity.x = direction * profile.speed / Haste;
        }

        private bool MoveWanted(Vector2 offset)
        {
            var dx = Mathf.Abs(offset.x);
            switch (Move)
            {
                case "slam":
                    return dx <= SlamRange && Mathf.Abs(offset.y) <= AttackHeightTolerance;
                case "volley":
                    return dx >= VolleyMin && dx <= VolleyMax && offset.y < 64f;
                default:
                    return false;
            }
        }

        private void StartMove(ref Vector2 velocity)
        {
            switch (Move)
            {
                case "slam":
                    SetState(State.SlamWindup, SlamWindup * Haste);
                    sprite.Pose("attack", 1);
                    Tint(TintSlam);
                    break;
                case "volley":
                    SetState(State.Volley, VolleyTime);
                    volleyThrown = false;
                    velocity = new Vector2(direction * 40f, VolleyVelocityY);
                    grounded = false;
                    sprite.Play("idle");
                    Tint(TintWindup);
                    Audio.Sfx.Play("jump", -4f);
                    break;
            }
        }

        /// <summary>The ground shakes and a tall wave runs along the floor the way it faces (and, in a rampage, the other way too).</summary>
        private void Slam()
        {
            sprite.Restart("attack");
            Tint(Color.white);
            Wave(direction, true);
            if (Third == "rampage" && InPhaseThree)
            {
                Wave(-direction, true);
            }
            Feedback.Shake(0.5f);
            Audio.Sfx.Play("land");
            var halfWidth = profile.body.x / 2f;
            for (var i = 0; i < 4; i++)
            {
                Feedback.Spark(
                    (Vector2)transform.position + new Vector2(direction * (halfWidth + i * 10f), 2f) / Ppu,
                    EnemyShot.WaveDust
                );
            }
            moveCooldown = SlamCooldown * Haste;
            SetState(State.SlamRecover, SlamRecover * Haste);
        }

        private void Wave(int dir, bool big)
        {
            var at = (Vector2)transform.position + new Vector2(dir * (profile.body.x / 2f + 8f) / Ppu, 0f);
            EnemyShot.Spawn(EnemyShot.Kind.Wave, at, new Vector2(dir, 0f), profile.attackDamage, big);
        }

        private bool ThirdWanted(Vector2 offset)
        {
            var dx = Mathf.Abs(offset.x);
            switch (Third)
            {
                case "court":
                    return thirdCooldown <= 0f && dx >= CastMin && dx <= CastMax;
                case "mirror":
                    if (smiteCooldown <= 0f && dx <= SmiteRange && offset.y <= 24f)
                    {
                        return true;
                    }
                    return thirdCooldown <= 0f && dx >= CastMin && dx <= CastMax;
                default:
                    return false;
            }
        }

        private void StartThird(Vector2 offset, ref Vector2 velocity)
        {
            if (Third == "mirror" && smiteCooldown <= 0f && Mathf.Abs(offset.x) <= SmiteRange)
            {
                StartSmite(offset, ref velocity);
                return;
            }
            // The hand goes up (the strip's first attack frame) and the light gathers.
            SetState(State.CastWindup, CastWindup * Haste);
            sprite.Pose("attack", 0);
            Tint(TintCast);
        }

        /// <summary>A bolt from where the boss stands, straight at the hero: hellfire, or the hero's own light for the Lowest.</summary>
        private void Cast()
        {
            sprite.Restart("attack");
            Tint(Color.white);
            var from =
                (Vector2)transform.position
                + new Vector2(direction * (profile.body.x / 2f + 4f), profile.body.y / 2f) / Ppu;
            var hero = EnemyPhysics.LiveHero();
            var at =
                hero != null ? (Vector2)hero.transform.position + new Vector2(0f, 12f / Ppu) : from + Vector2.right;
            var aim = (at - from).normalized;
            if (aim == Vector2.zero)
            {
                aim = new Vector2(direction, 0f);
            }
            var mirror = Third == "mirror";
            EnemyShot.Spawn(
                EnemyShot.Kind.Bolt,
                from,
                aim,
                1,
                false,
                mirror ? VolleyColor : EnemyShot.BoltColor,
                mirror ? VolleyCore : EnemyShot.BoltCore
            );
            Audio.Sfx.Play("swing", -2f);
            Feedback.Spark(from, mirror ? VolleyColor : EnemyShot.BoltColor);
            thirdCooldown = CastCooldown * Haste;
            SetState(State.CastRecover, CastRecover * Haste);
        }

        /// <summary>The Lowest's Smite: straight up, over the hero, and straight down.</summary>
        private void StartSmite(Vector2 offset, ref Vector2 velocity)
        {
            Face(offset.x >= 0f ? 1 : -1);
            velocity = new Vector2(Mathf.Clamp(offset.x / SmiteRiseTime, -200f, 200f), SmiteVelocityY);
            grounded = false;
            SetState(State.SmiteRise, SmiteRiseTime);
            sprite.Play(sprite.Has("jump") ? "jump" : "idle");
            Tint(TintCast);
            Audio.Sfx.Play("jump", -4f);
        }

        private void SmiteFall(ref Vector2 velocity)
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero != null)
            {
                Face(hero.transform.position.x >= transform.position.x ? 1 : -1);
            }
            velocity = new Vector2(0f, -MaxFallSpeed);
            SetState(State.SmiteFall, SmiteFallTime);
            sprite.Pose("attack", 0);
        }

        /// <summary>The landing: the ground shakes and a wave runs out each way.</summary>
        private void StepSmiteFall(ref Vector2 velocity)
        {
            velocity.y = -MaxFallSpeed;
            if (stateLeft <= 0f)
            {
                SetState(State.Approach, 0f);
                Tint(Color.white);
                return;
            }
            if (!grounded)
            {
                return;
            }
            velocity.y = 0f;
            sprite.Restart("attack");
            Tint(Color.white);
            Wave(1, false);
            Wave(-1, false);
            Feedback.Shake(0.6f);
            Audio.Sfx.Play("land");
            for (var i = 0; i < 3; i++)
            {
                Feedback.Spark((Vector2)transform.position + new Vector2((i - 1) * 14f, 2f) / Ppu, EnemyShot.WaveDust);
            }
            smiteCooldown = SmiteCooldown * Haste;
            SetState(State.SmiteRecover, SmiteRecover * Haste);
        }

        /// <summary>In the air: at the top of the leap three bolts of light go down at the hero in a fan; landing ends it.</summary>
        private void StepVolley(ref Vector2 velocity)
        {
            if (!volleyThrown && velocity.y <= 0f)
            {
                volleyThrown = true;
                ThrowVolley();
            }
            if ((grounded && stateLeft < VolleyTime - 0.1f) || stateLeft <= 0f)
            {
                velocity.x = 0f;
                Tint(Color.white);
                moveCooldown = VolleyCooldown * Haste;
                SetState(State.Approach, 0f);
                sprite.Play(profile.walkAnim);
            }
        }

        private void ThrowVolley()
        {
            sprite.Restart("attack");
            Tint(Color.white);
            var from = (Vector2)transform.position + new Vector2(direction * 8f, profile.body.y / 2f) / Ppu;
            var hero = EnemyPhysics.LiveHero();
            var at = hero != null ? (Vector2)hero.transform.position + new Vector2(0f, 12f / Ppu) : from + Vector2.down;
            var aim = (at - from).normalized;
            if (aim == Vector2.zero)
            {
                aim = Vector2.down;
            }
            for (var i = 0; i < 3; i++)
            {
                var rotated = Quaternion.Euler(0f, 0f, (i - 1) * VolleySpread) * aim;
                EnemyShot.Spawn(EnemyShot.Kind.Bolt, from, rotated, 1, false, VolleyColor, VolleyCore);
            }
            Audio.Sfx.Play("swing", -2f);
            Feedback.Spark(from, VolleyColor);
        }

        /// <summary>The imp kings' move: two imps come running when the fight turns.</summary>
        private void Summon(string behaviour)
        {
            if (dead || impPrefab == null)
            {
                return;
            }
            for (var i = 0; i < SummonCount; i++)
            {
                var at = (Vector2)transform.position + new Vector2((i * 2 - 1) * SummonSpacing, 8f) / Ppu;
                var imp = Instantiate(impPrefab, at, Quaternion.identity, transform.parent);
                imp.SetBehaviour(behaviour);
                imp.DeathY = transform.position.y - 2000f / Ppu;
                Feedback.Puff(at + new Vector2(0f, 12f / Ppu), TintCharge);
            }
        }

        private void StartWindup()
        {
            SetState(State.Windup, profile.windup * Haste);
            sprite.Pose("attack", 0);
            Tint(TintWindup);
        }

        private void StepSwing(ref Vector2 velocity)
        {
            velocity.x = 0f;
            if (state == State.Strike)
            {
                var centre =
                    (Vector2)transform.position
                    + new Vector2((profile.body.x / 2f + profile.reach.x / 2f) * direction, profile.reach.y / 2f) / Ppu;
                EnemyPhysics.HurtHero(centre, profile.reach / Ppu, profile.attackDamage, transform.position, hits);
            }
            if (stateLeft > 0f)
            {
                return;
            }
            switch (state)
            {
                case State.Windup:
                    SetState(State.Strike, profile.strike);
                    Tint(Color.white);
                    sprite.Restart("attack");
                    break;
                case State.Strike:
                    SetState(State.Recover, profile.recover * Haste);
                    sprite.Play("idle");
                    break;
                case State.Recover:
                    attackCooldown = profile.cooldown * Haste;
                    SetState(State.Approach, 0f);
                    sprite.Play(profile.walkAnim);
                    break;
            }
        }

        private void StartChargeWindup()
        {
            SetState(State.ChargeWindup, profile.chargeWindup * Haste);
            sprite.Play("idle");
            Tint(TintCharge);
        }

        private void StartCharge()
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero != null)
            {
                Face(hero.transform.position.x >= transform.position.x ? 1 : -1);
            }
            SetState(State.Charge, ChargeTimeMax);
            Tint(Color.white);
            sprite.Play(profile.walkAnim);
            sprite.SpeedScale = 2f;
            leapAfterCharge = InPhaseTwo;
            bouncesLeft = Third == "rampage" && InPhaseThree ? RampageBounces : 0;
            Audio.Sfx.Play("dash", -4f);
        }

        /// <summary>The rush: straight ahead at full speed until a wall or the time runs out.</summary>
        private void StepCharge(ref Vector2 velocity)
        {
            velocity.x = direction * profile.chargeSpeed;
            var onWall = EnemyPhysics.IsOnWall(box, direction, hits);
            if (onWall && bouncesLeft > 0)
            {
                // A rampage turns at the wall and comes straight back.
                bouncesLeft--;
                Face(-direction);
                stateLeft = ChargeTimeMax;
                Feedback.Shake(0.3f);
                Audio.Sfx.Play("dash", -4f);
                return;
            }
            if (onWall || stateLeft <= 0f)
            {
                sprite.SpeedScale = 1f;
                chargeCooldown = profile.chargeCooldown;
                if (leapAfterCharge && EnemyPhysics.LiveHero() != null)
                {
                    StartLeap(ref velocity);
                }
                else
                {
                    velocity.x = 0f;
                    SetState(State.Approach, 0f);
                    Feedback.Shake(0.25f);
                }
            }
        }

        /// <summary>Phase two: a jump that lands by the hero.</summary>
        private void StartLeap(ref Vector2 velocity)
        {
            leapAfterCharge = false;
            var hero = EnemyPhysics.LiveHero();
            var offset = ((Vector2)hero.transform.position - (Vector2)transform.position) * Ppu;
            Face(offset.x >= 0f ? 1 : -1);
            var airTime = 2f * LeapVelocityY / Gravity;
            velocity = new Vector2(Mathf.Clamp(offset.x / airTime, -220f, 220f), LeapVelocityY);
            grounded = false;
            SetState(State.Leap, LeapTime);
            sprite.Play("idle");
            Audio.Sfx.Play("jump", -4f);
        }

        private void SetState(State newState, float duration)
        {
            state = newState;
            stateLeft = duration;
        }

        private void Face(int dir)
        {
            direction = dir;
            sprite.FlipX = (dir > 0) == profile.facesLeft;
        }

        private void Tint(Color color)
        {
            sprite.Color = profile.tint * color;
        }

        private void HurtHero()
        {
            var size = (profile.body + new Vector2(4f, 0f)) / Ppu;
            var centre = (Vector2)transform.position + new Vector2(0f, profile.body.y / 2f / Ppu);
            EnemyPhysics.HurtHero(centre, size, profile.contactDamage, transform.position, hits);
        }

        /// <summary>Hits flash and count, but never stagger the boss.</summary>
        public bool Hit(int damage, Vector2 from)
        {
            if (dead || state == State.Dormant)
            {
                return false;
            }
            health = Mathf.Max(health - damage, 0);
            HealthChanged?.Invoke(health, maxHealth);
            Audio.Sfx.Play("hit");
            Feedback.Flash(this, 0.1f);
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, profile.body.y / 2f / Ppu));
            hurtLeft = HurtTime;
            Tint(TintHurt);
            if (health <= 0)
            {
                Die();
                return true;
            }
            if (!phaseTwo && InPhaseTwo)
            {
                EnterPhaseTwo();
            }
            if (!phaseThree && InPhaseThree)
            {
                EnterPhaseThree();
            }
            return true;
        }

        /// <summary>The turn of the fight: a roar, a flash, a shake, the body swelling; the imp kings call their imps.</summary>
        private void EnterPhaseTwo()
        {
            phaseTwo = true;
            Audio.Sfx.Play("roar");
            Feedback.Flash(this, 0.35f);
            Feedback.Shake(0.5f);
            Feedback.HitStop(0.1f);
            StartCoroutine(Pulse(1.15f, 0.12f, 0.25f));
            if (Move == "summon")
            {
                Summon("chase");
            }
        }

        /// <summary>The last turn: the roar again, and the False King calls his court (two imps that pounce).</summary>
        private void EnterPhaseThree()
        {
            phaseThree = true;
            Audio.Sfx.Play("roar");
            Feedback.Flash(this, 0.45f);
            Feedback.Shake(0.6f);
            Feedback.HitStop(0.12f);
            StartCoroutine(Pulse(1.25f, 0.15f, 0.3f));
            if (Third == "court")
            {
                Summon("pounce");
            }
        }

        private System.Collections.IEnumerator Pulse(float by, float outTime, float inTime)
        {
            var baseScale = Vector3.one * profile.scale;
            yield return Tween.Scale(sprite.transform, baseScale, baseScale * by, outTime);
            yield return Tween.Scale(sprite.transform, baseScale * by, baseScale, inTime);
        }

        /// <summary>Death: the death strip, a long shake, a puff of light, heal motes and the hoard.</summary>
        private void Die()
        {
            dead = true;
            SetState(State.Dead, 0f);
            Audio.Sfx.Play("enemy_die");
            Audio.Sfx.Play("level_complete", -6f);
            Feedback.Shake(0.9f);
            Feedback.HitStop(0.15f);
            var centre = (Vector2)transform.position + new Vector2(0f, profile.body.y / 2f / Ppu);
            for (var i = 0; i < 4; i++)
            {
                Feedback.Spark(
                    centre + new Vector2(Random.Range(-12f, 12f), Random.Range(-20f, 20f)) / Ppu,
                    DeathSparks
                );
            }
            box.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            sprite.SpeedScale = 1f;
            Tint(Color.white);
            Feedback.Puff(centre, DeathPuff);
            Kills.Record(PlacementId);
            if (profile != null && !string.IsNullOrEmpty(profile.name))
            {
                Story.StoryFlags.Set(FlagFor(profile.name));
            }
            Died?.Invoke(this);
            StartCoroutine(EnemyDeath.Play(sprite, -direction, CorpseTime, () => Destroy(gameObject)));
            for (var i = 0; i < profile.motes; i++)
            {
                HealMote.Spawn(centre + new Vector2((i - 0.5f) * 20f / Ppu, 0f));
            }
            Loot.Drop((Vector2)transform.position + new Vector2(0f, 8f / Ppu), profile.loot);
            Defeated?.Invoke(this);
        }

        /// <summary>One character's strips, so one Boss prefab can wear any body.</summary>
        [System.Serializable]
        public struct Wardrobe
        {
            public string character;
            public SpriteAnimator.Strip[] strips;
        }

        private enum State
        {
            Dormant,
            Approach,
            Windup,
            Strike,
            Recover,
            ChargeWindup,
            Charge,
            Leap,
            SlamWindup,
            SlamRecover,
            Volley,
            CastWindup,
            CastRecover,
            SmiteRise,
            SmiteFall,
            SmiteRecover,
            Dead,
        }
    }
}
