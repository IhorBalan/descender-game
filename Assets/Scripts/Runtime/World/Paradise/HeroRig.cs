using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Ansel at full resolution for the new Paradise: painted meshes that bend on a skeleton
    /// (Assets/Art/Paradise/Ansel, assembled by ParadiseHero in the editor), animated here in
    /// code from what the hero is actually doing, so motion stays fluid at any resolution and
    /// always matches the controller: a run cycle whose stride follows his speed, airborne
    /// poses from his vertical speed, the sword's swing timed to the attack window, the dash's
    /// lean, a hurt flicker, death, wall grip, and a torn cloak that trails behind him on
    /// springs. Each bone eases toward its target pose, so states blend instead of snapping.
    /// Rotation 0 is the rest pose (as painted, limbs down); + turns counterclockwise, which
    /// swings a limb forward while he faces right. The rig is mirrored for facing left.
    /// </summary>
    public sealed class HeroRig : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField]
        private Transform hips;

        [SerializeField]
        private Transform torso;

        [SerializeField]
        private Transform chest;

        [SerializeField]
        private Transform head;

        [SerializeField]
        private Transform upperArmFront;

        [SerializeField]
        private Transform foreArmFront;

        [SerializeField]
        private Transform sword;

        [SerializeField]
        private Transform upperArmBack;

        [SerializeField]
        private Transform foreArmBack;

        [SerializeField]
        private Transform thighFront;

        [SerializeField]
        private Transform shinFront;

        [SerializeField]
        private Transform bootFront;

        [SerializeField]
        private Transform thighBack;

        [SerializeField]
        private Transform shinBack;

        [SerializeField]
        private Transform bootBack;

        [Tooltip("The cloak's bones, chain after chain, each chain from the shoulder to the hem.")]
        [SerializeField]
        private Transform[] cloakBones = new Transform[0];

        [SerializeField, Min(1)]
        private int cloakChainLength = 5;

        [Header("Motion")]
        [Tooltip("Units of ground covered by one full stride (two steps).")]
        [SerializeField, Min(0.2f)]
        private float stride = 2.2f;

        [Tooltip("How fast bones ease toward their pose, per second.")]
        [SerializeField, Min(1f)]
        private float ease = 20f;

        [Tooltip("The sword's angle in the hand at rest, relative to the forearm.")]
        [SerializeField]
        private float swordRest = -35f;

        private Player.PlayerController hero;
        private SpriteRenderer[] renderers;
        private Color[] colors;
        private Vector3 hipsRest;
        private float phase;
        private float stateTime;
        private Player.PlayerState lastState;
        private float deathTurn;
        private float thighLength;
        private float shinLength;
        private float[] cloakAngle = new float[0];
        private float[] cloakSpeed = new float[0];

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
            thighLength = shinFront != null ? shinFront.localPosition.magnitude : 0f;
            shinLength = bootFront != null ? bootFront.localPosition.magnitude : 0f;
            cloakAngle = new float[cloakBones.Length];
            cloakSpeed = new float[cloakBones.Length];
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

            // Facing, by mirroring the rig; dying, by turning over onto the ground.
            var face =
                hero.IsGrippingWall ? -hero.WallDir
                : Skidding(v) ? (v.x < 0f ? -1 : 1)
                : hero.Facing;
            transform.localScale = new Vector3(face >= 0 ? 1f : -1f, 1f, 1f);
            deathTurn = Mathf.MoveTowards(deathTurn, state == Player.PlayerState.Dead ? 1f : 0f, dt * 3f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -80f * Mathf.SmoothStep(0f, 1f, deathTurn));

            Cloak(dt, v, face, speed01);
            Flicker();
        }

        private Pose Target(Player.PlayerState state, Vector2 v, float speed01)
        {
            var p = new Pose { swordF = swordRest };
            switch (state)
            {
                case Player.PlayerState.Attack:
                    Slash(ref p, speed01);
                    break;
                case Player.PlayerState.Dash:
                    p.torso = -28f;
                    p.head = 14f;
                    p.upperF = -70f;
                    p.foreF = 30f;
                    p.upperB = -80f;
                    p.foreB = 25f;
                    p.thighF = 38f;
                    p.shinF = -48f;
                    p.thighB = -52f;
                    p.shinB = -18f;
                    p.squash = 0.06f;
                    Hold(ref p, -172f);
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
                    Legs(ref p, 0f, true);
                    break;
                case Player.PlayerState.Slam:
                    Slam(ref p);
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
                        Hold(ref p, -120f);
                    }
                    else if (!hero.IsGrounded)
                    {
                        if (hero.IsGliding)
                        {
                            Glide(ref p);
                        }
                        else
                        {
                            Air(ref p, v);
                        }
                    }
                    else
                    {
                        Ground(ref p, v, speed01, Time.time);
                    }
                    break;
            }
            if (hero.IsGrounded && state != Player.PlayerState.Dead)
            {
                Plant(ref p);
            }
            return p;
        }

        /// <summary>Points the blade at a fixed angle in the world, whatever the arm is doing.</summary>
        private static void Hold(ref Pose p, float worldAngle)
        {
            p.swordF = worldAngle - (p.torso + p.upperF + p.foreF);
        }

        /// <summary>Lowers the hips until the lower foot is on the ground, so bent legs never float.</summary>
        private void Plant(ref Pose p)
        {
            p.hipsY += Mathf.Max(Reach(p.thighF, p.shinF), Reach(p.thighB, p.shinB)) - thighLength - shinLength;
        }

        private float Reach(float thigh, float shin)
        {
            return thighLength * Mathf.Cos(thigh * Mathf.Deg2Rad)
                + shinLength * Mathf.Cos((thigh + shin) * Mathf.Deg2Rad);
        }

        private bool Skidding(Vector2 v)
        {
            return hero.State == Player.PlayerState.Run
                && hero.MoveInput != 0f
                && Mathf.Sign(hero.MoveInput) != Mathf.Sign(v.x)
                && Mathf.Abs(v.x) * Player.PlayerTuning.PixelsPerUnit >= hero.Tuning.skidSpeed;
        }

        private void Ground(ref Pose p, Vector2 v, float speed01, float t)
        {
            if (Skidding(v))
            {
                p.torso = 14f;
                p.head = -6f;
                p.upperF = 35f;
                p.foreF = 20f;
                p.upperB = 50f;
                p.foreB = 25f;
                p.thighF = 30f;
                p.shinF = -18f;
                p.thighB = -18f;
                p.shinB = -28f;
                p.squash = 0.03f;
                Hold(ref p, -20f);
                return;
            }
            Legs(ref p, speed01, false);
            var s = Mathf.Sin(phase);
            var breath = Mathf.Sin(t * 1.6f);
            // Arms swing against the legs with the elbows bent; the sword arm, carrying weight, swings less.
            p.upperF = Mathf.Lerp(breath * 2f, -14f - 20f * s, speed01);
            p.foreF = Mathf.Lerp(8f, 62f + 14f * Mathf.Max(0f, s), speed01);
            p.upperB = Mathf.Lerp(-breath * 2f, 8f + 42f * s, speed01);
            p.foreB = Mathf.Lerp(8f, 70f + 16f * Mathf.Max(0f, -s), speed01);
            p.torso = Mathf.Lerp(breath * 1.2f, -17f, speed01);
            p.head = Mathf.Lerp(0f, 11f, speed01);
            // Standing, the sword hangs from the hand; running, the blade streams behind him.
            var rest = p.swordF;
            Hold(ref p, -155f);
            p.swordF = Mathf.LerpAngle(rest, p.swordF, speed01);
            if (hero.LandingShowing)
            {
                p.squash = 0.08f;
                p.hipsY -= 0.08f;
            }
        }

        private void Legs(ref Pose p, float speed01, bool planted)
        {
            if (planted && speed01 < 0.05f)
            {
                p.thighF = 22f;
                p.shinF = -24f;
                p.thighB = -14f;
                p.shinB = -8f;
                p.bootF = 2f;
                p.bootB = 22f;
                return;
            }
            var s = Mathf.Sin(phase);
            var c = Mathf.Cos(phase);
            // The thighs swing further forward than back, as they do at a run.
            p.thighF = (10f + 48f * s) * speed01;
            p.thighB = (10f - 48f * s) * speed01;
            // The knee folds hard as the leg swings through, and stays nearly straight on the ground.
            p.shinF = -(10f + 85f * Mathf.Max(0f, c)) * speed01;
            p.shinB = -(10f + 85f * Mathf.Max(0f, -c)) * speed01;
            // On the ground the foot stays flat; in the air the toes drop.
            p.bootF = Foot(p.thighF, p.shinF, -c, c) * speed01;
            p.bootB = Foot(p.thighB, p.shinB, c, -c) * speed01;
        }

        private static float Foot(float thigh, float shin, float contact, float swing)
        {
            return Mathf.Lerp(-18f * Mathf.Max(0f, swing), -(thigh + shin), Mathf.SmoothStep(0f, 1f, contact));
        }

        /// <summary>The attack: wind up behind the head, cut through in a fast arc, and let the blade follow through.</summary>
        private void Slash(ref Pose p, float speed01)
        {
            var u = Mathf.Clamp01(stateTime / Mathf.Max(0.05f, hero.AttackTime()));
            var windup = Mathf.SmoothStep(0f, 1f, u / 0.18f);
            var through = 1f - Mathf.Pow(1f - Mathf.Clamp01((u - 0.18f) / 0.32f), 3f);
            var settle = Mathf.SmoothStep(0f, 1f, (u - 0.5f) / 0.5f);
            p.upperF = Mathf.Lerp(Mathf.Lerp(0f, 140f, windup), 30f, through) - 25f * settle;
            p.foreF = Mathf.Lerp(Mathf.Lerp(8f, 75f, windup), 6f, through) + 10f * settle;
            p.swordF = Mathf.Lerp(-95f, -81f, through);
            p.torso = Mathf.Lerp(Mathf.Lerp(0f, 10f, windup), -18f, through) + 6f * settle;
            p.head = Mathf.Lerp(0f, 6f, through);
            p.upperB = Mathf.Lerp(-10f, -45f, through);
            p.foreB = 35f;
            Legs(ref p, speed01 * 0.6f, true);
        }

        private void Slam(ref Pose p)
        {
            if (hero.SlamLanded)
            {
                p.torso = -28f;
                p.head = 16f;
                p.upperF = 50f;
                p.foreF = 25f;
                p.upperB = -40f;
                p.foreB = 30f;
                p.thighF = 62f;
                p.shinF = -105f;
                p.thighB = 18f;
                p.shinB = -95f;
                p.bootF = 43f;
                p.bootB = 77f;
                p.squash = 0.05f;
            }
            else
            {
                p.torso = -14f;
                p.head = 12f;
                p.upperF = 60f;
                p.foreF = 30f;
                p.upperB = 35f;
                p.foreB = 40f;
                p.thighF = 48f;
                p.shinF = -85f;
                p.thighB = 22f;
                p.shinB = -70f;
            }
            Hold(ref p, -90f);
        }

        private static void Glide(ref Pose p)
        {
            p.torso = -20f;
            p.head = 12f;
            p.upperF = -50f;
            p.foreF = 30f;
            p.upperB = -65f;
            p.foreB = 25f;
            p.thighF = -12f;
            p.shinF = -28f;
            p.thighB = -28f;
            p.shinB = -38f;
            p.bootF = -20f;
            p.bootB = -20f;
            Hold(ref p, -165f);
        }

        private void Air(ref Pose p, Vector2 v)
        {
            if (hero.DoubleJumpShowing)
            {
                p.torso = -20f;
                p.head = 10f;
                p.upperF = 25f;
                p.foreF = 55f;
                p.upperB = -35f;
                p.foreB = 40f;
                p.thighF = 75f;
                p.shinF = -105f;
                p.thighB = 40f;
                p.shinB = -95f;
                Hold(ref p, -140f);
                return;
            }
            // Rising: the lead knee up, the blade behind; falling: legs open, arms out for balance.
            var rising = Mathf.Clamp01(v.y / 8f);
            var falling = Mathf.Clamp01(-v.y / 10f);
            p.thighF = Mathf.Lerp(20f, 58f, rising) - 15f * falling;
            p.shinF = Mathf.Lerp(-30f, -88f, rising) + 15f * falling;
            p.thighB = Mathf.Lerp(-15f, -8f, rising) - 12f * falling;
            p.shinB = Mathf.Lerp(-25f, -52f, rising);
            p.bootF = -12f;
            p.bootB = -18f;
            p.upperF = Mathf.Lerp(-8f, -22f, rising) + 30f * falling;
            p.foreF = 38f;
            p.upperB = Mathf.Lerp(-30f, -55f, rising) + 75f * falling;
            p.foreB = 35f;
            p.torso = Mathf.Lerp(-6f, -12f, rising) + 8f * falling;
            p.head = -4f * rising + 6f * falling;
            Hold(ref p, Mathf.Lerp(-105f, -150f, rising));
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
            // The lean is shared along the spine, so the body curves instead of tipping.
            Turn(torso, p.torso * 0.55f);
            Turn(chest, p.torso * 0.45f);
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
        }

        private static void Turn(Transform bone, float degrees)
        {
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }
        }

        /// <summary>
        /// The cloak: every joint of every chain is a damped spring chasing an angle set by how
        /// he moves. Running and falling stream it behind him, a jump lets it hang, and a ripple
        /// travels from the shoulders to the hem. Stiff at the shoulders, loose at the hem.
        /// </summary>
        private void Cloak(float dt, Vector2 v, int face, float speed01)
        {
            if (cloakBones.Length == 0)
            {
                return;
            }
            dt = Mathf.Min(dt, 1f / 30f);
            var trail = -Mathf.Clamp(v.x * face * 8f, -30f, 58f);
            trail -= Mathf.Clamp(-v.y * 4.5f, 0f, 42f);
            trail += Mathf.Clamp(v.y * 2f, 0f, 14f);
            if (hero.IsGliding)
            {
                trail -= 22f;
            }
            var chainCount = cloakBones.Length / cloakChainLength;
            var rate = 2.2f + 6f * speed01;
            var t = Time.time;
            for (var i = 0; i < cloakBones.Length; i++)
            {
                var chain = i / cloakChainLength;
                var joint = i % cloakChainLength;
                var along = (joint + 1f) / cloakChainLength;
                // The chain at the back of the fan trails most, the one by the chest least.
                var reach = Mathf.Lerp(1.15f, 0.65f, chainCount > 1 ? chain / (chainCount - 1f) : 0f);
                var ripple = Mathf.Sin(t * rate - joint * 0.9f - chain * 1.3f) * (1f + 4.5f * speed01) * along;
                var target = trail * reach * Mathf.Lerp(0.34f, 0.1f, along) + ripple;
                var stiffness = Mathf.Lerp(75f, 28f, along);
                cloakSpeed[i] += (target - cloakAngle[i]) * stiffness * dt;
                cloakSpeed[i] *= Mathf.Exp(-9f * dt);
                cloakAngle[i] += cloakSpeed[i] * dt;
                Turn(cloakBones[i], cloakAngle[i]);
            }
        }

        /// <summary>While he cannot be hurt after a hit, he flickers, as the old sprite did.</summary>
        private void Flicker()
        {
            var hidden = hero.InvulnerableLeft > 0f && hero.IsAlive && Mathf.Repeat(Time.time * 14f, 1f) < 0.35f;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }
                var c = colors[i];
                c.a = hidden ? colors[i].a * 0.35f : colors[i].a;
                renderers[i].color = c;
            }
        }

        public void SetBones(
            Transform hipsBone,
            Transform spineBone,
            Transform chestBone,
            Transform headBone,
            Transform upperF,
            Transform foreF,
            Transform swordBone,
            Transform upperB,
            Transform foreB,
            Transform thighF,
            Transform shinF,
            Transform bootF,
            Transform thighB,
            Transform shinB,
            Transform bootB,
            Transform[] cloak,
            int cloakJointsPerChain
        )
        {
            hips = hipsBone;
            torso = spineBone;
            chest = chestBone;
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
            cloakBones = cloak;
            cloakChainLength = cloakJointsPerChain;
        }
    }
}
