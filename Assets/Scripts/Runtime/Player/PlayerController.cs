using System.Collections.Generic;
using Descender.Combat;
using Descender.Economy;
using Descender.Fx;
using Descender.Story;
using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// The hero: run, jump with variable height, coyote time and an input buffer,
    /// the sword, a short dash and limited inner healing, plus the angel abilities he
    /// unlocks (Wings, Light Dash, Wall Grip, Holy Light, Glide, Smite), hurt, death
    /// and respawn at the last checkpoint, the timed power-ups and the permanent
    /// upgrades. Numbers come from <see cref="PlayerTuning"/> in level pixels. Input is
    /// read in Update into a small buffer and consumed in FixedUpdate; the body is a
    /// dynamic Rigidbody2D with gravity scale 0 and this script applies gravity so
    /// rise, fall, glide and wall slide can differ. The origin is at the feet. Port of
    /// scripts/player.gd; the sprite side is <see cref="PlayerSprite"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour, IHeroMenuActions
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;

        /// <summary>How many fixed steps a dying hero waits before respawning on his own.</summary>
        private const float RespawnDelayExtra = 0f;

        /// <summary>How deep in the solid world, in pixels and for how many steps, counts as crushed.</summary>
        private const float CrushDepthPixels = 4f;
        private const int CrushSteps = 3;

        /// <summary>How many half units to either side a crushed hero looks for room.</summary>
        private const int CrushSearchSteps = 12;

        /// <summary>Rising faster than this, in units a second, he is passing through a platform, not standing on it.</summary>
        private const float RisingThrough = 0.5f;

        [SerializeField]
        private PlayerTuning tuning;

        [SerializeField]
        private SpriteAnimator sprite;

        [SerializeField]
        private LightBolt boltPrefab;

        [Tooltip(
            "Come back at the last checkpoint on its own after the death animation; off when the level flow respawns him."
        )]
        [SerializeField]
        private bool autoRespawn = true;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private ContactFilter2D groundFilter;
        private ContactFilter2D worldOnlyFilter;
        private ContactFilter2D wallFilter;
        private ContactFilter2D enemyFilter;
        private ContactFilter2D barrierFilter;
        private readonly Collider2D[] hits = new Collider2D[8];
        private readonly RaycastHit2D[] lineHits = new RaycastHit2D[8];
        private readonly List<IHittable> attackHit = new List<IHittable>(8);
        private readonly HashSet<Save.Ability> abilities = new HashSet<Save.Ability>();
        private static PhysicsMaterial2D slick;

        private Input.IHeroInput inputSource;

        // Buffered input (set in Update, consumed in FixedUpdate).
        private float moveX;
        private bool downHeld;
        private bool jumpHeld;
        private float jumpBufferLeft;
        private float dashBufferLeft;
        private float attackBufferLeft;
        private float castBufferLeft;
        private float healBufferLeft;

        // State.
        private PlayerState state = PlayerState.Idle;
        private int facing = 1;
        private bool grounded;
        private bool wasGrounded;
        private IMovingGround movingGround;
        private Triggers.Climbable climbing;
        private Triggers.Water water;
        private float strokeLockLeft;
        private float climbLockLeft;
        private bool upHeld;
        private bool unsteadyGround;
        private Vector2 carry;
        private Vector2 wind;
        private Vector2 windAsked;
        private float windGain;
        private Vector2 sentVelocity;
        private int crushedSteps;
        private float lastFallSpeed;
        private float coyoteLeft;
        private float stateLeft;
        private float dashCooldownLeft;
        private float castCooldownLeft;
        private float wallJumpLockLeft;
        private float dropLeft;
        private float invulnerableLeft;
        private float wingFlashLeft;
        private float landLeft;
        private float doubleJumpLeft;
        private float safeGroundTime;
        private bool dashAvailable = true;
        private bool dashFromFloor;
        private bool airJumpAvailable;
        private bool onWallGrip;
        private int wallDir;
        private bool gliding;
        private bool phasing;
        private bool jumpRising;
        private bool slamLanded;
        private bool ledgesIgnored;
        private bool barriersIgnored;
        private int health;
        private int maxHealth;
        private int healCharges;
        private int maxHealCharges;
        private readonly float[] powerLeft = new float[3];
        private Vector2 respawnPosition;
        private Vector2 lastSafePosition;

        public event System.Action<int, int> HealthChanged;
        public event System.Action<int, int> HealChargesChanged;
        public event System.Action<Save.Ability> AbilityUnlocked;
        public event System.Action<UpgradeKind> UpgradeCollected;
        public event System.Action PowerChanged;
        public event System.Action<Power> PowerGranted;
        public event System.Action PurseChanged;
        public event System.Action<string> LootCollected;
        public event System.Action XpChanged;
        public event System.Action<int> LeveledUp;
        public event System.Action Died;
        public event System.Action Respawned;
        public event System.Action Landed;

        /// <summary>The Smite landed: where and how wide, for the secret walls that break under it.</summary>
        public static event System.Action<Vector2, Vector2> Smote;

        /// <summary>The hero in the scene, for enemies and pickups; null before he spawns.</summary>
        public static PlayerController Current { get; private set; }

        public PlayerTuning Tuning => tuning;

        /// <summary>Hands him another tuning to play with: the live copy of the dev console's `tune`.</summary>
        public void UseTuning(PlayerTuning value)
        {
            if (value != null)
            {
                tuning = value;
            }
        }

        public PlayerState State => state;
        public bool IsGrounded => grounded;
        public bool IsDashing => state == PlayerState.Dash;
        public bool IsAlive => state != PlayerState.Dead;
        public bool IsTravelling { get; private set; }
        public bool IsGrippingWall => onWallGrip;
        public int WallDir => wallDir;
        public bool IsGliding => gliding;
        public bool SlamLanded => slamLanded;

        /// <summary>How many attacks he has started; a new swing can follow the last with no tick between them.</summary>
        public int AttackCount { get; private set; }
        public int Facing => facing;
        public int Health => health;
        public int MaxHealth => maxHealth;
        public int HealCharges => healCharges;
        public int MaxHealCharges => maxHealCharges;
        public float InvulnerableLeft => invulnerableLeft;
        public bool WingFlashing => wingFlashLeft > 0f;
        public bool DoubleJumpShowing => doubleJumpLeft > 0f;
        public bool LandingShowing => landLeft > 0f;
        public float MoveInput => moveX;
        public Vector2 RespawnPosition => respawnPosition;
        public Vector2 LastSafePosition => lastSafePosition;
        public Vector2 Velocity => body.linearVelocity;

        /// <summary>What the moving ground under him adds to his speed, in units a second; zero on still ground.</summary>
        public Vector2 CarryVelocity => carry;

        /// <summary>The middle of his body is in water.</summary>
        public bool IsInWater => water != null;

        /// <summary>He has hold of something to climb.</summary>
        public bool IsClimbing => state == PlayerState.Climb;

        /// <summary>What the wind he is in adds to his speed, in level pixels a second; zero in still air.</summary>
        public Vector2 WindPixels => wind * Ppu;

        /// <summary>The hero's velocity in level pixels per second.</summary>
        public Vector2 VelocityPixels => body.linearVelocity * Ppu;

        /// <summary>Falling below this y (units) counts as death; the level sets it.</summary>
        public float DeathY { get; set; } = float.NegativeInfinity;

        /// <summary>Set by the level at its end: input is ignored and nothing can hurt him, but he still lands.</summary>
        public bool Frozen { get; private set; }

        /// <summary>Set while a dialogue box is open: input is ignored and nothing can hurt him.</summary>
        public bool Talking { get; set; }

        /// <summary>
        /// Where the hero reads his input: the InputRouter in the scene while this is null,
        /// otherwise what a script set (a ScriptedInput for the scripted playtest).
        /// </summary>
        public Input.IHeroInput InputSource
        {
            get => inputSource;
            set => inputSource = value;
        }

        /// <summary>Whether the Wings' second jump is still to be had before he next lands.</summary>
        public bool AirJumpAvailable => airJumpAvailable;

        // --- Abilities, powers, stats -------------------------------------------

        /// <summary>Whether he can use an ability now: one he owns, unless the dev console forced it on or off.</summary>
        public bool Has(Save.Ability ability) => LiveTuning.Has(ability, abilities.Contains(ability));

        /// <summary>Whether the ability is his to keep, which is what a save records.</summary>
        public bool Owns(Save.Ability ability) => abilities.Contains(ability);

        /// <summary>Unlocks an angel power for the rest of the run. Unlocking one he already has does nothing.</summary>
        public void Grant(Save.Ability ability)
        {
            if (abilities.Add(ability))
            {
                AbilityUnlocked?.Invoke(ability);
            }
        }

        public void SetAbilities(IEnumerable<Save.Ability> granted)
        {
            abilities.Clear();
            foreach (var ability in granted)
            {
                abilities.Add(ability);
            }
        }

        public IEnumerable<Save.Ability> Abilities => abilities;

        public bool HasPower(Power power) => powerLeft[(int)power] > 0f;

        /// <summary>Seconds left on a timed power-up (infinity for the Halo), 0 without it.</summary>
        public float PowerTimeLeft(Power power) => powerLeft[(int)power];

        /// <summary>A timed power-up. Picking up one he already has restarts its time.</summary>
        public void GrantPower(Power power)
        {
            powerLeft[(int)power] = power == Power.Halo ? float.PositiveInfinity : HeroPowers.Duration;
            PowerChanged?.Invoke();
            PowerGranted?.Invoke(power);
        }

        private float StatBonus(string stat) => Inventory.Bonus(stat) + Skills.Bonus(stat);

        private float StatFactor(string stat) => Inventory.Factor(stat) * Skills.Factor(stat);

        /// <summary>The sword: its base, Pell's forge, the blade worn, the Edge skill and the Lit Blade.</summary>
        public int AttackDamage =>
            tuning.attackDamage
            + Upgrades.SwordBonus()
            + (int)StatBonus("damage")
            + (HasPower(Power.Blade) ? tuning.bladeBonusDamage : 0);

        /// <summary>How long a swing takes: a heavy blade swings slower, a knife quicker.</summary>
        public float AttackTime() => tuning.attackTime * StatFactor("attack_time");

        public int HealAmount() => tuning.healAmount + (int)StatBonus("heal");

        /// <summary>Hearts the worn armour and the Iron Skin skill take off a hit.</summary>
        public int Guard() => (int)StatBonus("guard");

        public float DashCooldown() =>
            tuning.dashCooldown
            * (HasPower(Power.Zeal) ? tuning.zealCooldownMultiplier : 1f)
            * StatFactor("dash_cooldown");

        public float RunSpeed() =>
            tuning.runSpeed * (HasPower(Power.Zeal) ? tuning.zealSpeedMultiplier : 1f) * StatFactor("speed");

        /// <summary>The difficulty's blow less the guard, never below one heart.</summary>
        public int GuardedDamage(int amount) => Mathf.Max(Save.SaveManager.ScaledDamage(amount) - Guard(), 1);

        /// <summary>How much of the dash cooldown has passed, 0 to 1; 0 while the one dash per airtime is spent.</summary>
        public float DashReadiness
        {
            get
            {
                if (!dashAvailable && !grounded)
                {
                    return 0f;
                }
                var cooldown = DashCooldown();
                return cooldown <= 0f ? 1f : 1f - dashCooldownLeft / cooldown;
            }
        }

        public bool DashReady => dashAvailable && dashCooldownLeft <= 0f;

        // --- Unity messages -------------------------------------------------------

        private void Awake()
        {
            Layers.ApplyCollisionMatrix();
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            }
            tuning = LiveTuning.For(tuning);
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteAnimator>();
            }
            if (sprite == null)
            {
                sprite = SpriteAnimator.CreateChild(transform);
            }
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            if (slick == null)
            {
                slick = new PhysicsMaterial2D("Slick") { friction = 0f, bounciness = 0f };
            }
            box.sharedMaterial = slick;
            groundFilter = Layers.Solid(Layers.GroundMask);
            worldOnlyFilter = Layers.Solid(Layers.WorldMask);
            wallFilter = Layers.Solid(Layers.WorldMask);
            enemyFilter = Layers.Any(Layers.EnemyMask);
            barrierFilter = Layers.Solid(Layers.LightBarrierMask);
            respawnPosition = transform.position;
            lastSafePosition = transform.position;
            SetMaximums();
            health = maxHealth;
            healCharges = maxHealCharges;
        }

        private void Start()
        {
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
            SetLedgesIgnored(false);
            SetBarriersIgnored(false);
        }

        private void Update()
        {
            var input = inputSource;
            if (input == null)
            {
                input = Input.InputRouter.Instance;
            }
            if (input == null || InputBlocked || state == PlayerState.Dead)
            {
                moveX = 0f;
                downHeld = false;
                upHeld = false;
                jumpHeld = false;
                return;
            }
            moveX = input.MoveX;
            downHeld = input.DownHeld;
            upHeld = input.UpHeld;
            jumpHeld = input.JumpHeld;
            // Sampled every frame so a press near the end of an attack, dash, heal or
            // hurt is buffered instead of dropped.
            if (input.JumpPressed)
            {
                jumpBufferLeft = tuning.inputBufferTime;
            }
            if (input.DashPressed)
            {
                dashBufferLeft = tuning.inputBufferTime;
            }
            if (input.AttackPressed)
            {
                attackBufferLeft = tuning.inputBufferTime;
            }
            if (input.CastPressed)
            {
                castBufferLeft = tuning.inputBufferTime;
            }
            if (input.HealPressed)
            {
                healBufferLeft = tuning.inputBufferTime;
            }
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            TickTimers(dt);
            if (IsTravelling)
            {
                body.linearVelocity = Vector2.zero;
                carry = Vector2.zero;
                wind = Vector2.zero;
                windAsked = Vector2.zero;
                sentVelocity = Vector2.zero;
                return;
            }
            // What the wind and the ground under him added last step comes off first, so
            // every state works with his own speed.
            body.linearVelocity -= WindLeft();
            body.linearVelocity -= carry;

            wasGrounded = grounded;
            grounded = CheckGround();
            water = Triggers.Water.At(box.bounds.center);
            if (water != null && state != PlayerState.Dash)
            {
                // In water the dash is always ready, and a stroke stands in for the air jump.
                dashAvailable = true;
                airJumpAvailable = true;
            }
            if (grounded && state != PlayerState.Dash)
            {
                dashAvailable = true;
                airJumpAvailable = true;
            }

            switch (state)
            {
                case PlayerState.Dead:
                    StepDead(dt);
                    break;
                case PlayerState.Dash:
                    StepDash(dt);
                    break;
                case PlayerState.Attack:
                    StepAttack(dt);
                    break;
                case PlayerState.Heal:
                    StepHeal(dt);
                    break;
                case PlayerState.Hurt:
                    StepHurt(dt);
                    break;
                case PlayerState.Cast:
                    StepCast(dt);
                    break;
                case PlayerState.Climb:
                    StepClimb(dt);
                    break;
                case PlayerState.Slam:
                    StepSlam(dt);
                    break;
                default:
                    StepNormal(dt);
                    break;
            }

            UpdatePhasing();
            UpdateDropping();
            lastFallSpeed = -VelocityPixels.y;

            if (grounded && !wasGrounded)
            {
                if (lastFallSpeed >= tuning.landSoundSpeed && state != PlayerState.Dead)
                {
                    Audio.Sfx.Play("land", -6f);
                    Feedback.Dust(transform.position);
                }
                if (lastFallSpeed >= tuning.landAnimSpeed)
                {
                    landLeft = tuning.landAnimTime;
                    sprite.ClearOneShot();
                }
                Landed?.Invoke();
            }
            TrackSafeGround(dt);
            RideGround();
            CheckCrushed();
            TakeWind(dt);
            if (transform.position.y < DeathY && state != PlayerState.Dead && !Frozen)
            {
                Die();
            }
        }

        private void TickTimers(float dt)
        {
            Tick(ref coyoteLeft, dt);
            Tick(ref jumpBufferLeft, dt);
            Tick(ref dashBufferLeft, dt);
            Tick(ref attackBufferLeft, dt);
            Tick(ref castBufferLeft, dt);
            Tick(ref healBufferLeft, dt);
            Tick(ref dashCooldownLeft, dt);
            Tick(ref castCooldownLeft, dt);
            Tick(ref stateLeft, dt);
            Tick(ref invulnerableLeft, dt);
            Tick(ref wingFlashLeft, dt);
            Tick(ref dropLeft, dt);
            Tick(ref climbLockLeft, dt);
            Tick(ref strokeLockLeft, dt);
            Tick(ref wallJumpLockLeft, dt);
            Tick(ref landLeft, dt);
            Tick(ref doubleJumpLeft, dt);
            TickPowers(dt);
        }

        // --- States ----------------------------------------------------------------

        private bool InputBlocked => Frozen || Talking;

        private void StepNormal(float dt)
        {
            if (TakeHold())
            {
                StepClimb(dt);
                return;
            }
            var velocity = VelocityPixels;
            var inputDir = InputBlocked ? 0f : moveX;
            if (wallJumpLockLeft > 0f && (int)Mathf.Sign(inputDir) == wallDir && inputDir != 0f)
            {
                // Just sprang off a wall: steering back towards it is ignored for a
                // moment, so the wall cannot be climbed by rapid jumps.
                inputDir = 0f;
            }
            if (inputDir != 0f)
            {
                facing = inputDir > 0f ? 1 : -1;
            }
            if (grounded)
            {
                coyoteLeft = tuning.coyoteTime;
            }
            UpdateWallGrip(inputDir, ref velocity);
            UpdateGlide();

            ApplyGravity(ref velocity, dt);
            ApplyHorizontal(ref velocity, inputDir, dt, water != null ? tuning.waterMoveShare : 1f);

            if (jumpBufferLeft > 0f && grounded && WantsDrop())
            {
                StartDrop(ref velocity);
            }
            else if (jumpBufferLeft > 0f && water != null)
            {
                // With his head out the surface is ground enough for a full jump; under
                // it jump is a stroke, and a press too soon after the last one waits.
                if (box.bounds.max.y > water.Surface)
                {
                    velocity.y = tuning.JumpVelocity;
                    jumpBufferLeft = 0f;
                    coyoteLeft = 0f;
                    jumpRising = true;
                    grounded = false;
                    Audio.Sfx.Play("jump");
                }
                else if (strokeLockLeft <= 0f)
                {
                    velocity.y = tuning.StrokeVelocity;
                    jumpBufferLeft = 0f;
                    coyoteLeft = 0f;
                    jumpRising = false;
                    grounded = false;
                    strokeLockLeft = tuning.waterStrokeGap;
                }
            }
            else if (jumpBufferLeft > 0f && coyoteLeft > 0f)
            {
                velocity.y = tuning.JumpVelocity;
                jumpBufferLeft = 0f;
                coyoteLeft = 0f;
                jumpRising = true;
                grounded = false;
                Audio.Sfx.Play("jump");
            }
            else if (jumpBufferLeft > 0f && onWallGrip)
            {
                // Wall Grip: spring away from the wall; the air jump and dash come back.
                velocity = new Vector2(tuning.wallJumpVelocity.x * -wallDir, tuning.wallJumpVelocity.y);
                facing = -wallDir;
                wallJumpLockLeft = tuning.wallJumpLock;
                onWallGrip = false;
                jumpBufferLeft = 0f;
                jumpRising = true;
                airJumpAvailable = true;
                dashAvailable = true;
                Audio.Sfx.Play("jump");
            }
            else if (jumpBufferLeft > 0f && airJumpAvailable && Has(Save.Ability.Wings))
            {
                // Wings: a second jump from mid-air, with the same variable height.
                velocity.y = tuning.DoubleJumpVelocity;
                jumpBufferLeft = 0f;
                airJumpAvailable = false;
                jumpRising = true;
                wingFlashLeft = tuning.wingFlashTime;
                doubleJumpLeft = tuning.doubleJumpAnimTime;
                sprite.ClearOneShot();
                Audio.Sfx.Play("double_jump");
            }
            else if (jumpBufferLeft > 0f && velocity.y < 0f && Has(Save.Ability.Glide))
            {
                // Glide: the press opens the wings; holding it keeps them open.
                gliding = true;
                jumpBufferLeft = 0f;
            }
            else if (jumpRising && velocity.y <= 0f)
            {
                jumpRising = false;
            }
            else if (jumpRising && !InputBlocked && !jumpHeld)
            {
                // Variable jump height: letting go while rising cuts the jump short.
                velocity.y *= tuning.jumpCutMultiplier;
                jumpRising = false;
            }

            CorrectCorners(ref velocity, inputDir, dt);
            body.linearVelocity = velocity / Ppu;

            if (InputBlocked)
            {
                state = grounded ? PlayerState.Idle : PlayerState.Fall;
                return;
            }
            if (dashBufferLeft > 0f && dashCooldownLeft <= 0f && dashAvailable)
            {
                StartDash();
                return;
            }
            if (attackBufferLeft > 0f && !grounded && downHeld && Has(Save.Ability.Smite))
            {
                StartSlam();
                return;
            }
            if (attackBufferLeft > 0f)
            {
                StartAttack();
                return;
            }
            if (castBufferLeft > 0f && castCooldownLeft <= 0f && Has(Save.Ability.HolyLight))
            {
                StartCast();
                return;
            }
            if (healBufferLeft > 0f && grounded && healCharges > 0 && health < maxHealth)
            {
                healBufferLeft = 0f;
                StartHeal();
                return;
            }
            if (!grounded)
            {
                state = velocity.y > 0f ? PlayerState.Jump : PlayerState.Fall;
            }
            else
            {
                state = Mathf.Abs(velocity.x) > 5f ? PlayerState.Run : PlayerState.Idle;
            }
        }

        /// <summary>
        /// Up or down with his feet in something to climb takes hold of it: up when there
        /// is some of it above his feet, down when there is some below and he is not
        /// standing on solid ground (on a ledge at its top he is, and climbs down through).
        /// </summary>
        private bool TakeHold()
        {
            if (InputBlocked || climbLockLeft > 0f || (!upHeld && !downHeld))
            {
                return false;
            }
            var slack = 2f / Ppu;
            var feet = Feet();
            var found = Triggers.Climbable.At(feet, slack);
            if (found == null)
            {
                return false;
            }
            var area = found.Area;
            var up = upHeld && feet.y < area.yMax - slack;
            var down = downHeld && feet.y > area.yMin + slack && (!grounded || OnLedge());
            if (!up && !down)
            {
                return false;
            }
            SetState(PlayerState.Climb, 0f);
            climbing = found;
            // Up may be the jump key too: that press took hold and is not also a jump.
            jumpBufferLeft = 0f;
            jumpRising = false;
            airJumpAvailable = true;
            dashAvailable = true;
            SetLedgesIgnored(true);
            return true;
        }

        /// <summary>The solid world is under his feet, whatever ledges he is passing through.</summary>
        private bool OnSolidGround()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 2f / Ppu, 2f / Ppu);
            var centre = new Vector2(bounds.center.x, bounds.min.y);
            return Physics2D.OverlapBox(centre, size, 0f, worldOnlyFilter, hits) > 0;
        }

        private Vector2 Feet()
        {
            var bounds = box.bounds;
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        /// <summary>
        /// Climbing: no gravity, up and down at the climb speed and sideways across it.
        /// Jump with a side held leaps off, jump alone lets go with a hop (not while up
        /// is held, which may be the same key), and each end lets go: the top with his
        /// feet at its edge, the bottom on the ground or out of it.
        /// </summary>
        private void StepClimb(float dt)
        {
            var slack = 2f / Ppu;
            var feet = Feet();
            var held = climbing != null && climbing.isActiveAndEnabled;
            if (!held || InputBlocked || Triggers.Climbable.At(feet, slack) != climbing)
            {
                LetGo(Vector2.zero);
                return;
            }
            if (moveX != 0f)
            {
                facing = moveX > 0f ? 1 : -1;
            }
            if (jumpBufferLeft > 0f)
            {
                jumpBufferLeft = 0f;
                if (moveX != 0f)
                {
                    LetGo(new Vector2(moveX * RunSpeed(), tuning.JumpVelocity * tuning.climbLeap));
                    jumpRising = true;
                    Audio.Sfx.Play("jump");
                    return;
                }
                if (!upHeld)
                {
                    LetGo(new Vector2(0f, tuning.climbHopSpeed));
                    return;
                }
            }
            var area = climbing.Area;
            var way = (upHeld ? 1f : 0f) - (downHeld ? 1f : 0f);
            if (way > 0f && feet.y >= area.yMax - 0.5f / Ppu)
            {
                LetGo(Vector2.zero);
                return;
            }
            if (way < 0f && (OnSolidGround() || feet.y <= area.yMin))
            {
                LetGo(Vector2.zero);
                return;
            }
            var velocity = new Vector2(moveX * tuning.climbSideSpeed, way * tuning.climbSpeed);
            // No further than the top, where he steps off, and not out of its sides.
            velocity.y = Mathf.Min(velocity.y, (area.yMax - feet.y) * Ppu / dt);
            var next = feet.x + velocity.x / Ppu * dt;
            if (next < area.xMin || next > area.xMax)
            {
                velocity.x = 0f;
            }
            body.linearVelocity = velocity / Ppu;
        }

        /// <summary>Lets go of what he climbs, with a speed in pixels a second.</summary>
        private void LetGo(Vector2 velocity)
        {
            climbing = null;
            climbLockLeft = tuning.climbRegrabTime;
            state = velocity.y > 0f ? PlayerState.Jump : PlayerState.Fall;
            body.linearVelocity = velocity / Ppu;
        }

        private void StartDash()
        {
            dashFromFloor = grounded;
            var light = Has(Save.Ability.LightDash);
            SetState(PlayerState.Dash, light ? tuning.lightDashTime : tuning.dashTime);
            if (light)
            {
                phasing = true;
            }
            dashBufferLeft = 0f;
            dashCooldownLeft = DashCooldown();
            dashAvailable = false;
            body.linearVelocity = new Vector2(DashSpeed() * facing, 0f) / Ppu;
            Audio.Sfx.Play("dash");
        }

        /// <summary>The dash's speed where he is: slower in water.</summary>
        private float DashSpeed() => tuning.dashSpeed * (water != null ? tuning.waterDashShare : 1f);

        private void StepDash(float dt)
        {
            var velocity = new Vector2(DashSpeed() * facing, 0f);
            if (stateLeft <= 0f)
            {
                velocity.x = tuning.dashEndSpeed * facing;
                // A dash that ran off a ledge still allows the late jump it would have had when walking off.
                if (dashFromFloor)
                {
                    coyoteLeft = tuning.coyoteTime;
                }
                state = PlayerState.Idle;
            }
            body.linearVelocity = velocity / Ppu;
        }

        private void StartAttack()
        {
            SetState(PlayerState.Attack, AttackTime());
            AttackCount++;
            attackBufferLeft = 0f;
            attackHit.Clear();
            sprite.PlayOnce("attack");
            if (sprite != null)
            {
                // A slow or quick blade plays the strip to match.
                sprite.SpeedScale = tuning.attackTime / Mathf.Max(AttackTime(), 0.001f);
            }
            Audio.Sfx.Play("swing");
        }

        private void StepAttack(float dt)
        {
            // The hero keeps moving while he swings (slower on the ground) but keeps
            // facing the way the swing started.
            var velocity = VelocityPixels;
            var speedMultiplier = grounded ? tuning.attackMoveMultiplier : 1f;
            if (grounded)
            {
                coyoteLeft = tuning.coyoteTime;
            }
            ApplyGravity(ref velocity, dt);
            ApplyHorizontal(ref velocity, InputBlocked ? 0f : moveX, dt, speedMultiplier);
            body.linearVelocity = velocity / Ppu;

            var total = AttackTime();
            var scale = total / tuning.attackTime;
            var elapsed = total - stateLeft;
            var active = elapsed >= tuning.attackActiveFrom * scale && elapsed <= tuning.attackActiveUntil * scale;
            if (active)
            {
                // A longer blade widens the hitbox away from the body.
                var size = tuning.attackShapeSize;
                size.x *= StatFactor("reach");
                var extra = (size.x - tuning.attackShapeSize.x) / 2f;
                var centre =
                    (Vector2)transform.position
                    + new Vector2((tuning.attackOffset.x + extra) * facing, tuning.attackOffset.y) / Ppu;
                StrikeEnemies(centre, size / Ppu, AttackDamage, attackHit);
            }
            if (stateLeft <= 0f)
            {
                EndAttack();
                state = PlayerState.Idle;
            }
        }

        /// <summary>Hurts every enemy in a box once per swing; a landed hit stops time for a moment.</summary>
        private void StrikeEnemies(Vector2 centre, Vector2 size, int damage, List<IHittable> already)
        {
            var count = Physics2D.OverlapBox(centre, size, 0f, enemyFilter, hits);
            for (var i = 0; i < count; i++)
            {
                var target = FindHittable(hits[i]);
                if (target == null || already.Contains(target))
                {
                    continue;
                }
                already.Add(target);
                if (target.Hit(damage, transform.position))
                {
                    Feedback.HitStop(tuning.hitStopTime);
                }
            }
        }

        private static IHittable FindHittable(Collider2D collider)
        {
            if (
                collider.attachedRigidbody != null
                && collider.attachedRigidbody.TryGetComponent<IHittable>(out var onBody)
            )
            {
                return onBody;
            }
            return collider.GetComponentInParent<IHittable>();
        }

        private void EndAttack()
        {
            if (sprite != null)
            {
                sprite.SpeedScale = 1f;
            }
        }

        // --- Holy Light and Smite -------------------------------------------------

        private void StartCast()
        {
            SetState(PlayerState.Cast, tuning.castTime);
            castBufferLeft = 0f;
            castCooldownLeft = tuning.castCooldown;
            var at = (Vector2)transform.position + new Vector2(tuning.castOffset.x * facing, tuning.castOffset.y) / Ppu;
            LightBolt.Spawn(boltPrefab, at, facing);
            Audio.Sfx.Play("swing", -3f);
            Feedback.Spark(at, LightBolt.Glow);
        }

        /// <summary>The hero keeps his momentum while the bolt leaves his hand.</summary>
        private void StepCast(float dt)
        {
            var velocity = VelocityPixels;
            if (grounded)
            {
                coyoteLeft = tuning.coyoteTime;
            }
            ApplyGravity(ref velocity, dt);
            ApplyHorizontal(ref velocity, 0f, dt);
            body.linearVelocity = velocity / Ppu;
            if (stateLeft <= 0f)
            {
                state = PlayerState.Idle;
            }
        }

        private void StartSlam()
        {
            SetState(PlayerState.Slam, 0f);
            attackBufferLeft = 0f;
            slamLanded = false;
            attackHit.Clear();
            body.linearVelocity = new Vector2(0f, -tuning.slamSpeed) / Ppu;
            dropLeft = 0f;
            Audio.Sfx.Play("dash", -2f);
        }

        /// <summary>Falls at full speed until the ground, then the shock: everything around the feet is hurt once.</summary>
        private void StepSlam(float dt)
        {
            if (!slamLanded)
            {
                body.linearVelocity = new Vector2(0f, -tuning.slamSpeed) / Ppu;
                if (grounded)
                {
                    LandSlam();
                }
                return;
            }
            var velocity = VelocityPixels;
            ApplyGravity(ref velocity, dt);
            velocity.x = 0f;
            body.linearVelocity = velocity / Ppu;
            var elapsed = tuning.slamRecoverTime - stateLeft;
            if (elapsed <= tuning.slamHitTime)
            {
                var centre = (Vector2)transform.position + new Vector2(0f, tuning.slamReach.y / 2f) / Ppu;
                StrikeEnemies(centre, tuning.slamReach / Ppu, tuning.slamDamage, attackHit);
            }
            if (stateLeft <= 0f)
            {
                EndAttack();
                state = PlayerState.Idle;
            }
        }

        private void LandSlam()
        {
            slamLanded = true;
            stateLeft = tuning.slamRecoverTime;
            Smote?.Invoke(transform.position, tuning.slamReach);
            Audio.Sfx.Play("land");
            Feedback.Shake(0.6f);
            Feedback.HitStop(0.06f);
            for (var i = 0; i < 3; i++)
            {
                Feedback.Spark(
                    (Vector2)transform.position + new Vector2((i - 1) * 18f, 4f) / Ppu,
                    PlayerSprite.TintSlam
                );
            }
        }

        // --- Heal, hurt, dead -------------------------------------------------------

        private void StartHeal()
        {
            SetState(PlayerState.Heal, tuning.healTime);
            var velocity = VelocityPixels;
            velocity.x = 0f;
            body.linearVelocity = velocity / Ppu;
        }

        /// <summary>Starts a healing charge if the hero stands still with one to spend and a heart to fill.</summary>
        public bool Heal()
        {
            if (healCharges <= 0 || health >= maxHealth || !grounded || InputBlocked)
            {
                return false;
            }
            if (state != PlayerState.Idle && state != PlayerState.Run)
            {
                return false;
            }
            StartHeal();
            return true;
        }

        private void StepHeal(float dt)
        {
            var velocity = VelocityPixels;
            ApplyGravity(ref velocity, dt);
            velocity.x = 0f;
            body.linearVelocity = velocity / Ppu;
            if (stateLeft <= 0f)
            {
                healCharges--;
                health = Mathf.Min(health + HealAmount(), maxHealth);
                HealthChanged?.Invoke(health, maxHealth);
                HealChargesChanged?.Invoke(healCharges, maxHealCharges);
                Audio.Sfx.Play("heal");
                state = PlayerState.Idle;
            }
        }

        private void StepHurt(float dt)
        {
            var velocity = VelocityPixels;
            ApplyGravity(ref velocity, dt);
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, tuning.groundFriction * 0.5f * dt);
            body.linearVelocity = velocity / Ppu;
            if (stateLeft <= 0f)
            {
                state = PlayerState.Idle;
            }
        }

        private void StepDead(float dt)
        {
            var velocity = VelocityPixels;
            ApplyGravity(ref velocity, dt);
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, tuning.groundFriction * dt);
            body.linearVelocity = velocity / Ppu;
            if (stateLeft <= 0f && autoRespawn)
            {
                Respawn();
            }
        }

        private void SetState(PlayerState newState, float duration)
        {
            EndAttack();
            onWallGrip = false;
            gliding = false;
            state = newState;
            stateLeft = duration;
            landLeft = 0f;
            sprite.ClearOneShot();
        }

        // --- Physics helpers ----------------------------------------------------------

        private void ApplyGravity(ref Vector2 velocity, float dt)
        {
            if (grounded)
            {
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                }
                return;
            }
            if (water != null)
            {
                gliding = false;
                if (velocity.y < -tuning.waterSinkSpeed)
                {
                    // Coming in fast, the water brakes him to the sink speed in a moment.
                    velocity.y = Mathf.MoveTowards(velocity.y, -tuning.waterSinkSpeed, 2f * tuning.FallGravity * dt);
                    return;
                }
                var pull = (velocity.y < 0f ? tuning.FallGravity : tuning.Gravity) * tuning.waterGravity;
                velocity.y = Mathf.Max(velocity.y - pull * dt, -tuning.waterSinkSpeed);
                return;
            }
            if (onWallGrip)
            {
                velocity.y = Mathf.Max(velocity.y - tuning.Gravity * dt, -tuning.wallSlideSpeed);
                return;
            }
            var gravity = velocity.y < 0f ? tuning.FallGravity : tuning.Gravity;
            if (gliding)
            {
                velocity.y = Mathf.Max(
                    velocity.y - gravity * tuning.glideGravityMultiplier * dt,
                    -tuning.glideFallSpeed
                );
                return;
            }
            var airborne = state == PlayerState.Jump || state == PlayerState.Fall;
            if (airborne && !InputBlocked && Mathf.Abs(velocity.y) < tuning.apexSpeedThreshold && jumpHeld)
            {
                gravity *= tuning.apexGravityMultiplier;
            }
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -tuning.maxFallSpeed);
        }

        private void ApplyHorizontal(ref Vector2 velocity, float inputDir, float dt, float speedMultiplier = 1f)
        {
            if (inputDir != 0f)
            {
                var turning = Mathf.Abs(velocity.x) > 0.01f && Mathf.Sign(velocity.x) == -Mathf.Sign(inputDir);
                float accel;
                if (turning)
                {
                    accel = grounded ? tuning.groundTurn : tuning.airTurn;
                }
                else
                {
                    accel = grounded ? tuning.groundAccel : tuning.airAccel;
                }
                var topSpeed = inputDir * RunSpeed() * speedMultiplier;
                velocity.x = Mathf.MoveTowards(velocity.x, topSpeed, accel * dt);
            }
            else
            {
                var friction = grounded ? tuning.groundFriction : tuning.airFriction;
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, friction * dt);
            }
        }

        /// <summary>
        /// The two near misses a jump forgives. Rising with his head under the corner of a
        /// ceiling by no more than <see cref="PlayerTuning.cornerCorrection"/> pixels, he is
        /// moved aside by that much and keeps rising. Coming down against the side of a
        /// ledge he is steering at, with his feet no more than
        /// <see cref="PlayerTuning.ledgeCatch"/> pixels under its top, he is lifted onto it.
        /// </summary>
        private void CorrectCorners(ref Vector2 velocity, float inputDir, float dt)
        {
            if (grounded || onWallGrip)
            {
                return;
            }
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 1f / Ppu, bounds.size.y - 1f / Ppu);
            Vector2 centre = bounds.center;
            if (velocity.y > 0f)
            {
                var up = new Vector2(0f, (velocity.y * dt + 1f) / Ppu);
                if (!Blocked(centre + up, size))
                {
                    return;
                }
                for (var pixels = 1; pixels <= tuning.cornerCorrection; pixels++)
                {
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var aside = new Vector2(side * pixels / Ppu, 0f);
                        if (!Blocked(centre + aside, size) && !Blocked(centre + aside + up, size))
                        {
                            // The check is half a pixel slack, so half a pixel more leaves him clear.
                            Nudge(aside + new Vector2(side * 0.5f / Ppu, 0f));
                            return;
                        }
                    }
                }
                return;
            }
            var steering = inputDir != 0f && velocity.x != 0f && Mathf.Sign(inputDir) == Mathf.Sign(velocity.x);
            if (!steering)
            {
                return;
            }
            var ahead = new Vector2(Mathf.Sign(velocity.x) * (Mathf.Abs(velocity.x) * dt + 1f) / Ppu, 0f);
            if (!Blocked(centre + ahead, size))
            {
                return;
            }
            for (var pixels = 1; pixels <= tuning.ledgeCatch; pixels++)
            {
                var lift = new Vector2(0f, pixels / Ppu);
                if (!Blocked(centre + lift, size) && !Blocked(centre + lift + ahead, size))
                {
                    Nudge(lift + new Vector2(0f, 0.5f / Ppu));
                    velocity.y = 0f;
                    return;
                }
            }
        }

        private bool Blocked(Vector2 centre, Vector2 size) =>
            Physics2D.OverlapBox(centre, size, 0f, worldOnlyFilter, hits) > 0;

        /// <summary>Moves him by a few pixels at once, outside the physics step.</summary>
        private void Nudge(Vector2 by)
        {
            body.position += by;
            transform.position += (Vector3)by;
        }

        /// <summary>Wall Grip: holding towards a wall while in the air and falling grips it.</summary>
        private void UpdateWallGrip(float inputDir, ref Vector2 velocity)
        {
            if (!Has(Save.Ability.WallGrip) || grounded || InputBlocked)
            {
                onWallGrip = false;
                return;
            }
            var pushing = inputDir == 0f ? 0 : (int)Mathf.Sign(inputDir);
            if (pushing == 0 || (velocity.y > 0f && !onWallGrip))
            {
                onWallGrip = false;
                return;
            }
            if (CheckWall(pushing))
            {
                if (!onWallGrip)
                {
                    velocity.y = Mathf.Max(velocity.y, -tuning.wallSlideSpeed);
                    airJumpAvailable = true;
                    dashAvailable = true;
                }
                onWallGrip = true;
                wallDir = pushing;
                jumpRising = false;
            }
            else
            {
                onWallGrip = false;
            }
        }

        /// <summary>Glide lasts while the button is held and the hero falls.</summary>
        private void UpdateGlide()
        {
            if (gliding && (grounded || onWallGrip || InputBlocked || !jumpHeld))
            {
                gliding = false;
            }
        }

        /// <summary>Down and jump on a one-way ledge drops through it; the same press on solid ground is a jump.</summary>
        private bool WantsDrop() => !InputBlocked && downHeld && OnLedge();

        private void StartDrop(ref Vector2 velocity)
        {
            jumpBufferLeft = 0f;
            coyoteLeft = 0f;
            dropLeft = tuning.dropTime;
            SetLedgesIgnored(true);
            grounded = false;
            // Out of the ledge's thin strip at once.
            velocity.y = Mathf.Min(velocity.y, -40f);
        }

        /// <summary>
        /// The ledges come back once the drop is over, and once a slam has landed; but not
        /// while he is still partly in one, or it would lift him back onto itself.
        /// </summary>
        private void UpdateDropping()
        {
            var passing = state == PlayerState.Climb || (state == PlayerState.Slam && !slamLanded);
            var ignore = dropLeft > 0f || passing;
            SetLedgesIgnored(ignore || (ledgesIgnored && state != PlayerState.Dead && InsideLedge()));
        }

        private bool InsideLedge()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 2f / Ppu, bounds.size.y - 2f / Ppu);
            var count = Physics2D.OverlapBox(bounds.center, size, 0f, groundFilter, hits);
            for (var i = 0; i < count; i++)
            {
                if (hits[i].gameObject.layer == Layers.Ledge)
                {
                    return true;
                }
            }
            return false;
        }

        private void SetLedgesIgnored(bool ignore)
        {
            if (ledgesIgnored == ignore || Layers.Player < 0 || Layers.Ledge < 0)
            {
                return;
            }
            ledgesIgnored = ignore;
            Physics2D.IgnoreLayerCollision(Layers.Player, Layers.Ledge, ignore);
        }

        /// <summary>Light barriers are passed through while light dashing, and until the hero is clear of one he ended inside.</summary>
        private void UpdatePhasing()
        {
            var dashing = state == PlayerState.Dash && Has(Save.Ability.LightDash);
            if (dashing)
            {
                SetBarriersIgnored(true);
            }
            else if (phasing)
            {
                var bounds = box.bounds;
                var inside = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, barrierFilter, hits) > 0;
                if (!inside)
                {
                    phasing = false;
                    SetBarriersIgnored(false);
                }
            }
        }

        private void SetBarriersIgnored(bool ignore)
        {
            if (barriersIgnored == ignore || Layers.Player < 0 || Layers.LightBarrier < 0)
            {
                return;
            }
            barriersIgnored = ignore;
            Physics2D.IgnoreLayerCollision(Layers.Player, Layers.LightBarrier, ignore);
        }

        /// <summary>
        /// Moving ground carries him: its speed is added to his own for the coming step.
        /// When he leaves it, by a jump or off its end, he keeps a share of its sideways
        /// speed, and a rising platform adds to the jump up to a cap.
        /// </summary>
        private void RideGround()
        {
            // Rising through a one-way platform from below touches it without standing on it.
            var own = body.linearVelocity;
            var standing = grounded && own.y <= RisingThrough;
            var riding = standing && movingGround != null && state != PlayerState.Dead;
            var next = riding ? movingGround.Velocity : Vector2.zero;
            if (riding && own.y > 0f)
            {
                // What a rising platform's push left over would lift him off it.
                body.linearVelocity = new Vector2(own.x, 0f);
            }
            if (!grounded && carry != Vector2.zero)
            {
                var kept = carry * tuning.platformMomentum;
                kept.y = Mathf.Clamp(kept.y, 0f, tuning.platformLiftMax / Ppu);
                body.linearVelocity += kept;
            }
            body.linearVelocity += next;
            carry = next;
        }

        /// <summary>
        /// A wind blows on him for the step now being taken: called by what blows (a
        /// <see cref="Triggers.ForceZone"/>) before his own step, every step it lasts. The
        /// wind is a speed in level pixels a second that is added to his own, which he
        /// picks up, and loses again, at `gain` pixels a second squared (0 for at once).
        /// Several winds add up.
        /// </summary>
        public void Blow(Vector2 pixelsPerSecond, float gain)
        {
            windAsked += pixelsPerSecond / Ppu;
            windGain = Mathf.Max(gain, 0f) / Ppu;
        }

        /// <summary>
        /// The wind's part of the speed the body has now. What the world took from the
        /// speed he was sent off with (a wall he was blown against, the floor under a
        /// downdraught) is taken from the wind's part first, so being held there is not
        /// counted as him moving the other way.
        /// </summary>
        private Vector2 WindLeft()
        {
            if (wind == Vector2.zero)
            {
                return Vector2.zero;
            }
            var lost = sentVelocity - body.linearVelocity;
            return new Vector2(Left(wind.x, lost.x), Left(wind.y, lost.y));
        }

        private static float Left(float blown, float lost)
        {
            if (blown > 0f && lost > 0f)
            {
                return blown - Mathf.Min(blown, lost);
            }
            if (blown < 0f && lost < 0f)
            {
                return blown - Mathf.Max(blown, lost);
            }
            return blown;
        }

        /// <summary>Moves the wind on him towards what was asked for this step and adds it to his speed.</summary>
        private void TakeWind(float dt)
        {
            var asked = state == PlayerState.Dead ? Vector2.zero : windAsked;
            wind = windGain > 0f ? Vector2.MoveTowards(wind, asked, windGain * dt) : asked;
            windAsked = Vector2.zero;
            body.linearVelocity += wind;
            sentVelocity = body.linearVelocity;
        }

        /// <summary>
        /// Squeezed into the solid world by something that moves (a platform coming down on
        /// him, or pressing him to a wall): he is hurt as by a hazard and put beside it.
        /// </summary>
        private void CheckCrushed()
        {
            if (state == PlayerState.Dead)
            {
                crushedSteps = 0;
                return;
            }
            var bounds = box.bounds;
            var inset = 2f * CrushDepthPixels / Ppu;
            var size = new Vector2(bounds.size.x - inset, bounds.size.y - inset);
            var squeezed = Physics2D.OverlapBox(bounds.center, size, 0f, worldOnlyFilter, hits) > 0;
            crushedSteps = squeezed ? crushedSteps + 1 : 0;
            if (crushedSteps < CrushSteps)
            {
                return;
            }
            crushedSteps = 0;
            var spot = FreeSpotBeside();
            if (!TakeHazardDamage(1, spot))
            {
                PlaceAt(spot);
            }
        }

        /// <summary>
        /// The nearest place to either side where his whole body is clear of the solid world
        /// and that he could have walked to: nothing that stands still is in between, so he
        /// is never put through a wall. Else the last safe ground.
        /// </summary>
        private Vector2 FreeSpotBeside()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 1f / Ppu, bounds.size.y - 1f / Ppu);
            Vector2 feet = transform.position;
            Vector2 toCentre = (Vector2)bounds.center - feet;
            for (var i = 1; i <= CrushSearchSteps; i++)
            {
                // A little above where he is too: he may have been pressed into the floor.
                for (var lift = 0; lift <= 2; lift++)
                {
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var spot = feet + new Vector2(side * i * 0.5f, lift * 0.25f);
                        var clear = Physics2D.OverlapBox(spot + toCentre, size, 0f, worldOnlyFilter, hits) == 0;
                        if (clear && !StillWorldBetween(feet + toCentre, spot + toCentre))
                        {
                            return spot;
                        }
                    }
                }
            }
            return lastSafePosition;
        }

        /// <summary>Whether solid world that does not move lies on the line between two points.</summary>
        private bool StillWorldBetween(Vector2 from, Vector2 to)
        {
            var count = Physics2D.Linecast(from, to, worldOnlyFilter, lineHits);
            for (var i = 0; i < count; i++)
            {
                var owner = lineHits[i].rigidbody;
                if (owner == null || owner.bodyType == RigidbodyType2D.Static)
                {
                    return true;
                }
            }
            return false;
        }

        private void TrackSafeGround(float dt)
        {
            var steady = movingGround == null && !unsteadyGround;
            if (grounded && steady && (state == PlayerState.Idle || state == PlayerState.Run))
            {
                safeGroundTime += dt;
                if (safeGroundTime >= tuning.safeGroundTime)
                {
                    lastSafePosition = transform.position;
                }
            }
            else
            {
                safeGroundTime = 0f;
            }
        }

        private bool CheckGround()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 2f / Ppu, 2f / Ppu);
            var centre = new Vector2(bounds.center.x, bounds.min.y);
            var filter = ledgesIgnored ? worldOnlyFilter : groundFilter;
            var count = Physics2D.OverlapBox(centre, size, 0f, filter, hits);
            movingGround = null;
            unsteadyGround = false;
            for (var i = 0; i < count; i++)
            {
                unsteadyGround |= hits[i].GetComponentInParent<IUnsteadyGround>() != null;
            }
            for (var i = 0; i < count && movingGround == null; i++)
            {
                var under = hits[i].attachedRigidbody;
                if (under != null && under.TryGetComponent<IMovingGround>(out var moving))
                {
                    movingGround = moving;
                }
            }
            return count > 0;
        }

        private bool OnLedge()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 2f / Ppu, 4f / Ppu);
            var count = Physics2D.OverlapBox(new Vector2(bounds.center.x, bounds.min.y), size, 0f, groundFilter, hits);
            for (var i = 0; i < count; i++)
            {
                if (hits[i].gameObject.layer == Layers.Ledge)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckWall(int side)
        {
            var bounds = box.bounds;
            var size = new Vector2(2f / Ppu, bounds.size.y - 4f / Ppu);
            var x = side > 0 ? bounds.max.x + 0.5f / Ppu : bounds.min.x - 0.5f / Ppu;
            return Physics2D.OverlapBox(new Vector2(x, bounds.center.y), size, 0f, wallFilter, hits) > 0;
        }

        private static void Tick(ref float timer, float dt)
        {
            if (timer > 0f)
            {
                timer = Mathf.Max(0f, timer - dt);
            }
        }

        // --- Upgrades and power-ups -----------------------------------------------------

        /// <summary>A permanent pickup: raises the maximum and fills it, and records the pickup as a story flag.</summary>
        public void ApplyUpgrade(UpgradeKind kind, string flag)
        {
            if (!string.IsNullOrEmpty(flag))
            {
                StoryFlags.Set(flag);
            }
            RaiseMaximum(kind);
            switch (kind)
            {
                case UpgradeKind.Heart:
                    health = maxHealth;
                    break;
                case UpgradeKind.Vial:
                    healCharges = maxHealCharges;
                    break;
            }
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
            UpgradeCollected?.Invoke(kind);
        }

        /// <summary>Sets the maximums from the upgrade flags in the save, for a loaded hero, and fills them.</summary>
        public void RestoreUpgrades()
        {
            SetMaximums();
            health = maxHealth;
            healCharges = maxHealCharges;
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
        }

        /// <summary>The maximums from scratch: the base, the difficulty, the upgrade flags, the equipment and the skills.</summary>
        private void SetMaximums()
        {
            maxHealth = tuning.maxHealth + Save.SaveManager.BonusHearts() + Upgrades.Count(UpgradeKind.Heart);
            maxHealCharges = tuning.maxHealCharges + Save.SaveManager.BonusCharges() + Upgrades.Count(UpgradeKind.Vial);
            maxHealth = Mathf.Max(maxHealth + (int)StatBonus("hearts"), 1);
            maxHealCharges = Mathf.Max(maxHealCharges + (int)StatBonus("charges"), 0);
        }

        private void RaiseMaximum(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Heart:
                    maxHealth++;
                    break;
                case UpgradeKind.Vial:
                    maxHealCharges++;
                    break;
            }
        }

        private void TickPowers(float dt)
        {
            var expired = false;
            for (var i = 0; i < powerLeft.Length; i++)
            {
                if (powerLeft[i] > 0f && !float.IsPositiveInfinity(powerLeft[i]))
                {
                    powerLeft[i] -= dt;
                    if (powerLeft[i] <= 0f)
                    {
                        powerLeft[i] = 0f;
                        expired = true;
                    }
                }
            }
            if (expired)
            {
                PowerChanged?.Invoke();
            }
        }

        /// <summary>Turns a hit aside with the Halo, if the hero has one. True when the hit was absorbed.</summary>
        private bool SpendHalo()
        {
            if (!HasPower(Power.Halo))
            {
                return false;
            }
            powerLeft[(int)Power.Halo] = 0f;
            PowerChanged?.Invoke();
            invulnerableLeft = tuning.invulnerableTime;
            Audio.Sfx.Play("checkpoint", -4f);
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, 20f) / Ppu, new Color(1f, 0.85f, 0.4f));
            Feedback.Flash(this, 0.12f);
            return true;
        }

        private void ClearPowers()
        {
            var any = false;
            for (var i = 0; i < powerLeft.Length; i++)
            {
                any |= powerLeft[i] > 0f;
                powerLeft[i] = 0f;
            }
            if (any)
            {
                PowerChanged?.Invoke();
            }
        }

        /// <summary>A Warden's Halo worn gives a Halo at every rest (and respawn).</summary>
        private void RestHalo()
        {
            if (Inventory.Bonus("halo_at_rest") > 0f && !HasPower(Power.Halo))
            {
                powerLeft[(int)Power.Halo] = float.PositiveInfinity;
                PowerChanged?.Invoke();
            }
        }

        // --- The pack ---------------------------------------------------------------------

        /// <summary>Light or tally bones picked up (see LightDrop).</summary>
        public void CollectCurrency(string currency, int amount)
        {
            Inventory.AddCurrency(currency, amount);
            PurseChanged?.Invoke();
        }

        /// <summary>An item picked up off the ground (see LootDrop). A boss's unique loot sets its flag so it is never dropped again.</summary>
        public void CollectLoot(string itemId)
        {
            Inventory.Add(itemId);
            if (Items.IsUnique(itemId))
            {
                StoryFlags.Set(Items.LootFlag(itemId));
            }
            LootCollected?.Invoke(itemId);
        }

        /// <summary>Uses a consumable from the pack. Returns "" when it was used, else why it was not.</summary>
        public string UseItem(string itemId)
        {
            if (!Inventory.Has(itemId) || !Items.IsConsumable(itemId))
            {
                return "Nothing to use";
            }
            switch (itemId)
            {
                case "flask":
                    if (healCharges >= maxHealCharges)
                    {
                        return "The healing charges are full";
                    }
                    healCharges++;
                    HealChargesChanged?.Invoke(healCharges, maxHealCharges);
                    Audio.Sfx.Play("heal");
                    break;
                case "halo_shard":
                    GrantPower(Power.Halo);
                    break;
                case "blade_oil":
                    GrantPower(Power.Blade);
                    break;
                case "zeal_draught":
                    GrantPower(Power.Zeal);
                    break;
                default:
                    return "Nothing to use";
            }
            Inventory.Remove(itemId);
            return "";
        }

        /// <summary>Experience from a kill; enough of it is a level and a skill point.</summary>
        public void GainXp(int amount)
        {
            var levels = Skills.AddXp(amount);
            if (levels <= 0 && amount <= 0)
            {
                return;
            }
            XpChanged?.Invoke();
            if (levels > 0)
            {
                Audio.Sfx.Play("checkpoint", -2f);
                Feedback.Spark((Vector2)transform.position + new Vector2(0f, 20f) / Ppu, new Color(1f, 0.95f, 0.7f));
                LeveledUp?.Invoke(Skills.Level());
            }
        }

        /// <summary>Wears a piece from the pack, or takes it off if it is worn.</summary>
        public void ToggleEquipment(string itemId)
        {
            if (Inventory.IsEquipped(itemId))
            {
                Inventory.Unequip(Items.SlotOf(itemId));
            }
            else if (!Inventory.Equip(itemId))
            {
                return;
            }
            RefreshEquipment();
        }

        /// <summary>Sets the maximums again after the equipment changed, without filling them.</summary>
        public void RefreshEquipment()
        {
            SetMaximums();
            health = Mathf.Min(health, maxHealth);
            healCharges = Mathf.Min(healCharges, maxHealCharges);
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
        }

        // --- Damage, death, checkpoints -------------------------------------------------

        /// <summary>True while nothing can hurt the hero: dead, dashing, blinking after a hit, talking or frozen.</summary>
        public bool CanBeHurt =>
            !InputBlocked && state != PlayerState.Dead && state != PlayerState.Dash && invulnerableLeft <= 0f;

        /// <summary>Takes health away and dies at zero. True when the hero survived.</summary>
        private bool LoseHealth(int amount)
        {
            health = Mathf.Max(health - amount, 0);
            HealthChanged?.Invoke(health, maxHealth);
            if (health == 0)
            {
                Die();
                return false;
            }
            return true;
        }

        /// <summary>Called by enemies and shots: knockback away from the attacker. False when the hit did nothing.</summary>
        public bool Hurt(int amount, Vector2 from)
        {
            if (!CanBeHurt)
            {
                return false;
            }
            if (SpendHalo())
            {
                return true;
            }
            if (!LoseHealth(GuardedDamage(amount)))
            {
                return true;
            }
            var dir = transform.position.x >= from.x ? 1f : -1f;
            SetState(PlayerState.Hurt, tuning.hurtTime);
            body.linearVelocity = new Vector2(tuning.knockback.x * dir, tuning.knockback.y) / Ppu;
            invulnerableLeft = tuning.invulnerableTime;
            HurtFeedback();
            return true;
        }

        /// <summary>Called by spikes: put back on the last safe ground.</summary>
        public bool TakeHazardDamage(int amount) => TakeHazardDamage(amount, lastSafePosition);

        /// <summary>Hurt by something he has to be taken out of: put at `backTo`. False when he cannot be hurt now.</summary>
        private bool TakeHazardDamage(int amount, Vector2 backTo)
        {
            if (!CanBeHurt)
            {
                return false;
            }
            if (SpendHalo())
            {
                // The Halo spares the heart, but the hero still has to leave the spikes.
                PlaceAt(backTo);
                return true;
            }
            if (!LoseHealth(GuardedDamage(amount)))
            {
                return true;
            }
            HurtFeedback();
            SetState(PlayerState.Hurt, tuning.hurtTime);
            PlaceAt(backTo);
            invulnerableLeft = tuning.invulnerableTime;
            return true;
        }

        private void HurtFeedback()
        {
            Audio.Sfx.Play("hurt");
            Feedback.Flash(this, 0.12f);
            Feedback.Shake(0.45f);
            Feedback.HitStop(0.08f);
        }

        /// <summary>Heals from a pickup. False when there is nothing to heal, so the pickup can stay.</summary>
        public bool RestoreHealth(int amount)
        {
            if (state == PlayerState.Dead || health >= maxHealth)
            {
                return false;
            }
            health = Mathf.Min(health + amount, maxHealth);
            HealthChanged?.Invoke(health, maxHealth);
            return true;
        }

        /// <summary>Resting at a checkpoint: full health, healing refilled, respawn point set.</summary>
        public void RestAt(Vector2 checkpointPosition)
        {
            respawnPosition = checkpointPosition;
            lastSafePosition = checkpointPosition;
            health = maxHealth;
            healCharges = maxHealCharges;
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
            RestHalo();
        }

        /// <summary>Makes a point the place he comes back to, without resting him.</summary>
        public void SetRespawnPoint(Vector2 point)
        {
            respawnPosition = point;
            lastSafePosition = point;
        }

        /// <summary>Where he stands, in units; for the screens.</summary>
        public Vector2 Position => transform.position;

        /// <summary>Resting at a camp: full hearts and charges without moving the respawn point.</summary>
        public void RestAtCamp() => RestoreAll();

        /// <summary>Full hearts and charges without moving the respawn point.</summary>
        public void RestoreAll()
        {
            health = maxHealth;
            healCharges = maxHealCharges;
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
        }

        /// <summary>Ends the level: input is ignored and nothing can hurt him; gravity keeps running so he lands.</summary>
        public void Freeze()
        {
            if (state == PlayerState.Dead)
            {
                return;
            }
            Frozen = true;
            SetState(PlayerState.Idle, 0f);
            var velocity = VelocityPixels;
            velocity.x = 0f;
            body.linearVelocity = velocity / Ppu;
        }

        public void Unfreeze() => Frozen = false;

        private void Die()
        {
            SetState(PlayerState.Dead, tuning.deathTime + RespawnDelayExtra);
            body.linearVelocity = new Vector2(0f, tuning.deathLaunchSpeed) / Ppu;
            ClearPowers();
            Audio.Sfx.Play("death");
            Feedback.Shake(0.6f);
            Died?.Invoke();
        }

        /// <summary>Kills the hero outright (the dev console, a fall below DeathY).</summary>
        public void Kill()
        {
            if (state != PlayerState.Dead)
            {
                Die();
            }
        }

        /// <summary>The angels' system resurrects the hero at the last checkpoint with full health.</summary>
        public void Respawn()
        {
            PlaceAt(respawnPosition);
            lastSafePosition = respawnPosition;
            jumpRising = false;
            jumpBufferLeft = 0f;
            dashBufferLeft = 0f;
            attackBufferLeft = 0f;
            castBufferLeft = 0f;
            healBufferLeft = 0f;
            coyoteLeft = 0f;
            dashCooldownLeft = 0f;
            castCooldownLeft = 0f;
            dropLeft = 0f;
            slamLanded = true;
            SetLedgesIgnored(false);
            phasing = false;
            SetBarriersIgnored(false);
            onWallGrip = false;
            gliding = false;
            wallJumpLockLeft = 0f;
            dashAvailable = true;
            airJumpAvailable = true;
            health = maxHealth;
            healCharges = maxHealCharges;
            HealthChanged?.Invoke(health, maxHealth);
            HealChargesChanged?.Invoke(healCharges, maxHealCharges);
            invulnerableLeft = tuning.invulnerableTime;
            state = PlayerState.Idle;
            sprite.ClearOneShot();
            Enemies.Kills.Clear();
            RestHalo();
            Respawned?.Invoke();
        }

        /// <summary>Brings a dead hero back at a point with full health.</summary>
        public void Respawn(Vector2 point)
        {
            respawnPosition = point;
            Respawn();
        }

        /// <summary>
        /// Throws the hero straight up so he rises `heightPixels` under his own gravity,
        /// as a bounce pod does (docs/design/paradise.md, zone 2). His sideways speed is
        /// kept; the air jump and the dash come back, and letting go of jump does not
        /// cut the launch short. Ignored while he is dead or travelling.
        /// </summary>
        public void Launch(float heightPixels)
        {
            if (!IsAlive || IsTravelling || heightPixels <= 0f)
            {
                return;
            }
            var speed = Mathf.Sqrt(2f * tuning.Gravity * heightPixels);
            body.linearVelocity = new Vector2(body.linearVelocity.x, speed / Ppu);
            grounded = false;
            coyoteLeft = 0f;
            jumpRising = false;
            airJumpAvailable = true;
            dashAvailable = true;
            if (state != PlayerState.Dash)
            {
                state = PlayerState.Jump;
            }
            Audio.Sfx.Play("jump");
        }

        /// <summary>Puts the hero somewhere at rest, as a door or a respawn does.</summary>
        public void PlaceAt(Vector2 point)
        {
            body.position = point;
            transform.position = point;
            body.linearVelocity = Vector2.zero;
            carry = Vector2.zero;
            wind = Vector2.zero;
            windAsked = Vector2.zero;
            sentVelocity = Vector2.zero;
            coyoteLeft = 0f;
            jumpBufferLeft = 0f;
        }

        public void BeginTravel()
        {
            IsTravelling = true;
            body.linearVelocity = Vector2.zero;
        }

        public void EndTravel() => IsTravelling = false;
    }
}
