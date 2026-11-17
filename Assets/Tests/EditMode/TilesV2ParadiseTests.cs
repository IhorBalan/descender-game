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
            // And the corners of the view are drawn in a little: the eye is held to its middle.
            Assert.That(glow.TryGet<Vignette>(out var corners), Is.True);
            Assert.That(corners.intensity.overrideState, Is.True);
            Assert.That(corners.intensity.value, Is.InRange(0.05f, 0.3f), "a little: it is not a tunnel");
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

        [Test]
        public void NothingIsABareBarOrAPlankOnTheAir()
        {
            // What is stood where: counted by what its key begins with.
            var stood = new Dictionary<string, int>();
            var corbelled = new HashSet<(int column, int row)>();
            var kinds = new[]
            {
                "eden_corbel_",
                "eden_post_",
                "eden_lantern_trunk",
                "eden_lantern_root",
                "eden_crown_trunk",
                "eden_inlay_",
                "eden_gloom_",
                "heaven_puff_",
            };
            foreach (var mark in map.Marks)
            {
                if (mark.Kind != TilesV2Things.Stood)
                {
                    continue;
                }
                var key = mark.Word(0);
                foreach (var kind in kinds)
                {
                    if (key.StartsWith(kind))
                    {
                        stood[kind] = stood.TryGetValue(kind, out var n) ? n + 1 : 1;
                    }
                }
                if (key.StartsWith("eden_corbel_"))
                {
                    corbelled.Add((mark.Column, mark.Row));
                    Assert.That(Ledge(mark.Column, mark.Row), Is.True, key + " hangs under no ledge");
                    var ledge = set.LedgeOf(Glyph(mark.Column, mark.Row).key);
                    Assert.That(key, Does.Contain(ledge.key), "a ledge sits on what is of its own make");
                }
            }
            // Every ledge of two tiles or more has something under it, hung from its own cell.
            var ledges = 0;
            for (var row = 1; row < map.Height - 1; row++)
            {
                for (var column = 1; column < map.Width - 1; column++)
                {
                    if (!Ledge(column, row) || Ledge(column - 1, row))
                    {
                        continue;
                    }
                    var end = column;
                    var held = false;
                    var open = true;
                    while (Ledge(end, row))
                    {
                        held |= corbelled.Contains((end, row));
                        open &= !Solid(end, row + 1);
                        end++;
                    }
                    if (end - column >= 2 && open)
                    {
                        ledges++;
                        Assert.That(held, Is.True, "the ledge at " + column + ", " + row + " is a plank on the air");
                    }
                }
            }
            Assert.That(ledges, Is.GreaterThan(300));
            // What holds a garden's platforms up is a picture of its own, not a bar of wall; the
            // two great trunks are pictures a storey tall, stood one over another.
            Assert.That(stood["eden_post_"], Is.GreaterThan(40));
            Assert.That(
                stood["eden_lantern_trunk"],
                Is.GreaterThanOrEqualTo(8),
                "the Lantern Tree is as tall as its room"
            );
            Assert.That(stood["eden_lantern_root"], Is.EqualTo(1));
            Assert.That(stood["eden_crown_trunk"], Is.GreaterThanOrEqualTo(12), "the Crown Vine too");
            // Bigger than a tile in every zone's ground; the dark under the roofs; billows where ground lies on cloud.
            Assert.That(stood["eden_inlay_"], Is.GreaterThan(200));
            Assert.That(stood["eden_gloom_"], Is.GreaterThan(100));
            Assert.That(stood["heaven_puff_"], Is.GreaterThan(500));
            foreach (var kind in new[] { "earth", "loam", "vinewood", "tilth", "marble", "pearl", "silver" })
            {
                Assert.That(
                    System.Array.Exists(set.backdrops, backdrop => backdrop.key == "eden_inlay_" + kind + "_0"),
                    Is.True,
                    kind
                );
            }
        }

        [Test]
        public void TheGreatThingsAreSeenWholeInRoomsComposedRoundThem()
        {
            // Between the eye and the Low Gate, and the Making-house's door, there is no floor:
            // their rooms' platforms are tiers to either side. (The Wheel's room keeps the floors
            // the plan needs in its middle, under the hub.)
            foreach (var view in new[] { "eden_gate", "eden_door" })
            {
                Assert.That(map.Views.TryGetValue(view, out var at), Is.True, view);
                var middle = Mathf.FloorToInt(at.x);
                var crossed = 0;
                // (From under the room's roof to over its floor: the two storeys the view shows.)
                for (var row = Mathf.FloorToInt(at.y) - 10; row < Mathf.FloorToInt(at.y) + 4; row++)
                {
                    for (var column = middle - 6; column <= middle + 6; column++)
                    {
                        crossed += Solid(column, row) ? 1 : 0;
                    }
                }
                Assert.That(crossed, Is.EqualTo(0), view + ": a floor crosses the great thing of the room");
            }
        }

        [Test]
        public void EveryZoneIsSeenInItsOwnLook()
        {
            Pickups.Checkpoint.Reset();
            var things = TilesV2Things.Build(
                world,
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.CheckpointPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.AbilityPath)
            );
            try
            {
                Assert.That(things.Grades.Count, Is.EqualTo(69), "every room has its look");
                Assert.That(things.Looks.Count, Is.EqualTo(7), "a look for each zone");
                foreach (var look in things.Looks)
                {
                    Assert.That(look.isGlobal, Is.True);
                    Assert.That(look.weight, Is.EqualTo(0f), "no look shows until a camera is somewhere");
                    Assert.That(look.sharedProfile.TryGet<ColorAdjustments>(out var adjust), Is.True);
                    Assert.That(adjust.contrast.overrideState && adjust.saturation.overrideState, Is.True);
                    Assert.That(
                        Mathf.Abs(adjust.contrast.value),
                        Is.LessThanOrEqualTo(20f),
                        "a turn of the dial, not a filter"
                    );
                    Assert.That(Mathf.Abs(adjust.saturation.value), Is.LessThanOrEqualTo(20f));
                    Assert.That(Mathf.Abs(adjust.postExposure.value), Is.LessThanOrEqualTo(0.2f));
                }
                // In a room, its zone's look is whole and it is the only one.
                Assert.That(world.TryView("eden_hollow", out var hollow), Is.True);
                things.Look(hollow);
                var whole = 0;
                var total = 0f;
                foreach (var look in things.Looks)
                {
                    whole += Mathf.Approximately(look.weight, 1f) ? 1 : 0;
                    total += look.weight;
                }
                Assert.That(whole, Is.EqualTo(1));
                Assert.That(total, Is.EqualTo(1f).Within(0.001f), "the Hollow is far from every other zone");
                Assert.That(things.TryGrade(hollow, out var dawn, out var rose), Is.True);
                Assert.That(rose.r, Is.GreaterThan(rose.b), "the Hollow is seen through the rose of the dawn");
                Assert.That(world.TryView("eden_gate", out var gate), Is.True);
                Assert.That(things.TryGrade(gate, out var dusk, out var cold), Is.True);
                Assert.That(cold.b, Is.GreaterThan(cold.r), "the Gate through the cold");
                Assert.That(dusk.z, Is.LessThan(dawn.z), "and with less colour in it");
                // Outside every room no look shows; a little way out of one, a part of its look.
                things.Look(new Vector2(-200f, -200f));
                foreach (var look in things.Looks)
                {
                    Assert.That(look.weight, Is.EqualTo(0f));
                }
                var area = things.Grades[0].area;
                things.Look(new Vector2(area.xMin - TilesV2Things.GradeBlend * 0.5f, area.center.y));
                var most = 0f;
                foreach (var look in things.Looks)
                {
                    most = Mathf.Max(most, look.weight);
                }
                Assert.That(most, Is.GreaterThan(0.2f).And.LessThanOrEqualTo(1f));
                things.Look(hollow);
                things.LookAway();
                foreach (var look in things.Looks)
                {
                    Assert.That(look.weight, Is.EqualTo(0f), "a photograph of the whole map is in no room");
                }
            }
            finally
            {
                Object.DestroyImmediate(things.gameObject);
                Pickups.Checkpoint.Reset();
            }
        }

        [Test]
        public void WhatGrowsOnTheFloorsBendsAsTheHeroGoesThroughItAndSpringsBack()
        {
            var life = world.Life;
            Assert.That(life.Brush.Count, Is.GreaterThan(300));
            Assert.That(life.Made.ContainsKey("brush"), Is.True);
            var stem = life.Brush[0];
            Vector2 at = stem.position;
            Assert.That(stem.localRotation, Is.EqualTo(Quaternion.identity));
            // He comes at it from its left, running: it leans over to the right, away from him.
            for (var step = 0; step < 40; step++)
            {
                life.Bend(at + new Vector2(-0.4f, 0f), 6f, 0.02f);
            }
            Assert.That(life.LeanOf(0), Is.LessThan(-8f), "it leans away from him");
            Assert.That(Mathf.Abs(life.LeanOf(0)), Is.LessThanOrEqualTo(TilesV2Life.BrushLean * 1.5f));
            Assert.That(stem.localRotation.eulerAngles.z, Is.GreaterThan(300f), "and is turned about its foot");
            // From its right, to the left.
            for (var step = 0; step < 80; step++)
            {
                life.Bend(at + new Vector2(0.4f, 0f), -6f, 0.02f);
            }
            Assert.That(life.LeanOf(0), Is.GreaterThan(8f));
            // A slow hero bends it less than one who runs.
            var ran = Mathf.Abs(life.LeanOf(0));
            for (var step = 0; step < 300; step++)
            {
                life.Bend(at + new Vector2(0.4f, 0f), 0f, 0.02f);
            }
            Assert.That(Mathf.Abs(life.LeanOf(0)), Is.LessThan(ran));
            Assert.That(
                Mathf.Abs(life.LeanOf(0)),
                Is.GreaterThan(0.5f),
                "it is still pushed aside while he stands in it"
            );
            // He has gone by: it springs back and comes to rest.
            for (var step = 0; step < 600; step++)
            {
                life.Bend(at + new Vector2(30f, 0f), 6f, 0.02f);
            }
            Assert.That(Mathf.Abs(life.LeanOf(0)), Is.LessThan(0.5f), "it has come to rest");
            // One a long way off never stirred.
            Assert.That(life.LeanOf(life.Brush.Count - 1), Is.EqualTo(0f));
        }

        private bool Ledge(int column, int row)
        {
            return set.TryGlyph(map.TerrainAt(column, row), out var glyph) && glyph.layer == TilesV2Set.Layer.Ledge;
        }

        private TilesV2Set.Glyph Glyph(int column, int row)
        {
            set.TryGlyph(map.TerrainAt(column, row), out var glyph);
            return glyph;
        }

        private bool Solid(int column, int row)
        {
            return set.TryGlyph(map.TerrainAt(column, row), out var glyph) && glyph.layer == TilesV2Set.Layer.Terrain;
        }
    }
}
