using System.Collections;
using System.Collections.Generic;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using Descender.Triggers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Platforms that do not stay: one that crumbles once stood on and one on a timer,
    /// each held to its times, and neither coming back on top of him.
    /// </summary>
    public class TimedPlatformTests
    {
        private const float Floor = 2f;
        private const float HalfHero = 0.4375f;
        private const float Pixel = 1f / PlayerTuning.PixelsPerUnit;

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
        }

        /// <summary>A crumbling or blinking platform that is gone until the test switches it on.</summary>
        private Piece PutTimed(PieceKind kind, Rect rect, float solidFor, float goneFor, bool hidden = true)
        {
            var piece = new Piece();
            piece.kind = kind;
            piece.rect = rect;
            piece.solidFor = solidFor;
            piece.goneFor = goneFor;
            piece.warnFor = 0.2f;
            piece.hidden = hidden;
            room.pieces.Add(piece);
            return piece;
        }

        private Transform Built(Piece piece) => lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));

        [UnityTest]
        public IEnumerator ACrumblingPlatformHoldsForItsTimeAndComesBackAfterItsTime()
        {
            var piece = PutTimed(PieceKind.Crumble, new Rect(10f, 4.5f, 3f, 0.25f), 0.5f, 1f);
            yield return Begin(new Vector2(6f, Floor));
            var crumble = Built(piece).GetComponent<Crumble>();
            Assert.That(crumble.IsSolid, Is.False, "switched off, it is gone");

            crumble.Set(SwitchTo.On);
            Assert.That(crumble.IsSolid, Is.True);
            hero.PlaceAt(new Vector2(11.5f, 4.75f));
            yield return probe.Until(() => crumble.Now == Crumble.Stage.Shaking, 20);
            Assert.That(crumble.Now, Is.EqualTo(Crumble.Stage.Shaking), "standing on it starts it");

            var held = 0;
            while (crumble.IsSolid && held < 100)
            {
                yield return null;
                held++;
                if (crumble.IsSolid && held > 3)
                {
                    Assert.That(heroBody.position.y, Is.EqualTo(4.75f).Within(2f * Pixel), $"held at step {held}");
                }
            }
            Assert.That(held, Is.InRange(23, 27), "it holds for half a second");

            var gone = 0;
            while (!crumble.IsSolid && gone < 200)
            {
                yield return null;
                gone++;
            }
            Assert.That(gone, Is.InRange(48, 53), "it is gone for a second");
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel), "he fell to the floor");
            Assert.That(crumble.Now, Is.EqualTo(Crumble.Stage.Whole));
        }

        [UnityTest]
        public IEnumerator ACrumbledPlatformDoesNotComeBackOnTopOfHim()
        {
            var piece = PutTimed(PieceKind.Crumble, new Rect(10f, Floor, 3f, 1f), 0.2f, 0.4f);
            yield return Begin(new Vector2(6f, Floor));
            var crumble = Built(piece).GetComponent<Crumble>();
            crumble.Set(SwitchTo.On);
            hero.PlaceAt(new Vector2(11.5f, Floor + 1f));
            yield return probe.Until(() => !crumble.IsSolid, 60);
            Assert.That(crumble.IsSolid, Is.False);

            // Twice its time: he has fallen into where it was and stands there.
            yield return probe.Step(40);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel));
            Assert.That(crumble.IsSolid, Is.False, "it waits for him to leave");

            input.MoveX = 1f;
            yield return probe.Until(() => crumble.IsSolid, 200);
            input.MoveX = 0f;
            Assert.That(crumble.IsSolid, Is.True, "it comes back once he is out");
            Assert.That(heroBody.position.x, Is.GreaterThan(13f + HalfHero - 2f * Pixel), "and he is beside it");
            yield return probe.Step(10);
            Assert.That(hero.Health, Is.EqualTo(hero.MaxHealth), "it did not hurt him");
        }

        [UnityTest]
        public IEnumerator ABlinkerCarriesHimOnlyWhileSolidAndWarnsBeforeItChanges()
        {
            var piece = PutTimed(PieceKind.Blinker, new Rect(10f, 4.5f, 3f, 0.25f), 1f, 1f);
            yield return Begin(new Vector2(6f, Floor));
            var blinker = Built(piece).GetComponent<Blinker>();
            Assert.That(blinker.IsSolid, Is.False, "switched off, it is gone");

            blinker.Set(SwitchTo.On);
            hero.PlaceAt(new Vector2(11.5f, 4.75f));
            var solid = 0;
            var warned = 0;
            while (blinker.IsSolid && solid < 200)
            {
                yield return null;
                solid++;
                if (blinker.IsSolid && blinker.IsWarning)
                {
                    warned++;
                }
                if (solid == 40)
                {
                    Assert.That(hero.IsGrounded, Is.True);
                    Assert.That(heroBody.position.y, Is.EqualTo(4.75f).Within(2f * Pixel), "it carries him");
                }
            }
            Assert.That(solid, Is.InRange(48, 52), "solid for a second");
            Assert.That(warned, Is.InRange(8, 12), "it blinks for the last fifth of a second");

            var gone = 0;
            while (!blinker.IsSolid && gone < 200)
            {
                yield return null;
                gone++;
            }
            Assert.That(gone, Is.InRange(48, 52), "gone for a second");
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel), "he fell when it went");
        }

        [UnityTest]
        public IEnumerator BlinkersHalfACycleApartTakeTurns()
        {
            var first = PutTimed(PieceKind.Blinker, new Rect(10f, 4.5f, 3f, 0.25f), 1f, 1f, false);
            var second = PutTimed(PieceKind.Blinker, new Rect(14f, 4.5f, 3f, 0.25f), 1f, 1f, false);
            second.ahead = 1f;
            yield return Begin(new Vector2(6f, Floor));
            var one = Built(first).GetComponent<Blinker>();
            var other = Built(second).GetComponent<Blinker>();

            var apart = 0;
            for (var i = 0; i < 150; i++)
            {
                yield return null;
                if (one.IsSolid != other.IsSolid)
                {
                    apart++;
                }
            }
            Assert.That(apart, Is.GreaterThanOrEqualTo(145), "one is solid while the other is gone");
        }

        [UnityTest]
        public IEnumerator APlatformThatDoesNotStayIsNotSafeGround()
        {
            var piece = PutTimed(PieceKind.Blinker, new Rect(10f, 4.5f, 3f, 0.25f), 10f, 1f);
            yield return Begin(new Vector2(6f, Floor));
            yield return probe.Step(60);
            var blinker = Built(piece).GetComponent<Blinker>();
            blinker.Set(SwitchTo.On);
            hero.PlaceAt(new Vector2(11.5f, 4.75f));
            yield return probe.Step(3);
            var safe = hero.LastSafePosition;

            yield return probe.Step(150);
            Assert.That(hero.IsGrounded, Is.True);
            Assert.That(heroBody.position.y, Is.EqualTo(4.75f).Within(2f * Pixel), "he stands on it");
            Assert.That(hero.LastSafePosition, Is.EqualTo(safe), "and it is not where a hazard puts him back");
        }
    }
}
