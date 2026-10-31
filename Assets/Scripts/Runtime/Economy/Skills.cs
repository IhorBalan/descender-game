using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Descender.Economy
{
    public sealed class SkillDef
    {
        public string Id;
        public string Name;
        public string Text;
        public int Ranks;
        public int Level;
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
    }

    /// <summary>
    /// The hero's experience, his level and the skills he has put it into. Every kill
    /// gives experience (`xp` in the enemy's loot table); enough of it is a level, and
    /// every level after the first is a skill point to spend at a checkpoint camp.
    /// A skill's stats use the item vocabulary: additive stats are added once per rank,
    /// multiplying stats multiplied once per rank. See docs/design/progression.md.
    /// </summary>
    public static class Skills
    {
        /// <summary>The level the experience curve stops at.</summary>
        public const int MaxLevel = 20;
        public const int XpBase = 25;
        public const int XpPerLevel = 10;

        public static readonly SkillDef[] Ordered =
        {
            S("vigour", "Vigour", "More of him holds together. One more heart for every rank.", 3, 1, ("hearts", 1f)),
            S("edge", "Edge", "He learns where the sword goes in. One more damage for every rank.", 3, 1, ("damage", 1f)),
            S("swift_feet", "Swift Feet", "The hurry the Making put in him, kept. He runs a little faster.", 3, 1, ("speed", 1.08f)),
            S("deep_draught", "Deep Draught", "He learns to make the Basin's water last. One more healing charge.", 2, 3, ("charges", 1f)),
            S("long_reach", "Long Reach", "The swing starts from the shoulder. The sword reaches further.", 2, 3, ("reach", 1.15f)),
            S("quick_dash", "Quick Dash", "The light in his legs gathers sooner. The dash comes back faster.", 2, 4, ("dash_cooldown", 0.8f)),
            S("lightkeeper", "Lightkeeper", "What leaks from a killed thing finds him. The light of a kill comes to him fuller.", 2, 5, ("light", 1.25f)),
            S("iron_skin", "Iron Skin", "He stops flinching before the blow lands. Every hit takes one heart less.", 2, 6, ("guard", 1f)),
            S("quick_hand", "Quick Hand", "The sword comes round again before they see it. The swing is faster.", 2, 8, ("attack_time", 0.88f)),
            S("mender", "Mender", "A healing charge closes more of the wound. One more heart for every heal.", 1, 10, ("heal", 1f)),
        };

        public static readonly Dictionary<string, SkillDef> Table = Index(Ordered);

        /// <summary>Experience gathered this save, never lost.</summary>
        public static int Xp { get; private set; }

        /// <summary>Skill id -> the rank bought, in the order they were first raised.</summary>
        private static readonly List<KeyValuePair<string, int>> Ranks = new List<KeyValuePair<string, int>>();

        /// <summary>A new game starts at the first level with nothing learned.</summary>
        public static void Reset()
        {
            Xp = 0;
            Ranks.Clear();
        }

        // --- Experience and levels ------------------------------------------------

        /// <summary>Experience needed to go from `level` to the next (0 at the cap).</summary>
        public static int XpToNext(int level) => level >= MaxLevel ? 0 : XpBase + XpPerLevel * (level - 1);

        /// <summary>Experience needed, from nothing, to reach `level`.</summary>
        public static int XpForLevel(int level)
        {
            var total = 0;
            for (var step = 1; step < Math.Min(level, MaxLevel); step++)
            {
                total += XpToNext(step);
            }
            return total;
        }

        /// <summary>The level `amount` experience makes.</summary>
        public static int LevelFor(int amount)
        {
            var level = 1;
            while (level < MaxLevel && amount >= XpForLevel(level + 1))
            {
                level++;
            }
            return level;
        }

        public static int Level() => LevelFor(Xp);

        /// <summary>Experience past the start of the current level, and how much the level takes; (0, 0) at the cap.</summary>
        public static (int into, int needed) LevelProgress()
        {
            var current = Level();
            if (current >= MaxLevel)
            {
                return (0, 0);
            }
            return (Xp - XpForLevel(current), XpToNext(current));
        }

        /// <summary>Adds experience. Returns how many levels it made, so the hero can say.</summary>
        public static int AddXp(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }
            var before = Level();
            Xp += amount;
            return Level() - before;
        }

        // --- Skill points ---------------------------------------------------------

        /// <summary>Every level after the first is a point.</summary>
        public static int PointsEarned() => Level() - 1;

        public static int PointsSpent()
        {
            var total = 0;
            foreach (var pair in Ranks)
            {
                total += pair.Value;
            }
            return total;
        }

        public static int PointsLeft() => PointsEarned() - PointsSpent();

        // --- Skills ---------------------------------------------------------------

        public static bool Has(string id) => !string.IsNullOrEmpty(id) && Table.ContainsKey(id);

        public static SkillDef Get(string id) => Has(id) ? Table[id] : null;

        public static string DisplayName(string id) => Get(id)?.Name ?? id;

        public static string Description(string id) => Get(id)?.Text ?? "";

        public static int MaxRank(string id) => Get(id)?.Ranks ?? 0;

        /// <summary>The level a skill asks for before its first point.</summary>
        public static int LevelNeeded(string id) => Get(id)?.Level ?? 1;

        public static int Rank(string id)
        {
            foreach (var pair in Ranks)
            {
                if (pair.Key == id)
                {
                    return pair.Value;
                }
            }
            return 0;
        }

        /// <summary>The ids in table order, for the menu.</summary>
        public static List<string> Ids()
        {
            var result = new List<string>();
            foreach (var def in Ordered)
            {
                result.Add(def.Id);
            }
            return result;
        }

        /// <summary>Why a point cannot go into a skill right now, or "" when it can.</summary>
        public static string BlockedReason(string id)
        {
            if (!Has(id))
            {
                return "No such skill";
            }
            if (Rank(id) >= MaxRank(id))
            {
                return $"{DisplayName(id)} is learned to the end";
            }
            if (Level() < LevelNeeded(id))
            {
                return $"{DisplayName(id)} needs level {LevelNeeded(id)}";
            }
            if (PointsLeft() <= 0)
            {
                return "No skill point to spend: the next level gives one";
            }
            return "";
        }

        /// <summary>Spends a point on the next rank of a skill. Returns "" when it was spent, else why it was not.</summary>
        public static string Raise(string id)
        {
            var reason = BlockedReason(id);
            if (reason.Length > 0)
            {
                return reason;
            }
            SetRank(id, Rank(id) + 1);
            return "";
        }

        /// <summary>The skills' additive stat, summed over every rank (0 when none give it).</summary>
        public static float Bonus(string stat)
        {
            var total = 0f;
            foreach (var pair in Ranks)
            {
                var def = Get(pair.Key);
                if (def != null && def.Stats.TryGetValue(stat, out var value))
                {
                    total += value * pair.Value;
                }
            }
            return total;
        }

        /// <summary>The skills' multiplying stat, multiplied once per rank (1 when none change it).</summary>
        public static float Factor(string stat)
        {
            var total = 1f;
            foreach (var pair in Ranks)
            {
                var def = Get(pair.Key);
                if (def != null && def.Stats.TryGetValue(stat, out var value))
                {
                    total *= (float)Math.Pow(value, pair.Value);
                }
            }
            return total;
        }

        /// <summary>One short line of what a rank does, in the item vocabulary: "+1 heart per rank".</summary>
        public static string Summary(string id)
        {
            var parts = new List<string>();
            var def = Get(id);
            if (def != null)
            {
                foreach (var pair in def.Stats)
                {
                    var value = pair.Value;
                    switch (pair.Key)
                    {
                        case "hearts": parts.Add($"{Items.Signed(value)} heart"); break;
                        case "charges": parts.Add($"{Items.Signed(value)} healing charge"); break;
                        case "damage": parts.Add($"{Items.Signed(value)} damage"); break;
                        case "guard": parts.Add($"{Items.Signed(value)} guard"); break;
                        case "heal": parts.Add($"{Items.Signed(value)} heart per heal"); break;
                        case "speed": parts.Add($"feet x{F2(value)}"); break;
                        case "reach": parts.Add($"reach x{F2(value)}"); break;
                        case "dash_cooldown": parts.Add($"dash back x{F2(1f / value)} sooner"); break;
                        case "attack_time": parts.Add($"swing x{F2(1f / value)} faster"); break;
                        case "light": parts.Add($"light x{F2(value)}"); break;
                        default: parts.Add($"{pair.Key} {value.ToString(CultureInfo.InvariantCulture)}"); break;
                    }
                }
            }
            return string.Join(", ", parts) + " per rank";
        }

        private static string F2(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        // --- Save -----------------------------------------------------------------

        public static JObject ToJson()
        {
            var ranks = new JObject();
            foreach (var pair in Ranks)
            {
                ranks[pair.Key] = pair.Value;
            }
            return new JObject { ["xp"] = Xp, ["ranks"] = ranks };
        }

        /// <summary>
        /// Replaces the progress with the one from a save; a skill the table no longer
        /// knows is dropped, a rank past the table's is cut to it, and points spent past
        /// the ones earned are given back, last raised first.
        /// </summary>
        public static void FromJson(JObject data)
        {
            Reset();
            if (data == null)
            {
                return;
            }
            Xp = Math.Max(data.Value<int?>("xp") ?? 0, 0);
            if (data["ranks"] is JObject saved)
            {
                foreach (var property in saved.Properties())
                {
                    var id = property.Name;
                    var wanted = property.Value.Type == JTokenType.Integer ? property.Value.Value<int>() : 0;
                    var value = Math.Min(wanted, MaxRank(id));
                    if (Has(id) && value > 0)
                    {
                        SetRank(id, value);
                    }
                }
            }
            while (PointsLeft() < 0 && Ranks.Count > 0)
            {
                var last = Ranks[Ranks.Count - 1];
                SetRank(last.Key, last.Value - 1);
            }
        }

        private static void SetRank(string id, int rank)
        {
            for (var i = 0; i < Ranks.Count; i++)
            {
                if (Ranks[i].Key != id)
                {
                    continue;
                }
                if (rank <= 0)
                {
                    Ranks.RemoveAt(i);
                }
                else
                {
                    Ranks[i] = new KeyValuePair<string, int>(id, rank);
                }
                return;
            }
            if (rank > 0)
            {
                Ranks.Add(new KeyValuePair<string, int>(id, rank));
            }
        }

        private static SkillDef S(string id, string name, string text, int ranks, int level, params (string stat, float value)[] stats)
        {
            var def = new SkillDef { Id = id, Name = name, Text = text, Ranks = ranks, Level = level };
            foreach (var (stat, value) in stats)
            {
                def.Stats[stat] = value;
            }
            return def;
        }

        private static Dictionary<string, SkillDef> Index(SkillDef[] defs)
        {
            var table = new Dictionary<string, SkillDef>();
            foreach (var def in defs)
            {
                table[def.Id] = def;
            }
            return table;
        }
    }
}
