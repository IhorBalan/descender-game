using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// The look of the menus: the Pirata One font, the nine-sliced stone buttons and
    /// panel, the divider, the slider grabber and the toggle icons from
    /// Assets/Art/UI/Theme, the title screen's gilded logo, halo, focus bar, rules,
    /// motes and key caps from Assets/Art/UI/Title and its Cinzel font, and the text colours of the prototype's stone theme. One
    /// asset, Assets/Settings/MenuTheme.asset, referenced by the menu screens in
    /// Main.unity and read by <see cref="MenuKit"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Menu Theme", fileName = "MenuTheme")]
    public sealed class MenuTheme : ScriptableObject
    {
        public const int BaseFontSize = 9;
        public const int MutedFontSize = 8;
        public const int SubheadingFontSize = 12;
        public const int HeadingFontSize = 18;
        public const int TitleFontSize = 30;

        [Header("Font")]
        public Font font;

        [Header("Sprites")]
        public Sprite buttonNormal;
        public Sprite buttonFocus;
        public Sprite buttonDisabled;
        public Sprite panel;
        public Sprite divider;
        public Sprite grabber;
        public Sprite toggleOn;
        public Sprite toggleOff;
        public Sprite titleBackdrop;

        [Header("Title screen (Assets/Art/UI/Title, drawn by tools/title_textures.py)")]
        public Sprite titleLogo;
        public Sprite titleHalo;
        public Sprite focusBar;
        public Sprite focusBarWide;
        public Sprite focusEmblem;
        public Sprite ruleItem;
        public Sprite ruleTagline;
        public Sprite ruleSide;
        public Sprite mote;
        public Sprite keyCap;
        public Sprite keyDpad;

        /// <summary>Cinzel as a TextMeshPro distance-field asset, made by Descender > Setup > Title font.</summary>
        public TMPro.TMP_FontAsset titleFont;

        [Header("Settings screen (Assets/Art/UI/Settings, drawn by tools/settings_textures.py)")]
        public SettingsLook settingsLook = new SettingsLook();

        [Header("Colours")]
        public Color text = new Color(0.85f, 0.8f, 0.75f);
        public Color textMuted = new Color(0.6f, 0.54f, 0.51f);
        public Color textDisabled = new Color(0.42f, 0.37f, 0.36f);
        public Color subheading = new Color(1f, 0.69f, 0.44f);
        public Color heading = new Color(0.91f, 0.85f, 0.75f);
        public Color outline = new Color(0.23f, 0.04f, 0.02f);
        public Color sliderTrack = new Color(0.1f, 0.06f, 0.07f);
        public Color sliderFill = new Color(0.75f, 0.25f, 0.1f);
        public Color good = new Color(0.56f, 0.88f, 0.54f);
        public Color bad = new Color(1f, 0.55f, 0.45f);
        public Color dim = new Color(0f, 0f, 0f, 0.6f);
    }
}
