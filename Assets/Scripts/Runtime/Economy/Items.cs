using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Descender.Economy
{
    public enum ItemSlot
    {
        None = -1,
        Weapon = 0,
        Armour = 1,
        Charm = 2,
    }

    public enum ItemKind
    {
        Equipment,
        Consumable,
    }

    /// <summary>One entry of the item table.</summary>
    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public ItemKind Kind;
        public ItemSlot Slot = ItemSlot.None;
        public bool Unique;
        public string Text;
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        public Color Color = Color.white;
    }

    /// <summary>
    /// Every item the hero can carry, by id: the equipment (a weapon, an armour and a
    /// charm may be worn at once), the consumables (used from the pack) and the unique
    /// loot the bosses leave. Each has a name, a line of description in the voice of
    /// the relics, a colour for its pickup, and its stats: what it does to the hero's
    /// numbers while it is worn (see <see cref="Inventory.Bonus"/> and <see cref="Inventory.Factor"/>).
    /// See docs/design/economy.md.
    ///
    /// Additive stats (summed over the worn pieces): damage, guard, hearts, charges,
    /// heal, mote_chance, halo_at_rest, heal_on_kill. Multiplying stats (multiplied
    /// over the worn pieces): reach, attack_time, speed, dash_cooldown, light, prices.
    /// </summary>
    public static class Items
    {
        public static readonly string[] AdditiveStats =
        {
            "damage", "guard", "hearts", "charges", "heal", "mote_chance", "halo_at_rest", "heal_on_kill",
        };

        public static readonly string[] FactorStats =
        {
            "reach", "attack_time", "speed", "dash_cooldown", "light", "prices",
        };

        public static string SlotName(ItemSlot slot)
        {
            switch (slot)
            {
                case ItemSlot.Weapon: return "Sword";
                case ItemSlot.Armour: return "Armour";
                case ItemSlot.Charm: return "Charm";
                default: return "";
            }
        }

        /// <summary>The table in its design order; <see cref="Table"/> indexes it by id.</summary>
        public static readonly ItemDef[] Ordered =
        {
            // --- Old Pell's forge ------------------------------------------------
            Equip("iron_sword", "Church Iron Sword", ItemSlot.Weapon,
                "A blade of hinge iron off a church door, hammered flat. Heavier than his, and it goes in further.",
                new Color(0.75f, 0.78f, 0.85f), ("damage", 1f)),
            Equip("hinge_blade", "Hinge Blade", ItemSlot.Weapon,
                "Long and thin, a strap hinge ground to an edge. It reaches further than a sword should.",
                new Color(0.7f, 0.72f, 0.8f), ("reach", 1.35f)),
            Equip("quilted_coat", "Quilted Coat", ItemSlot.Armour,
                "Pell's coat, the layers sewn with church iron wire. A blow lands a little softer through it.",
                new Color(0.6f, 0.45f, 0.3f), ("guard", 1f), ("speed", 0.95f)),
            Equip("scrap_mail", "Scrap Mail", ItemSlot.Armour,
                "Hinge plates riveted to a coat. It holds a little more of him in and slows his feet.",
                new Color(0.55f, 0.55f, 0.62f), ("guard", 1f), ("hearts", 1f), ("speed", 0.85f)),
            // --- Vael the Bargainer ----------------------------------------------
            Equip("imp_tooth", "Imp's Tooth", ItemSlot.Charm,
                "A tooth on a cord. The light of a killed thing leaks towards it, as if it still wanted to bite.",
                new Color(0.9f, 0.85f, 0.75f), ("light", 1.5f)),
            Equip("cold_coin", "Cold Coin", ItemSlot.Charm,
                "A coin from a dead man's mouth, Vael says. The dying give up more of their light around it.",
                new Color(0.6f, 0.75f, 0.85f), ("mote_chance", 0.3f)),
            Equip("basin_shard", "Basin Shard", ItemSlot.Charm,
                "Warden's glass from the Basin's rim. Its water closes more of a wound through it.",
                new Color(0.8f, 0.9f, 1.0f), ("heal", 1f)),
            // --- Consumables -----------------------------------------------------
            Consumable("flask", "Basin Flask",
                "Basin water in a soldier's flask. Drinking it puts a healing charge back.",
                new Color(1.0f, 0.85f, 0.45f)),
            Consumable("halo_shard", "Halo Shard",
                "A piece of a cracked halo. Held up, it turns the next hit aside.",
                new Color(1.0f, 0.85f, 0.4f)),
            Consumable("blade_oil", "Choir Oil",
                "Oil that burns with the Choir's fire. The sword hits harder for a while.",
                new Color(1.0f, 0.7f, 0.3f)),
            Consumable("zeal_draught", "Draught of Zeal",
                "The hurry the Making put in him, bottled. Faster dashes and feet for a while.",
                new Color(0.6f, 0.9f, 1.0f)),
            // --- Unique loot, one per boss ---------------------------------------
            Unique("wardens_halo", "Warden's Halo", ItemSlot.Charm,
                "The Warden's halo, cracked through. It still remembers how to protect: a Halo at every rest.",
                new Color(1.0f, 0.9f, 0.5f), ("halo_at_rest", 1f)),
            Unique("penitents_shroud", "Penitent's Shroud", ItemSlot.Armour,
                "The cloth an angel fell in. It weighs nothing, and a blow through it lands softer.",
                new Color(0.8f, 0.8f, 0.9f), ("guard", 1f)),
            Unique("lords_tally", "Lord's Tally", ItemSlot.Charm,
                "The Limbo Lord's tally bone, notched to the end. The light of a kill comes to it twice over.",
                new Color(0.95f, 0.9f, 0.8f), ("light", 2.0f)),
            Unique("velvet_gloves", "Velvet Gloves", ItemSlot.Charm,
                "The Keeper's gloves. Whatever they touch lets go sooner: the dash comes back in half the time.",
                new Color(0.85f, 0.4f, 0.7f), ("dash_cooldown", 0.5f)),
            Unique("gluttons_maw", "Glutton's Maw", ItemSlot.Weapon,
                "A jaw of the Bloated One, the teeth still in it. Slow to swing, and nothing it bites stays whole.",
                new Color(0.55f, 0.75f, 0.35f), ("damage", 2f), ("attack_time", 1.35f)),
            Unique("vault_seal", "Seal of the Vault", ItemSlot.Charm,
                "The Warden of the Vault's seal. Every trader below knows it, and asks less of whoever carries it.",
                new Color(1.0f, 0.8f, 0.3f), ("prices", 0.6f)),
            Unique("furys_edge", "Fury's Edge", ItemSlot.Weapon,
                "Gorrow's blade, too long for a man. It reaches further and bites deeper than it should.",
                new Color(1.0f, 0.45f, 0.3f), ("damage", 1f), ("reach", 1.3f)),
            Unique("bishops_mitre", "Bishop's Mitre", ItemSlot.Armour,
                "The Cursed Bishop's hat, scorched. A blow lands softer under it and the Basin's water goes further.",
                new Color(0.9f, 0.6f, 0.3f), ("guard", 1f), ("heal", 1f)),
            Unique("champions_plate", "Champion's Plate", ItemSlot.Armour,
                "The Champion's breastplate, dented by the arena. Two hearts of every blow stop at it; so do his feet.",
                new Color(0.85f, 0.3f, 0.3f), ("guard", 2f), ("speed", 0.8f)),
            Unique("false_crown", "False Crown", ItemSlot.Charm,
                "The False King's crown, which fooled an army. It holds two more hearts in whoever wears it.",
                new Color(0.7f, 0.5f, 0.9f), ("hearts", 2f)),
            Unique("brothers_knife", "Brother's Knife", ItemSlot.Weapon,
                "A kitchen knife from Earth, the handle worn by a hand like his. Quick, and it goes deep.",
                new Color(0.6f, 0.8f, 1.0f), ("damage", 2f), ("attack_time", 0.75f)),
            Unique("gardeners_seed", "Gardener's Seed", ItemSlot.Charm,
                "The first light, kept as a seed. Every life the sword takes gives a little of it back.",
                new Color(1.0f, 1.0f, 0.85f), ("heal_on_kill", 1f)),
        };

        public static readonly Dictionary<string, ItemDef> Table = Index(Ordered);

        /// <summary>
        /// Story flags that are things the hero carries, shown in the pack under
        /// "kept things" (the dialogue sets them; see the dialogue file).
        /// </summary>
        public static readonly (string flag, string name)[] KeptFlags =
        {
            ("item:letter", "Dace's letter"),
            ("item:warrant", "The warrant"),
            ("item:hook", "The hook"),
            ("item:token", "The token"),
            ("item:scarf", "Thorne's red scarf"),
            ("item:wheel_tablet", "The Choir's tablet"),
            ("clock_hand", "The clock's hand"),
            ("item:clock_weight", "The clock weight"),
            ("item:clock_chain", "The clock chain"),
            ("item:clock_key", "The clock key"),
        };

        public static bool Has(string id) => !string.IsNullOrEmpty(id) && Table.ContainsKey(id);

        /// <summary>The item's table entry, or null for an unknown id.</summary>
        public static ItemDef Get(string id) => Has(id) ? Table[id] : null;

        public static string DisplayName(string id) => Get(id)?.Name ?? id;

        public static string Description(string id) => Get(id)?.Text ?? "";

        public static Color ColorOf(string id) => Get(id)?.Color ?? Color.white;

        public static bool IsEquipment(string id) => Get(id)?.Kind == ItemKind.Equipment;

        public static bool IsConsumable(string id) => Get(id)?.Kind == ItemKind.Consumable;

        public static bool IsUnique(string id) => Get(id)?.Unique == true;

        /// <summary>The slot a piece of equipment goes in, or None for anything else.</summary>
        public static ItemSlot SlotOf(string id) => Get(id)?.Slot ?? ItemSlot.None;

        public static IReadOnlyDictionary<string, float> Stats(string id) =>
            Get(id)?.Stats ?? new Dictionary<string, float>();

        public static float Stat(string id, string stat, float fallback)
        {
            var def = Get(id);
            return def != null && def.Stats.TryGetValue(stat, out var value) ? value : fallback;
        }

        /// <summary>The story flag set when a boss's unique loot has been picked up.</summary>
        public static string LootFlag(string id) => $"loot:{id}";

        /// <summary>One short line of what a piece does, for the pack and the traders: "+1 damage, reach x1.3".</summary>
        public static string Summary(string id)
        {
            var parts = new List<string>();
            foreach (var pair in Stats(id))
            {
                var value = pair.Value;
                switch (pair.Key)
                {
                    case "damage": parts.Add($"{Signed(value)} damage"); break;
                    case "guard": parts.Add($"{Signed(value)} guard"); break;
                    case "hearts": parts.Add($"{Signed(value)} hearts"); break;
                    case "charges": parts.Add($"{Signed(value)} healing charges"); break;
                    case "heal": parts.Add($"{Signed(value)} heart per heal"); break;
                    case "mote_chance": parts.Add("more heal motes"); break;
                    case "halo_at_rest": parts.Add("a Halo at every rest"); break;
                    case "heal_on_kill": parts.Add($"{Signed(value)} heart per kill"); break;
                    case "reach": parts.Add($"reach x{Short(value)}"); break;
                    case "attack_time": parts.Add($"swing {(value > 1f ? "slower" : "faster")}"); break;
                    case "speed": parts.Add($"{(value < 1f ? "slower" : "faster")} feet"); break;
                    case "dash_cooldown": parts.Add($"dash back x{Short(1f / value)} sooner"); break;
                    case "light": parts.Add($"light x{Short(value)}"); break;
                    case "prices": parts.Add($"prices x{Short(value)}"); break;
                }
            }
            return string.Join(", ", parts);
        }

        /// <summary>"+1" / "-2", as GDScript's %+d.</summary>
        public static string Signed(float value)
        {
            var whole = (int)value;
            return whole >= 0 ? $"+{whole}" : whole.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Two significant digits, as GDScript's %.2g: 1.35 -> "1.3", 2 -> "2", 0.6 -> "0.6".</summary>
        public static string Short(float value)
        {
            return ((double)value).ToString("G2", CultureInfo.InvariantCulture);
        }

        private static ItemDef Equip(string id, string name, ItemSlot slot, string text, Color color,
            params (string stat, float value)[] stats)
        {
            return Make(id, name, ItemKind.Equipment, slot, false, text, color, stats);
        }

        private static ItemDef Unique(string id, string name, ItemSlot slot, string text, Color color,
            params (string stat, float value)[] stats)
        {
            return Make(id, name, ItemKind.Equipment, slot, true, text, color, stats);
        }

        private static ItemDef Consumable(string id, string name, string text, Color color)
        {
            return Make(id, name, ItemKind.Consumable, ItemSlot.None, false, text, color);
        }

        private static ItemDef Make(string id, string name, ItemKind kind, ItemSlot slot, bool unique, string text,
            Color color, params (string stat, float value)[] stats)
        {
            var def = new ItemDef { Id = id, Name = name, Kind = kind, Slot = slot, Unique = unique, Text = text, Color = color };
            foreach (var (stat, value) in stats)
            {
                def.Stats[stat] = value;
            }
            return def;
        }

        private static Dictionary<string, ItemDef> Index(ItemDef[] defs)
        {
            var table = new Dictionary<string, ItemDef>();
            foreach (var def in defs)
            {
                table[def.Id] = def;
            }
            return table;
        }
    }
}
