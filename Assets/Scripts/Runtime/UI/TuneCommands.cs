using System;
using System.Collections.Generic;
using Descender.Player;

namespace Descender.UI
{
    /// <summary>
    /// What the dev console's `tune` and `ability` say and do, apart from the panel so
    /// they can be tested. `tune` works on <see cref="LiveTuning"/>'s working copy of
    /// the hero's numbers: `tune` sums up, `tune list` names every field, `tune jump`
    /// shows the fields whose name or group matches, `tune jumpHeight 90` sets one,
    /// `tune reset [field]` goes back to the asset and `tune save` writes the asset (in
    /// the editor). `ability wings on|off|reset` forces an ability for the session.
    /// </summary>
    public static class TuneCommands
    {
        public const string NoHeroText = "No hero here: start a game or open the `lab` first.";
        public const string TuneUsage =
            "Usage: tune [list | reset [field] | save | panel [on|off|group] | field [value]]";
        public const string AbilityUsage = "Usage: ability [name|all] [on|off|reset]";

        /// <summary>`tune`, for the hero in the scene: he plays with the working copy from here on.</summary>
        public static string Tune(string[] args)
        {
            var hero = PlayerController.Current;
            if (hero == null)
            {
                return NoHeroText;
            }
            var live = LiveTuning.Begin(hero.Tuning);
            hero.UseTuning(live);
            var hearts = live.maxHealth;
            var charges = live.maxHealCharges;
            var answer = Run(args);
            if (live.maxHealth != hearts || live.maxHealCharges != charges)
            {
                // The maximums are worked out once, not read every step.
                hero.RestoreUpgrades();
            }
            return answer;
        }

        /// <summary>`tune` on the working copy <see cref="LiveTuning.Begin"/> made.</summary>
        public static string Run(string[] args)
        {
            if (LiveTuning.Working == null)
            {
                return NoHeroText;
            }
            if (args.Length == 0)
            {
                return Summary();
            }
            switch (args[0].ToLowerInvariant())
            {
                case "list":
                    return ListAll();
                case "reset":
                    return Reset(args);
                case "save":
                    return args.Length == 1 ? LiveTuning.WriteToAsset() : TuneUsage;
            }
            var field = LiveTuning.Find(args[0]);
            if (field == null)
            {
                return args.Length == 1 ? Search(args[0]) : NoField(args[0]);
            }
            if (args.Length == 1)
            {
                return Show(field);
            }
            var words = new string[args.Length - 1];
            Array.Copy(args, 1, words, 0, words.Length);
            return Set(field, words);
        }

        private static string NoField(string name) => $"No tuning field '{name}'. `tune list` names them all.";

        private static string Summary()
        {
            var rows = new List<string>();
            var changed = LiveTuning.Changed();
            if (changed.Count == 0)
            {
                rows.Add(LiveTuning.NothingTunedText);
            }
            else
            {
                rows.Add("Tuned: " + Join(changed));
            }
            var groups = new List<string>();
            foreach (var group in LiveTuning.Groups)
            {
                groups.Add($"{group} [{LiveTuning.InGroup(group).Count}]");
            }
            rows.Add("Groups: " + string.Join(", ", groups));
            rows.Add("`tune jump` shows the fields whose name or group matches; `tune jumpHeight 90` sets one.");
            return string.Join("\n", rows);
        }

        private static string ListAll()
        {
            var rows = new List<string>();
            foreach (var group in LiveTuning.Groups)
            {
                var names = new List<string>();
                foreach (var field in LiveTuning.InGroup(group))
                {
                    names.Add(field.Name);
                }
                rows.Add($"{group}: " + string.Join(", ", names));
            }
            return string.Join("\n", rows);
        }

        private static string Search(string text)
        {
            var found = LiveTuning.Matching(text);
            if (found.Count == 0)
            {
                return $"No tuning field or group matches '{text}'. `tune list` names them all.";
            }
            return Join(found);
        }

        private static string Join(List<LiveTuning.Field> fields)
        {
            var parts = new List<string>();
            foreach (var field in fields)
            {
                parts.Add(Value(field));
            }
            return string.Join(", ", parts);
        }

