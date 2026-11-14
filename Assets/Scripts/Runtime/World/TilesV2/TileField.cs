using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// What stands in every cell of a Tiles V2 map, read once from its tilemaps: the ground
    /// tile, whether a wall is behind it, the liquid in it, and whether it is open to the
    /// sky. The painters (TilesV2Painter) and the tile light (TileLightMap) read this rather
    /// than the tilemaps. A grid is row major with y up, from the bounds' bottom left; the
    /// bounds reach a few rows of open air above the highest tile, so every column starts
    /// under the sky and tall decor has room.
    /// </summary>
    public sealed class TileField
    {
        /// <summary>The rows of open air the field covers over the map's top tile.</summary>
        public const int SkyRows = 4;

        private TileField(BoundsInt bounds)
        {
            Bounds = bounds;
            Width = bounds.size.x;
            Height = bounds.size.y;
            var count = Width * Height;
            Ground = new TerrainTile[count];
            Walled = new bool[count];
            Liquid = new LiquidTile[count];
            Sky = new bool[count];
        }

        public BoundsInt Bounds { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>The ground tile of each cell (a material or a slope); null for none.</summary>
        public TerrainTile[] Ground { get; }

        /// <summary>Whether each cell has a wall behind it.</summary>
        public bool[] Walled { get; }

        /// <summary>The liquid in each cell; null for none.</summary>
        public LiquidTile[] Liquid { get; }

        /// <summary>Whether each cell is open to the sky: nothing over it in its column shades it.</summary>
        public bool[] Sky { get; }

        /// <summary>Reads the tilemaps of a map; null when they hold no tile at all.</summary>
        public static TileField Read(Tilemap terrain, Tilemap walls, IReadOnlyList<Tilemap> liquids)
        {
            var any = false;
            var bounds = new BoundsInt();
            Grow(terrain, ref bounds, ref any);
            Grow(walls, ref bounds, ref any);
            if (liquids != null)
            {
                foreach (var liquid in liquids)
                {
                    Grow(liquid, ref bounds, ref any);
                }
            }
            if (!any || bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                return null;
            }
            bounds = new BoundsInt(bounds.xMin, bounds.yMin, 0, bounds.size.x, bounds.size.y + SkyRows, 1);
            var field = new TileField(bounds);
            for (var y = 0; y < field.Height; y++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                    var i = y * field.Width + x;
                    field.Ground[i] = terrain != null ? terrain.GetTile(cell) as TerrainTile : null;
                    field.Walled[i] = walls != null && walls.HasTile(cell);
                    if (liquids == null)
                    {
                        continue;
                    }
                    foreach (var liquid in liquids)
                    {
                        if (liquid != null && liquid.GetTile(cell) is LiquidTile tile)
                        {
                            field.Liquid[i] = tile;
                            break;
                        }
                    }
                }
            }
            for (var x = 0; x < field.Width; x++)
            {
                for (var y = field.Height - 1; y >= 0; y--)
                {
                    var i = y * field.Width + x;
                    if (field.Shades(i))
                    {
                        break;
                    }
                    field.Sky[i] = true;
                }
            }
            return field;
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int Index(int x, int y) => y * Width + x;

        /// <summary>The cell of a grid position.</summary>
        public Vector3Int CellAt(int x, int y) => new Vector3Int(Bounds.xMin + x, Bounds.yMin + y, 0);

        /// <summary>Whether a cell holds ground of any shape.</summary>
        public bool Solid(int index) => Ground[index] != null;

        /// <summary>Whether a cell stops the sky: ground, a wall or a liquid.</summary>
        public bool Shades(int index) => Ground[index] != null || Walled[index] || Liquid[index] != null;

        /// <summary>The material of the full ground tile at a grid position; null for air, a slope or outside.</summary>
        public string BlockKey(int x, int y)
        {
            if (!Inside(x, y))
            {
                return null;
            }
            var tile = Ground[y * Width + x];
            return tile != null && !tile.IsSlope ? tile.BaseKey : null;
        }

        /// <summary>The material of the ground at a grid position, a slope's included; null for none or outside.</summary>
        public string GroundKey(int x, int y)
        {
            if (!Inside(x, y))
            {
                return null;
            }
            var tile = Ground[y * Width + x];
            return tile != null ? tile.BaseKey : null;
        }

        /// <summary>Whether nothing stands in a grid position inside the field: no ground and no liquid.</summary>
        public bool Empty(int x, int y)
        {
            if (!Inside(x, y))
            {
                return false;
            }
            var i = y * Width + x;
            return Ground[i] == null && Liquid[i] == null;
        }

        private static void Grow(Tilemap map, ref BoundsInt bounds, ref bool any)
        {
            if (map == null)
            {
                return;
            }
            map.CompressBounds();
            var own = map.cellBounds;
            if (own.size.x <= 0 || own.size.y <= 0)
            {
                return;
            }
            if (!any)
            {
                bounds = own;
                any = true;
                return;
            }
            var min = Vector3Int.Min(bounds.min, own.min);
            var max = Vector3Int.Max(bounds.max, own.max);
            bounds = new BoundsInt(min, max - min);
        }
    }
}
