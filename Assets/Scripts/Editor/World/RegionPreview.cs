using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.World
{
    /// <summary>
    /// Descender &gt; World &gt; Build region tiles / Clear region tiles: previews the
    /// tilemaps of every RegionTiles in the open scenes, as the game builds them when
    /// a region loads. The built objects are not saved with the scene (they carry
    /// HideFlags.DontSave), so the scene files stay small.
    /// </summary>
    public static class RegionPreview
    {
        [MenuItem("Descender/World/Build region tiles")]
        public static void Build()
        {
            var count = 0;
            foreach (var tiles in Object.FindObjectsByType<Descender.World.RegionTiles>(FindObjectsSortMode.None))
            {
                tiles.Build();
                count++;
            }
            Debug.Log($"Descender: built the tiles of {count} region(s).");
        }

        [MenuItem("Descender/World/Clear region tiles")]
        public static void Clear()
        {
            foreach (var tiles in Object.FindObjectsByType<Descender.World.RegionTiles>(FindObjectsSortMode.None))
            {
                tiles.Clear();
            }
        }
    }
}
