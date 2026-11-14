using TMPro;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// A line of text in the world (a sign over a lab shape, a key prompt over a door),
    /// kept readable. The world is drawn at 320 x 180 and scaled up, and the pixel font is
    /// rasterised at <see cref="UiTheme.FontSize"/>, so the text is drawn at that size and
    /// its left edge and baseline are put on whole pixels every frame; anything else
    /// drops or doubles rows and columns of the glyphs. The label stands at a point above
    /// its parent: centred on it across, its baseline on it.
    /// </summary>
    public sealed class PixelLabel : MonoBehaviour
    {
        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        /// <summary>The label's height in pixels; the baseline is at its middle.</summary>
        private const float Height = 16f;

        private Canvas canvas;
        private RectTransform rect;
        private TextMeshProUGUI text;
        private Vector2 anchor;
        private string measured;
        private float width;

        public Canvas Canvas => canvas;
        public TextMeshProUGUI Text => text;

        /// <summary>Where the label stands, in units from its parent's position.</summary>
        public Vector2 Anchor
        {
            get => anchor;
            set
            {
                anchor = value;
                Place();
            }
        }

        /// <summary>A label under `parent`, its baseline `anchor` units from the parent's position.</summary>
        public static PixelLabel Make(Transform parent, string name, Vector2 anchor, int sortingOrder, bool shadow)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<PixelLabel>();
            label.canvas = go.GetComponent<Canvas>();
            label.canvas.renderMode = RenderMode.WorldSpace;
            label.canvas.sortingOrder = sortingOrder;
            label.rect = go.GetComponent<RectTransform>();
            label.rect.pivot = new Vector2(0f, 0.5f);
            label.rect.sizeDelta = new Vector2(0f, Height);
            label.rect.localScale = Vector3.one / Ppu;
            label.text = UiKit.Label(
                label.rect,
                "Text",
                "",
                UiTheme.FontSize,
                UiTheme.Text,
                TextAlignmentOptions.BaselineLeft,
                shadow
            );
            UiKit.Fill(label.text.rectTransform);
            label.anchor = anchor;
            label.Place();
            return label;
        }

        /// <summary>Shows `value`, measured and put on whole pixels at once.</summary>
        public void Show(string value)
        {
            text.text = value ?? "";
            Place();
        }

        private void LateUpdate()
        {
            Place();
        }

        /// <summary>Measures the text when it has changed, and puts the label on whole pixels.</summary>
        private void Place()
        {
            if (text == null || rect == null)
            {
                return;
            }
            if (!ReferenceEquals(measured, text.text))
            {
                measured = text.text;
                width = Mathf.Ceil(text.GetPreferredValues(measured).x);
                rect.sizeDelta = new Vector2(width, Height);
            }
            var parent = transform.parent;
            var origin = parent != null ? parent.position : Vector3.zero;
            var left = Mathf.Round((origin.x + anchor.x) * Ppu - width / 2f) / Ppu;
            var baseline = Mathf.Round((origin.y + anchor.y) * Ppu) / Ppu;
            transform.position = new Vector3(left, baseline, origin.z);
        }
    }
}
