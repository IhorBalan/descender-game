using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Builders for the menu screens: a 320x180 overlay canvas, labels, stone
    /// buttons that swap sprites on focus, panels, columns, sliders and toggles,
    /// all from a <see cref="MenuTheme"/>. The screens are built in code once, on
    /// first open, so no prefab has to be authored by hand. Unity's legacy Text is
    /// used because it draws straight from the .ttf without TextMeshPro's imported
    /// resources.
    /// </summary>
    public static class MenuKit
    {
        public const int ReferenceWidth = 320;
        public const int ReferenceHeight = 180;
        public const float ButtonHeight = 13f;

        /// <summary>A screen's canvas: Screen Space - Overlay, scaled from 320x180 so it always fits.</summary>
        public static Canvas MakeCanvas(Transform parent, string name, int sortingOrder)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            holder.AddComponent<GraphicRaycaster>();
            holder.transform.SetParent(parent, false);
            var canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
            var scaler = holder.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }

        public static RectTransform MakeRect(Transform parent, string name)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            return holder.GetComponent<RectTransform>();
        }

        /// <summary>Fills the parent.</summary>
        public static RectTransform Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
            return rect;
        }

        /// <summary>Places a rect by one anchor (0..1 in both axes), pivot at the same point, size in pixels.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>A strip along the top or bottom edge: full width, the given height, y from that edge.</summary>
        public static RectTransform Strip(RectTransform rect, bool top, float fromEdge, float height)
        {
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.anchoredPosition = new Vector2(0f, top ? -fromEdge : fromEdge);
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

        public static Text MakeLabel(
            Transform parent,
            MenuTheme theme,
            string text,
            int size = MenuTheme.BaseFontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter
        )
        {
            var holder = new GameObject("Label", typeof(RectTransform), typeof(Text));
            holder.transform.SetParent(parent, false);
            var label = holder.GetComponent<Text>();
            label.font = theme.font;
            label.fontSize = size;
            label.text = text;
            label.color = theme.text;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A heading with the theme's dark outline, as the Godot Heading and Title variations.</summary>
        public static Text MakeHeading(Transform parent, MenuTheme theme, string text, int size)
        {
            var label = MakeLabel(parent, theme, text, size);
            label.color = theme.heading;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.outline;
            outline.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, bool sliced = false)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            var image = holder.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            if (sprite == null)
            {
                image.color = new Color(0.12f, 0.08f, 0.09f, 0.95f);
            }
            return image;
        }

        /// <summary>The stone panel, nine-sliced.</summary>
        public static Image MakePanel(Transform parent, MenuTheme theme) =>
            MakeImage(parent, "Panel", theme.panel, true);

        /// <summary>A column of controls, top to bottom, that sizes its children to its width.</summary>
        public static VerticalLayoutGroup MakeColumn(Transform parent, string name, float spacing = 1f)
        {
            var rect = MakeRect(parent, name);
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        /// <summary>A row of controls, left to right, each sized by its own preferred width.</summary>
        public static HorizontalLayoutGroup MakeRow(Transform parent, string name, float spacing = 3f)
        {
            var rect = MakeRect(parent, name);
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return group;
        }

        public static LayoutElement Size(Component target, float width = -1f, float height = ButtonHeight)
        {
            var element = target.gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.gameObject.AddComponent<LayoutElement>();
            }
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
            return element;
        }

        /// <summary>A stone button whose sprite swaps when focused, hovered or disabled, with a text in it.</summary>
        public static Button MakeButton(
            Transform parent,
            MenuTheme theme,
            string text,
            UnityEngine.Events.UnityAction onClick,
            TextAnchor alignment = TextAnchor.MiddleCenter
        )
        {
            var holder = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);
            var image = holder.GetComponent<Image>();
            image.sprite = theme.buttonNormal;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var button = holder.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = theme.buttonFocus,
                selectedSprite = theme.buttonFocus,
                pressedSprite = theme.buttonFocus,
                disabledSprite = theme.buttonDisabled,
            };
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            var label = MakeLabel(holder.transform, theme, text, MenuTheme.BaseFontSize, alignment);
            var rect = Stretch(label.rectTransform);
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
            var tint = holder.AddComponent<ButtonTint>();
            tint.Bind(label, theme);
            Size(button);
            return button;
        }

        public static void SetText(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>A volume slider from 0 to 100 in whole steps, with the theme's track, fill and grabber.</summary>
        public static Slider MakeSlider(
            Transform parent,
            MenuTheme theme,
            UnityEngine.Events.UnityAction<float> onChanged
        )
        {
            var holder = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            holder.transform.SetParent(parent, false);
            var slider = holder.GetComponent<Slider>();
            var track = MakeImage(holder.transform, "Track", null);
            track.color = theme.sliderTrack;
            var trackRect = Stretch(track.rectTransform);
            trackRect.offsetMin = new Vector2(0f, 4f);
            trackRect.offsetMax = new Vector2(0f, -4f);
            var fillArea = MakeRect(holder.transform, "Fill Area");
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(0f, 4f);
            fillArea.offsetMax = new Vector2(0f, -4f);
            var fill = MakeImage(fillArea, "Fill", null);
            fill.color = theme.sliderFill;
            Stretch(fill.rectTransform);
            var handleArea = MakeRect(holder.transform, "Handle Slide Area");
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(3f, 0f);
            handleArea.offsetMax = new Vector2(-3f, 0f);
            var handle = MakeImage(handleArea, "Handle", theme.grabber);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(6f, 0f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            if (onChanged != null)
            {
                slider.onValueChanged.AddListener(onChanged);
            }
            Size(slider, 90f, 11f);
            return slider;
        }

        /// <summary>An On/Off button with the toggle icon on its left; the caller flips the state on click.</summary>
        public static Button MakeToggle(Transform parent, MenuTheme theme, UnityEngine.Events.UnityAction onClick)
        {
            var button = MakeButton(parent, theme, "Off", onClick, TextAnchor.MiddleLeft);
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            var icon = MakeImage(button.transform, "Icon", theme.toggleOff);
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(12f, 8f));
            Size(button, 50f);
            return button;
        }

        public static void SetToggle(Button toggle, MenuTheme theme, bool on)
        {
            SetText(toggle, on ? "On" : "Off");
            var icon = toggle.transform.Find("Icon");
            if (icon != null && icon.TryGetComponent<Image>(out var image))
            {
                image.sprite = on ? theme.toggleOn : theme.toggleOff;
            }
        }

        /// <summary>Links the selectables up and down in order, so the arrows and the stick walk the list.</summary>
        public static void LinkVertical(IList<Selectable> items, bool wrap = true)
        {
            var count = items.Count;
            for (var i = 0; i < count; i++)
            {
                var navigation = items[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = i > 0 ? items[i - 1] : (wrap ? items[count - 1] : null);
                navigation.selectOnDown = i < count - 1 ? items[i + 1] : (wrap ? items[0] : null);
                items[i].navigation = navigation;
            }
        }

        /// <summary>Links the selectables left and right in order.</summary>
        public static void LinkHorizontal(IList<Selectable> items, bool wrap = true)
        {
            var count = items.Count;
            for (var i = 0; i < count; i++)
            {
                var navigation = items[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = i > 0 ? items[i - 1] : (wrap ? items[count - 1] : null);
                navigation.selectOnRight = i < count - 1 ? items[i + 1] : (wrap ? items[0] : null);
                items[i].navigation = navigation;
            }
        }

        /// <summary>The first selectable in a list that can take the focus, or null.</summary>
        public static Selectable FirstEnabled(IList<Selectable> items)
        {
            foreach (var item in items)
            {
                if (item != null && item.interactable && item.gameObject.activeInHierarchy)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>The hint line along the bottom, for the keys or the pad buttons the player last used.</summary>
        public static string Hint(bool gamepad, string moveDoes, string acceptDoes, string cancelDoes)
        {
            return gamepad
                ? $"D-pad {moveDoes}   A {acceptDoes}   B {cancelDoes}"
                : $"Arrows {moveDoes}   Enter {acceptDoes}   Esc {cancelDoes}";
        }
    }
}
