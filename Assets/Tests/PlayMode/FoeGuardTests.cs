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
    /// A shield stops a cut and a bolt from in front and lets through the slam, a cut
    /// from behind and one from above; a summoner warns, makes one foe at a time up to
    /// its cap, makes another when one dies, and is called off by a hit.
    /// </summary>
    public class FoeGuardTests
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
        public IEnumerator AShieldStopsACutAndABoltFromInFrontAndNothingElse()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), new Shield());
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var shield = Act<Shield>(foe);
            Assert.That(foe.Facing, Is.EqualTo(1), "it faces right");
            var front = new Vector2(22.5f, Floor);
            var behind = new Vector2(18.5f, Floor);
            var above = new Vector2(21f, Floor + 1.75f);

            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, front)), Is.False, "a cut from in front");
            Assert.That(foe.Struck(new Strike(StrikeKind.Bolt, 1, front)), Is.False, "a bolt from in front");
            Assert.That(foe.Health, Is.EqualTo(9), "do nothing");
            Assert.That(foe.IsHurt, Is.False, "and do not hold it");
            Assert.That(shield.Blocked, Is.EqualTo(2));

            Assert.That(foe.Struck(new Strike(StrikeKind.Slam, 1, front)), Is.True, "the slam lands");
            Assert.That(foe.Health, Is.EqualTo(8));
            yield return probe.Step(30);
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, behind)), Is.True, "a cut from behind lands");
            Assert.That(foe.Health, Is.EqualTo(7));
            yield return probe.Step(30);
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, above)), Is.True, "a cut from above lands");
            Assert.That(foe.Health, Is.EqualTo(6));
            yield return probe.Step(30);

            foe.Face(-1);
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, behind)), Is.False, "turned, the shield is there");
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, front)), Is.True, "and its back is here");
            Assert.That(foe.Health, Is.EqualTo(5));
            Assert.That(shield.Blocked, Is.EqualTo(3));
        }

        private static Summon Summoning()
        {
            var imp = new FoeBody
            {
                health = 1,
                contactDamage = 0,
                size = new Vector2(12f, 12f),
            };
            return new Summon
            {
                summons = new FoeRecipe("imp", imp),
                cap = 2,
                windup = 0.5f,
                cooldown = 0.6f,
            };
        }

        [UnityTest]
        public IEnumerator ASummonerWarnsMakesFoesUpToItsCapAndAnotherWhenOneDies()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), Summoning());
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var summon = Act<Summon>(foe);
            yield return probe.Step(40);
            Assert.That(foe.IsTelegraphing, Is.False, "out of range it makes nothing");
            Assert.That(summon.Made, Is.Empty);

            hero.PlaceAt(new Vector2(26f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True, "in range it warns");
            var steps = 0;
            while (foe.IsTelegraphing && steps < 100)
            {
                Assert.That(summon.Made, Is.Empty, "nothing comes while it warns");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(summon.windup / MoveProbe.Dt).Within(2f), "for its whole windup");
            Assert.That(summon.Alive, Is.EqualTo(1), "then one");
            var first = summon.Made[0];
            Assert.That(first.transform.parent, Is.SameAs(foe.transform.parent), "in its room");
            Assert.That(first.Box.bounds.min.x, Is.GreaterThan(foe.Box.bounds.max.x), "ahead of it");
            yield return probe.Step(20);
            Assert.That(first.Box.bounds.min.y, Is.EqualTo(Floor).Within(2f * Pixel), "on its floor");

            yield return probe.Until(() => summon.Alive == 2, 80);
            Assert.That(summon.Alive, Is.EqualTo(2), "and a second");
            yield return probe.Step(120);
            Assert.That(summon.Made, Has.Count.EqualTo(2), "at its cap it makes no more");

            Assert.That(first.Struck(new Strike(StrikeKind.Sword, 1, Vector2.zero)), Is.True);
            Assert.That(first.IsDead, Is.True);
            Assert.That(summon.Alive, Is.EqualTo(1));
            yield return probe.Until(() => summon.Alive == 2, 80);
            Assert.That(summon.Made, Has.Count.EqualTo(3), "one dies, and it makes another");
        }

        [UnityTest]
        public IEnumerator AHitDuringTheWarningCallsTheSummoningOff()
        {
            var piece = PutFoe(20f, Floor, 1f, 1.5f, Harmless(), Summoning());
            yield return Begin(new Vector2(45f, Floor));
            var foe = FoeOf(piece);
            var summon = Act<Summon>(foe);
            hero.PlaceAt(new Vector2(26f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(foe.IsTelegraphing, Is.True);
            yield return probe.Step(5);
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, hero.transform.position)), Is.True);
            Assert.That(foe.IsTelegraphing, Is.False, "the warning ends");
            // The windup would have ended within these steps.
            yield return probe.Step(25);
            Assert.That(summon.Made, Is.Empty, "and nothing comes");
            yield return probe.Until(() => summon.Alive == 1, 80);
            Assert.That(summon.Alive, Is.EqualTo(1), "until it tries again");
        }
    }
}
