using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.Tests
{
    /// <summary>
    /// The second style of Tiles V2 (docs/art/tiles-v2.md): a picture drawn in it carries its
    /// normal map as the sprite's <c>_NormalMap</c>, imported as a normal map, the same size
    /// as the picture; and the lamps of Tiles V2 read normal maps, so they light its relief.
    /// </summary>
    public class TilesV2StyleTests
    {
        private const string Folder = "Assets/Art/TilesV2/";

        // A few pictures of the second style: trees of the Earth's, a stirring one of
        // Paradise's (a frame of it too), one of Eden's, and a topiary.
        private static readonly string[] Styled =
        {
            "earth_oak_0",
            "earth_apple_1",
            "heaven_blossom_0",
            "heaven_gold_2_f2",
            "eden_teal_tree_1",
            "earth_pine_0",
            "earth_giant_oak",
            "heaven_hedge_2",
        };

        [TestCaseSource(nameof(Styled))]
        public void APictureOfTheSecondStyleCarriesItsNormalMap(string key)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + key + ".png");
            Assert.That(sprite, Is.Not.Null, key);
            var secondary = new SecondarySpriteTexture[sprite.GetSecondaryTextureCount()];
            sprite.GetSecondaryTextures(secondary);
            Assert.That(secondary.Length, Is.EqualTo(1), key);
            Assert.That(secondary[0].name, Is.EqualTo("_NormalMap"));
            var normal = secondary[0].texture;
            Assert.That(normal, Is.Not.Null, key);
            Assert.That(AssetDatabase.GetAssetPath(normal), Is.EqualTo(Folder + key + "_n.png"));
            Assert.That(normal.width, Is.EqualTo(sprite.texture.width));
            Assert.That(normal.height, Is.EqualTo(sprite.texture.height));
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + key + "_n.png");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        }

        // A shaft of light is a light, not a thing: it has no relief and stays as it is drawn.
        [Test]
        public void APictureOfTheFirstStyleHasNoNormalMap()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "earth_rays.png");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.GetSecondaryTextureCount(), Is.EqualTo(0));
        }

        [Test]
        public void ALampReadsTheReliefOfWhatItLights()
        {
            Assert.That(ReliefLight.Supported, Is.True, "URP's Light2D has no normal map fields to set");
            var holder = new GameObject("Lamp");
            try
            {
                var light = holder.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                Assert.That(light.normalMapQuality, Is.EqualTo(Light2D.NormalMapQuality.Disabled));
                ReliefLight.Raise(light);
                Assert.That(light.normalMapQuality, Is.EqualTo(Light2D.NormalMapQuality.Accurate));
                Assert.That(light.normalMapDistance, Is.EqualTo(ReliefLight.Distance));
                ReliefLight.Raise(light, 2f);
                Assert.That(light.normalMapDistance, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
