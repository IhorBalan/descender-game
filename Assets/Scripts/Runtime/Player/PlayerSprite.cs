using Descender.Fx;
using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// Picks the hero's strip and tint every frame from what <see cref="PlayerController"/>
    /// is doing: the pose frames of the jump arc, the landing squash, the skid when the
    /// stick points against the run, the wall, glide and double-jump strips, the tints
    /// of the dash, heal, cast and slam, and the blink while invulnerable. Sits on the
    /// Visual child next to the SpriteAnimator. Port of the visuals in scripts/player.gd.
    /// </summary>
    public sealed class PlayerSprite : MonoBehaviour
    {
        public static readonly Color TintDash = new Color(0.7f, 0.9f, 1.0f, 0.85f);
        public static readonly Color TintLightDash = new Color(1.0f, 0.95f, 0.7f, 0.85f);
        public static readonly Color TintHeal = new Color(1.0f, 0.92f, 0.6f);
        public static readonly Color TintWings = new Color(1.0f, 1.0f, 0.8f);
        public static readonly Color TintGlide = new Color(0.9f, 0.95f, 1.0f);
        public static readonly Color TintCast = new Color(1.0f, 0.97f, 0.75f);
        public static readonly Color TintSlam = new Color(1.0f, 0.8f, 0.6f);
        public static readonly Color TintBlade = new Color(1.0f, 0.85f, 0.5f);

        [SerializeField]
        private PlayerController controller;

        [SerializeField]
        private SpriteAnimator sprite;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<PlayerController>();
            }
            if (sprite == null)
            {
                sprite = GetComponent<SpriteAnimator>();
            }
        }

        private void LateUpdate()
        {
            if (controller == null || sprite == null)
            {
                return;
            }
            var tuning = controller.Tuning;
            var velocity = controller.VelocityPixels;
            var state = controller.State;
            sprite.FlipX = controller.Facing < 0;
            sprite.Color = Color.white;
            if (state != PlayerState.Attack)
            {
                sprite.SpeedScale = 1f;
            }
            switch (state)
            {
                case PlayerState.Idle:
                case PlayerState.Run:
                    UpdateGround(velocity, tuning);
                    break;
                case PlayerState.Jump:
                    if (controller.DoubleJumpShowing)
                    {
                        sprite.PlayOnce("double_jump");
                        sprite.Color = TintWings;
                    }
                    else if (velocity.y > tuning.jumpLaunchSpeed)
                    {
                        sprite.Pose("jump", 0);
                    }
                    else if (velocity.y > tuning.apexSpeedThreshold)
                    {
                        sprite.Pose("jump", 1);
                    }
                    else
                    {
                        sprite.Pose("jump", 2);
                    }
                    break;
                case PlayerState.Fall:
                    if (controller.IsGrippingWall)
                    {
                        sprite.Play("wall");
                        sprite.FlipX = controller.WallDir < 0;
                    }
                    else if (controller.IsGliding)
                    {
                        sprite.Play("glide");
                        sprite.Color = TintGlide;
                    }
                    else if (controller.DoubleJumpShowing)
                    {
                        sprite.PlayOnce("double_jump");
                        sprite.Color = TintWings;
                    }
                    else if (velocity.y > -tuning.apexSpeedThreshold)
                    {
                        // Just past the top of the arc, before the fall proper.
                        sprite.Pose("jump", 2);
                    }
                    else
                    {
                        sprite.Play("fall");
                    }
                    break;
                case PlayerState.Attack:
                    // Started once by the controller; the Lit Blade tints the swing.
                    if (controller.HasPower(Power.Blade))
                    {
                        sprite.Color = TintBlade;
                    }
                    break;
                case PlayerState.Dash:
                    sprite.Play("dash");
                    sprite.Color = controller.Has(Save.Ability.LightDash) ? TintLightDash : TintDash;
                    break;
                case PlayerState.Hurt:
                    sprite.PlayOnce("hurt");
                    break;
                case PlayerState.Heal:
                    sprite.Play("heal");
                    sprite.Color = TintHeal;
                    break;
                case PlayerState.Cast:
                    sprite.PlayOnce("cast");
                    sprite.Color = TintCast;
                    break;
                case PlayerState.Slam:
                    sprite.Pose("slam", controller.SlamLanded ? 1 : 0);
                    sprite.Color = TintSlam;
                    break;
                case PlayerState.Dead:
                    sprite.PlayOnce("death");
                    break;
            }
            // Blink while invulnerable.
            var blinking = controller.InvulnerableLeft > 0f && state != PlayerState.Dead;
            sprite.Renderer.enabled = !blinking || controller.InvulnerableLeft % 0.12f < 0.07f;
        }

        /// <summary>On the ground: the landing squash first, then the skid, else the run paced by the speed, or the idle.</summary>
        private void UpdateGround(Vector2 velocity, PlayerTuning tuning)
        {
            if (controller.LandingShowing)
            {
                sprite.PlayOnce("land");
                return;
            }
            var move = controller.MoveInput;
            var skidding =
                controller.State == PlayerState.Run
                && move != 0f
                && Mathf.Sign(move) != Mathf.Sign(velocity.x)
                && Mathf.Abs(velocity.x) >= tuning.skidSpeed;
            if (skidding)
            {
                sprite.Pose("skid", 0);
                // The feet point the way he slides; he looks back where he is going.
                sprite.FlipX = velocity.x < 0f;
            }
            else if (controller.State == PlayerState.Run)
            {
                sprite.Play("run");
                sprite.SpeedScale = Mathf.Clamp(
                    Mathf.Abs(velocity.x) / tuning.runSpeed,
                    tuning.runAnimSpeedRange.x,
                    tuning.runAnimSpeedRange.y
                );
            }
            else
            {
                sprite.Play("idle");
            }
        }
    }
}
