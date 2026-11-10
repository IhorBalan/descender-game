using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    public sealed class ParadiseOrchardTests
    {
        private const string Orchard = "Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity";

        [Test]
        public void EveryOrchardTreesFruitGivesOffLight()
        {
            var scene = EditorSceneManager.OpenScene(Orchard, OpenSceneMode.Additive);
            try
            {
                var trees = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                    .Where(r => r.name == "blossom_tree")
                    .ToList();
                Assert.That(trees, Is.Not.Empty);
                foreach (var tree in trees)
                {
                    var glow = tree.transform.Find("Glow")?.GetComponent<SpriteRenderer>();
                    Assert.That(glow, Is.Not.Null, $"tree at {tree.transform.position.x}");
                    Assert.That(glow.sprite.name, Does.StartWith("blossom_tree_glow"));
                    Assert.That(glow.flipX, Is.EqualTo(tree.flipX), "the glow sits on the fruit of a turned tree too");
                    Assert.That(glow.sortingOrder, Is.GreaterThan(tree.sortingOrder));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