        /// <summary>"name value", with the asset's value after one that was changed.</summary>
        public static string Value(LiveTuning.Field field)
        {
            var text = $"{field.Name} {LiveTuning.Read(LiveTuning.Working, field)}";
            if (LiveTuning.IsChanged(field))
            {
                text += $" (asset {LiveTuning.Read(LiveTuning.Source, field)})";
            }
            return text;
        }

        private static string Show(LiveTuning.Field field)
        {
            var text = $"{Value(field)}   [{field.Group}]";
            if (field.Help.Length > 0)
            {
                text += "\n  " + field.Help;
            }
            return text;
        }

        private static string Set(LiveTuning.Field field, string[] words)
        {
            if (!LiveTuning.TryParse(field, words, out var value))
            {
                return $"Usage: tune {field.Name} {LiveTuning.Hint(field)}";
            }
            var before = LiveTuning.Read(LiveTuning.Working, field);
            LiveTuning.Set(field, value);
            return $"{Value(field)}, was {before}";
        }

        private static string Reset(string[] args)
        {
            if (args.Length == 1)
            {
                var count = LiveTuning.Restore();
                return count == 0 ? LiveTuning.NothingTunedText : $"Put {count} back to the asset's.";
            }
            if (args.Length > 2)
            {
                return TuneUsage;
            }
            var field = LiveTuning.Find(args[1]);
            if (field == null)
            {
                return NoField(args[1]);
            }
            LiveTuning.Restore(field);
            return Value(field);
        }

        // --- ability -----------------------------------------------------------------

        /// <summary>`ability`: lists them, or forces one (or all) on or off, or lets it go again.</summary>
        public static string Ability(string[] args)
        {
            if (args.Length == 0)
            {
                return AbilityList();
            }
            if (args.Length > 2)
            {
                return AbilityUsage;
            }
            var name = args[0].ToLowerInvariant();
            var what = args.Length == 2 ? args[1].ToLowerInvariant() : "";
            if (name == "reset" && args.Length == 1)
            {
                LiveTuning.ReleaseAll();
                return AbilityList();
            }
            var all = name == "all";
            if (!all && !TryAbility(name, out _))
            {
                return $"No ability '{args[0]}'. Abilities: " + string.Join(", ", AbilityNames());
            }
            if (args.Length == 1)
            {
                return all ? AbilityList() : AbilityRow(Parse(name));
            }
            var release = what == "reset";
            if (!release && !LiveTuning.TryOnOff(what, out _))
            {
                return AbilityUsage;
            }
            LiveTuning.TryOnOff(what, out var on);
            foreach (Save.Ability ability in Enum.GetValues(typeof(Save.Ability)))
            {
                if (!all && ability != Parse(name))
                {
                    continue;
                }
                if (release)
                {
                    LiveTuning.Release(ability);
                }
                else
                {
                    LiveTuning.Force(ability, on);
                }
            }
            return all ? AbilityList() : AbilityRow(Parse(name));
        }

        private static List<string> AbilityNames()
        {
            var names = new List<string>();
            foreach (Save.Ability ability in Enum.GetValues(typeof(Save.Ability)))
            {
                names.Add(ability.ToString().ToLowerInvariant());
            }
            return names;
        }

        private static bool TryAbility(string name, out Save.Ability ability)
        {
            foreach (Save.Ability candidate in Enum.GetValues(typeof(Save.Ability)))
            {
                if (candidate.ToString().ToLowerInvariant() == name)
                {
                    ability = candidate;
                    return true;
                }
            }
            ability = default;
            return false;
        }

        private static Save.Ability Parse(string name)
        {
            TryAbility(name, out var ability);
            return ability;
        }

        private static string AbilityList()
        {
            var rows = new List<string>();
            foreach (Save.Ability ability in Enum.GetValues(typeof(Save.Ability)))
            {
                rows.Add(AbilityRow(ability));
            }
            return string.Join(", ", rows);
        }

        /// <summary>"wings on", with "(forced, he owns it)" or "(forced, not his)" after a forced one.</summary>
        private static string AbilityRow(Save.Ability ability)
        {
            var hero = PlayerController.Current;
            var owned = hero != null && hero.Owns(ability);
            var name = ability.ToString().ToLowerInvariant();
            if (LiveTuning.IsForced(ability, out var on))
            {
                return $"{name} {(on ? "on" : "off")} (forced, {(owned ? "he owns it" : "not his")})";
            }
            return $"{name} {(owned ? "on" : "off")}";
        }
    }
}
