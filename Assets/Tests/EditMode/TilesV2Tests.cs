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
    /// The Tiles V2 standard (docs/art/tiles-v2.md): the generator's frames are the 47
    /// the Rule Tiles expect, every material has a rule a frame with all its variants, a
    /// painted block picks the right frames, joined materials meet with no edge, blends
    /// land on stone touching dirt, and the sample scene stays out of the build.
    /// </summary>
    public class TilesV2Tests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private TilesV2Manifest manifest;

        [SetUp]
        public void SetUp()
        {
            manifest = TilesV2Manifest.Load();
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
                Assert.That(tile.m_TilingRules.Count, Is.EqualTo(manifest.frames.Length), material.key);
                var expected = material.IsWall ? Tile.ColliderType.None : Tile.ColliderType.Grid;
                foreach (var rule in tile.m_TilingRules)
                {
                    Assert.That(rule.m_Sprites.Length, Is.EqualTo(manifest.variants), material.key);
                    Assert.That(rule.m_Sprites, Has.None.Null, material.key);
                    Assert.That(rule.m_ColliderType, Is.EqualTo(expected), material.key);
                }
                foreach (var slope in material.slopes ? manifest.slopes : new string[0])
                {
                    var slopeTile = AssetDatabase.LoadAssetAtPath<TerrainTile>(
                        TilesV2Manifest.SlopePath(material.key, slope)
                    );
                    Assert.That(slopeTile, Is.Not.Null, material.key + " " + slope);
                    Assert.That(slopeTile.m_TilingRules[0].m_ColliderType, Is.EqualTo(Tile.ColliderType.Sprite));
                    Assert.That(slopeTile.Joins(tile), Is.True, "a slope must join its ground");
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
        public void VariantsAreSpreadEvenlyOverCells()
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
        }

        [Test]
        public void BlendsLandOnStoneSidesThatTouchDirtOrGrass()
        {
            var terrain = NewTilemap("Terrain");
            var blend = NewTilemap("Blend");
            terrain.SetTile(new Vector3Int(0, 0, 0), TileOf("stone"));
            terrain.SetTile(new Vector3Int(0, 1, 0), TileOf("dirt"));
            terrain.SetTile(new Vector3Int(1, 0, 0), TileOf("grass"));
            terrain.SetTile(new Vector3Int(5, 0, 0), TileOf("stone"));
            var painted = TilesV2Blends.Paint(terrain, blend, manifest);
            Assert.That(painted, Is.EqualTo(1));
            var expected = AssetDatabase.LoadAssetAtPath<TileBase>(TilesV2Manifest.BlendPath("dirt_on_stone", 1 | 2));
            Assert.That(expected, Is.Not.Null);
            Assert.That(blend.GetTile(new Vector3Int(0, 0, 0)), Is.SameAs(expected));
            Assert.That(blend.GetTile(new Vector3Int(5, 0, 0)), Is.Null);
        }

        [Test]
        public void ParallaxLayersFollowTheCameraByTheShareTheyDoNotScroll()
        {
            var anchor = new Vector2(10f, 4f);
            var view = new Vector2(10f, 6f);
            var moved = new Vector2(18f, 2f);
            Assert.That(ParallaxLayer.PositionFor(anchor, view, moved, 0f), Is.EqualTo(new Vector2(18f, 0f)));
            Assert.That(ParallaxLayer.PositionFor(anchor, view, moved, 1f), Is.EqualTo(anchor));
            Assert.That(ParallaxLayer.PositionFor(anchor, view, moved, 0.5f), Is.EqualTo(new Vector2(14f, 2f)));
            var snapped = ParallaxLayer.PositionFor(anchor, view, new Vector2(10.01f, 6f), 0.5f);
            Assert.That(snapped.x * 16f, Is.EqualTo(Mathf.Round(snapped.x * 16f)), "layers sit on whole art pixels");
        }

        [Test]
        public void TheSampleMapHasMatchingGridsAndItsSceneStaysOutOfTheBuild()
        {
            var (terrain, walls) = TilesV2Sample.ReadMap(System.IO.File.ReadAllText(TilesV2Sample.MapPath));
            Assert.That(walls.Count, Is.EqualTo(terrain.Count));
            foreach (var row in terrain)
            {
                Assert.That(row.Length, Is.EqualTo(terrain[0].Length));
            }
            Assert.That(TilesV2Sample.CellOf(0, 0, terrain.Count).y, Is.EqualTo(terrain.Count - 1));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(TilesV2Sample.ScenePath),
                Is.Not.Null,
                "build the sample scene"
            );
            foreach (var scene in EditorBuildSettings.scenes)
            {
                Assert.That(scene.path, Is.Not.EqualTo(TilesV2Sample.ScenePath), "the sample is not part of the game");
            }
        }

        private Tilemap NewTilemap(string name)
        {
            var root = new GameObject("Tiles V2 test grid");
            made.Add(root);
            root.AddComponent<Grid>();
            var holder = new GameObject(name);
            holder.transform.SetParent(root.transform, false);
            return holder.AddComponent<Tilemap>();
        }

        private static TerrainTile TileOf(string key)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(TilesV2Manifest.TilePath(key));
            Assert.That(tile, Is.Not.Null, key);
            return tile;
        }

        private static void AssertFrame(Tilemap map, int x, int y, string key, int frame)
        {
            var sprite = map.GetSprite(new Vector3Int(x, y, 0));
            Assert.That(sprite, Is.Not.Null, $"no sprite at {x},{y}");
            Assert.That(sprite.name, Does.StartWith($"tv2_{key}_f{frame}_v"), $"cell {x},{y}");
        }
    }
}
