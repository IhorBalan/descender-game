using Descender.Economy;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The numbers the ported enemies (the Godot prototype's walker, flyer, hanged thing
    /// and boss) kept as constants rather than in their profiles, kept here so the
    /// foes <see cref="FoeKinds"/> makes from those profiles fight as they did. Level
    /// pixels and seconds.
    /// </summary>
    public static class FoeNumbers
    {
        /// <summary>A walker's habits.</summary>
        public static class Walker
        {
            public const float DeathHitStop = 0.06f;

            /// <summary>How far above and below its feet the hero's may be for a pounce, and above for a lunge.</summary>
            public const float PounceAbove = 48f;
            public const float PounceBelow = 8f;
            public const float LungeAbove = 24f;

            // Lunge
            public const float LungeSight = 200f;
            public const float LungeSpeed = 2.4f;
            public const float LungeMin = 28f;
            public const float LungeMax = 72f;
            public const float LungeWindup = 0.25f;
            public static readonly Vector2 LungeVelocity = new Vector2(210f, 90f);
            public const float LungeCooldown = 1.2f;

            // Hurl
            public const float HurlRange = 150f;
            public const float HurlHeightTolerance = 48f;
            public const float HurlRetreat = 48f;
            public const float HurlWindup = 0.5f;
            public const float HurlRecover = 0.5f;
            public const float HurlCooldown = 1.8f;

            // Slam
            public const float SlamMax = 110f;
            public const float SlamWindup = 0.7f;
            public const float SlamRecover = 0.9f;
            public const float SlamCooldown = 2.5f;

            /// <summary>The habit a placement names, as the Godot placements name it, in any case; anything else is none.</summary>
            public static WalkerBehaviour Parse(string godotName)
            {
                switch ((godotName ?? "").Trim().ToLowerInvariant())
                {
                    case "chase":
                        return WalkerBehaviour.Chase;
                    case "ambush":
                        return WalkerBehaviour.Ambush;
                    case "pounce":
                        return WalkerBehaviour.Pounce;
                    case "hurl":
                        return WalkerBehaviour.Hurl;
                    case "slam":
                        return WalkerBehaviour.Slam;
                    case "lunge":
                        return WalkerBehaviour.Lunge;
                    default:
                        return WalkerBehaviour.None;
                }
            }
        }

        /// <summary>A flyer that dives.</summary>
        public static class Flyer
        {
            /// <summary>Where a diving flyer hangs over the hero's feet before it comes down.</summary>
            public static readonly Vector2 DivePerch = new Vector2(0f, 64f);
            public const float AimTime = 0.5f;
            public const float DiveSpeed = 190f;
            public const float DiveCooldown = 1.5f;
        }

        /// <summary>The hanged thing.</summary>
        public static class Hanger
        {
            public static readonly Vector2 BodySize = new Vector2(16f, 44f);
            public const float MaxFallSpeed = 300f;
            public const float DropRangeX = 18f;
            public const float DropRangeY = 220f;
            public const float CreakTime = 0.35f;
            public const float RestTime = 1.4f;
            public const float DropCooldown = 1f;
            public const int DropDamage = 2;
            public const int ContactDamage = 1;
            public static readonly Color DeathPuff = new Color(0.8f, 0.88f, 1.0f);
            public const float DropChance = 0.5f;

            /// <summary>What it leaks and carried: a traitor's light, and a bone now and then.</summary>
            public static readonly LootTable Loot = new LootTable
            {
                LightMin = 7,
                LightMax = 10,
                BoneChance = 0.12f,
                FlaskChance = 0.04f,
                Xp = 8,
            };
        }

        /// <summary>A boss.</summary>
        public static class Boss
        {
            /// <summary>The share of its health at which a boss changes phase, and how much shorter its windups are after.</summary>
            public const float PhaseTwoAt = 0.5f;
            public const float PhaseTwoHaste = 0.7f;

            /// <summary>The share of its health at which a boss with a third move changes again.</summary>
            public const float PhaseThreeAt = 0.25f;
            public const float AttackHeightTolerance = 32f;
            public const float SlamRange = 130f;
            public const float SlamWindup = 0.7f;
            public const float SlamRecover = 0.8f;
            public const float VolleyMin = 56f;
            public const float VolleyMax = 220f;
            public const int SummonCount = 2;
        }
    }
}
