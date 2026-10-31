using System.Collections;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The first PlayMode smoke test: a hero on a floor made in code stands still,
    /// jumps to the tuned height and lands. Region scenes join once they exist.
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
    }
}
