using Descender.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The numbers and assets behind the title screen's look: the focus bar's glide,
    /// the embers' fade, the intro's ease, the title art imported as smooth,
    /// uncompressed sprites, the key cap nine-sliced, and Cinzel as a static
    /// distance-field font.
    /// </summary>
    public class TitleLookTests
    {
        private const string ThemePath = "Assets/Settings/MenuTheme.asset";

        [Test]
        public void TheBarClosesOnItsTargetAndLandsOnIt()
        {
            var y = 470f;
            var target = 470f + 3 * TitleKit.Step;
            var steps = 0;
            while (y != target && steps < 200)
            {
                var next = TitleFocus.Ease(y, target, 1f / 60f);
                Assert.That(next, Is.GreaterThan(y), "every frame moves closer");
                Assert.That(next, Is.LessThanOrEqualTo(target), "and never overshoots");
                y = next;
                steps++;
            }
            Assert.That(y, Is.EqualTo(target), "the bar lands exactly on the target");
            Assert.That(steps, Is.InRange(15, 45), "in about half a second at 60 frames");
            Assert.That(TitleFocus.Ease(target, target, 1f / 60f), Is.EqualTo(target), "and stays there");
            Assert.That(TitleFocus.Ease(target, 470f, 1f / 60f), Is.LessThan(target), "it glides up as well as down");
        }

        [Test]
        public void EmbersFadeInAtTheBottomAndOutTowardsTheTop()
        {
            Assert.That(TitleEmbers.FadeAt(TitleKit.Height), Is.EqualTo(0f));
            Assert.That(TitleEmbers.FadeAt(700f), Is.EqualTo(1f));
            Assert.That(TitleEmbers.FadeAt(60f), Is.EqualTo(0f));
            Assert.That(TitleEmbers.FadeAt(200f), Is.InRange(0.1f, 0.9f));
        }

        [Test]
        public void TheIntroEasesFromDarkToFull()
        {
            Assert.That(TitleScreen.Smooth(-1f), Is.EqualTo(0f));
            Assert.That(TitleScreen.Smooth(0f), Is.EqualTo(0f));
            Assert.That(TitleScreen.Smooth(0.5f), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(TitleScreen.Smooth(1f), Is.EqualTo(1f));
            Assert.That(TitleScreen.Smooth(3f), Is.EqualTo(1f));
        }

        [Test]
        public void TheThemeHasEveryPieceOfTitleArtAsASmoothSprite()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
            Assert.That(theme, Is.Not.Null);
            var sprites = new[]
            {
                theme.titleLogo,
                theme.titleHalo,
                theme.focusBar,
                theme.focusBarWide,
                theme.focusEmblem,
                theme.ruleItem,
                theme.ruleTagline,
                theme.ruleSide,
                theme.mote,
                theme.keyCap,
                theme.keyDpad,
            };
            foreach (var sprite in sprites)
            {
                Assert.That(sprite, Is.Not.Null, "a title sprite is missing from the menu theme");
                var path = AssetDatabase.GetAssetPath(sprite);
                StringAssert.StartsWith("Assets/Art/UI/Title/", path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
            }
            Assert.That(theme.focusBarWide.rect.width, Is.GreaterThan(theme.focusBar.rect.width));
            Assert.That(theme.keyCap.border, Is.EqualTo(new Vector4(14f, 14f, 14f, 14f)), "the key cap stretches");
        }

        [Test]
        public void TheTitleFontIsCinzelAsAStaticDistanceField()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
            var font = theme.titleFont;
            Assert.That(font, Is.Not.Null, "run Descender > Setup > Title font");
            Assert.That(
                font.atlasPopulationMode,
                Is.EqualTo(AtlasPopulationMode.Static),
                "the atlas never changes in play"
            );
            Assert.That(font.material, Is.Not.Null);
            Assert.That(font.atlasTextures, Is.Not.Empty);
            foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:?.-")
            {
                Assert.That(font.HasCharacter(c), Is.True, $"'{c}' is not in the title font");
            }
            foreach (var line in TitleScreen.TaglineLines)
            {
                StringAssert.Contains(line, TitleScreen.Tagline.ToUpperInvariant(), "the two lines are the tagline");
            }
        }
    }
}
