using System;
using UnityEngine;

namespace Descender.Motion
{
    /// <summary>Which of speed, stride and cycle follows from the other two.</summary>
    public enum RunDerived
    {
        /// <summary>The cycle is the stride over the speed: a faster run turns the legs over faster.</summary>
        CycleDuration,

        /// <summary>The stride is the speed times the cycle: a faster run takes longer strides.</summary>
        StrideLength,

        /// <summary>The speed is the stride over the cycle.</summary>
        RunningSpeed,
    }

    /// <summary>
    /// The numbers of the run that the <see cref="RunGait"/> plays, edited on a
    /// <see cref="RunSkeleton"/> in the inspector. Speed, stride (the ground covered in one
    /// whole cycle, a step of each leg) and cycle duration are tied, speed = stride /
    /// cycle: <see cref="derived"/> names the one worked out from the other two.
    /// </summary>
    [Serializable]
    public sealed class RunGaitSettings
    {
        /// <summary>The part of a cycle each foot is on the ground; under a half, so a run has two flights.</summary>
        public const float Duty = 0.34f;

        [Tooltip("Which of speed, stride and cycle is worked out from the other two.")]
        public RunDerived derived = RunDerived.CycleDuration;

        [Tooltip("Units a second the body travels.")]
        [Range(1f, 14f)]
        public float runningSpeed = 5.5f;

        [Tooltip("Units travelled in one whole cycle (a step of each leg).")]
        [Range(1f, 6f)]
        public float strideLength = 4f;

        [Tooltip("Seconds for one whole cycle (a step of each leg).")]
        [Range(0.3f, 1.6f)]
        public float cycleDuration = 0.72f;

        [Tooltip("Degrees the whole body leans forward from the feet: the feet fall that much behind the hips.")]
        [Range(0f, 20f)]
        public float bodyLean = 8f;

        [Tooltip("Degrees the torso leans forward from the hips.")]
        [Range(0f, 35f)]
        public float torsoLean = 18f;

        [Tooltip("Units the hips rise and fall over a step.")]
        [Range(0f, 0.2f)]
        public float verticalBounce = 0.06f;

        [Tooltip("Degrees each upper arm swings forward and back from hanging.")]
        [Range(0f, 70f)]
        public float armSwing = 50f;

        [Tooltip("0 goes straight from key pose to key pose; 1 eases through them on a curve.")]
        [Range(0f, 1f)]
        public float smoothing = 1f;

        [Tooltip("Hips and shoulders turning against each other, as a part of the half-width of each.")]
        [Range(0f, 1f)]
        public float counterRotation = 0.6f;

        /// <summary>Works the derived one out from the other two and keeps every number in its range.</summary>
        public void Resolve()
        {
            runningSpeed = Mathf.Clamp(runningSpeed, 1f, 14f);
            strideLength = Mathf.Clamp(strideLength, 1f, 6f);
            cycleDuration = Mathf.Clamp(cycleDuration, 0.3f, 1.6f);
            switch (derived)
            {
                case RunDerived.CycleDuration:
                    cycleDuration = strideLength / runningSpeed;
                    break;
                case RunDerived.StrideLength:
                    strideLength = runningSpeed * cycleDuration;
                    break;
                default:
                    runningSpeed = strideLength / cycleDuration;
                    break;
            }
        }

        /// <summary>The ground a foot travels under the hips while it is down: the stride times the duty.</summary>
        public float StanceLength => strideLength * Duty;
    }

    /// <summary>The lengths of the placeholder body, in units (a tile is one): about two and a half tall.</summary>
    [Serializable]
    public sealed class RunBody
    {
        [Min(0.1f)]
        public float thigh = 0.52f;

        [Min(0.1f)]
        public float shin = 0.5f;

        [Tooltip("Height of the ankle over the sole.")]
        [Min(0f)]
        public float ankleHeight = 0.08f;

        [Tooltip("From the ankle to the toes along the sole.")]
        [Min(0.05f)]
        public float foot = 0.26f;

        [Tooltip("Half the distance between the hip joints.")]
        [Min(0f)]
        public float pelvisHalfWidth = 0.1f;

        [Tooltip("From the hips to the line of the shoulders.")]
        [Min(0.1f)]
        public float spine = 0.62f;

        [Tooltip("Half the distance between the shoulders.")]
        [Min(0f)]
        public float shoulderHalfWidth = 0.13f;

        [Min(0f)]
        public float neck = 0.12f;

        [Tooltip("Radius of the head.")]
        [Min(0.05f)]
        public float head = 0.16f;

        [Min(0.1f)]
        public float upperArm = 0.36f;

        [Min(0.1f)]
        public float forearm = 0.31f;

        [Min(0f)]
        public float hand = 0.08f;

        /// <summary>
        /// The crowned knight's lengths, measured on his standing frame (125 of its pixels a
        /// unit; tools/hero_knight/parts.py cuts the parts that wear them).
        /// </summary>
        public static RunBody Knight() =>
            new RunBody
            {
                thigh = 0.6f,
                shin = 0.41f,
                ankleHeight = 0.2f,
                foot = 0.24f,
                pelvisHalfWidth = 0.1f,
                spine = 0.77f,
                shoulderHalfWidth = 0.14f,
                neck = 0.06f,
                head = 0.2f,
                upperArm = 0.4f,
                forearm = 0.48f,
                hand = 0.06f,
            };

        /// <summary>The whole leg, hip to ankle.</summary>
        public float Leg => thigh + shin;
    }
}
