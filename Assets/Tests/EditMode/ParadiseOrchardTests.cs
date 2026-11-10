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

        [TestCase(8, "VIII")]
        [TestCase(39, "XXXIX")]
        [TestCase(40, "XL")]
        [TestCase(44, "XLIV")]
        [TestCase(45, "XLV")]
        [TestCase(49, "XLIX")]
        public void TreeNumbersAreRomanNumerals(int n, string roman)
        {
            Assert.That(EditorTools.Paradise.ParadiseGarden.Roman(n), Is.EqualTo(roman));
        }

        [Test]
        public void EveryTreeOfTheOrchardRowsIsNumberedInTheCopingUnderIt()
        {
            var zone = EditorTools.Paradise.ParadiseLayout.All().First(z => z.number == 2);
            var plan = EditorTools.Paradise.ParadiseGarden.GardenPlan(zone);
            var expected = new System.Collections.Generic.List<float>();
            for (
                var x = EditorTools.Paradise.ParadiseGarden.OrchardRowsFrom;
                x <= EditorTools.Paradise.ParadiseGarden.OrchardRowsTo;
                x += EditorTools.Paradise.ParadiseGarden.OrchardRowSpacing
            )
            {
                expected.Add(x);
            }
            Assert.That(
                plan.numbers.Select(n => n.at.x),
                Is.EqualTo(expected),
                "one number under every tree of the rows"
            );
            for (var i = 0; i < plan.numbers.Count; i++)
            {
                var n = plan.numbers[i];
                Assert.That(
                    n.name,
                    Is.EqualTo(
                        EditorTools.Paradise.ParadiseGarden.Roman(
                            EditorTools.Paradise.ParadiseGarden.FirstTreeNumber + i
                        )
                    )
                );
                var walk = EditorTools.Paradise.ParadiseDressing.SurfaceY(zone, n.at.x);
                Assert.That(walk - n.at.y, Is.EqualTo(EditorTools.Paradise.ParadiseGarden.NumberDrop).Within(1e-4f));
            }
            var scene = EditorSceneManager.OpenScene(Orchard, OpenSceneMode.Additive);
            try
            {
                // All of them in one mesh, so they cost one draw call: a quad for each letter.
                var meshes = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                    .Where(m => m.name == "TreeNumbers")
                    .ToList();
                Assert.That(meshes, Has.Count.EqualTo(1));
                Assert.That(
                    meshes[0].sharedMesh.vertexCount,
                    Is.EqualTo(4 * plan.numbers.Sum(n => n.name.Length)),
                    "every numeral is cut"
                );
                Assert.That(
                    meshes[0].GetComponent<MeshRenderer>().sortingOrder,
                    Is.EqualTo(EditorTools.Paradise.ParadiseGarden.NumberOrder)
                );
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
