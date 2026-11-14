using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The old enemies as recipes: a <see cref="WalkerProfile"/> and the habit a
    /// placement names, a <see cref="FlyerProfile"/>, or the hanged thing's constants
    /// become a <see cref="FoeRecipe"/> with the same numbers, so a region's enemies
    /// stand as foes without their data changing.
    /// </summary>
    public static class FoeKinds
    {
        /// <summary>A hero's middle is this far above his feet, level pixels: the old ranges were feet to feet.</summary>
        private const float HeroMiddle = 16f;

        /// <summary>How wide a flyer drifts about its post, and how near above him one that dives must be, level pixels.</summary>
        public const float FlyerDrift = 48f;
        public const float FlyerDiveRange = 16f;

        /// <summary>How fast the rope hauls the hanged thing back up, level pixels a second.</summary>
        public const float HangerRise = 120f;

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

        /// <summary>
        /// A flyer as a foe: a flying body of a size with the profile's health, drifting at
        /// its hover speed and coming for the hero at its chase speed while he is within
        /// its aggro. One that dives hangs over him instead and comes down on him.
        /// </summary>
        public static FoeRecipe FromFlyer(string name, FlyerProfile profile, Vector2 size, bool dives)
        {
            var body = new FoeBody
            {
                size = size,
                health = Mathf.Max(profile.health, 1),
                speed = profile.hover,
                flies = true,
                shape = dives ? ShapeKind.Triangle : ShapeKind.Circle,
                color = new Color(profile.puff.r, profile.puff.g, profile.puff.b),
            };
            var haunt = new Haunt { sight = profile.aggro, pace = profile.chase };
            var drift = new Hover { span = FlyerDrift };
            var recipe = new FoeRecipe(name ?? "", body, haunt, drift);
            if (dives)
            {
                haunt.above = Flyer.DivePerch.y - HeroMiddle;
                var dive = new Dive
                {
                    range = FlyerDiveRange,
                    depth = profile.aggro,
                    windup = Flyer.AimTime,
                    speed = Flyer.DiveSpeed,
                    cooldown = Flyer.DiveCooldown,
                };
                recipe.behaviours = new FoeBehaviour[] { dive, haunt, drift };
            }
            var drops = new FoeDrops { moteChance = profile.dropChance, powerChance = 0f };
            drops.loot = profile.loot;
            recipe.drops = drops;
            return recipe;
        }

        /// <summary>
        /// The hanged thing as a foe: it hangs where it is put, comes straight down on a
        /// hero who walks beneath it, lies where it lands for a while, and is hauled back
        /// up. The drop itself takes two hearts.
        /// </summary>
        public static FoeRecipe FromHanger(string name, int health)
        {
            var body = new FoeBody
            {
                size = Hanger.BodySize,
                health = Mathf.Max(health, 1),
                speed = 0f,
                contactDamage = Hanger.ContactDamage,
                // It is on a rope: a cut does not swing it away.
                pushShare = 0f,
                flies = true,
                color = Hanger.DeathPuff,
            };
            var drop = new Dive
            {
                range = Hanger.DropRangeX,
                depth = Hanger.DropRangeY,
                windup = Hanger.CreakTime,
                speed = Hanger.MaxFallSpeed,
                recover = Hanger.RestTime,
                riseSpeed = HangerRise,
                cooldown = Hanger.DropCooldown,
                damage = Hanger.DropDamage,
            };
            var recipe = new FoeRecipe(name ?? "", body, drop, new Hover { span = 0f });
            var drops = new FoeDrops { moteChance = Hanger.DropChance, powerChance = 0f };
            drops.loot = Hanger.Loot;
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
