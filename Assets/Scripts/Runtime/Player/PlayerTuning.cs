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

        [Tooltip(
            "Rising under the corner of a ceiling by up to this many pixels, he is nudged aside and keeps rising."
        )]
        [Min(0)]
        public int cornerCorrection = 4;

        [Tooltip(
            "Meeting the side of a ledge with his feet up to this many pixels under its top, he is lifted onto it."
        )]
        [Min(0)]
        public int ledgeCatch = 4;

        [Tooltip("How much of a moving platform's speed he keeps when he leaves it.")]
        [Range(0f, 1f)]
        public float platformMomentum = 1f;

        [Tooltip("The most a rising platform adds to a jump, in px/s.")]
        [Min(0f)]
        public float platformLiftMax = 120f;

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

        [Tooltip("The upward cut (up and attack): the middle of its box from his feet, and its size (px).")]
        public Vector2 attackUpOffset = new Vector2(0f, 40f);
        public Vector2 attackUpSize = new Vector2(28f, 30f);

        [Tooltip("The downward cut (down and attack in the air): its box's middle from his feet, and its size (px).")]
        public Vector2 attackDownOffset = new Vector2(0f, -10f);
        public Vector2 attackDownSize = new Vector2(26f, 28f);

        [Tooltip("A downward cut that lands bounces him this high (px); 0 for no bounce.")]
        [Min(0f)]
        public float pogoHeight = 40f;

        [Tooltip("With Smite, down and attack in the air is the slam; off, it is the downward cut.")]
        public bool downAttackSlams = true;

        [Tooltip("How many cuts ahead make a combo when attack is pressed again inside the window; 1 for none.")]
        [Range(1, 3)]
        public int comboCuts = 3;

        [Tooltip("Seconds after a cut ends in which the next press carries the combo on.")]
        [Min(0f)]
        public float comboWindow = 0.35f;

        [Tooltip("The combo's last cut: its time as a share of a cut's, and the damage it adds.")]
        [Min(0.1f)]
        public float finisherTimeScale = 1.4f;

        [Min(0)]
        public int finisherDamage = 1;

        [Tooltip("How hard a cut pushes what it hits, and how hard the combo's last cut does (px/s).")]
        [Min(0f)]
        public float attackPush = 60f;

        [Min(0f)]
        public float finisherPush = 140f;

        [Tooltip("A cut ahead that lands while he stands shoves him back at this speed (px/s); 0 for none.")]
        [Min(0f)]
        public float attackRecoil = 60f;

        [Tooltip("How long time stops for the combo's last cut (s); the other cuts use hitStopTime.")]
        [Min(0f)]
        public float finisherHitStop = 0.09f;

        [Tooltip("A buffered dash ends a swing early, a buffered jump does, and how far in the swing must be (s).")]
        public bool dashCancelsAttack = true;
        public bool jumpCancelsAttack;

        [Min(0f)]
        public float attackCancelFrom = 0.1f;

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

        [Header("Climb")]
        [Tooltip("Up and down a ladder or a vine (px/s).")]
        [Min(0f)]
        public float climbSpeed = 60f;

        [Tooltip("Sideways across something wide to climb (px/s).")]
        [Min(0f)]
        public float climbSideSpeed = 40f;

        [Tooltip("Jump alone lets go with a hop this fast upwards (px/s).")]
        [Min(0f)]
        public float climbHopSpeed = 110f;

        [Tooltip("Sideways and jump leaps off with this share of a jump's speed.")]
        [Range(0f, 1.5f)]
        public float climbLeap = 0.85f;

        [Tooltip("After letting go he cannot take hold again for this long (s).")]
        [Min(0f)]
        public float climbRegrabTime = 0.25f;

        [Header("Water")]
        [Tooltip("The share of gravity that pulls on him in water.")]
        [Range(0f, 1f)]
        public float waterGravity = 0.3f;

        [Tooltip("The fastest he sinks (px/s).")]
        [Min(0f)]
        public float waterSinkSpeed = 45f;

        [Tooltip("The share of his run speed he moves at in water.")]
        [Range(0f, 1f)]
        public float waterMoveShare = 0.7f;

        [Tooltip("The share of the dash speed a dash has in water.")]
        [Range(0f, 1f)]
        public float waterDashShare = 0.6f;

        [Tooltip("How high one stroke lifts him (px).")]
        [Min(0f)]
        public float waterStrokeHeight = 24f;

        [Tooltip("The least time between two strokes (s).")]
        [Min(0f)]
        public float waterStrokeGap = 0.25f;

        [Header("Ledge grab")]
        [Tooltip("A trial: falling past a ledge's corner while pressing towards it, he hangs from it.")]
        public bool ledgeGrab;

        [Tooltip("How far below the top of his head a corner can be and still be caught (px).")]
        [Min(1f)]
        public float ledgeGrabReach = 8f;

        [Tooltip("After letting go he cannot catch a ledge again for this long (s).")]
        [Min(0f)]
        public float ledgeGrabRegrab = 0.3f;

        /// <summary>Upward speed of a stroke in water, px/s: what reaches the stroke height there.</summary>
        public float StrokeVelocity => Mathf.Sqrt(2f * Gravity * waterGravity * waterStrokeHeight);

        /// <summary>Upward speed of the bounce off a downward cut, px/s.</summary>
        public float PogoVelocity => Mathf.Sqrt(2f * Gravity * pogoHeight);

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
