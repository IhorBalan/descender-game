using System.Collections.Generic;
using Descender.Combat;
using Descender.EditorTools.TilesV2;
using Descender.Save;
using Descender.Triggers;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.Tests
{
    /// <summary>
    /// The Tiles V2 world (docs/art/tiles-v2-world.md): the map names its eleven regions
    /// and marks the game's things; the tile set has every region's ledges, lamps, rock and
    /// air; the world builds with one-way ledges, a lamp for every prop, each region's own
    /// light in its air and rock behind its caves; the marks become the game's checkpoints,
    /// pickups and gates; and the chart shows only what has been seen. That the world can
    /// be played through is checked by the map's maker (tools/tiles_v2/tv2_reach.py).
    /// </summary>
    public class TilesV2WorldTests
    {
        private static readonly string[] RegionKeys =
        {
            "paradise",
            "earth",
            "limbo",
            "lust",
            "gluttony",
            "greed",
            "wrath",
            "heresy",
            "violence",
            "fraud",
            "treachery",
        };

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
            world = new GameObject("Tiles V2 world under test").AddComponent<TilesV2World>();
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
        public void TheMapNamesElevenRegionsTopToBottom()
        {
            Assert.That(map.Regions.Count, Is.EqualTo(RegionKeys.Length));
            for (var i = 0; i < RegionKeys.Length; i++)
            {
                Assert.That(map.Regions[i].Key, Is.EqualTo(RegionKeys[i]));
                Assert.That(set.RegionOf(RegionKeys[i]), Is.Not.Null, RegionKeys[i] + " is not in the tile set");
                Assert.That(map.RegionEnd(i), Is.GreaterThan(map.Regions[i].Top + 30), RegionKeys[i] + " is a sliver");
                Assert.That(map.RegionAt(map.Regions[i].Top), Is.EqualTo(i));
                Assert.That(map.RegionAt(map.RegionEnd(i) - 1), Is.EqualTo(i));
            }
            Assert.That(map.Regions[0].Top, Is.EqualTo(0), "Paradise is the sky");
            Assert.That(map.RegionEnd(RegionKeys.Length - 1), Is.EqualTo(map.Height));
            Assert.That(map.Width * map.Height, Is.GreaterThan(100000), "a world, not a room");
        }

        [Test]
        public void EveryCharacterOfTheMapIsInTheLegend()
        {
            for (var row = 0; row < map.Height; row++)
            {
                for (var column = 0; column < map.Width; column++)
                {
                    var stands = map.TerrainAt(column, row);
                    if (stands != TilesV2Map.Air)
                    {
                        Assert.That(set.TryGlyph(stands, out var glyph), Is.True, $"'{stands}' at {column}, {row}");
                        Assert.That(
                            glyph.layer,
                            Is.Not.EqualTo(TilesV2Set.Layer.Wall),
                            $"a wall in the ground at {column}, {row}"
                        );
                    }
                    var behind = map.WallAt(column, row);
                    if (behind != TilesV2Map.Air)
                    {
                        Assert.That(set.TryGlyph(behind, out var wall), Is.True, $"'{behind}' at {column}, {row}");
                        Assert.That(wall.layer, Is.EqualTo(TilesV2Set.Layer.Wall), $"'{behind}' at {column}, {row}");
                    }
                }
            }
        }

        [Test]
        public void TheMapMarksEveryAbilityOnceAndAPlaceToRestInEveryRegion()
        {
            var abilities = new List<Ability>();
            var rests = new HashSet<int>();
            var ends = 0;
            foreach (var mark in map.Marks)
            {
                // A relic may lie under water; nothing stands in the rock.
                if (mark.Kind != TilesV2Things.Wind)
                {
                    // A wind's mark is the corner of the air it blows in, which may be in the roof.
                    Assert.That(Solid(mark.Column, mark.Row), Is.False, mark.Kind + " stands in the rock");
                }
                switch (mark.Kind)
                {
                    case TilesV2Things.AbilityKind:
                        Assert.That(
                            System.Enum.TryParse<Ability>(mark.Word(0), out var ability),
                            Is.True,
                            mark.Word(0)
                        );
                        abilities.Add(ability);
                        Assert.That(
                            Solid(mark.Column, mark.Row + 1),
                            Is.True,
                            "a shrine's plinth is under the ability"
                        );
                        break;
                    case TilesV2Things.Checkpoint:
                        rests.Add(map.RegionAt(mark.Row));
                        Assert.That(Solid(mark.Column, mark.Row + 1), Is.True, "a checkpoint stands on the ground");
                        break;
                    case TilesV2Things.Barrier:
                        Assert.That(mark.Number(0, 0), Is.GreaterThan(1), "a barrier's height");
                        break;
                    case TilesV2Things.Seal:
                        Assert.That(mark.Word(0), Is.EqualTo("bolt").Or.EqualTo("slam"));
                        Assert.That(mark.Number(1, 0) * mark.Number(2, 0), Is.GreaterThan(1), "a seal's size");
                        break;
                    case TilesV2Things.End:
                        ends++;
                        Assert.That(map.RegionAt(mark.Row), Is.EqualTo(0), "the journey ends in Paradise");
                        break;
                }
            }
            Assert.That(abilities, Is.EquivalentTo((Ability[])System.Enum.GetValues(typeof(Ability))));
            Assert.That(ends, Is.EqualTo(1));
            for (var i = 0; i < RegionKeys.Length; i++)
            {
                Assert.That(rests.Contains(i), Is.True, "nowhere to rest in " + RegionKeys[i]);
            }
        }

        [Test]
        public void TheTileSetHasEveryRegionsLedgesLampsRockAndAir()
        {
            Assert.That(set.ledges.Length, Is.EqualTo(4));
            foreach (var ledge in set.ledges)
            {
                foreach (var piece in new[] { ledge.single, ledge.left, ledge.middle, ledge.right })
                {
                    Assert.That(piece, Is.InstanceOf<Tile>(), ledge.key);
                    Assert.That(((Tile)piece).colliderType, Is.EqualTo(Tile.ColliderType.Sprite), ledge.key);
                }
                Assert.That(ledge.Piece(false, false), Is.SameAs(ledge.single));
                Assert.That(ledge.Piece(false, true), Is.SameAs(ledge.left));
                Assert.That(ledge.Piece(true, true), Is.SameAs(ledge.middle));
                Assert.That(ledge.Piece(true, false), Is.SameAs(ledge.right));
            }
            Assert.That(set.props.Length, Is.EqualTo(4));
            foreach (var prop in set.props)
            {
                Assert.That(prop.frames, Is.Not.Empty.And.None.Null, prop.key);
                Assert.That(prop.intensity, Is.GreaterThan(0.5f), prop.key);
            }
            Assert.That(set.PropOf("torch").embers, Is.True);
            Assert.That(set.PropOf("crystal").embers, Is.False);
            foreach (var name in new[] { "barrier_0", "barrier_1", "seal_bolt", "seal_slam", "relic_0", "relic_1" })
            {
                Assert.That(set.Thing(name), Is.Not.Null, name);
            }
            Assert.That(set.regions.Length, Is.EqualTo(RegionKeys.Length));
            foreach (var region in set.regions)
            {
                var sky = region.key == "paradise";
                Assert.That(region.name, Is.Not.Empty, region.key);
                Assert.That(
                    region.cave == null,
                    Is.EqualTo(sky),
                    region.key + ": rock behind every region but the sky"
                );
                Assert.That(region.caveTop == null, Is.EqualTo(sky), region.key);
                var lit = region.key != "paradise" && region.key != "earth";
                Assert.That(
                    region.ambient.maxColorComponent > 0f,
                    Is.EqualTo(lit),
                    region.key + ": the circles have a light of their own"
                );
            }
        }

        [Test]
        public void TheWorldLaysOneWayLedgesAndALampForEveryProp()
        {
            var ledges = 0;
            var props = 0;
            var quiet = 0;
            for (var row = 0; row < map.Height; row++)
            {
                for (var column = 0; column < map.Width; column++)
                {
                    if (!set.TryGlyph(map.TerrainAt(column, row), out var glyph))
                    {
                        continue;
                    }
                    ledges += glyph.layer == TilesV2Set.Layer.Ledge ? 1 : 0;
                    if (glyph.layer == TilesV2Set.Layer.Prop)
                    {
                        props++;
                        quiet += set.PropOf(glyph.key).embers ? 0 : 1;
                    }
                }
            }
            Assert.That(ledges, Is.GreaterThan(300), "the shafts and the Pit are climbed on ledges");
            Assert.That(props, Is.GreaterThan(150));
            Assert.That(world.Ledges, Is.Not.Null);
            world.Ledges.CompressBounds();
            var laid = 0;
            foreach (var cell in world.Ledges.cellBounds.allPositionsWithin)
            {
                laid += world.Ledges.HasTile(cell) ? 1 : 0;
            }
            Assert.That(laid, Is.EqualTo(ledges));
            Assert.That(world.Ledges.gameObject.layer, Is.EqualTo(Layers.Ledge));
            var effector = world.Ledges.GetComponent<PlatformEffector2D>();
            Assert.That(effector, Is.Not.Null);
            Assert.That(effector.useOneWay, Is.True, "jumped up through");
            Assert.That(effector.useColliderMask, Is.False, "the hero drops through by the layer pair");
            Assert.That(world.Ledges.GetComponent<CompositeCollider2D>().usedByEffector, Is.True);
            foreach (var cell in world.Ledges.cellBounds.allPositionsWithin)
            {
                Assert.That(
                    world.Terrain.HasTile(cell) && world.Ledges.HasTile(cell),
                    Is.False,
                    "a ledge in the ground"
                );
            }
            var flames = world.Torches.GetComponentsInChildren<TorchFlame>();
            Assert.That(flames.Length, Is.EqualTo(props));
            var calm = 0;
            foreach (var flame in flames)
            {
                calm += flame.Embers ? 0 : 1;
                Assert.That(flame.Emission.maxColorComponent, Is.GreaterThan(0.5f), flame.name);
            }
            Assert.That(calm, Is.EqualTo(quiet), "no embers from a lantern or a crystal");
        }

        [Test]
        public void EachRegionsAirHasItsOwnLightTurningIntoTheNextOnes()
        {
            var rows = world.AirLight();
            Assert.That(rows.Length, Is.EqualTo(map.Height));
            for (var i = 0; i < RegionKeys.Length; i++)
            {
                var middle = (map.Regions[i].Top + map.RegionEnd(i)) / 2;
                var ambient = set.RegionOf(RegionKeys[i]).ambient;
                var here = rows[map.Height - 1 - middle];
                Assert.That(here.r, Is.EqualTo(ambient.r).Within(1e-4f), RegionKeys[i]);
                Assert.That(here.b, Is.EqualTo(ambient.b).Within(1e-4f), RegionKeys[i]);
            }
            // Across the edge between Limbo and Lust the red comes in a step at a time.
            var edge = map.Regions[3].Top;
            var last = rows[map.Height - 1 - (edge - TilesV2World.RegionBlend - 1)].r;
            for (var row = edge - TilesV2World.RegionBlend; row < edge + TilesV2World.RegionBlend; row++)
            {
                var now = rows[map.Height - 1 - row].r;
                Assert.That(now, Is.GreaterThanOrEqualTo(last - 1e-5f), "row " + row);
                Assert.That(now - last, Is.LessThan(0.03f), "no step in the light at row " + row);
                last = now;
            }
            // And the tile light carries it: the air of a hall in Treachery is lit, deep under any sky.
            Assert.That(world.TryView("treachery", out var hall), Is.True);
            var lit = world.Light.At(world.Terrain.WorldToCell(hall));
            Assert.That(lit.b, Is.GreaterThan(0.2f), "the frozen lake's own light");
            Assert.That(lit.b, Is.GreaterThan(lit.r), "and it is blue");
        }

        [Test]
        public void TheRockBehindTheCavesIsEachRegionsOwn()
        {
            var masks = world.GetComponentsInChildren<SpriteMask>();
            Assert.That(masks.Length, Is.EqualTo(RegionKeys.Length - 1), "every region under the sky");
            var ranges = new List<(int back, int front)>();
            foreach (var mask in masks)
            {
                Assert.That(mask.isCustomRangeActive, Is.True, mask.name + " cuts its own rock only");
                foreach (var (back, front) in ranges)
                {
                    Assert.That(
                        mask.backSortingOrder >= front || mask.frontSortingOrder <= back,
                        Is.True,
                        mask.name + " cuts another region's rock too"
                    );
                }
                ranges.Add((mask.backSortingOrder, mask.frontSortingOrder));
            }
            var rocks = 0;
            var far = 0;
            foreach (var layer in world.GetComponentsInChildren<ParallaxLayer>())
            {
                var picture = layer.GetComponent<SpriteRenderer>();
                if (picture.maskInteraction != SpriteMaskInteraction.VisibleInsideMask)
                {
                    continue;
                }
                rocks += layer.name.Contains("far") ? 0 : 1;
                far += layer.name.Contains("far") ? 1 : 0;
                var cut = ranges.FindAll(r => picture.sortingOrder > r.back && picture.sortingOrder <= r.front);
                Assert.That(cut.Count, Is.EqualTo(1), layer.name + " is cut by one mask, its own");
                Assert.That(picture.sharedMaterial, Is.SameAs(set.litMaterial), layer.name + " takes the tile light");
            }
            Assert.That(rocks, Is.EqualTo(masks.Length));
            Assert.That(far, Is.EqualTo(masks.Length), "something stands far off in every region under the sky");
            foreach (var region in set.regions)
            {
                Assert.That(region.far == null, Is.EqualTo(region.cave == null), region.key);
            }
        }

        [Test]
        public void OnlyTheAirOfTheRegionInViewGives()
        {
            var air = world.GetComponentInChildren<TilesV2Air>();
            Assert.That(air, Is.Not.Null);
            Assert.That(
                air.Count,
                Is.EqualTo(RegionKeys.Length - 1),
                "every region but the Earth has something in its air"
            );
            Assert.That(world.TryView("lust", out var tempest), Is.True);
            air.Follow(tempest);
            var giving = 0;
            for (var i = 0; i < air.Count; i++)
            {
                var system = air.SystemAt(i, out var span);
                var inside = tempest.y >= span.x && tempest.y < span.y;
                Assert.That(system.emission.enabled, Is.EqualTo(inside), system.name);
                giving += inside ? 1 : 0;
            }
            Assert.That(giving, Is.EqualTo(1));
            Assert.That(air.transform.position.x, Is.EqualTo(tempest.x).Within(1e-3f), "the air rides with the view");
        }

        [Test]
        public void TheGamesThingsStandWhereTheMapMarksThem()
        {
            var counts = new Dictionary<string, int>();
            foreach (var mark in map.Marks)
            {
                counts[mark.Kind] = counts.TryGetValue(mark.Kind, out var n) ? n + 1 : 1;
            }
            Pickups.Checkpoint.Reset();
            var things = TilesV2Things.Build(
                world,
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.CheckpointPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.AbilityPath)
            );
            try
            {
                Assert.That(things.Checkpoints, Is.EqualTo(counts[TilesV2Things.Checkpoint]));
                Assert.That(things.Abilities, Is.EqualTo(6));
                Assert.That(things.Relics, Is.EqualTo(counts[TilesV2Things.Relic]));
                Assert.That(things.Relics, Is.GreaterThan(15), "every circle hides something");
                Assert.That(things.Gates, Is.EqualTo(counts[TilesV2Things.Barrier] + counts[TilesV2Things.Seal]));
                Assert.That(things.Secrets, Is.EqualTo(counts[TilesV2Things.Secret]));
                Assert.That(things.Secrets, Is.GreaterThan(10), "walls that are not walls");
                Assert.That(things.Platforms, Is.EqualTo(counts[TilesV2Things.Platform]));
                Assert.That(things.Winds, Is.EqualTo(counts[TilesV2Things.Wind]));
                Assert.That(things.Winds, Is.GreaterThan(2), "the tempest blows");
                var crumbles = things.GetComponentsInChildren<Crumble>().Length;
                var blinkers = things.GetComponentsInChildren<Blinker>().Length;
                Assert.That(crumbles + blinkers, Is.EqualTo(things.Platforms));
                Assert.That(crumbles, Is.GreaterThan(0), "something gives way underfoot");
                Assert.That(blinkers, Is.GreaterThan(0), "something comes and goes");
                Assert.That(things.GetComponentsInChildren<ForceZone>().Length, Is.EqualTo(things.Winds));
                var pits = 0;
                foreach (var thing in things.All)
                {
                    if (thing.Kind == TilesV2Things.Secret)
                    {
                        var wall = thing.Made.GetComponent<Breakable>();
                        Assert.That(wall.By, Is.EqualTo(StruckBy.Any), "the sword breaks it");
                        Assert.That(
                            world.Terrain.HasTile((Vector3Int)thing.Cell),
                            Is.False,
                            "the wall is the game's, not a tile"
                        );
                        Assert.That(thing.Hidden.width * thing.Hidden.height, Is.GreaterThan(8), "a room is behind it");
                        Assert.That(
                            thing.Made.GetComponentInChildren<SpriteRenderer>().sprite,
                            Is.Not.Null,
                            "it looks like rock"
                        );
                    }
                    pits += thing.Kind == TilesV2Things.Checkpoint && thing.InPit ? 1 : 0;
                }
                Assert.That(pits, Is.GreaterThanOrEqualTo(10), "a place to rest in every antechamber of the Pit");
                var found = new HashSet<Ability>();
                foreach (var pickup in things.GetComponentsInChildren<Pickups.AbilityPickup>())
                {
                    found.Add(pickup.Ability);
                }
                Assert.That(found.Count, Is.EqualTo(6), "each ability once");
                var bolts = 0;
                var slams = 0;
                foreach (var seal in things.GetComponentsInChildren<Breakable>())
                {
                    if (seal.By == StruckBy.Any)
                    {
                        continue;
                    }
                    Assert.That(seal.Hits, Is.EqualTo(1));
                    Assert.That(seal.gameObject.layer, Is.EqualTo(Layers.World), "a seal stands in the way");
                    Assert.That(seal.GetComponent<BoxCollider2D>().isTrigger, Is.False);
                    var struck = seal.transform.Find("Struck");
                    Assert.That(struck.gameObject.layer, Is.EqualTo(Layers.Enemy), "where the hero's strikes look");
                    bolts += seal.By == StruckBy.Bolt ? 1 : 0;
                    slams += seal.By == StruckBy.Slam ? 1 : 0;
                }
                Assert.That(bolts, Is.GreaterThan(3));
                Assert.That(slams, Is.GreaterThan(3));
                var veils = things.GetComponentsInChildren<TilesV2Veil>();
                Assert.That(veils.Length, Is.EqualTo(counts[TilesV2Things.Barrier]));
                foreach (var veil in veils)
                {
                    Assert.That(veil.gameObject.layer, Is.EqualTo(Layers.LightBarrier), "what a Light Dash passes");
                    var box = veil.GetComponent<BoxCollider2D>();
                    var cell = world.Terrain.WorldToCell(veil.transform.position + new Vector3(0.5f, -0.5f, 0f));
                    Assert.That(world.Terrain.HasTile(cell), Is.False, "a barrier stands in the road, not in rock");
                    Assert.That(
                        world.Terrain.HasTile(cell + new Vector3Int(0, -(int)box.size.y, 0)),
                        Is.True,
                        "down to the floor"
                    );
                    Assert.That(world.Terrain.HasTile(cell + Vector3Int.up), Is.True, "up to the roof");
                }
            }
            finally
            {
                Object.DestroyImmediate(things.gameObject);
                Pickups.Checkpoint.Reset();
            }
        }

        [Test]
        public void TheChartShowsOnlyWhatHasBeenSeen()
        {
            var chart = new TilesV2Chart(world);
            try
            {
                Assert.That(chart.Picture.width, Is.EqualTo(map.Width));
                Assert.That(chart.Picture.height, Is.EqualTo(map.Height));
                Assert.That(chart.Seen, Is.EqualTo(0));
                var cell = world.Terrain.WorldToCell(world.Spawn);
                var at = new Vector2Int(cell.x, cell.y);
                Assert.That(chart.HasSeen(at), Is.False, "it does not know the land before the hero does");
                chart.Reveal(at);
                Assert.That(chart.HasSeen(at), Is.True);
                var round = Mathf.PI * TilesV2Chart.Sight * TilesV2Chart.Sight;
                Assert.That(chart.Seen, Is.InRange(round * 0.9f, round * 1.1f), "a circle of sight");
                Assert.That(chart.HasSeen(at + new Vector2Int(TilesV2Chart.Sight + 2, 0)), Is.False);
                chart.Show();
                Assert.That(chart.Picture.GetPixel(at.x, at.y).a, Is.EqualTo(1f).Within(0.01f));
                Assert.That(chart.Picture.GetPixel(at.x + 40, at.y).a, Is.LessThan(1f), "the unseen is veiled");
                // A room behind a wall that is not one stays rock on the chart until the wall is down.
                var room = new RectInt(at.x + 2, at.y + 2, 3, 2);
                var inside = new Vector2Int(at.x + 3, at.y + 2);
                var open = chart.Picture.GetPixel(inside.x, inside.y);
                chart.Hide(room);
                chart.Show();
                Assert.That(chart.IsHidden(inside), Is.True);
                Assert.That(
                    chart.Picture.GetPixel(inside.x, inside.y),
                    Is.Not.EqualTo(open),
                    "hidden, it shows as rock"
                );
                chart.Unhide(room);
                chart.Show();
                Assert.That(chart.Picture.GetPixel(inside.x, inside.y), Is.EqualTo(open));
                chart.RevealAll();
                Assert.That(chart.Seen, Is.EqualTo(map.Width * map.Height));
            }
            finally
            {
                Object.DestroyImmediate(chart.Picture);
            }
        }

        [Test]
        public void TheWorldSceneIsAJourneyAndStaysOutOfTheGamesBuild()
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                Assert.That(scene.path, Is.Not.EqualTo(TilesV2Sample.DescentPath));
            }
            var text = System.IO.File.ReadAllText(TilesV2Sample.DescentPath);
            Assert.That(text, Does.Contain("journey: 1"), "the hero starts with nothing");
            Assert.That(
                text,
                Does.Not.Contain("m_Name: " + TilesV2World.BuiltName),
                "nothing of the world is saved in the scene"
            );
            Assert.That(text.Length, Is.LessThan(40000), "the scene is a few objects");
        }

        private bool Solid(int column, int row)
        {
            return set.TryGlyph(map.TerrainAt(column, row), out var glyph) && glyph.layer == TilesV2Set.Layer.Terrain;
        }
    }
}
