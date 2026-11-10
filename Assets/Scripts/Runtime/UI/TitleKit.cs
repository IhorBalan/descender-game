using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Builders and colours for the title screen's gilded look: a flat dark ground,
    /// smooth gold ornaments, widely spaced Cinzel capitals, and items that are only
    /// text until the focus bar lights them. The screen is laid out on a 1920 x 1080
    /// stage; positions are the centres of things, in pixels from the stage's top left.
    /// </summary>
    public static class TitleKit
    {
        public const float Width = 1920f;
        public const float Height = 1080f;

        /// <summary>
        /// How many times its stage size the art is drawn (tools/title_textures.py), so it
        /// stays sharp up to 4K; sprites are shown at their size divided by this.
        /// </summary>
        public const float ArtScale = 2f;

        /// <summary>The gap between two items' middles.</summary>
        public const float Step = 62f;

        /// <summary>The size of an item's text, and the spacing of its capitals in hundredths of an em.</summary>
        public const float ItemSize = 27f;
        public const float CapsSpacing = 26f;

        /// <summary>The size and spacing of a mixed-case item (a save slot, a difficulty).</summary>
        public const float RowSize = 24f;
        public const float RowSpacing = 4f;

        /// <summary>The tagline's and the key hints' size, and the tagline's spacing.</summary>
        public const float SmallSize = 18f;
        public const float SmallSpacing = 30f;

        /// <summary>A heading over a column (the slot and difficulty pickers).</summary>
        public const float HeadingSize = 21f;

        /// <summary>The height of a key cap in the hints along the bottom.</summary>
        public const float KeyHeight = 36f;

        public static readonly Color Background = new Color32(14, 12, 12, 255);
        public static readonly Color Ivory = new Color32(255, 240, 214, 255);
        public static readonly Color Parchment = new Color32(198, 182, 160, 255);
        public static readonly Color Faded = new Color32(108, 98, 90, 255);
        public static readonly Color Muted = new Color32(186, 170, 150, 255);
        public static readonly Color Gold = new Color32(236, 190, 112, 255);
        public static readonly Color Glow = new Color32(226, 132, 40, 150);
        public static readonly Color Bad = new Color32(255, 140, 115, 255);

        private static Material glowMaterial;
        private static TMP_FontAsset glowSource;

        /// <summary>The font every title label is set in; the title screen sets it from its theme before it builds.</summary>
        public static TMP_FontAsset Font { get; set; }

        /// <summary>
        /// The font's material with a soft warm underlay, for the focused item's text:
        /// one shared copy, made the first time it is asked for.
        /// </summary>
        public static Material GlowMaterial
        {
            get
            {
                if (Font == null || Font.material == null)
                {
                    return null;
                }
                if (glowMaterial == null || glowSource != Font)
                {
                    glowSource = Font;
                    glowMaterial = new Material(Font.material) { name = Font.name + " Glow" };
                    glowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    glowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, Glow);
                    glowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.9f);
                    glowMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
                }
                return glowMaterial;
            }
        }

        /// <summary>Puts a rect `w` by `h` centred on (cx, cy) from the stage's top left.</summary>
        public static RectTransform Center(RectTransform rect, float cx, float cy, float w, float h)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(cx, -cy);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        /// <summary>The middle line of a rect placed by <see cref="Center"/>, in pixels from the top.</summary>
        public static float MiddleOf(RectTransform rect) => -rect.anchoredPosition.y;

        /// <summary>A sprite's size on the stage: its pixels divided by <see cref="ArtScale"/>.</summary>
        public static Vector2 SizeOf(Sprite sprite) => sprite != null ? sprite.rect.size / ArtScale : Vector2.zero;

        /// <summary>A sprite at its stage size, centred on (cx, cy).</summary>
        public static Image Sprite(Transform parent, string name, Sprite sprite, float cx, float cy)
        {
            var image = UiKit.Box(parent, name, sprite != null ? Color.white : Color.clear);
            image.sprite = sprite;
            var size = sprite != null ? SizeOf(sprite) : Vector2.one;
            Center(image.rectTransform, cx, cy, size.x, size.y);
            return image;
        }

        /// <summary>A line of text in the title font, centred on (cx, cy) in a rect `w` wide.</summary>
        public static TextMeshProUGUI Label(
            Transform parent,
            string text,
            float size,
            Color color,
            float spacing,
            float cx,
            float cy,
            float w = Width
        )
        {
            var rect = UiKit.Rect(parent, "Label");
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null)
            {
                label.font = Font;
            }
            label.fontSize = size;
            label.color = color;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = false;
            label.raycastTarget = false;
            label.text = text;
            Center(rect, cx, cy, w, size * 2f);
            return label;
        }

        /// <summary>How wide a label's text is, in stage pixels.</summary>
        public static float WidthOf(TextMeshProUGUI label) =>
            string.IsNullOrEmpty(label.text) ? 0f : label.GetPreferredValues(label.text).x;

        /// <summary>
        /// An item: a clickable strip `width` wide with a text in it and no box. Its
        /// <see cref="TitleItem"/> colours and lights the text by focus.
        /// </summary>
        public static Button Item(Transform parent, string text, float width, bool caps)
        {
            var rect = UiKit.Rect(parent, "Item");
            rect.sizeDelta = new Vector2(width, Step);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            var label = Label(
                rect,
                "",
                caps ? ItemSize : RowSize,
                Parchment,
                caps ? CapsSpacing : RowSpacing,
                width * 0.5f,
                Step * 0.5f,
                width
            );
            var item = rect.gameObject.AddComponent<TitleItem>();
            item.Bind(label, caps);
            item.SetText(text);
            return button;
        }

        /// <summary>The text of an item made by <see cref="Item"/>.</summary>
        public static void SetText(Button button, string text)
        {
            if (button != null && button.TryGetComponent<TitleItem>(out var item))
            {
                item.SetText(text);
            }
        }

        /// <summary>
        /// A key cap with a key's name in it, its left edge at `x` and its middle on `cy`;
        /// returns its width. The cap stretches (nine-sliced) to fit the name.
        /// </summary>
        public static float KeyCap(Transform parent, Sprite cap, string key, float x, float cy)
        {
            var box = UiKit.Box(parent, "Key", Color.white);
            box.sprite = cap;
            box.type = Image.Type.Sliced;
            // The cap is drawn at ArtScale, so its border shows at its stage size.
            box.pixelsPerUnitMultiplier = ArtScale;
            var label = Label(box.transform, key, SmallSize, Parchment, 4f, 0f, 0f, 400f);
            var w = Mathf.Max(KeyHeight, WidthOf(label) + 22f);
            Center(box.rectTransform, x + w * 0.5f, cy, w, KeyHeight);
            Center(label.rectTransform, w * 0.5f, KeyHeight * 0.5f, w, KeyHeight);
            return w;
        }
    }
}
