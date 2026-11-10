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
    /// The two near misses a jump forgives, each on both sides of its number: a ceiling's
    /// corner over his head by a few pixels, and a ledge's top a few pixels over his feet.
    /// </summary>
    public class CornerTests
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

        /// <summary>A low ceiling whose corner hangs over the right of his head by some pixels; he jumps.</summary>
        private IEnumerator JumpUnderACorner(int over)
        {
            Put(10f + HalfHero - over * Pixel, Floor + 3f, 4f, 1f);
            yield return Begin(new Vector2(10f, Floor));
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => hero.IsGrounded, 300);
        }

        [UnityTest]
        public IEnumerator ACeilingCornerWithinTheCorrectionIsSlippedPast()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var over = tuning.cornerCorrection - 1;
            var height = tuning.jumpHeight;
            Object.Destroy(tuning);
            Assert.That(over, Is.GreaterThan(0));

            yield return JumpUnderACorner(over);
            Assert.That(probe.Apex, Is.GreaterThan(height - 2f), "the jump reached its height");
            var moved = (10f - heroBody.position.x) / Pixel;
            Assert.That(moved, Is.InRange(over - 1f, over + 1f), "he was moved aside by the overlap and no more");
        }

        [UnityTest]
        public IEnumerator ACeilingCornerBeyondTheCorrectionStopsTheJump()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var over = tuning.cornerCorrection + 2;
            Object.Destroy(tuning);

            yield return JumpUnderACorner(over);
            // The ceiling is three units up and he is two tall.
            Assert.That(probe.Apex, Is.LessThan(PlayerTuning.PixelsPerUnit + 2f), "he bumped his head");
            Assert.That(Mathf.Abs(heroBody.position.x - 10f), Is.LessThan(Pixel), "and was not moved aside");
        }

        /// <summary>
        /// In the air beside a block, a pixel from its side, his feet some pixels under its
        /// top, steering at it. Two steps pass before he moves, so he is lower by then.
        /// </summary>
        private IEnumerator FallBesideALedge(float under)
        {
            Put(12f, Floor, 10f, 3f);
            yield return Begin(new Vector2(6f, Floor));
            hero.PlaceAt(new Vector2(12f - HalfHero - Pixel, Floor + 3f - under * Pixel));
            input.MoveX = 1f;
            // He still counts as standing where he was until a step has looked under him.
            yield return probe.Until(() => !hero.IsGrounded, 5);
            Assert.That(hero.IsGrounded, Is.False, "he was put in the air");
            yield return probe.Until(() => hero.IsGrounded, 120);
            yield return probe.Step(3);
            input.MoveX = 0f;
        }

        [UnityTest]
        public IEnumerator ALedgeTopJustOverHisFeetIsCaught()
        {
            yield return FallBesideALedge(1f);
            Assert.That(hero.IsGrounded, Is.True, $"he is at {heroBody.position}");
            Assert.That(heroBody.position.y, Is.EqualTo(Floor + 3f).Within(2f * Pixel), "he stands on the ledge");
            Assert.That(heroBody.position.x, Is.GreaterThan(12f - HalfHero), "and has moved onto it");
        }

        [UnityTest]
        public IEnumerator ALedgeTopWellOverHisFeetIsNot()
        {
            yield return FallBesideALedge(10f);
            Assert.That(hero.IsGrounded, Is.True);
            Assert.That(heroBody.position.y, Is.EqualTo(Floor).Within(2f * Pixel), "he fell to the floor");
            Assert.That(heroBody.position.x, Is.LessThan(12f - HalfHero + Pixel), "against the block's side");
        }
    }
}
