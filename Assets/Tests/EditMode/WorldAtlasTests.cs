using System.IO;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The world as tools/unity/import_godot_regions.py wrote it: twelve contiguous
    /// bands, a scene and a tile set for each, the start and every checkpoint inside
    /// its band, every cell of every layer inside its atlas and its band.
    /// </summary>
    public class WorldAtlasTests
    {
        private const string AtlasPath = "Assets/Data/World.asset";

        private static WorldAtlas Atlas()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>(AtlasPath);
            Assert.That(atlas, Is.Not.Null, AtlasPath + " is missing; run tools/unity/import_godot_regions.py");
            return atlas;
        }

        [Test]
        public void TwelveBandsStackWithoutGaps()
        {
            var atlas = Atlas();
            Assert.That(atlas.regions.Length, Is.EqualTo(12));
            Assert.That(atlas.regions[0].key, Is.EqualTo("paradise"));
            Assert.That(atlas.regions[1].key, Is.EqualTo("ruined_earth"));
            Assert.That(atlas.regions[11].key, Is.EqualTo("treachery"));
            Assert.That(atlas.regions[0].topRow, Is.EqualTo(0));
            for (var i = 1; i < atlas.regions.Length; i++)
            {
                Assert.That(
                    atlas.regions[i].topRow,
                    Is.EqualTo(atlas.regions[i - 1].topRow + atlas.regions[i - 1].rows),
                    $"{atlas.regions[i].key} does not start where {atlas.regions[i - 1].key} ends"
                );
            }
        }

        [Test]
        public void IndexAtFindsTheBandOfAWorldPoint()
        {
            var atlas = Atlas();
            Assert.That(atlas.IndexAt(-0.5f), Is.EqualTo(0));
            Assert.That(atlas.IndexAt(-50.5f), Is.EqualTo(0));
            Assert.That(atlas.IndexAt(-51.5f), Is.EqualTo(1));
            Assert.That(atlas.IndexAt(5f), Is.EqualTo(0), "above the world is still the first band");
            Assert.That(atlas.IndexAt(-100000f), Is.EqualTo(11), "below the world is still the last band");
        }

        [Test]
        public void StartAndCheckpointsLieInTheirBands()
        {
            var atlas = Atlas();
            Assert.That(atlas.startRegion, Is.EqualTo("paradise"));
            Assert.That(atlas.regions[atlas.IndexAt(atlas.start.y)].key, Is.EqualTo("paradise"));
            Assert.That(atlas.checkpoints.Length, Is.EqualTo(31));
            var lastY = float.PositiveInfinity;
            foreach (var checkpoint in atlas.checkpoints)
            {
                var band = atlas.regions[atlas.IndexAt(checkpoint.position.y)];
                Assert.That(band.key, Is.EqualTo(checkpoint.region), "a checkpoint lies outside its region's band");
                Assert.That(checkpoint.position.y, Is.LessThanOrEqualTo(lastY), "checkpoints run top to bottom");
                lastY = checkpoint.position.y;
            }
            Assert.That(atlas.CheckpointAt(-1).position, Is.EqualTo(atlas.start));
            Assert.That(atlas.CheckpointAt(31).position, Is.EqualTo(atlas.start));
        }

        [Test]
        public void AbilitiesFollowTheSaveEnum()
        {
            var atlas = Atlas();
            Assert.That(atlas.abilities.Length, Is.EqualTo(6));
            Assert.That(atlas.AbilityIndex("Wings (double jump)"), Is.EqualTo((int)Save.Ability.Wings));
            Assert.That(atlas.AbilityIndex("Halo Dash"), Is.EqualTo((int)Save.Ability.LightDash));
            Assert.That(atlas.AbilityIndex("Smite"), Is.EqualTo((int)Save.Ability.Smite));
        }

        [Test]
        public void EveryRegionHasASceneAndWholeTiles()
        {
            var atlas = Atlas();
            foreach (var band in atlas.regions)
            {
                var scenePath = $"Assets/Scenes/{band.SceneName}.unity";
                Assert.That(File.Exists(scenePath), scenePath + " is missing");
                var tileSet = AssetDatabase.LoadAssetAtPath<RegionTileSet>(
                    $"Assets/Data/Regions/Tiles/{band.key}_tiles.asset"
                );
                Assert.That(tileSet, Is.Not.Null, band.key + " has no tile set");
                Assert.That(tileSet.sprites.Length, Is.EqualTo(256), band.key + " atlas is not 16 x 16");
                Assert.That(tileSet.cells, Is.Not.Null, band.key + " has no cell data");
                for (var i = 0; i < tileSet.sprites.Length; i++)
                {
                    Assert.That(tileSet.sprites[i], Is.Not.Null, $"{band.key} sprite {i} did not import");
                }
                var data = tileSet.Data;
                Assert.That(data.Kinds.Length, Is.EqualTo(256));
                Assert.That(data.Layers.ContainsKey(RegionTiles.TerrainLayer), band.key + " has no Terrain layer");
                var solid = 0;
                foreach (var layer in data.Layers)
                {
                    foreach (var cell in layer.Value)
                    {
                        Assert.That(cell.X, Is.InRange(0, atlas.width - 1), $"{band.key} {layer.Key} cell off the grid");
                        Assert.That(cell.Y, Is.InRange(0, band.rows - 1), $"{band.key} {layer.Key} cell off the band");
                        Assert.That(cell.Tile, Is.InRange(0, 255), $"{band.key} {layer.Key} tile off the atlas");
                        if (layer.Key == RegionTiles.TerrainLayer && data.KindOf(cell.Tile) == TileKind.Solid)
                        {
                            solid++;
                        }
                    }
                }
                Assert.That(solid, Is.GreaterThan(100), band.key + " has almost no solid ground");
            }
        }

        [Test]
        public void TheCatalogResolvesByCharacterThenType()
        {
            var catalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            var imp = new GameObject("imp");
            var any = new GameObject("walker");
            var boss = new GameObject("boss");
            var save = new GameObject("save");
            try
            {
                catalog.Set("enemy:imp", imp);
                catalog.Set("enemy", any);
                catalog.Set("boss", boss);
                catalog.Set("save", save);
                Assert.That(catalog.Resolve("enemy", "imp"), Is.SameAs(imp));
                Assert.That(catalog.Resolve("enemy", "brute"), Is.SameAs(any));
                Assert.That(catalog.Resolve("enemy", Enemies.BossIds.All[0]), Is.SameAs(boss));
                Assert.That(catalog.Resolve("save", ""), Is.SameAs(save));
                Assert.That(catalog.Resolve("npc", "pell"), Is.Null);
                catalog.Set("save", any);
                Assert.That(catalog.Resolve("save", ""), Is.SameAs(any), "Set replaces an entry");
            }
            finally
            {
                Object.DestroyImmediate(imp);
                Object.DestroyImmediate(any);
                Object.DestroyImmediate(boss);
                Object.DestroyImmediate(save);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
