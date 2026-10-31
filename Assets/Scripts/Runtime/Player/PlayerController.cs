using System.Collections.Generic;
using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// The hero: run, jump with variable height, coyote time and an input buffer,
    /// double jump (Wings), wall slide and wall jump (Wall Grip), glide, dash and
    /// light dash, dropping through ledges, hurt and death. Numbers come from
    /// <see cref="PlayerTuning"/> in level pixels. Input is read in Update into a
    /// small buffer and consumed in FixedUpdate; the body is a dynamic
    /// Rigidbody2D with gravity scale 0, this script applies gravity so rise, fall
    /// and glide can differ. Port of scripts/player.gd (movement part; attacks,
    /// casting and the slam follow in PlayerCombat).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private Transform visual;
        [SerializeField] private Animator animator;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private ContactFilter2D groundFilter;
        private ContactFilter2D wallFilter;
        private readonly Collider2D[] hits = new Collider2D[4];
        private readonly HashSet<Save.Ability> abilities = new HashSet<Save.Ability>();

        // Buffered input (set in Update, consumed in FixedUpdate).
        private float moveX;
        private bool downHeld;
        private bool jumpHeld;
        private float jumpBufferLeft;
        private float dashBufferLeft;
        private bool jumpReleasedSinceJump;

        // State.
        private State state = State.Normal;
        private int facing = 1;
        private bool grounded;
        private bool wasGrounded;
        private int wallSide; // -1 left wall, 1 right wall, 0 none
        private float coyoteLeft;
        private float dashLeft;
        private float dashCooldownLeft;
        private float wallJumpLockLeft;
        private float dropLeft;
        private float hurtLeft;
        private float invulnerableLeft;
        private bool doubleJumpUsed;
        private bool lightDash;
        private int health;
        private int healCharges;
        private int ledgeLayer;
        private int playerLayer;

        public event System.Action<int, int> HealthChanged;
        public event System.Action Died;
        public event System.Action Landed;

        public PlayerTuning Tuning => tuning;
        public bool IsGrounded => grounded;
        public bool IsDashing => state == State.Dash;
        public bool IsAlive => state != State.Dead;
        public bool IsTravelling { get; private set; }
        public int Facing => facing;
        public int Health => health;
        public int HealCharges => healCharges;
        public Vector2 Velocity => body.linearVelocity;

        /// <summary>The hero's velocity in level pixels per second.</summary>
        public Vector2 VelocityPixels => body.linearVelocity * Ppu;

        public bool Has(Save.Ability ability) => abilities.Contains(ability);

        public void Grant(Save.Ability ability) => abilities.Add(ability);

        public void SetAbilities(IEnumerable<Save.Ability> granted)
        {
            abilities.Clear();
            foreach (var ability in granted)
            {
                abilities.Add(ability);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            }
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            ledgeLayer = LayerMask.NameToLayer("Ledge");
            playerLayer = LayerMask.NameToLayer("Player");
            groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("World", "Ledge"), useTriggers = false };
            wallFilter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("World"), useTriggers = false };
            health = tuning.maxHealth + Save.SaveManager.BonusHearts();
            healCharges = tuning.maxHealCharges + Save.SaveManager.BonusCharges();
        }

        private void Update()
        {
            var input = Input.InputRouter.Instance;
            if (input == null || IsTravelling || state == State.Dead)
            {
                moveX = 0f;
                return;
            }
            moveX = input.MoveX;
            downHeld = input.DownHeld;
            jumpHeld = input.JumpHeld;
            if (input.JumpPressed)
            {
                jumpBufferLeft = tuning.inputBufferTime;
            }
            if (input.JumpReleased)
            {
                jumpReleasedSinceJump = true;
            }
            if (input.DashPressed)
            {
                dashBufferLeft = tuning.inputBufferTime;
            }
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            Tick(ref jumpBufferLeft, dt);
            Tick(ref dashBufferLeft, dt);
            Tick(ref dashCooldownLeft, dt);
            Tick(ref wallJumpLockLeft, dt);
            Tick(ref invulnerableLeft, dt);

            if (state == State.Dead || IsTravelling)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            wasGrounded = grounded;
            grounded = CheckGround();
            wallSide = CheckWall();
            if (grounded)
            {
                coyoteLeft = tuning.coyoteTime;
                doubleJumpUsed = false;
                if (!wasGrounded)
                {
                    Landed?.Invoke();
                }
            }
            else
            {
                Tick(ref coyoteLeft, dt);
            }

            // Dropping through a ledge: ignore the Ledge layer for a moment.
            if (grounded && downHeld && jumpBufferLeft > 0f && OnLedge())
            {
                jumpBufferLeft = 0f;
                dropLeft = tuning.dropTime;
            }
            if (dropLeft > 0f)
            {
                Tick(ref dropLeft, dt);
                Physics2D.IgnoreLayerCollision(playerLayer, ledgeLayer, dropLeft > 0f);
            }

            switch (state)
            {
                case State.Normal:
                    StepNormal(dt);
                    break;
                case State.Dash:
                    StepDash(dt);
                    break;
                case State.Hurt:
                    StepHurt(dt);
                    break;
            }
            Face();
        }

        private void StepNormal(float dt)
        {
            var velocity = VelocityPixels;

            // Horizontal.
            if (wallJumpLockLeft <= 0f)
            {
                var accel = grounded ? tuning.groundAccel : tuning.airAccel;
                var turn = grounded ? tuning.groundTurn : tuning.airTurn;
                var friction = grounded ? tuning.groundFriction : tuning.airFriction;
                if (Mathf.Abs(moveX) > 0.01f)
                {
                    var turning = Mathf.Sign(moveX) != Mathf.Sign(velocity.x) && Mathf.Abs(velocity.x) > 1f;
                    velocity.x = Mathf.MoveTowards(velocity.x, moveX * tuning.runSpeed, (turning ? turn : accel) * dt);
                    facing = moveX > 0f ? 1 : -1;
                }
                else
                {
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, friction * dt);
                }
            }

            // Jump, double jump, wall jump.
            if (jumpBufferLeft > 0f)
            {
                if (grounded || coyoteLeft > 0f)
                {
                    velocity.y = tuning.JumpVelocity;
                    Jumped();
                }
                else if (wallSide != 0 && Has(Save.Ability.WallGrip))
                {
                    velocity = new Vector2(-wallSide * tuning.wallJumpVelocity.x, tuning.wallJumpVelocity.y);
                    facing = -wallSide;
                    wallJumpLockLeft = tuning.wallJumpLock;
                    Jumped();
                }
                else if (!doubleJumpUsed && Has(Save.Ability.Wings))
                {
                    velocity.y = tuning.DoubleJumpVelocity;
                    doubleJumpUsed = true;
                    Jumped();
                }
            }

            // Variable jump height: cut the rise when the button is let go.
            if (velocity.y > 0f && jumpReleasedSinceJump && !jumpHeld)
            {
                velocity.y *= tuning.jumpCutMultiplier;
                jumpReleasedSinceJump = false;
            }

            // Gravity: rise, apex hang, fall, glide, wall slide.
            var sliding = !grounded && wallSide != 0 && Has(Save.Ability.WallGrip) && velocity.y <= 0f && Mathf.Sign(moveX) == wallSide;
            var gliding = !grounded && velocity.y < 0f && jumpHeld && Has(Save.Ability.Glide) && !sliding;
            float gravity;
            if (velocity.y > 0f)
            {
                gravity = tuning.Gravity * (Mathf.Abs(velocity.y) < tuning.apexSpeedThreshold ? tuning.apexGravityMultiplier : 1f);
            }
            else
            {
                gravity = tuning.FallGravity * (gliding ? tuning.glideGravityMultiplier : 1f);
            }
            velocity.y -= gravity * dt;
            var fallCap = sliding ? tuning.wallSlideSpeed : gliding ? tuning.glideFallSpeed : tuning.maxFallSpeed;
            velocity.y = Mathf.Max(velocity.y, -fallCap);

            // Dash.
            if (dashBufferLeft > 0f && dashCooldownLeft <= 0f)
            {
                dashBufferLeft = 0f;
                lightDash = Has(Save.Ability.LightDash) && !grounded;
                dashLeft = lightDash ? tuning.lightDashTime : tuning.dashTime;
                var speed = tuning.dashSpeed * (Economy.Skills.Rank("zeal") > 0 ? tuning.zealSpeedMultiplier : 1f);
                var direction = Mathf.Abs(moveX) > 0.01f ? Mathf.Sign(moveX) : facing;
                facing = (int)direction;
                velocity = new Vector2(direction * speed, 0f);
                state = State.Dash;
            }

            body.linearVelocity = velocity / Ppu;
        }

        private void StepDash(float dt)
        {
            Tick(ref dashLeft, dt);
            if (dashLeft <= 0f)
            {
                var velocity = VelocityPixels;
                velocity.x = Mathf.Clamp(velocity.x, -tuning.dashEndSpeed, tuning.dashEndSpeed);
                body.linearVelocity = velocity / Ppu;
                dashCooldownLeft = tuning.dashCooldown * (Economy.Skills.Rank("zeal") > 0 ? tuning.zealCooldownMultiplier : 1f);
                state = State.Normal;
            }
        }

        private void StepHurt(float dt)
        {
            Tick(ref hurtLeft, dt);
            var velocity = VelocityPixels;
            velocity.y -= tuning.FallGravity * dt;
            velocity.y = Mathf.Max(velocity.y, -tuning.maxFallSpeed);
            body.linearVelocity = velocity / Ppu;
            if (hurtLeft <= 0f)
            {
                state = State.Normal;
            }
        }

        /// <summary>Takes damage from a direction; returns false while invulnerable or dead.</summary>
        public bool Hurt(int amount, Vector2 from)
        {
            if (invulnerableLeft > 0f || state == State.Dead || state == State.Dash && lightDash)
            {
                return false;
            }
            health = Mathf.Max(0, health - Save.SaveManager.ScaledDamage(amount));
            HealthChanged?.Invoke(health, tuning.maxHealth + Save.SaveManager.BonusHearts());
            if (health == 0)
            {
                Die();
                return true;
            }
            var side = transform.position.x < from.x ? -1f : 1f;
            body.linearVelocity = new Vector2(side * tuning.knockback.x, tuning.knockback.y) / Ppu;
            hurtLeft = tuning.hurtTime;
            invulnerableLeft = tuning.invulnerableTime;
            state = State.Hurt;
            CameraRig.GameCamera.Main?.Shake(3f, 0.15f);
            return true;
        }

        public bool Heal()
        {
            var max = tuning.maxHealth + Save.SaveManager.BonusHearts();
            if (healCharges <= 0 || health >= max || !grounded || state != State.Normal)
            {
                return false;
            }
            healCharges--;
            health = Mathf.Min(max, health + tuning.healAmount);
            HealthChanged?.Invoke(health, max);
            return true;
        }

        public void RestoreAll()
        {
            health = tuning.maxHealth + Save.SaveManager.BonusHearts();
            healCharges = tuning.maxHealCharges + Save.SaveManager.BonusCharges();
            HealthChanged?.Invoke(health, health);
        }

        private void Die()
        {
            state = State.Dead;
            body.linearVelocity = Vector2.zero;
            Died?.Invoke();
        }

        /// <summary>Brings a dead hero back at a point with full health.</summary>
        public void Respawn(Vector2 point)
        {
            PlaceAt(point);
            RestoreAll();
            invulnerableLeft = tuning.invulnerableTime;
            state = State.Normal;
        }

        public void PlaceAt(Vector2 point)
        {
            body.position = point;
            transform.position = point;
            body.linearVelocity = Vector2.zero;
            coyoteLeft = 0f;
            jumpBufferLeft = 0f;
        }

        public void BeginTravel()
        {
            IsTravelling = true;
            body.linearVelocity = Vector2.zero;
        }

        public void EndTravel() => IsTravelling = false;

        private void Jumped()
        {
            jumpBufferLeft = 0f;
            coyoteLeft = 0f;
            jumpReleasedSinceJump = false;
            grounded = false;
        }

        private void Face()
        {
            if (visual == null)
            {
                return;
            }
            var scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            visual.localScale = scale;
        }

        private bool CheckGround()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 2f / Ppu, 2f / Ppu);
            var center = new Vector2(bounds.center.x, bounds.min.y);
            return Physics2D.OverlapBox(center, size, 0f, groundFilter, hits) > 0;
        }

        private bool OnLedge()
        {
            var bounds = box.bounds;
            var size = new Vector2(bounds.size.x - 2f / Ppu, 2f / Ppu);
            var count = Physics2D.OverlapBox(new Vector2(bounds.center.x, bounds.min.y), size, 0f, groundFilter, hits);
            for (var i = 0; i < count; i++)
            {
                if (hits[i].gameObject.layer == ledgeLayer)
                {
                    return true;
                }
            }
            return false;
        }

        private int CheckWall()
        {
            var bounds = box.bounds;
            var size = new Vector2(2f / Ppu, bounds.size.y - 4f / Ppu);
            if (Physics2D.OverlapBox(new Vector2(bounds.max.x, bounds.center.y), size, 0f, wallFilter, hits) > 0)
            {
                return 1;
            }
            if (Physics2D.OverlapBox(new Vector2(bounds.min.x, bounds.center.y), size, 0f, wallFilter, hits) > 0)
            {
                return -1;
            }
            return 0;
        }

        private static void Tick(ref float timer, float dt)
        {
            if (timer > 0f)
            {
                timer = Mathf.Max(0f, timer - dt);
            }
        }

        private enum State
        {
            Normal,
            Dash,
            Hurt,
            Dead,
        }
    }
}
