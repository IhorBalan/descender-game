using System.Collections.Generic;
using Descender.EditorTools.TilesV2;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Descender.Tests
{
    /// <summary>
    /// The Tiles V2 standard (docs/art/tiles-v2.md), its tiles and what is laid by rule: the
    /// generator's frames are the 47 the Rule Tiles expect, a painted block picks the right
    /// frames, joined materials meet with no edge; blends, shade, decor and glints land
    /// where the rules say; liquids show their surface on top; the tile set holds
    /// everything a world needs; and the two sample scenes build the whole map and stay out
    /// of the game's build. The light is in TilesV2LightTests.
    /// </summary>
    public class TilesV2Tests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private TilesV2Manifest manifest;
        private TilesV2Set set;

        [SetUp]
        public void SetUp()
        {
            manifest = TilesV2Manifest.Load();
            set = TilesV2Builder.LoadSet();
            Assert.That(set, Is.Not.Null, "run Descender > Tiles V2 > Build tiles");
        }

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
        public void TheGeneratorsFramesAreTheFortySevenBlobFrames()
        {
            var frames = TilesV2Masks.AllFrames();
            Assert.That(frames.Count, Is.EqualTo(47));
            Assert.That(manifest.frames, Is.EqualTo(frames.ToArray()), "tiles_v2.json and TilesV2Masks disagree");
            Assert.That(manifest.tile, Is.EqualTo(16));
            Assert.That(manifest.ppu, Is.EqualTo(16), "one tile must be one unit");
        }

        [Test]
        public void EveryMaterialHasARulePerFrameWithAllItsVariants()
        {
            foreach (var material in manifest.materials)
            {
                var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(TilesV2Manifest.TilePath(material.key));
                Assert.That(tile, Is.Not.Null, material.key + " has no tile; run Descender > Tiles V2 > Build tiles");
                Assert.That(tile.JoinGroup, Is.EqualTo(material.group));
                Assert.That(tile.BaseKey, Is.EqualTo(material.key));
                Assert.That(tile.IsSlope, Is.False);
                Assert.That(tile.m_TilingRules.Count, Is.EqualTo(manifest.frames.Length), material.key);
                var expected = material.IsWall ? Tile.ColliderType.None : Tile.ColliderType.Grid;
                foreach (var rule in tile.m_TilingRules)
                {
                    Assert.That(rule.m_Sprites.Length, Is.EqualTo(manifest.variants), material.key);
                    Assert.That(rule.m_Sprites, Has.None.Null, material.key);
                    Assert.That(rule.m_ColliderType, Is.EqualTo(expected), material.key);
                }
                foreach (var slope in manifest.SlopesOf(material))
                {
                    var slopeTile = AssetDatabase.LoadAssetAtPath<TerrainTile>(
                        TilesV2Manifest.SlopePath(material.key, slope)
                    );
                    Assert.That(slopeTile, Is.Not.Null, material.key + " " + slope);
                    Assert.That(slopeTile.m_TilingRules[0].m_ColliderType, Is.EqualTo(Tile.ColliderType.Sprite));
                    Assert.That(slopeTile.Joins(tile), Is.True, "a slope must join its ground");
                    Assert.That(slopeTile.IsSlope, Is.True);
                    Assert.That(slopeTile.BaseKey, Is.EqualTo(material.key), "a slope is of its material");
                }
            }
        }

        [Test]
        public void ABlockOfDirtPicksItsEdgeCornerAndMiddleFrames()
        {
            var map = NewTilemap("Terrain");
            var dirt = TileOf("dirt");
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    map.SetTile(new Vector3Int(x, y, 0), dirt);
                }
            }
            map.SetTile(new Vector3Int(6, 0, 0), dirt);
            map.RefreshAllTiles();

            AssertFrame(map, 1, 1, "dirt", 255);
            // Top-left corner: solid to the east, south and south-east.
            AssertFrame(map, 0, 2, "dirt", TilesV2Masks.E | TilesV2Masks.SE | TilesV2Masks.S);
            // Middle of the top edge.
            AssertFrame(
                map,
                1,
                2,
                "dirt",
                TilesV2Masks.E | TilesV2Masks.SE | TilesV2Masks.S | TilesV2Masks.SW | TilesV2Masks.W
            );
            AssertFrame(map, 6, 0, "dirt", 0);
        }

        [Test]
        public void AnInnerCornerShowsWhereOnlyTheDiagonalIsAir()
        {
            var map = NewTilemap("Terrain");
            var stone = TileOf("stone");
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    if (!(x == 2 && y == 2))
                    {
                        map.SetTile(new Vector3Int(x, y, 0), stone);
                    }
                }
            }
            map.RefreshAllTiles();
            AssertFrame(map, 1, 1, "stone", 255 & ~TilesV2Masks.NE);
        }

        [Test]
        public void JoinedMaterialsMeetWithNoEdgeAndWallsDoNotJoinTerrain()
        {
            var map = NewTilemap("Terrain");
            map.SetTile(new Vector3Int(0, 0, 0), TileOf("dirt"));
            map.SetTile(new Vector3Int(1, 0, 0), TileOf("stone"));
            map.SetTile(new Vector3Int(2, 0, 0), TileOf("dirt_wall"));
            map.RefreshAllTiles();
            AssertFrame(map, 0, 0, "dirt", TilesV2Masks.E);
            AssertFrame(map, 1, 0, "stone", TilesV2Masks.W);
            AssertFrame(map, 2, 0, "dirt_wall", 0);
        }

        [Test]
        public void VariantsAndTheScatterHashAreTheGeneratorsSums()
        {
            var counts = new int[3];
            for (var x = -20; x < 20; x++)
            {
                for (var y = -20; y < 20; y++)
                {
                    counts[TerrainTile.Variant(new Vector3Int(x, y, 0), 3)]++;
                }
            }
            foreach (var count in counts)
            {
                Assert.That(count, Is.InRange(1600 / 4, 1600 / 2), string.Join(", ", counts));
            }
            Assert.That(TerrainTile.Variant(new Vector3Int(5, 7, 0), 1), Is.EqualTo(0));
            // The numbers tools/tiles_v2/tv2_base.py gives (variant_of, cell_hash): the preview
            // and Unity pick the same variant and scatter the same decor.
            Assert.That(TerrainTile.Variant(new Vector3Int(3, 5, 0), 3), Is.EqualTo(1));
            Assert.That(TerrainTile.Variant(new Vector3Int(-7, 2, 0), 3), Is.EqualTo(1));
            Assert.That(TilesV2Painter.Hash(3, 5, 1), Is.EqualTo(418488659));
            Assert.That(TilesV2Painter.Hash(-3, 5, 2), Is.EqualTo(2118870605));
        }

        [Test]
        public void BlendsLandOnStoneSidesThatTouchDirtOrGrass()
        {
            var grid = NewGrid();
            var terrain = NewTilemap(grid, "Terrain");
            var blend = NewTilemap(grid, "Blend");
            terrain.SetTile(new Vector3Int(0, 0, 0), TileOf("stone"));
            terrain.SetTile(new Vector3Int(0, 1, 0), TileOf("dirt"));
            terrain.SetTile(new Vector3Int(1, 0, 0), TileOf("grass"));
            terrain.SetTile(new Vector3Int(5, 0, 0), TileOf("stone"));
            var painted = TilesV2Painter.Blends(TileField.Read(terrain, null, null), set, blend);
            Assert.That(painted, Is.EqualTo(1));
            var expected = AssetDatabase.LoadAssetAtPath<TileBase>(TilesV2Manifest.BlendPath("dirt_on_stone", 1 | 2));
            Assert.That(expected, Is.Not.Null);
            Assert.That(blend.GetTile(new Vector3Int(0, 0, 0)), Is.SameAs(expected));
            Assert.That(blend.GetTile(new Vector3Int(5, 0, 0)), Is.Null);
        }

        [Test]
        public void DirtBlendsOverSandAndClayTooOneOverlayACell()
        {
            var grid = NewGrid();
            var terrain = NewTilemap(grid, "Terrain");
            var blend = NewTilemap(grid, "Blend");
            terrain.SetTile(new Vector3Int(0, 0, 0), TileOf("sand"));
            terrain.SetTile(new Vector3Int(1, 0, 0), TileOf("dirt"));
            terrain.SetTile(new Vector3Int(2, 0, 0), TileOf("clay"));
            terrain.SetTile(new Vector3Int(0, 5, 0), TileOf("copper_ore"));
            terrain.SetTile(new Vector3Int(1, 5, 0), TileOf("stone"));
            Assert.That(TilesV2Painter.Blends(TileField.Read(terrain, null, null), set, blend), Is.EqualTo(2));
            Assert.That(
                blend.GetTile(new Vector3Int(0, 0, 0)),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TileBase>(TilesV2Manifest.BlendPath("dirt_on_sand", 2)))
            );
            Assert.That(
                blend.GetTile(new Vector3Int(2, 0, 0)),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TileBase>(TilesV2Manifest.BlendPath("dirt_on_clay", 8)))
            );
        }

        [Test]
        public void AShadeCornerCountsOnlyBetweenTwoOpenSides()
        {
            Assert.That(TilesV2Painter.ShadeMask(TilesV2Masks.N), Is.EqualTo(TilesV2Masks.N));
            Assert.That(TilesV2Painter.ShadeMask(TilesV2Masks.NE), Is.EqualTo(TilesV2Masks.NE));
            Assert.That(
                TilesV2Painter.ShadeMask(TilesV2Masks.N | TilesV2Masks.NE),
                Is.EqualTo(TilesV2Masks.N),
                "the side's shade covers the corner"
            );
            Assert.That(
                TilesV2Painter.ShadeMask(255),
                Is.EqualTo(TilesV2Masks.N | TilesV2Masks.E | TilesV2Masks.S | TilesV2Masks.W)
            );
            var masks = new SortedSet<int>();
            for (var around = 0; around < 256; around++)
            {
                masks.Add(TilesV2Painter.ShadeMask(around));
            }
            masks.Remove(0);
            Assert.That(masks.Count, Is.EqualTo(46));
            Assert.That(
                manifest.shade.masks,
                Is.EqualTo(new List<int>(masks).ToArray()),
                "the generator drew other masks"
            );
            foreach (var mask in masks)
            {
                Assert.That(set.ShadeFor(mask), Is.Not.Null, "no shade tile for mask " + mask);
            }
        }

        [Test]
        public void TheGroundShadesTheWallBesideItAndBehindItsOpenEdges()
        {
            var grid = NewGrid();
            var terrain = NewTilemap(grid, "Terrain");
            var walls = NewTilemap(grid, "Walls");
            var shade = NewTilemap(grid, "Shade");
            terrain.SetTile(new Vector3Int(1, 2, 0), TileOf("stone"));
            var wall = TileOf("stone_wall");
            walls.SetTile(new Vector3Int(1, 1, 0), wall);
            walls.SetTile(new Vector3Int(1, 2, 0), wall);
            walls.SetTile(new Vector3Int(2, 1, 0), wall);
            walls.SetTile(new Vector3Int(5, 5, 0), wall);
            var painted = TilesV2Painter.Shade(TileField.Read(terrain, walls, null), set, shade);
            Assert.That(painted, Is.EqualTo(3));
            Assert.That(
                shade.GetTile(new Vector3Int(1, 1, 0)),
                Is.SameAs(set.ShadeFor(TilesV2Masks.N)),
                "under the tile"
            );
            Assert.That(
                shade.GetTile(new Vector3Int(2, 1, 0)),
                Is.SameAs(set.ShadeFor(TilesV2Masks.NW)),
                "off its corner"
            );
            Assert.That(shade.GetTile(new Vector3Int(1, 2, 0)), Is.SameAs(set.shadeFull), "behind the tile itself");
            Assert.That(shade.GetTile(new Vector3Int(5, 5, 0)), Is.Null, "far from any ground");
        }

        [Test]
        public void DecorGrowsOnOpenFacesByItsRulesTheSameEveryTime()
        {
            var grid = NewGrid();
            var terrain = NewTilemap(grid, "Terrain");
            var decor = NewTilemap(grid, "Decor");
            var grass = TileOf("grass");
            var stone = TileOf("stone");
            // A meadow under the sky from x = 0, and well to its right a low cave in stone.
            for (var x = 0; x < 40; x++)
            {
                terrain.SetTile(new Vector3Int(x, 0, 0), grass);
            }
            for (var x = 60; x < 140; x++)
            {
                terrain.SetTile(new Vector3Int(x, 0, 0), stone);
                terrain.SetTile(new Vector3Int(x, 4, 0), stone);
            }
            var taken = new HashSet<Vector3Int> { new Vector3Int(3, 1, 0) };
            var field = TileField.Read(terrain, null, null);
            var count = TilesV2Painter.Decor(field, set, decor, taken);
            Assert.That(count, Is.GreaterThan(30));

            var meadow = TilesOf("flower", "bush", "grass_tall", "tuft");
            var floor = TilesOf("mushroom", "stalagmite", "pebble");
            var roof = TilesOf("stalactite");
            var onGrass = 0;
            for (var x = 0; x < 40; x++)
            {
                var tile = decor.GetTile(new Vector3Int(x, 1, 0));
                if (tile != null)
                {
                    onGrass++;
                    Assert.That(meadow.Contains(tile), Is.True, $"{tile.name} on grass at {x}");
                }
            }
            Assert.That(onGrass, Is.InRange(16, 38), "about three quarters of a meadow is dressed");
            Assert.That(decor.GetTile(new Vector3Int(3, 1, 0)), Is.Null, "a taken cell stays clear");
            var inCave = 0;
            for (var x = 60; x < 140; x++)
            {
                var low = decor.GetTile(new Vector3Int(x, 1, 0));
                var high = decor.GetTile(new Vector3Int(x, 3, 0));
                Assert.That(decor.GetTile(new Vector3Int(x, 2, 0)), Is.Null, "nothing floats");
                Assert.That(
                    low == null || floor.Contains(low),
                    Is.True,
                    $"{(low != null ? low.name : "")} on a cave floor"
                );
                Assert.That(
                    high == null || roof.Contains(high),
                    Is.True,
                    $"{(high != null ? high.name : "")} under a cave roof"
                );
                inCave += (low != null ? 1 : 0) + (high != null ? 1 : 0);
            }
            Assert.That(inCave, Is.GreaterThan(10));

            var first = Snapshot(decor);
            TilesV2Painter.Decor(field, set, decor, taken);
            Assert.That(Snapshot(decor), Is.EqualTo(first), "the scatter is a function of the cells");
        }

        [Test]
        public void ALiquidShowsItsSurfaceOnTopAndItsBodyUnder()
        {
            var water = AssetDatabase.LoadAssetAtPath<LiquidTile>(TilesV2Manifest.TilePath("water"));
            Assert.That(water, Is.Not.Null);
            Assert.That(water.FrameCount, Is.GreaterThan(1), "it moves");
            var map = NewTilemap("Liquid water");
            map.SetTile(new Vector3Int(0, 0, 0), water);
            map.SetTile(new Vector3Int(0, 1, 0), water);
            map.RefreshAllTiles();
            Assert.That(map.GetSprite(new Vector3Int(0, 1, 0)).name, Is.EqualTo("tv2_water_surface_0"));
            Assert.That(map.GetSprite(new Vector3Int(0, 0, 0)).name, Is.EqualTo("tv2_water_body_0"));
            Assert.That(water.Keeps.b, Is.GreaterThan(water.Keeps.r), "water keeps blue longer than red");
            var lava = AssetDatabase.LoadAssetAtPath<LiquidTile>(TilesV2Manifest.TilePath("lava"));
            Assert.That(lava.Emits.r, Is.GreaterThan(0.5f), "lava gives light");
            Assert.That(set.LiquidOf("lava").lit, Is.False, "and is drawn unlit");
            Assert.That(set.LiquidOf("lava").hazard, Is.GreaterThan(0));
            Assert.That(set.LiquidOf("water").swim, Is.True);
        }

        [Test]
        public void ALiquidsStretchesRunFromItsTrueSurface()
        {
            var water = AssetDatabase.LoadAssetAtPath<LiquidTile>(TilesV2Manifest.TilePath("water"));
            var map = NewTilemap("Liquid water");
            // A pool with a shallow cell at each end.
            foreach (var (x, bottom) in new[] { (0, 1), (1, 0), (2, 0), (3, 1) })
            {
                for (var y = bottom; y <= 2; y++)
                {
                    map.SetTile(new Vector3Int(x, y, 0), water);
                }
            }
            var field = TileField.Read(null, null, new[] { map });
            var stretches = TilesV2Zones.Stretches(field, water);
            Assert.That(
                stretches,
                Is.EquivalentTo(new[] { new Rect(0, 1, 1, 2), new Rect(1, 0, 2, 3), new Rect(3, 1, 1, 2) })
            );
        }

        [Test]
        public void OreGlintsOnItsOwnBeat()
        {
            var glint = set.glint;
            Assert.That(glint, Is.Not.Null);
            Assert.That(glint.Variants, Is.EqualTo(manifest.variants));
            Assert.That(glint.LoopLength, Is.EqualTo(manifest.glint.loop.Length));
            var starts = new HashSet<int>();
            for (var x = 0; x < 12; x++)
            {
                var cell = new Vector3Int(x, 3, 0);
                starts.Add(glint.StartOf(cell));
                var sequence = glint.SequenceAt(cell);
                Assert.That(sequence.Length, Is.EqualTo(glint.LoopLength));
                var variant = TerrainTile.Variant(cell, glint.Variants);
                var lit = 0;
                foreach (var sprite in sequence)
                {
                    Assert.That(sprite, Is.Not.Null);
                    if (sprite.name != TilesV2Manifest.GlintNoneSprite)
                    {
                        lit++;
                        Assert.That(
                            sprite.name,
                            Does.StartWith($"tv2_glint_v{variant}_"),
                            "the ore tile's own variant"
                        );
                    }
                }
                Assert.That(lit, Is.InRange(1, sequence.Length / 2), "dark most of the time");
            }
            Assert.That(starts.Count, Is.GreaterThan(3), "a vein does not blink as one");

            var grid = NewGrid();
            var terrain = NewTilemap(grid, "Terrain");
            var glow = NewTilemap(grid, "Glow");
            terrain.SetTile(new Vector3Int(0, 0, 0), TileOf("copper_ore"));
            terrain.SetTile(new Vector3Int(1, 0, 0), TileOf("stone"));
            Assert.That(TilesV2Painter.Glints(TileField.Read(terrain, null, null), set, glow), Is.EqualTo(1));
            Assert.That(glow.GetTile(new Vector3Int(0, 0, 0)), Is.SameAs(glint));
            Assert.That(TileOf("copper_ore").Emits.r, Is.GreaterThan(0f), "ore gives a little light of its own");
        }

        [Test]
        public void SandTricklesOnlyFromAnOverhangAndFallsToTheFloor()
        {
            var terrain = NewTilemap("Terrain");
            var sand = TileOf("sand");
            var stone = TileOf("stone");
            for (var x = 0; x < 100; x++)
            {
                terrain.SetTile(new Vector3Int(x, 0, 0), stone);
                // The left half hangs five cells over the floor, the right half lies on it.
                terrain.SetTile(new Vector3Int(x, x < 50 ? 6 : 1, 0), sand);
            }
            var field = TileField.Read(terrain, null, null);
            var trickling = 0;
            for (var x = 0; x < 100; x++)
            {
                if (TilesV2Particles.Trickles(field, x, x < 50 ? 6 : 1, out var fall))
                {
                    trickling++;
                    Assert.That(x, Is.LessThan(50), "sand lying on the ground does not trickle");
                    Assert.That(fall, Is.EqualTo(5));
                }
            }
            Assert.That(trickling, Is.InRange(12, 34), "nearly half of an overhang");
        }

        [Test]
        public void ParallaxLayersFollowTheCameraByTheShareTheyDoNotScroll()
        {
            var anchor = new Vector2(10f, 4f);
            var view = new Vector2(10f, 6f);
            var moved = new Vector2(18f, 2f);
            var none = Vector2.zero;
            Assert.That(
                ParallaxLayer.PositionFor(anchor, view, moved, Vector2.zero, none, none),
                Is.EqualTo(new Vector2(18f, 0f))
            );
            Assert.That(ParallaxLayer.PositionFor(anchor, view, moved, Vector2.one, none, none), Is.EqualTo(anchor));
            Assert.That(
                ParallaxLayer.PositionFor(anchor, view, moved, new Vector2(0.5f, 1f), none, none),
                Is.EqualTo(new Vector2(14f, 4f)),
                "a ridge scrolls sideways and stands on the world's horizon"
            );
            var snapped = ParallaxLayer.PositionFor(
                anchor,
                view,
                new Vector2(10.01f, 6f),
                Vector2.one * 0.5f,
                none,
                none
            );
            Assert.That(snapped.x * 16f, Is.EqualTo(Mathf.Round(snapped.x * 16f)), "layers sit on whole art pixels");
            // A picture that repeats every 20 units never drifts more than half of that from the camera.
            for (var x = -300f; x < 300f; x += 37f)
            {
                var camera = new Vector2(x, 6f);
                var at = ParallaxLayer.PositionFor(
                    anchor,
                    view,
                    camera,
                    new Vector2(0.9f, 1f),
                    new Vector2(20f, 0f),
                    none
                );
                Assert.That(Mathf.Abs(at.x - x), Is.LessThanOrEqualTo(10.05f), "camera at " + x);
            }
        }

        [Test]
        public void TheTileSetHoldsEverythingAWorldIsBuiltFrom()
        {
            Assert.That(set.litMaterial, Is.Not.Null);
            Assert.That(set.unlitMaterial, Is.Not.Null);
            Assert.That(set.unlitMaterial.shader.name, Does.Contain("Unlit"));
            Assert.That(set.legend.Length, Is.EqualTo(manifest.legend.Length));
            foreach (var entry in manifest.legend)
            {
                Assert.That(set.TryGlyph(entry.character[0], out var glyph), Is.True, entry.character);
                var tiled =
                    glyph.layer == TilesV2Set.Layer.Terrain
                    || glyph.layer == TilesV2Set.Layer.Wall
                    || glyph.layer == TilesV2Set.Layer.Liquid;
                Assert.That(glyph.tile != null, Is.EqualTo(tiled), entry.character);
            }
            Assert.That(set.TryGlyph(TilesV2Map.Air, out _), Is.False);
            Assert.That(set.torchFrames.Length, Is.EqualTo(3));
            Assert.That(set.torchFrames, Has.None.Null);
            Assert.That(set.decor.Length, Is.EqualTo(manifest.decor.rules.Length));
            foreach (var rule in set.decor)
            {
                Assert.That(rule.last, Is.Not.Empty.And.None.Null, rule.key);
                Assert.That(rule.min, Is.InRange(1, TilesV2Painter.TallestDecor), rule.key);
                Assert.That(rule.max, Is.InRange(rule.min, TilesV2Painter.TallestDecor), rule.key);
                Assert.That(
                    rule.max,
                    Is.LessThanOrEqualTo(TileField.SkyRows),
                    "tall decor needs the field's rows of sky"
                );
            }
            Assert.That(set.backdrops.Length, Is.EqualTo(manifest.backdrops.Length));
            foreach (var layer in set.backdrops)
            {
                Assert.That(layer.sprite, Is.Not.Null, layer.key);
            }
            Assert.That(set.airKeeps, Is.GreaterThan(set.solidKeeps), "light dies faster in rock than in air");
            Assert.That(set.wallTint.maxColorComponent, Is.LessThan(1f), "a wall is further back than the ground");
        }

        [Test]
        public void TheHourMixesTheDaysKeysRoundTheClock()
        {
            Assert.That(set.day.Length, Is.GreaterThan(4));
            var noon = set.DayAt(12f);
            Assert.That(noon.light, Is.EqualTo(Color.white));
            Assert.That(noon.stars, Is.EqualTo(0f));
            var night = set.DayAt(0f);
            Assert.That(night.light.maxColorComponent, Is.LessThan(0.7f), "night is dim");
            Assert.That(night.light.b, Is.GreaterThan(night.light.r), "and blue");
            Assert.That(night.stars, Is.EqualTo(1f));
            Assert.That(set.DayAt(23.99f).light, Is.EqualTo(night.light), "the clock comes round");
            Assert.That(set.DayAt(36f).light, Is.EqualTo(noon.light), "an hour past 24 wraps");
            var a = set.day[2];
            var b = set.day[3];
            var between = set.DayAt((a.hour + b.hour) / 2f);
            Assert.That(between.light.r, Is.EqualTo((a.light.r + b.light.r) / 2f).Within(1e-4f));
            Assert.That(between.top.b, Is.EqualTo((a.top.b + b.top.b) / 2f).Within(1e-4f));

            Assert.That(set.SkyArc(12.5f, out var sun), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(sun, Is.True);
            Assert.That(
                set.SkyArc(0.5f, out sun),
                Is.EqualTo(0.5f).Within(1e-4f),
                "the moon is highest half way through the night"
            );
            Assert.That(sun, Is.False);
        }

        [Test]
        public void TheSkyIsBandsThatMeetInADither()
        {
            const int tall = 180;
            for (var x = 0; x < 8; x++)
            {
                Assert.That(Sky.Band(x, 0, tall, 7), Is.EqualTo(0));
                Assert.That(Sky.Band(x, tall - 1, tall, 7), Is.EqualTo(6));
            }
            var mixed = new HashSet<int>();
            // Half way between the first two bands a row shows both.
            var row = (tall - 1) / 12;
            for (var x = 0; x < 8; x++)
            {
                mixed.Add(Sky.Band(x, row, tall, 7));
                mixed.Add(Sky.Band(x, row + 1, tall, 7));
            }
            Assert.That(mixed, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void AMapIsTwoGridsAndItsProperties()
        {
            var map = TilesV2Map.Parse(
                "# a comment\n@surface 2\n@view cave 1.5 2\n@shot dusk cave 18.5\nG.\nDT\nSS\n\n..\ndd\nss\n"
            );
            Assert.That(map.Shots.Count, Is.EqualTo(1));
            Assert.That(map.Shots[0].Name, Is.EqualTo("dusk"));
            Assert.That(map.Shots[0].View, Is.EqualTo("cave"));
            Assert.That(map.Shots[0].Hour, Is.EqualTo(18.5f));
            Assert.That(map.Width, Is.EqualTo(2));
            Assert.That(map.Height, Is.EqualTo(3));
            Assert.That(map.Surface, Is.EqualTo(2));
            Assert.That(map.TerrainAt(0, 0), Is.EqualTo('G'));
            Assert.That(map.TerrainAt(1, 1), Is.EqualTo('T'));
            Assert.That(map.WallAt(1, 1), Is.EqualTo('d'));
            Assert.That(map.TerrainAt(5, 5), Is.EqualTo(TilesV2Map.Air), "air outside the map");
            Assert.That(map.CellOf(0, 0), Is.EqualTo(new Vector3Int(0, 2, 0)), "row 0 is the top");
            Assert.That(map.Views["cave"], Is.EqualTo(new Vector2(1.5f, 2f)));
            Assert.That(map.PointOf(map.Views["cave"]), Is.EqualTo(new Vector2(1.5f, 1f)));
            Assert.Throws<System.FormatException>(() => TilesV2Map.Parse("GG\nDD\n"), "a map needs its wall grid");
        }

        [Test]
        public void TheSampleSceneBuildsTheWholeMapAndSavesNoneOfIt()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(TilesV2Sample.MapPath);
            Assert.That(text, Is.Not.Null);
            var map = TilesV2Map.Parse(text.text);
            var ground = 0;
            var torches = 0;
            var liquid = 0;
            var start = Vector2.zero;
            for (var row = 0; row < map.Height; row++)
            {
                for (var column = 0; column < map.Width; column++)
                {
                    if (!set.TryGlyph(map.TerrainAt(column, row), out var glyph))
                    {
                        continue;
                    }
                    ground += glyph.layer == TilesV2Set.Layer.Terrain ? 1 : 0;
                    liquid += glyph.layer == TilesV2Set.Layer.Liquid ? 1 : 0;
                    torches += glyph.key == TilesV2World.TorchKey ? 1 : 0;
                    if (glyph.key == TilesV2World.SpawnKey)
                    {
                        start = new Vector2(column + 0.5f, map.Height - 1 - row);
                    }
                }
            }
            Assert.That(torches, Is.GreaterThan(5), "the sample shows torches");
            Assert.That(liquid, Is.GreaterThan(20), "and water and lava");
            Assert.That(map.Views.Count, Is.GreaterThan(4), "and names its places");
            Assert.That(map.Shots.Count, Is.GreaterThan(1), "and its pictures");
            foreach (var shot in map.Shots)
            {
                Assert.That(
                    map.Views.ContainsKey(shot.View),
                    Is.True,
                    shot.Name + " shows a place the map does not name"
                );
            }

            var scene = EditorSceneManager.OpenScene(TilesV2Sample.ScenePath, OpenSceneMode.Additive);
            try
            {
                TilesV2World world = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    world = world != null ? world : root.GetComponentInChildren<TilesV2World>();
                }
                Assert.That(world, Is.Not.Null, "build the sample scenes");
                Assert.That(world.transform.childCount, Is.EqualTo(0), "nothing of the world is saved in the scene");
                world.Build();
                Assert.That(world.IsBuilt, Is.True);
                Assert.That(
                    world.transform.Find(TilesV2World.BuiltName).gameObject.hideFlags,
                    Is.EqualTo(HideFlags.DontSave)
                );
                Assert.That(world.Terrain.GetUsedTilesCount(), Is.GreaterThan(5));
                Assert.That(Count(world.Terrain), Is.EqualTo(ground));
                Assert.That(world.Terrain.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("World")));
                Assert.That(world.Terrain.GetComponent<CompositeCollider2D>(), Is.Not.Null, "the hero can stand on it");
                var wet = 0;
                foreach (var liquidMap in world.Liquids)
                {
                    wet += Count(liquidMap);
                }
                Assert.That(wet, Is.EqualTo(liquid));
                Assert.That(world.Spawn, Is.EqualTo(start));
                Assert.That(world.Area, Is.EqualTo(new Rect(0f, 0f, map.Width, map.Height)));
                Assert.That(Count(world.Blend), Is.GreaterThan(20), "blends");
                Assert.That(Count(world.Shade), Is.GreaterThan(100), "shade");
                Assert.That(Count(world.Decor), Is.GreaterThan(60), "decor");
                Assert.That(Count(world.Glow), Is.GreaterThan(20), "glints");
                Assert.That(world.Walls.color.maxColorComponent, Is.LessThan(1f), "the walls are tinted back");
                foreach (var cell in world.Decor.cellBounds.allPositionsWithin)
                {
                    if (world.Decor.HasTile(cell))
                    {
                        Assert.That(world.Terrain.HasTile(cell), Is.False, "decor in the ground at " + cell);
                    }
                }

                var flames = world.Torches.GetComponentsInChildren<TorchFlame>();
                Assert.That(flames.Length, Is.EqualTo(torches));
                foreach (var flame in flames)
                {
                    Assert.That(flame.Glow, Is.Not.Null);
                    Assert.That(flame.FrameCount, Is.EqualTo(3));
                    Assert.That(
                        flame.GetComponentsInChildren<ParticleSystem>().Length,
                        Is.EqualTo(2),
                        "dust and embers"
                    );
                }
                var lights = world.GetComponentsInChildren<Light2D>();
                Assert.That(
                    System.Array.FindAll(lights, l => l.lightType == Light2D.LightType.Sprite).Length,
                    Is.EqualTo(1)
                );
                var ambient = System.Array.Find(lights, l => l.lightType == Light2D.LightType.Global);
                Assert.That(ambient.intensity, Is.EqualTo(set.ambient).Within(1e-4f), "deep rock is dark");
                Assert.That(world.Day, Is.Not.Null);
                Assert.That(world.Day.Hour, Is.EqualTo(set.dayStart).Within(1e-4f));
                Assert.That(world.Sky, Is.Not.Null);
                Assert.That(world.Sky.Palette.Length, Is.EqualTo(set.skyBands), "the sky is painted");
                Assert.That(world.Sky.Pixels, Is.EqualTo(set.ViewPixels), "at the size of the camera's view");
                Assert.That(world.Framed(Vector2.zero), Is.EqualTo(set.View / 2f), "a view is kept inside the map");
                Assert.That(world.Framed(world.Area.center), Is.EqualTo(world.Area.center));

                var layers = world.GetComponentsInChildren<ParallaxLayer>();
                Assert.That(layers.Length, Is.GreaterThanOrEqualTo(5));
                var masked = 0;
                foreach (var layer in layers)
                {
                    var picture = layer.GetComponent<SpriteRenderer>();
                    var lit = !picture.sharedMaterial.shader.name.Contains("Unlit");
                    masked += picture.maskInteraction == SpriteMaskInteraction.VisibleInsideMask ? 1 : 0;
                    Assert.That(
                        lit,
                        Is.EqualTo(layer.name.Contains("cave")),
                        layer.name + ": only the cave rock takes the light"
                    );
                }
                Assert.That(masked, Is.EqualTo(2), "the cave rock and what stands far off in it are cut to the ground");
                Assert.That(world.GetComponentsInChildren<SpriteMask>().Length, Is.EqualTo(1));
                Assert.That(
                    world.GetComponentsInChildren<Triggers.Water>().Length,
                    Is.GreaterThanOrEqualTo(2),
                    "a pond and a lake"
                );
                Assert.That(
                    world.GetComponentsInChildren<World.HazardArea>().Length,
                    Is.GreaterThanOrEqualTo(1),
                    "lava hurts"
                );
                Assert.That(world.TryView(TilesV2Sample.FirstView, out _), Is.True);

                var before = Snapshot(world.Decor);
                world.Build();
                Assert.That(Snapshot(world.Decor), Is.EqualTo(before), "building again changes nothing");
                Assert.That(world.transform.childCount, Is.EqualTo(1), "and leaves one built world");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ThePlaygroundIsReadyToPlayAndNeitherSceneIsInTheGame()
        {
            foreach (var path in new[] { TilesV2Sample.ScenePath, TilesV2Sample.PlaygroundPath })
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, "build the sample scenes");
                foreach (var listed in EditorBuildSettings.scenes)
                {
                    Assert.That(listed.path, Is.Not.EqualTo(path), "the sandbox is not part of the game");
                }
            }
            var scene = EditorSceneManager.OpenScene(TilesV2Sample.PlaygroundPath, OpenSceneMode.Additive);
            try
            {
                TilesV2Playground playground = null;
                Input.InputRouter router = null;
                Camera camera = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    playground = playground != null ? playground : root.GetComponentInChildren<TilesV2Playground>();
                    router = router != null ? router : root.GetComponentInChildren<Input.InputRouter>();
                    camera = camera != null ? camera : root.GetComponentInChildren<Camera>();
                }
                Assert.That(playground, Is.Not.Null);
                Assert.That(playground.World, Is.Not.Null);
                Assert.That(playground.Hero, Is.Not.Null, "the hero's prefab is in the scene");
                Assert.That(
                    PrefabUtility.IsPartOfPrefabInstance(playground.Hero),
                    Is.True,
                    "as an instance, so it follows the game's hero"
                );
                Assert.That(router, Is.Not.Null);
                Assert.That(router.Actions, Is.Not.Null, "the game's input actions");
                Assert.That(camera.GetComponent<CameraRig.GameCamera>(), Is.Not.Null, "the game's camera follows him");
                Assert.That(set.viewWidth, Is.EqualTo(manifest.view.width));
                Assert.That(set.viewHeight, Is.EqualTo(manifest.view.height));
                var pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
                Assert.That(
                    pixelPerfect.refResolutionX,
                    Is.EqualTo(set.viewWidth),
                    "the camera shows the standard's view"
                );
                Assert.That(pixelPerfect.refResolutionY, Is.EqualTo(set.viewHeight));
                Assert.That(camera.orthographicSize, Is.EqualTo(set.View.y / 2f).Within(1e-4f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ATorchWaversByLessThanATenth()
        {
            for (var t = 0f; t < 20f; t += 0.05f)
            {
                Assert.That(TorchFlame.Waver(t, 3.3f), Is.InRange(0.92f, 1.08f));
            }
            Assert.That(TorchFlame.Waver(1f, 0f), Is.Not.EqualTo(TorchFlame.Waver(1f, 5f)), "torches are out of step");
        }

        [Test]
        public void ThePaletteHoldsEveryMaterialSlopeAndLiquidOnce()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Palette.PalettePath);
            Assert.That(prefab, Is.Not.Null, "run Descender > Tiles V2 > Build tiles");
            var map = prefab.GetComponentInChildren<Tilemap>();
            var layout = TilesV2Palette.Layout(manifest);
            var expected = manifest.materials.Length + manifest.liquids.Length;
            foreach (var material in manifest.materials)
            {
                expected += manifest.SlopesOf(material).Length;
            }
            Assert.That(layout.Count, Is.EqualTo(expected));
            var seen = new HashSet<Vector3Int>();
            foreach (var (cell, path) in layout)
            {
                Assert.That(seen.Add(cell), Is.True, "two tiles at " + cell);
                Assert.That(map.GetTile(cell), Is.SameAs(AssetDatabase.LoadAssetAtPath<TileBase>(path)), path);
            }
        }

        private Transform NewGrid()
        {
            var root = new GameObject("Tiles V2 test grid");
            made.Add(root);
            root.AddComponent<Grid>();
            return root.transform;
        }

        private static Tilemap NewTilemap(Transform grid, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(grid, false);
            return holder.AddComponent<Tilemap>();
        }

        private Tilemap NewTilemap(string name) => NewTilemap(NewGrid(), name);

        private static TerrainTile TileOf(string key)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(TilesV2Manifest.TilePath(key));
            Assert.That(tile, Is.Not.Null, key);
            return tile;
        }

        // Every tile the decor rules of these keys lay.
        private HashSet<TileBase> TilesOf(params string[] keys)
        {
            var tiles = new HashSet<TileBase>();
            foreach (var rule in set.decor)
            {
                if (System.Array.IndexOf(keys, rule.key) < 0)
                {
                    continue;
                }
                tiles.UnionWith(rule.first);
                tiles.UnionWith(rule.middle);
                tiles.UnionWith(rule.last);
            }
            Assert.That(tiles, Is.Not.Empty, string.Join(", ", keys));
            return tiles;
        }

        private static int Count(Tilemap map)
        {
            var count = 0;
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                count += map.HasTile(cell) ? 1 : 0;
            }
            return count;
        }

        private static Dictionary<Vector3Int, string> Snapshot(Tilemap map)
        {
            var cells = new Dictionary<Vector3Int, string>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(cell);
                if (tile != null)
                {
                    cells[cell] = tile.name;
                }
            }
            return cells;
        }

        private static void AssertFrame(Tilemap map, int x, int y, string key, int frame)
        {
            var sprite = map.GetSprite(new Vector3Int(x, y, 0));
            Assert.That(sprite, Is.Not.Null, $"no sprite at {x},{y}");
            Assert.That(sprite.name, Does.StartWith($"tv2_{key}_f{frame}_v"), $"cell {x},{y}");
        }
    }
}
