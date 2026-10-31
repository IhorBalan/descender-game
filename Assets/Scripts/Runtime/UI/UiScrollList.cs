using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The scrolling column the pack, the shop and the skills list their rows in.
    /// Rows are stone buttons or plain lines added in order; the list keeps the
    /// focused row in view (Godot's follow_focus) and can put the focus back on
    /// the row that had it after a rebuild. Made by <see cref="UiKit.ScrollList"/>.
    /// </summary>
    public sealed class UiScrollList : MonoBehaviour
    {
        private ScrollRect scroll;
        private RectTransform content;
        private readonly List<Selectable> rows = new List<Selectable>();
        private GameObject lastSelected;

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

        /// <summary>A left-aligned stone button row in a colour.</summary>
        public Button AddRow(string text, Color color, UnityEngine.Events.UnityAction onPress, System.Action onFocus)
        {
            var button = UiKit.Button(content, $"Row{rows.Count}", text, onPress, TMPro.TextAlignmentOptions.Left);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = UiKit.RowHeight;
            layout.minHeight = UiKit.RowHeight;
            var look = button.GetComponent<UiButtonLook>();
            look.SetBaseColor(color);
            if (onFocus != null)
            {
                look.Focused += onFocus;
            }
            rows.Add(button);
            return button;
        }

        /// <summary>A plain line of text between the rows.</summary>
        public void AddLine(string text, Color color)
        {
            var label = UiKit.Label(content, "Line", text, UiTheme.FontSize, color);
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = UiTheme.FontSize + 2f;
            layout.minHeight = UiTheme.FontSize + 2f;
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
