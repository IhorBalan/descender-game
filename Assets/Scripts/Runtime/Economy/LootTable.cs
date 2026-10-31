using System;
using System.Collections.Generic;
using Descender.Story;

namespace Descender.Economy
{
    /// <summary>One thing a dead enemy leaves: a currency pile or an item.</summary>
    public readonly struct LootDrop
    {
        public readonly string Currency;
        public readonly int Amount;
        public readonly string ItemId;

        public static LootDrop Money(string currency, int amount) => new LootDrop(currency, amount, "");
        public static LootDrop Item(string itemId) => new LootDrop("", 0, itemId);

        private LootDrop(string currency, int amount, string itemId)
        {
            Currency = currency;
            Amount = amount;
            ItemId = itemId;
        }

        public bool IsItem => ItemId.Length > 0;
    }

    /// <summary>
    /// What a dead enemy leaves on the ground, from its profile's loot table:
    ///   light        [min, max] light, scaled by the hero's charms and skills
    ///   bone_chance  how often a kill leaves a tally bone
    ///   bones        tally bones left every time (bosses)
    ///   flask_chance how often a kill leaves a Basin Flask
    ///   unique       the item id a boss leaves, once per save
    ///   xp           the experience the kill gives the hero
    /// The scene side (spawning pickups, healing on kill) is in the enemy death
    /// behaviour; this is the pure roll, so it can be tested without a scene.
    /// </summary>
    [Serializable]
    public sealed class LootTable
    {
        public int LightMin;
        public int LightMax;
        public float BoneChance;
        public int Bones;
        public float FlaskChance;
        public string Unique = "";
        public int Xp;

        /// <summary>Drops are spread this far apart (level pixels) so a boss's pile reads as a pile.</summary>
        public const float Spread = 12f;

        /// <summary>
        /// Rolls the drops. `chance` gives a number in [0, 1) and `between` an integer in
        /// [min, max], so a test can fix the dice.
        /// </summary>
        public List<LootDrop> Roll(Func<float> chance, Func<int, int, int> between)
        {
            var drops = new List<LootDrop>();
            if (LightMax >= LightMin && LightMax > 0)
            {
                var light = between(LightMin, LightMax);
                light = (int)Math.Round(light * Inventory.Factor("light") * Skills.Factor("light"), MidpointRounding.AwayFromZero);
                if (light > 0)
                {
                    drops.Add(LootDrop.Money("light", light));
                }
            }
            var bones = Bones;
            if (chance() < BoneChance)
            {
                bones++;
            }
            for (var i = 0; i < bones; i++)
            {
                drops.Add(LootDrop.Money("bones", 1));
            }
            if (chance() < FlaskChance)
            {
                drops.Add(LootDrop.Item("flask"));
            }
            if (Items.IsUnique(Unique) && !StoryFlags.Has(Items.LootFlag(Unique)))
            {
                drops.Add(LootDrop.Item(Unique));
            }
            return drops;
        }

        /// <summary>The x offset of the i-th of n drops, centred on the body.</summary>
        public static float OffsetOf(int index, int count) => (index - (count - 1) / 2f) * Spread;
    }
}
