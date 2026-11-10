using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The sword's aim: up and attack cuts upward and not ahead, down and attack in
    /// the air cuts downward and a cut that lands bounces him by the tuned height, on
    /// the ground it is the cut ahead, and with Smite a tuning switch says whether down
    /// and attack is the slam or the cut.
    /// </summary>
    public class SwordTests
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
        }

        private Piece PutDummy(float x, float y, float width, float height, string label = "")
        {
            var piece = new Piece();
            piece.kind = PieceKind.Dummy;
            piece.rect = new Rect(x, y, width, height);
            piece.label = label;
            room.pieces.Add(piece);
            return piece;
        }

        private Transform Built(Piece piece) => lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));

        private Dummy DummyOf(Piece piece) => Built(piece).GetComponent<Dummy>();

        /// <summary>One press of attack with whatever is held, then frames until the swing is over.</summary>
        private IEnumerator Swing()
        {
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "the press started a swing");
            yield return probe.Until(() => hero.State != PlayerState.Attack, 100);
        }

        /// <summary>A jump straight up, to the frame he starts to fall.</summary>
        private IEnumerator JumpToTheTop()
        {
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => heroBody.linearVelocity.y <= 0f, 200);
            Assert.That(hero.IsGrounded, Is.False);
        }

        [UnityTest]
        public IEnumerator UpAndAttackCutsUpwardAsFarAsItsShapeAndNotAhead()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var top = PlayerTuning.Units(tuning.attackUpOffset.y + tuning.attackUpSize.y / 2f);
            Object.Destroy(tuning);
            var inside = PutDummy(HeroAt - 0.5f, Floor + top - 4f * Pixel, 1f, 0.5f);
            var past = PutDummy(HeroAt - 0.5f, Floor + top + 8f * Pixel, 1f, 0.5f);
            var ahead = PutDummy(HeroAt + 0.75f, Floor, 1f, 1.25f);
            yield return Begin(new Vector2(HeroAt, Floor));

            input.UpHeld = true;
            yield return Swing();
            input.UpHeld = false;
            Assert.That(DummyOf(inside).Hits, Is.EqualTo(1), "four pixels inside the upward cut");
            Assert.That(DummyOf(past).Hits, Is.EqualTo(0), "eight pixels past it");
            Assert.That(DummyOf(ahead).Hits, Is.EqualTo(0), "and nothing ahead");
            Assert.That(hero.IsGrounded, Is.True, "up is not a jump");

            yield return probe.Step(3);
            yield return Swing();
            Assert.That(DummyOf(ahead).Hits, Is.EqualTo(1), "without up it is the cut ahead");
            Assert.That(DummyOf(inside).Hits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DownAndAttackInTheAirCutsDownwardAndBouncesHim()
        {
            var below = PutDummy(HeroAt - 0.5f, Floor, 1f, 3f);
            var ahead = PutDummy(HeroAt + 1.25f, Floor + 3.5f, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(below);
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var height = tuning.pogoHeight;
            Object.Destroy(tuning);

            yield return JumpToTheTop();
            input.JumpHeld = false;
            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            // Where he was the frame before the cut landed is where the bounce starts from.
            var from = heroBody.position.y;
            for (var i = 0; i < 60 && dummy.Hits == 0; i++)
            {
                from = heroBody.position.y;
                yield return probe.Step();
            }
            input.DownHeld = false;
            Assert.That(dummy.Hits, Is.EqualTo(1), "the downward cut reached the dummy under him");
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Sword));
            Assert.That(DummyOf(ahead).Hits, Is.EqualTo(0), "and nothing ahead");
            Assert.That(heroBody.linearVelocity.y, Is.GreaterThan(0f), "the cut that landed throws him up");

            var peak = from;
            yield return probe.Until(
                () =>
                {
                    peak = Mathf.Max(peak, heroBody.position.y);
                    return heroBody.linearVelocity.y <= 0f;
                },
                200
            );
            // A frame may hold a step more or less of his fall than the one before it,
            // so the start is known to a few pixels; no bounce, or a jump's, is far off.
            Assert.That((peak - from) / Pixel, Is.EqualTo(height).Within(8f), "by the bounce's height");
            yield return probe.Until(() => hero.IsGrounded, 300);
            Assert.That(dummy.Hits, Is.EqualTo(1), "one cut, one hit");
        }

        [UnityTest]
        public IEnumerator OnTheGroundDownAndAttackIsTheCutAhead()
        {
            var ahead = PutDummy(HeroAt + 0.75f, Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));

            input.DownHeld = true;
            yield return Swing();
            input.DownHeld = false;
            Assert.That(DummyOf(ahead).Hits, Is.EqualTo(1));
            Assert.That(hero.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator WithSmiteASwitchSaysWhetherDownAndAttackIsTheSlamOrTheCut()
        {
            room.abilities = new[] { Save.Ability.Smite };
            var below = PutDummy(HeroAt - 0.5f, Floor, 1f, 3f);
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(below);

            yield return JumpToTheTop();
            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Step(2);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Slam), "as tuned, it is the slam");
            yield return probe.Until(() => hero.State != PlayerState.Slam, 300);
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Slam));

            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.downAttackSlams = false;
            hero.UseTuning(tuning);
            yield return probe.Settle();
            dummy.Clear();
            yield return JumpToTheTop();
            input.JumpHeld = false;
            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Step(2);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "switched off, it is the cut");
            yield return probe.Until(() => dummy.Hits > 0, 60);
            input.DownHeld = false;
            Assert.That(dummy.Hits, Is.EqualTo(1));
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Sword));
            yield return probe.Until(() => hero.IsGrounded, 300);
            Object.Destroy(tuning);
        }
    }
}
