using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// The settings screen's art, from Assets/Art/UI/Settings (drawn by
    /// tools/settings_textures.py at <see cref="TitleKit.ArtScale"/> times its stage
    /// size): the emblem, the rules, the tab underline, the section and row bars,
    /// the toggle, the slider, the chevron and one white icon per section. Held by
    /// the <see cref="MenuTheme"/>.
    /// </summary>
    [System.Serializable]
    public sealed class SettingsLook
    {
        public Sprite emblem;
        public Sprite ruleLong;
        public Sprite rulePlain;
        public Sprite ruleRow;
        public Sprite ruleVertical;
        public Sprite tabGlow;
        public Sprite sideBar;
        public Sprite rowBar;
        public Sprite toggleOn;
        public Sprite toggleOff;
        public Sprite sliderTrack;
        public Sprite sliderFill;
        public Sprite sliderHandle;
        public Sprite chevron;

        [Header("Section icons")]
        public Sprite iconGameplay;
        public Sprite iconInterface;
        public Sprite iconMovement;
        public Sprite iconActions;
        public Sprite iconScreen;
        public Sprite iconEffects;
        public Sprite iconVolume;
        public Sprite iconVoices;
    }
}
