using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The scrolling column the pack, the shop and the skills list their rows in.
    /// Rows are gilded lines of text that light a gold bar while focused, or plain
    /// lines of text, added in order (<see cref="RowHeight"/> apart); the list keeps the
    /// focused row in view (Godot's follow_focus) and can put the focus back on
    /// the row that had it after a rebuild. Made by <see cref="UiKit.ScrollList"/>.
    /// </summary>
    public sealed class UiScrollList : MonoBehaviour
    {
        private ScrollRect scroll;
        private RectTransform content;
        private readonly List<Selectable> rows = new List<Selectable>();
        private GameObject lastSelected;

        /// <summary>The height of a row, in pixels of the 1920 x 1080 stage.</summary>
        public const float RowHeight = 58f;

        public const float TextSize = 24f;

        public RectTransform Content => content;
        public IReadOnlyList<Selectable> Rows => rows;
        public int RowCount => rows.Count;

        public void Bind(ScrollRect scrollRect, RectTransform column)
        {
            scroll = scrollRect;
            content = column;
        }

        /// <summary>The index of the row with the focus, or -1.</summary>
        public int FocusedIndex()
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (UiKit.IsSelected(rows[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Removes every row and line.</summary>
        public void Clear()
        {
            rows.Clear();
            UiKit.Clear(content);
        }

        /// <summary>
        /// A row: its text in a colour, and the wide gold bar behind it while it is
        /// focused or under the mouse. A text laid out in columns with runs of spaces
        /// (MenuText's lines) keeps its first column on the left and sets the rest
        /// against the right end, since the spaces do not line up in Cinzel.
        /// </summary>
        public Button AddRow(string text, Color color, UnityEngine.Events.UnityAction onPress, System.Action onFocus)
        {
            var rect = UiKit.Rect(content, $"Row{rows.Count}");
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            if (onPress != null)
            {
                button.onClick.AddListener(onPress);
            }
            var theme = UiTheme.Current.MenuTheme;
            var bar = UiKit.Box(rect, "Bar", Color.white);
            bar.sprite = theme != null ? theme.focusBarWide : null;
            UiKit.Fill(bar.rectTransform, 0f, -7f, 0f, -7f);
            bar.enabled = false;
            SplitColumns(text, out var name, out var detail);
            var label = TitleKit.Label(rect, name, TextSize, color, 3f, 0f, 0f);
            label.alignment = TMPro.TextAlignmentOptions.Left;
            UiKit.Fill(label.rectTransform, 48f, 0f, 48f, 0f);
            if (detail.Length > 0)
            {
                var right = TitleKit.Label(rect, detail, 21f, TitleKit.Muted, 3f, 0f, 0f);
                right.alignment = TMPro.TextAlignmentOptions.Right;
                UiKit.Fill(right.rectTransform, 48f, 0f, 48f, 0f);
            }
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = RowHeight;
            layout.minHeight = RowHeight;
            var look = rect.gameObject.AddComponent<UiButtonLook>();
            look.BindGilded(button, label, bar);
            look.SetBaseColor(color);
            button.onClick.AddListener(() => Audio.Sfx.Play(TitleList.ConfirmSound, -3f));
            if (onFocus != null)
            {
                look.Focused += onFocus;
            }
            rows.Add(button);
            return button;
        }

        /// <summary>Splits a line at its first run of three or more spaces: the first column, and the rest.</summary>
        public static void SplitColumns(string text, out string first, out string rest)
        {
            var parts = System.Text.RegularExpressions.Regex.Split(text.TrimEnd(), " {3,}");
            first = parts[0];
            rest = parts.Length > 1 ? string.Join("   ", parts, 1, parts.Length - 1) : "";
        }

        /// <summary>A plain line of text between the rows.</summary>
        public void AddLine(string text, Color color)
        {
            var label = TitleKit.Label(content, text, 21f, color, 3f, 0f, 0f);
            label.alignment = TMPro.TextAlignmentOptions.Left;
            label.margin = new Vector4(48f, 0f, 48f, 0f);
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 40f;
            layout.minHeight = 40f;
        }

        /// <summary>Puts the focus on a row by index, clamped to the rows there are; false when there is none.</summary>
        public bool FocusNear(int index)
        {
            if (rows.Count == 0)
            {
                return false;
            }
            UiKit.Select(rows[Mathf.Clamp(index, 0, rows.Count - 1)]);
            return true;
        }

        private void Update()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelected || selected == null)
            {
                return;
            }
            lastSelected = selected;
            if (selected.transform.parent == content)
            {
                ScrollTo(selected.GetComponent<RectTransform>());
            }
        }

        /// <summary>Scrolls just far enough to show a row.</summary>
        public void ScrollTo(RectTransform row)
        {
            if (row == null || scroll == null)
            {
                return;
            }
            Canvas.ForceUpdateCanvases();
            var viewHeight = ((RectTransform)scroll.viewport).rect.height;
            var contentHeight = content.rect.height;
            if (contentHeight <= viewHeight)
            {
                scroll.verticalNormalizedPosition = 1f;
                return;
            }
            var rowTop = -row.anchoredPosition.y + row.rect.height * (1f - row.pivot.y);
            var rowBottom = rowTop + row.rect.height;
            var scrolled = content.anchoredPosition.y;
            if (rowTop < scrolled)
            {
                scrolled = rowTop;
            }
            else if (rowBottom > scrolled + viewHeight)
            {
                scrolled = rowBottom - viewHeight;
            }
            var range = contentHeight - viewHeight;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(scrolled / range);
        }
    }
}
