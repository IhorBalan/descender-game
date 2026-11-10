using Descender.Story;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The story is parked: its triggers, talking NPCs and relics do not stand, the
    /// mechanics among the placements do, and turning the mode off brings the story back.
    /// </summary>
    public class StoryModeTests
    {
        private GameObject placementObject;
        private bool parkedBefore;

        [SetUp]
        public void SetUp()
        {
            parkedBefore = StoryMode.Parked;
            StoryFlags.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            StoryMode.Parked = parkedBefore;
            Object.DestroyImmediate(placementObject);
        }

        [Test]
        public void TheStoryIsParkedInTheGame()
        {
            Assert.That(StoryMode.Parked, Is.True, "the story came back without docs/story/PARKED.md being followed");
        }

        [Test]
        public void TriggersTalkersAndRelicsAreStoryAndTheRestIsNot()
        {
            Assert.That(StoryMode.IsStory("trigger", true, false), Is.True);
            Assert.That(StoryMode.IsStory("npc", true, false), Is.True);
            Assert.That(StoryMode.IsStory("npc", true, true), Is.False, "a trader is a mechanic");
            Assert.That(StoryMode.IsStory("item", true, false), Is.True, "an item with lines is a relic");
            Assert.That(StoryMode.IsStory("item", false, false), Is.False, "an item without lines is loot");
            foreach (var type in new[] { "start", "save", "ability", "enemy", "upgrade", "power" })
            {
                Assert.That(StoryMode.IsStory(type, true, false), Is.False, type);
            }
        }

        [Test]
        public void AParkedStoryLeavesItsPlacementsOutAndComesBack()
        {
            var trigger = Placement("trigger", "", "opening");
            StoryMode.Parked = true;
            Assert.That(trigger.ShouldSpawn(), Is.False);
            StoryMode.Parked = false;
            Assert.That(trigger.ShouldSpawn(), Is.True);
        }

        [Test]
        public void MechanicsStandWhileTheStoryIsParked()
        {
            StoryMode.Parked = true;
            Assert.That(Placement("ability", "").ShouldSpawn(), Is.True);
            Object.DestroyImmediate(placementObject);
            Assert.That(Placement("item", "").ShouldSpawn(), Is.True, "loot");
            Object.DestroyImmediate(placementObject);
            Assert.That(Placement("npc", "pell", "pell_intro").ShouldSpawn(), Is.True, "a trader");
            Object.DestroyImmediate(placementObject);
            Assert.That(Placement("npc", "", "mereth_intro").ShouldSpawn(), Is.False, "a talker");
        }

        private WorldPlacement Placement(string type, string shop, params string[] dialogue)
        {
            placementObject = new GameObject("Placement");
            var placement = placementObject.AddComponent<WorldPlacement>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("type").stringValue = type;
            serialized.FindProperty("shop").stringValue = shop;
            NpcPlacementTests.SetStrings(serialized.FindProperty("dialogue"), dialogue);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return placement;
        }
    }
}
