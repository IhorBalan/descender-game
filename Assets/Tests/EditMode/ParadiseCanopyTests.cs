using System.Collections.Generic;
using System.Linq;
using Descender.EditorTools.Paradise;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>The orchard's canopy of leaves: the sun through it in flecks, its roof hanging over the walk.</summary>
    public sealed class ParadiseCanopyTests
    {
        private const string Region = "Assets/Scenes/Paradise/Region_paradise_new.unity";
        private const string Orchard = "Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity";

        private static ParadiseLayout.Zone Zone(int number) => ParadiseLayout.All().First(z => z.number == number);

        [Test]
        public void TheCanopyIsMixedAcrossAZoneBorderByEachZonesShare()
        {
            Assert.That(ZoneLook.MixCanopy(new List<(float, float)> { (1f, 0f) }), Is.EqualTo(0f));
            Assert.That(ZoneLook.MixCanopy(new List<(float, float)> { (1f, 1f) }), Is.EqualTo(1f));
            Assert.That(
                ZoneLook.MixCanopy(new List<(float, float)> { (0.25f, 0f), (0.75f, 1f) }),
                Is.EqualTo(0.75f).Within(1e-5f)
            );
            Assert.That(ZoneLook.MixCanopy(new List<(float, float)>()), Is.EqualTo(0f), "no zone, no canopy");
        }

        [Test]
        public void WhereTheCanopyEndsItsRoofDrawsBackUpOutOfTheView()
        {
            Assert.That(ZoneLook.CanopyLift(1f), Is.EqualTo(0f), "closed: where it hangs");
            Assert.That(ZoneLook.CanopyLift(0f), Is.EqualTo(ZoneLook.CanopyLiftUnits), "open: gone up out of the view");
            // The roof is 5 units deep from the view's top: lifted all the way, none of it shows.
            Assert.That(ZoneLook.CanopyLiftUnits, Is.GreaterThanOrEqualTo(5f));
            var last = 0f;
            for (var roof = 1f; roof >= 0f; roof -= 0.05f)
            {
                var lift = ZoneLook.CanopyLift(roof);
                Assert.That(lift, Is.GreaterThanOrEqualTo(last - 1e-5f), "it only rises as the canopy opens");
                last = lift;
            }
        }

        [TestCase(0f)]
        [TestCase(0.4f)]
        [TestCase(1f)]
        public void UnderTheCanopyTheSunComesThroughInFlecksAndNoLightIsLostOrGained(float canopy)
        {
            var split = SunAndClouds.SunThroughCanopy(0.6f, canopy);
            Assert.That(split.x + split.y, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(split.y, Is.EqualTo(0.6f * canopy).Within(1e-5f));
        }

        [Test]
        public void TheFlecksPatternTilesAndLightsAboutATenthOfTheGround()
        {
            var lit = 0;
            var samples = 0;
            for (var i = 0; i < 64; i++)
            {
                for (var j = 0; j < 32; j++)
                {
                    var u = i / 64f;
                    var v = j / 32f;
                    var f = SunAndClouds.Fleck(u, v, 0.3f);
                    Assert.That(f, Is.InRange(0f, 1f));
                    Assert.That(
                        SunAndClouds.Fleck(u + 1f, v, 0.3f),
                        Is.EqualTo(f).Within(1e-3f),
                        $"tiles across at {u}"
                    );
                    Assert.That(SunAndClouds.Fleck(u, v + 1f, 0.3f), Is.EqualTo(f).Within(1e-3f), $"tiles up at {v}");
                    lit += f > 0.5f ? 1 : 0;
                    samples++;
                }
            }
            var share = lit / (float)samples;
            Assert.That(share, Is.InRange(0.04f, 0.25f), "flecks, not a sunlit floor or a dark one");
        }

        [Test]
        public void OnlyTheOrchardHasACanopyWhereTheLayoutPutsItsRoof()
        {
            Assert.That(ParadiseGarden.CanopyOf(1, out _, out _), Is.False);
            Assert.That(ParadiseGarden.CanopyOf(2, out var from, out var to), Is.True);
            var zone = Zone(2);
            var roof = zone.shapes.First(s => s.name == ParadiseGarden.CanopyRoof).outline;
            Assert.That(from - ParadiseGarden.CanopyOpens, Is.GreaterThanOrEqualTo(zone.xMin));
            Assert.That(from - ParadiseGarden.CanopyOpens, Is.GreaterThanOrEqualTo(roof.Min(p => p.x) - 0.01f));
            Assert.That(to + ParadiseGarden.CanopyOpens, Is.LessThanOrEqualTo(roof.Max(p => p.x) + 0.01f));
        }

        [Test]
        public void UnderTheCanopyTheFramingsHangsGiveWayAndTheSunReachesInMoreOften()
        {
            var plan = ParadiseGarden.GardenPlan(Zone(2));
            ParadiseGarden.CanopyOf(2, out var from, out var to);
            // Its own near leaves hang over the walk instead; the framing's rising pieces remain.
            Assert.That(
                plan.foreground.Where(f =>
                    f.name.Contains("hang")
                    && f.at.x > from - ParadiseGarden.CanopyOpens
                    && f.at.x < to + ParadiseGarden.CanopyOpens
                ),
                Is.Empty
            );
            Assert.That(plan.foreground.Any(f => f.name.Contains("rise") && f.at.x > from && f.at.x < to));
            // The sun reaches in through its gaps more often than across the open sky.
            var under = plan.shafts.Where(b => b > from && b < to).OrderBy(b => b).ToList();
            Assert.That(under.Count, Is.GreaterThan(10));
            for (var i = 1; i < under.Count; i++)
            {
                Assert.That(
                    under[i] - under[i - 1],
                    Is.LessThanOrEqualTo(
                        ParadiseGarden.CanopyBeamSpacing + ParadiseGarden.CanopyBeamSpacingVaries + 0.01f
                    )
                );
            }
        }

        [Test]
        public void TheSunHasItsFlecksAndTheOrchardItsCanopyInTheBuiltScenes()
        {
            var region = EditorSceneManager.OpenScene(Region, OpenSceneMode.Additive);
            try
            {
                var sun = region
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SunAndClouds>(true))
                    .Single();
                // A 2D light (the test assembly does not reference URP's 2D package, so by its fields).
                var flecks = new SerializedObject(sun).FindProperty("flecks").objectReferenceValue;
                Assert.That(flecks, Is.Not.Null);
                Assert.That(flecks.GetType().Name, Is.EqualTo("Light2D"));
                var sunLight = new SerializedObject(sun).FindProperty("sun").objectReferenceValue;
                Assert.That(
                    new SerializedObject(flecks).FindProperty("m_LightType").intValue,
                    Is.EqualTo(new SerializedObject(sunLight).FindProperty("m_LightType").intValue),
                    "a sprite light, as the sun is"
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(region, true);
            }
            var orchard = EditorSceneManager.OpenScene(Orchard, OpenSceneMode.Additive);
            try
            {
                var look = orchard
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<ZoneLook>(true))
                    .Single();
                ParadiseGarden.CanopyOf(2, out var from, out var to);
                Assert.That(look.CanopyAt((from + to) / 2f), Is.EqualTo(ParadiseGarden.CanopyCover).Within(1e-4f));
                Assert.That(look.CanopyAt(1290f), Is.EqualTo(0f), "the exit climb is under open sky");
                // The roof of leaves: two bands pinned to the view's top (scroll.y 0, so their edge
                // never shows), the near one on the Foreground layer, and faded with the canopy.
                var layers = new SerializedObject(look).FindProperty("canopyLayers");
                Assert.That(layers.arraySize, Is.EqualTo(2));
                var names = new List<string>();
                for (var i = 0; i < layers.arraySize; i++)
                {
                    var layer = (Transform)layers.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(layer, Is.Not.Null);
                    names.Add(layer.name);
                    var backdrop = new SerializedObject(layer.GetComponent<World.BackdropLayer>());
                    Assert.That(backdrop.FindProperty("scroll").vector2Value.y, Is.EqualTo(0f), $"{layer.name} pinned");
                    Assert.That(
                        backdrop.FindProperty("viewOffset").vector2Value.y,
                        Is.GreaterThan(0f),
                        $"{layer.name} at the top"
                    );
                }
                Assert.That(names, Is.EquivalentTo(new[] { "CanopyBack", "CanopyFront" }));
                var back = (Transform)layers.GetArrayElementAtIndex(names.IndexOf("CanopyBack")).objectReferenceValue;
                var front = (Transform)layers.GetArrayElementAtIndex(names.IndexOf("CanopyFront")).objectReferenceValue;
                Assert.That(
                    back.GetComponent<SpriteRenderer>().sortingOrder,
                    Is.EqualTo(ParadiseGarden.CanopyBackOrder)
                );
                Assert.That(
                    front.GetComponent<SpriteRenderer>().sortingLayerName,
                    Is.EqualTo(ParadiseGarden.ForegroundLayer)
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(orchard, true);
            }
        }
    }
}
