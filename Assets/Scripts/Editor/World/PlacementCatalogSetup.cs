using System.IO;
using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.World
{
    /// <summary>
    /// Descender &gt; Setup &gt; Placement catalog: fills Assets/Data/Placements.asset from
    /// the prefabs under Assets/Prefabs by their names, so a WorldPlacement finds what
    /// to spawn: Pickups/Checkpoint for "save", Pickups/AbilityPickup for "ability",
    /// Pickups/Upgrade, Pickups/PowerUp, Pickups/LootDrop for "item", Story/Relic for
    /// "relic" (an item with dialogue), Story/StoryTrigger for "trigger", Enemies/&lt;body&gt;
    /// for "enemy:&lt;body&gt;", Enemies/Boss for "boss", Npcs/&lt;name&gt; for "npc:&lt;name&gt;" and
    /// Npcs/Npc for "npc". It makes the story prefabs first when they are missing
    /// (see <see cref="StoryPrefabs"/>), and copies every key except "save" into the
    /// garden's catalog (Assets/Data/Placements Paradise.asset, which keeps its marble
    /// shrine) when that exists. Safe to run again: a missing prefab leaves its entry alone.
    /// </summary>
    public static class PlacementCatalogSetup
    {
        public const string CatalogPath = "Assets/Data/Placements.asset";
        public const string ParadiseCatalogPath = Paradise.ParadiseGarden.CatalogPath;
        public const string PrefabsFolder = "Assets/Prefabs";

        private static readonly (string key, string path)[] Fixed =
        {
            ("save", "Pickups/Checkpoint"),
            ("ability", "Pickups/AbilityPickup"),
            ("upgrade", "Pickups/Upgrade"),
            ("power", "Pickups/PowerUp"),
            ("item", "Pickups/LootDrop"),
            ("relic", "Story/Relic"),
            ("trigger", "Story/StoryTrigger"),
            ("boss", "Enemies/Boss"),
            ("npc", "Npcs/Npc"),
        };

        [MenuItem("Descender/Setup/Placement catalog")]
        public static void Run()
        {
            StoryPrefabs.Run();
            var catalog = AssetDatabase.LoadAssetAtPath<Descender.World.PlacementCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath) ?? "Assets/Data");
                catalog = ScriptableObject.CreateInstance<Descender.World.PlacementCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var count = 0;
            foreach (var (key, path) in Fixed)
            {
                count += Assign(catalog, key, $"{PrefabsFolder}/{path}.prefab") ? 1 : 0;
            }
            count += AssignFolder(catalog, "enemy", $"{PrefabsFolder}/Enemies", "Boss");
            count += AssignFolder(catalog, "npc", $"{PrefabsFolder}/Npcs", "Npc");
            EditorUtility.SetDirty(catalog);
            var shared = CopyToParadise(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"Descender: placement catalog has {count} prefabs; {shared} shared with the garden's catalog.");
        }

        /// <summary>
        /// The garden zones spawn from their own catalog, a copy with a marble shrine for
        /// "save". Every other key follows the shared catalog. Returns how many were copied.
        /// </summary>
        private static int CopyToParadise(Descender.World.PlacementCatalog source)
        {
            var paradise = AssetDatabase.LoadAssetAtPath<Descender.World.PlacementCatalog>(ParadiseCatalogPath);
            if (paradise == null)
            {
                return 0;
            }
            var count = 0;
            foreach (var entry in source.Entries)
            {
                if (entry.key == "save" || entry.prefab == null)
                {
                    continue;
                }
                paradise.Set(entry.key, entry.prefab);
                count++;
            }
            EditorUtility.SetDirty(paradise);
            return count;
        }

        private static int AssignFolder(
            Descender.World.PlacementCatalog catalog,
            string type,
            string folder,
            string skip
        )
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return 0;
            }
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var stem = Path.GetFileNameWithoutExtension(path);
                if (stem == skip)
                {
                    continue;
                }
                count += Assign(catalog, $"{type}:{stem}", path) ? 1 : 0;
            }
            return count;
        }

        private static bool Assign(Descender.World.PlacementCatalog catalog, string key, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return false;
            }
            catalog.Set(key, prefab);
            return true;
        }
    }
}
