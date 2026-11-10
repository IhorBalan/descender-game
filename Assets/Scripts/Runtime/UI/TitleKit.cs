using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Builders and colours for the title screen's gilded look: a flat dark ground,
    /// gold rules, spaced capitals in the 9 px pixel font (<see cref="PixelText"/>), and items that are only
    /// text until the focus bar lights them. Everything is placed in whole level
    /// pixels from the top left of the 320 x 180 stage (see <see cref="UiKit.Place"/>),
    /// so it stays pixel perfect under <see cref="PixelCanvasScale"/>.
    /// </summary>
    public static class TitleKit
    {
        /// <summary>The one text size: the raster font's own, so no glyph is resampled.</summary>
        public const int FontSize = UiTheme.FontSize;

        /// <summary>The gap between two items' middles.</summary>
        public const int Step = 14;

        /// <summary>A key cap's height.</summary>
        public const float KeyHeight = 11f;

        /// <summary>Where a key cap's text sits in it.</summary>
        public const float KeyTextTop = -1f;

        /// <summary>Where an item's text sits in its strip, so the capitals are centred in it.</summary>
        public const float ItemTextTop = 0f;

        /// <summary>The letter spacing of the spaced capitals, in pixels.</summary>
        public const int CapsSpacing = 2;

        public static readonly Color Background = new Color32(15, 12, 12, 255);
        public static readonly Color Ivory = new Color32(255, 240, 212, 255);
        public static readonly Color Parchment = new Color32(196, 178, 156, 255);
        public static readonly Color Faded = new Color32(98, 88, 82, 255);
        public static readonly Color Muted = new Color32(150, 134, 118, 255);
        public static readonly Color Gold = new Color32(240, 190, 104, 255);
        public static readonly Color KeyFill = new Color32(30, 24, 22, 255);
        public static readonly Color KeyEdge = new Color32(120, 96, 70, 255);
        public static readonly Color Bad = new Color32(255, 140, 115, 255);

        /// <summary>The font every title label draws with; set by the title screen from its theme before it builds.</summary>
        public static PixelFont Font { get; set; }

        /// <summary>A label in the pixel font with `spacing` pixels between letters, drawn `scale` times over.</summary>
        public static PixelText Label(Transform parent, string text, Color color, int spacing = 0, int scale = 1)
        {
            var rect = UiKit.Rect(parent, "Label");
            // Added first: the graphic needs it and does not always get it from its base class.
            rect.gameObject.AddComponent<CanvasRenderer>();
            var label = rect.gameObject.AddComponent<PixelText>();
            label.Setup(Font, spacing, scale);
            label.color = color;
            label.Text = text;
            rect.sizeDelta = new Vector2(label.InkWidth, label.LineHeight);
            return label;
        }

        /// <summary>Sets a label's text and centres its ink in a span `width` wide, `y` from the top, on a whole pixel.</summary>
        public static void Center(PixelText label, string text, float width, float y)
        {
            label.Text = text;
            var w = label.InkWidth;
            UiKit.Place(label.rectTransform, Mathf.Floor((width - w) * 0.5f), y, w, label.LineHeight);
        }

        /// <summary>A sprite at its own pixel size, centred across a span `width` wide, `y` from the top.</summary>
        public static Image Sprite(Transform parent, string name, Sprite sprite, float width, float y)
        {
            var image = UiKit.Box(parent, name, Color.white);
            image.sprite = sprite;
            var size = sprite != null ? sprite.rect.size : new Vector2(1f, 1f);
            UiKit.Place(image.rectTransform, Mathf.Floor((width - size.x) * 0.5f), y, size.x, size.y);
            if (sprite == null)
            {
                image.color = Color.clear;
            }
            return image;
        }

        /// <summary>
        /// An item: a clickable strip `width` wide with a text in it and no box. Its
        /// <see cref="TitleItem"/> colours the text by focus and centres it.
        /// </summary>
        public static Button Item(
            Transform parent,
            string text,
            UnityEngine.Events.UnityAction onClick,
            float width,
            bool caps
        )
        {
            var rect = UiKit.Rect(parent, "Item");
            rect.sizeDelta = new Vector2(width, Step);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            var label = Label(rect, "", Parchment, caps ? CapsSpacing : 0);
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

        /// <summary>A key cap: a dark box with a one-pixel edge and the key's name in it. Returns its width.</summary>
        public static float KeyCap(Transform parent, string key, float x, float y)
        {
            var edge = UiKit.Box(parent, "Key", KeyEdge);
            var fill = UiKit.Box(edge.transform, "Fill", KeyFill);
            var label = Label(edge.transform, key, Parchment);
            var w = label.InkWidth + 6f;
            UiKit.Place(edge.rectTransform, x, y, w, KeyHeight);
            UiKit.Place(fill.rectTransform, 1f, 1f, w - 2f, KeyHeight - 2f);
            UiKit.Place(label.rectTransform, 3f, KeyTextTop, label.InkWidth, label.LineHeight);
            return w;
        }
    }
}
