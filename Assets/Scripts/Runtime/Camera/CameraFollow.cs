using UnityEngine;

namespace Descender.CameraRig
{
    /// <summary>
    /// How the camera's aim follows the hero, after the side-scroller practice in Itay
    /// Keren's "Scroll Back" (GDC 2015): the look-ahead travels to the side he faces only
    /// while he moves, faster the faster he runs (Cave Story), so turning on the spot does
    /// not swing the view; and the view's height is where he last stood (platform snapping,
    /// Super Mario World), held through a jump unless he climbs a jump's height above it or
    /// drops below it, eased to the new floor on landing.
    /// </summary>
    public static class CameraFollow
    {
        /// <summary>
        /// The look-ahead, in units, a step on from `current`: toward `facing` times `reach`,
        /// at `catchUp` of the hero's horizontal speed, never past it. Standing still, it
        /// stays where it is.
        /// </summary>
        public static float LookAhead(float current, int facing, float reach, float heroSpeedX, float catchUp, float dt)
        {
            var wanted = (facing < 0 ? -1f : 1f) * reach;
            var rate = Mathf.Abs(heroSpeedX) * Mathf.Max(catchUp, 0f);
            return Mathf.MoveTowards(current, wanted, rate * Mathf.Max(dt, 0f));
        }

        /// <summary>
        /// The height the view frames, a step on from `baseline`: the hero's own while he
        /// stands; in the air, held unless he rises more than `rise` above it or drops more
        /// than `drop` below it, when it follows at that distance (no jump between the two).
        /// </summary>
        public static float Baseline(float baseline, float heroY, bool grounded, float rise, float drop)
        {
            if (grounded)
            {
                return heroY;
            }
            return Mathf.Clamp(baseline, heroY - Mathf.Max(rise, 0f), heroY + Mathf.Max(drop, 0f));
        }
    }
}
