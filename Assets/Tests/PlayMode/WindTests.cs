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
    /// Areas that push, each held to its numbers: a wind in his face and at his back, a
    /// wall he is blown against, a downdraught, a lift, a gust, and the rate he picks a
    /// wind up at.
    /// </summary>
    public class WindTests
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
            LiveTuning.ReleaseAll();
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

        private const float Dt = 0.02f;

        /// <summary>A wind over an area, still until the test starts it; it is picked up at once.</summary>
        private Piece PutWind(Rect rect, Vector2 wind)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Wind;
            piece.rect = rect;
            piece.wind = wind;
            piece.gain = 0f;
            piece.hidden = true;
            room.pieces.Add(piece);
            return piece;
        }

        private ForceZone Built(Piece piece)
        {
            return lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece)).GetComponent<ForceZone>();
        }

        /// <summary>His own speed upwards, without the wind's part, in pixels a second.</summary>
        private float OwnRise => hero.VelocityPixels.y - hero.WindPixels.y;

        [UnityTest]
        public IEnumerator AHeadwindSlowsARunAndATailwindSpeedsIt()
        {
            var piece = PutWind(new Rect(8f, Floor, 34f, 5f), new Vector2(-60f, 0f));
            yield return Begin(new Vector2(30f, Floor));
            var run = ScriptableObject.CreateInstance<PlayerTuning>().runSpeed;
            var zone = Built(piece);
            var stood = heroBody.position.x;
            yield return probe.Step(20);
            Assert.That(heroBody.position.x, Is.EqualTo(stood).Within(Pixel), "switched off, it does not push");

            zone.Set(SwitchTo.On);
            input.MoveX = 1f;
            yield return probe.Step(40);
            Assert.That(hero.WindPixels, Is.EqualTo(new Vector2(-60f, 0f)));
            var from = heroBody.position.x;
            yield return probe.Step(50);
            var against = (heroBody.position.x - from) / Pixel;
            Assert.That(against, Is.EqualTo(run - 60f).Within(2f), "against it he runs slower by its speed");

            input.MoveX = -1f;
            yield return probe.Step(40);
            from = heroBody.position.x;
            yield return probe.Step(50);
            var with = (from - heroBody.position.x) / Pixel;
            Assert.That(with, Is.EqualTo(run + 60f).Within(2f), "with it he runs faster by its speed");
        }

        [UnityTest]
        public IEnumerator BlownAgainstAWallHeStandsStill()
        {
            Put(4f, Floor, 1f, 4f);
            var piece = PutWind(new Rect(5f, Floor, 10f, 5f), new Vector2(-80f, 0f));
            yield return Begin(new Vector2(5f + HalfHero + 0.25f, Floor));
            var zone = Built(piece);
            zone.Set(SwitchTo.On);
            yield return probe.Step(60);
            Assert.That(heroBody.position.x, Is.EqualTo(5f + HalfHero).Within(Pixel), "the wind holds him to the wall");
            Assert.That(hero.State, Is.EqualTo(PlayerState.Idle), "and he stands, he does not run on the spot");

            zone.Set(SwitchTo.Off);
            yield return probe.Step(20);
            Assert.That(hero.WindPixels, Is.EqualTo(Vector2.zero));
            Assert.That(heroBody.position.x, Is.EqualTo(5f + HalfHero).Within(Pixel));
        }

        [UnityTest]
        public IEnumerator ADowndraughtLeavesHimStandingAndShortensTheJump()
        {
            var piece = PutWind(new Rect(8f, Floor, 10f, 14f), new Vector2(0f, -100f));
            yield return Begin(new Vector2(12f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            Built(piece).Set(SwitchTo.On);
            for (var i = 0; i < 30; i++)
            {
                yield return null;
                Assert.That(hero.IsGrounded, Is.True, $"he stands at step {i}");
            }
            Assert.That(hero.State, Is.EqualTo(PlayerState.Idle));

            // His own jump, with the wind's 100 off every step of it.
            var speed = tuning.JumpVelocity;
            var height = 0f;
            var expected = 0f;
            while (speed - 100f > 0f)
            {
                height += (speed - 100f) * Dt;
                expected = Mathf.Max(expected, height);
                speed -= tuning.Gravity * Dt;
            }

            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => hero.IsGrounded, 200);
            Assert.That(probe.Apex, Is.EqualTo(expected).Within(5f), "the jump is shorter by what the wind takes");
            Assert.That(probe.Apex, Is.LessThan(tuning.jumpHeight * 0.7f));
        }

        [UnityTest]
        public IEnumerator ALiftRaisesAGlideAndNotAFall()
        {
            var piece = PutWind(new Rect(8f, Floor, 10f, 26f), new Vector2(0f, 140f));
            yield return Begin(new Vector2(12f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            LiveTuning.Force(Save.Ability.Glide, true);
            Built(piece).Set(SwitchTo.On);

            var highest = heroBody.position.y;
            for (var i = 0; i < 150; i++)
            {
                yield return null;
                highest = Mathf.Max(highest, heroBody.position.y);
            }
            Assert.That(highest, Is.LessThan(Floor + 2f), "a plain fall beats the lift: he only bobs");
            Assert.That(highest, Is.GreaterThan(Floor + 2f * Pixel), "but it does lift him");

            // A jump, and the glide opened once he is falling.
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => OwnRise < 0f, 100);
            if (!hero.IsGliding)
            {
                yield return probe.PressJump();
            }
            yield return probe.Step(20);
            Assert.That(hero.IsGliding, Is.True);
            var from = heroBody.position.y;
            yield return probe.Step(50);
            var rise = (heroBody.position.y - from) / Pixel;
            Assert.That(rise, Is.EqualTo(140f - tuning.glideFallSpeed).Within(4f), "the glide rides the lift up");
        }

        [UnityTest]
        public IEnumerator AGustBlowsForItsTimeAndRestsForItsTime()
        {
            var piece = PutWind(new Rect(8f, Floor, 24f, 5f), new Vector2(60f, 0f));
            piece.gustOn = 0.5f;
            piece.gustOff = 0.5f;
            yield return Begin(new Vector2(12f, Floor));
            var zone = Built(piece);
            zone.Set(SwitchTo.On);
            var from = heroBody.position.x;
            var blowing = 0;
            var changes = 0;
            var was = zone.IsBlowing;
            for (var i = 0; i < 100; i++)
            {
                yield return null;
                if (zone.IsBlowing)
                {
                    blowing++;
                }
                if (zone.IsBlowing != was)
                {
                    changes++;
                    was = zone.IsBlowing;
                }
            }
            Assert.That(blowing, Is.InRange(47, 53), "it blows half the time");
            Assert.That(changes, Is.InRange(3, 4), "in gusts of half a second");
            var blown = (heroBody.position.x - from) / Pixel;
            Assert.That(blown, Is.EqualTo(60f).Within(8f), "and he is pushed only while it blows");
        }

        [UnityTest]
        public IEnumerator AWindIsPickedUpAndLostAtItsGain()
        {
            var piece = PutWind(new Rect(8f, Floor, 30f, 5f), new Vector2(60f, 0f));
            piece.gain = 120f;
            yield return Begin(new Vector2(12f, Floor));
            var zone = Built(piece);
            zone.Set(SwitchTo.On);
            yield return probe.Step(10);
            Assert.That(hero.WindPixels.x, Is.EqualTo(24f).Within(5f), "ten steps at 120 a second squared");
            yield return probe.Step(40);
            Assert.That(hero.WindPixels.x, Is.EqualTo(60f).Within(0.01f), "and no more than the wind");

            zone.Set(SwitchTo.Off);
            yield return probe.Step(10);
            Assert.That(hero.WindPixels.x, Is.EqualTo(36f).Within(5f), "it dies down at the same rate");
            yield return probe.Step(40);
            Assert.That(hero.WindPixels.x, Is.EqualTo(0f));
        }
    }
}
