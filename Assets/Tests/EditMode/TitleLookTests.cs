using Descender.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The numbers behind the title screen's pixel-perfect look: the whole-number
    /// canvas scale, the focus bar's glide, the motes' fade and the letter spacing,
    /// and the theme's title art imported as point-filtered, uncompressed sprites.
    /// </summary>
    public class TitleLookTests
    {
        private const string ThemePath = "Assets/Settings/MenuTheme.asset";

        [TestCase(320, 180, 1)]
        [TestCase(1920, 1080, 6)]
        [TestCase(2560, 1440, 8)]
        [TestCase(1366, 768, 4)]
        [TestCase(1920, 1200, 6)]
        [TestCase(3440, 1440, 8)]
        [TestCase(200, 100, 1)]
        public void TheScaleIsTheLargestWholeOneThatFits(int width, int height, int scale)
        {
            Assert.That(PixelCanvasScale.ScaleFor(width, height), Is.EqualTo(scale));
        }

        [Test]
        public void TheBarClosesOnItsTargetAndLandsOnIt()
        {
            var y = 84f;
            var steps = 0;
            while (y != 126f && steps < 200)
            {
                var next = TitleFocus.Ease(y, 126f, 1f / 60f);
                Assert.That(next, Is.GreaterThan(y), "every frame moves closer");
                Assert.That(next, Is.LessThanOrEqualTo(126f), "and never overshoots");
                y = next;
                steps++;
            }
            Assert.That(y, Is.EqualTo(126f), "the bar lands exactly on the target");
            Assert.That(steps, Is.InRange(10, 40), "in about a third of a second at 60 frames");
            Assert.That(TitleFocus.Ease(126f, 126f, 1f / 60f), Is.EqualTo(126f), "and stays there");
            Assert.That(TitleFocus.Ease(126f, 84f, 1f / 60f), Is.LessThan(126f), "it glides up as well as down");
        }

        [Test]
        public void MotesFadeInAtTheBottomAndOutTowardsTheTop()
        {
            Assert.That(TitleEmbers.FadeAt(UiTheme.Height), Is.EqualTo(0f));
            Assert.That(TitleEmbers.FadeAt(100f), Is.EqualTo(1f));
            Assert.That(TitleEmbers.FadeAt(10f), Is.EqualTo(0f));
            Assert.That(TitleEmbers.FadeAt(40f), Is.InRange(0.1f, 0.9f));
        }

        [Test]
        public void ThePixelFontHasEveryLetterAndSpacesInWholePixels()
        {
            var font = ReadFont();
            Assert.That(font, Is.Not.Null, "the pixel font or its metrics are missing from the menu theme");
            Assert.That(font.Line, Is.GreaterThanOrEqualTo(TitleKit.FontSize));
            foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:?-")
            {
                font.TryGlyph(c, out var cell, out _, out var advance);
                Assert.That(cell.width, Is.GreaterThan(0), $"'{c}' has no ink");
                Assert.That(advance, Is.GreaterThan(0f), $"'{c}' does not move the pen");
                Assert.That(cell.xMax, Is.LessThanOrEqualTo(font.Sheet.width), $"'{c}' runs off the sheet");
                Assert.That(cell.yMax, Is.LessThanOrEqualTo(font.Sheet.height), $"'{c}' runs off the sheet");
            }
            font.Measure("NEW GAME", 0f, out var left, out var right);
            font.Measure("NEW GAME", TitleKit.CapsSpacing, out var spacedLeft, out var spacedRight);
            Assert.That(spacedLeft, Is.EqualTo(left), "spacing adds nothing before the first letter");
            Assert.That(
                spacedRight - right,
                Is.EqualTo(TitleKit.CapsSpacing * 7),
                "two pixels between each of 8 letters"
            );
            font.Measure("", TitleKit.CapsSpacing, out left, out right);
            Assert.That(right - left, Is.EqualTo(0));
            font.TryGlyph('é', out var missing, out _, out _);
            font.TryGlyph('?', out var question, out _, out _);
            Assert.That(missing, Is.EqualTo(question), "a character outside ASCII shows as a question mark");
        }

        private static PixelFont ReadFont()
        {
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
            return PixelFont.Read(theme.pixelFont, theme.pixelFontMetrics != null ? theme.pixelFontMetrics.text : null);
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
        public void TheThemeHasEveryPieceOfTitleArtAsAPixelSprite()
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
                theme.ruleHints,
            };
            foreach (var sprite in sprites)
            {
                Assert.That(sprite, Is.Not.Null, "a title sprite is missing from the menu theme");
                var path = AssetDatabase.GetAssetPath(sprite);
                StringAssert.StartsWith("Assets/Art/UI/Title/", path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
            }
            Assert.That(theme.focusBarWide.rect.width, Is.GreaterThan(theme.focusBar.rect.width));
            var sheet = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(theme.pixelFont));
            Assert.That(sheet.filterMode, Is.EqualTo(FilterMode.Point), "the font sheet is sampled as hard pixels");
            Assert.That(sheet.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }
    }
}
