using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The chart of a Tiles V2 world: a picture with one pixel for each tile that shows only
    /// what the hero has been near, so it never knows the land before he does. Each region
    /// has its own tint; rock is dark, air lighter, the liquids their own colours.
    /// </summary>
    public sealed class TilesV2Chart
    {
        /// <summary>How far round the hero the chart fills in, in tiles.</summary>
        public const int Sight = 14;

        private static readonly Color Unseen = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color Sky = new Color(0.36f, 0.52f, 0.78f, 1f);
        private static readonly Color Ledge = new Color(0.85f, 0.75f, 0.5f, 1f);

        private readonly TilesV2Map map;
        private readonly Color32[] colours;
        private readonly Color32[] pixels;
        private readonly bool[] seen;
        private readonly bool[] hidden;
        private readonly Color32[] rocks;
        private bool stale;
        private Vector2Int last = new Vector2Int(int.MinValue, int.MinValue);

        public TilesV2Chart(TilesV2World world)
        {
            map = world.Map;
            var set = world.Set;
            var count = map.Width * map.Height;
            colours = new Color32[count];
            pixels = new Color32[count];
            seen = new bool[count];
            hidden = new bool[count];
            rocks = new Color32[map.Height];
            for (var row = 0; row < map.Height; row++)
            {
                var tint = new Color(0.1f, 0.1f, 0.12f);
                var open = row < map.Surface;
                if (map.Regions.Count > 0)
                {
                    var region = set.RegionOf(map.Regions[map.RegionAt(row)].Key);
                    if (region != null && region.cave != null)
                    {
                        tint = region.ambient;
                    }
                }
                var rock = Color.Lerp(new Color(0.13f, 0.13f, 0.16f), tint, 0.55f);
                var air = open ? Sky : tint * 1.5f + new Color(0.14f, 0.14f, 0.16f);
                rock.a = 1f;
                air.a = 1f;
                rocks[map.Height - 1 - row] = rock;
                for (var column = 0; column < map.Width; column++)
                {
                    var colour = air;
                    if (set.TryGlyph(map.TerrainAt(column, row), out var glyph))
                    {
                        switch (glyph.layer)
                        {
                            case TilesV2Set.Layer.Terrain:
                                colour = rock;
                                break;
                            case TilesV2Set.Layer.Liquid:
                                colour = LiquidColour(set.LiquidOf(glyph.key));
                                break;
                            case TilesV2Set.Layer.Ledge:
                                colour = Ledge;
                                break;
                        }
                    }
                    var i = (map.Height - 1 - row) * map.Width + column;
                    colours[i] = colour;
                    pixels[i] = Unseen;
                }
            }
            Picture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                name = "Chart",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            Picture.SetPixels32(pixels);
            Picture.Apply(false);
        }

        /// <summary>The chart: a pixel a tile, the bottom row first.</summary>
        public Texture2D Picture { get; }

        /// <summary>How many tiles the hero has seen.</summary>
        public int Seen { get; private set; }

        /// <summary>Fills in what is within sight of a cell (y up, as the world's grid has it).</summary>
        public void Reveal(Vector2Int cell)
        {
            if (cell == last)
            {
                return;
            }
            last = cell;
            for (var dy = -Sight; dy <= Sight; dy++)
            {
                var y = cell.y + dy;
                if (y < 0 || y >= map.Height)
                {
                    continue;
                }
                for (var dx = -Sight; dx <= Sight; dx++)
                {
                    var x = cell.x + dx;
                    if (x < 0 || x >= map.Width || dx * dx + dy * dy > Sight * Sight)
                    {
                        continue;
                    }
                    var i = y * map.Width + x;
                    if (!seen[i])
                    {
                        seen[i] = true;
                        pixels[i] = hidden[i] ? rocks[y] : colours[i];
                        Seen++;
                        stale = true;
                    }
                }
            }
        }

        /// <summary>Fills in everything: the whole world on the chart.</summary>
        public void RevealAll()
        {
            for (var i = 0; i < seen.Length; i++)
            {
                seen[i] = true;
                pixels[i] = hidden[i] ? rocks[i / map.Width] : colours[i];
            }
            Seen = seen.Length;
            stale = true;
        }

        /// <summary>
        /// Keeps a rectangle of cells off the chart, drawn as rock even when the hero has been
        /// near: a room behind a wall he has not broken. `Show(rect)` puts it on again.
        /// </summary>
        public void Hide(RectInt cells) => Veil(cells, true);

        /// <summary>Lets a rectangle hidden by <see cref="Hide"/> show as what it is.</summary>
        public void Unhide(RectInt cells) => Veil(cells, false);

        /// <summary>Whether a cell is kept off the chart.</summary>
        public bool IsHidden(Vector2Int cell) =>
            cell.x >= 0
            && cell.y >= 0
            && cell.x < map.Width
            && cell.y < map.Height
            && hidden[cell.y * map.Width + cell.x];

        private void Veil(RectInt cells, bool hide)
        {
            for (var y = Mathf.Max(cells.yMin, 0); y < Mathf.Min(cells.yMax, map.Height); y++)
            {
                for (var x = Mathf.Max(cells.xMin, 0); x < Mathf.Min(cells.xMax, map.Width); x++)
                {
                    var i = y * map.Width + x;
                    hidden[i] = hide;
                    if (seen[i])
                    {
                        pixels[i] = hide ? rocks[y] : colours[i];
                        stale = true;
                    }
                }
            }
        }

        /// <summary>Whether a cell has been seen.</summary>
        public bool HasSeen(Vector2Int cell)
        {
            return cell.x >= 0
                && cell.y >= 0
                && cell.x < map.Width
                && cell.y < map.Height
                && seen[cell.y * map.Width + cell.x];
        }

        /// <summary>Puts what was revealed since the last call into the picture.</summary>
        public void Show()
        {
            if (!stale)
            {
                return;
            }
            stale = false;
            Picture.SetPixels32(pixels);
            Picture.Apply(false);
        }

        public void Release()
        {
            Object.Destroy(Picture);
        }

        private static Color LiquidColour(TilesV2Set.Liquid liquid)
        {
            if (liquid == null || liquid.tile == null)
            {
                return new Color(0.2f, 0.4f, 0.8f, 1f);
            }
            // A liquid that glows shows as its glow, one that does not as what it lets through.
            var glow = liquid.tile.Emits;
            var colour = glow.maxColorComponent > 0.3f ? glow : liquid.tile.Keeps * 0.75f;
            colour.r = Mathf.Clamp01(colour.r);
            colour.g = Mathf.Clamp01(colour.g);
            colour.b = Mathf.Clamp01(colour.b);
            colour.a = 1f;
            return colour;
        }
    }
}
