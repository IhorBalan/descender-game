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

        [Test]
        public void FirefliesWanderTheOrchardGlowingUpAndDimming()
        {
            var scene = EditorSceneManager.OpenScene(Orchard, OpenSceneMode.Additive);
            try
            {
                var fireflies = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<World.RegionParticles>(true))
                    .Where(p => p.name == "Fireflies")
                    .ToList();
                Assert.That(fireflies, Has.Count.EqualTo(1));
                var settings = new UnityEditor.SerializedObject(fireflies[0]);
                Assert.That(settings.FindProperty("blinks").intValue, Is.EqualTo(2));
                Assert.That(settings.FindProperty("sprite").objectReferenceValue.name, Is.EqualTo("firefly"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ABlinkFadesInAndOutAndGlowsUpAsOftenAsAsked(int times)
        {
            var gradient = World.RegionParticles.BlinkGradient(times);
            Assert.That(gradient.Evaluate(0f).a, Is.EqualTo(0f).Within(1e-4f), "never pops on");
            Assert.That(gradient.Evaluate(1f).a, Is.EqualTo(0f).Within(1e-4f), "never pops off");
            // Count the peaks: where it rises to full and falls again.
            var peaks = 0;
            var rising = false;
            var last = 0f;
            for (var t = 0f; t <= 1f; t += 0.002f)
            {
                var a = gradient.Evaluate(t).a;
                if (a > last + 1e-5f)
                {
                    rising = true;
                }
                else if (a < last - 1e-5f && rising)
                {
                    peaks++;
                    rising = false;
                    Assert.That(last, Is.GreaterThan(0.95f), $"a full glow before the dim at {t:0.00}");
                }
                last = a;
            }
            Assert.That(peaks, Is.EqualTo(times));
        }
    }
}
