using System.Collections;
using Descender.Input;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The hero's input seam: with a ScriptedInput as his InputSource he runs and jumps
    /// on what the script sets, and a press is a press until it is cleared.
    /// </summary>
    public class HeroInputTests
    {
        private GameObject floor;
        private GameObject hero;
        private PlayerController controller;
        private ScriptedInput input;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Pickups.Checkpoint.Reset();
            Physics2D.gravity = Vector2.zero;
            floor = new GameObject("Floor") { layer = LayerMask.NameToLayer("World") };
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            hero = new GameObject("Hero") { layer = LayerMask.NameToLayer("Player") };
            hero.transform.position = new Vector3(0f, 0.05f, 0f);
            var box = hero.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 1.5f);
            box.offset = new Vector2(0f, 0.75f);
            controller = hero.AddComponent<PlayerController>();
            input = new ScriptedInput();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(hero);
            Object.Destroy(floor);
        }

        private static IEnumerator Steps(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator AScriptedInputMovesTheHero()
        {
            Assert.That(controller.InputSource, Is.Null, "the router is the source until a script sets one");
            controller.InputSource = input;
            yield return Steps(20);
            input.MoveX = 1f;
            yield return Steps(40);
            Assert.That(controller.MoveInput, Is.EqualTo(1f));
            Assert.That(hero.transform.position.x, Is.GreaterThan(2f), "he should have run right");
            input.MoveX = -1f;
            var turned = hero.transform.position.x;
            yield return Steps(40);
            Assert.That(hero.transform.position.x, Is.LessThan(turned), "he should have run back left");
        }

        [UnityTest]
        public IEnumerator AJumpPressIsReadUntilItIsCleared()
        {
            controller.InputSource = input;
            yield return Steps(20);
            Assert.That(controller.IsGrounded, Is.True);
            input.JumpPressed = true;
            input.JumpHeld = true;
            yield return Steps(3);
            Assert.That(controller.IsGrounded, Is.False, "the press should have started a jump");
            Assert.That(controller.Velocity.y, Is.GreaterThan(0f));
            input.ClearPresses();
            Assert.That(input.JumpPressed, Is.False);
            Assert.That(input.JumpHeld, Is.True, "clearing presses leaves the held keys");
            input.Release();
            Assert.That(input.JumpHeld, Is.False);
            yield return Steps(90);
            Assert.That(controller.IsGrounded, Is.True, "he lands and, with the press cleared, does not jump again");
        }
    }
}
