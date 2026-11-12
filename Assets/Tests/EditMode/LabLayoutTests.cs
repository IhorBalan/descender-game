using System.Collections.Generic;
using System.Linq;
using Descender.Fx;
using Descender.Lab;
using Descender.Player;
using Descender.Save;
using Descender.UI;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The mechanics lab as data: every station is a closed room with a floor under its
    /// spawn, its pieces inside it, pads to its neighbours, and sizes that follow the
    /// hero's tuning. And the placeholder shape it is drawn with.
    /// </summary>
    public class LabLayoutTests
    {
        private PlayerTuning tuning;
        private List<Station> stations;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            stations = LabLayout.Make(tuning);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tuning);
            foreach (var thing in made)
            {
                Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        [Test]
        public void TheStationsAreTheListedOnesInOrder()
        {
            Assert.That(stations.Select(s => s.id), Is.EqualTo(LabLayout.Ids));
            Assert.That(stations.Select(s => s.title), Has.None.Empty);
        }

        [Test]
        public void EveryStationIsARoomWithAFloorUnderItsSpawn()
        {
            foreach (var station in stations)
            {
                var spawn = station.spawn;
                Assert.That(station.room.Contains(spawn), Is.True, $"{station.id}: the spawn is outside the room");
                var body = new Rect(spawn.x - 0.45f, spawn.y + 0.05f, 0.9f, 2f);
                var floors = 0;
                var blocked = 0;
                foreach (var piece in station.pieces)
                {
                    if (piece.kind != PieceKind.Solid)
                    {
                        continue;
                    }
                    var rect = piece.rect;
                    var spans = rect.xMin <= spawn.x - 0.5f && rect.xMax >= spawn.x + 0.5f;
                    if (spans && Mathf.Abs(rect.yMax - spawn.y) < 0.01f)
                    {
                        floors++;
                    }
                    if (rect.Overlaps(body))
                    {
                        blocked++;
                    }
                }
                Assert.That(floors, Is.GreaterThan(0), $"{station.id}: nothing solid under the spawn");
                Assert.That(blocked, Is.Zero, $"{station.id}: the hero would spawn inside something solid");
            }
        }

        [Test]
        public void EveryPieceIsInsideItsRoomAndRoomsDoNotTouch()
        {
            for (var i = 0; i < stations.Count; i++)
            {
                var room = stations[i].room;
                foreach (var piece in stations[i].pieces)
                {
                    var rect = piece.rect;
                    var what = $"{stations[i].id}: '{piece.label}' ({piece.kind}) at {rect}";
                    Assert.That(rect.width, Is.GreaterThan(0f), what + " has no width");
                    Assert.That(rect.height, Is.GreaterThan(0f), what + " has no height");
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(room.xMin - 0.01f), what + " leaves the room");
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(room.xMax + 0.01f), what + " leaves the room");
                    Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(room.yMin - 0.01f), what + " leaves the room");
                    Assert.That(rect.yMax, Is.LessThanOrEqualTo(room.yMax + 0.01f), what + " leaves the room");
                }
                if (i > 0)
                {
                    Assert.That(room.xMin, Is.GreaterThan(stations[i - 1].room.xMax + 5f), "rooms stand apart");
                }
            }
        }

        [Test]
        public void PadsLeadToTheNeighbours()
        {
            for (var i = 0; i < stations.Count; i++)
            {
                var targets = new List<int>();
                foreach (var piece in stations[i].pieces)
                {
                    if (piece.kind == PieceKind.Pad)
                    {
                        targets.Add((int)piece.value);
                    }
                }
                var expected = new List<int>();
                if (i > 0)
                {
                    expected.Add(i - 1);
                }
                if (i < stations.Count - 1)
                {
                    expected.Add(i + 1);
                }
                Assert.That(targets, Is.EquivalentTo(expected), stations[i].id);
            }
        }

        [Test]
        public void TheSizesFollowTheTuning()
        {
            var full = PlayerTuning.Units(tuning.jumpHeight);
            var jump = LabLayout.Find(stations, "jump");
            Assert.That(HasLineAt(jump, LabLayout.FloorTop + full), Is.True, "the line is at the tuned jump height");

            tuning.jumpHeight *= 2f;
            var taller = LabLayout.Find(LabLayout.Make(tuning), "jump");
            Assert.That(HasLineAt(taller, LabLayout.FloorTop + full * 2f), Is.True, "a higher jump moves the line");
            Assert.That(HasLineAt(taller, LabLayout.FloorTop + full), Is.False, "and the old line is gone");
        }

        private static bool HasLineAt(Station station, float y)
        {
            foreach (var piece in station.pieces)
            {
                // The whole-unit ruler lines carry a bare number; the jump's own line names itself.
                var named = piece.label.StartsWith("full jump");
                if (piece.kind == PieceKind.Mark && named && Mathf.Abs(piece.rect.yMin - y) < 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void StationsGiveTheAbilitiesTheirMoveNeeds()
        {
            Assert.That(LabLayout.Find(stations, "run").abilities, Is.Empty);
            Assert.That(LabLayout.Find(stations, "wings").abilities, Is.EqualTo(new[] { Ability.Wings }));
            Assert.That(LabLayout.Find(stations, "dash").abilities, Is.EqualTo(new[] { Ability.LightDash }));
            Assert.That(LabLayout.Find(stations, "wall").abilities, Is.EqualTo(new[] { Ability.WallGrip }));
            Assert.That(LabLayout.Find(stations, "glide").abilities, Does.Contain(Ability.Glide));
            Assert.That(LabLayout.Find(stations, "slam").abilities, Is.EqualTo(new[] { Ability.Smite }));
            Assert.That(LabLayout.Find(stations, "dummy").abilities, Does.Contain(Ability.Smite));
            Assert.That(LabLayout.Find(stations, "sword").abilities, Is.Empty, "so down and attack is the cut");
            Assert.That(LabLayout.Find(stations, "dummy").abilities, Does.Contain(Ability.HolyLight));
            var striker = LabLayout.Find(LabLayout.Find(stations, "hurt"), "striker");
            Assert.That(striker.kind, Is.EqualTo(PieceKind.Striker));
            Assert.That(striker.value, Is.GreaterThan(0f), "it hits on a timer");
            Assert.That(striker.reach.x, Is.GreaterThan(striker.rect.width), "in a reach wider than itself");
            var breaks = LabLayout.Find(stations, "break");
            Assert.That(LabLayout.Find(breaks, "cut").value, Is.EqualTo(3f), "three cuts break it");
            Assert.That(LabLayout.Find(breaks, "slab").by, Is.EqualTo(Triggers.StruckBy.Slam));
            Assert.That(LabLayout.Find(breaks, "far").by, Is.EqualTo(Triggers.StruckBy.Bolt));
            var walker = LabLayout.Find(LabLayout.Find(stations, "foes"), "walker");
            Assert.That(walker.kind, Is.EqualTo(PieceKind.Foe));
            Assert.That(walker.foe.behaviours, Has.Length.EqualTo(1), "a patrol and nothing else");
            Assert.That(walker.foe.behaviours[0], Is.InstanceOf<Enemies.Patrol>());
            var chase = LabLayout.Find(stations, "chase");
            var chaser = LabLayout.Find(chase, "chaser").foe;
            Assert.That(chaser.behaviours[0], Is.InstanceOf<Enemies.Swing>(), "the swing is asked before the chase");
            Assert.That(chaser.behaviours[1], Is.InstanceOf<Enemies.Chase>());
            Assert.That(chaser.behaviours[2], Is.InstanceOf<Enemies.Patrol>(), "and it patrols when it sees no one");
            var ambusher = LabLayout.Find(chase, "ambusher").foe;
            Assert.That(ambusher.behaviours[0], Is.InstanceOf<Enemies.Ambush>(), "nothing acts before it wakes");
            var brute = LabLayout.Find(chase, "brute").foe;
            Assert.That(brute.body.armour, Is.True, "a hit does not stop it");
            foreach (var recipe in new[] { chaser, ambusher, brute })
            {
                var swing = System.Array.Find(recipe.behaviours, act => act is Enemies.Swing) as Enemies.Swing;
                Assert.That(swing, Is.Not.Null, recipe.name);
                Assert.That(swing.windup, Is.GreaterThanOrEqualTo(0.25f), recipe.name + " warns long enough to see");
            }
            var air = LabLayout.Find(stations, "air");
            var diver = LabLayout.Find(air, "diver").foe;
            Assert.That(diver.body.flies, Is.True, "the diver has no gravity");
            Assert.That(diver.behaviours[0], Is.InstanceOf<Enemies.Dive>(), "the dive is asked before the hover");
            Assert.That(diver.behaviours[1], Is.InstanceOf<Enemies.Hover>());
            Assert.That(LabLayout.Find(air, "shooter").foe.behaviours[0], Is.InstanceOf<Enemies.Shoot>());
            var turret = (Enemies.Shoot)LabLayout.Find(air, "turret").foe.behaviours[0];
            Assert.That(turret.level, Is.True, "its bolts fly flat");
            Assert.That(turret.reach, Is.GreaterThanOrEqualTo(turret.range), "and reach as far as it sees");
            var guards = LabLayout.Find(stations, "guard");
            Assert.That(guards.abilities, Does.Contain(Ability.Smite), "the slam is one way past the shield");
            var guard = LabLayout.Find(guards, "guard").foe;
            Assert.That(guard.behaviours[0], Is.InstanceOf<Enemies.Shield>());
            Assert.That(((Enemies.Shield)guard.behaviours[0]).stopsSlam, Is.False);
            var summon = (Enemies.Summon)LabLayout.Find(guards, "summoner").foe.behaviours[0];
            Assert.That(summon.cap, Is.EqualTo(2));
            var makes = summon.summons.behaviours;
            Assert.That(makes, Has.None.InstanceOf<Enemies.Summon>(), "what it makes makes nothing");
            var light = LabLayout.Find(stations, "light");
            Assert.That(light.abilities, Is.EqualTo(new[] { Ability.HolyLight, Ability.Smite }));
            Assert.That(LabLayout.Find(light, "target").by, Is.EqualTo(Triggers.StruckBy.Bolt), "a bolt only");
            Assert.That(LabLayout.Find(light, "plate").by, Is.EqualTo(Triggers.StruckBy.Slam), "the slam only");
        }

        [Test]
        public void TheLabCommandNamesTheStations()
        {
            Assert.That(DevConsole.LabCommand(new[] { "list" }), Does.Contain("run, jump, gaps"));
            Assert.That(DevConsole.LabCommand(new[] { "nowhere" }), Does.StartWith("No station 'nowhere'"));
        }

        [Test]
        public void AShapeIsItsKindSizeAndColour()
        {
            var parent = new GameObject("Parent");
            made.Add(parent);
            var size = new Vector2(2f, 1f);
            var shape = Shape.Make(parent.transform, "Key", ShapeKind.Diamond, new Vector2(3f, 2f), size, Color.red);

            Assert.That(shape.Kind, Is.EqualTo(ShapeKind.Diamond));
            Assert.That(shape.Body.sprite, Is.SameAs(Shapes.Diamond));
            Assert.That(shape.Body.color, Is.EqualTo(Color.red));
            Assert.That(shape.Bounds.center.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(shape.Bounds.size.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(shape.Body.bounds.size.x, Is.EqualTo(2f).Within(0.01f), "drawn as wide as its size");
            Assert.That(shape.Body.bounds.size.y, Is.EqualTo(1f).Within(0.01f), "drawn as tall as its size");

            shape.Set(ShapeKind.Square, new Vector2(4f, 0.5f), Color.blue);
            Assert.That(shape.Body.sprite, Is.SameAs(Shapes.Square));
            Assert.That(shape.Body.bounds.size.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(shape.Body.bounds.size.y, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void EveryShapeKindHasItsOwnSprite()
        {
            var sprites = new List<Sprite>();
            foreach (ShapeKind kind in System.Enum.GetValues(typeof(ShapeKind)))
            {
                sprites.Add(Shape.SpriteOf(kind));
            }
            Assert.That(sprites, Has.None.Null);
            Assert.That(sprites.Distinct().Count(), Is.EqualTo(4));
        }
    }
}
