using System.Collections.Generic;
using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// Which prefab a WorldPlacement spawns, by key: the placement's type ("save",
    /// "ability", "upgrade", "power", "item", "relic", "trigger"),"enemy:&lt;character&gt;" or
    /// "npc:&lt;character&gt;" for a particular body, "boss" for an enemy whose character
    /// is a boss id, and "enemy" or "npc" as the fallbacks. One asset at
    /// Assets/Data/Placements.asset, filled by Descender &gt; Setup &gt; Placement catalog
    /// from the prefabs under Assets/Prefabs by their names.
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

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

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
                    if (type == "enemy" && System.Array.IndexOf(Enemies.BossIds.All, character) >= 0)
                    {
                        var boss = Find("boss");
                        if (boss != null)
                        {
                            return boss;
                        }
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
