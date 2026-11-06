using System.Collections;
using Descender.Story;
using Descender.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// A relic placed in the world: an item placement with dialogue spawns the relic
    /// (not the loot), examining it reads its lines, sets `item:&lt;id&gt;` and removes it,
    /// and one the save already has never stands.
    /// </summary>
    public class RelicTests
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
        public IEnumerator ExaminingARelicSetsItsFlagAndRemovesIt()
        {
            rig.Template("item");
            rig.Template<Relic>("relic");
            StoryFlags.Set("item:ledger");
            var token = rig.Place(
                new Vector2(6f, 0f),
                "{\"id\":\"test/Item1\",\"type\":\"item\",\"item\":\"token\",\"dialogue\":[\"item_token\"]}"
            );
            var ledger = rig.Place(
                new Vector2(12f, 0f),
                "{\"id\":\"test/Item2\",\"type\":\"item\",\"item\":\"ledger\",\"dialogue\":[\"item_ledger\"]}"
            );
            var loot = rig.Place(new Vector2(16f, 0f), "{\"id\":\"test/Item3\",\"type\":\"item\",\"item\":\"token\"}");
            yield return null;
            yield return null;
            yield return DialogueRig.Steps(5);

            Assert.That(ledger.Spawned == null, Is.True, "a relic whose flag is set must not stand");
            Assert.That(loot.Spawned, Is.Not.Null);
            Assert.That(loot.Spawned.GetComponent<Relic>(), Is.Null, "an item without dialogue is loot, not a relic");
            Assert.That(token.Spawned, Is.Not.Null, "the relic placement spawned nothing");
            var relic = token.Spawned.GetComponent<Relic>();
            Assert.That(relic, Is.Not.Null, "an item with dialogue should spawn the relic");
            Assert.That(relic.ItemId, Is.EqualTo("token"));
            Assert.That(rig.Runner.Hears(relic), Is.True);

            rig.MoveHero(6f);
            yield return DialogueRig.Steps(5);
            Assert.That(relic.IsPlayerNear, Is.True, "the hero stands at the relic");
            Assert.That(rig.Box.IsOpen, Is.False, "a relic waits for the interact key");

            // The key press, as Interactable.Update raises it with an InputRouter in the scene.
            Assert.That(rig.Runner.Interact(relic), Is.True);
            Assert.That(rig.Box.CurrentId, Is.EqualTo("item_token"));
            Assert.That(StoryFlags.Has("item:token"), Is.False);

            rig.ReadToTheEnd();
            Assert.That(StoryFlags.Has("item:token"), Is.True);
            Assert.That(rig.Runner.Hears(relic), Is.False, "the examined relic stopped listening at once");
            yield return null;
            Assert.That(token.Spawned == null, Is.True, "the examined relic should be gone");
        }
    }
}
