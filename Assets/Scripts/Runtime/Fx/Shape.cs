using TMPro;
using UnityEngine;

namespace Descender.Fx
{
    public enum ShapeKind
    {
        Square,
        Circle,
        Diamond,
        Triangle,
    }

    /// <summary>
    /// A placeholder: a square, circle, diamond or triangle of a size and a colour, with
    /// an optional label above it, drawn from <see cref="Shapes"/> with no art asset.
    /// Everything that has no art yet stands as one of these: an item, a trigger, a
    /// platform, an enemy. The object's position is the shape's middle. Made from code
    /// with <see cref="Make"/>, or placed by hand and filled in the inspector.
    /// </summary>
    public sealed class Shape : MonoBehaviour
    {
        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        /// <summary>The square sprite is 4 pixels, a quarter of a unit; the others are one unit across.</summary>
        private const float SquareUnits = 4f / Ppu;

        [SerializeField]
        private ShapeKind kind = ShapeKind.Square;

        [SerializeField]
        [Tooltip("Width and height in units.")]
        private Vector2 size = Vector2.one;

        [SerializeField]
        private Color color = Color.white;

        [SerializeField]
        [Tooltip("Text shown above the shape; empty for none.")]
        private string label = "";

        [SerializeField]
        private int sortingOrder;

        private SpriteRenderer body;
        private Canvas labelCanvas;
        private TextMeshProUGUI labelText;

        public ShapeKind Kind => kind;
        public Vector2 Size => size;
        public Color Color => color;
        public string Label => label;
        public SpriteRenderer Body => body;

        /// <summary>The shape's box in the world.</summary>
        public Bounds Bounds => new Bounds(transform.position, size);

        /// <summary>A new shape under `parent` with its middle at `centre` (local to the parent).</summary>
        public static Shape Make(
            Transform parent,
            string name,
            ShapeKind kind,
            Vector2 centre,
            Vector2 size,
            Color color,
            string label = "",
            int sortingOrder = 0
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var shape = go.AddComponent<Shape>();
            shape.Set(kind, size, color, label, sortingOrder);
            return shape;
        }

        /// <summary>Changes what is drawn.</summary>
        public void Set(ShapeKind newKind, Vector2 newSize, Color newColor, string newLabel = "", int order = 0)
        {
            kind = newKind;
            size = newSize;
            color = newColor;
            label = newLabel ?? "";
            sortingOrder = order;
            Rebuild();
        }

        /// <summary>The sprite a kind is drawn with.</summary>
        public static Sprite SpriteOf(ShapeKind kind)
        {
            switch (kind)
            {
                case ShapeKind.Circle:
                    return Shapes.Disc;
                case ShapeKind.Diamond:
                    return Shapes.Diamond;
                case ShapeKind.Triangle:
                    return Shapes.Triangle;
                default:
                    return Shapes.Square;
            }
        }

        private void Awake()
        {
            if (body == null)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            if (body == null)
            {
                var go = new GameObject("Body");
                go.transform.SetParent(transform, false);
                body = go.AddComponent<SpriteRenderer>();
                body.sharedMaterial = Shapes.Unlit;
            }
            body.sprite = SpriteOf(kind);
            body.color = color;
            body.sortingOrder = sortingOrder;
            var unit = kind == ShapeKind.Square ? SquareUnits : 1f;
            body.transform.localScale = new Vector3(size.x / unit, size.y / unit, 1f);
            SetLabel();
        }

        private void SetLabel()
        {
            if (string.IsNullOrEmpty(label))
            {
                if (labelCanvas != null)
                {
                    labelCanvas.gameObject.SetActive(false);
                }
                return;
            }
            if (labelCanvas == null)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
                go.transform.SetParent(transform, false);
                labelCanvas = go.GetComponent<Canvas>();
                labelCanvas.renderMode = RenderMode.WorldSpace;
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(160f, 12f);
                rect.localScale = Vector3.one / Ppu;
                labelText = UI.UiKit.Label(
                    rect,
                    "Text",
                    "",
                    UI.UiTheme.SmallFontSize,
                    UI.UiTheme.Text,
                    TextAlignmentOptions.Center,
                    true
                );
                UI.UiKit.Fill(labelText.rectTransform);
                labelText.verticalAlignment = VerticalAlignmentOptions.Middle;
            }
            labelCanvas.gameObject.SetActive(true);
            labelCanvas.sortingOrder = sortingOrder + 1;
            labelCanvas.transform.localPosition = new Vector3(0f, size.y / 2f + 0.5f, 0f);
            labelText.text = label;
        }
    }
}
