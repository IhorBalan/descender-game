using System.IO;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    public sealed class ParadiseLightingTests
    {
        private const string NormalsFolder = "Assets/Art/Paradise/Normals";

        [Test]
        public void TheCloudPatternAlwaysReachesPastBothSidesOfTheView()
        {
            const float period = 56f;
            for (var view = -400f; view < 2000f; view += 3.7f)
            {
                foreach (var drifted in new[] { 0f, 13.3f, 55.9f })
                {
                    var centre = SunAndClouds.Centre(view, drifted, period);
                    // The picture spans a period each way from its centre; a view is about 10 units half-wide.
                    Assert.That(Mathf.Abs(view - centre), Is.LessThanOrEqualTo(period * 0.5f + 0.001f));
                }
            }
        }

        [Test]
        public void TheCloudPatternOnlyEverJumpsByWholePeriods()
        {
            const float period = 56f;
            const float drifted = 13.3f;
            var first = SunAndClouds.Centre(10f, drifted, period);
            for (var view = -400f; view < 2000f; view += 3.7f)
            {
                var periods = (SunAndClouds.Centre(view, drifted, period) - first) / period;
                Assert.That(periods, Is.EqualTo(Mathf.Round(periods)).Within(0.0001f));
            }
        }

        [Test]
        public void EveryNormalMapIsOnItsSprite()
        {
            var maps = Directory.GetFiles(NormalsFolder, "*_n.png");
            Assert.That(maps, Is.Not.Empty);
            foreach (var map in maps)
            {
                var name = Path.GetFileNameWithoutExtension(map);
                name = name.Substring(0, name.Length - 2);
                var found = false;
                foreach (
                    var guid in AssetDatabase.FindAssets(
                        $"{name} t:Sprite",
                        new[] { "Assets/Art/Paradise/Kit", "Assets/Art/Paradise/Derived" }
                    )
                )
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                    if (sprite == null || sprite.texture.name != name)
                    {
                        continue;
                    }
                    found = true;
                    var secondary = new SecondarySpriteTexture[sprite.GetSecondaryTextureCount()];
                    sprite.GetSecondaryTextures(secondary);
                    Assert.That(secondary.Length, Is.EqualTo(1), name);
                    Assert.That(secondary[0].name, Is.EqualTo("_NormalMap"), name);
                }
                Assert.That(found, Is.True, $"no sprite for {map}");
            }
        }
    }
}
