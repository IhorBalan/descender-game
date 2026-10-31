using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Builds uGUI controls in the stone style from code, the way the Godot screens
    /// built theirs in _ready: a 320 x 180 canvas, labels, nine-sliced buttons with
    /// the focus texture, panels, a dim backdrop, scrolling lists and explicit
    /// up/down navigation. Positions are given as Godot did, from the top left in
    /// level pixels (<see cref="Place"/>), so a screen ports line for line. Every
    /// screen and the HUD go through here so they share one look.
    /// </summary>
    public static class UiKit
    {
        public const float RowHeight = UiTheme.ButtonHeight;
        public const float RowGap = 1f;

        /// <summary>
        /// A canvas that fills the 320 x 180 view and scales with the window: a Screen
        /// Space Overlay canvas with a Scale With Screen Size scaler, drawn in `sortOrder`.
        /// </summary>
        public static Canvas MakeCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = true;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiTheme.Width, UiTheme.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 16f;
            return canvas;
        }

        /// <summary>A canvas with a raycaster, for a screen whose buttons take clicks.</summary>
        public static Canvas MakeInteractiveCanvas(string name, int sortOrder, Transform parent = null)
        {
            var canvas = MakeCanvas(name, sortOrder, parent);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>An empty rect under `parent`, anchored to its top left.</summary>
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        /// <summary>Puts a rect at (x, y) from the parent's top left, `w` by `h`, in level pixels.</summary>
        public static RectTransform Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        /// <summary>Stretches a rect over its parent, with the given margins.</summary>
        public static RectTransform Fill(
            RectTransform rect,
            float left = 0f,
            float top = 0f,
            float right = 0f,
            float bottom = 0f
        )
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>A rect `w` wide centred on the parent, `y` from the top.</summary>
        public static RectTransform PlaceCentered(RectTransform rect, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        /// <summary>A rect spanning the parent's width, `y` from the top, with side margins.</summary>
        public static RectTransform PlaceRow(RectTransform rect, float y, float h, float margin = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(-2f * margin, h);
            return rect;
        }

        /// <summary>A rect spanning the parent's width, `bottom` up from its bottom edge.</summary>
        public static RectTransform PlaceBottomRow(RectTransform rect, float bottom, float h, float margin = 0f)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottom);
            rect.sizeDelta = new Vector2(-2f * margin, h);
            return rect;
        }

        /// <summary>A flat coloured box.</summary>
        public static Image Box(Transform parent, string name, Color color)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A nine-sliced theme texture; a flat box in the theme's muted colour when the sprite is missing.</summary>
        public static Image Sliced(Transform parent, string name, Sprite sprite, Color color)
        {
            var image = Box(parent, name, color);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }
            return image;
        }

        /// <summary>The stone panel texture, as a backing for a box of text.</summary>
        public static Image Panel(Transform parent, string name) =>
            Sliced(parent, name, UiTheme.Current.Panel, Color.white);

        /// <summary>The see-through red over the game behind a menu.</summary>
        public static Image Dim(Transform parent)
        {
            var image = Box(parent, "Dim", UiTheme.Dim);
            Fill(image.rectTransform);
            return image;
        }

        /// <summary>A line of text in the theme font at a pixel size, with an optional drop shadow.</summary>
        public static TextMeshProUGUI Label(
            Transform parent,
            string name,
            string text,
            int size,
            Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left,
            bool shadow = false
        )
        {
            var rect = Rect(parent, name);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = UiTheme.Current.FontAsset;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = true;
            label.raycastTarget = false;
            label.text = text;
            if (shadow)
            {
                AddShadow(label);
            }
            return label;
        }

        /// <summary>A label that wraps inside its rect.</summary>
        public static TextMeshProUGUI Paragraph(
            Transform parent,
            string name,
            string text,
            int size,
            Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left
        )
        {
            var label = Label(parent, name, text, size, color, align);
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        /// <summary>A heading in the theme's heading colour with its dark outline.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string text, int size = UiTheme.HeadingSize)
        {
            var label = Label(parent, "Heading", text, size, UiTheme.Heading, TextAlignmentOptions.Center);
            label.outlineColor = UiTheme.HeadingOutline;
            label.outlineWidth = 0.2f;
            return label;
        }

        public static void AddShadow(Graphic graphic)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UiTheme.Shadow;
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        /// <summary>
        /// A stone button: the normal texture, the focus texture while selected,
        /// pressed or hovered, the disabled one when it cannot be used, and a label
        /// whose colour follows the focus (<see cref="UiButtonLook"/>).
        /// </summary>
        public static Button Button(
            Transform parent,
            string name,
            string text,
            UnityEngine.Events.UnityAction onClick = null,
            TextAlignmentOptions align = TextAlignmentOptions.Center
        )
        {
            var theme = UiTheme.Current;
            var image = Sliced(parent, name, theme.ButtonNormal, Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (theme.ButtonNormal != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = theme.ButtonFocus,
                    pressedSprite = theme.ButtonFocus,
                    selectedSprite = theme.ButtonFocus,
                    disabledSprite = theme.ButtonDisabled,
                };
            }
            else
            {
                button.transition = Selectable.Transition.ColorTint;
            }
            var label = Label(image.transform, "Label", text, UiTheme.FontSize, UiTheme.Text, align);
            Fill(label.rectTransform, UiTheme.ButtonPadding, 0f, UiTheme.ButtonPadding, 0f);
            label.alignment =
                align == TextAlignmentOptions.Left ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            var look = image.gameObject.AddComponent<UiButtonLook>();
            look.Bind(button, label);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            button.navigation = navigation;
            return button;
        }

        /// <summary>The text of a button made by <see cref="Button"/>.</summary>
        public static TextMeshProUGUI LabelOf(Selectable selectable) =>
            selectable.GetComponentInChildren<TextMeshProUGUI>();

        public static void SetText(Selectable selectable, string text)
        {
            var label = LabelOf(selectable);
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>Links a column of selectables up and down, wrapping at the ends; left and right are left alone.</summary>
        public static void LinkVertical(IReadOnlyList<Selectable> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var navigation = items[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = items[(i - 1 + items.Count) % items.Count];
                navigation.selectOnDown = items[(i + 1) % items.Count];
                items[i].navigation = navigation;
            }
        }

        /// <summary>Links two columns side by side: left and right cross between them at the same row.</summary>
        public static void LinkColumns(IReadOnlyList<Selectable> left, IReadOnlyList<Selectable> right)
        {
            LinkVertical(left);
            LinkVertical(right);
            for (var i = 0; i < left.Count; i++)
            {
                var navigation = left[i].navigation;
                navigation.selectOnRight = right[Mathf.Min(i, right.Count - 1)];
                left[i].navigation = navigation;
            }
            for (var i = 0; i < right.Count; i++)
            {
                var navigation = right[i].navigation;
                navigation.selectOnLeft = left[Mathf.Min(i, left.Count - 1)];
                right[i].navigation = navigation;
            }
        }

        /// <summary>Links a list to a back button: down from the last row and up from the first both reach it.</summary>
        public static void LinkListToBack(IReadOnlyList<Selectable> rows, Selectable back)
        {
            if (rows.Count == 0)
            {
                return;
            }
            LinkVertical(rows);
            var first = rows[0].navigation;
            first.selectOnUp = back;
            rows[0].navigation = first;
            var last = rows[rows.Count - 1].navigation;
            last.selectOnDown = back;
            rows[rows.Count - 1].navigation = last;
            var backNav = back.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnUp = rows[rows.Count - 1];
            backNav.selectOnDown = rows[0];
            back.navigation = backNav;
        }

        /// <summary>
        /// A vertical list that scrolls to the focused row: a masked viewport with a
        /// content column that grows with its rows (<see cref="UiScrollList"/>).
        /// </summary>
        public static UiScrollList ScrollList(Transform parent, string name)
        {
            var rect = Rect(parent, name);
            var mask = rect.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = RowHeight;
            var content = Rect(rect, "List");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowGap;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = rect;
            var list = rect.gameObject.AddComponent<UiScrollList>();
            list.Bind(scroll, content);
            return list;
        }

        /// <summary>Destroys every child of a transform.</summary>
        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }
        }

        /// <summary>Gives a selectable the focus, as grab_focus did.</summary>
        public static void Select(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null)
            {
                return;
            }
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        /// <summary>Whether a selectable has the focus.</summary>
        public static bool IsSelected(Selectable selectable) =>
            selectable != null
            && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == selectable.gameObject;
    }
}
