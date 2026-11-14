using System.Collections.Generic;
using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// Which prefab a WorldPlacement spawns, by key: the placement's type ("save",
    /// "ability", "upgrade", "power", "item", "relic", "trigger"),"enemy:&lt;character&gt;" or
    /// "npc:&lt;character&gt;" for a particular body, and "enemy" or "npc" as the
    /// fallbacks; and which profile an enemy is made from, by its character (a body's or
    /// a boss's id; see <see cref="Enemies.FoeWorld"/>). One asset at
    /// Assets/Data/Placements.asset, filled by Descender &gt; Setup &gt; Placement catalog
    /// from the prefabs under Assets/Prefabs and the profiles under Assets/Data/Enemies
    /// and Assets/Data/Bosses by their names.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Placement Catalog", fileName = "Placements")]
    public sealed class PlacementCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string key;
            public GameObject prefab;
        }

        /// <summary>The profile an enemy of one character is made from.</summary>
        [System.Serializable]
        public struct FoeEntry
        {
            public string character;
            public ScriptableObject profile;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        [SerializeField]
        private List<FoeEntry> foes = new List<FoeEntry>();

        public IReadOnlyList<Entry> Entries => entries;
        public IReadOnlyList<FoeEntry> Foes => foes;

        /// <summary>
        /// The profile an enemy of `character` is made from (a WalkerProfile, a
        /// FlyerProfile, a HangerProfile or a BossProfile), or null when the catalog has none.
        /// </summary>
        public ScriptableObject FoeFor(string character)
        {
            if (string.IsNullOrEmpty(character))
            {
                return null;
            }
            foreach (var foe in foes)
            {
                if (foe.character == character)
                {
                    return foe.profile;
                }
            }
            return null;
        }

        public void SetFoe(string character, ScriptableObject profile)
        {
            for (var i = 0; i < foes.Count; i++)
            {
                if (foes[i].character == character)
                {
                    foes[i] = new FoeEntry { character = character, profile = profile };
                    return;
                }
            }
            foes.Add(new FoeEntry { character = character, profile = profile });
        }

        public GameObject Find(string key)
        {
            foreach (var entry in entries)
            {
                if (entry.key == key)
                {
                    return entry.prefab;
                }
            }
            return null;
        }

        /// <summary>The prefab for a placement of `type` with `character`, or null when the catalog has none.</summary>
        public GameObject Resolve(string type, string character)
        {
            if (type == "enemy" || type == "npc")
            {
                if (!string.IsNullOrEmpty(character))
                {
                    var own = Find(type + ":" + character);
                    if (own != null)
                    {
                        return own;
                    }
                }
            }
            return Find(type);
        }

        /// <summary>
        /// As <see cref="Resolve(string, string)"/>, but an "item" that carries dialogue is
        /// a relic to examine (the "relic" entry) rather than loot to pick up; a catalog
        /// with no relic falls back to the item.
        /// </summary>
        public GameObject Resolve(string type, string character, bool hasDialogue)
        {
            if (type == "item" && hasDialogue)
            {
                var relic = Find("relic");
                if (relic != null)
                {
                    return relic;
                }
            }
            return Resolve(type, character);
        }

        public void Set(string key, GameObject prefab)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    entries[i] = new Entry { key = key, prefab = prefab };
                    return;
                }
            }
            entries.Add(new Entry { key = key, prefab = prefab });
        }
    }
}
