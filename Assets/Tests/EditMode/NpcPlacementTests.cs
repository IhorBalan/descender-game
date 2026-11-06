using Descender.EditorTools.World;
using Descender.Fx;
using Descender.UI;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// An NPC reads its placement: the name, the shop, which way the art faces, the
    /// lines, and the body of the character it names (or a plain block).
    /// </summary>
    public class NpcPlacementTests
    {
        private GameObject placementObject;
        private GameObject npcObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(placementObject);
            Object.DestroyImmediate(npcObject);
        }

        private WorldPlacement Placement(string character, bool facesLeft)
        {
            placementObject = new GameObject("Placement");
            var placement = placementObject.AddComponent<WorldPlacement>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("type").stringValue = "npc";
            serialized.FindProperty("placementName").stringValue = "Old Pell";
            serialized.FindProperty("character").stringValue = character;
            serialized.FindProperty("shop").stringValue = "pell";
            serialized.FindProperty("facesLeft").boolValue = facesLeft;
            serialized.FindProperty("tint").colorValue = new Color(0.5f, 0.6f, 0.7f);
            SetStrings(serialized.FindProperty("dialogue"), "pell_intro", "pell_sky");
            SetStrings(serialized.FindProperty("rotate"), "pell_forge", "pell_light");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return placement;
        }

        internal static void SetStrings(SerializedProperty list, params string[] values)
        {
            list.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        [Test]
        public void ConfigureTakesTheNameShopAndLinesFromThePlacement()
        {
            npcObject = new GameObject("Npc");
            var npc = npcObject.AddComponent<Npc>();
            // As WorldPlacement.Spawn leaves a thing that faces left: mirrored.
            npcObject.transform.localScale = new Vector3(-1f, 1f, 1f);

            npc.Configure(Placement("pell", true));

            Assert.That(npc.NpcName, Is.EqualTo("Old Pell"));
            Assert.That(npc.Shop, Is.EqualTo("pell"));
            Assert.That(npc.Trades, Is.True);
            Assert.That(npc.FacesLeft, Is.True);
            Assert.That(npc.DialogueIds, Is.EqualTo(new[] { "pell_intro", "pell_sky" }));
            Assert.That(npc.RotateIds, Is.EqualTo(new[] { "pell_forge", "pell_light" }));
            Assert.That(npc.IsAutomatic, Is.False, "an NPC waits for the interact key");
            Assert.That(npcObject.transform.localScale.x, Is.EqualTo(1f), "the prompt must not be mirrored");
        }

        [Test]
        public void TheBodyIsTheCharactersIdleFrameOrAPlainBlock()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoryPrefabs.NpcPath);
            Assert.That(prefab, Is.Not.Null, StoryPrefabs.NpcPath + " is missing");
            npcObject = Object.Instantiate(prefab);
            var npc = npcObject.GetComponent<Npc>();
            var body = npcObject.GetComponentInChildren<SpriteRenderer>();
            Assert.That(body, Is.Not.Null, "the NPC prefab has no body sprite");

            npc.Configure(Placement("pell", false));
            Assert.That(npc.Character, Is.EqualTo("pell"));
            Assert.That(body.sprite, Is.Not.Null);
            Assert.That(body.sprite.name, Is.EqualTo("pell_idle_0"));
            Assert.That(body.color, Is.EqualTo(new Color(0.5f, 0.6f, 0.7f)), "the placement's tint");
            Assert.That(body.transform.localScale, Is.EqualTo(Vector3.one));

            Object.DestroyImmediate(placementObject);
            npc.Configure(Placement("nobody_drew_this", false));
            Assert.That(npc.Character, Is.EqualTo(""));
            Assert.That(body.sprite, Is.SameAs(Shapes.Square), "a character with no art is a plain block");
            Assert.That(body.color, Is.EqualTo(Npc.PlaceholderColor));
        }

        [Test]
        public void TheNpcPrefabListsEveryCharacterWithAnIdleStrip()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Npc>(StoryPrefabs.NpcPath);
            Assert.That(prefab, Is.Not.Null, StoryPrefabs.NpcPath + " is missing");
            var frames = StoryPrefabs.IdleFrames();
            Assert.That(frames.Count, Is.GreaterThan(0), "no character under Assets/Art/Sprites has an idle strip");
            Assert.That(prefab.Bodies.Count, Is.EqualTo(frames.Count), "run Descender > Setup > Story prefabs");
            foreach (var frame in frames)
            {
                Assert.That(prefab.IdleOf(frame.character), Is.SameAs(frame.idle), frame.character);
            }
        }
    }
}
