using System.Collections.Generic;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Paints the Tiles V2 blend overlays: on every cell of the terrain whose material is a
    /// blend's `onto`, the overlay for the sides that touch one of its `from` materials
    /// (dirt reaching over the edge of stone), on a separate tilemap drawn just above the
    /// terrain. Run it again after painting terrain (Descender > Tiles V2 > Paint blends,
    /// with the Grid or a tilemap under it selected).
    /// </summary>
    public static class TilesV2Blends
    {
        public const string TerrainName = "Terrain";
        public const string BlendName = "Blend";

        // Overlay bits for the four sides, the generator's BLEND_SIDES.
        private static readonly (int bit, Vector3Int offset)[] Sides =
        {
            (1, new Vector3Int(0, 1, 0)),
            (2, new Vector3Int(1, 0, 0)),
            (4, new Vector3Int(0, -1, 0)),
            (8, new Vector3Int(-1, 0, 0)),
        };

        [MenuItem("Descender/Tiles V2/Paint blends")]
        public static void PaintSelected()
        {
            var selected = Selection.activeGameObject;
            var root = selected == null ? null : selected.GetComponentInParent<Grid>();
            if (root == null)
            {
                Debug.LogWarning("Tiles V2: select the Grid (or a tilemap under it) to paint blends on.");
                return;
            }
            var terrain = Child(root.transform, TerrainName);
            var blend = Child(root.transform, BlendName);
            if (terrain == null || blend == null)
            {
                Debug.LogWarning($"Tiles V2: the Grid needs a {TerrainName} and a {BlendName} tilemap.");
                return;
            }
            Undo.RegisterCompleteObjectUndo(blend, "Paint Tiles V2 blends");
            var painted = Paint(terrain, blend, TilesV2Manifest.Load());
            Debug.Log($"Tiles V2: painted {painted} blend overlays.");
        }

        /// <summary>Clears `blend` and paints the overlays `terrain` calls for; returns how many.</summary>
        public static int Paint(Tilemap terrain, Tilemap blend, TilesV2Manifest manifest)
        {
            blend.ClearAllTiles();
            var painted = 0;
            var bounds = terrain.cellBounds;
            foreach (var entry in manifest.blends)
            {
                var from = new HashSet<string>(entry.from);
                var overlays = new Dictionary<int, TileBase>();
                foreach (var sides in entry.masks)
                {
                    overlays[sides] = AssetDatabase.LoadAssetAtPath<TileBase>(
                        TilesV2Manifest.BlendPath(entry.key, sides)
                    );
                }
                foreach (var cell in bounds.allPositionsWithin)
                {
                    if (!(terrain.GetTile(cell) is TerrainTile tile) || tile.MaterialKey != entry.onto)
                    {
                        continue;
                    }
                    var sides = SidesAt(terrain, cell, from);
                    if (sides != 0 && overlays.TryGetValue(sides, out var overlay) && overlay != null)
                    {
                        blend.SetTile(cell, overlay);
                        painted++;
                    }
                }
            }
            return painted;
        }

        /// <summary>The overlay bits of the sides of `cell` that touch one of the `from` materials.</summary>
        public static int SidesAt(Tilemap terrain, Vector3Int cell, HashSet<string> from)
        {
            var sides = 0;
            foreach (var (bit, offset) in Sides)
            {
                if (terrain.GetTile(cell + offset) is TerrainTile other && from.Contains(BaseKey(other.MaterialKey)))
                {
                    sides |= bit;
                }
            }
            return sides;
        }

        // A slope's key is its material's with the slope after it (grass_up_right).
        private static string BaseKey(string key)
        {
            var cut = key.IndexOf("_up_", System.StringComparison.Ordinal);
            return cut < 0 ? key : key.Substring(0, cut);
        }

        private static Tilemap Child(Transform root, string name)
        {
            var child = root.Find(name);
            return child != null && child.TryGetComponent<Tilemap>(out var map) ? map : null;
        }
    }
}
