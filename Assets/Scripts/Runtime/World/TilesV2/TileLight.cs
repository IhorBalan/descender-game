using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The Tiles V2 light (docs/art/tiles-v2.md): one value per tile and colour channel,
    /// spread from seeds (the open sky, torches, lava, glowing ore) to the eight
    /// neighbours. A tile passes on the share of its light its own medium keeps (air
    /// most, rock little, water more blue than red), that share to the power of the square
    /// root of two for a diagonal step, so a lone light is round and not a diamond; a
    /// diagonal step between two solid tiles is shut. A grid is row major with y up: cell
    /// (x, y) is index y * width + x. tools/tiles_v2/tv2_light.py does the same sums.
    /// </summary>
    public static class TileLight
    {
        /// <summary>The most sweeps of the map a spread makes; it stops as soon as one changes nothing.</summary>
        public const int MaxPasses = 32;

        private const float Diagonal = 1.41421356f;
        private const float Change = 1e-5f;

        /// <summary>
        /// Lights each column with daylight from the top down to its first cell that shades
        /// the sky (a solid tile, a wall, a liquid): the open sky. Keeps the brighter value
        /// where a seed is already set, and marks the lit cells in `open` when it is given.
        /// </summary>
        public static void SeedSky(Color[] seeds, bool[] shades, int width, int height, Color daylight, bool[] open)
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = height - 1; y >= 0; y--)
                {
                    var i = y * width + x;
                    if (shades[i])
                    {
                        break;
                    }
                    var seed = seeds[i];
                    seeds[i] = new Color(
                        Mathf.Max(seed.r, daylight.r),
                        Mathf.Max(seed.g, daylight.g),
                        Mathf.Max(seed.b, daylight.b),
                        1f
                    );
                    if (open != null)
                    {
                        open[i] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Spreads the seeds until nothing changes: every tile ends with the brightest of its
        /// own seed and of each neighbour's light times the share that neighbour keeps
        /// (`keep`, per channel). `solid` shuts the diagonal steps. Returns a new grid.
        /// </summary>
        public static Color[] Spread(Color[] seeds, Color[] keep, bool[] solid, int width, int height)
        {
            var light = (Color[])seeds.Clone();
            var slant = new Color[keep.Length];
            for (var i = 0; i < keep.Length; i++)
            {
                var k = keep[i];
                slant[i] = new Color(Mathf.Pow(k.r, Diagonal), Mathf.Pow(k.g, Diagonal), Mathf.Pow(k.b, Diagonal), 1f);
            }
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var changed = false;
                // Up the map from the bottom left: light from the west, the south and the two corners below.
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var i = y * width + x;
                        if (x > 0)
                        {
                            changed |= Pull(light, keep, i, i - 1);
                        }
                        if (y == 0)
                        {
                            continue;
                        }
                        changed |= Pull(light, keep, i, i - width);
                        if (x > 0 && !(solid[i - 1] && solid[i - width]))
                        {
                            changed |= Pull(light, slant, i, i - width - 1);
                        }
                        if (x < width - 1 && !(solid[i + 1] && solid[i - width]))
                        {
                            changed |= Pull(light, slant, i, i - width + 1);
                        }
                    }
                }
                // And back down from the top right.
                for (var y = height - 1; y >= 0; y--)
                {
                    for (var x = width - 1; x >= 0; x--)
                    {
                        var i = y * width + x;
                        if (x < width - 1)
                        {
                            changed |= Pull(light, keep, i, i + 1);
                        }
                        if (y == height - 1)
                        {
                            continue;
                        }
                        changed |= Pull(light, keep, i, i + width);
                        if (x < width - 1 && !(solid[i + 1] && solid[i + width]))
                        {
                            changed |= Pull(light, slant, i, i + width + 1);
                        }
                        if (x > 0 && !(solid[i - 1] && solid[i + width]))
                        {
                            changed |= Pull(light, slant, i, i + width - 1);
                        }
                    }
                }
                if (!changed)
                {
                    break;
                }
            }
            return light;
        }

        /// <summary>The light of both halves: the sky's under a daylight colour, or the sources' where they are brighter.</summary>
        public static void Combine(Color[] sky, Color[] sources, Color daylight, Color[] into)
        {
            for (var i = 0; i < into.Length; i++)
            {
                var s = sky[i];
                var o = sources[i];
                into[i] = new Color(
                    Mathf.Max(s.r * daylight.r, o.r),
                    Mathf.Max(s.g * daylight.g, o.g),
                    Mathf.Max(s.b * daylight.b, o.b),
                    1f
                );
            }
        }

        // Takes into `to` what `from` passes on, where that is brighter; says whether it was.
        private static bool Pull(Color[] light, Color[] share, int to, int from)
        {
            var a = light[to];
            var b = light[from];
            var k = share[from];
            var r = b.r * k.r;
            var g = b.g * k.g;
            var bl = b.b * k.b;
            if (r <= a.r + Change && g <= a.g + Change && bl <= a.b + Change)
            {
                return false;
            }
            light[to] = new Color(Mathf.Max(a.r, r), Mathf.Max(a.g, g), Mathf.Max(a.b, bl), 1f);
            return true;
        }
    }
}
