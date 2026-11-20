using UnityEngine;

namespace Descender.Motion
{
    /// <summary>The eight key poses of a running cycle, a step of the right leg and then of the left.</summary>
    public enum RunKey
    {
        /// <summary>The right foot comes down ahead of the hips.</summary>
        ContactRight,

        /// <summary>The right leg takes the weight: the hips at their lowest, the knee most bent.</summary>
        DownRight,

        /// <summary>The left leg passes the right, its knee driving forward.</summary>
        PassingRight,

        /// <summary>The right foot has pushed off: both feet in the air, the hips at their highest.</summary>
        FlightRight,

        /// <summary>The left foot comes down ahead of the hips.</summary>
        ContactLeft,

        /// <summary>The left leg takes the weight.</summary>
        DownLeft,

        /// <summary>The right leg passes the left.</summary>
        PassingLeft,

        /// <summary>The left foot has pushed off: both feet in the air.</summary>
        FlightLeft,
    }

    /// <summary>One limb's joints: from the body out (hip, knee, ankle, toes; or shoulder, elbow, wrist, fingers).</summary>
    public struct RunLimb
    {
        public Vector2 root;
        public Vector2 middle;
        public Vector2 end;
        public Vector2 tip;

        /// <summary>For a leg: whether its foot is on the ground.</summary>
        public bool contact;

        /// <summary>For a leg: where on the ground the foot bears (its toes once the heel is up).</summary>
        public Vector2 bearing;
    }

    /// <summary>
    /// The whole body at one moment of the run, every joint relative to the root (the point
    /// on the ground under the hips, which travels at the running speed). Filled in place by
    /// <see cref="RunGait.Evaluate"/>, so one is reused every frame.
    /// </summary>
    public sealed class RunPose
    {
        /// <summary>0 .. 1 through the cycle: 0 is the right foot's contact, 0.5 the left's.</summary>
        public float phase;

        /// <summary>The key pose the moment is nearest.</summary>
        public RunKey key;

        public Vector2 hips;
        public Vector2 chest;
        public Vector2 neck;
        public Vector2 head;
        public RunLimb leftLeg;
        public RunLimb rightLeg;
        public RunLimb leftArm;
        public RunLimb rightArm;

        /// <summary>Degrees the torso leans forward at this moment.</summary>
        public float lean;

        /// <summary>Whether neither foot is on the ground.</summary>
        public bool InFlight => !leftLeg.contact && !rightLeg.contact;
    }
}
