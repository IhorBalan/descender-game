using System.Collections.Generic;
using Descender.EditorTools.TilesV2;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The Earth of the Tiles V2 world (docs/art/tiles-v2-world.md, "The Earth"): it is
    /// dressed with pictures the tile set has, which stand on its ground and take the tile
    /// light; its trees are many and of several kinds; the old mine is timbered, its sets
    /// standing on the floor of a tunnel and reaching its roof, with track along its floors;
    /// its meadows flower and its mine has boarded walls; and the ridges behind the surface
    /// are four, the hills among them wider than a view.
    /// </summary>
    public class TilesV2EarthTests
    {
        private const string Picture = "picture";

        private TilesV2Set set;
        private TilesV2Map map;
        private TilesV2World world;

        [OneTimeSetUp]
        public void BuildTheWorld()
        {
            set = TilesV2Builder.LoadSet();
            Assert.That(set, Is.Not.Null, "run Descender > Tiles V2 > Build tiles");
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(TilesV2Sample.WorldMapPath);
            Assert.That(text, Is.Not.Null, "run tools/tiles_v2/make_world_map.py");
            map = TilesV2Map.Parse(text.text);
            world = new GameObject("Tiles V2 Earth under test").AddComponent<TilesV2World>();
            world.gameObject.hideFlags = HideFlags.DontSave;
            world.Configure(text, set, set.dayStart, false, true);
            world.Build();
        }

        [OneTimeTearDown]
        public void RemoveTheWorld()
        {
            if (world != null)
            {
                Object.DestroyImmediate(world.gameObject);
            }
        }

        [Test]
        public void TheEarthIsDressedWithPicturesThatStandOnItsGround()
        {
            var earth = EarthRows();
            var trees = new Dictionary<string, int>();
            var dressed = 0;
            foreach (var mark in map.Marks)
            {
                if (mark.Kind != Picture || !mark.Word(0).StartsWith("earth_"))
                {
                    continue;
                }
                dressed++;
                var key = mark.Word(0);
                var entry = System.Array.Find(set.backdrops, backdrop => backdrop.key == key);
                Assert.That(entry, Is.Not.Null, key + " is not in the tile set");
                Assert.That(entry.sprite, Is.Not.Null, key + " has no sprite");
                Assert.That(entry.kind, Is.EqualTo(TilesV2Set.BackdropKind.Picture), key);
                Assert.That(
                    mark.Row >= earth.x && mark.Row < earth.y,
                    Is.True,
                    key + " stands outside the Earth at row " + mark.Row
                );
                Assert.That(entry.order, Is.Not.InRange(-10, 10), key + " is drawn in the hero's place");
                var kind = TreeKind(key);
                if (kind != null)
                {
                    // A tree is marked in the cell of air over the ground it grows from.
                    Assert.That(
                        Solid(mark.Column, mark.Row + 1),
                        Is.True,
                        key + " grows from nothing at " + mark.Column
                    );
                    Assert.That(Solid(mark.Column, mark.Row), Is.False, key + " is buried at " + mark.Column);
                    Assert.That(mark.Row, Is.LessThan(map.Surface), key + " grows under the surface line");
                    trees[kind] = trees.TryGetValue(kind, out var n) ? n + 1 : 1;
                }
            }
            Assert.That(dressed, Is.GreaterThan(150), "the Earth is dressed");
            var all = 0;
            foreach (var count in trees.Values)
            {
                all += count;
            }
            Assert.That(all, Is.GreaterThan(25), "trees stand on the Earth (each six to nine tiles across)");
            Assert.That(
                trees.Count,
                Is.GreaterThanOrEqualTo(4),
                "trees of several kinds: " + string.Join(", ", trees.Keys)
            );
            foreach (
                var key in new[] { "earth_pithead", "earth_chapel", "earth_windmill", "earth_gateway", "earth_willow" }
            )
            {
                Assert.That(Count(key), Is.EqualTo(1), key + " stands once");
            }
        }

        [Test]
        public void TheOldMineIsTimberedAndHasTrack()
        {
            var sets = 0;
            var track = 0;
            foreach (var mark in map.Marks)
            {
                if (mark.Kind != Picture)
                {
                    continue;
                }
                var key = mark.Word(0);
                if (key.StartsWith("earth_post_"))
                {
                    sets++;
                    var rows = int.Parse(key.Substring("earth_post_".Length));
                    Assert.That(mark.Row, Is.GreaterThan(map.Surface), "timber is in the mine");
                    Assert.That(
                        Solid(mark.Column, mark.Row + 1),
                        Is.True,
                        "a set stands on the floor at " + mark.Column
                    );
                    for (var k = 0; k < rows; k++)
                    {
                        Assert.That(
                            map.TerrainAt(mark.Column, mark.Row - k),
                            Is.EqualTo(TilesV2Map.Air),
                            "a set of timber is as tall as its tunnel at " + mark.Column
                        );
                    }
                    Assert.That(Solid(mark.Column, mark.Row - rows), Is.True, "and carries the roof at " + mark.Column);
                }
                else if (key.StartsWith("earth_rail_"))
                {
                    track++;
                    Assert.That(Solid(mark.Column, mark.Row + 1), Is.True, "track lies on the floor at " + mark.Column);
                    Assert.That(map.TerrainAt(mark.Column, mark.Row), Is.EqualTo(TilesV2Map.Air));
                }
            }
            Assert.That(sets, Is.GreaterThan(20), "the old mine is timbered");
            Assert.That(track, Is.GreaterThan(60), "track runs along its floors");
        }

        [Test]
        public void MeadowsFlowerAndTheMineHasBoardedWalls()
        {
            Assert.That(set.TryGlyph('Ñ', out var meadow), Is.True, "the legend has the meadow");
            Assert.That(meadow.layer, Is.EqualTo(TilesV2Set.Layer.Terrain));
            Assert.That(meadow.tile, Is.Not.Null);
            Assert.That(set.TryGlyph('Ø', out var boards), Is.True, "the legend has the mine's boards");
            Assert.That(boards.layer, Is.EqualTo(TilesV2Set.Layer.Wall));
            var flowering = 0;
            var boarded = 0;
            for (var row = 0; row < map.Height; row++)
            {
                for (var column = 0; column < map.Width; column++)
                {
                    flowering += map.TerrainAt(column, row) == 'Ñ' ? 1 : 0;
                    boarded += map.WallAt(column, row) == 'Ø' ? 1 : 0;
                }
            }
            Assert.That(flowering, Is.GreaterThan(40), "meadows in flower");
            Assert.That(boarded, Is.GreaterThan(40), "boarded walls in the mine");
            var flowers = 0;
            foreach (var rule in set.decor)
            {
                flowers += rule.key == "wildflower" && System.Array.IndexOf(rule.on, "meadow") >= 0 ? 1 : 0;
            }
            Assert.That(flowers, Is.EqualTo(1), "wild flowers grow on the meadow");
        }

        [Test]
        public void ThePicturesStandAndTakeTheTileLight()
        {
            Pickups.Checkpoint.Reset();
            var things = TilesV2Things.Build(
                world,
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.CheckpointPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.AbilityPath)
            );
            try
            {
                var marked = 0;
                foreach (var mark in map.Marks)
                {
                    marked += mark.Kind == Picture ? 1 : 0;
                }
                Assert.That(things.Pictures, Is.EqualTo(marked), "every picture the map marks stands");
                var lit = 0;
                var glowing = 0;
                foreach (var shown in things.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!shown.gameObject.name.StartsWith("Picture earth_"))
                    {
                        continue;
                    }
                    if (shown.gameObject.name == "Picture earth_cottage_glow")
                    {
                        glowing++;
                        Assert.That(shown.sharedMaterial, Is.EqualTo(set.unlitMaterial), "a lamp in a window is unlit");
                        continue;
                    }
                    lit++;
                    Assert.That(
                        shown.sharedMaterial,
                        Is.EqualTo(set.litMaterial),
                        shown.name + " takes the tile light"
                    );
                }
                Assert.That(lit, Is.GreaterThan(150));
                Assert.That(glowing, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(things.gameObject);
                Pickups.Checkpoint.Reset();
            }
        }

        [Test]
        public void FourRidgesStandBehindTheSurface()
        {
            var ridges = new List<TilesV2Set.Backdrop>();
            foreach (var entry in set.backdrops)
            {
                if (entry.kind == TilesV2Set.BackdropKind.Ridge)
                {
                    ridges.Add(entry);
                }
            }
            Assert.That(ridges.Count, Is.EqualTo(4));
            ridges.Sort((a, b) => a.order.CompareTo(b.order));
            for (var i = 1; i < ridges.Count; i++)
            {
                Assert.That(ridges[i].scroll, Is.GreaterThan(ridges[i - 1].scroll), "further is slower");
            }
            var hills = ridges.Find(entry => entry.key == "backdrop_earth_hills");
            Assert.That(hills, Is.Not.Null);
            Assert.That(
                hills.sprite.rect.width,
                Is.GreaterThan(set.viewWidth),
                "the far hills are wider than a view, so their castle does not stand in every one"
            );
        }

        // The rows of the Earth's region: its first and the first of the next.
        private Vector2Int EarthRows()
        {
            for (var i = 0; i < map.Regions.Count; i++)
            {
                if (map.Regions[i].Key == "earth")
                {
                    return new Vector2Int(map.Regions[i].Top, map.RegionEnd(i));
                }
            }
            Assert.Fail("the map has no Earth");
            return default;
        }

        private static string TreeKind(string key)
        {
            foreach (var kind in new[] { "oak", "pine", "apple", "dead_tree", "willow" })
            {
                if (key.StartsWith("earth_" + kind))
                {
                    return kind;
                }
            }
            return null;
        }

        private int Count(string key)
        {
            var count = 0;
            foreach (var mark in map.Marks)
            {
                count += mark.Kind == Picture && mark.Word(0) == key ? 1 : 0;
            }
            return count;
        }

        private bool Solid(int column, int row)
        {
            return set.TryGlyph(map.TerrainAt(column, row), out var glyph) && glyph.layer == TilesV2Set.Layer.Terrain;
        }
    }
}
