using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A Tiles V2 map as text (docs/art/tiles-v2.md): `@key value` lines (properties), then
    /// a grid of what stands in each cell (ground, a liquid, a torch, the hero's start), a
    /// blank line, and a grid of the wall behind each cell; row 0 is the top and `#` starts
    /// a comment. The characters are the tile set's legend. Column and row count from the
    /// top left; the cell of (column, row) has y up with the bottom row at 0.
    /// </summary>
    public sealed class TilesV2Map
    {
        public const char Air = '.';

        private readonly List<string> terrain;
        private readonly List<string> walls;
        private readonly Dictionary<string, Vector2> views = new Dictionary<string, Vector2>();

        private TilesV2Map(List<string> terrainRows, List<string> wallRows)
        {
            terrain = terrainRows;
            walls = wallRows;
            Height = terrain.Count;
            foreach (var row in terrain)
            {
                Width = Mathf.Max(Width, row.Length);
            }
            Surface = Height;
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>The row the cave backdrop starts at (`@surface`): the map's height when it has none.</summary>
        public int Surface { get; private set; }

        /// <summary>The map's named places (`@view name column row`), as (column, row) from the top left.</summary>
        public IReadOnlyDictionary<string, Vector2> Views => views;

        /// <summary>What stands in a cell; air outside the map.</summary>
        public char TerrainAt(int column, int row) => At(terrain, column, row);

        /// <summary>The wall behind a cell; air outside the map.</summary>
        public char WallAt(int column, int row) => At(walls, column, row);

        /// <summary>The cell of a map position: y up, the bottom row at 0.</summary>
        public Vector3Int CellOf(int column, int row) => new Vector3Int(column, Height - 1 - row, 0);

        /// <summary>A map position (column and row from the top left, fractions allowed) in the grid's units.</summary>
        public Vector2 PointOf(Vector2 columnRow) => new Vector2(columnRow.x, Height - columnRow.y);

        /// <summary>Reads a map; throws when it has no wall grid.</summary>
        public static TilesV2Map Parse(string text)
        {
            var grids = new List<List<string>> { new List<string>() };
            var properties = new List<string[]>();
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("#", System.StringComparison.Ordinal))
                {
                    continue;
                }
                if (raw.StartsWith("@", System.StringComparison.Ordinal))
                {
                    properties.Add(raw.Substring(1).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
                    continue;
                }
                if (raw.Length == 0)
                {
                    if (grids[grids.Count - 1].Count > 0)
                    {
                        grids.Add(new List<string>());
                    }
                    continue;
                }
                grids[grids.Count - 1].Add(raw);
            }
            if (grids.Count < 2 || grids[1].Count == 0)
            {
                throw new System.FormatException("Tiles V2: a map needs a terrain grid, a blank line and a wall grid.");
            }
            var map = new TilesV2Map(grids[0], grids[1]);
            foreach (var parts in properties)
            {
                if (parts.Length == 2 && parts[0] == "surface" && int.TryParse(parts[1], out var surface))
                {
                    map.Surface = surface;
                }
                else if (
                    parts.Length == 4
                    && parts[0] == "view"
                    && TryNumber(parts[2], out var column)
                    && TryNumber(parts[3], out var row)
                )
                {
                    map.views[parts[1]] = new Vector2(column, row);
                }
            }
            return map;
        }

        private static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static char At(List<string> rows, int column, int row)
        {
            if (row < 0 || row >= rows.Count || column < 0 || column >= rows[row].Length)
            {
                return Air;
            }
            return rows[row][column];
        }
    }
}
