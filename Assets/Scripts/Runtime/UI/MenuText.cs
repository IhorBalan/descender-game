using System.Collections.Generic;
using System.Text;
using Descender.Economy;
using Descender.Story;

namespace Descender.UI
{
    /// <summary>
    /// The lines the in-game menus and the HUD write, as pure text so they can be
    /// tested without a scene: the purse, a pack row, a ware row, a skill row and
    /// the camp's status line. Ports of the static helpers in the Godot menus.
    /// </summary>
    public static class MenuText
    {
        public const string WornMark = "* ";
        public const string RankMarks = "IIIII";

        /// <summary>Story flags that are church iron carried for Pell start with this.</summary>
        public const string IronFlagPrefix = "upgrade:iron:";

        /// <summary>Story flags that mean the sword was forged once more start with this.</summary>
        public const string SwordFlagPrefix = "sword:";

        /// <summary>The purse as one line: "12 light   3 tally bones".</summary>
        public static string PurseText()
        {
            var parts = new List<string>();
            foreach (var currency in Inventory.Currencies)
            {
                parts.Add(
                    Inventory.PriceText(new Dictionary<string, int> { [currency.Id] = Inventory.Amount(currency.Id) })
                );
            }
            return string.Join("   ", parts);
        }

        /// <summary>What is worn in each slot: "Sword: Hinge Blade   Armour: -   Charm: -".</summary>
        public static string WornText()
        {
            var parts = new List<string>();
            foreach (var slot in new[] { ItemSlot.Weapon, ItemSlot.Armour, ItemSlot.Charm })
            {
                var id = Inventory.EquippedId(slot);
                parts.Add($"{Items.SlotName(slot)}: {(id.Length > 0 ? Items.DisplayName(id) : "-")}");
            }
            return string.Join("   ", parts);
        }

        /// <summary>One pack row: the worn mark, the name, the count and what the piece does.</summary>
        public static string PackLine(string id)
        {
            var count = Inventory.Count(id);
            var text = (Inventory.IsEquipped(id) ? WornMark : "  ") + Items.DisplayName(id);
            if (count > 1)
            {
                text += $" x{count}";
            }
            var what = Items.Summary(id);
            if (what.Length > 0)
            {
                text = $"{Pad(text, 26)}   {what}";
            }
            return text;
        }

        /// <summary>The things the story has handed the hero, by their flags, and the church iron he carries for Pell.</summary>
        public static List<string> KeptThings()
        {
            var result = new List<string>();
            foreach (var (flag, name) in Items.KeptFlags)
            {
                if (StoryFlags.Has(flag))
                {
                    result.Add(name);
                }
            }
            var iron = IronCarried();
            if (iron > 0)
            {
                result.Add($"Church iron x{iron}");
            }
            return result;
        }

        /// <summary>Church iron found and not yet forged into the sword (Upgrade.iron_carried).</summary>
        public static int IronCarried()
        {
            var found = CountFlags(IronFlagPrefix);
            var forged = CountFlags(SwordFlagPrefix);
            return System.Math.Max(found - System.Math.Max(forged - 1, 0), 0);
        }

        public static int CountFlags(string prefix)
        {
            var count = 0;
            foreach (var flag in StoryFlags.ToList())
            {
                if (flag.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>One ware row: the name and its price, or "sold".</summary>
        public static string ShopLine(string shopId, Ware ware)
        {
            var price = Shops.SoldOut(shopId, ware.Item) ? "sold" : Inventory.PriceText(Shops.PriceOf(ware));
            return $"{Pad(Items.DisplayName(ware.Item), 22)}   {price}";
        }

        /// <summary>The note under a focused ware: its description and what it does.</summary>
        public static string WareNote(string itemId)
        {
            var text = Items.Description(itemId);
            var what = Items.Summary(itemId);
            return what.Length > 0 ? $"{text}  ({what})" : text;
        }

        /// <summary>One skill row: the name, its rank marks, and the level it needs or what a rank does.</summary>
        public static string SkillLine(string id)
        {
            var rank = Skills.Rank(id);
            var marks =
                RankMarks.Substring(0, System.Math.Min(rank, RankMarks.Length))
                + new string('-', System.Math.Max(Skills.MaxRank(id) - rank, 0));
            var text = $"{Pad(Skills.DisplayName(id), 14)} {Pad(marks, 4)}";
            if (Skills.Level() < Skills.LevelNeeded(id))
            {
                text += $"   level {Skills.LevelNeeded(id)}";
            }
            else
            {
                text += $"   {Skills.Summary(id)}";
            }
            return text;
        }

        /// <summary>The colour a skill row takes: open to a point, locked, or plain.</summary>
        public static UnityEngine.Color SkillColor(string id)
        {
            if (Skills.BlockedReason(id).Length == 0)
            {
                return UiTheme.Highlight;
            }
            if (Skills.Level() < Skills.LevelNeeded(id) || Skills.Rank(id) >= Skills.MaxRank(id))
            {
                return UiTheme.Locked;
            }
            return UiTheme.Item;
        }

        /// <summary>"Level 4   130 / 160 experience", or at the cap that it is the last.</summary>
        public static string SkillStatusText()
        {
            var (into, needed) = Skills.LevelProgress();
            if (needed <= 0)
            {
                return $"Level {Skills.Level()}   the last";
            }
            return $"Level {Skills.Level()}   {into} / {needed} experience";
        }

        /// <summary>How many points wait to be spent.</summary>
        public static string SkillPointsText()
        {
            var points = Skills.PointsLeft();
            if (points <= 0)
            {
                return "No skill point to spend: the next level gives one";
            }
            return $"{points} skill point{(points == 1 ? "" : "s")} to spend";
        }

        /// <summary>The camp's line under its heading: which checkpoint of how many, the level and the points waiting.</summary>
        public static string CampStatusText(int index, int count)
        {
            var parts = new List<string>();
            if (index >= 0 && count > 0)
            {
                parts.Add($"Checkpoint {index + 1} of {count}");
            }
            parts.Add($"Level {Skills.Level()}");
            var points = Skills.PointsLeft();
            if (points > 0)
            {
                parts.Add($"{points} skill point{(points == 1 ? "" : "s")} to spend");
            }
            return string.Join("      ", parts);
        }

        /// <summary>The HUD's level line: "Lv 3", or "Lv 3   1 skill point" while one waits.</summary>
        public static string HudLevelText()
        {
            var points = Skills.PointsLeft();
            var text = $"Lv {Skills.Level()}";
            if (points > 0)
            {
                text += $"   {points} skill point{(points == 1 ? "" : "s")}";
            }
            return text;
        }

        /// <summary>Pads a line with spaces to a width, as GDScript's rpad.</summary>
        public static string Pad(string text, int width)
        {
            if (text.Length >= width)
            {
                return text;
            }
            var builder = new StringBuilder(text, width);
            builder.Append(' ', width - text.Length);
            return builder.ToString();
        }
    }
}
