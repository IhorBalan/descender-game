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
    /// Climbing, held to its numbers: taking hold, the climb speed, staying put with
    /// nothing held, letting go by a hop and by a leap, up not being a jump while he
    /// climbs, and stepping off at the top and climbing back down through it.
    /// </summary>
    public class ClimbTests
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

        private void PutClimb(float x, float y, float width, float height)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Climb;
            piece.rect = new Rect(x, y, width, height);
            room.pieces.Add(piece);
        }

        [UnityTest]
        public IEnumerator UpTakesHoldAndHeClimbsAtTheClimbSpeed()
        {
            PutClimb(10f, Floor, 1f, 10f);
            yield return Begin(new Vector2(10.5f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();

            input.DownHeld = true;
            yield return probe.Step(5);
            Assert.That(hero.IsClimbing, Is.False, "down on the ground under it does not take hold");
            input.DownHeld = false;

            input.UpHeld = true;
            yield return probe.Step(3);
            Assert.That(hero.IsClimbing, Is.True, "up takes hold");
            var from = heroBody.position.y;
            yield return probe.Step(50);
            var climbed = (heroBody.position.y - from) / Pixel;
            Assert.That(climbed, Is.EqualTo(tuning.climbSpeed).Within(2f), "a second of climbing");

            input.UpHeld = false;
            yield return probe.Step(3);
            from = heroBody.position.y;
            yield return probe.Step(25);
            Assert.That(hero.IsClimbing, Is.True);
            Assert.That(heroBody.position.y, Is.EqualTo(from).Within(Pixel), "with nothing held he stays where he is");

            input.DownHeld = true;
            yield return probe.Step(3);
            from = heroBody.position.y;
            yield return probe.Step(25);
            var down = (from - heroBody.position.y) / Pixel;
            Assert.That(down, Is.EqualTo(tuning.climbSpeed / 2f).Within(2f), "and down at the same speed");

            yield return probe.Until(() => !hero.IsClimbing, 300);
            yield return probe.Step(5);
            Assert.That(hero.IsClimbing, Is.False, "the ground lets him go");
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel));
        }

        [UnityTest]
        public IEnumerator JumpAloneLetsGoWithAHopAndASideWithJumpLeapsOff()
        {
            PutClimb(10f, Floor, 1f, 12f);
            yield return Begin(new Vector2(10.5f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();

            input.UpHeld = true;
            yield return probe.Step(60);
            input.UpHeld = false;
            yield return probe.Step(3);
            Assert.That(hero.IsClimbing, Is.True);
            probe.Begin();
            yield return probe.PressJump(false);
            yield return probe.Step(2);
            Assert.That(hero.IsClimbing, Is.False, "jump lets go");
            yield return probe.Until(() => hero.IsGrounded, 300);
            var hop = tuning.climbHopSpeed * tuning.climbHopSpeed / (2f * tuning.Gravity);
            Assert.That(probe.Apex, Is.EqualTo(hop).Within(3f), "with a small hop");
            Assert.That(Mathf.Abs(probe.Distance), Is.LessThan(1f), "straight up and down");

            yield return probe.Settle();
            input.UpHeld = true;
            yield return probe.Step(60);
            input.UpHeld = false;
            Assert.That(hero.IsClimbing, Is.True, "he took hold again");
            input.MoveX = 1f;
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            Assert.That(hero.IsClimbing, Is.False);
            yield return probe.Until(() => hero.IsGrounded, 300);
            input.MoveX = 0f;
            var speed = tuning.JumpVelocity * tuning.climbLeap;
            var leap = speed * speed / (2f * tuning.Gravity);
            Assert.That(probe.Apex, Is.EqualTo(leap).Within(5f), "a leap as high as its share of a jump");
            Assert.That(probe.Distance, Is.GreaterThan(2f * PlayerTuning.PixelsPerUnit), "and away to the side");
        }

        [UnityTest]
        public IEnumerator UpIsNotAJumpWhileHeClimbsOrTakesHold()
        {
            PutClimb(10f, Floor, 1f, 12f);
            yield return Begin(new Vector2(10.5f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();

            // Up and jump on the same key: the press that takes hold is not also a jump.
            var from = heroBody.position.y;
            input.UpHeld = true;
            yield return probe.PressJump();
            yield return probe.Step(10);
            Assert.That(hero.IsClimbing, Is.True, "he took hold instead of jumping");
            var rose = (heroBody.position.y - from) / Pixel;
            Assert.That(rose, Is.LessThan(tuning.climbSpeed * 13f * 0.02f + 2f), "and rose at the climb speed");

            yield return probe.PressJump();
            yield return probe.Step(5);
            Assert.That(hero.IsClimbing, Is.True, "nor does it let go while up is held");
        }

        [UnityTest]
        public IEnumerator TheTopLetsHimStepOffAndDownThereClimbsBack()
        {
            PutClimb(10f, Floor, 1f, 6f);
            var ledge = new Piece();
            ledge.kind = PieceKind.Ledge;
            ledge.rect = new Rect(10f, Floor + 5.75f, 1f, 0.25f);
            room.pieces.Add(ledge);
            Put(11f, Floor, 6f, 6f);
            yield return Begin(new Vector2(10.5f, Floor));

            input.UpHeld = true;
            yield return probe.Until(() => hero.IsClimbing, 10);
            yield return probe.Until(() => !hero.IsClimbing, 400);
            yield return probe.Step(40);
            Assert.That(hero.IsClimbing, Is.False, "up at the top does not take hold again");
            Assert.That(hero.IsGrounded, Is.True);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor + 6f).Within(2f * Pixel), "he stands at its top");
            input.UpHeld = false;

            input.MoveX = 1f;
            yield return probe.Step(40);
            input.MoveX = 0f;
            Assert.That(heroBody.position.x, Is.GreaterThan(11.5f), "and walks off onto the floor beside it");
            Assert.That(heroBody.position.y, Is.EqualTo(Floor + 6f).Within(2f * Pixel));

            hero.PlaceAt(new Vector2(10.5f, Floor + 6f));
            yield return probe.Step(20);
            input.DownHeld = true;
            yield return probe.Step(5);
            Assert.That(hero.IsClimbing, Is.True, "down on the ledge takes hold");
            yield return probe.Step(40);
            Assert.That(heroBody.position.y, Is.LessThan(Floor + 5f), "and he climbs down through it");
            yield return probe.Until(() => !hero.IsClimbing, 400);
            yield return probe.Step(5);
            input.DownHeld = false;
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel), "to the ground");
        }
    }
}
