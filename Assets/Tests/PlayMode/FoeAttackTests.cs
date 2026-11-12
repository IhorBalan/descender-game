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
    /// Foes that attack: a chase starts when the hero is in sight, stops short of him, at
    /// a wall and at an edge, and gives up a set time after it last saw him; a swing
    /// warns for its whole windup before it can hurt, misses a hero who left, is called
    /// off by a hit, and is not when the body is armoured; an ambush stays still until
    /// he is near, warns, and then lets the rest act.
    /// </summary>
    public class FoeAttackTests
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
        public IEnumerator AChaseStartsInSightStopsShortOfHimAndGivesUpAfterItLastSawHim()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Chase());
            yield return Begin(new Vector2(40f, Floor));
            var foe = FoeOf(piece);
            var chase = Act<Chase>(foe);
            var stoodAt = foe.transform.position.x;
            yield return probe.Step(30);
            Assert.That(chase.IsChasing, Is.False, "out of sight it does not come");
            Assert.That(foe.transform.position.x, Is.EqualTo(stoodAt).Within(Pixel));

            // Five and a half units from its middle: inside its sight of 120 pixels.
            hero.PlaceAt(new Vector2(26f, Floor));
            yield return probe.Step(2);
            Assert.That(chase.IsChasing, Is.True, "in sight it comes");
            Assert.That(foe.Facing, Is.EqualTo(1));
            yield return probe.Step(150);
            var gap = (hero.transform.position.x - foe.Box.bounds.center.x) / Pixel;
            var stop = chase.stopAt + foe.Body.size.x / 2f;
            Assert.That(gap, Is.InRange(stop - 2.5f, stop + 0.5f), "and stops short of him");
            var reached = foe.transform.position.x;
            yield return probe.Step(20);
            Assert.That(foe.transform.position.x, Is.EqualTo(reached).Within(Pixel), "where it stays");

            hero.PlaceAt(new Vector2(46f, Floor));
            var steps = 0;
            while (chase.IsChasing && steps < 300)
            {
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(chase.giveUp / MoveProbe.Dt).Within(3f), "it gives up after its time");
            Assert.That(foe.transform.position.x, Is.GreaterThan(reached + 1f), "having followed until then");
            var gaveUpAt = foe.transform.position.x;
            yield return probe.Step(20);
            Assert.That(foe.transform.position.x, Is.EqualTo(gaveUpAt).Within(Pixel), "and then it stops");
        }

        [UnityTest]
        public IEnumerator AChaseStopsAtAWallAndAtAnEdge()
        {
            Put(20f, Floor, 1f, 3f);
            var onFloor = PutFoe(16f, Floor, 1f, 1.5f, Harmless(), new Chase());
            Put(30f, Floor, 6f, 2f);
            var onTop = PutFoe(32.5f, Floor + 2f, 1f, 1.5f, Harmless(), new Chase());
            yield return Begin(new Vector2(50f, Floor));
            var walker = FoeOf(onFloor);
            var keeper = FoeOf(onTop);

            // Beyond the wall, in sight.
            hero.PlaceAt(new Vector2(23f, Floor));
            yield return probe.Step(150);
            Assert.That(Act<Chase>(walker).IsChasing, Is.True);
            Assert.That(walker.Box.bounds.max.x, Is.EqualTo(20f).Within(3f * Pixel), "it stops at the wall");

            // On the floor past the platform's end, near enough in height to be seen.
            hero.PlaceAt(new Vector2(39f, Floor));
            var lowest = keeper.Box.bounds.min.y;
            for (var i = 0; i < 200; i++)
            {
                yield return probe.Step();
                lowest = Mathf.Min(lowest, keeper.Box.bounds.min.y);
            }
            Assert.That(Act<Chase>(keeper).IsChasing, Is.True);
            var front = keeper.Box.bounds.max.x;
            Assert.That(front, Is.InRange(36f - 6f * Pixel, 36f + 4f * Pixel), "it stops at the edge");
            Assert.That(lowest, Is.GreaterThan(Floor + 2f - 2f * Pixel), "and does not step off");
        }

        [UnityTest]
        public IEnumerator ASwingWarnsForItsWholeWindupBeforeItCanHurt()
        {
            var piece = PutFoe(HeroAt + 1f, Floor, 1f, 1.5f, Harmless(), new Swing());
            yield return Begin(new Vector2(40f, Floor));
            var foe = FoeOf(piece);
            var swing = Act<Swing>(foe);
            var full = hero.Health;
            yield return probe.Step(20);
            Assert.That(foe.IsTelegraphing, Is.False, "with no one near it does nothing");

            hero.PlaceAt(new Vector2(HeroAt, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True, "near, it warns");
            Assert.That(swing.IsWindingUp, Is.True);
            Assert.That(foe.Facing, Is.EqualTo(-1), "facing him");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(hero.Health, Is.EqualTo(full), "nothing hurts while it warns");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(swing.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(swing.IsStriking, Is.True, "then it strikes");
            yield return probe.Until(() => hero.Health < full, 5);
            Assert.That(hero.Health, Is.EqualTo(full - swing.damage), "and that hurts");
        }

        [UnityTest]
        public IEnumerator ASwingMissesAHeroWhoLeftIsCalledOffByAHitAndNotWhenArmoured()
        {
            var plain = PutFoe(HeroAt + 1f, Floor, 1f, 1.5f, Harmless(), new Swing());
            var plated = PutFoe(31f, Floor, 1f, 1.5f, Harmless(true), new Swing());
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(plain);
            var swing = Act<Swing>(foe);
            var full = hero.Health;

            hero.PlaceAt(new Vector2(HeroAt, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True);
            hero.PlaceAt(new Vector2(HeroAt - 6f, Floor));
            yield return probe.Step(60);
            Assert.That(hero.Health, Is.EqualTo(full), "he left during the warning: it hits nothing");
            Assert.That(swing.IsAttacking, Is.False, "and the swing is over");

            // Past its cooldown, it swings again; a cut during the warning calls it off.
            yield return probe.Step(45);
            hero.PlaceAt(new Vector2(HeroAt, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True);
            yield return probe.Step(5);
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, hero.transform.position)), Is.True);
            Assert.That(foe.IsTelegraphing, Is.False, "a hit ends the warning");
            Assert.That(swing.IsAttacking, Is.False, "and the swing");
            yield return probe.Step(30);
            Assert.That(hero.Health, Is.EqualTo(full), "so nothing lands");

            var armoured = FoeOf(plated);
            var stoodAt = armoured.transform.position.x;
            hero.PlaceAt(new Vector2(30f, Floor));
            yield return probe.Until(() => armoured.IsTelegraphing, 5);
            Assert.That(armoured.IsTelegraphing, Is.True);
            yield return probe.Step(5);
            Assert.That(armoured.Struck(new Strike(StrikeKind.Sword, 1, hero.transform.position)), Is.True);
            Assert.That(armoured.Health, Is.EqualTo(8), "armour still takes the damage");
            Assert.That(armoured.IsHurt, Is.False, "but is not held");
            Assert.That(armoured.IsTelegraphing, Is.True, "and goes on warning");
            yield return probe.Until(() => hero.Health < full, 30);
            Assert.That(hero.Health, Is.EqualTo(full - 1), "its swing lands");
            Assert.That(armoured.transform.position.x, Is.EqualTo(stoodAt).Within(Pixel), "and it was not pushed");
        }

        [UnityTest]
        public IEnumerator AnAmbushStaysStillUntilHeIsNearWarnsAndThenLetsTheRestAct()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Ambush(), new Chase());
            var other = PutFoe(44f, Floor, 1f, 1.5f, Harmless(), new Ambush(), new Chase());
            yield return Begin(new Vector2(50f, Floor));
            var foe = FoeOf(piece);
            var ambush = Act<Ambush>(foe);
            var stoodAt = foe.transform.position.x;

            // Five units away: the chase would see him, the ambush does not.
            hero.PlaceAt(new Vector2(25.5f, Floor));
            yield return probe.Step(50);
            Assert.That(ambush.IsAwake, Is.False);
            Assert.That(foe.IsTelegraphing, Is.False);
            Assert.That(foe.transform.position.x, Is.EqualTo(stoodAt).Within(Pixel), "it waits");

            hero.PlaceAt(new Vector2(22.5f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 3);
            Assert.That(foe.IsTelegraphing, Is.True, "near, it warns");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(foe.transform.position.x, Is.EqualTo(stoodAt).Within(Pixel), "still, while it warns");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(ambush.mark / MoveProbe.Dt).Within(2f));
            Assert.That(ambush.IsAwake, Is.True);
            yield return probe.Step(10);
            Assert.That(foe.transform.position.x, Is.GreaterThan(stoodAt + 4f * Pixel), "then it comes");

            var sleeper = FoeOf(other);
            Assert.That(Act<Ambush>(sleeper).IsAwake, Is.False);
            Assert.That(sleeper.Struck(new Strike(StrikeKind.Sword, 1, Vector2.zero)), Is.True);
            Assert.That(Act<Ambush>(sleeper).IsAwake, Is.True, "a hit wakes one at once");
        }
    }
}
