using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Descender.Save
{
    /// <summary>The abilities, in the order the save numbers them (the Godot enum's order; saves store the index).</summary>
    public enum Ability
    {
        Wings = 0,
        LightDash = 1,
        WallGrip = 2,
        HolyLight = 3,
        Glide = 4,
        Smite = 5,
    }

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>
    /// Everything a save needs to put the hero back where he was, in the shape the
    /// Godot game wrote (version 1), so a Godot save file still loads:
    /// {version, level, checkpoint, abilities: [int], flags: [string], inventory,
    /// skills, difficulty, play_time, saved_at}.
    /// </summary>
    public sealed class SaveData
    {
        public const int Version = 1;

        public string Level = "world_1";
        public int Checkpoint = -1;
        public List<Ability> Abilities = new List<Ability>();
        public List<string> Flags = new List<string>();
        public JObject Inventory = new JObject();
        public JObject Skills = new JObject();
        public Difficulty Difficulty = Difficulty.Normal;
        public double PlayTime;
        public double SavedAt;

        public JObject ToJson()
        {
            var abilities = new JArray();
            foreach (var ability in Abilities)
            {
                abilities.Add((int)ability);
            }
            return new JObject
            {
                ["version"] = Version,
                ["level"] = Level,
                ["checkpoint"] = Checkpoint,
                ["abilities"] = abilities,
                ["flags"] = new JArray(Flags),
                ["inventory"] = Inventory,
                ["skills"] = Skills,
                ["difficulty"] = (int)Difficulty,
                ["play_time"] = PlayTime,
                ["saved_at"] = SavedAt,
            };
        }

        /// <summary>The save a JSON object holds, or null when it is not a version 1 save.</summary>
        public static SaveData FromJson(JObject data)
        {
            if (data == null || (data.Value<int?>("version") ?? 0) != Version)
            {
                return null;
            }
            var save = new SaveData
            {
                Level = data.Value<string>("level") ?? "",
                Checkpoint = data.Value<int?>("checkpoint") ?? -1,
                Inventory = data["inventory"] as JObject ?? new JObject(),
                Skills = data["skills"] as JObject ?? new JObject(),
                Difficulty = DifficultyOf(data),
                PlayTime = data.Value<double?>("play_time") ?? 0.0,
                SavedAt = data.Value<double?>("saved_at") ?? 0.0,
            };
            if (data["abilities"] is JArray abilities)
            {
                foreach (var token in abilities)
                {
                    if (token.Type == JTokenType.Integer)
                    {
                        var index = token.Value<int>();
                        if (index >= 0 && index <= (int)Ability.Smite)
                        {
                            save.Abilities.Add((Ability)index);
                        }
                    }
                }
            }
            if (data["flags"] is JArray flags)
            {
                foreach (var token in flags)
                {
                    var flag = token.ToString();
                    if (flag.Length > 0)
                    {
                        save.Flags.Add(flag);
                    }
                }
            }
            return save;
        }

        /// <summary>The difficulty a save was played at; older saves are Normal.</summary>
        public static Difficulty DifficultyOf(JObject data)
        {
            var value = data?.Value<int?>("difficulty") ?? (int)Difficulty.Normal;
            return (Difficulty)System.Math.Max(0, System.Math.Min(value, (int)Difficulty.Hard));
        }
    }
}
