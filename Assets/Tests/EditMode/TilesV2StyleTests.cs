using System.IO;
using Descender.EditorTools.TilesV2;
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

        // The ground: every frame and slope on a material's sheet, and every overlay on a
        // blend's sheet, reads the sheet's normal map.
        [TestCase("stone", 47 * 3)]
        [TestCase("grass", 47 * 3)]
        [TestCase("marble_wall", 47 * 3)]
        [TestCase("blend_dirt_on_stone", 15 * 3)]
        public void AMaterialSheetCarriesItsNormalMap(string key, int least)
        {
            var sprites = 0;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Folder + key + ".png"))
            {
                if (asset is not Sprite sprite)
                {
                    continue;
                }
                sprites++;
                var secondary = new SecondarySpriteTexture[sprite.GetSecondaryTextureCount()];
                sprite.GetSecondaryTextures(secondary);
                Assert.That(secondary.Length, Is.EqualTo(1), sprite.name);
                Assert.That(secondary[0].name, Is.EqualTo("_NormalMap"));
                Assert.That(AssetDatabase.GetAssetPath(secondary[0].texture), Is.EqualTo(Folder + key + "_n.png"));
            }
            Assert.That(sprites, Is.GreaterThanOrEqualTo(least), "every frame of every variant");
        }

        // A shaft of light is a light, not a thing: it has no relief and stays as it is drawn.
        [Test]
        public void APictureOfTheFirstStyleHasNoNormalMap()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "earth_rays.png");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.GetSecondaryTextureCount(), Is.EqualTo(0));
        }

        // What grows on the floors bends in its own shader: the set draws it with the brush
        // material, sized from its pictures, and each picture has room on either side for the
        // most the shader can lean it, so a bent blade is never cut off.
        [Test]
        public void TheBrushBendsInItsShaderWithRoomOnEitherSide()
        {
            var material = TilesV2Builder.LoadSet().brushMaterial;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Descender/Tiles V2/Brush Lit"));
            Assert.That(material.shader.isSupported, Is.True);
            var reach =
                material.GetFloat("_Wind")
                + material.GetFloat("_Gust")
                + material.GetFloat("_Flutter")
                + material.GetFloat("_PushReach");
            var room = Mathf.CeilToInt(reach);
            for (var kind = 0; kind < 6; kind++)
            {
                var path = Folder + "eden_brush_" + kind + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(material.GetVector("_Size").x, Is.EqualTo(sprite.rect.width), path);
                Assert.That(material.GetVector("_Size").y, Is.EqualTo(sprite.rect.height), path);
                Assert.That(material.GetFloat("_Foot"), Is.EqualTo(sprite.pivot.y / sprite.rect.height).Within(1e-4f));
                var picture = new Texture2D(2, 2);
                try
                {
                    Assert.That(picture.LoadImage(File.ReadAllBytes(path)), Is.True);
                    for (var y = 0; y < picture.height; y++)
                    {
                        for (var x = 0; x < room; x++)
                        {
                            Assert.That(
                                picture.GetPixel(x, y).a,
                                Is.EqualTo(0f),
                                $"{path}: room on the left at {x}, {y}"
                            );
                            Assert.That(
                                picture.GetPixel(picture.width - 1 - x, y).a,
                                Is.EqualTo(0f),
                                $"{path}: room on the right at {x}, {y}"
                            );
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(picture);
                }
            }
        }

        // A tree's crown moves in its own shader, smoothly: the set draws it with the crown
        // material, whose quad grows by more than the most the shader moves the picture, so a
        // leaning crown is never cut off; and a texel is as many world units as the pictures'.
        [Test]
        public void ACrownMovesInItsShaderWithRoomToLean()
        {
            var material = TilesV2Builder.LoadSet().crownMaterial;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Descender/Tiles V2/Crown Lit"));
            Assert.That(material.shader.isSupported, Is.True);
            var reach = material.GetFloat("_Sway") + material.GetFloat("_Gust") + material.GetFloat("_Rustle");
            Assert.That(reach, Is.GreaterThan(1f), "the crown hardly moves");
            Assert.That(material.GetFloat("_Room"), Is.GreaterThan(reach + 1f));
            Assert.That(material.GetFloat("_Snap"), Is.EqualTo(0f), "it moves smoothly, not a texel at a time");
            foreach (
                var key in new[]
                {
                    "heaven_blossom_0",
                    "heaven_gold_0",
                    "eden_teal_tree_0",
                    "earth_oak_0",
                    "earth_giant_oak",
                    "earth_apple_1",
                }
            )
            {
                Assert.That(TilesV2Things.Crowned(key), Is.True, key);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + key + ".png");
                Assert.That(sprite, Is.Not.Null, key);
                Assert.That(material.GetFloat("_TexelUnits"), Is.EqualTo(1f / sprite.pixelsPerUnit).Within(1e-6f));
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + key + ".png");
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), key);
            }
            Assert.That(TilesV2Things.Crowned("heaven_banner"), Is.False);
            Assert.That(TilesV2Things.Crowned("earth_pine_0"), Is.False, "a pine is stiff");
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
