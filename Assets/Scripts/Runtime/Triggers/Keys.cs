using System.Collections.Generic;

namespace Descender.Triggers
{
    /// <summary>
    /// The keys and tokens the hero carries for triggers: plain names that are not items
    /// of the pack (no stats, no screen, not saved yet). A <see cref="TriggerNeeds"/> asks
    /// for one, a <see cref="GiveItem"/> or <see cref="TakeItem"/> action hands it over.
    /// Static state with a Reset, like the pack.
    /// </summary>
    public static class Keys
    {
        private static readonly HashSet<string> Held = new HashSet<string>();

        public static int Count => Held.Count;

        public static void Reset() => Held.Clear();

        public static bool Has(string id) => !string.IsNullOrEmpty(id) && Held.Contains(id);

        public static void Add(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                Held.Add(id);
            }
        }

        public static bool Remove(string id) => Held.Remove(id ?? "");

        /// <summary>Whether the hero has a thing by this name, as an item of the pack or as a key.</summary>
        public static bool Holding(string id) => Has(id) || Economy.Inventory.Has(id);
    }
}
