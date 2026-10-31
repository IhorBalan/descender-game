using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// The look of a region: the tint on the screen while the hero is there, the sky's
    /// two colours when the region has its own, whether it is a cave, the rim light
    /// the sky throws on the tiles, the colour grade and the music loop. One asset per
    /// region under Assets/Data/Regions, referenced by its WorldRegion.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Region Look", fileName = "region")]
    public sealed class RegionLook : ScriptableObject
    {
        [Tooltip("The colour that tints the screen while the hero is in the region.")]
        public Color ambient = new Color(0.76f, 0.8f, 0.9f);

        [Header("Sky")]
        [Tooltip("Whether the region has sky colours of its own, below.")]
        public bool ownSky;
        public Color skyTop = new Color(0.4f, 0.6f, 0.9f);
        public Color skyBottom = new Color(0.8f, 0.85f, 0.95f);
        [Tooltip("Flat dark streaks instead of clouds and ridges.")]
        public bool cave;

        [Header("Light")]
        [Tooltip("The rim light the sky throws on the tiles; transparent for the sky top.")]
        public Color light = new Color(0f, 0f, 0f, 0f);

        [Header("Grade")]
        [Tooltip("A colour the view is multiplied by.")]
        public Color tint = Color.white;
        [Range(0f, 3f)] public float contrast = 1f;
        [Range(0f, 3f)] public float saturation = 1f;

        [Header("Sound")]
        [Tooltip("The music loop; empty for the region's key.")]
        public string music = "";
    }
}
