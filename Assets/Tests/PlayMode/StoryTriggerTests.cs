using System.Collections;
using Descender.Story;
using Descender.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// A story trigger placed in the world: the hero walking in opens its dialogue,
    /// once. Everything is made in code; the lines are the real `opening` entry.
    /// </summary>
    public class StoryTriggerTests
    {
        private DialogueRig rig;

        [SetUp]
        public void SetUp()
        {
            rig = DialogueRig.Make();
        }

        [TearDown]
        public void TearDown()
        {
            rig.Dispose();
        }

        [UnityTest]
        public IEnumerator WalkingIntoATriggerOpensItsDialogueOnce()
        {
            rig.Template<StoryTrigger>("trigger");
            var placement = rig.Place(
                new UnityEngine.Vector2(6f, 0f),
                "{\"id\":\"test/Trigger1\",\"type\":\"trigger\",\"dialogue\":[\"opening\"]}"
            );
            yield return null;
            yield return DialogueRig.Steps(10);
            Assert.That(placement.Spawned, Is.Not.Null, "the trigger placement spawned nothing");
            var trigger = placement.Spawned.GetComponent<StoryTrigger>();
            Assert.That(trigger.IsAutomatic, Is.True);
            Assert.That(trigger.DialogueIds, Is.EqualTo(new[] { "opening" }));
            Assert.That(rig.Runner.Hears(trigger), Is.True, "the spawned trigger did not register with the runner");
            Assert.That(rig.Box.IsOpen, Is.False, "the hero has not walked in yet");

            rig.MoveHero(6f);
            yield return DialogueRig.Steps(5);
            Assert.That(rig.Box.IsOpen, Is.True, "walking in should open the box");
            Assert.That(rig.Box.CurrentId, Is.EqualTo("opening"));
            Assert.That(rig.Box.PageIndex, Is.EqualTo(0));
            Assert.That(ShownText(), Is.EqualTo("You are unfinished."));
            Assert.That(rig.Hero.Talking, Is.True, "the hero stands still while the box is open");
            Assert.That(StoryFlags.Has("opening_seen"), Is.False, "the flag is set when the lines end, not before");

            rig.ReadToTheEnd();
            Assert.That(rig.Box.IsOpen, Is.False);
            Assert.That(StoryFlags.Has("opening_seen"), Is.True);
            yield return null;
            yield return null;
            Assert.That(rig.Hero.Talking, Is.False, "the hero moves again once the box has closed");

            // He is still standing inside: nothing more to say.
            yield return DialogueRig.Steps(20);
            Assert.That(rig.Box.IsOpen, Is.False, "the trigger spoke again while the hero stood inside");

            rig.MoveHero(0f);
            yield return DialogueRig.Steps(5);
            Assert.That(trigger.IsPlayerNear, Is.False);
            rig.MoveHero(6f);
            yield return DialogueRig.Steps(5);
            Assert.That(trigger.IsPlayerNear, Is.True, "the hero should be inside the trigger again");
            Assert.That(rig.Box.IsOpen, Is.False, "walking in a second time must not replay the opening");
        }

        /// <summary>The text label of the box (its name label is "Name").</summary>
        private string ShownText()
        {
            foreach (var label in rig.Box.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "Text")
                {
                    return label.text;
                }
            }
            return null;
        }
    }
}
