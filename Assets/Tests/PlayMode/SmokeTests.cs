using System.Collections;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests on a floor made in code: the hero stands, takes hits and
    /// hazards, and an imp patrols, turns at a wall and dies to a hit. Region scenes
    /// join once they exist.
    /// </summary>
    public class SmokeTests
    {
        private GameObject floor;
        private GameObject hero;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            Physics2D.gravity = Vector2.zero;
            floor = new GameObject("Floor") { layer = LayerMask.NameToLayer("World") };
            var floorBox = floor.AddComponent<BoxCollider2D>();
            floorBox.size = new Vector2(40f, 1f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);

            hero = new GameObject("Hero") { layer = LayerMask.NameToLayer("Player") };
            hero.transform.position = new Vector3(0f, 1f, 0f);
            var box = hero.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 1.5f);
            box.offset = new Vector2(0f, 0.75f);
            hero.AddComponent<PlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(hero);
            Object.Destroy(floor);
            foreach (var extra in extras)
            {
                if (extra != null)
                {
                    Object.Destroy(extra);
                }
            }
            extras.Clear();
        }

        private readonly System.Collections.Generic.List<GameObject> extras =
            new System.Collections.Generic.List<GameObject>();

        private GameObject Wall(float x)
        {
            var wall = new GameObject("Wall") { layer = LayerMask.NameToLayer("World") };
            var box = wall.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 6f);
            wall.transform.position = new Vector3(x, 3f, 0f);
            extras.Add(wall);
            return wall;
        }

        private Enemies.Walker Imp(float x)
        {
            var profile = ScriptableObject.CreateInstance<Enemies.WalkerProfile>();
            profile.body = new Vector2(18f, 24f);
            profile.speed = 32f;
            profile.health = 1;
            var imp = new GameObject("Imp") { layer = LayerMask.NameToLayer("Enemy") };
            imp.transform.position = new Vector3(x, 0f, 0f);
            imp.AddComponent<BoxCollider2D>();
            imp.SetActive(false);
            var walker = imp.AddComponent<Enemies.Walker>();
            typeof(Enemies.Walker)
                .GetField("profile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(walker, profile);
            imp.SetActive(true);
            extras.Add(imp);
            return walker;
        }

        private static IEnumerator Steps(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator HeroFallsOntoTheFloorAndStandsStill()
        {
            var controller = hero.GetComponent<PlayerController>();
            for (var i = 0; i < 90; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(controller.IsGrounded, Is.True, "the hero should be standing on the floor");
            Assert.That(controller.Velocity.magnitude, Is.LessThan(0.05f));
            Assert.That(hero.transform.position.y, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator AHitTakesAHeartAndStartsInvulnerability()
        {
            var controller = hero.GetComponent<PlayerController>();
            yield return Steps(30);
            var before = controller.Health;
            Assert.That(controller.Hurt(1, hero.transform.position + Vector3.right), Is.True);
            Assert.That(controller.Health, Is.EqualTo(before - 1));
            Assert.That(controller.CanBeHurt, Is.False, "a second hit in the same breath should not land");
            Assert.That(controller.Hurt(1, hero.transform.position), Is.False);
        }

        [UnityTest]
        public IEnumerator SpikesUnderTheHeroHurtHim()
        {
            var controller = hero.GetComponent<PlayerController>();
            yield return Steps(30);
            var before = controller.Health;
            var spikes = Pickups.Hazard.Spawn(new Vector2(-1f, -0.25f), new Vector2(32f, 8f));
            extras.Add(spikes.gameObject);
            yield return Steps(10);
            Assert.That(controller.Health, Is.EqualTo(before - 1));
        }

        [UnityTest]
        public IEnumerator AnImpPatrolsAndTurnsAtTheWall()
        {
            hero.transform.position = new Vector3(-15f, 1f, 0f);
            Wall(4f);
            var imp = Imp(2f);
            yield return Steps(5);
            Assert.That(imp.Direction, Is.EqualTo(-1), "an unflipped placement walks left");
            var start = imp.transform.position.x;
            yield return Steps(30);
            Assert.That(imp.transform.position.x, Is.LessThan(start), "it should have walked");
            // Turn it round and let it reach the wall.
            Wall(-2f);
            yield return Steps(240);
            Assert.That(imp.Direction, Is.EqualTo(1), "it should have turned at the wall");
            Assert.That(imp.transform.position.x, Is.GreaterThan(-2f));
        }

        [UnityTest]
        public IEnumerator AHitKillsAnImpAndRecordsTheKill()
        {
            hero.transform.position = new Vector3(-15f, 1f, 0f);
            var imp = Imp(5f);
            imp.PlacementId = "test:imp:0";
            yield return Steps(5);
            Assert.That(imp.Hit(1, imp.transform.position + Vector3.left), Is.True);
            Assert.That(imp.IsDead, Is.True);
            Assert.That(Enemies.Kills.IsDead("test:imp:0"), Is.True);
            yield return Steps(5);
            var table = new Economy.LootTable { LightMin = 3, LightMax = 3, Xp = 2 };
            Pickups.Loot.Drop(new Vector2(5f, 1f), table);
            Assert.That(Pickups.Loot.LastDropCount, Is.EqualTo(1), "three light is one drop");
            var drop = Object.FindAnyObjectByType<Pickups.LightDrop>();
            Assert.That(drop, Is.Not.Null);
            Assert.That(drop.Amount, Is.EqualTo(3));
            extras.Add(drop.gameObject);
        }
    }
}
