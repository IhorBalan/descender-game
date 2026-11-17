using System.Collections.Generic;
using Descender.EditorTools.TilesV2;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The Paradise level of Tiles V2 (tools/tiles_v2/make_paradise_map.py, from the
    /// wireframe in tv2_paradise_plan.py): its map reads, builds, and holds what the plan
    /// says: seven zones, their shrines, the two abilities found there, the story's people
    /// and the way out; its scene is a journey and stays out of the game's build.
    /// That it can be played through is checked where it is made (`--check`).
    /// </summary>
    public sealed class TilesV2ParadiseTests
    {
        private static readonly string[] ZoneNames =
        {
            "The Waking Hollow",
            "The Orchard Under the Canopy",
            "The Great Vines",
            "The Tallied Fields",
            "The Terraces",
            "The Making-house Approach",
            "The Low Gate",
        };

        private TilesV2Set set;
        private TilesV2Map map;
        private TilesV2World world;

        [OneTimeSetUp]
        public void BuildTheLevel()
        {
            set = TilesV2Builder.LoadSet();
            Assert.That(set, Is.Not.Null, "run Descender > Tiles V2 > Build tiles");
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(TilesV2Sample.ParadiseMapPath);
            Assert.That(text, Is.Not.Null, "run tools/tiles_v2/make_paradise_map.py");
            map = TilesV2Map.Parse(text.text);
            world = new GameObject("Tiles V2 Paradise under test").AddComponent<TilesV2World>();
            world.gameObject.hideFlags = HideFlags.DontSave;
            world.Configure(text, set, set.dayStart, false, true);
            world.Build();
        }

        [OneTimeTearDown]
        public void RemoveTheLevel()
        {
            if (world != null)
            {
                Object.DestroyImmediate(world.gameObject);
            }
        }

        [Test]
        public void TheMapIsOneRegionOfSkyAndEveryCharacterIsInTheLegend()
        {
            Assert.That(map.Regions.Count, Is.EqualTo(1));
            Assert.That(map.Regions[0].Key, Is.EqualTo("paradise"));
            Assert.That(
                map.Surface,
                Is.GreaterThan(map.Height - 6),
                "the whole level is over the surface line: open sky"
            );
            var unknown = new HashSet<char>();
            for (var row = 0; row < map.Height; row++)
            {
                for (var column = 0; column < map.Width; column++)
                {
                    foreach (var character in new[] { map.TerrainAt(column, row), map.WallAt(column, row) })
                    {
                        if (character != TilesV2Map.Air && !set.TryGlyph(character, out _))
                        {
                            unknown.Add(character);
                        }
                    }
                }
            }
            Assert.That(unknown, Is.Empty, "characters the tile set has no tile for");
        }

        [Test]
        public void TheLevelHoldsWhatThePlanSays()
        {
            var shrines = 0;
            var abilities = new List<string>();
            var zones = new HashSet<string>();
            var rooms = 0;
            var ends = 0;
            var vistas = 0;
            foreach (var mark in map.Marks)
            {
                switch (mark.Kind)
                {
                    case TilesV2Things.Checkpoint:
                        shrines++;
                        Assert.That(Solid(mark.Column, mark.Row + 1), Is.True, "a shrine stands on the ground");
                        break;
                    case TilesV2Things.AbilityKind:
                        abilities.Add(mark.Word(0));
                        break;
                    case TilesV2Things.Zone:
                        rooms++;
                        zones.Add(string.Join(" ", mark.Rest, 3, mark.Rest.Length - 3));
                        break;
                    case TilesV2Things.End:
                        ends++;
                        break;
                    case TilesV2Things.Vista:
                        vistas++;
                        break;
                    case TilesV2Things.Stood:
                        Assert.That(
                            System.Array.Exists(set.backdrops, backdrop => backdrop.key == mark.Word(0)),
                            Is.True,
                            "the tile set has no " + mark.Word(0)
                        );
                        Assert.That(Solid(mark.Column, mark.Row), Is.False, mark.Word(0) + " is marked in the ground");
                        break;
                }
            }
            Assert.That(shrines, Is.EqualTo(16), "the wireframe's places to rest");
            Assert.That(abilities, Is.EquivalentTo(new[] { "Wings", "LightDash" }), "what is found in Paradise");
            Assert.That(rooms, Is.EqualTo(69), "every room of the wireframe says its zone");
            Assert.That(zones, Is.EquivalentTo(ZoneNames));
            Assert.That(ends, Is.EqualTo(1), "one way out: the Low Gate");
            Assert.That(vistas, Is.GreaterThan(3), "the view draws back at the landmarks");
        }

        [Test]
        public void TheThingsOfTheLevelStandAndTheHeroWakesInTheHollow()
        {
            Pickups.Checkpoint.Reset();
            var things = TilesV2Things.Build(
                world,
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.CheckpointPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.AbilityPath)
            );
            try
            {
                Assert.That(things.Zones.Count, Is.EqualTo(69));
                Assert.That(things.EndTitle, Is.EqualTo("The Low Gate"));
                Assert.That(things.EndWords, Is.Not.Empty);
                Assert.That(
                    things.Signs,
                    Is.GreaterThan(11),
                    "the Voice, Mereth, Ithuriel, the gathered, and what is found"
                );
                Assert.That(things.Vistas, Is.GreaterThan(3));
                Assert.That(things.Pictures, Is.GreaterThan(1000), "the level is dressed");
                // The hero starts in the first zone, and each zone's rooms hold some place of the level.
                var start = world.Spawn;
                var first = "";
                foreach (var (area, name) in things.Zones)
                {
                    Assert.That(world.Area.Overlaps(area), Is.True, name + " lies outside the level");
                    if (area.Contains(start + Vector2.up))
                    {
                        first = name;
                    }
                }
                Assert.That(first, Is.EqualTo(ZoneNames[0]), "the hero wakes in the Waking Hollow");
            }
            finally
            {
                Object.DestroyImmediate(things.gameObject);
                Pickups.Checkpoint.Reset();
            }
        }

        [Test]
        public void ItIsAnOldPlaceAtAnHourThatDoesNotPass()
        {
            // The level says its own hour, late and gold, and that its sun does not move.
            Assert.That(map.Hour, Is.InRange(16.5f, 18.5f));
            Assert.That(map.Still, Is.True);
            Assert.That(world.Day.Hour, Is.EqualTo(map.Hour).Within(0.01f), "the world takes the map's hour");
            Assert.That(world.Day.Running, Is.False, "the sun stands still");
            // A fire by every shrine, an arch round every doorway, fog in the Warden's door.
            var shrines = 0;
            var fires = 0;
            var arches = 0;
            var fog = 0;
            foreach (var mark in map.Marks)
            {
                shrines += mark.Kind == TilesV2Things.Checkpoint ? 1 : 0;
                if (mark.Kind != TilesV2Things.Stood)
                {
                    continue;
                }
                fires += mark.Word(0) == "eden_bonfire" ? 1 : 0;
                arches += mark.Word(0).StartsWith("eden_arch") ? 1 : 0;
                fog += mark.Word(0) == "eden_fog" ? 1 : 0;
            }
            Assert.That(fires, Is.EqualTo(shrines), "a fire to rest by at every shrine");
            Assert.That(arches, Is.GreaterThan(25), "the doorways stand in arches");
            Assert.That(fog, Is.EqualTo(1), "fog stands in the Warden's door");
            // The fog is several pictures shown in turn, and nothing stops the hero at it.
            Assert.That(System.Array.Exists(set.backdrops, backdrop => backdrop.key == "eden_fog_f1"), Is.True);
        }

        [Test]
        public void TheSkyOfATallLevelIsBehindEveryRoom()
        {
            // The heaven layers go up with the camera by more under a sky higher than the world's.
            Assert.That(map.Surface, Is.GreaterThan(TilesV2Backdrops.TallestSetSky));
            Assert.That(world.Light, Is.Not.Null);
            // Open sky: the air of a room under the sky is lit as by day, high in the level and low.
            foreach (var view in new[] { "eden_gate", "eden_hollow", "eden_terraces" })
            {
                Assert.That(world.TryView(view, out var at), Is.True, view);
                var cell = new Vector3Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y), 0);
                Assert.That(world.Light.At(cell).maxColorComponent, Is.GreaterThan(0.85f), view + " is in the dark");
            }
        }

        [Test]
        public void TheLevelsSceneIsAJourneyAndStaysOutOfTheGamesBuild()
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                Assert.That(scene.path, Is.Not.EqualTo(TilesV2Sample.ParadisePath));
            }
            var text = System.IO.File.ReadAllText(TilesV2Sample.ParadisePath);
            Assert.That(text, Does.Contain("journey: 1"), "the hero starts with nothing");
            Assert.That(
                text,
                Does.Not.Contain("m_Name: " + TilesV2World.BuiltName),
                "nothing of the level is saved in the scene"
            );
            Assert.That(text.Length, Is.LessThan(40000), "the scene is a few objects");
        }

        private bool Solid(int column, int row)
        {
            return set.TryGlyph(map.TerrainAt(column, row), out var glyph) && glyph.layer == TilesV2Set.Layer.Terrain;
        }
    }
}
