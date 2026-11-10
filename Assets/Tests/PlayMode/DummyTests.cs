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
    /// The training dummy and what the attacks tell it: the sword's damage, the time
    /// it lands, how far it reaches and that it lands once a swing; the slam and the
    /// bolt named as what they are; and the read-out's counting and forgetting.
    /// </summary>
    public class DummyTests
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

        /// <summary>One press of attack, then frames until the swing is over.</summary>
        private IEnumerator Swing()
        {
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "the press started a swing");
            yield return probe.Until(() => hero.State != PlayerState.Attack, 100);
        }

        [UnityTest]
        public IEnumerator TheSwordLandsOnceASwingInItsActiveTimeForItsDamage()
        {
            var piece = PutDummy(HeroAt + 0.75f, Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(piece);
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var from = tuning.attackActiveFrom;
            Object.Destroy(tuning);
            Assert.That(dummy.Hits, Is.EqualTo(0));

            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "the press started a swing");
            var steps = 0;
            while (dummy.Hits == 0 && steps < 50)
            {
                yield return probe.Step();
                steps++;
            }
            Assert.That(dummy.Hits, Is.EqualTo(1), "the swing landed");
            var after = steps * MoveProbe.Dt;
            Assert.That(after, Is.InRange(from - 2f * MoveProbe.Dt, from + 2f * MoveProbe.Dt), "when the blade is out");
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Sword));
            Assert.That(dummy.Last.Damage, Is.EqualTo(hero.AttackDamage));
            Assert.That(dummy.Last.From.x, Is.EqualTo(heroBody.position.x).Within(2f * Pixel), "from where he stands");

            yield return probe.Until(() => hero.State != PlayerState.Attack, 100);
            yield return probe.Step(5);
            Assert.That(dummy.Hits, Is.EqualTo(1), "once a swing, however long the blade is out");

            yield return Swing();
            Assert.That(dummy.Hits, Is.EqualTo(2), "and again with the next");
            Assert.That(dummy.Total, Is.EqualTo(2 * hero.AttackDamage));
        }

        [UnityTest]
        public IEnumerator TheSwordReachesAsFarAsItsShapeAndOnlyAhead()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var reach = PlayerTuning.Units(tuning.attackOffset.x + tuning.attackShapeSize.x / 2f);
            Object.Destroy(tuning);
            var near = PutDummy(HeroAt + reach - 4f * Pixel, Floor, 0.5f, 2f);
            var far = PutDummy(HeroAt + reach + 8f * Pixel, Floor, 0.5f, 2f);
            var behind = PutDummy(HeroAt - 1.5f, Floor, 0.5f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            Assert.That(heroBody.position.x, Is.EqualTo(HeroAt).Within(Pixel));
            Assert.That(hero.Facing, Is.EqualTo(1), "he faces right");

            yield return Swing();
            Assert.That(DummyOf(near).Hits, Is.EqualTo(1), "four pixels inside the blade's reach");
            Assert.That(DummyOf(far).Hits, Is.EqualTo(0), "eight pixels past it");
            Assert.That(DummyOf(behind).Hits, Is.EqualTo(0), "behind his back");
        }

        [UnityTest]
        public IEnumerator TheSlamAndTheBoltSayWhatTheyAre()
        {
            room.abilities = new[] { Save.Ability.Smite, Save.Ability.HolyLight };
            var piece = PutDummy(HeroAt + 1.5f, Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(piece);
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var slamDamage = tuning.slamDamage;
            Object.Destroy(tuning);

            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => heroBody.linearVelocity.y <= 0f, 200);
            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Slam, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Slam));
            Assert.That(dummy.Hits, Is.EqualTo(0), "nothing until he lands");
            yield return probe.Until(() => hero.State != PlayerState.Slam, 300);
            input.DownHeld = false;
            input.JumpHeld = false;
            Assert.That(dummy.Hits, Is.EqualTo(1), "the shock lands once");
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Slam));
            Assert.That(dummy.Last.Damage, Is.EqualTo(slamDamage));

            yield return probe.Settle();
            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Until(() => dummy.Hits == 2, 100);
            Assert.That(dummy.Hits, Is.EqualTo(2), "the bolt reaches it");
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Bolt));
            Assert.That(dummy.Last.Damage, Is.EqualTo(LightBolt.Damage));
            Assert.That(dummy.Total, Is.EqualTo(slamDamage + LightBolt.Damage));
        }

        [UnityTest]
        public IEnumerator TheReadOutCountsShowsAndForgets()
        {
            var piece = PutDummy(HeroAt + 6f, Floor, 1f, 2f, "target");
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(piece);
            var shape = Built(piece).GetComponentInChildren<Fx.Shape>();
            Assert.That(shape.Label, Is.EqualTo("target"), "its name until it is hit");

            Assert.That(dummy.Hit(2, Vector2.zero), Is.True, "a plain hit counts as the sword's");
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Sword));
            Assert.That(Strikes.Deliver(dummy, new Strike(StrikeKind.Slam, 3, Vector2.zero)), Is.True);
            Assert.That(dummy.Hits, Is.EqualTo(2));
            Assert.That(dummy.Total, Is.EqualTo(5));
            Assert.That(dummy.DamagePerSecond, Is.EqualTo(5f / Dummy.Window).Within(0.001f));
            Assert.That(shape.Label, Does.StartWith("target\nSlam 3\n2 hits, 5 in all\n"));
            Assert.That(shape.Label, Is.EqualTo(dummy.ReadOut()));

            yield return probe.Until(() => dummy.DamagePerSecond <= 0f, 250);
            yield return probe.Step(2);
            Assert.That(dummy.DamagePerSecond, Is.EqualTo(0f), "the hits leave the window");
            Assert.That(dummy.Total, Is.EqualTo(5), "the total stays");
            Assert.That(shape.Label, Does.EndWith("0.0 a second"), "and the label follows");

            dummy.Clear();
            Assert.That(dummy.Hits, Is.EqualTo(0));
            Assert.That(shape.Label, Is.EqualTo("target"));
        }
    }
}
