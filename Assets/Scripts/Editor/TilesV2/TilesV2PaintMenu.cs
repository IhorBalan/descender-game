using System.Collections.Generic;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender > Tiles V2 > Paint by rule: for a map painted by hand with the Tile Palette
    /// (a Grid with a Terrain tilemap, and any of Walls, Blend, Shade, Decor and Glow beside
    /// it), lays what the standard lays by rule on the tilemaps that are there: the blend
    /// overlays, the shade on the walls, the scattered decor and the glints of ore
    /// (<see cref="TilesV2Painter"/>). Run it again after painting; it clears what it
    /// painted before. Select the Grid or a tilemap under it first.
    /// </summary>
    public static class TilesV2PaintMenu
    {
        public const string TerrainName = "Terrain";
        public const string WallsName = "Walls";
        public const string BlendName = "Blend";
        public const string ShadeName = "Shade";
        public const string DecorName = "Decor";
        public const string GlowName = "Glow";

        [MenuItem("Descender/Tiles V2/Paint by rule")]
        public static void PaintSelected()
        {
            var selected = Selection.activeGameObject;
            var root = selected == null ? null : selected.GetComponentInParent<Grid>();
            if (root == null)
            {
                Debug.LogWarning("Tiles V2: select the Grid (or a tilemap under it) to paint on.");
                return;
            }
            var set = TilesV2Builder.LoadSet();
            var terrain = Child(root.transform, TerrainName);
            if (terrain == null || set == null)
            {
                Debug.LogWarning($"Tiles V2: the Grid needs a {TerrainName} tilemap, and the tiles must be built.");
                return;
            }
            var painted = Paint(root.transform, set);
            Debug.Log($"Tiles V2: painted {painted} cells by rule.");
        }

        /// <summary>Paints every rule layer that is a child of `grid`; returns how many cells were laid.</summary>
        public static int Paint(Transform grid, TilesV2Set set)
        {
            var terrain = Child(grid, TerrainName);
            var liquids = new List<Tilemap>();
            foreach (var liquid in set.liquids)
            {
                var map = Child(grid, "Liquid " + liquid.key);
                if (map != null)
                {
                    liquids.Add(map);
                }
            }
            var field = TileField.Read(terrain, Child(grid, WallsName), liquids);
            if (field == null)
            {
                return 0;
            }
            var painted = 0;
            var blend = Child(grid, BlendName);
            if (blend != null)
            {
                Undo.RegisterCompleteObjectUndo(blend, "Paint Tiles V2 blends");
                painted += TilesV2Painter.Blends(field, set, blend);
            }
            var shade = Child(grid, ShadeName);
            if (shade != null)
            {
                Undo.RegisterCompleteObjectUndo(shade, "Paint Tiles V2 shade");
                painted += TilesV2Painter.Shade(field, set, shade);
            }
            var decor = Child(grid, DecorName);
            if (decor != null)
            {
                Undo.RegisterCompleteObjectUndo(decor, "Scatter Tiles V2 decor");
                painted += TilesV2Painter.Decor(field, set, decor, null);
            }
            var glow = Child(grid, GlowName);
            if (glow != null)
            {
                Undo.RegisterCompleteObjectUndo(glow, "Paint Tiles V2 glints");
                painted += TilesV2Painter.Glints(field, set, glow);
            }
            return painted;
        }

        private static Tilemap Child(Transform root, string name)
        {
            var child = root.Find(name);
            return child != null && child.TryGetComponent<Tilemap>(out var map) ? map : null;
        }
    }
}
