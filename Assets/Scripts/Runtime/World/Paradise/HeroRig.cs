using System;
using Descender.Combat;
using Descender.Fx;
using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Ansel at full resolution for the new Paradise: painted meshes that bend on a skeleton
    /// (Assets/Art/Paradise/Ansel, assembled by ParadiseHero in the editor), animated here in
    /// code from what the hero is actually doing, so motion always matches the controller.
    ///
    /// The body: a run cycle paced by his speed with planted feet, airborne poses from his
    /// vertical speed, the sword's wind-up, cut and follow-through timed to the attack window,
    /// and poses for dash, double jump, glide, slam, skid, hurt, heal, wall grip and death.
    /// Each bone eases toward its pose at its own rate (shoulder first, then forearm, then
    /// hand), and the forearm and hand drag behind fast swings, so limbs overlap instead of
    /// moving as one. Accents: a stretch at take-off, a landing that bends the knees as hard
    /// as he fell, a turn through profile, a weight shift as he stops. Standing, he breathes,
    /// shifts his weight, glances about and resettles his grip, and his feet find the ground
    /// under each of them.
    ///
    /// Cloth and hair: the cloak (three chains), two locks of hair, the hood and the tunic's
    /// tails are damped springs chasing angles set by how he moves.
    ///
    /// Effects: dust on landings, take-offs, skids and hard steps; streaks on the dash and
    /// glide; a burst of light on the double jump; a shockwave on the slam; an arc of light
    /// behind the cut. Rotation 0 is the rest pose (as painted); + turns counterclockwise,
    /// which swings a limb forward while he faces right. The rig is mirrored for facing left.
    ///
    /// HeroRig.Moves.cs holds what reaches past his own body: the three cuts of the combo,
    /// hits that throw him away from where they came from, a glance at what is near, a hand
    /// on the wall he grips, the moments of a long rest, cloth that the wind pushes and the
    /// ground stops, and feet that scuff the stone or splash in a pool.
    /// </summary>
    public sealed partial class HeroRig : MonoBehaviour
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
        private Transform handFront;

        [SerializeField]
        private Transform sword;

        [SerializeField]
        private Transform upperArmBack;

        [SerializeField]
        private Transform foreArmBack;

        [SerializeField]
        private Transform handBack;

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

        [SerializeField]
        private Transform hood;

        [Tooltip("The cloak's bones, chain after chain, each chain from the shoulder to the hem.")]
        [SerializeField]
        private Transform[] cloakBones = new Transform[0];

        [SerializeField, Min(1)]
        private int cloakChainLength = 5;

        [Tooltip("Locks of hair: the back, then the top.")]
        [SerializeField]
        private Transform[] hairBones = new Transform[0];

        [Tooltip("The tunic's tails: the back, then the front.")]
        [SerializeField]
        private Transform[] tailBones = new Transform[0];

        [Header("Effects")]
        [SerializeField]
        private ParticleSystem dust;

        [SerializeField]
        private ParticleSystem glints;

        [SerializeField]
        private ParticleSystem streaks;

        [SerializeField]
        private SpriteRenderer slashArc;

        [SerializeField]
        private SpriteRenderer ring;

        [Tooltip("Droplets thrown up by a foot in a pool.")]
        [SerializeField]
        private ParticleSystem splash;

        [Tooltip("Rings spreading on a pool's surface where a foot came down.")]
        [SerializeField]
        private ParticleSystem ripples;

        [Tooltip("Given to the shared Feedback sparks and dust while he is in the world, so they are soft too.")]
        [SerializeField]
        private Sprite feedbackSprite;

        [SerializeField]
        private Material feedbackMaterial;

        [Header("Motion")]
        [Tooltip("Units of ground covered by one full stride (two steps).")]
        [SerializeField, Min(0.2f)]
        private float stride = 2.2f;

        [Tooltip("How fast bones ease toward their pose, per second.")]
        [SerializeField, Min(1f)]
        private float ease = 20f;

        [Tooltip("The sword's angle in the hand at rest, relative to the hand.")]
        [SerializeField]
        private float swordRest = -35f;

        private const float LandTime = 0.3f;
        private const float TakeoffTime = 0.14f;
        private const float StopTime = 0.35f;
        private const float TurnTime = 0.12f;
        private const float RingTime = 0.4f;
        private static readonly Color Gold = new Color(1f, 0.86f, 0.55f, 1f);
        private static readonly Color DustTint = new Color(0.86f, 0.8f, 0.7f, 0.55f);

        private Player.PlayerController hero;
        private SpriteRenderer[] renderers;
        private Color[] colors;
        private Vector3 hipsRest;
        private Vector3 shoulderFrontRest;
        private Vector3 shoulderBackRest;
        private float thighLength;
        private float shinLength;
        private float farLift;
        private Pose current;

        private float phase;
        private float stateTime;
        private Player.PlayerState lastState;
        private float deathTurn;
        private float lastUpperF;
        private float lastForeF;
        private float lastUpperB;
        private float lastForeB;
        private Vector2 lastVelocity;

        private bool wasGrounded = true;
        private bool wasDouble;
        private bool wasSlamLanded;
        private bool wasSkidding;
        private float fallSpeed;
        private float landAt = -10f;
        private float landImpact;
        private float takeoffAt = -10f;
        private float stopAt = -10f;
        private float stopSpeed;
        private float lastSpeed01;
        private int lastFace = 1;
        private float turnAt = -10f;
        private float lastStep;
        private float nextDust;
        private float nextStreak;

        private float shift;
        private float shiftTarget;
        private float nextShift = 4f;
        private float glanceUntil;
        private float glanceAngle;
        private float nextGlance = 6f;
        private float gripAt = -10f;
        private float nextGrip = 8f;

        private float[] cloakAngle = new float[0];
        private float[] cloakSpeed = new float[0];
        private float[] hairAngle = new float[0];
        private float[] hairSpeed = new float[0];
        private float[] tailAngle = new float[0];
        private float[] tailSpeed = new float[0];
        private float hoodAngle;
        private float hoodSpeed;

        private float ringAt = -10f;
        private float ringSize;
        private Vector3 ringAt3;

        private struct Pose
        {
            public float hipsX;
            public float hipsY;
            public float torso;
            public float breath;
            public float head;
            public float upperF;
            public float foreF;
            public float handF;
            public float swordF;
            public float upperB;
            public float foreB;
            public float handB;
            public float thighF;
            public float shinF;
            public float bootF;
            public float thighB;
            public float shinB;
            public float bootB;
            public float squash;
        }

        public void Bind(Player.PlayerController controller)
        {
            hero = controller;
        }

        private void Awake()
        {
            // The contact shadow (HeroShadow) keeps its own colour.
            renderers = System.Array.FindAll(GetComponentsInChildren<SpriteRenderer>(true), r => r.name != "Shadow");
            colors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                colors[i] = renderers[i].color;
            }
            if (hips != null)
            {
                hipsRest = hips.localPosition;
            }
            if (upperArmFront != null)
            {
                shoulderFrontRest = upperArmFront.localPosition;
            }
            if (upperArmBack != null)
            {
                shoulderBackRest = upperArmBack.localPosition;
            }
            thighLength = shinFront != null ? shinFront.localPosition.magnitude : 0f;
            shinLength = bootFront != null ? bootFront.localPosition.magnitude : 0f;
            // The far leg is drawn a little shorter, so its foot meets the ground a little higher.
            if (shinBack != null && bootBack != null)
            {
                farLift =
                    thighLength + shinLength - shinBack.localPosition.magnitude - bootBack.localPosition.magnitude;
            }
            current.swordF = swordRest;
            PrepareMoves();
            cloakAngle = new float[cloakBones.Length];
            cloakSpeed = new float[cloakBones.Length];
            hairAngle = new float[hairBones.Length];
            hairSpeed = new float[hairBones.Length];
            tailAngle = new float[tailBones.Length];
            tailSpeed = new float[tailBones.Length];
            if (slashArc != null)
            {
                slashArc.enabled = false;
            }
            if (ring != null)
            {
                ring.enabled = false;
            }
            if (feedbackSprite != null && feedbackMaterial != null)
            {
                Feedback.UseLook(feedbackSprite, feedbackMaterial, 3f);
            }
        }

        private void OnDestroy()
        {
            if (feedbackSprite != null && feedbackMaterial != null)
            {
                Feedback.UseLook(null, null, 1f);
            }
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
            if (dt <= 0f)
            {
                return;
            }
            var now = Time.time;
            var state = hero.State;
            // A swing can follow the last with no tick between them: the count tells them apart.
            var newCut = state == Player.PlayerState.Attack && hero.AttackCount != lastAttackCount;
            stateTime = state == lastState && !newCut ? stateTime + dt : 0f;
            if (newCut)
            {
                cut = RigMath.NextCut(
                    cut,
                    lastState == Player.PlayerState.Attack ? 0f : now - attackEndedAt,
                    ComboWindow,
                    Cuts
                );
                cutStreaked = false;
            }
            if (lastState == Player.PlayerState.Attack && state != Player.PlayerState.Attack)
            {
                attackEndedAt = now;
            }
            lastAttackCount = hero.AttackCount;
            var v = hero.Velocity;
            var runSpeed = hero.Tuning.runSpeed / Player.PlayerTuning.PixelsPerUnit;
            var speed01 = Mathf.Clamp01(Mathf.Abs(v.x) / Mathf.Max(0.01f, runSpeed));
            var grounded = hero.IsGrounded;
            var skidding = grounded && Skidding(v);
            var face =
                hero.IsGrippingWall ? -hero.WallDir
                : skidding ? (v.x < 0f ? -1 : 1)
                : hero.Facing;
            if (grounded)
            {
                phase += Mathf.Abs(v.x) * dt / stride * Mathf.PI * 2f;
            }

            Events(state, v, speed01, grounded, skidding, face, now);

            var target = Target(state, v, speed01, skidding, now);
            Look(ref target, state, face, dt, now);
            if (state == Player.PlayerState.Idle && grounded && stateTime > 0.4f)
            {
                Life(ref target, dt, now);
                Rest(ref target, now);
            }
            else
            {
                shift = Mathf.MoveTowards(shift, 0f, dt * 2f);
                glanceUntil = 0f;
                EndRest();
            }
            Accents(ref target, grounded, now);
            if (grounded && state != Player.PlayerState.Dead)
            {
                if (speed01 < 0.3f && !skidding && state != Player.PlayerState.Dash)
                {
                    Stand(ref target, face);
                }
                else
                {
                    Plant(ref target);
                }
            }

            // Follow-through: the forearm drags behind a fast swing of the upper arm, the hand
            // behind the forearm, so a swing whips instead of moving as one stiff piece.
            target.foreF += Mathf.Clamp(-(current.upperF - lastUpperF) / dt * 0.035f, -22f, 22f);
            target.handF += Mathf.Clamp(-(current.foreF - lastForeF) / dt * 0.03f, -25f, 25f);
            target.foreB += Mathf.Clamp(-(current.upperB - lastUpperB) / dt * 0.035f, -22f, 22f);
            target.handB += Mathf.Clamp(-(current.foreB - lastForeB) / dt * 0.03f, -25f, 25f);
            lastUpperF = current.upperF;
            lastForeF = current.foreF;
            lastUpperB = current.upperB;
            lastForeB = current.foreB;

            var rate = state == Player.PlayerState.Attack ? ease * 2.5f : ease;
            current = Blend(current, target, 1f - Mathf.Exp(-rate * dt));
            Apply(current);

            // Facing, by mirroring the rig, through profile when he turns; dying, by turning over.
            var turn = Mathf.SmoothStep(0f, 1f, (now - turnAt) / TurnTime);
            transform.localScale = new Vector3(face * Mathf.Lerp(-1f, 1f, turn), 1f, 1f);
            deathTurn = Mathf.MoveTowards(deathTurn, state == Player.PlayerState.Dead ? 1f : 0f, dt * 3f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -80f * Mathf.SmoothStep(0f, 1f, deathTurn));

            // Hands that hold on to something: the wall he grips, the blade he wipes.
            Hands(v, dt, now);

            var accel = (v - lastVelocity) / dt;
            Cloak(dt, v, face, speed01, grounded, now);
            Secondary(dt, v, accel, face, now);
            Effects(state, face, now);
            Tint(now);

            lastState = state;
            lastVelocity = v;
            lastSpeed01 = speed01;
            lastFace = face;
        }

        // --- Moments: landing, take-off, stopping, turning, and the effects they throw -----------

        private void Events(
            Player.PlayerState state,
            Vector2 v,
            float speed01,
            bool grounded,
            bool skidding,
            int face,
            float now
        )
        {
            var feet = transform.position;
            if (!grounded)
            {
                fallSpeed = Mathf.Max(fallSpeed, -v.y);
            }
            if (grounded && !wasGrounded)
            {
                landImpact = Mathf.Clamp01((fallSpeed - 4f) / 14f);
                landAt = now;
                if (WaterQuad.PoolAt(feet))
                {
                    Splash(feet, 0.7f + 0.8f * landImpact);
                    Audio.Sfx.Play("splash", -4f + 4f * landImpact, 0.1f);
                }
                else if (landImpact > 0.15f)
                {
                    Dust(feet, 4 + Mathf.RoundToInt(10f * landImpact), 1.6f, 0.9f + 0.6f * landImpact);
                }
                // The hem flares out as he comes down, and falls back.
                for (var i = 0; i < cloakSpeed.Length; i++)
                {
                    cloakSpeed[i] += 90f * landImpact * ((i % cloakChainLength) + 1f) / cloakChainLength;
                }
                fallSpeed = 0f;
            }
            if (!grounded && wasGrounded && v.y > 0.5f)
            {
                takeoffAt = now;
                if (WaterQuad.PoolAt(feet))
                {
                    Splash(feet, 0.6f);
                    Audio.Sfx.Play("splash", -8f, 0.12f);
                }
                else
                {
                    Dust(feet, 4, 0.9f, 0.7f);
                }
            }
            if (state == Player.PlayerState.Hurt && lastState != Player.PlayerState.Hurt)
            {
                Struck(v, face, now);
            }
            if (lastSpeed01 > 0.6f && speed01 < 0.2f && grounded && !skidding)
            {
                stopAt = now;
                stopSpeed = lastSpeed01;
            }
            if (face != lastFace && !hero.IsGrippingWall)
            {
                turnAt = now;
                // The cloak and hair were trailing the old way: throw them across.
                for (var i = 0; i < cloakSpeed.Length; i++)
                {
                    cloakSpeed[i] += 150f * ((i % cloakChainLength) + 1f) / cloakChainLength;
                }
                for (var i = 0; i < hairSpeed.Length; i++)
                {
                    hairSpeed[i] += 120f;
                }
            }
            if (grounded && speed01 > 0.5f && lastSpeed01 <= 0.5f)
            {
                Dust(feet - new Vector3(face * 0.2f, 0f, 0f), 3, 0.8f, 0.6f);
            }
            // A step: each time a foot comes down (the front as the cycle's cosine turns negative,
            // the back as it turns positive), a scuff on stone or a splash in a pool.
            var step = Mathf.Sign(Mathf.Cos(phase));
            var walking = state == Player.PlayerState.Run || state == Player.PlayerState.Idle;
            if (grounded && walking && !skidding && speed01 > 0.25f && step != lastStep)
            {
                Footfall(step < 0f ? bootFront : bootBack, speed01);
            }
            lastStep = step;
            if (skidding && now >= nextDust)
            {
                Dust(feet + new Vector3(face * 0.25f, 0f, 0f), 2, 1.2f, 0.6f);
                nextDust = now + 0.05f;
            }
            if (skidding && !wasSkidding)
            {
                stopAt = now;
                stopSpeed = 0.6f;
            }
            if (hero.DoubleJumpShowing && !wasDouble)
            {
                Burst(feet + new Vector3(0f, 0.55f, 0f), 22, Gold);
                Ring(feet + new Vector3(0f, 0.1f, 0f), 1.2f);
                for (var i = 0; i < cloakSpeed.Length; i++)
                {
                    cloakSpeed[i] -= 120f * ((i % cloakChainLength) + 1f) / cloakChainLength;
                }
            }
            if (state == Player.PlayerState.Slam && hero.SlamLanded && !wasSlamLanded)
            {
                Ring(feet + new Vector3(0f, 0.08f, 0f), 3f);
                Dust(feet, 16, 2.6f, 1.2f);
                Burst(feet + new Vector3(0f, 0.3f, 0f), 10, Gold);
            }
            if (state == Player.PlayerState.Dash && lastState != Player.PlayerState.Dash)
            {
                for (var i = 0; i < 8; i++)
                {
                    Streak(feet + new Vector3(-face * 0.2f, 0.3f + i * 0.15f, 0f), face, 1.4f);
                }
                if (grounded)
                {
                    Dust(feet, 6, 1.4f, 0.8f);
                }
            }
            var trailing = state == Player.PlayerState.Dash || hero.IsGliding;
            if (trailing && now >= nextStreak)
            {
                var height = hero.IsGliding ? 0.5f : 0.4f + UnityEngine.Random.value * 1.1f;
                Streak(feet + new Vector3(-face * 0.45f, height, 0f), face, hero.IsGliding ? 0.6f : 1f);
                nextStreak = now + (hero.IsGliding ? 0.09f : 0.03f);
            }
            wasGrounded = grounded;
            wasDouble = hero.DoubleJumpShowing;
            wasSlamLanded = state == Player.PlayerState.Slam && hero.SlamLanded;
            wasSkidding = skidding;
        }

        private void Accents(ref Pose p, bool grounded, float now)
        {
            // Landing: the knees take the fall, as deep and as long as the fall was hard.
            var t = (now - landAt) / LandTime;
            if (grounded && t < 1f && landImpact > 0f)
            {
                var a = landImpact * (1f - t) * (1f - t);
                p.thighF += 34f * a;
                p.shinF -= 62f * a;
                p.thighB += 22f * a;
                p.shinB -= 50f * a;
                p.bootF += 28f * a;
                p.bootB += 28f * a;
                p.torso -= 10f * a;
                p.upperF += 18f * a;
                p.upperB += 24f * a;
                p.squash += 0.05f * a;
            }
            // Take-off: a stretch as the legs push off.
            t = (now - takeoffAt) / TakeoffTime;
            if (!grounded && t < 1f)
            {
                var a = 1f - t;
                p.squash -= 0.07f * a;
                p.thighB -= 12f * a;
                p.bootB -= 25f * a;
            }
            // Stopping: the weight rocks back over the heels, the arms swing on.
            t = (now - stopAt) / StopTime;
            if (grounded && t < 1f)
            {
                var a = stopSpeed * (1f - t) * (1f - t);
                p.torso += 9f * a;
                p.upperF += 20f * a;
                p.upperB += 25f * a;
                p.foreF += 15f * a;
                p.hipsY -= 0.02f * a;
            }
        }

        /// <summary>Standing still: breathing, a weight shift now and then, a glance, a new grip.</summary>
        private void Life(ref Pose p, float dt, float now)
        {
            var breath = Mathf.Sin(now * 1.7f);
            p.breath = breath * 1.3f;
            p.hipsY -= 0.004f * (breath + 1f) * 0.5f;
            p.upperF += breath * 1.2f;
            p.upperB -= breath * 1.0f;

            if (now >= nextShift)
            {
                shiftTarget = shiftTarget != 0f ? 0f : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                nextShift = now + UnityEngine.Random.Range(3.5f, 7f);
            }
            shift = Mathf.MoveTowards(shift, shiftTarget, dt * 1.4f);
            var s = Mathf.SmoothStep(-1f, 1f, shift * 0.5f + 0.5f);
            p.hipsX += 0.014f * s;
            p.thighF += 4f * s;
            p.thighB -= 3f * s;
            p.torso -= 1.5f * s;

            if (now >= nextGlance)
            {
                glanceAngle = UnityEngine.Random.value < 0.5f ? -9f : 7f;
                glanceUntil = now + 1.3f;
                nextGlance = now + UnityEngine.Random.Range(5f, 9f);
            }
            if (now < glanceUntil)
            {
                var u = 1f - (glanceUntil - now) / 1.3f;
                p.head += glanceAngle * Mathf.Sin(Mathf.PI * u);
            }

            if (now >= nextGrip)
            {
                gripAt = now;
                nextGrip = now + UnityEngine.Random.Range(7f, 12f);
            }
            var g = (now - gripAt) / 0.5f;
            if (g < 1f)
            {
                p.handF += 24f * Mathf.Sin(Mathf.PI * g);
            }
        }

        // --- Poses -------------------------------------------------------------------------------

        private Pose Target(Player.PlayerState state, Vector2 v, float speed01, bool skidding, float now)
        {
            var p = new Pose
            {
                swordF = swordRest,
                handF = -6f,
                handB = -4f,
            };
            switch (state)
            {
                case Player.PlayerState.Attack:
                    switch (cut)
                    {
                        case 1:
                            Rising(ref p, speed01);
                            break;
                        case 2:
                            Thrust(ref p, speed01);
                            break;
                        default:
                            Slash(ref p, speed01);
                            break;
                    }
                    break;
                case Player.PlayerState.Dash:
                    p.torso = -28f;
                    p.head = 14f;
                    p.upperF = -70f;
                    p.foreF = 30f;
                    p.handF = -20f;
                    p.upperB = -80f;
                    p.foreB = 25f;
                    p.handB = -25f;
                    p.thighF = 38f;
                    p.shinF = -48f;
                    p.thighB = -52f;
                    p.shinB = -18f;
                    p.squash = 0.06f;
                    Hold(ref p, -172f);
                    break;
                case Player.PlayerState.Hurt:
                    Hit(ref p, now);
                    break;
                case Player.PlayerState.Heal:
                case Player.PlayerState.Cast:
                    p.torso = -4f;
                    p.upperF = 85f;
                    p.foreF = 35f;
                    p.handF = 40f;
                    p.upperB = 70f;
                    p.foreB = 40f;
                    p.handB = 45f;
                    Legs(ref p, 0f, true);
                    break;
                case Player.PlayerState.Slam:
                    Slam(ref p);
                    break;
                case Player.PlayerState.Dead:
                    p.torso = 10f;
                    p.head = 15f;
                    p.upperF = 40f;
                    p.handF = 20f;
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
                        p.handF = 50f;
                        p.upperB = 135f;
                        p.foreB = 25f;
                        p.handB = 45f;
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
                    else if (skidding)
                    {
                        Skid(ref p);
                    }
                    else
                    {
                        Ground(ref p, speed01, now);
                    }
                    break;
            }
            return p;
        }

        /// <summary>Points the blade at a fixed angle in the world, whatever the arm is doing.</summary>
        private static void Hold(ref Pose p, float worldAngle)
        {
            p.swordF = worldAngle - (p.torso + p.upperF + p.foreF + p.handF);
        }

        private bool Skidding(Vector2 v)
        {
            return hero.State == Player.PlayerState.Run
                && hero.MoveInput != 0f
                && Mathf.Sign(hero.MoveInput) != Mathf.Sign(v.x)
                && Mathf.Abs(v.x) * Player.PlayerTuning.PixelsPerUnit >= hero.Tuning.skidSpeed;
        }

        private static void Skid(ref Pose p)
        {
            p.torso = 14f;
            p.head = -6f;
            p.upperF = 35f;
            p.foreF = 20f;
            p.handF = 20f;
            p.upperB = 50f;
            p.foreB = 25f;
            p.handB = 25f;
            p.thighF = 30f;
            p.shinF = -18f;
            p.thighB = -18f;
            p.shinB = -28f;
            p.squash = 0.03f;
            Hold(ref p, -20f);
        }

        private void Ground(ref Pose p, float speed01, float now)
        {
            Legs(ref p, speed01, false);
            var s = Mathf.Sin(phase);
            var breath = Mathf.Sin(now * 1.6f);
            // Arms swing against the legs; the elbow folds more on the forward swing. The sword
            // arm, carrying weight, swings less.
            p.upperF = Mathf.Lerp(breath * 2f, -14f - 20f * s, speed01);
            p.foreF = Mathf.Lerp(8f, 55f + 30f * Mathf.Max(0f, -s), speed01);
            p.handF = Mathf.Lerp(-6f, -14f, speed01);
            p.upperB = Mathf.Lerp(-breath * 2f, 8f + 42f * s, speed01);
            p.foreB = Mathf.Lerp(8f, 60f + 32f * Mathf.Max(0f, s), speed01);
            p.handB = Mathf.Lerp(-4f, -12f + 14f * s, speed01);
            p.torso = Mathf.Lerp(breath * 1.2f, -17f, speed01);
            p.head = Mathf.Lerp(0f, 11f, speed01);
            // Standing, the sword hangs from the hand; running, the blade streams behind him.
            var rest = p.swordF;
            Hold(ref p, -155f);
            p.swordF = Mathf.LerpAngle(rest, p.swordF, speed01);
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
            // The wrist cocks back in the wind-up and snaps through the cut.
            p.handF = Mathf.Lerp(Mathf.Lerp(-6f, 28f, windup), -24f, through) + 18f * settle;
            p.swordF = Mathf.Lerp(-95f, -81f, through) - p.handF;
            p.torso = Mathf.Lerp(Mathf.Lerp(0f, 10f, windup), -18f, through) + 6f * settle;
            p.head = Mathf.Lerp(0f, 6f, through);
            p.upperB = Mathf.Lerp(-10f, -45f, through);
            p.foreB = 35f;
            p.handB = Mathf.Lerp(0f, -20f, through);
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
                p.handF = -10f;
                p.upperB = -40f;
                p.foreB = 30f;
                p.handB = -20f;
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
                p.handF = 0f;
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
            p.handF = -25f;
            p.upperB = -65f;
            p.foreB = 25f;
            p.handB = -30f;
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
                p.handF = -15f;
                p.upperB = -35f;
                p.foreB = 40f;
                p.handB = -20f;
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
            p.handF = -10f + 12f * falling;
            p.upperB = Mathf.Lerp(-30f, -55f, rising) + 75f * falling;
            p.foreB = 35f;
            p.handB = -15f + 25f * falling;
            p.torso = Mathf.Lerp(-6f, -12f, rising) + 8f * falling;
            p.head = -4f * rising + 6f * falling;
            Hold(ref p, Mathf.Lerp(-105f, -150f, rising));
        }

        // --- Feet on the ground -------------------------------------------------------------------

        /// <summary>At a run, lowers the hips until the lower foot is on the ground, so bent legs never float.</summary>
        private void Plant(ref Pose p)
        {
            p.hipsY +=
                Mathf.Max(Reach(p.thighF, p.shinF), Reach(p.thighB, p.shinB) + farLift) - thighLength - shinLength;
        }

        /// <summary>
        /// Standing: each foot finds the ground under it, on a step or a slope. The hips sit as
        /// low as the shorter leg allows, the other knee bends to reach, and each sole lies flat
        /// along the ground beneath it.
        /// </summary>
        private void Stand(ref Pose p, int face)
        {
            Ground(bootFront, face, out var dyF, out var slopeF);
            Ground(bootBack, face, out var dyB, out var slopeB);
            dyB += farLift;
            var reachF = Reach(p.thighF, p.shinF);
            var reachB = Reach(p.thighB, p.shinB);
            var length = thighLength + shinLength;
            var hip = Mathf.Min(dyF + reachF, dyB + reachB);
            p.hipsY += hip - length;
            Reach(ref p.thighF, ref p.shinF, hip - dyF);
            Reach(ref p.thighB, ref p.shinB, hip - dyB);
            p.bootF = -(p.thighF + p.shinF) + slopeF;
            p.bootB = -(p.thighB + p.shinB) + slopeB;
        }

        /// <summary>How far below the rig's feet the ground is under a foot, and its slope there, facing-relative.</summary>
        private void Ground(Transform foot, int face, out float dy, out float slope)
        {
            dy = 0f;
            slope = 0f;
            if (foot == null)
            {
                return;
            }
            var origin = new Vector2(foot.position.x, transform.position.y + 0.6f);
            var hit = Physics2D.Raycast(origin, Vector2.down, 1.2f, Layers.GroundMask);
            if (hit.collider == null || hit.normal.y < 0.5f)
            {
                return;
            }
            dy = Mathf.Clamp(hit.point.y - transform.position.y, -0.3f, 0.3f);
            slope = -Mathf.Atan2(hit.normal.x, hit.normal.y) * Mathf.Rad2Deg * face;
        }

        private float Reach(float thigh, float shin)
        {
            return thighLength * Mathf.Cos(thigh * Mathf.Deg2Rad)
                + shinLength * Mathf.Cos((thigh + shin) * Mathf.Deg2Rad);
        }

        /// <summary>Two-bone reach: bends the knee so the ankle sits the given height below the hip, keeping it over the same spot.</summary>
        private void Reach(ref float thigh, ref float shin, float height)
        {
            var a = thigh * Mathf.Deg2Rad;
            var b = (thigh + shin) * Mathf.Deg2Rad;
            var x = thighLength * Mathf.Sin(a) + shinLength * Mathf.Sin(b);
            var d = Mathf.Min(Mathf.Sqrt(x * x + height * height), (thighLength + shinLength) * 0.999f);
            var knee = Mathf.Acos(
                Mathf.Clamp(
                    (thighLength * thighLength + shinLength * shinLength - d * d) / (2f * thighLength * shinLength),
                    -1f,
                    1f
                )
            );
            var toward = Mathf.Atan2(x, height);
            var lift = Mathf.Acos(
                Mathf.Clamp(
                    (thighLength * thighLength + d * d - shinLength * shinLength) / (2f * thighLength * d),
                    -1f,
                    1f
                )
            );
            thigh = (toward + lift) * Mathf.Rad2Deg;
            shin = -(Mathf.PI - knee) * Mathf.Rad2Deg;
        }

        // --- Blending and applying ------------------------------------------------------------------

        private static Pose Blend(Pose a, Pose b, float k)
        {
            // The shoulder leads, the forearm follows, the hand comes last.
            var lead = Mathf.Min(1f, k * 1.15f);
            var follow = k * 0.8f;
            var last = k * 0.6f;
            return new Pose
            {
                hipsX = Mathf.Lerp(a.hipsX, b.hipsX, k),
                hipsY = Mathf.Lerp(a.hipsY, b.hipsY, k),
                torso = Mathf.LerpAngle(a.torso, b.torso, k),
                breath = Mathf.Lerp(a.breath, b.breath, k),
                head = Mathf.LerpAngle(a.head, b.head, follow),
                upperF = Mathf.LerpAngle(a.upperF, b.upperF, lead),
                foreF = Mathf.LerpAngle(a.foreF, b.foreF, follow),
                handF = Mathf.LerpAngle(a.handF, b.handF, last),
                swordF = Mathf.LerpAngle(a.swordF, b.swordF, Mathf.Min(1f, k * 1.1f)),
                upperB = Mathf.LerpAngle(a.upperB, b.upperB, lead),
                foreB = Mathf.LerpAngle(a.foreB, b.foreB, follow),
                handB = Mathf.LerpAngle(a.handB, b.handB, last),
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
                hips.localPosition = hipsRest + new Vector3(p.hipsX, p.hipsY, 0f);
                hips.localScale = new Vector3(1f + p.squash, 1f - p.squash, 1f);
            }
            // The lean is shared along the spine, so the body curves instead of tipping.
            Turn(torso, p.torso * 0.55f);
            Turn(chest, p.torso * 0.45f + p.breath);
            Turn(head, p.head - p.torso * 0.4f - p.breath);
            // The shoulders ride up as the arms swing forward and up.
            Shoulder(upperArmFront, shoulderFrontRest, p.upperF);
            Shoulder(upperArmBack, shoulderBackRest, p.upperB);
            Turn(upperArmFront, p.upperF);
            Turn(foreArmFront, p.foreF);
            Turn(handFront, p.handF);
            Turn(sword, p.swordF);
            Turn(upperArmBack, p.upperB);
            Turn(foreArmBack, p.foreB);
            Turn(handBack, p.handB);
            Turn(thighFront, p.thighF);
            Turn(shinFront, p.shinF);
            Turn(bootFront, p.bootF);
            Turn(thighBack, p.thighB);
            Turn(shinBack, p.shinB);
            Turn(bootBack, p.bootB);
        }

        private static void Shoulder(Transform bone, Vector3 rest, float swing)
        {
            if (bone == null)
            {
                return;
            }
            var up = Mathf.Clamp01(swing / 120f);
            bone.localPosition = rest + new Vector3(0.012f * Mathf.Sin(swing * Mathf.Deg2Rad), 0.022f * up, 0f);
        }

        private static void Turn(Transform bone, float degrees)
        {
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }
        }

        // --- Cloth and hair -----------------------------------------------------------------------

        private static void Spring(
            ref float angle,
            ref float speed,
            float target,
            float stiffness,
            float damping,
            float dt
        )
        {
            speed += (target - angle) * stiffness * dt;
            speed *= Mathf.Exp(-damping * dt);
            angle += speed * dt;
        }

        /// <summary>
        /// The cloak: every joint of every chain is a damped spring chasing an angle set by how
        /// he moves. Running and falling stream it behind him, a jump lets it hang, and a ripple
        /// travels from the shoulders to the hem. Stiff at the shoulders, loose at the hem.
        /// </summary>
        private void Cloak(float dt, Vector2 v, int face, float speed01, bool grounded, float now)
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
            // The garden's wind: blowing the way he faces, it lifts the cloak forward; against
            // him, it streams it back. Gusts make it flutter.
            var wind = GardenWind.At(transform.position.x) * face;
            trail += Mathf.Clamp(wind * 18f, -26f, 26f);
            var flutter = Mathf.Abs(wind) * 3.5f;
            var ground = ClothGround(grounded);
            var chainCount = cloakBones.Length / cloakChainLength;
            var rate = 2.2f + 6f * speed01 + 4f * Mathf.Abs(wind);
            for (var i = 0; i < cloakBones.Length; i++)
            {
                var chain = i / cloakChainLength;
                var joint = i % cloakChainLength;
                var along = (joint + 1f) / cloakChainLength;
                // The chain at the back of the fan trails most, the one by the chest least.
                var reach = Mathf.Lerp(1.15f, 0.65f, chainCount > 1 ? chain / (chainCount - 1f) : 0f);
                var ripple =
                    Mathf.Sin(now * rate - joint * 0.9f - chain * 1.3f) * (1f + 4.5f * speed01 + flutter) * along;
                var target = trail * reach * Mathf.Lerp(0.34f, 0.1f, along) + ripple;
                Spring(ref cloakAngle[i], ref cloakSpeed[i], target, Mathf.Lerp(75f, 28f, along), 9f, dt);
                Turn(cloakBones[i], cloakAngle[i]);
                Settle(i, joint, ground);
            }
        }

        /// <summary>Hair, hood and tunic tails: each a spring, pushed by his speed and his changes of speed.</summary>
        private void Secondary(float dt, Vector2 v, Vector2 accel, int face, float now)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            var forward = v.x * face;
            var push = Mathf.Clamp(accel.x * face, -60f, 60f);
            var lift = Mathf.Clamp(accel.y, -80f, 80f);
            var wind = GardenWind.At(transform.position.x) * face;
            for (var i = 0; i < hairBones.Length; i++)
            {
                var target =
                    Mathf.Clamp(
                        -forward * 2.2f - push * 0.15f + Mathf.Clamp(-v.y, -8f, 12f) * 1.2f + wind * 9f,
                        -24f,
                        24f
                    )
                    + Mathf.Sin(now * (1.3f + 3f * Mathf.Abs(wind)) + i * 1.7f) * (1.5f + 2f * Mathf.Abs(wind));
                Spring(ref hairAngle[i], ref hairSpeed[i], target, 60f, 8f, dt);
                Turn(hairBones[i], hairAngle[i]);
            }
            Spring(ref hoodAngle, ref hoodSpeed, Mathf.Clamp(-forward * 1.5f - lift * 0.04f, -12f, 12f), 90f, 10f, dt);
            Turn(hood, hoodAngle);
            for (var i = 0; i < tailBones.Length; i++)
            {
                var leg = i == 0 ? current.thighB : current.thighF;
                var target = Mathf.Clamp(-forward * 3f - push * 0.08f + leg * 0.35f + wind * 6f, -35f, 35f);
                Spring(ref tailAngle[i], ref tailSpeed[i], target, 70f, 9f, dt);
                Turn(tailBones[i], tailAngle[i]);
            }
        }

        // --- Effects ------------------------------------------------------------------------------

        private void Effects(Player.PlayerState state, int face, float now)
        {
            if (slashArc != null)
            {
                // The arc's head rides the blade's tip (see Slash and Rising for the sweep). The
                // thrust draws no arc: it throws streaks along the blade instead.
                var show = 0f;
                var blade = 0f;
                if (state == Player.PlayerState.Attack)
                {
                    var u = Mathf.Clamp01(stateTime / Mathf.Max(0.05f, hero.AttackTime()));
                    var through = Through(u);
                    show = Mathf.Clamp01((u - 0.2f) / 0.06f) * (1f - Mathf.Clamp01((u - 0.5f) / 0.25f));
                    blade = cut == 1 ? Mathf.Lerp(RisingFrom, RisingTo, through) : Mathf.Lerp(130f, -63f, through);
                    if (cut == 2)
                    {
                        show = 0f;
                        if (!cutStreaked && through > 0.2f)
                        {
                            cutStreaked = true;
                            ThrustStreaks(face);
                        }
                    }
                }
                slashArc.enabled = show > 0.01f;
                if (slashArc.enabled)
                {
                    // The tail lies where the blade came from: clockwise of the head for the
                    // downward cut (mirrored), counterclockwise for the rising one.
                    slashArc.flipX = cut != 1;
                    slashArc.transform.localRotation = Quaternion.Euler(0f, 0f, blade - 90f);
                    var c = slashArc.color;
                    c.a = show;
                    slashArc.color = c;
                }
            }
            if (ring != null)
            {
                var t = (now - ringAt) / RingTime;
                ring.enabled = t < 1f;
                if (ring.enabled)
                {
                    ring.transform.position = ringAt3;
                    var size = Mathf.Lerp(0.25f, ringSize, 1f - (1f - t) * (1f - t));
                    ring.transform.localScale = new Vector3(size, size * 0.32f, 1f);
                    var c = ring.color;
                    c.a = 0.8f * (1f - t);
                    ring.color = c;
                }
            }
        }

        private void Ring(Vector3 at, float size)
        {
            ringAt = Time.time;
            ringAt3 = at;
            ringSize = size;
        }

        private void Dust(Vector3 at, int count, float spread, float size)
        {
            if (dust == null)
            {
                return;
            }
            var emit = new ParticleSystem.EmitParams { startColor = DustTint };
            for (var i = 0; i < count; i++)
            {
                var side = UnityEngine.Random.Range(-1f, 1f);
                emit.position = at + new Vector3(side * 0.15f, 0.04f, 0f);
                emit.velocity = new Vector3(side * spread, UnityEngine.Random.Range(0.2f, 0.7f), 0f);
                emit.startSize = size * UnityEngine.Random.Range(0.35f, 0.6f);
                emit.startLifetime = UnityEngine.Random.Range(0.35f, 0.7f);
                emit.rotation = UnityEngine.Random.Range(0f, 360f);
                dust.Emit(emit, 1);
            }
        }

        private void Burst(Vector3 at, int count, Color color)
        {
            if (glints == null)
            {
                return;
            }
            var emit = new ParticleSystem.EmitParams { startColor = color };
            for (var i = 0; i < count; i++)
            {
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var speed = UnityEngine.Random.Range(1.2f, 3.2f);
                emit.position = at;
                emit.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f, 0f) * speed;
                emit.startSize = UnityEngine.Random.Range(0.08f, 0.2f);
                emit.startLifetime = UnityEngine.Random.Range(0.35f, 0.7f);
                glints.Emit(emit, 1);
            }
        }

        private void Streak(Vector3 at, int face, float strength)
        {
            if (streaks == null)
            {
                return;
            }
            var emit = new ParticleSystem.EmitParams
            {
                position = at,
                velocity = new Vector3(-face * UnityEngine.Random.Range(1.5f, 3f) * strength, 0f, 0f),
                startSize3D = new Vector3(1f, 0.22f, 1f) * (UnityEngine.Random.Range(0.5f, 0.9f) * strength),
                startLifetime = UnityEngine.Random.Range(0.18f, 0.32f),
                startColor = new Color(1f, 0.96f, 0.86f, 0.55f),
            };
            streaks.Emit(emit, 1);
        }

        // --- Built by ParadiseHero ----------------------------------------------------------------

        public void SetBones(
            Func<string, Transform> bone,
            Transform[] cloak,
            int cloakJointsPerChain,
            Transform[] hair,
            Transform[] tails
        )
        {
            hips = bone("Hips");
            torso = bone("Spine");
            chest = bone("Chest");
            head = bone("Head");
            upperArmFront = bone("UpperArmFront");
            foreArmFront = bone("ForeArmFront");
            handFront = bone("HandFront");
            sword = bone("Sword");
            upperArmBack = bone("UpperArmBack");
            foreArmBack = bone("ForeArmBack");
            handBack = bone("HandBack");
            thighFront = bone("ThighFront");
            shinFront = bone("ShinFront");
            bootFront = bone("BootFront");
            thighBack = bone("ThighBack");
            shinBack = bone("ShinBack");
            bootBack = bone("BootBack");
            hood = bone("Hood");
            cloakBones = cloak;
            cloakChainLength = cloakJointsPerChain;
            hairBones = hair;
            tailBones = tails;
        }

        public void SetEffects(
            ParticleSystem dustSystem,
            ParticleSystem glintSystem,
            ParticleSystem streakSystem,
            SpriteRenderer arc,
            SpriteRenderer shockwave,
            Sprite softSprite,
            Material softMaterial
        )
        {
            dust = dustSystem;
            glints = glintSystem;
            streaks = streakSystem;
            slashArc = arc;
            ring = shockwave;
            feedbackSprite = softSprite;
            feedbackMaterial = softMaterial;
        }

        public void SetWater(ParticleSystem droplets, ParticleSystem rings)
        {
            splash = droplets;
            ripples = rings;
        }
    }
}
