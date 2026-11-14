using System.Collections.Generic;
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

        /// <summary>How near a sleeping boss lets the hero come, level pixels: half its old arena.</summary>
        public const float BossWake = 128f;

        /// <summary>How long a boss warns as it wakes, seconds.</summary>
        public const float BossWarning = 1f;

        /// <summary>Seconds between a boss's attacks in its first phase; later phases rest less.</summary>
        public const float BossRest = 0.6f;

        /// <summary>How far to either side a boss charges from and runs, level pixels.</summary>
        public const float BossChargeRange = 140f;

        /// <summary>How long a boss warns before its bolt or its call, seconds.</summary>
        public const float BossVolleyWindup = 0.5f;

        public static readonly Color BossColor = new Color(0.85f, 0.75f, 0.45f);

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

        /// <summary>
        /// A boss as a foe: armoured and never pushed, it stands where it is put until the
        /// hero comes within `wake` of it, warns, and then fights in phases built from its
        /// profile: a swing and a charge, and the move its body has (a brute's wave, an
        /// angel's bolts, an imp king's imps, called from `imp`). At half its health it
        /// changes phase and every windup is shorter; a boss with a third move changes
        /// again at a quarter and is quicker still. It leaves its motes, its loot and the
        /// flag that keeps it dead.
        /// </summary>
        public static FoeRecipe FromBoss(string id, BossProfile profile, FoeRecipe imp)
        {
            var body = new FoeBody
            {
                size = profile.body,
                health = Mathf.Max(profile.health, 1),
                speed = profile.speed,
                contactDamage = profile.contactDamage,
                armour = true,
                pushShare = 0f,
                shape = ShapeKind.Diamond,
                color = BossColor,
            };
            var last = string.IsNullOrEmpty(profile.third) ? 0f : Boss.PhaseThreeAt;
            var phases = new List<BossPhase>();
            phases.Add(new BossPhase(Boss.PhaseTwoAt, BossRest, BossAttacks(profile, imp, 1f)));
            var quick = Boss.PhaseTwoHaste;
            phases.Add(new BossPhase(last, BossRest * quick, BossAttacks(profile, imp, quick)));
            if (last > 0f)
            {
                var quicker = quick * quick;
                phases.Add(new BossPhase(0f, BossRest * quicker, BossAttacks(profile, imp, quicker)));
            }
            var pattern = new Pattern();
            pattern.phases = phases.ToArray();
            pattern.stopAt = Mathf.Max(profile.attackRange - profile.body.x / 2f - 8f, 0f);
            var wake = new Ambush { range = BossWake, mark = BossWarning };
            var title = string.IsNullOrEmpty(profile.displayName) ? id : profile.displayName;
            var recipe = new FoeRecipe(title, body);
            recipe.behaviours = new FoeBehaviour[] { wake, pattern };
            var drops = new FoeDrops
            {
                moteChance = 0f,
                powerChance = 0f,
                loot = profile.loot,
                motes = profile.motes,
                flag = BossIds.FlagFor(id),
            };
            recipe.drops = drops;
            return recipe;
        }

        /// <summary>One phase's attacks, their windups shortened by `haste` (1 for none).</summary>
        private static FoeBehaviour[] BossAttacks(BossProfile profile, FoeRecipe imp, float haste)
        {
            var swing = new Swing
            {
                range = Mathf.Max(profile.attackRange - profile.body.x / 2f, 0f),
                heightTolerance = Boss.AttackHeightTolerance,
                reach = profile.reach,
                windup = profile.windup * haste,
                strike = profile.strike,
                recover = profile.recover * haste,
                cooldown = 0f,
                damage = profile.attackDamage,
            };
            var charge = new Charge
            {
                range = BossChargeRange,
                heightTolerance = Boss.AttackHeightTolerance,
                windup = profile.chargeWindup * haste,
                speed = profile.chargeSpeed,
                distance = BossChargeRange,
                cooldown = 0f,
                reach = new Vector2(14f, profile.body.y * 0.75f),
                damage = profile.attackDamage,
            };
            var move = BossMove(profile, imp, haste);
            if (move == null)
            {
                return new FoeBehaviour[] { swing, charge };
            }
            return new FoeBehaviour[] { swing, move, charge };
        }

        /// <summary>The move its body has: a brute's wave, an angel's bolt, an imp king's imps; null for none.</summary>
        private static FoeBehaviour BossMove(BossProfile profile, FoeRecipe imp, float haste)
        {
            switch (profile.character)
            {
                case "brute":
                {
                    var quake = new Quake
                    {
                        nearest = 0f,
                        farthest = Boss.SlamRange,
                        heightTolerance = Boss.AttackHeightTolerance,
                        windup = Boss.SlamWindup * haste,
                        recover = Boss.SlamRecover * haste,
                        cooldown = 0f,
                        big = true,
                    };
                    return quake;
                }
                case "angel_guard":
                {
                    var volley = new Shoot
                    {
                        nearest = Boss.VolleyMin,
                        range = Boss.VolleyMax,
                        reach = Boss.VolleyMax + 40f,
                        windup = BossVolleyWindup * haste,
                        cooldown = 0f,
                        damage = 1,
                    };
                    return volley;
                }
                case "imp":
                {
                    var call = new Summon
                    {
                        summons = imp,
                        cap = Boss.SummonCount,
                        range = BossChargeRange * 3f,
                        windup = BossVolleyWindup * haste,
                        cooldown = 0f,
                    };
                    return call;
                }
                default:
                    return null;
            }
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
