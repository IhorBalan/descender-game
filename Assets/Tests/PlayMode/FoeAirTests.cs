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
    /// Foes that shoot and dive: a bolt leaves only after the whole warning, flies at
    /// its speed and as far as its reach; an aimed one breaks on a wall and hurts a hero
    /// in the open; a flying body stays where it is put, hovers across its span and comes
    /// back to its height; a dive warns, comes down on him, and climbs back.
    /// </summary>
    public class FoeAirTests
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
        public IEnumerator ABoltLeavesAfterTheWholeWarningAndFliesAtItsSpeedAsFarAsItsReach()
        {
            // He stands on a shelf above the line of a flat bolt: in range, never hit.
            Put(23f, Floor + 3f, 3f, 0.5f);
            var with = new Shoot { level = true, reach = 100f };
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), with);
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var shoot = Act<Shoot>(foe);
            var full = hero.Health;
            yield return probe.Step(20);
            Assert.That(foe.IsTelegraphing, Is.False, "out of range it does nothing");
            Assert.That(shoot.Shots, Is.EqualTo(0));

            hero.PlaceAt(new Vector2(24.5f, Floor + 3.5f));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True, "in range it warns");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(shoot.Shots, Is.EqualTo(0), "nothing leaves while it warns");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(shoot.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(shoot.Shots, Is.EqualTo(1), "then one bolt");
            var bolt = shoot.Last;
            Assert.That(bolt != null, Is.True);
            Assert.That(bolt.Direction, Is.EqualTo(Vector2.right), "flat, the way it faces");

            var from = bolt.transform.position.x;
            yield return probe.Step(10);
            var pace = (bolt.transform.position.x - from) / Pixel / (10f * MoveProbe.Dt);
            Assert.That(pace, Is.EqualTo(shoot.speed).Within(shoot.speed * 0.12f), "at its speed");
            var flown = 0f;
            var limit = 0;
            while (bolt != null && limit < 200)
            {
                flown = bolt.Flown;
                yield return probe.Step();
                limit++;
            }
            Assert.That(bolt == null, Is.True, "it ends");
            Assert.That(flown, Is.EqualTo(shoot.reach).Within(shoot.speed * MoveProbe.Dt + 1f), "at its reach");
            Assert.That(hero.Health, Is.EqualTo(full));
        }

        [UnityTest]
        public IEnumerator AnAimedBoltBreaksOnAWallAndHurtsAHeroInTheOpen()
        {
            var open = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Shoot());
            var walled = PutFoe(35f, Floor, 1f, 1.5f, Harmless(), new Shoot());
            Put(38f, Floor, 1f, 5f);
            yield return Begin(new Vector2(50f, Floor));
            var full = hero.Health;

            // Behind the wall, in range of the second.
            hero.PlaceAt(new Vector2(41f, Floor));
            var behind = Act<Shoot>(FoeOf(walled));
            yield return probe.Until(() => behind.Shots > 0, 60);
            Assert.That(behind.Shots, Is.EqualTo(1));
            var bolt = behind.Last;
            Assert.That(bolt.Direction.x, Is.GreaterThan(0.9f), "aimed at him");
            var flown = 0f;
            var limit = 0;
            while (bolt != null && limit < 100)
            {
                flown = bolt.Flown;
                yield return probe.Step();
                limit++;
            }
            Assert.That(bolt == null, Is.True);
            Assert.That(flown, Is.LessThan(40f), "it broke on the wall, a unit and a half on");
            Assert.That(hero.Health, Is.EqualTo(full), "and did not reach him");

            hero.PlaceAt(new Vector2(25f, Floor));
            var clear = Act<Shoot>(FoeOf(open));
            yield return probe.Until(() => clear.Shots > 0, 60);
            Assert.That(clear.Shots, Is.EqualTo(1));
            yield return probe.Until(() => hero.Health < full, 80);
            Assert.That(hero.Health, Is.EqualTo(full - 1), "in the open it hits");
        }

        [UnityTest]
        public IEnumerator AFlyingBodyHoversAcrossItsSpanAndComesBackToItsHeight()
        {
            var with = Harmless();
            with.flies = true;
            var piece = PutFoe(20f, Floor + 5f, 1f, 0.75f, with, new Hover { span = 64f });
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var home = (Vector2)foe.transform.position;
            Assert.That(home.y, Is.EqualTo(Floor + 5.375f).Within(Pixel), "it has not fallen");

            var turns = 0;
            var facing = foe.Facing;
            var least = home.x;
            var most = home.x;
            for (var i = 0; i < 400; i++)
            {
                yield return probe.Step();
                if (foe.Facing != facing)
                {
                    facing = foe.Facing;
                    turns++;
                }
                least = Mathf.Min(least, foe.transform.position.x);
                most = Mathf.Max(most, foe.transform.position.x);
                Assert.That(foe.transform.position.y, Is.EqualTo(home.y).Within(Pixel), "it keeps its height");
            }
            Assert.That(turns, Is.GreaterThanOrEqualTo(2), "it turns at both ends");
            Assert.That(most, Is.EqualTo(home.x + 2f).Within(3f * Pixel), "half its span to the right");
            Assert.That(least, Is.EqualTo(home.x - 2f).Within(3f * Pixel), "and to the left");

            Assert.That(foe.Struck(new Strike(StrikeKind.Slam, 1, home, new Vector2(0f, -120f))), Is.True);
            yield return probe.Step(6);
            Assert.That(foe.transform.position.y, Is.LessThan(home.y - 2f * Pixel), "a blow knocks it down");
            yield return probe.Step(120);
            Assert.That(foe.transform.position.y, Is.EqualTo(home.y).Within(Pixel), "and it comes back up");
        }

        [UnityTest]
        public IEnumerator ADiveWarnsComesDownOnHimAndClimbsBack()
        {
            var with = Harmless();
            with.flies = true;
            with.contactDamage = 1;
            var piece = PutFoe(20f, Floor + 5f, 1f, 0.75f, with, new Dive());
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var dive = Act<Dive>(foe);
            var home = (Vector2)foe.transform.position;
            var full = hero.Health;
            yield return probe.Step(30);
            Assert.That(dive.IsAttacking, Is.False, "with no one below it waits");
            Assert.That(Vector2.Distance(foe.transform.position, home), Is.LessThan(Pixel), "where it was put");

            hero.PlaceAt(new Vector2(20.5f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True, "below it, he is warned");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(hero.Health, Is.EqualTo(full));
                var moved = Vector2.Distance(foe.transform.position, home);
                Assert.That(moved, Is.LessThan(Pixel), "still, while it warns");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(dive.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(dive.IsDiving, Is.True, "then it dives");
            yield return probe.Until(() => hero.Health < full, 40);
            Assert.That(hero.Health, Is.EqualTo(full - 1), "onto him");

            yield return probe.Until(() => !dive.IsAttacking, 300);
            Assert.That(dive.IsAttacking, Is.False, "it is done");
            yield return probe.Step(2);
            Assert.That(Vector2.Distance(foe.transform.position, home), Is.LessThan(2f * Pixel), "back where it began");
        }
    }
}
