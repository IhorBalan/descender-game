using System.Collections;
using System.Collections.Generic;
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
    /// The lantern fruit: the hero beneath sets it shivering for its warning time, then it
    /// falls and bursts on him; it grows back on its stalk after its time; a strike pops it
    /// and hurts no one; and a hero who keeps running is past before it lands.
    /// </summary>
    public class LanternFruitTests
    {
        private const float Floor = 2f;
        private const float FruitX = 20f;
        private const float Side = 0.75f;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private Station room;
        private LabRoot lab;
        private PlayerController hero;
        private ScriptedInput input;
        private MoveProbe probe;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Kills.Reset();
            Pickups.Checkpoint.Reset();
            LiveTuning.Forget();
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            room = new Station();
            room.id = "test";
            room.title = "test";
            room.room = new Rect(0f, 0f, 56f, 30f);
            room.spawn = new Vector2(4f, Floor);
            Put(PieceKind.Solid, 0f, 0f, 56f, Floor);
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

        private Piece Put(PieceKind kind, float x, float y, float width, float height)
        {
            var piece = new Piece();
            piece.kind = kind;
            piece.rect = new Rect(x, y, width, height);
            room.pieces.Add(piece);
            return piece;
        }

        /// <summary>A fruit hanging with its middle at (x, y), under a branch a stalk's length above.</summary>
        private Piece Hang(float x, float y)
        {
            Put(PieceKind.Solid, x - 1f, y + Side / 2f + 0.5f, 2f, 0.5f);
            return Put(PieceKind.Fruit, x - Side / 2f, y - Side / 2f, Side, Side);
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
            input = new ScriptedInput();
            hero.InputSource = input;
            probe = new MoveProbe(hero, input);
            yield return null;
            lab.Enter(hero, 0);
            hero.PlaceAt(at);
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, "he stands where the test put him");
        }

        private LanternFruit FruitAt(Piece piece)
        {
            foreach (var fruit in lab.GetComponentsInChildren<LanternFruit>())
            {
                if (Vector2.Distance(fruit.transform.position, piece.rect.center) < 0.5f)
                {
                    return fruit;
                }
            }
            Assert.Fail("no fruit was built for the piece at " + piece.rect);
            return null;
        }

        private static int Steps(float seconds) => Mathf.RoundToInt(seconds / Time.fixedDeltaTime);

        [UnityTest]
        public IEnumerator BeneathItItShiversForItsWarningThenFallsAndBurstsOnHim()
        {
            var piece = Hang(FruitX, Floor + 5f);
            yield return Begin(new Vector2(4f, Floor));
            var fruit = FruitAt(piece);
            Assert.That(fruit.IsHanging, Is.True, "it hangs while he is away");
            var health = hero.Health;

            hero.PlaceAt(new Vector2(FruitX, Floor));
            yield return probe.Until(() => fruit.IsRipening, 10);
            Assert.That(fruit.IsRipening, Is.True, "him beneath sets it shivering");
            var shivered = 0;
            while (fruit.IsRipening && shivered < 100)
            {
                yield return probe.Step();
                shivered++;
            }
            Assert.That(fruit.IsFalling, Is.True, "after the shiver it lets go");
            Assert.That(shivered, Is.EqualTo(Steps(LanternFruit.RipenTime)).Within(2), "the warning lasts its time");

            yield return probe.Until(() => fruit.IsGone, 200);
            Assert.That(fruit.IsGone, Is.True, "it burst");
            Assert.That(fruit.Bursts, Is.EqualTo(1));
            Assert.That(hero.Health, Is.EqualTo(health - LanternFruit.FallDamage), "it landed on him, once");
        }

        [UnityTest]
        public IEnumerator ItGrowsBackOnItsStalkAfterItsTime()
        {
            var piece = Hang(FruitX, Floor + 5f);
            yield return Begin(new Vector2(4f, Floor));
            var fruit = FruitAt(piece);
            hero.PlaceAt(new Vector2(FruitX, Floor));
            yield return probe.Until(() => fruit.IsGone, 300);
            Assert.That(fruit.IsGone, Is.True, "it fell and burst");
            hero.PlaceAt(new Vector2(4f, Floor));

            yield return probe.Step(Steps(LanternFruit.RegrowTime) - 5);
            Assert.That(fruit.IsGone, Is.True, "not back before its time");
            yield return probe.Until(() => fruit.IsGrowing, 10);
            Assert.That(fruit.IsGrowing, Is.True, "it starts to grow back at its time");
            Assert.That((Vector2)fruit.transform.position, Is.EqualTo(fruit.Home), "on its stalk");
            yield return probe.Until(() => fruit.IsHanging, Steps(LanternFruit.GrowTime) + 5);
            Assert.That(fruit.IsHanging, Is.True, "grown, it hangs again");
        }

        [UnityTest]
        public IEnumerator AStrikePopsItAndHurtsNoOne()
        {
            // Inside the blade, at his chest, and further aside than it looks beneath.
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var dx = (tuning.attackOffset.x + 10f) / PlayerTuning.PixelsPerUnit;
            var dy = tuning.attackOffset.y / PlayerTuning.PixelsPerUnit;
            Object.Destroy(tuning);
            Assert.That(dx * PlayerTuning.PixelsPerUnit, Is.GreaterThan(LanternFruit.DropRangeX));
            var piece = Hang(10f + dx, Floor + dy);
            yield return Begin(new Vector2(10f, Floor));
            var fruit = FruitAt(piece);
            Assert.That(hero.Facing, Is.EqualTo(1), "he faces it");
            Assert.That(fruit.IsHanging, Is.True, "beside him it does not fall");
            var health = hero.Health;

            var filter = Combat.Layers.Any(Combat.Layers.EnemyMask);
            var found = new Collider2D[4];
            var seen = Physics2D.OverlapBox(fruit.transform.position, Vector2.one * 0.2f, 0f, filter, found);
            Assert.That(seen, Is.EqualTo(1), "its body is on the enemies' layer where attacks look");
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "the press started a swing");
            yield return probe.Until(() => hero.State != PlayerState.Attack, 100);
            Assert.That(fruit.Pops, Is.EqualTo(1), "the swing popped it");
            Assert.That(fruit.IsGone, Is.True);
            Assert.That(fruit.Bursts, Is.Zero, "a pop is not a burst");
            Assert.That(hero.Health, Is.EqualTo(health), "and hurts no one");
        }

        [UnityTest]
        public IEnumerator AHeroWhoKeepsRunningIsPastBeforeItLands()
        {
            var piece = Hang(FruitX, Floor + 5f);
            yield return Begin(new Vector2(4f, Floor));
            var fruit = FruitAt(piece);
            var health = hero.Health;

            input.MoveX = 1f;
            yield return probe.Until(() => fruit.IsGone, 400);
            input.MoveX = 0f;
            Assert.That(fruit.IsGone, Is.True, "it fell and burst behind him");
            Assert.That(fruit.Bursts, Is.EqualTo(1));
            Assert.That(hero.transform.position.x, Is.GreaterThan(FruitX + 1f), "he was past it");
            Assert.That(hero.Health, Is.EqualTo(health), "and it missed");
        }
    }
}
