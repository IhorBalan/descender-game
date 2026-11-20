using UnityEngine;

namespace Descender.Motion
{
    /// <summary>
    /// A running cycle worked out for any moment, from eight key poses (contact, down,
    /// passing and flight for each leg, <see cref="RunKey"/>), for the
    /// <see cref="RunSkeleton"/> of the CharacterAnimationLab scene.
    ///
    /// A foot on the ground does not slide: from its contact to its push-off it lies at a
    /// fixed place on the ground while the hips travel over it at the running speed, so
    /// relative to the hips it goes back the stance length (the stride times the
    /// <see cref="RunGaitSettings.Duty"/>) in that time; in the last part of the stance
    /// the heel lifts and the foot turns about its toes, which stay where they are. A foot
    /// in the air follows a curve through the key poses: up behind (the heel recovering
    /// toward the seat), tucked under the hips, the knee driving forward, the leg reaching
    /// out and coming back a little to meet the ground. The hips are set at the height the
    /// legs can reach those feet from, then rise and fall over the step. Each leg is solved
    /// by two-bone inverse kinematics from its hip to its ankle, the knee forward, so thigh
    /// and shin keep their lengths. The torso leans and the hips and shoulders turn against
    /// each other; the arms swing against the legs, their elbows bent.
    ///
    /// The channels between key poses are eased by a Catmull-Rom curve, or straight when
    /// smoothing is 0. Nothing is allocated: a pose is filled in place.
    /// </summary>
    public static class RunGait
    {
        public const int KeyCount = 8;

        /// <summary>How far the heel turns up about the toes by the push-off, in degrees.</summary>
        public const float HeelLift = 35f;

        /// <summary>From where in the stance (0 .. 1) the heel starts to lift.</summary>
        public const float HeelLiftStart = 0.55f;

        /// <summary>The share of a leg's full reach the hips are set within, so a foot on the ground is always reached.</summary>
        public const float Reach = 0.985f;

        // The keyed channels, one value at each key pose (RunKey order: phase k / 8).
        // The hips' rise and fall, in units of the vertical bounce (0 at contact).
        private static readonly float[] Bounce = { 0f, -1f, -0.45f, 0.7f, 0f, -1f, -0.45f, 0.7f };

        // The lean, as a part more or less than the torso lean.
        private static readonly float[] LeanExtra = { 0f, -0.12f, 0f, 0.2f, 0f, -0.12f, 0f, 0.2f };

        // How far forward the right hip is turned (the left as far back; the shoulders the other way).
        private static readonly float[] RightHipForward = { 1f, 0.6f, 0f, -0.6f, -1f, -0.6f, 0f, 0.6f };

        // The right upper arm's swing, in units of the arm swing (forward positive): against the right leg.
        private static readonly float[] RightArmSwing = { -1f, -0.7f, 0f, 0.75f, 1f, 0.7f, 0f, -0.75f };

        // The right elbow's bend in degrees: nearly open as the arm swings back, most bent as it comes forward.
        private static readonly float[] RightElbow = { 32f, 48f, 80f, 100f, 108f, 92f, 62f, 40f };

        // The swinging foot's key points at phases 3/8 .. 7/8 of its own leg: the ankle's place
        // relative to the hips' ground point, in leg lengths (x) and leg lengths over the ankle's
        // standing height (y), and the foot's angle in degrees (toes down positive).
        private static readonly float[] SwingPhase = { 0.375f, 0.5f, 0.625f, 0.75f, 0.875f };
        private static readonly float[] SwingX = { -0.84f, -0.62f, -0.2f, 0.25f, 0.64f };
        private static readonly float[] SwingY = { 0.24f, 0.55f, 0.64f, 0.46f, 0.14f };
        private static readonly float[] SwingAngle = { 65f, 80f, 45f, 5f, -18f };

        // The swing curve's knots: lift-off, the five keys, touchdown.
        private static readonly float[] knotT = new float[7];
        private static readonly float[] knotX = new float[7];
        private static readonly float[] knotY = new float[7];
        private static readonly float[] knotA = new float[7];

