using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
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
    /// Holy Light and Smite by their numbers: the bolt flies at its speed, as far as its
    /// range, for its damage and with its push, and no sooner than its cooldown; the slam
    /// hurts what is inside its area and pushes it away; and a zone that asks for one kind
    /// of strike answers to that kind only.
    /// </summary>
    public class LightAndSmiteTests
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

        private Piece PutZone(float x, float y, float width, float height, StruckBy by)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Zone;
            piece.rect = new Rect(x, y, width, height);
            piece.on = TriggerEvent.Hit;
            piece.once = false;
            piece.by = by;
            room.pieces.Add(piece);
            return piece;
        }

        private TriggerZone ZoneOf(Piece piece) => Built(piece).GetComponent<TriggerZone>();

        /// <summary>The x, in units from his middle, the front of a bolt reaches before it fades.</summary>
        private static float BoltReach(PlayerTuning tuning)
        {
            return PlayerTuning.Units(tuning.castOffset.x + tuning.boltRange + LightBolt.Size.x / 2f);
        }

        /// <summary>A jump straight up and the slam from its top, to the frame the slam is over.</summary>
        private IEnumerator JumpAndSlam()
        {
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => heroBody.linearVelocity.y <= 0f, 200);
            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Slam, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Slam));
            yield return probe.Until(() => hero.State != PlayerState.Slam, 300);
            input.DownHeld = false;
            input.JumpHeld = false;
        }

        [UnityTest]
        public IEnumerator TheBoltFliesAtItsSpeedForItsDamageAndPushesWhatItHits()
        {
            room.abilities = new[] { Save.Ability.HolyLight };
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            tuning.boltDamage = 3;
            tuning.boltRange = 200f;
            // The bolt's front starts this far ahead of his middle; the dummy stands 96 pixels past it.
            var front = tuning.castOffset.x + LightBolt.Size.x / 2f;
            const float Gap = 96f;
            var piece = PutDummy(HeroAt + PlayerTuning.Units(front + Gap), Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            hero.UseTuning(tuning);
            var dummy = DummyOf(piece);

            yield return probe.Press(i => i.CastPressed = true);
            var steps = 0;
            while (dummy.Hits == 0 && steps < 100)
            {
                yield return probe.Step();
                steps++;
            }
            Assert.That(dummy.Hits, Is.EqualTo(1), "the bolt reached it");
            var expected = Gap / (tuning.boltSpeed * MoveProbe.Dt);
            Assert.That(steps, Is.EqualTo(expected).Within(4f), "at the bolt's speed");
            Assert.That(dummy.Last.Kind, Is.EqualTo(StrikeKind.Bolt));
            Assert.That(dummy.Last.Damage, Is.EqualTo(3), "for the tuned damage");
            Assert.That(dummy.Last.Push.x, Is.EqualTo(tuning.boltPush).Within(0.001f), "pushed the way it flew");
            yield return probe.Until(() => !dummy.IsSliding, 100);
            Assert.That(dummy.Pushed, Is.EqualTo(Dummy.SlideOf(tuning.boltPush)).Within(1.5f));

            // Twice as fast, it is there in half the steps.
            yield return probe.Until(() => dummy.Pushed <= 0f, 300);
            tuning.boltSpeed *= 2f;
            dummy.Clear();
            yield return probe.Press(i => i.CastPressed = true);
            var quick = 0;
            while (dummy.Hits == 0 && quick < 100)
            {
                yield return probe.Step();
                quick++;
            }
            Assert.That(dummy.Hits, Is.EqualTo(1));
            Assert.That(quick, Is.EqualTo(expected / 2f).Within(4f));
            Assert.That(quick, Is.LessThan(steps));
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator TheBoltFadesAtTheEndOfItsRange()
        {
            room.abilities = new[] { Save.Ability.HolyLight };
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var reach = BoltReach(tuning);
            // Eight pixels past where the bolt's front gets to.
            var piece = PutDummy(HeroAt + reach + 8f * Pixel, Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            hero.UseTuning(tuning);
            var dummy = DummyOf(piece);

            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Step(3);
            var bolt = Object.FindAnyObjectByType<LightBolt>();
            Assert.That(bolt, Is.Not.Null, "a bolt left his hand");
            yield return probe.Until(() => bolt == null, 100);
            Assert.That(bolt == null, Is.True, "it faded");
            Assert.That(dummy.Hits, Is.EqualTo(0), "eight pixels short of the dummy");

            tuning.boltRange += 16f;
            yield return probe.Step(30);
            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Until(() => dummy.Hits > 0, 100);
            Assert.That(dummy.Hits, Is.EqualTo(1), "sixteen pixels more range and it lands");
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator ASecondBoltWaitsForTheCooldown()
        {
            room.abilities = new[] { Save.Ability.HolyLight };
            var piece = PutDummy(HeroAt + 3f, Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(piece);
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var cooldown = tuning.castCooldown;
            var buffer = tuning.inputBufferTime;
            Object.Destroy(tuning);
            Assert.That(cooldown, Is.GreaterThan(0.2f + buffer), "a press 0.2 s in is not held to its end");

            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Step(9);
            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Step(Mathf.CeilToInt(cooldown / MoveProbe.Dt));
            Assert.That(dummy.Hits, Is.EqualTo(1), "the press inside the cooldown cast nothing");

            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Until(() => dummy.Hits == 2, 60);
            Assert.That(dummy.Hits, Is.EqualTo(2), "after it, the next press casts");
        }

        [UnityTest]
        public IEnumerator TheSlamHurtsWhatIsInItsAreaAndPushesItAway()
        {
            room.abilities = new[] { Save.Ability.Smite };
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var half = PlayerTuning.Units(tuning.slamReach.x / 2f);
            var damage = tuning.slamDamage;
            var push = tuning.slamPush;
            Object.Destroy(tuning);
            // Six pixels inside the shock on either side, and eight pixels outside it.
            var left = PutDummy(HeroAt - half + 6f * Pixel - 0.5f, Floor, 0.5f, 2f);
            var right = PutDummy(HeroAt + half - 6f * Pixel, Floor, 0.5f, 2f);
            var outside = PutDummy(HeroAt + half + 8f * Pixel, Floor, 0.5f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));

            yield return JumpAndSlam();
            Assert.That(heroBody.position.x, Is.EqualTo(HeroAt).Within(2f * Pixel), "he came straight down");
            Assert.That(DummyOf(left).Hits, Is.EqualTo(1), "inside the shock on the left");
            Assert.That(DummyOf(right).Hits, Is.EqualTo(1), "and on the right");
            Assert.That(DummyOf(outside).Hits, Is.EqualTo(0), "eight pixels outside it");
            Assert.That(DummyOf(right).Last.Kind, Is.EqualTo(StrikeKind.Slam));
            Assert.That(DummyOf(right).Last.Damage, Is.EqualTo(damage));
            Assert.That(DummyOf(right).Last.Push.x, Is.EqualTo(push).Within(0.001f), "pushed away from him");
            Assert.That(DummyOf(left).Last.Push.x, Is.EqualTo(-push).Within(0.001f), "to either side");
        }

        [UnityTest]
        public IEnumerator AZoneThatAsksForOneStrikeAnswersToThatOneOnly()
        {
            room.abilities = new[] { Save.Ability.HolyLight, Save.Ability.Smite };
            // Both in the sword's reach and in the slam's shock; the bolt flies over the plate.
            var target = PutZone(HeroAt + 1.25f, Floor + 0.75f, 0.5f, 1.25f, StruckBy.Bolt);
            var plate = PutZone(HeroAt + 0.6f, Floor, 1.5f, 0.375f, StruckBy.Slam);
            yield return Begin(new Vector2(HeroAt, Floor));
            var bolts = ZoneOf(target);
            var slams = ZoneOf(plate);
            Assert.That(bolts.By, Is.EqualTo(StruckBy.Bolt));
            Assert.That(bolts.Answers(StrikeKind.Bolt), Is.True);
            Assert.That(bolts.Answers(StrikeKind.Sword), Is.False);

            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack));
            yield return probe.Until(() => hero.State != PlayerState.Attack, 100);
            Assert.That(bolts.TimesFired, Is.EqualTo(0), "the sword does not set off the bolt's target");
            Assert.That(slams.TimesFired, Is.EqualTo(0), "nor the slam's plate");

            yield return probe.Settle();
            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Until(() => bolts.TimesFired > 0, 60);
            Assert.That(bolts.TimesFired, Is.EqualTo(1), "a bolt does");
            Assert.That(slams.TimesFired, Is.EqualTo(0));

            yield return probe.Settle();
            yield return JumpAndSlam();
            Assert.That(slams.TimesFired, Is.EqualTo(1), "the slam sets off its plate");
            Assert.That(bolts.TimesFired, Is.EqualTo(1), "and not the bolt's target");
        }
    }
}
