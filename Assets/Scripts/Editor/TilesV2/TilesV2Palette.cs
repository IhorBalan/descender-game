using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// The Tiles V2 Tile Palette (Window > 2D > Tile Palette, "TilesV2 Palette"): the
    /// terrain materials on the top row, their slopes on the next, the walls under them,
    /// the liquids last. What is laid by rule is not in it (blends, shade, decor, glints):
    /// Descender > Tiles V2 > Paint by rule lays those. Made
    /// once and refilled after, so its GUID stays; Build tiles refreshes it.
    /// </summary>
    public static class TilesV2Palette
    {
        public const string PaletteName = "TilesV2 Palette";
        public const string PalettePath = TilesV2Manifest.Folder + "/" + PaletteName + ".prefab";

        /// <summary>Where each tile sits in the palette, by asset path.</summary>
        public static List<(Vector3Int cell, string path)> Layout(TilesV2Manifest manifest)
        {
            var layout = new List<(Vector3Int, string)>();
            var terrain = 0;
            var slope = 0;
            var wall = 0;
            foreach (var material in manifest.materials)
            {
                if (material.IsWall)
                {
                    layout.Add((new Vector3Int(wall++, -2, 0), TilesV2Manifest.TilePath(material.key)));
                    continue;
                }
                layout.Add((new Vector3Int(terrain++, 0, 0), TilesV2Manifest.TilePath(material.key)));
                if (!material.slopes)
                {
                    continue;
                }
                foreach (var kind in manifest.slopes)
                {
                    layout.Add((new Vector3Int(slope++, -1, 0), TilesV2Manifest.SlopePath(material.key, kind)));
                }
            }
            for (var i = 0; i < manifest.liquids.Length; i++)
            {
                layout.Add((new Vector3Int(i, -3, 0), TilesV2Manifest.TilePath(manifest.liquids[i].key)));
            }
            return layout;
        }

        /// <summary>Makes the palette if it is missing and lays every tile in it; returns how many.</summary>
        public static int Build(TilesV2Manifest manifest)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) == null)
            {
                GridPaletteUtility.CreateNewPalette(
                    TilesV2Manifest.Folder,
                    PaletteName,
                    GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Manual,
                    Vector3.one,
                    GridLayout.CellSwizzle.XYZ
                );
            }
            var root = PrefabUtility.LoadPrefabContents(PalettePath);
            try
            {
                var map = root.GetComponentInChildren<Tilemap>();
                map.ClearAllTiles();
                var count = 0;
                foreach (var (cell, path) in Layout(manifest))
                {
                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null)
                    {
                        map.SetTile(cell, tile);
                        count++;
                    }
                }
                map.CompressBounds();
                PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
                return count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
