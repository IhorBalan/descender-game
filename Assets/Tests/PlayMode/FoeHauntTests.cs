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
    /// What the old flyers and the hanged thing needed: a flier comes at a hero in its
    /// sight, lets him go beyond it and drifts back to its post; and a dive with its own
    /// damage takes that, not the body's touch.
    /// </summary>
    public class FoeHauntTests
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

        /// <summary>A flying body with a touch of a damage; 0 for one that only its attacks make hurt.</summary>
        private static FoeBody Flying(int touch)
        {
            return new FoeBody
            {
                health = 9,
                contactDamage = touch,
                flies = true,
            };
        }

        [UnityTest]
        public IEnumerator AHauntComesAtAHeroInItsSightLetsHimGoBeyondItAndDriftsBackToItsPost()
        {
            var piece = PutFoe(20f, Floor + 5f, 1f, 1f, Flying(0), new Haunt(), new Hover { span = 16f });
            var post = new Vector2(20.5f, Floor + 5.5f);
            yield return Begin(new Vector2(44f, Floor));
            var foe = FoeOf(piece);
            var haunt = Act<Haunt>(foe);
            yield return probe.Step(20);
            Assert.That(haunt.IsHaunting, Is.False, "out of its sight it keeps its post");
            Assert.That(((Vector2)foe.transform.position - post).magnitude, Is.LessThan(1f));

            // His middle is 56 pixels along and 72 down from its own: 91 away, inside 110.
            hero.PlaceAt(new Vector2(24f, Floor));
            yield return probe.Step(3);
            Assert.That(haunt.IsHaunting, Is.True, "in its sight it comes");
            Assert.That(foe.Facing, Is.EqualTo(1), "facing him");
            yield return probe.Step(150);
            var middle = (Vector2)hero.transform.position + Vector2.up;
            var off = ((Vector2)foe.Box.bounds.center - middle).magnitude;
            Assert.That(off, Is.LessThan(3f * Pixel), "all the way to him");

            hero.PlaceAt(new Vector2(44f, Floor));
            yield return probe.Step(3);
            Assert.That(haunt.IsHaunting, Is.False, "beyond its sight it lets him go");
            yield return probe.Step(300);
            Assert.That(foe.transform.position.y, Is.EqualTo(post.y).Within(2f * Pixel), "and drifts back up");
            Assert.That(foe.transform.position.x, Is.EqualTo(post.x).Within(1f), "to its post");
        }

        [UnityTest]
        public IEnumerator ADiveWithItsOwnDamageTakesThatAndNotTheBodysTouch()
        {
            var piece = PutFoe(20f, Floor + 5f, 1f, 1f, Flying(1), new Dive { damage = 2 });
            yield return Begin(new Vector2(44f, Floor));
            var foe = FoeOf(piece);
            var dive = Act<Dive>(foe);
            var full = hero.Health;
            yield return probe.Step(10);
            Assert.That(dive.IsAttacking, Is.False);

            // Straight under it.
            hero.PlaceAt(new Vector2(20.5f, Floor));
            yield return probe.Until(() => foe.IsTelegraphing, 5);
            Assert.That(dive.IsWindingUp, Is.True, "under it, it warns");
            yield return probe.Until(() => hero.Health < full, 100);
            Assert.That(dive.IsDiving, Is.True, "it lands on him in the dive");
            Assert.That(hero.Health, Is.EqualTo(full - 2), "which takes its own two hearts, once");
        }

        [UnityTest]
        public IEnumerator ABossSleepsUntilHeComesNearThenWarnsAndFights()
        {
            var profile = ScriptableObject.CreateInstance<BossProfile>();
            profile.character = "brute";
            profile.body = new Vector2(32f, 48f);
            profile.health = 12;
            profile.contactDamage = 0;
            var recipe = FoeKinds.FromBoss("test", profile, null);
            var piece = PutFoe(20f, Floor, 2f, 3f, recipe.body, recipe.behaviours);
            yield return Begin(new Vector2(46f, Floor));
            var boss = FoeOf(piece);
            var wake = Act<Ambush>(boss);
            var pattern = Act<Pattern>(boss);
            var stood = boss.transform.position.x;
            yield return probe.Step(40);
            Assert.That(wake.IsAwake, Is.False, "far off, it sleeps");
            Assert.That(boss.IsTelegraphing, Is.False);
            Assert.That(boss.transform.position.x, Is.EqualTo(stood).Within(Pixel), "and does not move");

            // Six units from its middle: 96 pixels, inside the 128 it wakes at.
            hero.PlaceAt(new Vector2(27f, Floor));
            yield return probe.Until(() => boss.IsTelegraphing, 5);
            Assert.That(boss.IsTelegraphing, Is.True, "near, it warns as it wakes");
            Assert.That(wake.IsAwake, Is.False, "for its whole warning");
            yield return probe.Until(() => wake.IsAwake, 60);
            Assert.That(wake.IsAwake, Is.True);
            var fought = false;
            for (var i = 0; i < 200 && !fought; i++)
            {
                yield return probe.Step();
                fought = pattern.IsBusy;
            }
            Assert.That(fought, Is.True, "then it fights");
            Object.Destroy(profile);
        }
    }
}
