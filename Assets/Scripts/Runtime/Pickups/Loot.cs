using Descender.Economy;
using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// What a dead enemy leaves on the ground, from its profile's <see cref="LootTable"/>:
    /// light (scaled by the hero's charms), tally bones, a Basin Flask now and then, and
    /// a boss's unique item once per save. A charm that heals on a kill is paid here
    /// too, and so is the experience, since the enemies do not know the hero. Port of
    /// scripts/loot.gd.
    /// </summary>
    public static class Loot
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;

        /// <summary>How many drops the last call laid down; for the tests.</summary>
        public static int LastDropCount { get; private set; }

        /// <summary>Spawns the drops of <paramref name="table"/> at <paramref name="at"/> (the dying body's centre, in units).</summary>
        public static void Drop(Vector2 at, LootTable table)
        {
            LastDropCount = 0;
            if (table == null)
            {
                return;
            }
            var drops = table.Roll(() => Random.value, (low, high) => Random.Range(low, high + 1));
            LastDropCount = drops.Count;
            for (var i = 0; i < drops.Count; i++)
            {
                var where = at + new Vector2(LootTable.OffsetOf(i, drops.Count) / Ppu, 0f);
                var drop = drops[i];
                if (drop.IsItem)
                {
                    LootDrop.Spawn(drop.ItemId, where);
                }
                else
                {
                    LightDrop.Spawn(drop.Currency, drop.Amount, where);
                }
            }
            var hero = PlayerController.Current;
            if (hero == null || !hero.IsAlive)
            {
                return;
            }
            var heal = (int)Inventory.Bonus("heal_on_kill");
            if (heal > 0 && hero.RestoreHealth(heal))
            {
                Feedback.Spark((Vector2)hero.transform.position + new Vector2(0f, 20f / Ppu), HealMote.Glow);
            }
            hero.GainXp(table.Xp);
        }
    }
}
