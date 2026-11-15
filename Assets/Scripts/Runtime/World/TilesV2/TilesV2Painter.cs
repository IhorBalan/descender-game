using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Paints what a Tiles V2 map gets by rule on top of its ground and walls
    /// (docs/art/tiles-v2.md), each on its own tilemap: the blend overlays where one
    /// material meets another, the shade the ground casts on the wall behind it, the decor
    /// scattered on open faces and the glints of ore. Everything follows from the cells
    /// alone (a hash of the cell where there is a choice), so painting again changes
    /// nothing and the generator's preview (tools/tiles_v2) shows the same map.
    /// </summary>
    public static class TilesV2Painter
    {
        // Each neighbour's bit and offset, y up: the generator's N, NE, E, SE, S, SW, W, NW.
        private static readonly (int bit, int dx, int dy)[] Around =
        {
            (1, 0, 1),
            (2, 1, 1),
            (4, 1, 0),
            (8, 1, -1),
            (16, 0, -1),
            (32, -1, -1),
            (64, -1, 0),
            (128, -1, 1),
        };

        // A blend overlay's bit for each side, the generator's BLEND_SIDES.
        private static readonly (int bit, int dx, int dy)[] Sides = { (1, 0, 1), (2, 1, 0), (4, 0, -1), (8, -1, 0) };

        /// <summary>The most cells a decor may stack.</summary>
        public const int TallestDecor = 4;

        /// <summary>
        /// A hash of a cell and a salt, 0 to 2^31 - 1, in 32-bit integers: what scatters the
        /// decor and staggers the glints. The generator's cell_hash is the same sum.
        /// </summary>
        public static int Hash(int x, int y, int salt)
        {
            unchecked
            {
                var h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)(h & 0x7fffffffu);
            }
        }

        /// <summary>
        /// The shade a wall cell shows for the ground round it (bits N 1, NE 2, E 4, SE 8,
        /// S 16, SW 32, W 64, NW 128): every side, and a corner only when neither side beside
        /// it has ground, since otherwise the sides' shade covers it.
        /// </summary>
        public static int ShadeMask(int around)
        {
            var mask = around & (1 | 4 | 16 | 64);
            if ((around & 2) != 0 && (around & (1 | 4)) == 0)
            {
                mask |= 2;
            }
            if ((around & 8) != 0 && (around & (16 | 4)) == 0)
            {
                mask |= 8;
            }
            if ((around & 32) != 0 && (around & (16 | 64)) == 0)
            {
                mask |= 32;
            }
            if ((around & 128) != 0 && (around & (1 | 64)) == 0)
            {
                mask |= 128;
            }
            return mask;
        }

        /// <summary>
        /// Clears `blend` and lays, on every full tile whose material is a blend's `onto`, the
        /// overlay for the sides that touch one of its `from` materials: one overlay a cell,
        /// the first blend of the set that applies. Returns how many.
        /// </summary>
        public static int Blends(TileField field, TilesV2Set set, Tilemap blend)
        {
            blend.ClearAllTiles();
            var cells = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            var done = new HashSet<int>();
            foreach (var rule in set.blends)
            {
                var from = new HashSet<string>(rule.from);
                for (var y = 0; y < field.Height; y++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var i = field.Index(x, y);
                        if (field.BlockKey(x, y) != rule.onto || done.Contains(i))
                        {
                            continue;
                        }
                        var sides = 0;
                        foreach (var (bit, dx, dy) in Sides)
                        {
                            var other = field.GroundKey(x + dx, y + dy);
                            if (other != null && from.Contains(other))
                            {
                                sides |= bit;
                            }
                        }
                        if (sides == 0 || sides >= rule.overlays.Length || rule.overlays[sides] == null)
                        {
                            continue;
                        }
                        cells.Add(field.CellAt(x, y));
                        tiles.Add(rule.overlays[sides]);
                        done.Add(i);
                    }
                }
            }
            blend.SetTiles(cells.ToArray(), tiles.ToArray());
            return cells.Count;
        }

        /// <summary>
        /// Clears `shade` and lays the shade of the ground on the walls: on a wall cell with
        /// no full tile in it, the tile for the ground round it; behind a full tile that has
        /// open air beside it, the full shade (seen through its rounded corners). Returns how many.
        /// </summary>
        public static int Shade(TileField field, TilesV2Set set, Tilemap shade)
        {
            shade.ClearAllTiles();
            var cells = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            for (var y = 0; y < field.Height; y++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    if (!field.Walled[field.Index(x, y)])
                    {
                        continue;
                    }
                    var around = 0;
                    foreach (var (bit, dx, dy) in Around)
                    {
                        if (field.BlockKey(x + dx, y + dy) != null)
                        {
                            around |= bit;
                        }
                    }
                    TileBase tile = null;
                    if (field.BlockKey(x, y) != null)
                    {
                        tile = around != 255 ? set.shadeFull : null;
                    }
                    else if (ShadeMask(around) != 0)
                    {
                        tile = set.ShadeFor(ShadeMask(around));
                    }
                    if (tile != null)
                    {
                        cells.Add(field.CellAt(x, y));
                        tiles.Add(tile);
                    }
                }
            }
            shade.SetTiles(cells.ToArray(), tiles.ToArray());
            return cells.Count;
        }

        /// <summary>
        /// Clears `decor` and scatters the set's decor: for each open face of a full ground
        /// tile, at most one decor that grows on its material, picked by a hash of the cell;
        /// first what stands on floors, then what hangs from ceilings. `taken` are cells that
        /// stay clear (a torch, the hero's start). Returns how many cells were laid.
        /// </summary>
        public static int Decor(TileField field, TilesV2Set set, Tilemap decor, ICollection<Vector3Int> taken)
        {
            decor.ClearAllTiles();
            var placed = new Dictionary<Vector3Int, TileBase>();
            Scatter(field, set, TilesV2Set.Anchor.Floor, 1, set.floorSalt, taken, placed);
            Scatter(field, set, TilesV2Set.Anchor.Ceiling, -1, set.ceilingSalt, taken, placed);
            var cells = new Vector3Int[placed.Count];
            var tiles = new TileBase[placed.Count];
            var n = 0;
            foreach (var pair in placed)
            {
                cells[n] = pair.Key;
                tiles[n++] = pair.Value;
            }
            decor.SetTiles(cells, tiles);
            return n;
        }

        /// <summary>
        /// Clears `glow` and lays the glint on every full tile of a material that glints.
        /// Returns how many.
        /// </summary>
        public static int Glints(TileField field, TilesV2Set set, Tilemap glow)
        {
            glow.ClearAllTiles();
            if (set.glint == null || set.glintOn.Length == 0)
            {
                return 0;
            }
            var on = new HashSet<string>(set.glintOn);
            var cells = new List<Vector3Int>();
            for (var y = 0; y < field.Height; y++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var key = field.BlockKey(x, y);
                    if (key != null && on.Contains(key))
                    {
                        cells.Add(field.CellAt(x, y));
                    }
                }
            }
            var tiles = new TileBase[cells.Count];
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i] = set.glint;
            }
            glow.SetTiles(cells.ToArray(), tiles);
            return cells.Count;
        }

        // One pass of the scatter: `up` is 1 for decor standing over its ground, -1 for decor hanging under it.
        private static void Scatter(
            TileField field,
            TilesV2Set set,
            TilesV2Set.Anchor anchor,
            int up,
            int salt,
            ICollection<Vector3Int> taken,
            Dictionary<Vector3Int, TileBase> placed
        )
        {
            var fitting = new List<TilesV2Set.DecorRule>();
            // Top row first, as the generator's preview walks the map.
            for (var y = field.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var key = field.BlockKey(x, y);
                    if (key == null)
                    {
                        continue;
                    }
                    var room = 0;
                    while (room < TallestDecor && Free(field, x, y + up * (room + 1), taken, placed))
                    {
                        room++;
                    }
                    if (room == 0)
                    {
                        continue;
                    }
                    var openSky = field.Sky[field.Index(x, y + up)];
                    fitting.Clear();
                    foreach (var rule in set.decor)
                    {
                        if (rule.anchor == anchor && Fits(rule, key, openSky) && rule.min <= room)
                        {
                            fitting.Add(rule);
                        }
                    }
                    var cell = field.CellAt(x, y);
                    var roll = Hash(cell.x, cell.y, salt) / 2147483648.0;
                    TilesV2Set.DecorRule chosen = null;
                    foreach (var rule in fitting)
                    {
                        if (roll < rule.chance)
                        {
                            chosen = rule;
                            break;
                        }
                        roll -= rule.chance;
                    }
                    if (chosen == null)
                    {
                        continue;
                    }
                    var span = Mathf.Max(chosen.max - chosen.min + 1, 1);
                    var count = Mathf.Min(chosen.min + Hash(cell.x, cell.y, salt + 1) % span, room);
                    for (var i = 0; i < count; i++)
                    {
                        var variants =
                            i == count - 1 ? chosen.last
                            : i == 0 ? chosen.first
                            : chosen.middle;
                        if (variants.Length == 0)
                        {
                            continue;
                        }
                        var tile = variants[Hash(cell.x, cell.y, salt + 2 + i) % variants.Length];
                        placed[field.CellAt(x, y + up * (i + 1))] = tile;
                    }
                }
            }
        }

        private static bool Fits(TilesV2Set.DecorRule rule, string key, bool openSky)
        {
            if (rule.where == TilesV2Set.Where.Sky && !openSky)
            {
                return false;
            }
            if (rule.where == TilesV2Set.Where.Cave && openSky)
            {
                return false;
            }
            return System.Array.IndexOf(rule.on, key) >= 0;
        }

        private static bool Free(
            TileField field,
            int x,
            int y,
            ICollection<Vector3Int> taken,
            Dictionary<Vector3Int, TileBase> placed
        )
        {
            if (!field.Empty(x, y))
            {
                return false;
            }
            var cell = field.CellAt(x, y);
            return !placed.ContainsKey(cell) && (taken == null || !taken.Contains(cell));
        }
    }
}
