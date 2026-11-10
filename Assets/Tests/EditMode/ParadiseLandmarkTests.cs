using System.Linq;
using Descender.EditorTools.Paradise;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Descender.Tests
{
    public sealed class ParadiseLandmarkTests
    {
        private const string Region = "Assets/Scenes/Paradise/Region_paradise_new.unity";

        private static SpriteRenderer Named(Scene scene, string name) =>
            scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == name)
                .GetComponentInChildren<SpriteRenderer>(true);

        [TestCase("Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity")]
        [TestCase("Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity")]
        public void TheGoldenTreeStandsOverTheTempleIslandsAndBehindTheNearClouds(string zonePath)
        {
            var region = EditorSceneManager.OpenScene(Region, OpenSceneMode.Additive);
            var zone = EditorSceneManager.OpenScene(zonePath, OpenSceneMode.Additive);
            try
            {
                var tree = Named(region, "GoldenTree");
                var glow = Named(region, "Glow");
                Assert.That(tree.sortingOrder, Is.EqualTo(ParadiseGarden.GoldenTreeOrder));
                Assert.That(tree.sortingOrder, Is.GreaterThan(Named(zone, "MidFar").sortingOrder));
                Assert.That(tree.sortingOrder, Is.GreaterThan(Named(zone, "Mid").sortingOrder));
                Assert.That(glow.sortingOrder, Is.GreaterThan(tree.sortingOrder));
                Assert.That(glow.sortingOrder, Is.LessThan(Named(zone, "CloudsFront").sortingOrder));
            }
            finally
            {
                EditorSceneManager.CloseScene(zone, true);
                EditorSceneManager.CloseScene(region, true);
            }
        }
    }
}
