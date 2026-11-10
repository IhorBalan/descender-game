using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// One line of the settings screen, a button laid out on the 1920 x 1080 stage:
    /// a section in the left column (icon and name), or a setting in the middle one
    /// with its name on the left and its value on the right, drawn by kind: a value
    /// between two chevrons, an On/Off switch, a slider with its number, or the keys
    /// bound to an action. The screen gives it what to show and what to do
    /// (<see cref="Bind"/>); the row draws itself in <see cref="Refresh"/>, lights its
    /// text while focused, moves the focus onto itself when the mouse is over it, and
    /// sets a slider from where it is clicked or dragged.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class SettingsRow : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IDragHandler
    {
        public enum Kind
        {
            Section,
            Cycle,
            Toggle,
            Slider,
            Key,
        }

        public const float RowWidth = 820f;
        public const float SectionWidth = 340f;
        public const float TextSize = 22f;
        public const float SectionTextSize = 24f;

        // Where things sit inside a setting row, from its left edge.
        private const float LabelLeft = 26f;
        private const float ValueRight = 795f;
        private const float ChevronLeftX = 560f;
        private const float ChevronRightX = 785f;
        private const float CycleValueX = 672f;
        private const float ToggleX = 760f;
        private const float SliderX = 620f;

        private Button button;
        private TextMeshProUGUI label;
        private TextMeshProUGUI value;
        private Image icon;
        private Image chevronLeft;
        private Image chevronRight;
        private Image toggle;
        private RectTransform track;
        private Image fill;
        private RectTransform handle;
        private SettingsLook look;
        private System.Func<string> text;
        private System.Func<float> amount;
        private System.Action<int> step;
        private System.Action<float> set;

        public Kind Type { get; private set; }

        public string Label { get; private set; }

        /// <summary>The line the description panel shows while this row is focused.</summary>
        public string Help { get; private set; }

        /// <summary>A section row is lit while its section is the one on show.</summary>
        public bool Current { get; set; }

        /// <summary>The value as shown, for tests: "On", "Normal", "70", the key names.</summary>
        public string ValueText => value != null ? value.text : "";

        /// <summary>Whether left and right change this row's value.</summary>
        public bool Adjusts => Type == Kind.Cycle || Type == Kind.Toggle || Type == Kind.Slider;

        /// <summary>Makes a row of `kind` under `parent`, its text set but not yet bound.</summary>
        public static SettingsRow Make(
            Transform parent,
            SettingsLook look,
            Kind kind,
            string label,
            string help,
            Sprite icon = null
        )
        {
            var width = kind == Kind.Section ? SectionWidth : RowWidth;
            var rect = UiKit.Rect(parent, label);
            rect.sizeDelta = new Vector2(width, TitleKit.Step);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            var row = rect.gameObject.AddComponent<SettingsRow>();
            row.button = button;
            row.look = look;
            row.Type = kind;
            row.Label = label;
            row.Help = help;
            row.Build(icon);
            return row;
        }

        /// <summary>
        /// What the row shows and does: `text` gives its value's words, `amount` a
        /// toggle's state (above a half is on) or a slider's place (0..1), `step`
        /// moves the value one way or the other, `set` puts a slider at a place.
        /// </summary>
        public void Bind(
            System.Func<string> showText,
            System.Func<float> showAmount,
            System.Action<int> stepBy,
            System.Action<float> setTo = null
        )
        {
            text = showText;
            amount = showAmount;
            step = stepBy;
            set = setTo;
            Refresh();
        }

        public Button Button => button;

        /// <summary>Moves the value one step left (-1) or right (1).</summary>
        public void Step(int direction) => step?.Invoke(direction);

        private void Build(Sprite iconSprite)
        {
            var h = TitleKit.Step;
            if (Type == Kind.Section)
            {
                icon = TitleKit.Sprite(transform, "Icon", iconSprite, 52f, h * 0.5f);
                label = TitleKit.Label(transform, Label, SectionTextSize, TitleKit.Parchment, 2f, 0f, 0f, 260f);
                label.alignment = TextAlignmentOptions.Left;
                TitleKit.Center(label.rectTransform, 92f + 130f, h * 0.5f, 260f, h);
                return;
            }
            label = TitleKit.Label(transform, Label, TextSize, TitleKit.Parchment, 2f, 0f, 0f, 420f);
            label.alignment = TextAlignmentOptions.Left;
            TitleKit.Center(label.rectTransform, LabelLeft + 210f, h * 0.5f, 420f, h);
            switch (Type)
            {
                case Kind.Cycle:
                    chevronLeft = TitleKit.Sprite(transform, "Left", look.chevron, ChevronLeftX, h * 0.5f);
                    chevronLeft.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                    chevronRight = TitleKit.Sprite(transform, "Right", look.chevron, ChevronRightX, h * 0.5f);
                    value = ValueLabel(CycleValueX, 200f, TextAlignmentOptions.Center);
                    break;
                case Kind.Toggle:
                    toggle = TitleKit.Sprite(transform, "Switch", look.toggleOff, ToggleX, h * 0.5f);
                    value = ValueLabel(ToggleX - 48f - 60f, 120f, TextAlignmentOptions.Right);
                    break;
                case Kind.Slider:
                    var trackImage = TitleKit.Sprite(transform, "Track", look.sliderTrack, SliderX, h * 0.5f);
                    track = trackImage.rectTransform;
                    fill = TitleKit.Sprite(transform, "Fill", look.sliderFill, SliderX, h * 0.5f);
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                    handle = TitleKit.Sprite(transform, "Handle", look.sliderHandle, SliderX, h * 0.5f).rectTransform;
                    value = ValueLabel(ValueRight - 30f, 60f, TextAlignmentOptions.Right);
                    break;
                default:
                    value = ValueLabel(ValueRight - 200f, 400f, TextAlignmentOptions.Right);
                    break;
            }
        }

        private TextMeshProUGUI ValueLabel(float cx, float w, TextAlignmentOptions alignment)
        {
            var label = TitleKit.Label(transform, "", TextSize, TitleKit.Parchment, 2f, 0f, 0f, w);
            label.alignment = alignment;
            TitleKit.Center(label.rectTransform, cx, TitleKit.Step * 0.5f, w, TitleKit.Step);
            return label;
        }

        /// <summary>Draws the value as it is now.</summary>
        public void Refresh()
        {
            if (value != null && text != null)
            {
                value.text = text();
            }
            var place = amount != null ? Mathf.Clamp01(amount()) : 0f;
            if (toggle != null)
            {
                toggle.sprite = place > 0.5f ? look.toggleOn : look.toggleOff;
            }
            if (fill != null)
            {
                fill.fillAmount = place;
                // The track sprite has two pixels of glow room at each end.
                var width = TitleKit.SizeOf(look.sliderTrack).x - 4f;
                var left = SliderX - width * 0.5f;
                handle.anchoredPosition = new Vector2(left + width * place, handle.anchoredPosition.y);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.interactable && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        public void OnPointerDown(PointerEventData eventData) => SetFromPointer(eventData);

        public void OnDrag(PointerEventData eventData) => SetFromPointer(eventData);

        /// <summary>A slider goes to the place under the pointer when that is on or near its track.</summary>
        private void SetFromPointer(PointerEventData eventData)
        {
            if (Type != Kind.Slider || set == null || track == null)
            {
                return;
            }
            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    track,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var local
                )
            )
            {
                return;
            }
            var half = track.rect.width * 0.5f - 2f;
            if (eventData.dragging || Mathf.Abs(local.x) <= half + 16f)
            {
                set(Mathf.Clamp01((local.x + half) / (half * 2f)));
                Refresh();
            }
        }

        private void LateUpdate()
        {
            var system = EventSystem.current;
            var focused = system != null && system.currentSelectedGameObject == gameObject;
            var lit = focused || (Type == Kind.Section && Current);
            label.color = lit ? TitleKit.Ivory : TitleKit.Parchment;
            if (icon != null)
            {
                icon.color = lit ? TitleKit.Gold : TitleKit.Muted;
            }
            if (value != null)
            {
                value.color = focused ? TitleKit.Ivory : TitleKit.Parchment;
            }
            if (chevronLeft != null)
            {
                var tint = focused ? TitleKit.Gold : TitleKit.Faded;
                chevronLeft.color = tint;
                chevronRight.color = tint;
            }
        }
    }
}
