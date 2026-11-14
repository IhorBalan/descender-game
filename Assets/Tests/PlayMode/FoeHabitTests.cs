using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Enemies;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The old walkers' habits as parts: a pounce warns, leaves the ground, hurts once on
    /// the way and lands, and a hit during the warning keeps it down; a hurler backs away
    /// to where it throws from and stands at an edge; a quake sends a wave along the
    /// floor that a wall stops and that hurts a hero standing in its way.
    /// </summary>
    public class FoeHabitTests
    {
        private const float Floor = 2f;
        private const float HalfHero = 0.4375f;
        private const float Pixel = 1f / PlayerTuning.PixelsPerUnit;
        private const float HeroAt = 10f;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private Station room;
        private LabRoot lab;
        private PlayerController hero;
        private Rigidbody2D heroBody;
        private ScriptedInput input;
        private MoveProbe probe;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            LiveTuning.Forget();
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            room = new Station();
            room.id = "test";
            room.title = "test";
            room.room = new Rect(0f, 0f, 56f, 30f);
            room.spawn = new Vector2(40f, Floor);
            Put(0f, 0f, 56f, Floor);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = captureBefore;
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.Destroy(thing);
                }
            }
            made.Clear();
        }

        private void Put(float x, float y, float width, float height)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Solid;
            piece.rect = new Rect(x, y, width, height);
            room.pieces.Add(piece);
        }

        private IEnumerator Begin(Vector2 at)
        {
            var stations = new List<Station>();
            stations.Add(room);
            lab = LabBuilder.Build(stations);
            made.Add(lab.gameObject);
            var go = new GameObject("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            made.Add(go);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            heroBody = go.GetComponent<Rigidbody2D>();
            input = new ScriptedInput();
            hero.InputSource = input;
            probe = new MoveProbe(hero, input);
            yield return null;
            lab.Enter(hero, 0);
            hero.PlaceAt(at);
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, "he stands where the test put him");
            // Arriving in a room leaves him safe for a while; the tests start once he can be hurt.
            yield return probe.Until(() => hero.CanBeHurt, 100);
            Assert.That(hero.CanBeHurt, Is.True);
        }

        private Transform Built(Piece piece) => lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));

        private Piece PutFoe(float x, float y, float width, float height, FoeBody with, params FoeBehaviour[] acts)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Foe;
            piece.rect = new Rect(x, y, width, height);
            piece.foe = new FoeRecipe("foe", with, acts);
            room.pieces.Add(piece);
            return piece;
        }

        private Foe FoeOf(Piece piece) => Built(piece).GetComponent<Foe>();

        private static T Act<T>(Foe foe)
            where T : FoeBehaviour
        {
            foreach (var act in foe.Behaviours)
            {
                if (act is T found)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>A body that does nothing by touch, so only its attacks can hurt.</summary>
        private static FoeBody Harmless(bool armour = false)
        {
            return new FoeBody
            {
                health = 9,
                contactDamage = 0,
                armour = armour,
            };
        }

        [UnityTest]
        public IEnumerator APounceWarnsLeavesTheGroundHurtsOnceOnTheWayAndLands()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Pounce());
            yield return Begin(new Vector2(40f, Floor));
            var foe = FoeOf(piece);
            var pounce = Act<Pounce>(foe);
            var full = hero.Health;
            yield return probe.Step(20);
            Assert.That(foe.IsTelegraphing, Is.False, "too far off it does nothing");

            // One unit from its middle: nearer than a pounce starts from.
            hero.PlaceAt(new Vector2(21.5f, Floor));
            yield return probe.Step(10);
            Assert.That(foe.IsTelegraphing, Is.False, "nor too near");

            // Four units: 64 pixels, inside 36 to 96.
            hero.PlaceAt(new Vector2(24.5f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(pounce.IsWindingUp, Is.True, "a few steps off, it warns");
            Assert.That(foe.Facing, Is.EqualTo(1), "facing him");
            var ground = foe.Box.bounds.min.y;
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(hero.Health, Is.EqualTo(full), "nothing hurts while it warns");
                Assert.That(foe.Box.bounds.min.y, Is.EqualTo(ground).Within(Pixel), "and it stays down");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(pounce.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(pounce.IsLeaping, Is.True, "then it leaps");

            var peak = ground;
            steps = 0;
            while (pounce.IsLeaping && steps < 100)
            {
                yield return probe.Step();
                peak = Mathf.Max(peak, foe.Box.bounds.min.y);
                steps++;
            }
            // 190 pixels a second up against 700 a second squared: about 26 pixels.
            Assert.That(peak - ground, Is.InRange(1.3f, 1.8f), "off the ground");
            Assert.That(foe.IsGrounded, Is.True, "and lands");
            Assert.That(foe.Box.bounds.center.x, Is.GreaterThan(23f), "further on");
            Assert.That(hero.Health, Is.EqualTo(full - pounce.damage), "having hurt him once on the way");
        }

        [UnityTest]
        public IEnumerator AHitDuringTheWarningKeepsAPounceDown()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Pounce());
            yield return Begin(new Vector2(40f, Floor));
            var foe = FoeOf(piece);
            var pounce = Act<Pounce>(foe);
            var full = hero.Health;
            hero.PlaceAt(new Vector2(24.5f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(pounce.IsWindingUp, Is.True);
            var ground = foe.Box.bounds.min.y;
            yield return probe.Step(5);
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, hero.transform.position)), Is.True);
            Assert.That(foe.IsTelegraphing, Is.False, "a hit ends the warning");
            Assert.That(pounce.IsBusy, Is.False, "and the pounce");
            var peak = ground;
            for (var i = 0; i < 40; i++)
            {
                yield return probe.Step();
                peak = Mathf.Max(peak, foe.Box.bounds.min.y);
            }
            Assert.That(peak, Is.EqualTo(ground).Within(2f * Pixel), "it never left the ground");
            Assert.That(hero.Health, Is.EqualTo(full));
        }

        [UnityTest]
        public IEnumerator AHurlerBacksAwayToWhereItThrowsFromAndStandsAtAnEdge()
        {
            Put(30f, Floor, 6f, 2f);
            var piece = PutFoe(32.5f, Floor + 2f, 1f, 1.5f, Harmless(), new Shoot { nearest = 48f }, new Retreat());
            yield return Begin(new Vector2(50f, Floor));
            var foe = FoeOf(piece);
            var shoot = Act<Shoot>(foe);
            var back = Act<Retreat>(foe);
            yield return probe.Step(20);
            Assert.That(back.IsRetreating, Is.False, "with no one near it stands");
            Assert.That(shoot.Shots, Is.EqualTo(0));

            // On its platform, two units from its middle: nearer than it throws from.
            hero.PlaceAt(new Vector2(31f, Floor + 2f));
            yield return probe.Step(3);
            Assert.That(back.IsRetreating, Is.True, "too near, it backs away");
            Assert.That(foe.Facing, Is.EqualTo(1), "its back to him");
            yield return probe.Until(() => shoot.IsAttacking, 100);
            Assert.That(shoot.IsAttacking, Is.True, "until it is far enough to throw");
            Assert.That(shoot.Shots, Is.EqualTo(0), "it threw nothing while he was near");
            var gap = (foe.Box.bounds.center.x - hero.transform.position.x) / Pixel;
            Assert.That(gap, Is.InRange(47f, 51f), "which is where it stops");
            Assert.That(foe.Facing, Is.EqualTo(-1), "and turns to him");
            yield return probe.Until(() => shoot.Shots > 0, 40);
            Assert.That(shoot.Shots, Is.EqualTo(1));

            // He follows a unit behind it to the end of the platform: it has nowhere to go.
            var lowest = foe.Box.bounds.min.y;
            for (var i = 0; i < 150; i++)
            {
                hero.PlaceAt(new Vector2(foe.Box.bounds.center.x - 1f, Floor + 2f));
                yield return probe.Step();
                lowest = Mathf.Min(lowest, foe.Box.bounds.min.y);
            }
            Assert.That(back.IsRetreating, Is.True);
            var front = foe.Box.bounds.max.x;
            Assert.That(front, Is.InRange(36f - 6f * Pixel, 36f + 4f * Pixel), "it stands at the edge");
            Assert.That(lowest, Is.GreaterThan(Floor + 2f - 2f * Pixel), "and does not step off");
        }

        [UnityTest]
        public IEnumerator AQuakeSendsAWaveAlongTheFloorThatAWallStopsAndThatHurtsInItsWay()
        {
            Put(16f, Floor, 1f, 2f);
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Quake());
            yield return Begin(new Vector2(40f, Floor));
            var foe = FoeOf(piece);
            var quake = Act<Quake>(foe);
            var full = hero.Health;
            yield return probe.Step(20);
            Assert.That(foe.IsTelegraphing, Is.False, "too far off it does nothing");

            // Beyond the wall, six and a half units from its middle: inside 30 to 110 pixels.
            hero.PlaceAt(new Vector2(14f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(quake.IsWindingUp, Is.True, "in reach, it warns");
            Assert.That(foe.Facing, Is.EqualTo(-1), "facing him");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(quake.Waves, Is.EqualTo(0), "nothing leaves while it warns");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(quake.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(quake.Waves, Is.EqualTo(1), "then the wave");
            Assert.That(quake.Last != null, Is.True);
            Assert.That(quake.Last.ShotKind, Is.EqualTo(EnemyShot.Kind.Wave));
            Assert.That(quake.Last.Direction.x, Is.EqualTo(-1f), "the way it faces");
            Assert.That(quake.Last.transform.position.y, Is.EqualTo(Floor).Within(Pixel), "along the floor");
            yield return probe.Step(80);
            Assert.That(quake.Last == null, Is.True, "the wall stops it");
            Assert.That(hero.Health, Is.EqualTo(full), "so it does not reach him");

            // On its other side with nothing between them, once it has rested.
            hero.PlaceAt(new Vector2(25f, Floor));
            yield return probe.Until(() => quake.Waves == 2, 300);
            Assert.That(quake.Waves, Is.EqualTo(2));
            Assert.That(quake.Last.Direction.x, Is.EqualTo(1f));
            yield return probe.Until(() => hero.Health < full, 60);
            Assert.That(hero.Health, Is.EqualTo(full - quake.damage), "standing in its way hurts");
            yield return probe.Step(2);
            Assert.That(quake.Last == null, Is.True, "and the wave is spent");
        }
    }
}
