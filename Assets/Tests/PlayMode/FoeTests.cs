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
    /// A foe made of parts: a patrol turns at a wall and at an edge and stays on its
    /// platform; its touch hurts the hero no more often than his safe time lets; cuts
    /// take its health, push it by the strike's push and kill it; and a heavy body is
    /// barely moved.
    /// </summary>
    public class FoeTests
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

        [UnityTest]
        public IEnumerator APatrolTurnsAtAWallAndAtAnEdgeAndStaysOnItsPlatform()
        {
            // On the floor, walking right into a wall three units on.
            Put(20f, Floor, 1f, 3f);
            var onFloor = PutFoe(16f, Floor, 1f, 1.5f, new FoeBody(), new Patrol());
            // On a platform six units wide, two up.
            Put(30f, Floor, 6f, 2f);
            var onTop = PutFoe(32.5f, Floor + 2f, 1f, 1.5f, new FoeBody(), new Patrol());
            room.spawn = new Vector2(50f, Floor);
            yield return Begin(new Vector2(50f, Floor));
            var walker = FoeOf(onFloor);
            var keeper = FoeOf(onTop);
            Assert.That(walker.Facing, Is.EqualTo(1));

            var farthest = walker.transform.position.x;
            yield return probe.Until(
                () =>
                {
                    farthest = Mathf.Max(farthest, walker.Box.bounds.max.x);
                    return walker.Facing < 0;
                },
                200
            );
            Assert.That(walker.Facing, Is.EqualTo(-1), "it turned");
            Assert.That(farthest, Is.EqualTo(20f).Within(3f * Pixel), "at the wall");

            // Eight seconds: at its pace it crosses the platform more than twice.
            var turns = 0;
            var facing = keeper.Facing;
            var lowest = keeper.Box.bounds.min.y;
            var left = keeper.Box.bounds.min.x;
            var right = keeper.Box.bounds.max.x;
            for (var i = 0; i < 400; i++)
            {
                yield return probe.Step();
                if (keeper.Facing != facing)
                {
                    facing = keeper.Facing;
                    turns++;
                }
                var bounds = keeper.Box.bounds;
                lowest = Mathf.Min(lowest, bounds.min.y);
                left = Mathf.Min(left, bounds.min.x);
                right = Mathf.Max(right, bounds.max.x);
            }
            Assert.That(turns, Is.GreaterThanOrEqualTo(2), "it turned at both edges");
            Assert.That(lowest, Is.GreaterThan(Floor + 2f - 2f * Pixel), "and never left the platform");
            Assert.That(left, Is.GreaterThan(30f - 4f * Pixel));
            Assert.That(right, Is.LessThan(36f + 4f * Pixel));
        }

        [UnityTest]
        public IEnumerator ItsTouchHurtsHimNoOftenerThanHisSafeTimeLets()
        {
            // A wide body that stands still, so the throw of a hit leaves him inside it.
            var piece = PutFoe(HeroAt - 5f, Floor, 10f, 2f, new FoeBody { contactDamage = 1 });
            room.spawn = new Vector2(50f, Floor);
            yield return Begin(new Vector2(50f, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var safeSteps = tuning.invulnerableTime / MoveProbe.Dt;
            Object.Destroy(tuning);
            var full = hero.Health;
            yield return probe.Step(20);
            Assert.That(hero.Health, Is.EqualTo(full), "out of its touch nothing hurts him");

            hero.PlaceAt(new Vector2(HeroAt, Floor));
            yield return probe.Until(() => hero.Health < full, 10);
            Assert.That(hero.Health, Is.EqualTo(full - 1), "its touch hurts");
            var steps = 0;
            while (hero.Health == full - 1 && steps < 200)
            {
                yield return probe.Step();
                steps++;
            }
            Assert.That(hero.Health, Is.EqualTo(full - 2), "still touching it, he is hurt again");
            Assert.That(steps, Is.EqualTo(safeSteps).Within(4f), "once his safe time is over");
            Assert.That(FoeOf(piece).Health, Is.EqualTo(FoeOf(piece).Body.health), "and it took nothing");
        }

        [UnityTest]
        public IEnumerator CutsTakeItsHealthPushItAndKillIt()
        {
            var with = new FoeBody { health = 3, contactDamage = 0 };
            var piece = PutFoe(HeroAt + 0.75f, Floor, 1f, 1.5f, with);
            yield return Begin(new Vector2(HeroAt, Floor));
            var foe = FoeOf(piece);
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var slide = tuning.attackPush * tuning.attackPush / (2f * Foe.Friction);
            Object.Destroy(tuning);
            IEnemy died = null;
            foe.Died += who => died = who;
            var stoodAt = foe.transform.position.x;

            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => foe.Health < 3, 30);
            Assert.That(foe.Health, Is.EqualTo(2), "a cut takes its damage");
            Assert.That(foe.IsHurt, Is.True, "and holds it a moment");
            yield return probe.Step(40);
            var pushed = (foe.transform.position.x - stoodAt) / Pixel;
            Assert.That(pushed, Is.EqualTo(slide).Within(2f), "pushed as far as the cut's push says");
            Assert.That(foe.IsHurt, Is.False);

            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => foe.Health < 2, 30);
            Assert.That(foe.Health, Is.EqualTo(1));
            Assert.That(foe.IsDead, Is.False);
            yield return probe.Step(40);
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => foe.IsDead, 30);
            Assert.That(foe.IsDead, Is.True, "the third kills it");
            Assert.That(died, Is.SameAs(foe), "it says so");
            Assert.That(foe.gameObject.activeSelf, Is.False, "and it is gone");
            var late = new Strike(StrikeKind.Sword, 1, Vector2.zero);
            Assert.That(foe.Struck(late), Is.False, "a dead one takes nothing");
        }

        [UnityTest]
        public IEnumerator AHeavyBodyIsBarelyMovedAndAStrikeWithNoPushStillShovesItAway()
        {
            var unmoved = new FoeBody { health = 9, pushShare = 0f, contactDamage = 0 };
            var heavy = PutFoe(HeroAt + 4f, Floor, 1f, 1.5f, unmoved);
            var light = PutFoe(HeroAt + 8f, Floor, 1f, 1.5f, new FoeBody { health = 9, contactDamage = 0 });
            yield return Begin(new Vector2(HeroAt, Floor));
            var stone = FoeOf(heavy);
            var leaf = FoeOf(light);
            var stoneAt = stone.transform.position.x;
            var leafAt = leaf.transform.position.x;
            var from = new Vector2(HeroAt, Floor + 1f);

            Assert.That(stone.Struck(new Strike(StrikeKind.Slam, 2, from, new Vector2(200f, 0f))), Is.True);
            Assert.That(leaf.Hit(1, from), Is.True, "a plain hit counts as a cut");
            yield return probe.Step(40);
            Assert.That(stone.Health, Is.EqualTo(7));
            Assert.That(stone.transform.position.x, Is.EqualTo(stoneAt).Within(0.5f * Pixel), "nothing moves it");
            var slide = Foe.PlainPush * Foe.PlainPush / (2f * Foe.Friction);
            var moved = (leaf.transform.position.x - leafAt) / Pixel;
            Assert.That(moved, Is.EqualTo(slide).Within(2f), "shoved away from where the hit came");
        }
    }
}
