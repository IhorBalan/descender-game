using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Descender.UI
{
    /// <summary>
    /// The stone look of the menus, as godot_assets/ui_theme.tres had it: the Pirata
    /// One font, the nine-sliced button and panel textures under Assets/Art/UI/Theme
    /// and the colours every screen shares. One asset, Assets/Resources/UiTheme.asset,
    /// reached through <see cref="Current"/>; <see cref="UiKit"/> reads it when it
    /// builds a control. Descender > Setup > UI theme fills the asset in the editor.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        public const string ResourceName = "UiTheme";

        /// <summary>The game renders at 320 x 180; every screen is laid out in those pixels.</summary>
        public const int Width = 320;
        public const int Height = 180;

        /// <summary>Font sizes in pixels, as the Godot theme's variations.</summary>
        public const int FontSize = 9;
        public const int SmallFontSize = 8;
        public const int SubheadingSize = 12;
        public const int HeadingSize = 18;
        public const int TitleSize = 30;

        /// <summary>The border of the nine-sliced theme textures, in texture pixels.</summary>
        public const int SliceBorder = 4;

        /// <summary>Height of a stone button, and the padding inside it.</summary>
        public const float ButtonHeight = 16f;
        public const float ButtonPadding = 8f;

        // --- Colours, as the Godot theme and the menu scripts named them ----------------

        public static readonly Color Text = new Color(0.85f, 0.8f, 0.75f);
        public static readonly Color TextFocus = new Color(1f, 0.89f, 0.72f);
        public static readonly Color TextDisabled = new Color(0.42f, 0.37f, 0.36f);
        public static readonly Color Heading = new Color(0.91f, 0.85f, 0.75f);
        public static readonly Color HeadingOutline = new Color(0.23f, 0.04f, 0.02f);
        public static readonly Color Subheading = new Color(1f, 0.69f, 0.44f);
        public static readonly Color Muted = new Color(0.6f, 0.54f, 0.51f);
        public static readonly Color Dim = new Color(0.08f, 0.02f, 0.02f, 0.75f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.8f);

        /// <summary>The colours the in-game menus write their lines in.</summary>
        public static readonly Color Highlight = new Color(1f, 0.95f, 0.7f);
        public static readonly Color Item = new Color(0.95f, 0.95f, 1f);
        public static readonly Color Note = new Color(0.7f, 0.7f, 0.78f);
        public static readonly Color Warn = new Color(1f, 0.7f, 0.6f);
        public static readonly Color Done = new Color(0.75f, 1f, 0.8f);
        public static readonly Color Locked = new Color(0.55f, 0.52f, 0.7f);
        public static readonly Color Pale = new Color(0.85f, 0.85f, 0.95f);

        /// <summary>Key names in a prompt are written in this colour, so they stand out.</summary>
        public const string KeyColorHex = "#ffe2b8";

        [SerializeField]
        private Font font;

        [SerializeField]
        private TMP_FontAsset fontAsset;

        [SerializeField]
        private Sprite buttonNormal;

        [SerializeField]
        private Sprite buttonFocus;

        [SerializeField]
        private Sprite buttonDisabled;

        [SerializeField]
        private Sprite panel;

        [SerializeField]
        private Sprite divider;

        [SerializeField]
        private Sprite grabber;

        [SerializeField]
        private Sprite toggleOn;

        [SerializeField]
        private Sprite toggleOff;

        [SerializeField]
        private Sprite titleBackdrop;

        private TMP_FontAsset runtimeFont;
        private static UiTheme current;

        public Font Font => font;
        public Sprite ButtonNormal => buttonNormal;
        public Sprite ButtonFocus => buttonFocus;
        public Sprite ButtonDisabled => buttonDisabled;
        public Sprite Panel => panel;
        public Sprite Divider => divider;
        public Sprite Grabber => grabber;
        public Sprite ToggleOn => toggleOn;
        public Sprite ToggleOff => toggleOff;
        public Sprite TitleBackdrop => titleBackdrop;

        /// <summary>
        /// The theme in use: the one assigned here, else Assets/Resources/UiTheme.asset,
        /// else a bare instance (plain colours, the default font) so a screen built
        /// before the asset exists still shows up.
        /// </summary>
        public static UiTheme Current
        {
            get
            {
                if (current == null)
                {
                    current = Resources.Load<UiTheme>(ResourceName);
                }
                if (current == null)
                {
                    Debug.LogWarning("UiTheme: no Resources/UiTheme asset; run Descender > Setup > UI theme.");
                    current = CreateInstance<UiTheme>();
                }
                return current;
            }
            set => current = value;
        }

        /// <summary>
        /// The TextMeshPro font: the asset when one is set, else one made at runtime
        /// from the font (rasterised at the pixel size, no smoothing), else TMP's default.
        /// </summary>
        public TMP_FontAsset FontAsset
        {
            get
            {
                if (fontAsset != null)
                {
                    return fontAsset;
                }
                if (runtimeFont == null && font != null)
                {
                    runtimeFont = TMP_FontAsset.CreateFontAsset(
                        font,
                        FontSize,
                        1,
                        GlyphRenderMode.RASTER,
                        512,
                        512,
                        AtlasPopulationMode.Dynamic
                    );
                }
                return runtimeFont != null ? runtimeFont : TMP_Settings.defaultFontAsset;
            }
        }
    }
}
