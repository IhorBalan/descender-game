using System.Collections.Generic;
using Descender.Save;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// The construction rules of docs/design/paradise.md, section 2, as numbers: the
    /// largest gap and step up the main path may ask of the hero at an x (which grows
    /// with the abilities ParadiseLayout.Grants gives him on the way), the longest fall
    /// onto hard ground, and how far apart save shrines may stand. ParadiseChecker walks
    /// each zone's main path against them.
    /// </summary>
    public static class ParadiseRules
    {
        /// <summary>A fall of more than two screens (22 tiles) must land on something soft.</summary>
        public const float MaxFall = 22f;

        /// <summary>A save shrine stands on the main path: within this many tiles of one of its surfaces.</summary>
        public const float ShrineReach = 1f;

        /// <summary>
        /// The longest stretch of x without a save shrine. The design says "every 400 to 600
        /// tiles"; only the maximum is enforced, because zones 1 and 2 as built space theirs
        /// at 290 to 384 (decision D-0006 in docs/delivery/STATE.md).
        /// </summary>
        public const float MaxShrineSpacing = 600f;

        /// <summary>The region holds at least this many save shrines (a floor, not a total: D-0006).</summary>
        public const int MinShrines = 15;

        /// <summary>The largest gap and step up, in tiles, the main path may ask for.</summary>
        public readonly struct Limits
        {
            public readonly float gap;
            public readonly float step;

            public Limits(float gap, float step)
            {
                this.gap = gap;
                this.step = step;
            }
        }

        public static readonly Limits BeforeWings = new Limits(4f, 4f);
        public static readonly Limits WithWings = new Limits(7f, 7f);

        /// <summary>
        /// The design's table leaves the step with Wings and the dash blank: the dash adds
        /// reach, not height, so the Wings step of 7 is carried forward.
        /// </summary>
        public static readonly Limits WithWingsAndDash = new Limits(9f, 7f);

        /// <summary>The limits that apply to a jump taken at x, from the abilities the region has granted by then.</summary>
        public static Limits For(float x) => For(x, ParadiseLayout.Grants);

        /// <summary>The limits at x under a given list of grants. The dash without Wings changes nothing.</summary>
        public static Limits For(float x, IReadOnlyList<ParadiseLayout.Grant> grants)
        {
            var wings = false;
            var dash = false;
            foreach (var grant in grants)
            {
                if (grant.x > x)
                {
                    continue;
                }
                wings |= grant.ability == Ability.Wings;
                dash |= grant.ability == Ability.LightDash;
            }
            if (!wings)
            {
                return BeforeWings;
            }
            return dash ? WithWingsAndDash : WithWings;
        }
    }
}
