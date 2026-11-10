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
    /// Water, held to its numbers: the sink speed, a stroke's height and the gap between
    /// strokes, the full jump at the surface, and moving and dashing slower.
    /// </summary>
    public class WaterTests
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

        private void PutWater(float x, float y, float width, float height)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Water;
            piece.rect = new Rect(x, y, width, height);
            room.pieces.Add(piece);
        }

        [UnityTest]
        public IEnumerator HeSinksAtTheSinkSpeed()
        {
            PutWater(8f, Floor, 10f, 14f);
            yield return Begin(new Vector2(4f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            Assert.That(hero.IsInWater, Is.False);

            hero.PlaceAt(new Vector2(13f, Floor + 10f));
            yield return probe.Step(40);
            Assert.That(hero.IsInWater, Is.True);
            var from = heroBody.position.y;
            yield return probe.Step(50);
            var sank = (from - heroBody.position.y) / Pixel;
            Assert.That(sank, Is.EqualTo(tuning.waterSinkSpeed).Within(1.5f), "a second of sinking");
        }

        [UnityTest]
        public IEnumerator AStrokeLiftsHimItsHeightAndCanRepeatAfterItsGap()
        {
            PutWater(8f, Floor, 10f, 14f);
            yield return Begin(new Vector2(13f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            Assert.That(hero.IsInWater, Is.True);

            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            // A second press at once is too soon and does nothing.
            yield return probe.PressJump();
            yield return probe.Until(() => hero.VelocityPixels.y < 0f, 100);
            Assert.That(probe.Apex, Is.EqualTo(tuning.waterStrokeHeight).Within(3f), "one stroke, its height");

            // Another, from where he is now, lifts him as much again.
            yield return probe.Step(Mathf.CeilToInt(tuning.waterStrokeGap / 0.02f));
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => hero.VelocityPixels.y < 0f, 100);
            Assert.That(probe.Apex, Is.EqualTo(tuning.waterStrokeHeight).Within(3f), "and the next one the same");
            Assert.That(hero.IsGrounded, Is.False, "from mid-water");
        }

        [UnityTest]
        public IEnumerator AtTheSurfaceJumpIsAFullJumpOut()
        {
            // Deep enough for the middle of him, not for his head.
            PutWater(8f, Floor, 10f, 1.5f);
            yield return Begin(new Vector2(13f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            Assert.That(hero.IsInWater, Is.True);

            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => hero.IsGrounded, 300);
            // The first half unit of the rise is still in water, which pulls less.
            Assert.That(probe.Apex, Is.InRange(tuning.jumpHeight - 2f, tuning.jumpHeight + 12f), "a full jump");
        }

        [UnityTest]
        public IEnumerator HeMovesAndDashesSlowerInWater()
        {
            PutWater(8f, Floor, 30f, 6f);
            yield return Begin(new Vector2(12f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();

            input.MoveX = 1f;
            yield return probe.Step(40);
            var from = heroBody.position.x;
            yield return probe.Step(50);
            var ran = (heroBody.position.x - from) / Pixel;
            Assert.That(ran, Is.EqualTo(tuning.runSpeed * tuning.waterMoveShare).Within(2f), "a second of walking");

            yield return probe.Press(i => i.DashPressed = true);
            var fastest = 0f;
            for (var i = 0; i < 6; i++)
            {
                yield return null;
                fastest = Mathf.Max(fastest, hero.VelocityPixels.x);
            }
            input.MoveX = 0f;
            Assert.That(fastest, Is.EqualTo(tuning.dashSpeed * tuning.waterDashShare).Within(2f), "the dash");
        }
    }
}
