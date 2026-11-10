using Descender.Combat;
using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// The half of HeroRig that reaches past his own body (see HeroRig.cs for the rest).
    ///
    /// The combo: three cuts that follow one another while the next comes within
    /// <see cref="ComboWindow"/> of the last: the downward cut, a rising backhand, and a
    /// lunging thrust that throws streaks of light along the blade. Each has its wind-up,
    /// cut and recovery.
    ///
    /// Hits: he is thrown away from where the hit came from, harder at first, his cloak and
    /// hair whipping the other way, with a red flare over the flicker.
    ///
    /// A glance: his head turns to the nearest enemy, or failing that the nearest NPC or
    /// relic, ahead of him.
    ///
    /// Hands: gripping a wall, his far hand reaches back and lies flat on it, and dust
    /// comes off it as he slides.
    ///
    /// A long rest: after <see cref="RestAfter"/> seconds standing still he looks up at the
    /// sky, or wipes his blade with his free hand, and later may kneel with the sword
    /// planted before him until he moves.
    ///
    /// Cloth and ground: the cloak's joints lift off the ground rather than pass into it.
    /// Feet: a quiet scuff on each step, or a splash and a ring on a pool.
    /// </summary>
    public sealed partial class HeroRig
    {
        private const float ComboWindow = 0.5f;
        private const int Cuts = 3;
        private const float RisingFrom = -150f;
        private const float RisingTo = 75f;
        private const float FlashTime = 0.1f;
        private const float RestAfter = 10f;
        private const int LookUp = 0;
        private const int Wipe = 1;
        private const int Kneel = 2;
        private static readonly Color HurtFlash = new Color(1f, 0.72f, 0.66f, 1f);
        private static readonly Color Droplet = new Color(0.86f, 0.94f, 1f, 0.9f);
        private static readonly Color Ripple = new Color(0.92f, 0.97f, 1f, 0.7f);
        private static readonly Collider2D[] Nearby = new Collider2D[8];

        [Header("Reach")]
        [Tooltip("How far, in units, he notices enemies and people to glance at.")]
        [SerializeField, Min(0.5f)]
        private float lookRange = 5f;

        private int cut;
        private int lastAttackCount;
        private float attackEndedAt = -10f;
        private bool cutStreaked;

        private float hurtAt = -10f;
        private bool hurtFront = true;

        private float lookAngle;
        private float nextLook;
        private bool hasLook;
        private Vector2 lookPoint;
        private ContactFilter2D lookFilter;

        private float wallReach;
        private float nextWallDust;

        private int rest = -1;
        private int restsDone;
        private float restAt;
        private float restWeight;
        private float nextRest = RestAfter;
        private float bladeLength = 0.6f;

        /// <summary>The cut of the combo he is on: 0 the downward cut, 1 the rising backhand, 2 the thrust.</summary>
        public int Cut => cut;

        /// <summary>What his long rest is now: -1 none, else look up, wipe the blade, kneel.</summary>
        public int RestKind => rest;

        /// <summary>The head's glance at what is near him, in degrees, + up.</summary>
        public float Glance => lookAngle;

        private void PrepareMoves()
        {
            lookFilter = Layers.Any(Layers.EnemyMask);
            var blade = sword != null ? sword.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (blade != null && blade.sprite != null)
            {
                bladeLength = Mathf.Max(0.2f, blade.transform.localPosition.x + blade.sprite.bounds.max.x);
            }
        }

        // --- The combo ----------------------------------------------------------------------------

        private static float Windup(float u) => Mathf.SmoothStep(0f, 1f, u / 0.18f);

        private static float Through(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01((u - 0.18f) / 0.32f), 3f);

        private static float Recover(float u) => Mathf.SmoothStep(0f, 1f, (u - 0.5f) / 0.5f);

        private float AttackProgress() => Mathf.Clamp01(stateTime / Mathf.Max(0.05f, hero.AttackTime()));

        /// <summary>The second cut: the arm drops back low, then the blade sweeps up and forward over his head.</summary>
        private void Rising(ref Pose p, float speed01)
        {
            var u = AttackProgress();
            var windup = Windup(u);
            var through = Through(u);
            var recover = Recover(u);
            p.upperF = Mathf.Lerp(Mathf.Lerp(0f, -55f, windup), 125f, through) - 30f * recover;
            p.foreF = Mathf.Lerp(Mathf.Lerp(8f, 35f, windup), 15f, through) + 10f * recover;
            p.handF = Mathf.Lerp(Mathf.Lerp(-6f, -22f, windup), 18f, through) - 10f * recover;
            p.torso = Mathf.Lerp(Mathf.Lerp(0f, -14f, windup), 10f, through) - 6f * recover;
            p.head = Mathf.Lerp(Mathf.Lerp(0f, 4f, windup), 10f, through) - 5f * recover;
            p.upperB = Mathf.Lerp(Mathf.Lerp(-10f, 45f, windup), -35f, through);
            p.foreB = 40f;
            p.handB = Mathf.Lerp(10f, -20f, through);
            // He dips into the wind-up and rises with the cut.
            p.hipsY -= 0.04f * windup * (1f - through);
            p.squash = -0.035f * through * (1f - recover);
            Legs(ref p, speed01 * 0.6f, true);
            Hold(ref p, Mathf.Lerp(Mathf.Lerp(-100f, RisingFrom, windup), RisingTo, through) - 25f * recover);
        }

        /// <summary>The third cut: the blade drawn back by the chest, then a lunge that drives it straight ahead.</summary>
        private void Thrust(ref Pose p, float speed01)
        {
            var u = AttackProgress();
            var windup = Windup(u);
            var through = Through(u);
            var recover = Recover(u);
            p.upperF = Mathf.Lerp(Mathf.Lerp(0f, -38f, windup), 84f, through) - 20f * recover;
            p.foreF = Mathf.Lerp(Mathf.Lerp(8f, 105f, windup), 2f, through) + 12f * recover;
            p.handF = Mathf.Lerp(-6f, -4f, through);
            p.torso = Mathf.Lerp(Mathf.Lerp(0f, 8f, windup), -24f, through) + 10f * recover;
            p.head = Mathf.Lerp(Mathf.Lerp(0f, -2f, windup), 18f, through) - 6f * recover;
            // The free arm is flung back against the lunge.
            p.upperB = Mathf.Lerp(Mathf.Lerp(-10f, 30f, windup), -62f, through) + 20f * recover;
            p.foreB = Mathf.Lerp(30f, 18f, through);
            p.handB = Mathf.Lerp(0f, -22f, through);
            Legs(ref p, speed01 * 0.6f, true);
            var lunge = through * (1f - 0.6f * recover);
            p.thighF = Mathf.Lerp(p.thighF, 58f, lunge);
            p.shinF = Mathf.Lerp(p.shinF, -54f, lunge);
            p.thighB = Mathf.Lerp(p.thighB, -40f, lunge);
            p.shinB = Mathf.Lerp(p.shinB, -10f, lunge);
            p.hipsX += 0.07f * lunge;
            p.squash = 0.03f * lunge;
            Hold(ref p, Mathf.Lerp(Mathf.Lerp(-8f, 6f, windup), 0f, through));
        }

        /// <summary>Light thrown along the blade as the thrust drives home.</summary>
        private void ThrustStreaks(int face)
        {
            if (sword == null)
            {
                return;
            }
            for (var i = 0; i < 5; i++)
            {
                var at = sword.TransformPoint(new Vector3(bladeLength * (0.15f + 0.22f * i), 0f, 0f));
                // Streak sends its light backward from the way he faces: from the other side, it goes ahead.
                Streak(at + new Vector3(0f, UnityEngine.Random.Range(-0.05f, 0.05f), 0f), -face, 1.3f);
            }
            Burst(sword.TransformPoint(new Vector3(bladeLength, 0f, 0f)), 8, Gold);
        }

        // --- Hits --------------------------------------------------------------------------------

        /// <summary>The moment of a hit: which way it threw him, and the cloth whipped the other way.</summary>
        private void Struck(Vector2 v, int face, float now)
        {
            hurtAt = now;
            // The knockback carries him away from the attacker: backward means it came from in front.
            hurtFront = Mathf.Abs(v.x) < 0.01f || Mathf.Sign(v.x) != face;
            var whip = hurtFront ? 1f : -1f;
            for (var i = 0; i < cloakSpeed.Length; i++)
            {
                cloakSpeed[i] += whip * 240f * ((i % cloakChainLength) + 1f) / cloakChainLength;
            }
            for (var i = 0; i < hairSpeed.Length; i++)
            {
                hairSpeed[i] += whip * 180f;
            }
            for (var i = 0; i < tailSpeed.Length; i++)
            {
                tailSpeed[i] += whip * 150f;
            }
            hoodSpeed += whip * 120f;
        }

        /// <summary>Thrown back by a hit from in front, or pitched forward by one from behind; hardest at first.</summary>
        private void Hit(ref Pose p, float now)
        {
            var snap = 1f + 0.4f * Mathf.Clamp01(1f - (now - hurtAt) / 0.16f);
            if (hurtFront)
            {
                p.torso = 16f;
                p.head = 12f;
                p.upperF = 70f;
                p.foreF = 40f;
                p.handF = 30f;
                p.upperB = 95f;
                p.foreB = 30f;
                p.handB = 35f;
                p.thighF = 20f;
                p.shinF = -30f;
                p.thighB = -10f;
                p.shinB = -25f;
            }
            else
            {
                p.torso = -22f;
                p.head = 16f;
                p.upperF = -40f;
                p.foreF = 35f;
                p.handF = -20f;
                p.upperB = -55f;
                p.foreB = 30f;
                p.handB = -25f;
                p.thighF = 32f;
                p.shinF = -45f;
                p.thighB = -22f;
                p.shinB = -20f;
            }
            p.torso *= snap;
            p.head *= snap;
            p.upperF *= snap;
            p.upperB *= snap;
        }

        /// <summary>His colour: a red flare as a hit lands, and the flicker while he cannot be hurt again.</summary>
        private void Tint(float now)
        {
            var hidden = hero.InvulnerableLeft > 0f && hero.IsAlive && Mathf.Repeat(now * 14f, 1f) < 0.35f;
            var tint = Color.Lerp(Color.white, HurtFlash, Mathf.Clamp01(1f - (now - hurtAt) / FlashTime));
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r == slashArc || r == ring)
                {
                    continue;
                }
                var c = colors[i] * tint;
                c.a = hidden ? colors[i].a * 0.35f : colors[i].a;
                r.color = c;
            }
        }

        // --- A glance ----------------------------------------------------------------------------

        private void Look(ref Pose p, Player.PlayerState state, int face, float dt, float now)
        {
            var free =
                (
                    state == Player.PlayerState.Idle
                    || state == Player.PlayerState.Run
                    || state == Player.PlayerState.Jump
                    || state == Player.PlayerState.Fall
                )
                && !hero.IsGrippingWall
                && rest < 0;
            if (now >= nextLook)
            {
                nextLook = now + 0.2f;
                hasLook = free && Seek(face, out lookPoint);
            }
            var want = 0f;
            if (hasLook && free && head != null)
            {
                var angle = RigMath.LookAngle(head.position, lookPoint, face, 25f, 30f);
                want = float.IsNaN(angle) ? 0f : angle;
            }
            lookAngle = Mathf.Lerp(lookAngle, want, 1f - Mathf.Exp(-5f * dt));
            p.head += lookAngle * 0.75f;
            p.torso += lookAngle * 0.12f;
        }

        /// <summary>The nearest enemy ahead of him, else the nearest NPC or relic ahead, within reach.</summary>
        private bool Seek(int face, out Vector2 point)
        {
            var eye = (Vector2)transform.position + Vector2.up * 1.3f;
            var best = lookRange * lookRange;
            var nearest = Vector2.zero;
            var found = false;
            void Consider(Vector2 at)
            {
                var distance = (at - eye).sqrMagnitude;
                if ((at.x - eye.x) * face > 0.3f && distance < best)
                {
                    best = distance;
                    nearest = at;
                    found = true;
                }
            }
            var count = Physics2D.OverlapCircle(eye, lookRange, lookFilter, Nearby);
            for (var i = 0; i < count; i++)
            {
                Consider(Nearby[i].bounds.center);
            }
            if (!found)
            {
                var people = UI.Interactable.Enabled;
                for (var i = 0; i < people.Count; i++)
                {
                    var someone = people[i];
                    if (someone != null && !someone.IsAutomatic)
                    {
                        Consider((Vector2)someone.transform.position + Vector2.up * 1.1f);
                    }
                }
            }
            point = nearest;
            return found;
        }

        // --- Hands -------------------------------------------------------------------------------

        /// <summary>After the pose is applied: the far hand on the wall he grips, or along the blade he wipes.</summary>
        private void Hands(Vector2 v, float dt, float now)
        {
            var wall = false;
            var onWall = Vector2.zero;
            if (hero.IsGrippingWall && hero.IsAlive && chest != null)
            {
                var side = hero.WallDir;
                var hit = Physics2D.Raycast(chest.position, new Vector2(side, 0f), 0.9f, Layers.WorldMask);
                if (hit.collider != null)
                {
                    wall = true;
                    onWall = new Vector2(hit.point.x - side * 0.07f, chest.position.y + 0.12f);
                }
            }
            wallReach = Mathf.MoveTowards(wallReach, wall ? 1f : 0f, dt * 7f);
            if (wall && wallReach > 0.01f)
            {
                ReachFor(upperArmBack, foreArmBack, handBack, onWall, wallReach, Vector2.up);
                if (v.y < -1f && now >= nextWallDust && handBack != null)
                {
                    Dust(handBack.position, 1, 0.3f, 0.35f);
                    nextWallDust = now + 0.07f;
                }
            }
            if (rest == Wipe && restWeight > 0.01f && sword != null)
            {
                var along = Mathf.Lerp(0.25f, 0.85f, 0.5f + 0.5f * Mathf.Sin((now - restAt) * 3.2f));
                var point = sword.TransformPoint(new Vector3(bladeLength * along, 0.03f, 0f));
                ReachFor(upperArmBack, foreArmBack, handBack, point, restWeight, sword.TransformVector(Vector3.right));
            }
        }

        /// <summary>
        /// Two-bone reach: turns the upper arm and forearm (by <paramref name="weight"/> of the
        /// way) so the wrist meets <paramref name="target"/>, elbow down, and the hand along
        /// <paramref name="handAlong"/>. Worked in the rig's own space, so it holds mirrored.
        /// </summary>
        private void ReachFor(
            Transform upper,
            Transform fore,
            Transform hand,
            Vector2 target,
            float weight,
            Vector2 handAlong
        )
        {
            if (upper == null || fore == null || hand == null || Mathf.Abs(transform.lossyScale.x) < 0.3f)
            {
                return;
            }
            Vector2 Local(Vector3 world) => transform.InverseTransformPoint(world);
            var s = Local(upper.position);
            var e = Local(fore.position);
            var h = Local(hand.position);
            var t = Local(target);
            var elbow = RigMath.Elbow(s, t, (e - s).magnitude, (h - e).magnitude);
            TurnBy(upper, Vector2.SignedAngle(e - s, elbow - s) * weight);
            e = Local(fore.position);
            h = Local(hand.position);
            TurnBy(fore, Vector2.SignedAngle(h - e, t - e) * weight);
            if (handAlong.sqrMagnitude > 0f)
            {
                // The hand points on the way the forearm did at rest.
                var now = (Vector2)transform.InverseTransformVector(hand.TransformVector(hand.localPosition));
                var want = (Vector2)transform.InverseTransformVector(handAlong);
                TurnBy(hand, Vector2.SignedAngle(now, want) * weight);
            }
        }

        private static void TurnBy(Transform bone, float degrees)
        {
            bone.localRotation = Quaternion.Euler(0f, 0f, degrees) * bone.localRotation;
        }

        // --- A long rest -------------------------------------------------------------------------

        private void Rest(ref Pose p, float now)
        {
            if (rest < 0)
            {
                if (stateTime < nextRest)
                {
                    return;
                }
                rest =
                    restsDone > 0 && UnityEngine.Random.value < 0.5f ? Kneel : UnityEngine.Random.Range(LookUp, Kneel);
                restAt = now;
            }
            var t = now - restAt;
            var length =
                rest == Kneel ? float.PositiveInfinity
                : rest == Wipe ? 4.5f
                : 3.5f;
            if (t >= length)
            {
                rest = -1;
                restsDone++;
                restWeight = 0f;
                nextRest = stateTime + UnityEngine.Random.Range(7f, 11f);
                return;
            }
            restWeight =
                Mathf.SmoothStep(0f, 1f, t / 0.9f) * (1f - Mathf.SmoothStep(0f, 1f, (t - length + 0.9f) / 0.9f));
            var pose = p;
            switch (rest)
            {
                case Kneel:
                    // On one knee, the sword planted point down before him, head bowed.
                    pose.thighF = 78f;
                    pose.shinF = -82f;
                    pose.thighB = 0f;
                    pose.shinB = -92f;
                    pose.torso = 4f;
                    pose.head = -12f + 3f * Mathf.Sin(t * 0.6f);
                    pose.breath = Mathf.Sin(t * 1.2f) * 1.6f;
                    pose.upperF = 42f;
                    pose.foreF = 32f;
                    pose.handF = -10f;
                    pose.upperB = 48f;
                    pose.foreB = 40f;
                    pose.handB = 10f;
                    Hold(ref pose, -92f);
                    break;
                case Wipe:
                    // The blade held level before his chest; the free hand runs along it (Hands).
                    pose.upperF = 55f;
                    pose.foreF = 70f;
                    pose.handF = -15f;
                    pose.torso = 3f;
                    pose.head = -14f;
                    pose.upperB = 40f;
                    pose.foreB = 60f;
                    Hold(ref pose, 4f);
                    break;
                default:
                    // Up at the sky, leaning back a little.
                    pose.head = 32f;
                    pose.torso = 7f;
                    pose.upperB -= 4f;
                    break;
            }
            p = Mix(p, pose, restWeight);
        }

        private void EndRest()
        {
            rest = -1;
            restWeight = 0f;
            nextRest = RestAfter;
        }

        private static Pose Mix(Pose a, Pose b, float k)
        {
            return new Pose
            {
                hipsX = Mathf.Lerp(a.hipsX, b.hipsX, k),
                hipsY = Mathf.Lerp(a.hipsY, b.hipsY, k),
                torso = Mathf.LerpAngle(a.torso, b.torso, k),
                breath = Mathf.Lerp(a.breath, b.breath, k),
                head = Mathf.LerpAngle(a.head, b.head, k),
                upperF = Mathf.LerpAngle(a.upperF, b.upperF, k),
                foreF = Mathf.LerpAngle(a.foreF, b.foreF, k),
                handF = Mathf.LerpAngle(a.handF, b.handF, k),
                swordF = Mathf.LerpAngle(a.swordF, b.swordF, k),
                upperB = Mathf.LerpAngle(a.upperB, b.upperB, k),
                foreB = Mathf.LerpAngle(a.foreB, b.foreB, k),
                handB = Mathf.LerpAngle(a.handB, b.handB, k),
                thighF = Mathf.LerpAngle(a.thighF, b.thighF, k),
                shinF = Mathf.LerpAngle(a.shinF, b.shinF, k),
                bootF = Mathf.LerpAngle(a.bootF, b.bootF, k),
                thighB = Mathf.LerpAngle(a.thighB, b.thighB, k),
                shinB = Mathf.LerpAngle(a.shinB, b.shinB, k),
                bootB = Mathf.LerpAngle(a.bootB, b.bootB, k),
                squash = Mathf.Lerp(a.squash, b.squash, k),
            };
        }

        // --- Cloth on the ground -----------------------------------------------------------------

        /// <summary>The height of the ground under him for the cloth: his feet when grounded, else the ground below, if near.</summary>
        private float ClothGround(bool grounded)
        {
            if (grounded)
            {
                return transform.position.y + 0.02f;
            }
            var hit = Physics2D.Raycast(
                (Vector2)transform.position + Vector2.up * 0.1f,
                Vector2.down,
                2.5f,
                Layers.GroundMask
            );
            return hit.collider != null ? hit.point.y + 0.02f : float.NegativeInfinity;
        }

        /// <summary>Lifts one joint of the cloak so the stretch below it lies on the ground instead of passing into it.</summary>
        private void Settle(int i, int joint, float ground)
        {
            var mirror = transform.lossyScale.x;
            if (float.IsNegativeInfinity(ground) || Mathf.Abs(mirror) < 0.3f)
            {
                return;
            }
            var bone = cloakBones[i];
            var tip =
                joint + 1 < cloakChainLength && i + 1 < cloakBones.Length
                    ? cloakBones[i + 1].position
                    : bone.TransformPoint(bone.localPosition);
            // Worked out in the world; mirrored, a turn in the world is the other way in the rig.
            var lift = RigMath.LiftToGround(bone.position, tip, ground) * Mathf.Sign(mirror);
            if (lift == 0f)
            {
                return;
            }
            cloakAngle[i] += lift;
            if (cloakSpeed[i] * lift < 0f)
            {
                cloakSpeed[i] *= 0.3f;
            }
            Turn(bone, cloakAngle[i]);
        }

        // --- Feet --------------------------------------------------------------------------------

        private void Footfall(Transform foot, float speed01)
        {
            if (foot == null)
            {
                return;
            }
            var at = new Vector3(foot.position.x, transform.position.y, 0f);
            if (WaterQuad.PoolAt(at))
            {
                Splash(at, 0.35f + 0.4f * speed01);
                Audio.Sfx.Play("splash", Mathf.Lerp(-16f, -9f, speed01), 0.15f);
                return;
            }
            if (speed01 > 0.8f)
            {
                Dust(at, 1, 0.5f, 0.45f);
            }
            Audio.Sfx.Play("step", Mathf.Lerp(-20f, -11f, speed01), 0.12f);
        }

        /// <summary>Droplets thrown up from a pool, and a ring spreading on its surface.</summary>
        private void Splash(Vector3 at, float strength)
        {
            if (splash != null)
            {
                var emit = new ParticleSystem.EmitParams { startColor = Droplet };
                var count = 4 + Mathf.RoundToInt(10f * strength);
                for (var i = 0; i < count; i++)
                {
                    var side = UnityEngine.Random.Range(-1f, 1f);
                    emit.position = at + new Vector3(side * 0.12f, 0.03f, 0f);
                    emit.velocity = new Vector3(
                        side * 1.3f * strength,
                        UnityEngine.Random.Range(1.4f, 3.4f) * strength,
                        0f
                    );
                    emit.startSize = UnityEngine.Random.Range(0.035f, 0.08f);
                    emit.startLifetime = UnityEngine.Random.Range(0.35f, 0.6f);
                    splash.Emit(emit, 1);
                }
            }
            if (ripples != null)
            {
                ripples.Emit(
                    new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(0f, -0.02f, 0f),
                        startSize3D = new Vector3(0.5f, 0.12f, 1f) * (0.6f + strength),
                        startLifetime = 0.6f,
                        startColor = Ripple,
                    },
                    1
                );
            }
        }
    }
}
