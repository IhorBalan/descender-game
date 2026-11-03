using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Ansel drawn at full resolution for the new Paradise: a cutout of painted parts on
    /// bones (Assets/Art/Paradise/Hero, assembled by ParadiseHero in the editor), animated
    /// here in code from what the hero is actually doing, so motion stays fluid at any
    /// resolution and always matches the controller: a run cycle whose stride follows his
    /// speed, airborne poses from his vertical speed, the sword's swing timed to the attack
    /// window, the dash's lean, a hurt flicker, death, wall grip, glide, and the half-formed
    /// wing of light that beats softly and flaps hard on the second jump. Each bone eases
    /// toward its target pose, so states blend instead of snapping. Rotation 0 is the rest
    /// pose (limbs straight down); + turns counterclockwise, which swings a limb forward while
    /// he faces right. The rig is mirrored for facing left.
    /// </summary>
    public sealed class HeroRig : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField] private Transform hips;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform upperArmFront;
        [SerializeField] private Transform foreArmFront;
        [SerializeField] private Transform sword;
        [SerializeField] private Transform upperArmBack;
        [SerializeField] private Transform foreArmBack;
        [SerializeField] private Transform thighFront;
        [SerializeField] private Transform shinFront;
        [SerializeField] private Transform bootFront;
        [SerializeField] private Transform thighBack;
        [SerializeField] private Transform shinBack;
        [SerializeField] private Transform bootBack;
        [SerializeField] private Transform wing;
        [SerializeField] private SpriteRenderer wingGlow;
        [SerializeField] private Transform hair;

        [Header("Motion")]
        [Tooltip("Units of ground covered by one full stride (two steps).")]
        [SerializeField, Min(0.2f)] private float stride = 1.9f;
        [Tooltip("How fast bones ease toward their pose, per second.")]
        [SerializeField, Min(1f)] private float ease = 20f;
        [Tooltip("The sword's angle in the hand at rest, relative to the forearm.")]
        [SerializeField] private float swordRest = -70f;

        private Player.PlayerController hero;
        private SpriteRenderer[] renderers;
        private Color[] colors;
        private Vector3 hipsRest;
        private float phase;
        private float stateTime;
        private Player.PlayerState lastState;
        private float wingPhase;
        private float deathTurn;

        private struct Pose
        {
            public float hipsY;
            public float torso;
            public float head;
            public float upperF;
            public float foreF;
            public float swordF;
            public float upperB;
            public float foreB;
            public float thighF;
            public float shinF;
            public float bootF;
            public float thighB;
            public float shinB;
            public float bootB;
            public float wing;
            public float squash;
        }

        private Pose current;

        public void Bind(Player.PlayerController controller)
        {
            hero = controller;
        }

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                colors[i] = renderers[i].color;
            }
            if (hips != null)
            {
                hipsRest = hips.localPosition;
            }
            current.swordF = swordRest;
        }

        private void LateUpdate()
        {
            if (hero == null)
            {
                hero = GetComponentInParent<Player.PlayerController>();
                if (hero == null)
                {
                    return;
                }
            }
            var dt = Time.deltaTime;
            var state = hero.State;
            stateTime = state == lastState ? stateTime + dt : 0f;
            lastState = state;
            var v = hero.Velocity;
            var runSpeed = hero.Tuning.runSpeed / Player.PlayerTuning.PixelsPerUnit;
            var speed01 = Mathf.Clamp01(Mathf.Abs(v.x) / Mathf.Max(0.01f, runSpeed));
            if (hero.IsGrounded)
            {
                phase += Mathf.Abs(v.x) * dt / stride * Mathf.PI * 2f;
            }

            var target = Target(state, v, speed01);
            // The sword arm follows a swing faster than the rest of the body eases.
            var k = 1f - Mathf.Exp(-(state == Player.PlayerState.Attack ? ease * 2.5f : ease) * dt);
            current = Blend(current, target, k);
            Apply(current);

            // The wing: a slow beat, faster aloft, a hard flap on the second jump.
            var beat = hero.DoubleJumpShowing ? 14f : hero.IsGrounded ? 2.2f : 5f;
            wingPhase += dt * beat;
            var flap = hero.DoubleJumpShowing ? 35f : hero.IsGliding ? 10f : 6f;
            if (wing != null)
            {
                wing.localRotation = Quaternion.Euler(0f, 0f, current.wing + Mathf.Sin(wingPhase) * flap);
            }
            if (wingGlow != null)
            {
                // Always clearly there (it is all that shows of the wing against a pale sky), brighter in flight.
                var glow = hero.WingFlashing || hero.DoubleJumpShowing || hero.IsGliding ? 1f : 0.85f + Mathf.Sin(Time.time * 2f) * 0.1f;
                var c = wingGlow.color;
                c.a = Mathf.MoveTowards(c.a, glow, dt * 4f);
                wingGlow.color = c;
            }

            // Facing, by mirroring the rig; dying, by turning over onto the ground.
            var face = hero.IsGrippingWall ? -hero.WallDir : hero.Facing;
            transform.localScale = new Vector3(face >= 0 ? 1f : -1f, 1f, 1f);
            deathTurn = Mathf.MoveTowards(deathTurn, state == Player.PlayerState.Dead ? 1f : 0f, dt * 3f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -80f * Mathf.SmoothStep(0f, 1f, deathTurn));

            Flicker();
        }

        private Pose Target(Player.PlayerState state, Vector2 v, float speed01)
        {
            var p = new Pose { swordF = swordRest, wing = 8f };
            var t = Time.time;
            switch (state)
            {
                case Player.PlayerState.Attack:
                {
                    // Up and over, then a fast cut forward and down across the attack window.
                    var u = Mathf.Clamp01(stateTime / Mathf.Max(0.05f, hero.AttackTime()));
                    // A short windup, then the cut lands by 55% of the window (the hit is live from ~35%).
                    var cut = u < 0.15f ? Mathf.Lerp(0f, -0.2f, u / 0.15f) : Mathf.SmoothStep(-0.2f, 1f, (u - 0.15f) / 0.4f);
                    // The blade runs along the arm at -90 (it points forward when the arm hangs down).
                    p.upperF = Mathf.Lerp(160f, 40f, Mathf.Clamp01(cut));
                    p.foreF = Mathf.Lerp(15f, 5f, Mathf.Clamp01(cut));
                    p.swordF = Mathf.Lerp(-95f, -85f, Mathf.Clamp01(cut));
                    p.torso = Mathf.Lerp(8f, -14f, Mathf.Clamp01(cut));
                    p.upperB = -35f;
                    p.foreB = 30f;
                    Legs(ref p, v, speed01 * 0.6f, true);
                    break;
                }
                case Player.PlayerState.Dash:
                    p.torso = -22f;
                    p.head = 10f;
                    p.upperF = -55f;
                    p.foreF = 20f;
                    p.upperB = -70f;
                    p.foreB = 15f;
                    p.thighF = 35f;
                    p.shinF = -40f;
                    p.thighB = -45f;
                    p.shinB = -15f;
                    p.squash = 0.06f;
                    p.wing = -20f;
                    break;
                case Player.PlayerState.Hurt:
                    p.torso = 16f;
                    p.head = 12f;
                    p.upperF = 70f;
                    p.foreF = 40f;
                    p.upperB = 95f;
                    p.foreB = 30f;
                    p.thighF = 20f;
                    p.shinF = -30f;
                    p.thighB = -10f;
                    p.shinB = -25f;
                    break;
                case Player.PlayerState.Heal:
                case Player.PlayerState.Cast:
                    p.torso = -4f;
                    p.upperF = 85f;
                    p.foreF = 35f;
                    p.upperB = 70f;
                    p.foreB = 40f;
                    p.wing = 25f;
                    Legs(ref p, v, 0f, true);
                    break;
                case Player.PlayerState.Dead:
                    p.torso = 10f;
                    p.head = 15f;
                    p.upperF = 40f;
                    p.upperB = 60f;
                    p.thighF = 10f;
                    p.thighB = -5f;
                    break;
                default:
                    if (hero.IsGrippingWall)
                    {
                        p.torso = 4f;
                        p.upperF = 150f;
                        p.foreF = 20f;
                        p.upperB = 135f;
                        p.foreB = 25f;
                        p.thighF = 35f;
                        p.shinF = -55f;
                        p.thighB = 10f;
                        p.shinB = -25f;
                    }
                    else if (!hero.IsGrounded)
                    {
                        Air(ref p, v);
                    }
                    else
                    {
                        Ground(ref p, v, speed01, t);
                    }
                    break;
            }
            return p;
        }

        private void Ground(ref Pose p, Vector2 v, float speed01, float t)
        {
            Legs(ref p, v, speed01, false);
            var s = Mathf.Sin(phase);
            // Arms swing against the legs; the body leans into the run and bobs twice a stride.
            p.upperF = Mathf.Lerp(Mathf.Sin(t * 1.6f) * 2f, -38f * s, speed01);
            p.foreF = Mathf.Lerp(8f, 35f + 20f * Mathf.Max(0f, s), speed01);
            p.upperB = Mathf.Lerp(-Mathf.Sin(t * 1.6f) * 2f, 38f * s, speed01);
            p.foreB = Mathf.Lerp(8f, 35f + 20f * Mathf.Max(0f, -s), speed01);
            p.torso = Mathf.Lerp(Mathf.Sin(t * 1.6f) * 1.2f, -8f, speed01);
            p.head = Mathf.Lerp(0f, 5f, speed01);
            p.hipsY = speed01 * 0.05f * Mathf.Abs(Mathf.Cos(phase)) + (1f - speed01) * Mathf.Sin(t * 1.6f) * 0.008f;
            if (hero.LandingShowing)
            {
                p.squash = 0.08f;
                p.hipsY -= 0.08f;
            }
        }

        private void Legs(ref Pose p, Vector2 v, float speed01, bool planted)
        {
            if (planted && speed01 < 0.05f)
            {
                p.thighF = 12f;
                p.shinF = -14f;
                p.thighB = -8f;
                p.shinB = -6f;
                return;
            }
            var s = Mathf.Sin(phase);
            var c = Mathf.Cos(phase);
            p.thighF = 36f * s * speed01;
            p.thighB = -36f * s * speed01;
            // The knee folds as the leg swings through.
            p.shinF = -(8f + 62f * Mathf.Max(0f, c)) * speed01;
            p.shinB = -(8f + 62f * Mathf.Max(0f, -c)) * speed01;
            p.bootF = 10f * Mathf.Max(0f, -s) * speed01;
            p.bootB = 10f * Mathf.Max(0f, s) * speed01;
        }

        private static void Air(ref Pose p, Vector2 v)
        {
            // Rising: knees up, arm reaching; falling: legs open, arms out for balance.
            var rising = Mathf.Clamp01(v.y / 8f);
            var falling = Mathf.Clamp01(-v.y / 10f);
            p.thighF = Mathf.Lerp(20f, 55f, rising) - 15f * falling;
            p.shinF = Mathf.Lerp(-30f, -85f, rising) + 15f * falling;
            p.thighB = Mathf.Lerp(-15f, -5f, rising) - 10f * falling;
            p.shinB = Mathf.Lerp(-25f, -50f, rising);
            p.upperF = Mathf.Lerp(40f, 95f, rising) + 40f * falling;
            p.foreF = 30f;
            p.upperB = Mathf.Lerp(-30f, -50f, rising) + 90f * falling;
            p.foreB = 25f;
            p.torso = Mathf.Lerp(-4f, -10f, rising) + 6f * falling;
            p.head = -4f * rising + 6f * falling;
            p.wing = Mathf.Lerp(15f, 30f, falling);
        }

        private static Pose Blend(Pose a, Pose b, float k)
        {
            return new Pose
            {
                hipsY = Mathf.Lerp(a.hipsY, b.hipsY, k),
                torso = Mathf.LerpAngle(a.torso, b.torso, k),
                head = Mathf.LerpAngle(a.head, b.head, k),
                upperF = Mathf.LerpAngle(a.upperF, b.upperF, k * 1.3f > 1f ? 1f : k * 1.3f),
                foreF = Mathf.LerpAngle(a.foreF, b.foreF, k),
                swordF = Mathf.LerpAngle(a.swordF, b.swordF, k * 1.3f > 1f ? 1f : k * 1.3f),
                upperB = Mathf.LerpAngle(a.upperB, b.upperB, k),
                foreB = Mathf.LerpAngle(a.foreB, b.foreB, k),
                thighF = Mathf.LerpAngle(a.thighF, b.thighF, k),
                shinF = Mathf.LerpAngle(a.shinF, b.shinF, k),
                bootF = Mathf.LerpAngle(a.bootF, b.bootF, k),
                thighB = Mathf.LerpAngle(a.thighB, b.thighB, k),
                shinB = Mathf.LerpAngle(a.shinB, b.shinB, k),
                bootB = Mathf.LerpAngle(a.bootB, b.bootB, k),
                wing = Mathf.LerpAngle(a.wing, b.wing, k),
                squash = Mathf.Lerp(a.squash, b.squash, k),
            };
        }

        private void Apply(Pose p)
        {
            if (hips != null)
            {
                hips.localPosition = hipsRest + new Vector3(0f, p.hipsY, 0f);
                hips.localScale = new Vector3(1f + p.squash, 1f - p.squash, 1f);
            }
            Turn(torso, p.torso);
            Turn(head, p.head - p.torso * 0.4f);
            Turn(upperArmFront, p.upperF);
            Turn(foreArmFront, p.foreF);
            Turn(sword, p.swordF);
            Turn(upperArmBack, p.upperB);
            Turn(foreArmBack, p.foreB);
            Turn(thighFront, p.thighF);
            Turn(shinFront, p.shinF);
            Turn(bootFront, p.bootF);
            Turn(thighBack, p.thighB);
            Turn(shinBack, p.shinB);
            Turn(bootBack, p.bootB);
            if (hair != null)
            {
                hair.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 2f) * 6f - current.torso * 0.5f);
            }
        }

        private static void Turn(Transform bone, float degrees)
        {
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }
        }

        /// <summary>While he cannot be hurt after a hit, he flickers, as the old sprite did.</summary>
        private void Flicker()
        {
            var hidden = hero.InvulnerableLeft > 0f && hero.IsAlive && Mathf.Repeat(Time.time * 14f, 1f) < 0.35f;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] == wingGlow)
                {
                    continue;
                }
                var c = colors[i];
                c.a = hidden ? colors[i].a * 0.35f : colors[i].a;
                renderers[i].color = c;
            }
        }

        public void SetBones(
            Transform hipsBone, Transform torsoBone, Transform headBone,
            Transform upperF, Transform foreF, Transform swordBone,
            Transform upperB, Transform foreB,
            Transform thighF, Transform shinF, Transform bootF,
            Transform thighB, Transform shinB, Transform bootB,
            Transform wingBone, SpriteRenderer wingGlowRenderer, Transform hairBone)
        {
            hips = hipsBone;
            torso = torsoBone;
            head = headBone;
            upperArmFront = upperF;
            foreArmFront = foreF;
            sword = swordBone;
            upperArmBack = upperB;
            foreArmBack = foreB;
            thighFront = thighF;
            shinFront = shinF;
            bootFront = bootF;
            thighBack = thighB;
            shinBack = shinB;
            bootBack = bootB;
            wing = wingBone;
            wingGlow = wingGlowRenderer;
            hair = hairBone;
        }
    }
}