        /// <summary>The phase of a key pose: its index over eight.</summary>
        public static float PhaseOf(RunKey key) => (int)key / (float)KeyCount;

        /// <summary>The key pose a phase is nearest.</summary>
        public static RunKey KeyAt(float phase) => (RunKey)(Mathf.FloorToInt(Wrap(phase) * KeyCount + 0.5f) % KeyCount);

        /// <summary>Whether a leg whose own phase this is (0 at its contact) has its foot on the ground.</summary>
        public static bool InStance(float legPhase) => Wrap(legPhase) < RunGaitSettings.Duty;

        /// <summary>The hips' height over the ground before their rise and fall: as high as the feet on the ground can be reached from.</summary>
        public static float BaseHipHeight(RunGaitSettings settings, RunBody body)
        {
            var leg = body.Leg;
            var reach = leg * Reach;
            // From the leg's own hip joint, turned forward or back with the pelvis at that moment.
            var turn = settings.counterRotation * body.pelvisHalfWidth;
            var contact = StanceAnkle(0f, settings, body, out _);
            var contactX = contact.x - Channel(RightHipForward, 0f, settings.smoothing) * turn;
            var atContact = Mathf.Sqrt(Mathf.Max(reach * reach - contactX * contactX, 0f)) + contact.y;
            var toeOff = StanceAnkle(1f, settings, body, out _);
            var toeOffX = toeOff.x - Channel(RightHipForward, RunGaitSettings.Duty, settings.smoothing) * turn;
            var rise =
                Mathf.Max(0f, Channel(Bounce, RunGaitSettings.Duty, settings.smoothing)) * settings.verticalBounce;
            var atToeOff = Mathf.Sqrt(Mathf.Max(reach * reach - toeOffX * toeOffX, 0f)) + toeOff.y - rise;
            return Mathf.Max(leg * 0.35f, Mathf.Min(leg * 0.94f, Mathf.Min(atContact, atToeOff)));
        }

        /// <summary>Fills `pose` with the body at `phase` (0 .. 1, wrapped) of the cycle.</summary>
        public static void Evaluate(float phase, RunGaitSettings settings, RunBody body, RunPose pose)
        {
            phase = Wrap(phase);
            var smooth = settings.smoothing;
            pose.phase = phase;
            pose.key = KeyAt(phase);

            // The torso: the hips over the root, leaning forward, the head steadied.
            var hipHeight = BaseHipHeight(settings, body) + Channel(Bounce, phase, smooth) * settings.verticalBounce;
            pose.hips = new Vector2(0f, hipHeight);
            pose.lean = settings.torsoLean * (1f + Channel(LeanExtra, phase, smooth));
            pose.chest = pose.hips + Forward(pose.lean) * body.spine;
            pose.neck = pose.chest + Forward(pose.lean * 0.6f) * body.neck;
            pose.head = pose.neck + Forward(pose.lean * 0.3f) * body.head;

            // Hips and shoulders turned against each other.
            var turn = Channel(RightHipForward, phase, smooth) * settings.counterRotation;
            var hipOffset = new Vector2(turn * body.pelvisHalfWidth, 0f);
            var shoulderOffset = new Vector2(turn * body.shoulderHalfWidth, 0f);

            Leg(phase, pose.hips + hipOffset, settings, body, ref pose.rightLeg);
            Leg(phase + 0.5f, pose.hips - hipOffset, settings, body, ref pose.leftLeg);
            Arm(phase, pose.chest - shoulderOffset, pose.lean, settings, body, ref pose.rightArm);
            Arm(phase + 0.5f, pose.chest + shoulderOffset, pose.lean, settings, body, ref pose.leftArm);
        }

