using System;
using System.Collections.Generic;
using UnityEngine;

namespace Descender.CameraRig
{
    /// <summary>
    /// How the camera frames the hero at one moment: where the view centres relative to
    /// him, how much it shows, how far it looks ahead and whether an axis is held to a
    /// place. The room has one set, a <see cref="CameraZone"/> another, and the camera
    /// blends between them, so nothing here may jump when two sets are mixed.
    /// </summary>
    [Serializable]
    public struct CameraSettings
    {
        /// <summary>Where the view centres relative to the hero, in units.</summary>
        public Vector2 framing;

        /// <summary>How much is shown: 1 is the room's view, 2 shows twice as far each way.</summary>
        public float zoom;

        public float lookAheadPixels;

        /// <summary>Per axis, how firmly the view is held to <see cref="lockAt"/>: 0 follows, 1 stays.</summary>
        public Vector2 lockWeight;

        /// <summary>The place a held axis stays at, in world units.</summary>
        public Vector2 lockAt;

        /// <summary>
        /// The settings a share of the way from one set to another, eased at both ends so
        /// the view starts and stops moving gently. At 0 it is `from`, at 1 it is `to`.
        /// </summary>
        public static CameraSettings Blend(CameraSettings from, CameraSettings to, float share)
        {
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(share));
            var result = new CameraSettings();
            result.framing = Vector2.Lerp(from.framing, to.framing, t);
            result.zoom = Mathf.Lerp(from.zoom, to.zoom, t);
            result.lookAheadPixels = Mathf.Lerp(from.lookAheadPixels, to.lookAheadPixels, t);
            result.lockWeight = Vector2.Lerp(from.lockWeight, to.lockWeight, t);
            result.lockAt.x = HeldAt(from.lockAt.x, from.lockWeight.x, to.lockAt.x, to.lockWeight.x, t);
            result.lockAt.y = HeldAt(from.lockAt.y, from.lockWeight.y, to.lockAt.y, to.lockWeight.y, t);
            return result;
        }

        /// <summary>
        /// The place an axis is held at while blending. A side that does not hold the axis
        /// has no place of its own, so the other side's is used all the way: the weight
        /// fades and the place stands still.
        /// </summary>
        private static float HeldAt(float from, float fromWeight, float to, float toWeight, float t)
        {
            if (fromWeight <= 0f)
            {
                return to;
            }
            if (toWeight <= 0f)
            {
                return from;
            }
            return Mathf.Lerp(from, to, t);
        }

        /// <summary>Where the view should centre, given where following the hero would put it.</summary>
        public Vector2 Hold(Vector2 following)
        {
            var x = Mathf.Lerp(following.x, lockAt.x, lockWeight.x);
            var y = Mathf.Lerp(following.y, lockAt.y, lockWeight.y);
            return new Vector2(x, y);
        }

        /// <summary>The number of the smallest rectangle, or -1 for none: the innermost zone wins.</summary>
        public static int Innermost(IReadOnlyList<Rect> areas)
        {
            var best = -1;
            var bestArea = float.PositiveInfinity;
            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i].width * areas[i].height;
                if (area < bestArea)
                {
                    best = i;
                    bestArea = area;
                }
            }
            return best;
        }
    }
}
