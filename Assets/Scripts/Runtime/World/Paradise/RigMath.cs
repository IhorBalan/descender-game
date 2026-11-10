using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// The geometry HeroRig poses Ansel with, kept apart so it can be tested on its own:
    /// two-bone reach for an arm, lifting a cloth joint off the ground, the angle a glance
    /// turns the head through, and which cut of the combo comes next.
    /// </summary>
    public static class RigMath
    {
        /// <summary>
        /// Where the elbow goes for a hand to reach <paramref name="target"/> from
        /// <paramref name="shoulder"/>, bending downward (elbows hang). Out of reach, the arm
        /// points straight at the target.
        /// </summary>
        public static Vector2 Elbow(Vector2 shoulder, Vector2 target, float upper, float fore)
        {
            var to = target - shoulder;
            var direction = to.sqrMagnitude > 1e-8f ? to.normalized : Vector2.down;
            var distance = Mathf.Clamp(to.magnitude, Mathf.Abs(upper - fore) + 1e-4f, (upper + fore) * 0.999f);
            var cos = Mathf.Clamp(
                (upper * upper + distance * distance - fore * fore) / (2f * upper * distance),
                -1f,
                1f
            );
            var bend = Mathf.Acos(cos);
            var one = shoulder + Rotate(direction, bend) * upper;
            var other = shoulder + Rotate(direction, -bend) * upper;
            return one.y <= other.y ? one : other;
        }

        public static Vector2 Rotate(Vector2 v, float radians)
        {
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>
        /// Degrees (counterclockwise) to turn a joint at <paramref name="pivot"/> so its tip, now
        /// at <paramref name="tip"/>, rests on the ground at <paramref name="groundY"/> on the
        /// same side. Zero when the tip is above the ground or the joint cannot reach it.
        /// </summary>
        public static float LiftToGround(Vector2 pivot, Vector2 tip, float groundY)
        {
            if (tip.y >= groundY)
            {
                return 0f;
            }
            var arm = tip - pivot;
            var length = arm.magnitude;
            var drop = groundY - pivot.y;
            if (length < 1e-4f || drop >= 0f || -drop >= length)
            {
                return 0f;
            }
            var side = arm.x < 0f ? -1f : 1f;
            var rested = new Vector2(side * Mathf.Sqrt(length * length - drop * drop), drop);
            return Vector2.SignedAngle(arm, rested);
        }

        /// <summary>
        /// How far the head tips (degrees, + is up) to look from <paramref name="eye"/> at
        /// <paramref name="target"/> while facing <paramref name="face"/>, within the limits;
        /// NaN when the target is behind him, where a glance would need a turn.
        /// </summary>
        public static float LookAngle(Vector2 eye, Vector2 target, int face, float down, float up)
        {
            var ahead = (target.x - eye.x) * face;
            if (ahead < 0.3f)
            {
                return float.NaN;
            }
            var angle = Mathf.Atan2(target.y - eye.y, ahead) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle, -down, up);
        }

        /// <summary>The next cut of a combo of <paramref name="cuts"/>: on from the last if it came within the window, else the first.</summary>
        public static int NextCut(int last, float sinceLastEnded, float window, int cuts)
        {
            return sinceLastEnded <= window ? (last + 1) % cuts : 0;
        }
    }
}