        /// <summary>
        /// Two-bone inverse kinematics: the middle joint (a knee, an elbow) of a limb from
        /// `root` toward `target` with bones `upper` and `lower` long, bent to the side a
        /// positive `bend` turns the limb's line toward (anticlockwise). Where the target is
        /// out of reach the limb points at it, straight; the bones keep their lengths.
        /// </summary>
        public static void SolveTwoBone(
            Vector2 root,
            Vector2 target,
            float upper,
            float lower,
            float bend,
            out Vector2 middle,
            out Vector2 end
        )
        {
            var toward = target - root;
            var distance = toward.magnitude;
            var direction = distance > 1e-6f ? toward / distance : Vector2.down;
            distance = Mathf.Clamp(distance, Mathf.Abs(upper - lower) + 1e-4f, upper + lower - 1e-4f);
            var cos = (upper * upper + distance * distance - lower * lower) / (2f * upper * distance);
            var angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Sign(bend);
            middle = root + Rotate(direction, angle * Mathf.Rad2Deg) * upper;
            end = middle + (root + direction * distance - middle).normalized * lower;
        }

        /// <summary>A vector turned anticlockwise by degrees.</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>
        /// How far behind the hips the feet fall, so the whole body leans forward over them by
        /// the body lean (a leg's length times its tangent): the run's axis tilts, not only the torso.
        /// </summary>
        public static float Behind(RunGaitSettings settings, RunBody body) =>
            body.Leg * Mathf.Tan(settings.bodyLean * Mathf.Deg2Rad);

        private static float Wrap(float phase) => phase - Mathf.Floor(phase);

        /// <summary>Up, leant forward (toward +x) by degrees.</summary>
        private static Vector2 Forward(float degrees) =>
            new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

        private static void Leg(float legPhase, Vector2 hip, RunGaitSettings settings, RunBody body, ref RunLimb leg)
        {
            legPhase = Wrap(legPhase);
            Vector2 ankle;
            float footAngle;
            leg.contact = InStance(legPhase);
            if (leg.contact)
            {
                ankle = StanceAnkle(legPhase / RunGaitSettings.Duty, settings, body, out footAngle);
            }
            else
            {
                ankle = SwingAnkle(legPhase, settings, body, out footAngle);
            }
            leg.root = hip;
            SolveTwoBone(hip, ankle, body.thigh, body.shin, 1f, out leg.middle, out leg.end);
            leg.tip = leg.end + Rotate(new Vector2(body.foot, -body.ankleHeight), -footAngle);
            leg.bearing = leg.contact ? new Vector2(leg.tip.x, 0f) : leg.tip;
        }

        /// <summary>
        /// The ankle of a foot on the ground, `through` the stance (0 at contact, 1 at the
        /// push-off): its toes at a place that goes back under the hips exactly as fast as
        /// the hips go forward, and from <see cref="HeelLiftStart"/> the heel turning up about them.
        /// </summary>
        private static Vector2 StanceAnkle(float through, RunGaitSettings settings, RunBody body, out float footAngle)
        {
            var length = settings.StanceLength;
            var toes = new Vector2(length * 0.5f - through * length + body.foot - Behind(settings, body), 0f);
            var lift = Mathf.Clamp01((through - HeelLiftStart) / (1f - HeelLiftStart));
            footAngle = HeelLift * lift * lift * (3f - 2f * lift);
            return toes + Rotate(new Vector2(-body.foot, body.ankleHeight), -footAngle);
        }

        /// <summary>The ankle of a foot in the air: a curve from its push-off through the swing keys to its next contact.</summary>
        private static Vector2 SwingAnkle(float legPhase, RunGaitSettings settings, RunBody body, out float footAngle)
        {
            var leg = body.Leg;
            var start = StanceAnkle(1f, settings, body, out var startAngle);
            knotT[0] = RunGaitSettings.Duty;
            knotX[0] = start.x;
            knotY[0] = start.y;
            knotA[0] = startAngle;
            for (var i = 0; i < SwingPhase.Length; i++)
            {
                knotT[i + 1] = SwingPhase[i];
                knotX[i + 1] = SwingX[i] * leg - Behind(settings, body);
                knotY[i + 1] = body.ankleHeight + SwingY[i] * leg;
                knotA[i + 1] = SwingAngle[i];
            }
            knotT[6] = 1f;
            knotX[6] = settings.StanceLength * 0.5f - Behind(settings, body);
            knotY[6] = body.ankleHeight;
            knotA[6] = 0f;
            // At push-off the foot leaves going back as fast as it went on the ground; at
            // touchdown it is coming back at half that, so it meets the ground nearly at rest.
            var groundSpeed = -settings.StanceLength / RunGaitSettings.Duty;
            var smooth = settings.smoothing;
            var x = Curve(knotT, knotX, legPhase, groundSpeed, groundSpeed * 0.5f, smooth);
            var y = Curve(knotT, knotY, legPhase, float.NaN, float.NaN, smooth);
            footAngle = Curve(knotT, knotA, legPhase, float.NaN, float.NaN, smooth);
            return new Vector2(x, y);
        }

