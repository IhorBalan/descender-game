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
    /// Health by its rules: after a hit nothing hurts him for the invulnerable time; a
    /// hit throws him away from what hit him by the knockback, while a hazard puts him
    /// back on safe ground instead; a heal takes its time, spends a charge and fills
    /// hearts, and a hit during it stops it without spending the charge; and a striker
    /// lands no more often than the invulnerable time lets it.
    /// </summary>
    public class HurtTests
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
            // Arriving in a room leaves him safe for a while; the tests start once he can be hurt.
            yield return probe.Until(() => hero.CanBeHurt, 100);
            Assert.That(hero.CanBeHurt, Is.True);
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

        private Piece PutStriker(float x, float y, Vector2 reach, float every)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Striker;
            piece.rect = new Rect(x, y, 1f, 2f);
            piece.reach = reach;
            piece.value = every;
            room.pieces.Add(piece);
            return piece;
        }

        [UnityTest]
        public IEnumerator AfterAHitNothingHurtsHimForTheInvulnerableTime()
        {
            yield return Begin(new Vector2(HeroAt, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var safeFor = tuning.invulnerableTime;
            Object.Destroy(tuning);
            var full = hero.Health;
            var from = new Vector2(HeroAt + 1f, Floor + 1f);

            Assert.That(hero.Hurt(1, from), Is.True, "the first hit lands");
            Assert.That(hero.Health, Is.EqualTo(full - 1));
            Assert.That(hero.State, Is.EqualTo(PlayerState.Hurt));
            Assert.That(hero.InvulnerableLeft, Is.EqualTo(safeFor).Within(0.001f));

            // A hit tried every frame does nothing until the time is up.
            var steps = 0;
            while (steps < 200)
            {
                yield return probe.Step();
                steps++;
                if (hero.Hurt(1, from))
                {
                    break;
                }
            }
            Assert.That(hero.Health, Is.EqualTo(full - 2), "then the next one lands");
            var waited = steps * MoveProbe.Dt;
            Assert.That(waited, Is.EqualTo(safeFor).Within(3f * MoveProbe.Dt), "after the invulnerable time");
            Assert.That(hero.TakeHazardDamage(1), Is.False, "and spikes wait for it too");
        }

        [UnityTest]
        public IEnumerator AHitThrowsHimAwayAndAHazardPutsHimBack()
        {
            yield return Begin(new Vector2(HeroAt, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var knock = tuning.knockback;
            var hurtTime = tuning.hurtTime;
            var safeFor = tuning.invulnerableTime;
            // Thrown at the knockback's speed and slowed by half the ground's friction while he is hurt.
            var slowed = Mathf.Max(knock.x - tuning.groundFriction * 0.5f * hurtTime, 0f);
            var across = (knock.x + slowed) / 2f * hurtTime;
            var up = knock.y * knock.y / (2f * tuning.Gravity);
            Object.Destroy(tuning);
            var full = hero.Health;
            var stoodAt = heroBody.position;

            // Hit from the right: thrown to the left and up.
            Assert.That(hero.Hurt(1, stoodAt + new Vector2(1f, 1f)), Is.True);
            var peak = stoodAt.y;
            var left = false;
            yield return probe.Until(
                () =>
                {
                    peak = Mathf.Max(peak, heroBody.position.y);
                    left |= !hero.IsGrounded;
                    return left && hero.IsGrounded && hero.State != PlayerState.Hurt;
                },
                200
            );
            yield return probe.Step(10);
            var thrown = (stoodAt.x - heroBody.position.x) / Pixel;
            Assert.That(thrown, Is.GreaterThan(across - 4f), "away from what hit him, about the knockback's throw");
            Assert.That(thrown, Is.LessThan(across + 10f));
            Assert.That((peak - stoodAt.y) / Pixel, Is.EqualTo(up).Within(4f), "and up by its height");
            Assert.That(hero.Health, Is.EqualTo(full - 1));

            // A hazard costs the same heart and puts him back where he last stood safely: no throw.
            yield return probe.Step(Mathf.CeilToInt(safeFor / MoveProbe.Dt) + 15);
            Assert.That(hero.CanBeHurt, Is.True);
            var safeAt = heroBody.position;
            yield return probe.PressJump(false);
            yield return probe.Until(() => !hero.IsGrounded, 10);
            Assert.That(hero.IsGrounded, Is.False);
            Assert.That(hero.TakeHazardDamage(1), Is.True);
            Assert.That(hero.Health, Is.EqualTo(full - 2));
            yield return probe.Step(2);
            Assert.That(heroBody.position.x, Is.EqualTo(safeAt.x).Within(2f * Pixel), "back on safe ground");
            Assert.That(heroBody.position.y, Is.EqualTo(safeAt.y).Within(4f * Pixel));
            yield return probe.Settle();
            Assert.That(heroBody.position.x, Is.EqualTo(safeAt.x).Within(2f * Pixel), "and not thrown from it");
        }

        [UnityTest]
        public IEnumerator AHealTakesItsTimeAndAHitStopsItWithoutSpendingTheCharge()
        {
            yield return Begin(new Vector2(HeroAt, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var healSteps = tuning.healTime / MoveProbe.Dt;
            var amount = tuning.healAmount;
            var safeSteps = Mathf.CeilToInt(tuning.invulnerableTime / MoveProbe.Dt) + 15;
            Object.Destroy(tuning);
            var full = hero.Health;
            var charges = hero.HealCharges;
            Assert.That(charges, Is.GreaterThan(0));
            var from = new Vector2(HeroAt + 1f, Floor + 1f);

            yield return probe.Press(i => i.HealPressed = true);
            yield return probe.Step(3);
            Assert.That(hero.State, Is.Not.EqualTo(PlayerState.Heal), "nothing to heal at full health");

            Assert.That(hero.Hurt(3, from), Is.True);
            yield return probe.Step(safeSteps);
            yield return probe.Settle();
            Assert.That(hero.Health, Is.EqualTo(full - 3));

            // A hit part of the way through: the heal stops, the charge stays, no heart comes back.
            yield return probe.Press(i => i.HealPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Heal, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Heal));
            yield return probe.Step(Mathf.RoundToInt(healSteps / 2f));
            Assert.That(hero.State, Is.EqualTo(PlayerState.Heal), "still healing halfway");
            Assert.That(hero.Hurt(1, from), Is.True);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Hurt));
            yield return probe.Step(safeSteps);
            yield return probe.Settle();
            Assert.That(hero.Health, Is.EqualTo(full - 4), "the hit landed and nothing was healed");
            Assert.That(hero.HealCharges, Is.EqualTo(charges), "the charge was not spent");

            // Left alone it takes its time, spends a charge and fills its hearts.
            yield return probe.Press(i => i.HealPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Heal, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Heal));
            var steps = 0;
            while (hero.State == PlayerState.Heal && steps < 200)
            {
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(healSteps).Within(3f), "the heal's time");
            Assert.That(hero.Health, Is.EqualTo(Mathf.Min(full - 4 + amount, full)));
            Assert.That(hero.HealCharges, Is.EqualTo(charges - 1));
        }

        [UnityTest]
        public IEnumerator AStrikerHitsHimInItsReachNoOftenerThanTheInvulnerableTimeLets()
        {
            // Tries every 0.4 s; he cannot be hurt for 0.9 s, so every third try lands.
            var piece = PutStriker(HeroAt + 1f, Floor, new Vector2(12f, 6f), 0.4f);
            room.spawn = new Vector2(HeroAt + 20f, Floor);
            yield return Begin(new Vector2(HeroAt + 20f, Floor));
            var striker = Built(piece).GetComponent<Striker>();
            var full = hero.Health;
            Assert.That(striker.Reaches(hero), Is.False);
            yield return probe.Step(30);
            Assert.That(striker.Tries, Is.EqualTo(0), "out of its reach it leaves him alone");

            hero.PlaceAt(new Vector2(HeroAt, Floor));
            yield return probe.Until(() => striker.Landed == 1, 20);
            Assert.That(striker.Landed, Is.EqualTo(1), "it hits as he comes into reach");
            Assert.That(hero.Health, Is.EqualTo(full - 1));
            yield return probe.Step(3);
            Assert.That(heroBody.position.x, Is.LessThan(HeroAt), "thrown away from it");

            yield return probe.Step(100);
            Assert.That(striker.Reaches(hero), Is.True, "still in its reach");
            Assert.That(striker.Tries, Is.GreaterThanOrEqualTo(5));
            Assert.That(striker.Landed, Is.EqualTo(2), "two seconds on, one more hit has landed");
            Assert.That(hero.Health, Is.EqualTo(full - 2));
            Assert.That(Built(piece).GetComponent<Dummy>(), Is.Not.Null, "and it takes hits like any dummy");
        }
    }
}
