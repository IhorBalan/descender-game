using System.Collections.Generic;
using System.Linq;

namespace Descender.Story
{
    /// <summary>
    /// Story flags: what the hero has seen, who he has talked to and which relics
    /// he has picked up. Flags are plain strings, set once and never cleared
    /// within a run; dialogue entries check them (see <see cref="DialogueData"/>)
    /// and the game session writes them into the save. Static, like the GDScript
    /// original, so every system reaches one state without a scene reference.
    /// </summary>
    public static class StoryFlags
    {
        private static readonly HashSet<string> Flags = new HashSet<string>();

        /// <summary>A new game starts with nothing seen.</summary>
        public static void Reset() => Flags.Clear();

        public static void Set(string flag)
        {
            if (!string.IsNullOrEmpty(flag))
            {
                Flags.Add(flag);
            }
        }

        public static bool Has(string flag) => !string.IsNullOrEmpty(flag) && Flags.Contains(flag);

        public static bool HasAll(IEnumerable<string> names) => names.All(Has);

        public static bool HasAny(IEnumerable<string> names) => names.Any(Has);

        public static int Count => Flags.Count;

        /// <summary>The flags as a sorted list, for the save file.</summary>
        public static List<string> ToList()
        {
            var result = Flags.ToList();
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        /// <summary>Replaces the flags with the ones from a save.</summary>
        public static void FromList(IEnumerable<string> values)
        {
            Flags.Clear();
            if (values == null)
            {
                return;
            }
            foreach (var value in values)
            {
                Set(value);
            }
        }
    }
}
