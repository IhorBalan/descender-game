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
    /// The hero on moving platforms, in a room made for each test: he rides a sideways
    /// and a vertical one without slipping, keeps their speed when he jumps off, uses a
    /// moving ledge as a ledge, and is hurt and put aside when a solid one crushes him.
    /// </summary>
    public class PlatformTests
    {
        private const float Floor = 2f;
        private const float Pixel = 1f / PlayerTuning.PixelsPerUnit;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private Station room;
        private LabRoot lab;
        private PlayerController hero;
        private Rigidbody2D heroBody;
        private ScriptedInput input;
        private MoveProbe probe;

        private static float Dt => MoveProbe.Dt;

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
            room.spawn = new Vector2(30f, Floor);
            Put(PieceKind.Solid, 0f, 0f, 56f, Floor);
            Put(PieceKind.Solid, 0f, Floor, 2f, 26f);
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

        private Piece Put(PieceKind kind, float x, float y, float width, float height)
        {
            var piece = new Piece();
            piece.kind = kind;
            piece.rect = new Rect(x, y, width, height);
            room.pieces.Add(piece);
            return piece;
        }

        /// <summary>A moving piece that waits to be started.</summary>
        private Piece PutMover(PieceKind kind, Rect rect, Vector2 travel, float speed)
        {
            var piece = Put(kind, rect.x, rect.y, rect.width, rect.height);
            piece.travel = travel;
            piece.speed = speed;
            return piece;
        }

        /// <summary>Builds the room, puts the hero at a place in it and lets him settle.</summary>
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
            // Past the moment after a respawn when nothing can hurt him.
            yield return probe.Until(() => hero.InvulnerableLeft <= 0f, 200);
        }

        private Mover MoverOf(Piece piece)
        {
            var built = lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));
            return built.GetComponent<Mover>();
        }

        private static Vector2 PlaceOf(Mover mover) => mover.GetComponent<Rigidbody2D>().position;

        [UnityTest]
        public IEnumerator HeRidesASidewaysPlatformThereAndBackWithoutSlipping()
        {
            var piece = PutMover(PieceKind.Lift, new Rect(10f, 4f, 4f, 0.25f), new Vector2(8f, 0f), 6f);
            yield return Begin(new Vector2(12f, 4.25f));
            var mover = MoverOf(piece);
            var offset = heroBody.position - PlaceOf(mover);
            var startX = heroBody.position.x;

            mover.Set(SwitchTo.On);
            var steps = Mathf.CeilToInt(2f * 8f / 6f / Dt) + 5;
            var slipped = 0f;
            var farthest = 0f;
            for (var i = 0; i < steps; i++)
            {
                yield return null;
                Assert.That(hero.IsGrounded, Is.True, $"step {i}: he stays on it");
                var now = heroBody.position - PlaceOf(mover);
                slipped = Mathf.Max(slipped, (now - offset).magnitude);
                farthest = Mathf.Max(farthest, heroBody.position.x - startX);
            }
            Assert.That(farthest, Is.GreaterThan(7.5f), "it took him to the far end");
            Assert.That(slipped, Is.LessThanOrEqualTo(Pixel), "his place on it never moved by more than a pixel");
            Assert.That(hero.State, Is.EqualTo(PlayerState.Idle), "standing still, he is not running");
        }

        [UnityTest]
        public IEnumerator HeRidesAVerticalPlatformUpAndDownWithoutLeavingIt()
        {
            var piece = PutMover(PieceKind.Lift, new Rect(10f, 4f, 4f, 0.25f), new Vector2(0f, 8f), 6f);
            yield return Begin(new Vector2(12f, 4.25f));
            var mover = MoverOf(piece);
            var offset = heroBody.position - PlaceOf(mover);
            var startY = heroBody.position.y;

            mover.Set(SwitchTo.On);
            var steps = Mathf.CeilToInt(2f * 8f / 6f / Dt) + 5;
            var slipped = 0f;
            var highest = 0f;
            for (var i = 0; i < steps; i++)
            {
                yield return null;
                Assert.That(hero.IsGrounded, Is.True, $"step {i}: he stays on it");
                var now = heroBody.position - PlaceOf(mover);
                slipped = Mathf.Max(slipped, (now - offset).magnitude);
                highest = Mathf.Max(highest, heroBody.position.y - startY);
            }
            Assert.That(highest, Is.GreaterThan(7.5f), "it took him to the top");
            Assert.That(slipped, Is.LessThanOrEqualTo(Pixel), "his place on it never moved by more than a pixel");
        }

        [UnityTest]
        public IEnumerator JumpingOffKeepsTheShareOfItsSpeedTheTuningGives()
        {
            // A long deck: in the air he falls behind it, and he has to come down on it again.
            var piece = PutMover(PieceKind.Lift, new Rect(4f, 4f, 12f, 0.25f), new Vector2(30f, 0f), 4f);
            yield return Begin(new Vector2(10f, 4.25f));
            var mover = MoverOf(piece);
            var speed = 4f * PlayerTuning.PixelsPerUnit;
            mover.Set(SwitchTo.On);
            yield return probe.Step(10);
            Assert.That(hero.CarryVelocity.x, Is.EqualTo(4f).Within(0.01f));

            // All of it: he leaves at the platform's speed, less a few steps of air friction.
            hero.Tuning.platformMomentum = 1f;
            yield return probe.PressJump();
            yield return probe.Step(2);
            Assert.That(hero.IsGrounded, Is.False);
            Assert.That(hero.CarryVelocity, Is.EqualTo(Vector2.zero), "in the air nothing carries him");
            var friction = hero.Tuning.airFriction * Dt;
            Assert.That(hero.VelocityPixels.x, Is.InRange(speed - 3f * friction, speed + 0.5f));

            // He comes down on it again (it is long); with none of it kept he leaves at a standstill.
            yield return probe.Until(() => hero.IsGrounded, 300);
            yield return probe.Step(10);
            Assert.That(hero.CarryVelocity.x, Is.EqualTo(4f).Within(0.01f), "he landed on it again");
            hero.Tuning.platformMomentum = 0f;
            yield return probe.PressJump();
            yield return probe.Step(2);
            Assert.That(hero.IsGrounded, Is.False);
            Assert.That(Mathf.Abs(hero.VelocityPixels.x), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator ARisingPlatformAddsToTheJumpUpToTheCap()
        {
            // Ten units a second is more than the cap, so the cap is what the jump gets.
            var piece = PutMover(PieceKind.Lift, new Rect(10f, 4f, 4f, 0.25f), new Vector2(0f, 16f), 10f);
            yield return Begin(new Vector2(12f, 4.25f));
            var mover = MoverOf(piece);
            var tuning = hero.Tuning;
            Assert.That(10f * PlayerTuning.PixelsPerUnit, Is.GreaterThan(tuning.platformLiftMax));
            mover.Set(SwitchTo.On);
            yield return probe.Step(10);
            Assert.That(hero.IsGrounded, Is.True);

            yield return probe.PressJump();
            var fastest = 0f;
            for (var i = 0; i < 3; i++)
            {
                yield return probe.Step();
                fastest = Mathf.Max(fastest, hero.VelocityPixels.y);
            }
            var expected = tuning.JumpVelocity + tuning.platformLiftMax;
            Assert.That(fastest, Is.InRange(expected - tuning.Gravity * Dt - 1f, expected + 1f));
        }

        [UnityTest]
        public IEnumerator AMovingLedgeIsJumpedUpThroughAndDroppedThrough()
        {
            var piece = PutMover(PieceKind.Lift, new Rect(10f, 4.5f, 4f, 0.25f), new Vector2(6f, 0f), 2f);
            piece.running = true;
            yield return Begin(new Vector2(12.5f, Floor));
            var mover = MoverOf(piece);
            Assert.That(mover.Running, Is.True);

            // Up through it from the floor, and down onto it.
            yield return probe.Until(() => PlaceOf(mover).x < heroBody.position.x, 400);
            // Passing up through it he touches it for a moment; standing on it is coming down onto it.
            yield return probe.PressJump();
            yield return probe.Step(3);
            yield return probe.Until(() => hero.IsGrounded && hero.CarryVelocity != Vector2.zero, 300);
            yield return probe.Step(5);
            Assert.That(hero.IsGrounded, Is.True, $"he came down on the ledge; he is at {heroBody.position}");
            Assert.That(heroBody.position.y, Is.EqualTo(4.75f).Within(2f * Pixel), "he stands on the ledge");
            Assert.That(Mathf.Abs(hero.CarryVelocity.x), Is.EqualTo(2f).Within(0.01f), "and it carries him");

            // Down and jump drops through it to the floor.
            input.Release();
            input.DownHeld = true;
            yield return probe.PressJump(false);
            yield return probe.Step(3);
            input.DownHeld = false;
            yield return probe.Until(() => hero.IsGrounded && heroBody.position.y < 3f, 300);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel), "he dropped to the floor");
            Assert.That(hero.CarryVelocity, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator ABlockComingDownOnHimHurtsOnceAndPutsHimBesideIt()
        {
            var block = new Rect(10f, 6f, 3f, 1.5f);
            var piece = PutMover(PieceKind.Platform, block, new Vector2(0f, -4f), 2f);
            yield return Begin(new Vector2(11.5f, Floor));
            var mover = MoverOf(piece);
            var health = hero.Health;

            mover.Set(SwitchTo.On);
            yield return probe.Until(() => hero.Health < health, 200);
            Assert.That(hero.Health, Is.EqualTo(health - 1), "crushed, he loses a heart");
            Assert.That(hero.IsAlive, Is.True);
            yield return probe.Step(2);
            var x = heroBody.position.x;
            Assert.That(x < block.xMin - 0.4f || x > block.xMax + 0.4f, Is.True, $"he is beside the block, at {x}");
            Assert.That(Mathf.Abs(x - 11.5f), Is.LessThan(4f), "and not far from where he was");

            // The block finishes its way down and comes up again; he is not hurt twice.
            yield return probe.Step(150);
            Assert.That(hero.Health, Is.EqualTo(health - 1));
            Assert.That(hero.IsGrounded, Is.True);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel));
        }

        [UnityTest]
        public IEnumerator ABlockPressingHimToAWallHurtsOnceAndPutsHimClear()
        {
            // The wall's face is at x 2; the block comes from the right until it touches the wall.
            var block = new Rect(6f, Floor, 2f, 3f);
            var piece = PutMover(PieceKind.Platform, block, new Vector2(-4f, 0f), 2f);
            yield return Begin(new Vector2(3f, Floor));
            var mover = MoverOf(piece);
            var health = hero.Health;

            mover.Set(SwitchTo.On);
            yield return probe.Until(() => hero.Health < health, 200);
            Assert.That(hero.Health, Is.EqualTo(health - 1), "squeezed, he loses a heart");
            Assert.That(hero.IsAlive, Is.True);
            yield return probe.Step(2);
            var blockRight = PlaceOf(mover).x + block.width / 2f;
            var heroLeft = heroBody.position.x - 0.4375f;
            Assert.That(heroLeft, Is.GreaterThanOrEqualTo(blockRight - Pixel), "he is put on the block's open side");

            yield return probe.Step(150);
            Assert.That(hero.Health, Is.EqualTo(health - 1));
            Assert.That(hero.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator StandingOnASolidPlatformIsNotBeingCrushedAndSafeGroundStaysOffIt()
        {
            var piece = PutMover(PieceKind.Platform, new Rect(10f, Floor, 4f, 1f), new Vector2(0f, 6f), 3f);
            yield return Begin(new Vector2(12f, Floor + 1f));
            var mover = MoverOf(piece);
            var health = hero.Health;
            var safe = hero.LastSafePosition;

            mover.Set(SwitchTo.On);
            yield return probe.Step(Mathf.CeilToInt(2f * 6f / 3f / Dt) + 5);
            Assert.That(hero.Health, Is.EqualTo(health), "riding a solid platform up and down does not hurt");
            Assert.That(hero.IsGrounded, Is.True);
            Assert.That(hero.LastSafePosition, Is.EqualTo(safe), "a moving platform is not safe ground");
        }
    }
}
