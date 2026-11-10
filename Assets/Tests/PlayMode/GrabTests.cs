using System.Collections;
using System.Collections.Generic;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The ledge grab, a trial behind a switch: catching a corner on the way down,
    /// hanging, climbing with up, letting go with down, the switch being off unless a
    /// room or the tuning turns it on, and a ledge with no room to stand on.
    /// </summary>
    public class GrabTests
    {
        private const float Floor = 2f;
        private const float HalfHero = 0.4375f;
        private const float Pixel = 1f / PlayerTuning.PixelsPerUnit;
        private const float HeroHeight = 2f;
        private const float WallAt = 12f;

        // Above a full jump (4.5 units) by less than his reach would not do: his head
        // passes this top on the way up and comes back down past it.
        private const float WallHeight = 6f;

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

        /// <summary>A wall right of x = 12 whose top is a little over a jump; he runs at it and jumps.</summary>
        private IEnumerator JumpAtTheWall()
        {
            input.MoveX = 1f;
            yield return probe.Step(30);
            Assert.That(heroBody.position.x, Is.EqualTo(WallAt - HalfHero).Within(2f * Pixel), "he is at the wall");
            yield return probe.PressJump();
        }

        [UnityTest]
        public IEnumerator FallingPastACornerWhilePressingTowardsItHeHangsAndUpClimbs()
        {
            room.ledgeGrab = true;
            Put(WallAt, Floor, 6f, WallHeight);
            yield return Begin(new Vector2(WallAt - 1.5f, Floor));

            yield return JumpAtTheWall();
            var peak = 0f;
            yield return probe.Until(
                () =>
                {
                    peak = Mathf.Max(peak, heroBody.position.y);
                    return hero.IsHanging;
                },
                150
            );
            Assert.That(hero.IsHanging, Is.True, "he caught the corner");
            input.JumpHeld = false;
            input.MoveX = 0f;
            var hands = Floor + WallHeight - HeroHeight;
            Assert.That(heroBody.position.y, Is.EqualTo(hands).Within(Pixel), "with his hands at its top");
            Assert.That(peak, Is.GreaterThan(hands + 2f * Pixel), "on the way down: rising past it he did not");

            var from = heroBody.position;
            yield return probe.Step(50);
            Assert.That(hero.IsHanging, Is.True, "he hangs with nothing held");
            Assert.That(Vector2.Distance(heroBody.position, from), Is.LessThan(Pixel), "and stays put");

            input.UpHeld = true;
            yield return probe.Step(3);
            input.UpHeld = false;
            Assert.That(hero.IsHanging, Is.False, "up climbs");
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor + WallHeight).Within(2f * Pixel), "onto the ledge");
            Assert.That(heroBody.position.x, Is.GreaterThan(WallAt + HalfHero - Pixel), "all of him");
        }

        [UnityTest]
        public IEnumerator JumpClimbsTooAndDownLetsGo()
        {
            room.ledgeGrab = true;
            Put(WallAt, Floor, 6f, WallHeight);
            yield return Begin(new Vector2(WallAt - 1.5f, Floor));

            yield return JumpAtTheWall();
            yield return probe.Until(() => hero.IsHanging, 150);
            Assert.That(hero.IsHanging, Is.True);
            input.JumpHeld = false;
            yield return probe.Step(5);
            // He still holds towards the wall: letting go must not catch it again.
            input.DownHeld = true;
            yield return probe.Step(2);
            input.DownHeld = false;
            Assert.That(hero.IsHanging, Is.False, "down lets go");
            yield return probe.Until(() => hero.IsGrounded, 300);
            Assert.That(hero.IsHanging, Is.False);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel), "and he falls to the floor");

            yield return probe.Settle();
            input.MoveX = 1f;
            yield return probe.PressJump();
            yield return probe.Until(() => hero.IsHanging, 150);
            Assert.That(hero.IsHanging, Is.True, "he catches it again from a new jump");
            input.JumpHeld = false;
            yield return probe.Step(5);
            yield return probe.PressJump(false);
            yield return probe.Step(3);
            Assert.That(hero.IsHanging, Is.False, "jump climbs");
            input.MoveX = 0f;
            yield return probe.Settle();
            Assert.That(heroBody.position.y, Is.EqualTo(Floor + WallHeight).Within(2f * Pixel), "onto the ledge");
        }

        [UnityTest]
        public IEnumerator ItIsOffUnlessTheRoomOrTheTuningSwitchesItOn()
        {
            Put(WallAt, Floor, 6f, WallHeight);
            yield return Begin(new Vector2(WallAt - 1.5f, Floor));
            Assert.That(hero.GrabsLedges, Is.False, "the room does not ask for it");

            yield return JumpAtTheWall();
            var hung = false;
            var left = false;
            var peak = 0f;
            yield return probe.Until(
                () =>
                {
                    hung |= hero.IsHanging;
                    left |= !hero.IsGrounded;
                    peak = Mathf.Max(peak, heroBody.position.y);
                    return left && hero.IsGrounded;
                },
                300
            );
            input.JumpHeld = false;
            var hands = Floor + WallHeight - HeroHeight;
            Assert.That(peak, Is.GreaterThan(hands + 2f * Pixel), "his head went above the corner and came back");
            Assert.That(hung, Is.False, "switched off, he falls past the corner");
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel));

            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.ledgeGrab = true;
            hero.UseTuning(tuning);
            yield return probe.Settle();
            input.MoveX = 1f;
            yield return probe.PressJump();
            yield return probe.Until(() => hero.IsHanging, 150);
            input.JumpHeld = false;
            input.MoveX = 0f;
            Assert.That(hero.IsHanging, Is.True, "the tuning switches it on");
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator WithNoRoomToStandHeHangsAndUpDoesNothing()
        {
            room.ledgeGrab = true;
            Put(WallAt, Floor, 6f, WallHeight);
            // A slot a unit high over the ledge: he is two.
            Put(WallAt, Floor + WallHeight + 1f, 6f, 1f);
            yield return Begin(new Vector2(WallAt - 1.5f, Floor));

            yield return JumpAtTheWall();
            yield return probe.Until(() => hero.IsHanging, 150);
            Assert.That(hero.IsHanging, Is.True, "he catches it all the same");
            input.JumpHeld = false;
            input.MoveX = 0f;
            var from = heroBody.position;

            input.UpHeld = true;
            yield return probe.Step(20);
            input.UpHeld = false;
            yield return probe.PressJump(false);
            yield return probe.Step(10);
            Assert.That(hero.IsHanging, Is.True, "neither up nor jump climbs where he cannot stand");
            Assert.That(Vector2.Distance(heroBody.position, from), Is.LessThan(Pixel));

            input.DownHeld = true;
            yield return probe.Step(2);
            input.DownHeld = false;
            Assert.That(hero.IsHanging, Is.False, "down still lets go");
        }
    }
}
