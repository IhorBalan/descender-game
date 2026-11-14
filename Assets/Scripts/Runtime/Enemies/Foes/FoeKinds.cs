using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The old enemies as recipes: a <see cref="WalkerProfile"/> and the habit a
    /// placement names become a <see cref="FoeRecipe"/> with the same numbers, so a
    /// region's walkers stand as foes without their data changing.
    /// </summary>
    public static class FoeKinds
    {
        /// <summary>A hero's middle is this far above his feet, level pixels: the old ranges were feet to feet.</summary>
        private const float HeroMiddle = 16f;

        /// <summary>
        /// A walker as a foe: its body, its swing and its drops from the profile, and after
        /// the swing the parts of its habit ("chase", "ambush", "pounce", "lunge", "hurl",
        /// "slam"; anything else only patrols). A passive one only patrols and its touch
        /// does not hurt.
        /// </summary>
        public static FoeRecipe FromWalker(string name, WalkerProfile profile, string habit, bool passive)
        {
            var kind = Walker.Parse(habit);
            var body = new FoeBody
            {
                size = profile.body,
                health = Mathf.Max(profile.health, 1),
                speed = profile.speed,
                contactDamage = passive ? 0 : 1,
                armour = profile.armour,
                shape = ShapeOf(kind),
                color = new Color(profile.puff.r, profile.puff.g, profile.puff.b),
            };
            var recipe = new FoeRecipe(name ?? "", body);
            recipe.behaviours = passive ? new FoeBehaviour[] { new Patrol() } : Habits(profile, kind);
            var drops = new FoeDrops
            {
                moteChance = profile.dropChance,
                powerChance = profile.powerChance,
                power = profile.power,
                loot = profile.loot,
            };
            recipe.drops = drops;
            return recipe;
        }

        private static FoeBehaviour[] Habits(WalkerProfile profile, WalkerBehaviour kind)
        {
            var swing = new Swing
            {
                // The old range was from its middle; a swing's is from the edge of its body.
                range = Mathf.Max(profile.attackRange - profile.body.x / 2f, 0f),
                reach = profile.reach,
                windup = profile.windup,
                strike = profile.strike,
                recover = profile.recover,
                cooldown = profile.cooldown,
                damage = profile.attackDamage,
            };
            // How far its middle is above where the hero's would be on the same floor.
            var taller = profile.body.y / 2f - HeroMiddle;
            switch (kind)
            {
                case WalkerBehaviour.Chase:
                    return new FoeBehaviour[] { swing, new Chase(), new Patrol() };
                case WalkerBehaviour.Ambush:
                    return new FoeBehaviour[] { new Ambush(), swing, new Chase(), new Patrol() };
                case WalkerBehaviour.Pounce:
                    return new FoeBehaviour[] { swing, Leap(profile, taller), new Patrol() };
                case WalkerBehaviour.Lunge:
                    return new FoeBehaviour[] { swing, Lunge(profile, taller), Run(), new Patrol() };
                case WalkerBehaviour.Hurl:
                    return new FoeBehaviour[] { swing, Hurl(), Back(), new Patrol() };
                case WalkerBehaviour.Slam:
                    return new FoeBehaviour[] { swing, Slam(profile), new Patrol() };
                default:
                    return new FoeBehaviour[] { swing, new Patrol() };
            }
        }

        private static Pounce Leap(WalkerProfile profile, float taller)
        {
            return new Pounce
            {
                above = Walker.PounceAbove - taller,
                below = Walker.PounceBelow + taller,
                reach = profile.reach,
                damage = profile.attackDamage,
            };
        }

        private static Pounce Lunge(WalkerProfile profile, float taller)
        {
            return new Pounce
            {
                nearest = Walker.LungeMin,
                farthest = Walker.LungeMax,
                above = Walker.LungeAbove - taller,
                below = Walker.PounceBelow + taller,
                windup = Walker.LungeWindup,
                spring = Walker.LungeVelocity,
                cooldown = Walker.LungeCooldown,
                reach = profile.reach,
                damage = profile.attackDamage,
            };
        }

        private static Chase Run()
        {
            return new Chase { sight = Walker.LungeSight, pace = Walker.LungeSpeed };
        }

        private static Shoot Hurl()
        {
            return new Shoot
            {
                nearest = Walker.HurlRetreat,
                range = Walker.HurlRange,
                windup = Walker.HurlWindup,
                recover = Walker.HurlRecover,
                cooldown = Walker.HurlCooldown,
            };
        }

        private static Retreat Back()
        {
            var back = new Retreat();
            back.within = Walker.HurlRetreat;
            back.heightTolerance = Walker.HurlHeightTolerance;
            return back;
        }

        private static Quake Slam(WalkerProfile profile)
        {
            return new Quake
            {
                nearest = profile.attackRange,
                farthest = Walker.SlamMax,
                windup = Walker.SlamWindup,
                recover = Walker.SlamRecover,
                cooldown = Walker.SlamCooldown,
            };
        }

        private static ShapeKind ShapeOf(WalkerBehaviour kind)
        {
            switch (kind)
            {
                case WalkerBehaviour.Ambush:
                case WalkerBehaviour.Pounce:
                case WalkerBehaviour.Lunge:
                    return ShapeKind.Triangle;
                case WalkerBehaviour.Hurl:
                    return ShapeKind.Circle;
                case WalkerBehaviour.Slam:
                    return ShapeKind.Diamond;
                default:
                    return ShapeKind.Square;
            }
        }
    }
}
