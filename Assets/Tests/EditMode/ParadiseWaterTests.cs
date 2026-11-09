using System.Linq;
using Descender.EditorTools.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    public sealed class ParadiseWaterTests
    {
        private const string RendererPath = "Assets/Settings/Descender 2D Renderer.asset";
        private const string WaterfallMaterial = "Assets/Settings/Materials/Paradise/Paradise Waterfall.mat";

        /// <summary>Zone 1's fall at x 126 lands beside a tall wall whose column stands out to about 124.3.</summary>
        private const float FallBesideAWall = 126f;
        private const float ItsFoot = -66f;
        private const float ColumnFace = 124.3f;

        private static ParadiseLayout.Zone Zone(int number) => ParadiseLayout.All().First(z => z.number == number);

        [Test]
        public void APoolStopsShortOfAWallAndSpreadsFreelyOnOpenGround()
        {
            const float limit = 5f;
            var reach = ParadiseGarden.PoolReach(Zone(1), FallBesideAWall, ItsFoot, limit);
            Assert.That(reach.x, Is.GreaterThan(0.5f));
            Assert.That(FallBesideAWall - reach.x, Is.GreaterThanOrEqualTo(ColumnFace));
            Assert.That(reach.y, Is.EqualTo(limit));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void EveryPoolEndsOnTheGroundItStartedOn(int number)
        {
            var zone = Zone(number);
            var pools = ParadiseGarden.GardenPlan(zone).water.Where(w => w.foam).ToList();
            Assert.That(pools, Is.Not.Empty);
            foreach (var w in pools)
            {
                var reach = ParadiseGarden.PoolReach(zone, w.x, w.bottom, 6f);
                var ground = ParadiseDressing.SurfaceY(zone, w.x, float.NaN, w.bottom + 1f);
                foreach (var end in new[] { w.x - reach.x, w.x + reach.y })
                {
                    var there = ParadiseDressing.SurfaceY(zone, end, float.NaN, w.bottom + 1f);
                    Assert.That(float.IsNaN(there), Is.False, $"fall at {w.x}, end {end}");
                    Assert.That(there, Is.EqualTo(ground).Within(0.4f), $"fall at {w.x}, end {end}");
                }
            }
        }

        [Test]
        public void ThePoolsDrawOverTheCapturedWorldAndUnderTheForeground()
        {
            var world = SortingLayer.GetLayerValueFromName("Default");
            var water = SortingLayer.GetLayerValueFromName(ParadiseGarden.WaterLayer);
            var front = SortingLayer.GetLayerValueFromName(ParadiseGarden.ForegroundLayer);
            Assert.That(SortingLayer.NameToID(ParadiseGarden.WaterLayer), Is.Not.Zero);
            Assert.That(SortingLayer.NameToID(ParadiseGarden.ForegroundLayer), Is.Not.Zero);
            Assert.That(world, Is.LessThan(water));
            Assert.That(water, Is.LessThan(front));
        }

        [Test]
        public void TheRendererCapturesTheWorldForThePoolsToMirror()
        {
            var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(RendererPath));
            Assert.That(data.FindProperty("m_UseCameraSortingLayersTexture").boolValue, Is.True);
            Assert.That(
                data.FindProperty("m_CameraSortingLayersTextureBound").intValue,
                Is.EqualTo(SortingLayer.NameToID("Default"))
            );
        }

        [Test]
        public void AFallStaysSolidBelowItsLip()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterfallMaterial);
            Assert.That(material.GetFloat("_SolidLength"), Is.GreaterThan(1f));
        }

        [TestCase("Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity")]
        [TestCase("Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity")]
        public void TheBuiltZonesPutPoolsAndFramingOnTheirLayers(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var all = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                    .ToList();
                var pools = all.Where(r => r.name == "Pool").ToList();
                Assert.That(pools, Is.Not.Empty);
                foreach (var pool in pools)
                {
                    Assert.That(pool.sortingLayerName, Is.EqualTo(ParadiseGarden.WaterLayer));
                    Assert.That(pool.sharedMaterial.shader.name, Is.EqualTo("Descender/Water Reflection"));
                }
                var front = all.Where(r => r.name.StartsWith("fg_soft_")).ToList();
                Assert.That(front, Is.Not.Empty);
                foreach (var piece in front)
                {
                    Assert.That(piece.sortingLayerName, Is.EqualTo(ParadiseGarden.ForegroundLayer), piece.name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
