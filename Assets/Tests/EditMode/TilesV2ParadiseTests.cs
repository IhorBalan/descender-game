using System.Collections.Generic;
using Descender.EditorTools.TilesV2;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            // The level says the hour the hero wakes at, the Hollow's dawn, and that its sun does not move.
            Assert.That(map.Hour, Is.InRange(6f, 7.5f));
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
        public void TheRoadIsADayAndEveryZoneKeepsItsOwnHour()
        {
            Pickups.Checkpoint.Reset();
            var things = TilesV2Things.Build(
                world,
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.CheckpointPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.AbilityPath)
            );
            try
            {
                Assert.That(things.Moods.Count, Is.EqualTo(69), "every room has its hour");
                // From the Hollow's dawn to the Gate's dusk, never back.
                var road = new[]
                {
                    "eden_hollow",
                    "eden_orchard",
                    "eden_vines",
                    "eden_fields",
                    "eden_terraces",
                    "eden_making",
                    "eden_gate",
                };
                var before = 0f;
                foreach (var view in road)
                {
                    Assert.That(world.TryView(view, out var at), Is.True, view);
                    Assert.That(things.TryMood(at, out var hour), Is.True, view + " has no hour of its own");
                    Assert.That(hour, Is.GreaterThanOrEqualTo(before), view + " is earlier than the zone before it");
                    before = hour;
                }
                Assert.That(world.TryView("eden_hollow", out var hollow), Is.True);
                Assert.That(things.TryMood(hollow, out var dawn), Is.True);
                Assert.That(dawn, Is.EqualTo(map.Hour).Within(0.01f), "the hero wakes at the Hollow's hour");
                Assert.That(
                    TilesV2Life.DawnAt(dawn),
                    Is.GreaterThan(0.5f),
                    "the Hollow is at dawn: its sun stands in shafts"
                );
                Assert.That(before, Is.InRange(18.5f, 20.5f), "the Gate is at dusk");
                Assert.That(
                    things.TryMood(new Vector2(-50f, -50f), out _),
                    Is.False,
                    "outside the rooms no hour is said"
                );
            }
            finally
            {
                Object.DestroyImmediate(things.gameObject);
                Pickups.Checkpoint.Reset();
            }
        }

        [Test]
        public void EveryZoneHasDepthAirLifeAndWear()
        {
            // Far off behind the floors, more of each zone, passed slower than the world.
            var life = world.Life;
            Assert.That(life, Is.Not.Null);
            Assert.That(life.Far.Count, Is.GreaterThan(40));
            foreach (var shown in life.Far)
            {
                var layer = shown.GetComponent<ParallaxLayer>();
                Assert.That(layer, Is.Not.Null, shown.name);
                Assert.That(
                    shown.sortingOrder,
                    Is.LessThan(TilesV2World.FoliageOrder),
                    shown.name + " is not behind its room"
                );
            }
            var far = new HashSet<string>();
            var stood = new Dictionary<string, int>();
            foreach (var mark in map.Marks)
            {
                if (mark.Kind == TilesV2Life.Kind && mark.Word(0) == "far")
                {
                    far.Add(mark.Word(1));
                }
                if (mark.Kind == TilesV2Things.Stood)
                {
                    var key = mark.Word(0);
                    foreach (
                        var kind in new[] { "eden_crack", "eden_moss", "eden_under", "eden_mist", "eden_great_leaf" }
                    )
                    {
                        if (key.StartsWith(kind))
                        {
                            stood[kind] = stood.TryGetValue(kind, out var n) ? n + 1 : 1;
                        }
                    }
                }
            }
            foreach (
                var key in new[]
                {
                    "eden_far_hollow",
                    "eden_far_orchard",
                    "eden_far_vines",
                    "eden_far_fields",
                    "eden_far_terraces",
                    "eden_far_cliffs",
                    "eden_far_house_0",
                    "eden_far_house_1",
                    "eden_far_gate_0",
                    "eden_far_gate_1",
                    "eden_far_wheel",
                }
            )
            {
                Assert.That(far, Does.Contain(key), key + " stands nowhere");
            }
            // Near, and in the air: dark shapes before the hero, shafts of sun, each zone's own motes.
            foreach (
                var kind in new[] { "front", "rays", "pollen", "snow", "butterflies", "fireflies", "smoke", "birds" }
            )
            {
                Assert.That(life.Made.ContainsKey(kind), Is.True, "no " + kind + " in the level");
            }
            Assert.That(life.Made["front"], Is.GreaterThan(40));
            Assert.That(life.Swaying.Count, Is.GreaterThan(10), "the leaves and the wheat close to the eye sway");
            // Wear: cracks and growth on the cut stone, and what the floors in the air have under them.
            Assert.That(stood["eden_crack"], Is.GreaterThan(30));
            Assert.That(stood["eden_moss"], Is.GreaterThan(50));
            Assert.That(stood["eden_under"], Is.GreaterThan(400));
            Assert.That(stood["eden_mist"], Is.GreaterThan(20));
            Assert.That(stood["eden_great_leaf"], Is.GreaterThanOrEqualTo(1), "the Rising Leaf");
            // The gathered walk: their line is eight pictures shown in turn.
            Assert.That(System.Array.Exists(set.backdrops, backdrop => backdrop.key == "eden_gathered_f7"), Is.True);
            Assert.That(System.Array.Exists(set.backdrops, backdrop => backdrop.key == "eden_piston_f3"), Is.True);
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

        [Test]
        public void WhatIsBrightInTheLevelGlows()
        {
            // The level's scene has a glow of its own: a global Volume that blooms what is bright,
            // and a camera that renders it. The standard's other scenes have none.
            var glow = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TilesV2Sample.ParadiseGlowPath);
            Assert.That(glow, Is.Not.Null, "build the Paradise scene (TilesV2Sample.BuildParadise)");
            Assert.That(glow.TryGet<Bloom>(out var bloom), Is.True, "the glow is a bloom");
            Assert.That(bloom.active, Is.True);
            Assert.That(bloom.intensity.overrideState, Is.True);
            Assert.That(bloom.intensity.value, Is.InRange(0.2f, 1f), "soft: it does not wash the picture out");
            Assert.That(bloom.threshold.value, Is.GreaterThan(0.8f), "only what is bright");
            var guid = AssetDatabase.AssetPathToGUID(TilesV2Sample.ParadiseGlowPath);
            var text = System.IO.File.ReadAllText(TilesV2Sample.ParadisePath);
            Assert.That(text, Does.Contain("m_Name: " + TilesV2Sample.GlowName));
            Assert.That(text, Does.Contain(guid), "the scene's Volume uses the glow's profile");
            Assert.That(text, Does.Contain("m_RenderPostProcessing: 1"), "the level's camera renders it");
            foreach (var other in new[] { TilesV2Sample.DescentPath, TilesV2Sample.PlaygroundPath })
            {
                Assert.That(System.IO.File.ReadAllText(other), Does.Not.Contain(guid), other);
            }
        }

        private bool Solid(int column, int row)
        {
            return set.TryGlyph(map.TerrainAt(column, row), out var glyph) && glyph.layer == TilesV2Set.Layer.Terrain;
        }
    }
}
