using Descender.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.Tests
{
    /// <summary>
    /// What the settings screen's look stands on: every piece of its art in the menu
    /// theme, imported smooth, mipmapped and uncompressed from Assets/Art/UI/Settings,
    /// and the UI map's Reset action on F and the pad's north button.
    /// </summary>
    public class SettingsLookTests
    {
        private const string ThemePath = "Assets/Settings/MenuTheme.asset";
        private const string ActionsPath = "Assets/Settings/Descender.inputactions";

        [Test]
        public void TheThemeHasEveryPieceOfSettingsArtAsASharpSmoothSprite()
        {
            var look = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath).settingsLook;
            Assert.That(look, Is.Not.Null);
            var sprites = new[]
            {
                look.emblem,
                look.ruleLong,
                look.rulePlain,
                look.ruleRow,
                look.ruleVertical,
                look.tabGlow,
                look.sideBar,
                look.rowBar,
                look.toggleOn,
                look.toggleOff,
                look.sliderTrack,
                look.sliderFill,
                look.sliderHandle,
                look.chevron,
                look.iconGameplay,
                look.iconInterface,
                look.iconMovement,
                look.iconActions,
                look.iconScreen,
                look.iconEffects,
                look.iconVolume,
                look.iconVoices,
            };
            foreach (var sprite in sprites)
            {
                Assert.That(sprite, Is.Not.Null, "a settings sprite is missing from the menu theme");
                var path = AssetDatabase.GetAssetPath(sprite);
                StringAssert.StartsWith("Assets/Art/UI/Settings/", path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.mipmapEnabled, Is.True, path);
            }
            Assert.That(look.toggleOn, Is.Not.SameAs(look.toggleOff));
            Assert.That(
                TitleKit.SizeOf(look.rowBar).x,
                Is.GreaterThanOrEqualTo(SettingsRow.RowWidth),
                "the row bar spans a row"
            );
        }

        [Test]
        public void ResetIsOnFAndThePadsNorthButton()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            var reset = actions.FindActionMap("UI", true).FindAction("Reset", false);
            Assert.That(reset, Is.Not.Null, "the UI map has no Reset action");
            var paths = new System.Collections.Generic.List<string>();
            foreach (var binding in reset.bindings)
            {
                paths.Add(binding.path);
            }
            Assert.That(paths, Does.Contain("<Keyboard>/f"));
            Assert.That(paths, Does.Contain("<Gamepad>/buttonNorth"));
        }
    }
}
