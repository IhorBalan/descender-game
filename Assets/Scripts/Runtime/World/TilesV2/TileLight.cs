using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The Tiles V2 light: one value per tile, spread from the sky and from light sources
    /// through the map, losing a share at every tile it enters, most in solid ground. A
    /// wall behind a tile shades it from the sky but lets light through like air. A grid
    /// is row major with y up: cell (x, y) is index y * width + x.
    /// </summary>
    public static class TileLight
    {
        /// <summary>How many times the four sweeps run: enough for light to turn a corner twice.</summary>
        public const int Passes = 3;

        /// <summary>How much light a tile keeps when it enters each kind of cell.</summary>
        public readonly struct Decay
        {
            public readonly float Air;
            public readonly float Solid;

            public Decay(float air, float solid)
            {
                Air = air;
                Solid = solid;
            }
        }

        /// <summary>
        /// Lights each column with daylight from the top down to its first solid tile or
        /// wall: the open sky. Adds to the seeds, keeping the brighter value.
        /// </summary>
        public static void SeedSky(Color[] seeds, bool[] solid, bool[] wall, int width, int height, Color daylight)
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = height - 1; y >= 0; y--)
                {
                    var i = y * width + x;
                    if (solid[i] || wall[i])
                    {
                        break;
                    }
                    seeds[i] = Max(seeds[i], daylight);
                }
            }
        }

        /// <summary>
        /// Spreads the seeds: each tile ends with the brightest of its own seed and of its
        /// neighbours' light times the share the tile keeps. Returns a new grid.
        /// </summary>
        public static Color[] Spread(Color[] seeds, bool[] solid, int width, int height, Decay decay)
        {
            var light = (Color[])seeds.Clone();
            var keep = new float[light.Length];
            for (var i = 0; i < keep.Length; i++)
            {
                keep[i] = solid[i] ? decay.Solid : decay.Air;
            }
            for (var pass = 0; pass < Passes; pass++)
            {
                for (var y = 0; y < height; y++)
                {
                    var row = y * width;
                    for (var x = 1; x < width; x++)
                    {
                        Pull(light, keep, row + x, row + x - 1);
                    }
                    for (var x = width - 2; x >= 0; x--)
                    {
                        Pull(light, keep, row + x, row + x + 1);
                    }
                }
                for (var x = 0; x < width; x++)
                {
                    for (var y = 1; y < height; y++)
                    {
                        Pull(light, keep, y * width + x, (y - 1) * width + x);
                    }
                    for (var y = height - 2; y >= 0; y--)
                    {
                        Pull(light, keep, y * width + x, (y + 1) * width + x);
                    }
                }
            }
            return light;
        }

        private static void Pull(Color[] light, float[] keep, int to, int from)
        {
            var k = keep[to];
            var a = light[to];
            var b = light[from];
            light[to] = new Color(Mathf.Max(a.r, b.r * k), Mathf.Max(a.g, b.g * k), Mathf.Max(a.b, b.b * k), 1f);
        }

        private static Color Max(Color a, Color b) =>
            new Color(Mathf.Max(a.r, b.r), Mathf.Max(a.g, b.g), Mathf.Max(a.b, b.b), 1f);
    }
}
