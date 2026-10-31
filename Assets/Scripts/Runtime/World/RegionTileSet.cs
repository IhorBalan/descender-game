using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A region's tiles: the 256 sprites of its 64 px atlas (Assets/Art/Tiles/&lt;key&gt;.png,
    /// sliced by the import script, index = row * 16 + column), the kind of each
    /// (TileKind, from the Godot TileSet's custom data), the compact cell data of its
    /// four layers and the materials the tilemaps draw with. One asset per region
    /// under Assets/Data/Regions/Tiles, written by tools/unity/import_godot_regions.py.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Region Tile Set", fileName = "region_tiles")]
    public sealed class RegionTileSet : ScriptableObject
    {
        [Tooltip("The atlas's sprites by index, row by row from the top.")]
        public Sprite[] sprites = new Sprite[0];

        [Tooltip("The layers' cells and the kind of each tile (see RegionTileData).")]
        public TextAsset cells;

        [Tooltip("What the lit layers (Background, Decor, Terrain) draw with.")]
        public Material litMaterial;

        [Tooltip("What the Glow layer draws with: unlit.")]
        public Material glowMaterial;

        private RegionTileData.Data parsed;

        /// <summary>The cells and kinds, parsed once.</summary>
        public RegionTileData.Data Data
        {
            get
            {
                if (parsed == null)
                {
                    parsed = RegionTileData.Parse(cells != null ? cells.text : "");
                }
                return parsed;
            }
        }

        public TileKind KindOf(int tile) => Data.KindOf(tile);

        public Sprite SpriteOf(int tile) => tile >= 0 && tile < sprites.Length ? sprites[tile] : null;

        private void OnValidate()
        {
            parsed = null;
        }
    }
}