        private static void Arm(
            float phase,
            Vector2 shoulder,
            float lean,
            RunGaitSettings settings,
            RunBody body,
            ref RunLimb arm
        )
        {
            var smooth = settings.smoothing;
            var swing = Channel(RightArmSwing, phase, smooth) * settings.armSwing + lean * 0.5f;
            var upper = new Vector2(Mathf.Sin(swing * Mathf.Deg2Rad), -Mathf.Cos(swing * Mathf.Deg2Rad));
            var lower = Rotate(upper, Channel(RightElbow, phase, smooth));
            arm.root = shoulder;
            arm.middle = shoulder + upper * body.upperArm;
            arm.end = arm.middle + lower * body.forearm;
            arm.tip = arm.end + lower * body.hand;
            arm.contact = false;
            arm.bearing = arm.tip;
        }

        /// <summary>A periodic channel keyed at the eight key poses, at `phase`.</summary>
        private static float Channel(float[] keys, float phase, float smoothing)
        {
            var at = Wrap(phase) * KeyCount;
            var i = Mathf.FloorToInt(at) % KeyCount;
            var u = at - Mathf.Floor(at);
            var p0 = keys[(i + KeyCount - 1) % KeyCount];
            var p1 = keys[i];
            var p2 = keys[(i + 1) % KeyCount];
            var p3 = keys[(i + 2) % KeyCount];
            var straight = Mathf.Lerp(p1, p2, u);
            var curved = Hermite(p1, p2, (p2 - p0) * 0.5f, (p3 - p1) * 0.5f, u);
            return Mathf.Lerp(straight, curved, smoothing);
        }

        /// <summary>
        /// A curve through knots at uneven times (Catmull-Rom slopes), at `t`. The slopes at
        /// the two ends are given per unit of t, or NaN for the slope of the first or last span.
        /// </summary>
        private static float Curve(float[] t, float[] v, float at, float startSlope, float endSlope, float smoothing)
        {
            var last = t.Length - 1;
            var i = 0;
            while (i < last - 1 && at > t[i + 1])
            {
                i++;
            }
            var span = Mathf.Max(t[i + 1] - t[i], 1e-6f);
            var u = Mathf.Clamp01((at - t[i]) / span);
            var straight = Mathf.Lerp(v[i], v[i + 1], u);
            var m0 = Slope(t, v, i, startSlope, endSlope);
            var m1 = Slope(t, v, i + 1, startSlope, endSlope);
            var curved = Hermite(v[i], v[i + 1], m0 * span, m1 * span, u);
            return Mathf.Lerp(straight, curved, smoothing);
        }

        private static float Slope(float[] t, float[] v, int i, float startSlope, float endSlope)
        {
            var last = t.Length - 1;
            if (i == 0)
            {
                return float.IsNaN(startSlope) ? (v[1] - v[0]) / (t[1] - t[0]) : startSlope;
            }
            if (i == last)
            {
                return float.IsNaN(endSlope) ? (v[last] - v[last - 1]) / (t[last] - t[last - 1]) : endSlope;
            }
            return (v[i + 1] - v[i - 1]) / (t[i + 1] - t[i - 1]);
        }

        private static float Hermite(float p1, float p2, float m1, float m2, float u)
        {
            var u2 = u * u;
            var u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * p1 + (u3 - 2f * u2 + u) * m1 + (-2f * u3 + 3f * u2) * p2 + (u3 - u2) * m2;
        }
    }
}
