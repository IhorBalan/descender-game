using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A Tiles V2 material (docs/art/tiles-v2.md): a Rule Tile whose rules pick one of the
    /// 47 autotile frames from its neighbours, where every tile of the same join group
    /// counts as "this" (so dirt, grass and stone meet with no edge between them), and
    /// whose random variants are chosen by a hash of the cell rather than Perlin noise, so
    /// neighbours differ and every variant is used about as often. Built from the
    /// generator's manifest by Descender > Tiles V2 > Build tiles.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Tiles V2/Terrain Tile")]
    public sealed class TerrainTile : RuleTile
    {
        [Tooltip("The material's key in the Tiles V2 manifest (dirt, grass, stone...).")]
        [SerializeField]
        private string materialKey = "";

        [Tooltip("Tiles with the same join group meet with no edge; empty joins only this tile.")]
        [SerializeField]
        private string joinGroup = "";

        [Tooltip(
            "The material this tile is of: its own key, or for a slope or an overlay the key of what it is cut from."
        )]
        [SerializeField]
        private string baseKey = "";

        [Tooltip("A slope: half a tile, which neither casts shade nor carries decor.")]
        [SerializeField]
        private bool slope;

        [Tooltip("The light the tile gives of its own, in the tile light's units (glowing ore).")]
        [SerializeField]
        private Color emits = Color.black;

        public string MaterialKey => materialKey;
        public string JoinGroup => joinGroup;

        /// <summary>The material the tile is of: `grass` for the grass tile and for both grass slopes.</summary>
        public string BaseKey => string.IsNullOrEmpty(baseKey) ? materialKey : baseKey;

        public bool IsSlope => slope;

        /// <summary>The light the tile gives of its own; black for most.</summary>
        public Color Emits => emits;

        /// <summary>Sets the key and join group (the builder's entry point for an overlay).</summary>
        public void Configure(string key, string group)
        {
            Configure(key, group, key, false, Color.black);
        }

        /// <summary>Sets what the tile is (the builder's entry point for a material and its slopes).</summary>
        public void Configure(string key, string group, string material, bool isSlope, Color light)
        {
            materialKey = key ?? "";
            joinGroup = group ?? "";
            baseKey = material ?? "";
            slope = isSlope;
            emits = new Color(light.r, light.g, light.b, 1f);
        }

        /// <summary>Whether a neighbouring tile counts as solid ground of this material.</summary>
        public bool Joins(TileBase other)
        {
            if (other == this)
            {
                return true;
            }
            return joinGroup.Length > 0 && other is TerrainTile terrain && terrain.joinGroup == joinGroup;
        }

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            switch (neighbor)
            {
                case TilingRuleOutput.Neighbor.This:
                    return Joins(other);
                case TilingRuleOutput.Neighbor.NotThis:
                    return !Joins(other);
            }
            return base.RuleMatch(neighbor, other);
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            var chosen = tileData.sprite;
            if (chosen == null)
            {
                return;
            }
            // Swap the Perlin pick for a hashed one from the same rule's variants.
            foreach (var rule in m_TilingRules)
            {
                if (rule.m_Output != TilingRuleOutput.OutputSprite.Random || rule.m_Sprites.Length < 2)
                {
                    continue;
                }
                if (System.Array.IndexOf(rule.m_Sprites, chosen) >= 0)
                {
                    tileData.sprite = rule.m_Sprites[Variant(position, rule.m_Sprites.Length)];
                    return;
                }
            }
        }

        /// <summary>The variant a cell shows: a hash of its position, spread evenly over `count`.</summary>
        public static int Variant(Vector3Int position, int count)
        {
            if (count <= 1)
            {
                return 0;
            }
            unchecked
            {
                var hash = position.x * 73856093 ^ position.y * 19349663;
                hash ^= hash >> 13;
                hash *= 0x5bd1e995;
                hash ^= hash >> 15;
                return (hash & 0x7fffffff) % count;
            }
        }
    }
}
