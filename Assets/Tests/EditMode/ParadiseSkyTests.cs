using System.IO;
using System.Linq;
using Descender.EditorTools.Paradise;
using Descender.World;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Descender.Tests
{
    public sealed class ParadiseSkyTests
    {
        private const string Clouds = "Assets/Art/Paradise/Backdrop_Garden/garden_bg0_clouds.png";

        private static Transform Named(Scene scene, string name) =>
            scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == name);

        [Test]
        public void TheTwoScreensOfCloudsMeetInClearSkySoNoCloudIsCut()
        {
            var image = new Texture2D(2, 2);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(Clouds)), Is.True);
                var quarter = image.width / 4;
                foreach (var column in new[] { quarter - 1, quarter, quarter * 3 - 1, quarter * 3 })
                {
                    for (var y = 0; y < image.height; y += 2)
                    {
                        Assert.That(image.GetPixel(column, y).a, Is.Zero.Within(0.01f), $"column {column}, row {y}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        [TestCase("Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity")]
        [TestCase("Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity")]
        public void EachZonesCloudsMovePastASkyThatStaysPut(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var sky = Named(scene, "Sky");
                var clouds = Named(scene, "HighClouds");
                var far = Named(scene, "Far");
                Assert.That(sky.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("garden_bg0_sky_clear"));
                Assert.That(sky.GetComponent<BackdropLayer>().Cover, Is.True);

                var layer = new SerializedObject(clouds.GetComponent<BackdropLayer>());
                Assert.That(layer.FindProperty("scroll").vector2Value.x, Is.GreaterThan(0f));
                Assert.That(layer.FindProperty("drift").vector2Value.x, Is.GreaterThan(0f));

                var order = clouds.GetComponent<SpriteRenderer>().sortingOrder;
                Assert.That(order, Is.GreaterThan(sky.GetComponent<SpriteRenderer>().sortingOrder));
                Assert.That(order, Is.LessThan(far.GetComponent<SpriteRenderer>().sortingOrder));

                var look = new SerializedObject(sky.root.GetComponentInChildren<ZoneLook>(true));
                var layers = look.FindProperty("layers");
                var faded = Enumerable
                    .Range(0, layers.arraySize)
                    .Select(i => layers.GetArrayElementAtIndex(i).objectReferenceValue);
                Assert.That(faded, Does.Contain(clouds), "the clouds fade with their zone");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity")]
        [TestCase("Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity")]
        public void SunbeamsLeanWithTheSunAndTheOnesInFrontStayFaint(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var beams = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                    .Where(r => r.name.StartsWith("Beam"))
                    .ToList();
                Assert.That(beams.Count, Is.GreaterThan(4));
                // Down the way the light runs: the foot of a beam lies toward the side away from the sun.
                var lean = SunAndClouds.SunDirection.x > 0f ? -1f : 1f;
                foreach (var beam in beams)
                {
                    var down = beam.transform.rotation * Vector3.down;
                    Assert.That(Mathf.Sign(down.x), Is.EqualTo(lean), beam.name);
                    Assert.That(Vector2.Angle(down, -(Vector2)SunAndClouds.SunDirection), Is.LessThan(1f), beam.name);
                }
                var inFront = beams.Where(b => b.name == "Beam Front").ToList();
                Assert.That(inFront, Is.Not.Empty);
                foreach (var beam in inFront)
                {
                    Assert.That(beam.sortingLayerName, Is.EqualTo(ParadiseGarden.ForegroundLayer));
                    Assert.That(
                        beam.sharedMaterial.GetFloat("_Intensity"),
                        Is.LessThan(beams.First(b => b.name == "Beam").sharedMaterial.GetFloat("_Intensity"))
                    );
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
