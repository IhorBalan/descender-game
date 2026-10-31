using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// The hero's movement numbers, in level pixels and seconds as they were in
    /// scripts/player.gd, so the feel carries over unchanged. Divide a pixel value
    /// by <see cref="PixelsPerUnit"/> where it meets a Unity transform or body.
    /// Create > Descender > Player Tuning; the Player prefab points at the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        /// <summary>One Unity unit is one 16 px tile.</summary>
        public const float PixelsPerUnit = 16f;

        [Header("Run (px/s, px/s²)")]
        [Min(0f)]
        public float runSpeed = 100f;

        [Min(0f)]
        public float groundAccel = 700f;

        [Min(0f)]
        public float groundTurn = 1000f;

        [Min(0f)]
        public float groundFriction = 800f;

        [Min(0f)]
        public float airAccel = 450f;

        [Min(0f)]
        public float airTurn = 600f;

        [Min(0f)]
        public float airFriction = 200f;

        [Min(0f)]
        public float maxFallSpeed = 280f;

        [Header("Jump (px, s)")]
        [Min(1f)]
        public float jumpHeight = 72f;

        [Min(0.05f)]
        public float jumpTimeToApex = 0.45f;

        [Min(0.05f)]
        public float jumpTimeToLand = 0.36f;

        [Range(0f, 1f)]
        public float jumpCutMultiplier = 0.45f;

        [Min(0f)]
        public float apexSpeedThreshold = 30f;

        [Range(0f, 1f)]
        public float apexGravityMultiplier = 0.6f;

        [Min(0f)]
        public float coyoteTime = 0.1f;

        [Min(0f)]
        public float inputBufferTime = 0.12f;

        [Min(0f)]
        public float dropTime = 0.25f;

        [Min(0)]
        public int cornerCorrection = 4;

        [Header("Wings and glide")]
        [Min(1f)]
        public float doubleJumpHeight = 56f;

        [Min(0f)]
        public float wallSlideSpeed = 40f;
        public Vector2 wallJumpVelocity = new Vector2(150f, 2f * 72f * 0.9f / 0.45f);

        [Min(0f)]
        public float wallJumpLock = 0.16f;

        [Min(0f)]
        public float glideFallSpeed = 45f;

        [Range(0f, 1f)]
        public float glideGravityMultiplier = 0.35f;

        [Header("Dash")]
        [Min(0f)]
        public float dashSpeed = 260f;

        [Min(0f)]
        public float dashTime = 0.14f;

        [Min(0f)]
        public float lightDashTime = 0.22f;

        [Min(0f)]
        public float dashCooldown = 0.4f;

        [Min(0f)]
        public float dashEndSpeed = 160f;

        [Min(0f)]
        public float zealCooldownMultiplier = 0.375f;

        [Min(0f)]
        public float zealSpeedMultiplier = 1.25f;

        [Header("Attack and cast")]
        [Min(0f)]
        public float attackTime = 0.28f;

        [Min(0f)]
        public float attackActiveFrom = 0.1f;

        [Min(0f)]
        public float attackActiveUntil = 0.24f;

        [Min(0)]
        public int attackDamage = 1;

        [Min(0)]
        public int bladeBonusDamage = 1;
        public Vector2 attackOffset = new Vector2(20f, 18f);
        public Vector2 attackShapeSize = new Vector2(30f, 28f);

        [Range(0f, 1f)]
        public float attackMoveMultiplier = 0.6f;

        [Min(0f)]
        public float castTime = 0.22f;

        [Min(0f)]
        public float castCooldown = 0.45f;
        public Vector2 castOffset = new Vector2(14f, 18f);

        [Min(0f)]
        public float slamSpeed = 330f;

        [Min(0)]
        public int slamDamage = 2;
        public Vector2 slamReach = new Vector2(84f, 28f);

        [Min(0f)]
        public float slamHitTime = 0.12f;

        [Min(0f)]
        public float slamRecoverTime = 0.18f;

        [Header("Health")]
        [Min(1)]
        public int maxHealth = 5;

        [Min(0)]
        public int maxHealCharges = 3;

        [Min(0)]
        public int healAmount = 2;

        [Min(0f)]
        public float healTime = 0.7f;

        [Min(0f)]
        public float hurtTime = 0.25f;

        [Min(0f)]
        public float invulnerableTime = 0.9f;
        public Vector2 knockback = new Vector2(110f, 110f);

        [Min(0f)]
        public float deathTime = 1.2f;

        [Min(0f)]
        public float safeGroundTime = 0.2f;

        [Header("Feel")]
        [Min(0f)]
        public float landSoundSpeed = 120f;

        [Min(0f)]
        public float landAnimSpeed = 60f;

        [Min(0f)]
        public float landAnimTime = 0.14f;

        [Min(0f)]
        public float skidSpeed = 40f;
        public Vector2 runAnimSpeedRange = new Vector2(0.6f, 1.6f);

        [Tooltip("The launch frame of the jump lasts while the hero rises faster than this (px/s).")]
        [Min(0f)]
        public float jumpLaunchSpeed = 200f;

        [Tooltip("The wing beat of the second jump shows this long.")]
        [Min(0f)]
        public float doubleJumpAnimTime = 0.2f;

        [Tooltip("The sprite is tinted this long after the second jump.")]
        [Min(0f)]
        public float wingFlashTime = 0.15f;

        [Tooltip("How fast the body is thrown up as the hero dies (px/s).")]
        [Min(0f)]
        public float deathLaunchSpeed = 90f;

        [Min(0f)]
        public float hitStopTime = 0.05f;

        [Min(0)]
        public int hitFlashFrames = 4;

        /// <summary>Upward launch speed for the jump, px/s (positive: Y is up in Unity).</summary>
        public float JumpVelocity => 2f * jumpHeight / jumpTimeToApex;

        /// <summary>Gravity while rising, px/s².</summary>
        public float Gravity => 2f * jumpHeight / (jumpTimeToApex * jumpTimeToApex);

        /// <summary>Gravity while falling, px/s².</summary>
        public float FallGravity => 2f * jumpHeight / (jumpTimeToLand * jumpTimeToLand);

        public float DoubleJumpVelocity => Mathf.Sqrt(2f * Gravity * doubleJumpHeight);

        /// <summary>Converts a level-pixel value to Unity units.</summary>
        public static float Units(float pixels) => pixels / PixelsPerUnit;

        public static Vector2 Units(Vector2 pixels) => pixels / PixelsPerUnit;
    }
}
