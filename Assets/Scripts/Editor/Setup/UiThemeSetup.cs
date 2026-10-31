using System.IO;
using Descender.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Descender.EditorTools.Setup
{
    /// <summary>
    /// Fills the UI theme, from the menu Descender > Setup > UI theme: gives the
    /// stone textures under Assets/Art/UI/Theme their four-pixel nine-slice border,
    /// makes the Pirata One TextMeshPro font asset (rasterised at 9 px, no
    /// smoothing, dynamic atlas) and writes Assets/Resources/UiTheme.asset pointing
    /// at all of it. Also makes a TMP Settings asset when the TMP essentials were
    /// never imported, so TextMeshPro can run. Safe to run again.
    /// </summary>
    public static class UiThemeSetup
    {
        public const string ThemeFolder = "Assets/Art/UI/Theme";
        public const string FontPath = "Assets/Art/Fonts/PirataOne-Regular.ttf";
        public const string FontAssetPath = "Assets/Art/Fonts/PirataOne 9px.asset";
        public const string ResourcesFolder = "Assets/Resources";
        public const string ThemeAssetPath = ResourcesFolder + "/UiTheme.asset";
        public const string TmpResourcesFolder = "Assets/TextMesh Pro/Resources";
        public const string TmpSettingsPath = TmpResourcesFolder + "/TMP Settings.asset";

        private static readonly string[] Sliced = { "button_normal", "button_focus", "button_disabled", "panel" };

        [MenuItem("Descender/Setup/UI theme")]
        public static void Run()
        {
            SetBorders();
            var fontAsset = MakeFontAsset();
            EnsureTmpSettings(fontAsset);
            Directory.CreateDirectory(ResourcesFolder);
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemeAssetPath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
            }
            var serialized = new SerializedObject(theme);
            serialized.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            serialized.FindProperty("fontAsset").objectReferenceValue = fontAsset;
            Set(serialized, "buttonNormal", "button_normal");
            Set(serialized, "buttonFocus", "button_focus");
            Set(serialized, "buttonDisabled", "button_disabled");
            Set(serialized, "panel", "panel");
            Set(serialized, "divider", "divider");
            Set(serialized, "grabber", "grabber");
            Set(serialized, "toggleOn", "toggle_on");
            Set(serialized, "toggleOff", "toggle_off");
            Set(serialized, "titleBackdrop", "title_backdrop");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("Descender: UI theme written to " + ThemeAssetPath);
        }

        private static void Set(SerializedObject theme, string field, string sprite)
        {
            theme.FindProperty(field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{ThemeFolder}/{sprite}.png"
            );
        }

        /// <summary>The nine-sliced textures keep a four-pixel border, as the Godot StyleBoxTextures had.</summary>
        private static void SetBorders()
        {
            foreach (var name in Sliced)
            {
                var path = $"{ThemeFolder}/{name}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }
                var border = Vector4.one * UiTheme.SliceBorder;
                if (importer.spriteBorder == border && importer.filterMode == FilterMode.Point)
                {
                    continue;
                }
                importer.spriteBorder = border;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
        }

        private static TMP_FontAsset MakeFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogWarning("UiThemeSetup: no font at " + FontPath);
                return null;
            }
            var asset = TMP_FontAsset.CreateFontAsset(
                font,
                UiTheme.FontSize,
                1,
                GlyphRenderMode.RASTER,
                512,
                512,
                AtlasPopulationMode.Dynamic
            );
            asset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
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
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// TextMeshPro reads its defaults from Resources/TMP Settings; without the
        /// essentials imported there is none, and every TMP text throws. This writes a
        /// minimal one with the theme font as the default.
        /// </summary>
        private static void EnsureTmpSettings(TMP_FontAsset defaultFont)
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return;
            }
            Directory.CreateDirectory(TmpResourcesFolder);
            var settings = ScriptableObject.CreateInstance<TMP_Settings>();
            var serialized = new SerializedObject(settings);
            var fontProperty = serialized.FindProperty("m_defaultFontAsset");
            if (fontProperty != null)
            {
                fontProperty.objectReferenceValue = defaultFont;
            }
            var styleSheet = serialized.FindProperty("m_defaultStyleSheet");
            if (styleSheet != null && styleSheet.objectReferenceValue == null)
            {
                var sheet = ScriptableObject.CreateInstance<TMP_StyleSheet>();
                AssetDatabase.CreateAsset(sheet, TmpResourcesFolder + "/Default Style Sheet.asset");
                styleSheet.objectReferenceValue = sheet;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, TmpSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Descender: wrote a minimal TMP Settings; import the TMP Essential Resources for the full set.");
        }
    }
}
