using System.IO;
using Descender.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Descender.EditorTools.Setup
{
    /// <summary>
    /// Makes the title screen's font, from the menu Descender > Setup > Title font:
    /// a TextMeshPro signed-distance-field asset of Cinzel Regular (Assets/Art/Fonts,
    /// OFL) holding every printable ASCII character in a static atlas, so the
    /// smooth text stays sharp at any size and the asset never changes at runtime,
    /// and points the menu theme's titleFont at it. Safe to run again; an existing
    /// asset is kept.
    /// </summary>
    public static class TitleFontSetup
    {
        public const string FontPath = "Assets/Art/Fonts/Cinzel-Regular.ttf";
        public const string FontAssetPath = "Assets/Art/Fonts/Cinzel SDF.asset";

        /// <summary>The glyph size the atlas is sampled at; the SDF scales from it smoothly.</summary>
        public const int SamplingSize = 64;

        [MenuItem("Descender/Setup/Title font")]
        public static void Run()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (asset == null)
            {
                asset = Make();
            }
            if (asset == null)
            {
                return;
            }
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(MainSceneSetup.MenuThemePath);
            if (theme != null && theme.titleFont != asset)
            {
                theme.titleFont = asset;
                EditorUtility.SetDirty(theme);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Descender: title font at " + FontAssetPath);
        }

        private static TMP_FontAsset Make()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogWarning("TitleFontSetup: no font at " + FontPath);
                return null;
            }
            var asset = TMP_FontAsset.CreateFontAsset(
                font,
                SamplingSize,
                8,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic
            );
            asset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            var ascii = new System.Text.StringBuilder();
            for (var c = ' '; c <= '~'; c++)
            {
                ascii.Append(c);
            }
            asset.TryAddCharacters(ascii.ToString(), out var missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("TitleFontSetup: Cinzel lacks " + missing);
            }
            // Filled once here; the atlas stays as it is in play and in builds.
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
            {
                asset.atlasTextures[0].name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }
            if (asset.material != null)
            {
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
