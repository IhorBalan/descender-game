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
    /// top left; the cell of (column, row) has y up with the bottom row at 0. A world of
    /// several regions says where each starts (`@region key row`) and what of the game's
    /// stands where (`@mark kind column row ...`: a checkpoint, an ability, a gate).
    /// </summary>
    public sealed class TilesV2Map
    {
        public const char Air = '.';

        private readonly List<string> terrain;
        private readonly List<string> walls;
        private readonly Dictionary<string, Vector2> views = new Dictionary<string, Vector2>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Region> regions = new List<Region>();
        private readonly List<Mark> marks = new List<Mark>();

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

        /// <summary>
        /// The hour the map's day starts at (`@hour 17.8`), below zero when it does not say;
        /// and whether its day stands still at that hour (`@hour 17.8 still`): a place where
        /// the sun does not move.
        /// </summary>
        public float Hour { get; private set; } = -1f;
        public bool Still { get; private set; }

        /// <summary>The map's named places (`@view name column row`), as (column, row) from the top left.</summary>
        public IReadOnlyDictionary<string, Vector2> Views => views;

        /// <summary>The map's pictures (`@shot name view hour`): one of its named places at an hour of the day.</summary>
        public IReadOnlyList<Shot> Shots => shots;

        /// <summary>The map's regions (`@region key row`), top to bottom; none for a map that is all one.</summary>
        public IReadOnlyList<Region> Regions => regions;

        /// <summary>The game's things the map places (`@mark kind column row ...`).</summary>
        public IReadOnlyList<Mark> Marks => marks;

        /// <summary>The number of the region a row is in, in <see cref="Regions"/>; -1 for a map with none.</summary>
        public int RegionAt(int row)
        {
            var found = -1;
            for (var i = 0; i < regions.Count; i++)
            {
                if (row >= regions[i].Top || i == 0)
                {
                    found = i;
                }
            }
            return found;
        }

        /// <summary>The row after a region's last: the next region's first, or the map's height.</summary>
        public int RegionEnd(int index) => index + 1 < regions.Count ? regions[index + 1].Top : Height;

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
                else if (parts.Length >= 2 && parts[0] == "hour" && TryNumber(parts[1], out var startHour))
                {
                    map.Hour = Mathf.Repeat(startHour, 24f);
                    map.Still = parts.Length > 2 && parts[2] == "still";
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
                else if (parts.Length == 4 && parts[0] == "shot" && TryNumber(parts[3], out var hour))
                {
                    map.shots.Add(new Shot(parts[1], parts[2], hour));
                }
                else if (parts.Length == 3 && parts[0] == "region" && int.TryParse(parts[2], out var top))
                {
                    map.regions.Add(new Region(parts[1], top));
                }
                else if (
                    parts.Length >= 4
                    && parts[0] == "mark"
                    && int.TryParse(parts[2], out var markColumn)
                    && int.TryParse(parts[3], out var markRow)
                )
                {
                    var rest = new string[parts.Length - 4];
                    System.Array.Copy(parts, 4, rest, 0, rest.Length);
                    map.marks.Add(new Mark(parts[1], markColumn, markRow, rest));
                }
            }
            map.regions.Sort((a, b) => a.Top.CompareTo(b.Top));
            return map;
        }

        /// <summary>A picture worth taking of the map: one of its named places at an hour of the day.</summary>
        public readonly struct Shot
        {
            public readonly string Name;
            public readonly string View;
            public readonly float Hour;

            public Shot(string name, string view, float hour)
            {
                Name = name;
                View = view;
                Hour = hour;
            }
        }

        /// <summary>A region of the map: its key in the tile set and its first row.</summary>
        public readonly struct Region
        {
            public readonly string Key;
            public readonly int Top;

            public Region(string key, int top)
            {
                Key = key;
                Top = top;
            }
        }

        /// <summary>Something of the game's that stands in a cell: its kind, the cell, and what else the kind needs.</summary>
        public readonly struct Mark
        {
            public readonly string Kind;
            public readonly int Column;
            public readonly int Row;
            public readonly string[] Rest;

            public Mark(string kind, int column, int row, string[] rest)
            {
                Kind = kind;
                Column = column;
                Row = row;
                Rest = rest;
            }

            /// <summary>One of the words after the cell; "" when there are fewer.</summary>
            public string Word(int index) => index < Rest.Length ? Rest[index] : "";

            /// <summary>One of the words after the cell as a whole number; `fallback` when it is not one.</summary>
            public int Number(int index, int fallback) =>
                index < Rest.Length && int.TryParse(Rest[index], out var value) ? value : fallback;
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
