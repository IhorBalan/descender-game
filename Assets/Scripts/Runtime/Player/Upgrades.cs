using System;
using Descender.Story;

namespace Descender.Player
{
    /// <summary>
    /// The flags the permanent upgrades leave in the save and what they add up to:
    /// `upgrade:&lt;kind&gt;:&lt;level&gt;:&lt;n&gt;` per pickup, `sword:&lt;n&gt;` per forging
    /// at Old Pell. The hero counts his maximums back from them. Port of
    /// scripts/upgrade.gd (the static part; the pickup is UpgradePickup).
    /// </summary>
    public static class Upgrades
    {
        public const string SwordFlagPrefix = "sword:";

        private static readonly string[] FlagNames = { "heart", "vial", "iron" };

        /// <summary>The dialogue shown when an upgrade is taken: the relic text, then the line with the new maximum ({0}).</summary>
        public static readonly string[][] Descriptions =
        {
            new[]
            {
                "A little of someone's light, kept back from the Basin before it could be pressed."
                    + " It is warm, and it does not ask whose it was.",
                "Hearts: {0}.",
            },
            new[]
            {
                "Basin water in Warden's glass. The gathered drink it on the way in so they do not"
                    + " struggle. In him it only closes wounds.",
                "Healing charges: {0}.",
            },
            new[]
            {
                "A bar of church iron, off a hinge the demons hung. Old Pell on Earth can hammer it"
                    + " into the sword, if someone holds.",
                "Church iron carried: {0}.",
            },
        };

        public static string FlagName(UpgradeKind kind) => FlagNames[(int)kind];

        /// <summary>Every pickup of a kind sets a flag starting with this, whatever level it is in.</summary>
        public static string FlagPrefix(UpgradeKind kind) => "upgrade:" + FlagName(kind) + ":";

        /// <summary>`index` numbers a pickup in its level; a gift from a dialogue uses "gift" and the entry's id.</summary>
        public static string FlagFor(UpgradeKind kind, string levelId, string index) =>
            FlagPrefix(kind) + levelId + ":" + index;

        /// <summary>Extra sword damage from Pell's forge, counted from the save's flags.</summary>
        public static int SwordBonus() => CountFlags(SwordFlagPrefix);

        /// <summary>Church iron picked up and not yet hammered in; Pell's first forging uses his own stock.</summary>
        public static int IronCarried()
        {
            var found = CountFlags(FlagPrefix(UpgradeKind.Iron));
            return Math.Max(found - Math.Max(SwordBonus() - 1, 0), 0);
        }

        /// <summary>How many pickups of a kind the save holds.</summary>
        public static int Count(UpgradeKind kind) => CountFlags(FlagPrefix(kind));

        private static int CountFlags(string prefix)
        {
            var count = 0;
            foreach (var flag in StoryFlags.ToList())
            {
                if (flag.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
