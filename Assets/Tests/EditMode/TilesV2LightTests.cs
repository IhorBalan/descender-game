using System.Collections.Generic;
using Descender.EditorTools.TilesV2;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.Tests
{
    /// <summary>
    /// The Tiles V2 light (docs/art/tiles-v2.md): daylight falls down every column to what
    /// shades it, a tile passes on the share its own medium keeps, a lone light is round, a
    /// diagonal between two solid tiles is shut, water keeps blue longer than red, the
    /// daylight's colour changes without spreading again, and a real map of tilemaps is lit
    /// by its sky, its torch, its lava and its ore.
    /// </summary>
    public class TilesV2LightTests
    {
        private const float Air = 0.9f;
        private const float Rock = 0.5f;

        private readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in made)
            {
                Object.DestroyImmediate(item);
            }
            made.Clear();
        }

        [Test]
        public void DaylightFillsEachColumnDownToWhatShadesIt()
        {
            var grid = new Rows("...", "#.w", "#..", "###");
            var seeds = new Color[grid.Count];
            var open = new bool[grid.Count];
            TileLight.SeedSky(seeds, grid.Shades, grid.Width, grid.Height, Color.white, open);
            // Column 0 stops on its tile, column 1 is open to the floor, column 2 stops on the wall.
            Assert.That(seeds[grid.At(0, 3)].r, Is.EqualTo(1f));
            Assert.That(seeds[grid.At(0, 2)].r, Is.EqualTo(0f));
            Assert.That(seeds[grid.At(1, 1)].r, Is.EqualTo(1f));
            Assert.That(open[grid.At(1, 1)], Is.True);
            Assert.That(seeds[grid.At(2, 2)].r, Is.EqualTo(0f));
            Assert.That(seeds[grid.At(2, 1)].r, Is.EqualTo(0f), "light under a wall is spread, not sky");
            Assert.That(open[grid.At(2, 1)], Is.False);
        }

        [Test]
        public void ATilePassesOnTheShareItsOwnMediumKeeps()
        {
            var grid = new Rows(".....", "#####", "#####");
            var seeds = new Color[grid.Count];
            seeds[grid.At(0, 2)] = Color.white;
            var light = TileLight.Spread(seeds, grid.Keep(Air, Rock), grid.Solid, grid.Width, grid.Height);
            Assert.That(light[grid.At(4, 2)].r, Is.EqualTo(Mathf.Pow(Air, 4f)).Within(1e-5f), "four tiles of air");
            // The rock right under the light is lit by the air over it: the surface of the ground is bright.
            Assert.That(light[grid.At(0, 1)].r, Is.EqualTo(Air).Within(1e-5f), "into the first tile of rock");
            Assert.That(light[grid.At(0, 0)].r, Is.EqualTo(Air * Rock).Within(1e-5f), "and it dies in the next");
        }

        [Test]
        public void ALoneLightIsRoundNotADiamond()
        {
            var grid = new Rows(".....", ".....", ".....", ".....", ".....");
            var seeds = new Color[grid.Count];
            seeds[grid.At(2, 2)] = Color.white;
            var light = TileLight.Spread(seeds, grid.Keep(Air, Rock), grid.Solid, grid.Width, grid.Height);
            var straight = light[grid.At(4, 2)].r;
            var corner = light[grid.At(3, 3)].r;
            Assert.That(
                corner,
                Is.EqualTo(Mathf.Pow(Air, Mathf.Sqrt(2f))).Within(1e-5f),
                "a diagonal step is its true length"
            );
            Assert.That(corner, Is.GreaterThan(straight), "the corner at 1.41 tiles is brighter than the side at 2");
            Assert.That(light[grid.At(4, 3)].r, Is.EqualTo(Mathf.Pow(Air, 1f + Mathf.Sqrt(2f))).Within(1e-5f));
        }

        [Test]
        public void LightDoesNotSqueezeBetweenTwoSolidTiles()
        {
            var shut = new Rows("#.", ".#");
            var seeds = new Color[shut.Count];
            seeds[shut.At(0, 0)] = Color.white;
            var light = TileLight.Spread(seeds, shut.Keep(Air, Rock), shut.Solid, shut.Width, shut.Height);
            // The only way is through one of the tiles: the air's share to enter it, the rock's to leave it.
            Assert.That(light[shut.At(1, 1)].r, Is.EqualTo(Air * Rock).Within(1e-5f));

            var open = new Rows("#.", "..");
            seeds = new Color[open.Count];
            seeds[open.At(0, 0)] = Color.white;
            light = TileLight.Spread(seeds, open.Keep(Air, Rock), open.Solid, open.Width, open.Height);
            Assert.That(
                light[open.At(1, 1)].r,
                Is.EqualTo(Mathf.Pow(Air, Mathf.Sqrt(2f))).Within(1e-5f),
                "round a corner it goes"
            );
        }

        [Test]
        public void LightFollowsATunnelAroundTheRock()
        {
            var grid = new Rows(".####", ".####", ".....");
            var seeds = new Color[grid.Count];
            seeds[grid.At(0, 2)] = new Color(1f, 0.5f, 0.25f);
            var light = TileLight.Spread(seeds, grid.Keep(Air, Rock), grid.Solid, grid.Width, grid.Height);
            // Down one, across the inside of the bend, along three: 4 + sqrt 2 tiles of air, keeping its colour.
            var end = light[grid.At(4, 0)];
            Assert.That(end.r, Is.EqualTo(Mathf.Pow(Air, 4f + Mathf.Sqrt(2f))).Within(1e-5f));
            Assert.That(end.b / end.r, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(light[grid.At(4, 2)].r, Is.LessThan(end.r), "through the rock is darker than round it");
        }

        [Test]
        public void WaterKeepsBlueLongerThanRed()
        {
            var grid = new Rows(".", "~", "~", "~", "~");
            var keep = grid.Keep(Air, Rock);
            var water = new Color(0.7f, 0.85f, 0.95f, 1f);
            for (var y = 0; y < 4; y++)
            {
                keep[grid.At(0, y)] = water;
            }
            var seeds = new Color[grid.Count];
            seeds[grid.At(0, 4)] = Color.white;
            var light = TileLight.Spread(seeds, keep, grid.Solid, grid.Width, grid.Height);
            var deep = light[grid.At(0, 0)];
            Assert.That(deep.r, Is.EqualTo(Air * Mathf.Pow(0.7f, 3f)).Within(1e-5f));
            Assert.That(deep.b, Is.EqualTo(Air * Mathf.Pow(0.95f, 3f)).Within(1e-5f));
            Assert.That(deep.b, Is.GreaterThan(deep.r * 2f), "the deep is blue");
        }

        [Test]
        public void TheDaylightChangesWithoutSpreadingAgain()
        {
            var sky = new[] { new Color(1f, 1f, 1f), new Color(0.5f, 0.5f, 0.5f), new Color(0f, 0f, 0f) };
            var sources = new[] { new Color(0f, 0f, 0f), new Color(0.4f, 0.2f, 0.1f), new Color(0.9f, 0.5f, 0.2f) };
            var light = new Color[3];
            TileLight.Combine(sky, sources, Color.white, light);
            Assert.That(light[0], Is.EqualTo(new Color(1f, 1f, 1f, 1f)));
            Assert.That(light[1], Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)), "the brighter of the two halves");
            Assert.That(light[2], Is.EqualTo(new Color(0.9f, 0.5f, 0.2f, 1f)));
            TileLight.Combine(sky, sources, new Color(0.2f, 0.25f, 0.5f), light);
            Assert.That(light[0], Is.EqualTo(new Color(0.2f, 0.25f, 0.5f, 1f)), "night");
            Assert.That(light[1], Is.EqualTo(new Color(0.4f, 0.2f, 0.25f, 1f)), "a torch shows more at night");
        }

        [Test]
        public void AMapIsLitByItsSkyItsTorchItsLavaAndItsOre()
        {
            var root = new GameObject("Tiles V2 light test");
            made.Add(root);
            root.AddComponent<Grid>();
            var terrain = Layer(root, "Terrain");
            var walls = Layer(root, "Walls");
            var lavaMap = Layer(root, "Liquid lava");
            var stone = Tile<TerrainTile>("stone");
            var ore = Tile<TerrainTile>("copper_ore");
            var lava = Tile<LiquidTile>("lava");
            // A slab of stone 30 wide and 12 deep, a walled room cut in it, a lava cell and an ore tile.
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 12; y++)
                {
                    var room = x >= 4 && x <= 9 && y >= 4 && y <= 6;
                    if (room)
                    {
                        walls.SetTile(new Vector3Int(x, y, 0), Tile<TerrainTile>("stone_wall"));
                    }
                    else
                    {
                        terrain.SetTile(new Vector3Int(x, y, 0), x == 26 && y == 3 ? ore : stone);
                    }
                }
            }
            terrain.SetTile(new Vector3Int(18, 5, 0), null);
            lavaMap.SetTile(new Vector3Int(18, 5, 0), lava);
            var torches = new GameObject("Torches").transform;
            torches.SetParent(root.transform, false);
            var torch = new GameObject("Torch");
            torch.transform.SetParent(torches, false);
            torch.transform.position = new Vector3(6.5f, 5.5f, 0f);
            torch.AddComponent<SpriteRenderer>();
            var flame = torch.AddComponent<TorchFlame>();
            flame.Configure(new Sprite[0], null, 0.3f, new Color(1f, 0.7f, 0.4f), 1.5f);

            var holder = new GameObject("Tile Light");
            holder.transform.SetParent(root.transform, false);
            holder.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            var map = holder.AddComponent<TileLightMap>();
            map.Configure(terrain, walls, new[] { lavaMap }, torches, 0.91f, 0.56f);
            map.Refresh();

            Assert.That(map.Bounds.size.y, Is.EqualTo(12 + TileField.SkyRows), "the light reaches over the top tile");
            Assert.That(map.At(new Vector3Int(3, 12, 0)), Is.EqualTo(Color.white), "open sky");
            Assert.That(map.At(new Vector3Int(3, 11, 0)).r, Is.EqualTo(0.91f).Within(1e-4f), "the top tile is bright");
            Assert.That(map.At(new Vector3Int(3, 10, 0)).r, Is.EqualTo(0.91f * 0.56f).Within(1e-4f));
            Assert.That(map.At(new Vector3Int(14, 0, 0)).maxColorComponent, Is.LessThan(0.05f), "deep rock is dark");
            Assert.That(map.At(new Vector3Int(6, 5, 0)).r, Is.EqualTo(1.5f).Within(1e-4f), "the torch lights its tile");
            Assert.That(
                map.At(new Vector3Int(9, 5, 0)).r,
                Is.EqualTo(1.5f * Mathf.Pow(0.91f, 3f)).Within(1e-3f),
                "and its room"
            );
            Assert.That(map.At(new Vector3Int(18, 5, 0)).r, Is.EqualTo(lava.Emits.r).Within(1e-4f), "lava glows");
            Assert.That(map.At(new Vector3Int(19, 5, 0)).r, Is.EqualTo(lava.Emits.r * lava.Keeps.r).Within(1e-3f));
            Assert.That(
                map.At(new Vector3Int(26, 3, 0)).r,
                Is.EqualTo(ore.Emits.r).Within(1e-4f),
                "ore glints in the dark"
            );
            Assert.That(map.At(new Vector3Int(100, 100, 0)), Is.EqualTo(Color.black), "nothing outside the map");

            // Night: the sky's half dims, the torch's does not.
            map.Daylight = new Color(0.2f, 0.2f, 0.4f);
            Assert.That(map.At(new Vector3Int(3, 12, 0)).b, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(map.At(new Vector3Int(6, 5, 0)).r, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(map.Light[0].a, Is.EqualTo(1f), "the light's texture stays opaque");
        }

        private static Tilemap Layer(GameObject root, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(root.transform, false);
            return holder.AddComponent<Tilemap>();
        }

        private static T Tile<T>(string key)
            where T : TileBase
        {
            var tile = AssetDatabase.LoadAssetAtPath<T>(TilesV2Manifest.TilePath(key));
            Assert.That(tile, Is.Not.Null, key);
            return tile;
        }

        // A grid for the light's sums from rows drawn top first: # solid, w wall, ~ liquid, . air.
        private sealed class Rows
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool[] Solid;
            public readonly bool[] Shades;

            public Rows(params string[] rows)
            {
                Width = rows[0].Length;
                Height = rows.Length;
                Solid = new bool[Width * Height];
                Shades = new bool[Width * Height];
                for (var r = 0; r < Height; r++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var i = (Height - 1 - r) * Width + x;
                        Solid[i] = rows[r][x] == '#';
                        Shades[i] = rows[r][x] != '.';
                    }
                }
            }

            public int Count => Width * Height;

            public int At(int x, int y) => y * Width + x;

            public Color[] Keep(float air, float rock)
            {
                var keep = new Color[Count];
                for (var i = 0; i < keep.Length; i++)
                {
                    var share = Solid[i] ? rock : air;
                    keep[i] = new Color(share, share, share, 1f);
                }
                return keep;
            }
        }
    }
}
