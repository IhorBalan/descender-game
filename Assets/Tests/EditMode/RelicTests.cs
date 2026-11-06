using Descender.Story;
using Descender.UI;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>A relic reads its placement: the item it is and the lines it carries.</summary>
    public class RelicTests
    {
        private GameObject placementObject;
        private GameObject relicObject;

        [SetUp]
        public void SetUp()
        {
            StoryFlags.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            StoryFlags.Reset();
            Object.DestroyImmediate(placementObject);
            if (relicObject != null)
            {
                Object.DestroyImmediate(relicObject);
            }
        }

        private WorldPlacement Placement()
        {
            placementObject = new GameObject("Placement");
            var placement = placementObject.AddComponent<WorldPlacement>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("type").stringValue = "item";
            serialized.FindProperty("item").stringValue = "token";
            NpcPlacementTests.SetStrings(serialized.FindProperty("dialogue"), "item_token");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return placement;
        }

        [Test]
        public void ConfigureTakesTheItemAndLines()
        {
            relicObject = new GameObject("Relic");
            var relic = relicObject.AddComponent<Relic>();

            relic.Configure(Placement());

            Assert.That(relic.ItemId, Is.EqualTo("token"));
            Assert.That(relic.DialogueIds, Is.EqualTo(new[] { "item_token" }));
            Assert.That(relic.RotateIds, Is.Empty);
            Assert.That(relic.IsAutomatic, Is.False, "a relic is examined with the interact key");
            Assert.That(Relic.FlagFor("token"), Is.EqualTo("item:token"));
            Assert.That(relicObject != null, Is.True, "a relic the save does not have yet stands");
        }

        [Test]
        public void ARelicTheSaveAlreadyHasRemovesItselfOnConfigure()
        {
            StoryFlags.Set("item:token");
            relicObject = new GameObject("Relic");
            var relic = relicObject.AddComponent<Relic>();

            relic.Configure(Placement());

            Assert.That(relicObject == null, Is.True, "the relic should be gone");
        }
    }
}
