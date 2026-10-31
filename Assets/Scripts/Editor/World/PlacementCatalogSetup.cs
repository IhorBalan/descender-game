using System.IO;
using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.World
{
    /// <summary>
    /// Descender &gt; Setup &gt; Placement catalog: fills Assets/Data/Placements.asset from
    /// the prefabs under Assets/Prefabs by their names, so a WorldPlacement finds what
    /// to spawn: Pickups/Checkpoint for "save", Pickups/AbilityPickup for "ability",
    /// Pickups/Upgrade, Pickups/PowerUp, Pickups/LootDrop for "item", Enemies/&lt;body&gt;
    /// for "enemy:&lt;body&gt;", Enemies/Boss for "boss", Npcs/&lt;name&gt; for "npc:&lt;name&gt;" and
    /// Npcs/Npc for "npc". Safe to run again: a missing prefab leaves its entry alone.
    /// </summary>
    public static class PlacementCatalogSetup
    {
        public const string CatalogPath = "Assets/Data/Placements.asset";
        public const string PrefabsFolder = "Assets/Prefabs";

        private static readonly (string key, string path)[] Fixed =
        {
            ("save", "Pickups/Checkpoint"),
            ("ability", "Pickups/AbilityPickup"),
            ("upgrade", "Pickups/Upgrade"),
            ("power", "Pickups/PowerUp"),
            ("item", "Pickups/LootDrop"),
            ("trigger", "Story/StoryTrigger"),
            ("boss", "Enemies/Boss"),
            ("npc", "Npcs/Npc"),
        };

        [MenuItem("Descender/Setup/Placement catalog")]
        public static void Run()
        {
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
            AssetDatabase.SaveAssets();
            Debug.Log($"Descender: placement catalog has {count} prefabs.");
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
