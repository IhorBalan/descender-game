using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Enemies;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The boss template: a charge warns, runs its distance, hurts a hero in its way and
    /// stops at a wall; a pattern changes phase at each health threshold, warning and
    /// taking no harm while it does; and its attacks come round in order, each one
    /// telegraphed before it can hurt.
    /// </summary>
    public class BossTests
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

        private Transform Built(Piece piece) => lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));

        private Piece PutFoe(float x, float y, float width, float height, FoeBody with, params FoeBehaviour[] acts)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Foe;
            piece.rect = new Rect(x, y, width, height);
            piece.foe = new FoeRecipe("foe", with, acts);
            room.pieces.Add(piece);
            return piece;
        }

        private Foe FoeOf(Piece piece) => Built(piece).GetComponent<Foe>();

        private static T Act<T>(Foe foe)
            where T : FoeBehaviour
        {
            foreach (var act in foe.Behaviours)
            {
                if (act is T found)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>A body that does nothing by touch, so only its attacks can hurt.</summary>
        private static FoeBody Harmless(bool armour = false)
        {
            return new FoeBody
            {
                health = 9,
                contactDamage = 0,
                armour = armour,
            };
        }

        [UnityTest]
        public IEnumerator AChargeWarnsRunsItsDistanceHurtsHimInItsWayAndStopsAtAWall()
        {
            var open = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Charge());
            var walled = PutFoe(40f, Floor, 1f, 1.5f, Harmless(), new Charge());
            Put(43f, Floor, 1f, 4f);
            yield return Begin(new Vector2(8f, Floor));
            var foe = FoeOf(open);
            var charge = Act<Charge>(foe);
            var full = hero.Health;
            var stoodAt = foe.transform.position.x;

            hero.PlaceAt(new Vector2(24f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True, "in range it warns");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(hero.Health, Is.EqualTo(full), "nothing hurts while it warns");
                Assert.That(foe.transform.position.x, Is.EqualTo(stoodAt).Within(Pixel), "and it stands still");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(charge.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(charge.IsCharging, Is.True, "then it runs");
            yield return probe.Until(() => !charge.IsCharging, 100);
            var ran = (foe.transform.position.x - stoodAt) / Pixel;
            Assert.That(ran, Is.EqualTo(charge.distance).Within(charge.speed * MoveProbe.Dt * 2f + 2f), "its distance");
            Assert.That(hero.Health, Is.EqualTo(full - 1), "and he was in its way");

            // Beyond a wall from the second, and out of the first one's range.
            hero.PlaceAt(new Vector2(45.5f, Floor));
            var other = FoeOf(walled);
            var blocked = Act<Charge>(other);
            yield return probe.Until(() => blocked.IsCharging, 60);
            Assert.That(blocked.IsCharging, Is.True);
            yield return probe.Until(() => !blocked.IsCharging, 100);
            yield return probe.Step(3);
            Assert.That(other.Box.bounds.max.x, Is.EqualTo(43f).Within(5f * Pixel), "a wall stops it");
        }

        private static FoeBody Boss()
        {
            var body = Harmless(true);
            body.health = 10;
            body.speed = 0f;
            return body;
        }

        [UnityTest]
        public IEnumerator APatternChangesPhaseAtEachThresholdWarningAndTakingNoHarmWhileItDoes()
        {
            var pattern = new Pattern();
            var first = new BossPhase(0.7f, 0.3f, new Swing());
            var second = new BossPhase(0.4f, 0.3f, new Swing());
            var last = new BossPhase(0f, 0.3f, new Swing());
            pattern.phases = new[] { first, second, last };
            var piece = PutFoe(20f, Floor, 2f, 2.5f, Boss(), pattern);
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var mine = Act<Pattern>(foe);
            var blow = new Strike(StrikeKind.Slam, 3, hero.transform.position);
            yield return probe.Step(5);
            Assert.That(mine.Phase, Is.EqualTo(0));
            Assert.That(foe.IsTelegraphing, Is.False);

            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, hero.transform.position)), Is.True);
            yield return probe.Step(5);
            Assert.That(mine.Phase, Is.EqualTo(0), "above the threshold nothing changes");
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 2, hero.transform.position)), Is.True);
            yield return probe.Until(() => mine.Phase == 1, 5);
            Assert.That(mine.Phase, Is.EqualTo(1), "at seven of ten the second phase begins");
            Assert.That(mine.IsChanging, Is.True);
            Assert.That(foe.IsTelegraphing, Is.True, "with a warning");
            Assert.That(foe.Struck(blow), Is.False, "and no harm while it changes");
            Assert.That(foe.Health, Is.EqualTo(7));
            var steps = 0;
            while (mine.IsChanging && steps < 200)
            {
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(mine.changeTime / MoveProbe.Dt).Within(3f), "for its change time");
            Assert.That(foe.IsTelegraphing, Is.False);

            Assert.That(foe.Struck(blow), Is.True);
            Assert.That(foe.Health, Is.EqualTo(4));
            yield return probe.Until(() => mine.Phase == 2, 5);
            Assert.That(mine.Phase, Is.EqualTo(2), "at four of ten the third");
            yield return probe.Until(() => !mine.IsChanging, 200);
            Assert.That(foe.Struck(blow), Is.True);
            yield return probe.Step(10);
            Assert.That(foe.Health, Is.EqualTo(1));
            Assert.That(mine.Phase, Is.EqualTo(2), "the last phase lasts");
            Assert.That(mine.Changes, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ItsAttacksComeRoundInOrderEachOneTelegraphedFirst()
        {
            // He stands seven units off: near enough to set each attack off, too far for either to reach.
            var swing = new Swing
            {
                range = 140f,
                reach = new Vector2(10f, 20f),
                cooldown = 0f,
            };
            var charge = new Charge
            {
                range = 200f,
                distance = 20f,
                cooldown = 0f,
            };
            var pattern = new Pattern();
            pattern.phases = new[] { new BossPhase(0f, 0.3f, swing, charge) };
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Boss(), pattern);
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var mine = Act<Pattern>(foe);
            var cut = (Swing)mine.phases[0].attacks[0];
            var rush = (Charge)mine.phases[0].attacks[1];
            var full = hero.Health;
            hero.PlaceAt(new Vector2(27.5f, Floor));

            var order = new List<int>();
            var warned = false;
            var warning = false;
            var acting = false;
            for (var i = 0; i < 400; i++)
            {
                yield return probe.Step();
                if (foe.IsTelegraphing && !warning)
                {
                    order.Add(mine.Attack);
                    warned = true;
                }
                warning = foe.IsTelegraphing;
                var now = cut.IsStriking || rush.IsCharging;
                if (now && !acting)
                {
                    Assert.That(warned, Is.True, "attack " + mine.Attack + " came without a warning");
                    Assert.That(foe.IsTelegraphing, Is.False, "the warning is over when it acts");
                    warned = false;
                }
                acting = now;
            }
            Assert.That(order.Count, Is.GreaterThanOrEqualTo(4), "it keeps attacking");
            var began = order.GetRange(0, 4);
            Assert.That(began, Is.EqualTo(new[] { 0, 1, 0, 1 }), "the swing, the charge, and round again");
            Assert.That(hero.Health, Is.EqualTo(full), "out of their reach nothing hurt him");
        }
    }
}
