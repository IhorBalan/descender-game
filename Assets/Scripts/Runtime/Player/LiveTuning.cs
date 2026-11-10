using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// The hero's numbers and abilities, changed while playing. The first change makes
    /// a working copy of the hero's <see cref="PlayerTuning"/> and hands him that, so
    /// the asset itself is only written by <see cref="WriteToAsset"/> (the console's
    /// `tune save`, in the editor). The fields are found by reflection, grouped by their
    /// headers, so a new tuning field needs nothing here. A forced ability answers
    /// <see cref="PlayerController.Has"/> without touching what he owns, so it never
    /// reaches a save. All of it lasts until the game is closed.
    /// </summary>
    public static class LiveTuning
    {
        /// <summary>One tunable number: a public float, int, bool or Vector2 field.</summary>
        public sealed class Field
        {
            public string Name;
            public string Group;
            public string Help;
            public FieldInfo Info;
        }

        public const string NoGroup = "Other";
        public const string CannotSaveText = "A build cannot write the tuning asset: `tune save` works in the editor.";
        public const string NothingTunedText = "Nothing differs from the asset.";
        public const string NotAnAssetText = "This hero's tuning is not an asset, so there is nothing to write to.";

        private static readonly int AbilityCount = Enum.GetValues(typeof(Save.Ability)).Length;

        private static List<Field> fields;
        private static List<string> groups;
        private static PlayerTuning source;
        private static PlayerTuning working;

        // 0 leaves an ability as he owns it, 1 forces it on, -1 forces it off.
        private static readonly int[] forced = new int[AbilityCount];

        /// <summary>The copy the hero plays with, or null before anything was tuned.</summary>
        public static PlayerTuning Working => working != null ? working : null;

        /// <summary>The tuning the working copy was made from: the asset.</summary>
        public static PlayerTuning Source => working != null ? source : null;

        /// <summary>The tunable fields, in the order the class declares them.</summary>
        public static IReadOnlyList<Field> Fields
        {
            get
            {
                if (fields == null)
                {
                    Gather();
                }
                return fields;
            }
        }

        /// <summary>The header of each group of fields, in order.</summary>
        public static IReadOnlyList<string> Groups
        {
            get
            {
                if (groups == null)
                {
                    Gather();
                }
                return groups;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnLoad()
        {
            source = null;
            working = null;
            Array.Clear(forced, 0, forced.Length);
        }

        private static void Gather()
        {
            fields = new List<Field>();
            groups = new List<string>();
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var found = typeof(PlayerTuning).GetFields(flags);
            Array.Sort(found, ByDeclaration);
            var group = NoGroup;
            foreach (var info in found)
            {
                var header = info.GetCustomAttribute<HeaderAttribute>();
                if (header != null)
                {
                    group = header.header;
                }
                if (!Supported(info.FieldType))
                {
                    continue;
                }
                var tooltip = info.GetCustomAttribute<TooltipAttribute>();
                var field = new Field();
                field.Name = info.Name;
                field.Group = group;
                field.Help = tooltip != null ? tooltip.tooltip : "";
                field.Info = info;
                fields.Add(field);
                if (!groups.Contains(group))
                {
                    groups.Add(group);
                }
            }
        }

        private static int ByDeclaration(FieldInfo a, FieldInfo b) => a.MetadataToken.CompareTo(b.MetadataToken);

        private static bool Supported(Type type) =>
            type == typeof(float) || type == typeof(int) || type == typeof(bool) || type == typeof(Vector2);

        /// <summary>The field of that name, whatever its capitals, or null.</summary>
        public static Field Find(string name)
        {
            foreach (var field in Fields)
            {
                if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            return null;
        }

        /// <summary>The fields whose name holds the text or whose group starts with it.</summary>
        public static List<Field> Matching(string text)
        {
            var result = new List<Field>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }
            foreach (var field in Fields)
            {
                var inName = field.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                var inGroup = field.Group.StartsWith(text, StringComparison.OrdinalIgnoreCase);
                if (inName || inGroup)
                {
                    result.Add(field);
                }
            }
            return result;
        }

        /// <summary>The fields under one header.</summary>
        public static List<Field> InGroup(string group)
        {
            var result = new List<Field>();
            foreach (var field in Fields)
            {
                if (field.Group == group)
                {
                    result.Add(field);
                }
            }
            return result;
        }

        // --- The working copy ------------------------------------------------------

        /// <summary>
        /// The working copy of a tuning, made on the first call. Asking with the copy
        /// itself or with the tuning it was made from gives the same copy back; another
        /// tuning starts a fresh one.
        /// </summary>
        public static PlayerTuning Begin(PlayerTuning tuning)
        {
            if (tuning == null)
            {
                return null;
            }
            if (working != null && (tuning == working || tuning == source))
            {
                return working;
            }
            source = tuning;
            working = UnityEngine.Object.Instantiate(tuning);
            working.name = tuning.name + " (live)";
            return working;
        }

        /// <summary>What a hero should play with: the working copy when his tuning has one.</summary>
        public static PlayerTuning For(PlayerTuning tuning) => working != null && tuning == source ? working : tuning;

        /// <summary>Drops the working copy and every forced ability.</summary>
        public static void Forget()
        {
            if (working != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(working);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(working);
                }
            }
            source = null;
            working = null;
            Array.Clear(forced, 0, forced.Length);
        }

        /// <summary>A field's value on a tuning, as the console prints it.</summary>
        public static string Read(PlayerTuning tuning, Field field) => Format(field.Info.GetValue(tuning));

        /// <summary>Whether the working copy's value differs from the asset's.</summary>
        public static bool IsChanged(Field field) =>
            working != null && !object.Equals(field.Info.GetValue(working), field.Info.GetValue(source));

        /// <summary>The fields whose working value differs from the asset's.</summary>
        public static List<Field> Changed()
        {
            var result = new List<Field>();
            foreach (var field in Fields)
            {
                if (IsChanged(field))
                {
                    result.Add(field);
                }
            }
            return result;
        }

        /// <summary>Sets a field on the working copy; false before <see cref="Begin"/>.</summary>
        public static bool Set(Field field, object value)
        {
            if (working == null)
            {
                return false;
            }
            field.Info.SetValue(working, value);
            return true;
        }

        /// <summary>Puts one field back to the asset's value; whether it had changed.</summary>
        public static bool Restore(Field field)
        {
            if (!IsChanged(field))
            {
                return false;
            }
            field.Info.SetValue(working, field.Info.GetValue(source));
            return true;
        }

        /// <summary>Puts every field back to the asset's value; how many had changed.</summary>
        public static int Restore()
        {
            var count = 0;
            foreach (var field in Fields)
            {
                if (Restore(field))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Writes the changed values to the tuning asset and says what it did. Only the
        /// editor can: a build answers <see cref="CannotSaveText"/>.
        /// </summary>
        public static string WriteToAsset()
        {
            var changed = Changed();
            if (changed.Count == 0)
            {
                return NothingTunedText;
            }
#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.Contains(source))
            {
                return NotAnAssetText;
            }
            foreach (var field in changed)
            {
                field.Info.SetValue(source, field.Info.GetValue(working));
            }
            UnityEditor.EditorUtility.SetDirty(source);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(source);
            var path = UnityEditor.AssetDatabase.GetAssetPath(source);
            return $"Wrote {changed.Count} {(changed.Count == 1 ? "value" : "values")} to {path}.";
#else
            return CannotSaveText;
#endif
        }

        // --- Text ------------------------------------------------------------------

        /// <summary>A value as the console prints it: numbers without a culture, a vector as "x y".</summary>
        public static string Format(object value)
        {
            switch (value)
            {
                case float number:
                    return number.ToString("0.####", CultureInfo.InvariantCulture);
                case int whole:
                    return whole.ToString(CultureInfo.InvariantCulture);
                case bool flag:
                    return flag ? "on" : "off";
                case Vector2 vector:
                    return Format(vector.x) + " " + Format(vector.y);
            }
            return value != null ? value.ToString() : "";
        }

        /// <summary>What a field takes, for a usage line.</summary>
        public static string Hint(Field field)
        {
            var type = field.Info.FieldType;
            if (type == typeof(int))
            {
                return "a whole number";
            }
            if (type == typeof(bool))
            {
                return "on|off";
            }
            if (type == typeof(Vector2))
            {
                return "x y";
            }
            return "a number";
        }

        /// <summary>Reads the words typed after a field's name as a value of its type.</summary>
        public static bool TryParse(Field field, string[] words, out object value)
        {
            value = null;
            var type = field.Info.FieldType;
            if (type == typeof(Vector2))
            {
                var parts = words;
                if (words.Length == 1)
                {
                    parts = words[0].Split(',');
                }
                if (parts.Length != 2 || !TryNumber(parts[0], out var x) || !TryNumber(parts[1], out var y))
                {
                    return false;
                }
                value = new Vector2(x, y);
                return true;
            }
            if (words.Length != 1)
            {
                return false;
            }
            if (type == typeof(float))
            {
                if (!TryNumber(words[0], out var number))
                {
                    return false;
                }
                value = number;
                return true;
            }
            if (type == typeof(int))
            {
                var style = NumberStyles.AllowLeadingSign;
                if (!int.TryParse(words[0], style, CultureInfo.InvariantCulture, out var whole))
                {
                    return false;
                }
                value = whole;
                return true;
            }
            if (type == typeof(bool))
            {
                if (!TryOnOff(words[0], out var flag))
                {
                    return false;
                }
                value = flag;
                return true;
            }
            return false;
        }

        private static bool TryNumber(string word, out float number)
        {
            var style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (!float.TryParse(word, style, CultureInfo.InvariantCulture, out number))
            {
                return false;
            }
            return !float.IsNaN(number) && !float.IsInfinity(number);
        }

        /// <summary>Reads "on" or "off" (or "true", "false", "1", "0").</summary>
        public static bool TryOnOff(string word, out bool on)
        {
            switch ((word ?? "").ToLowerInvariant())
            {
                case "on":
                case "true":
                case "1":
                    on = true;
                    return true;
                case "off":
                case "false":
                case "0":
                    on = false;
                    return true;
            }
            on = false;
            return false;
        }

        // --- Abilities ---------------------------------------------------------------

        /// <summary>Whether the hero has an ability, given whether he owns it: a forced one wins.</summary>
        public static bool Has(Save.Ability ability, bool owned)
        {
            var index = (int)ability;
            if (index < 0 || index >= forced.Length || forced[index] == 0)
            {
                return owned;
            }
            return forced[index] > 0;
        }

        /// <summary>Whether an ability is forced, and which way.</summary>
        public static bool IsForced(Save.Ability ability, out bool on)
        {
            var index = (int)ability;
            var state = index >= 0 && index < forced.Length ? forced[index] : 0;
            on = state > 0;
            return state != 0;
        }

        /// <summary>Forces an ability on or off for the session, whatever the hero owns.</summary>
        public static void Force(Save.Ability ability, bool on)
        {
            var index = (int)ability;
            if (index >= 0 && index < forced.Length)
            {
                forced[index] = on ? 1 : -1;
            }
        }

        /// <summary>Leaves an ability as the hero owns it again.</summary>
        public static void Release(Save.Ability ability)
        {
            var index = (int)ability;
            if (index >= 0 && index < forced.Length)
            {
                forced[index] = 0;
            }
        }

        /// <summary>Leaves every ability as the hero owns it.</summary>
        public static void ReleaseAll() => Array.Clear(forced, 0, forced.Length);
    }
}
