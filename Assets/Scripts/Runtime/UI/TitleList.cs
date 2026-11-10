using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// One column of title screen items on the stage: the items one
    /// <see cref="TitleKit.Step"/> apart from a top line, a short gold rule between
    /// each two that are showing, and the focus bar sprite this column lights its
    /// focused item with. The rules sit in a layer under the bar, the items over it.
    /// </summary>
    public sealed class TitleList
    {
        private readonly List<Button> items = new List<Button>();
        private readonly List<Image> rules = new List<Image>();
        private readonly RectTransform ruleLayer;
        private readonly RectTransform itemLayer;
        private readonly Sprite ruleSprite;
        private readonly float width;
        private readonly float top;

        public TitleList(Transform stage, string name, float top, float width, Sprite bar, Sprite rule)
        {
            Root = UiKit.Rect(stage, name);
            UiKit.Fill(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            ruleLayer = UiKit.Rect(Root, "Rules");
            UiKit.Fill(ruleLayer);
            itemLayer = UiKit.Rect(Root, "Items");
            UiKit.Fill(itemLayer);
            this.top = top;
            this.width = width;
            Bar = bar;
            ruleSprite = rule;
        }

        public RectTransform Root { get; }

        /// <summary>Fades the column in when it is shown.</summary>
        public CanvasGroup Group { get; }

        /// <summary>The focus bar this column uses.</summary>
        public Sprite Bar { get; }

        /// <summary>Where the focus bar goes among the column's children: over the rules, under the items.</summary>
        public int BarSiblingIndex => ruleLayer.GetSiblingIndex() + 1;

        /// <summary>The items in order, hidden ones included.</summary>
        public IReadOnlyList<Button> Items => items;

        /// <summary>The sound an item that goes back plays; the others play <see cref="ConfirmSound"/>.</summary>
        public const string BackSound = "menu_back";

        public const string ConfirmSound = "menu_confirm";

        /// <summary>Adds an item at the end, in spaced capitals unless `caps` is false, that plays `sound` when pressed.</summary>
        public Button Add(
            string text,
            UnityEngine.Events.UnityAction onClick,
            bool caps = true,
            string sound = ConfirmSound
        )
        {
            var button = TitleKit.Item(itemLayer, text, null, width, caps);
            button.onClick.AddListener(() => Audio.Sfx.Play(sound, -3f));
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            items.Add(button);
            return button;
        }

        /// <summary>Whether a selected object is one of this column's items.</summary>
        public bool Contains(GameObject selected)
        {
            if (selected == null)
            {
                return false;
            }
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].gameObject == selected)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The middle line of an item, in pixels from the stage's top.</summary>
        public float MiddleOf(GameObject item) =>
            -((RectTransform)item.transform).anchoredPosition.y + TitleKit.Step / 2;

        /// <summary>
        /// Puts the showing items one step apart from the top, centred on the stage,
        /// with a rule between each two, and returns them in order for linking.
        /// </summary>
        public List<Selectable> Layout()
        {
            var shown = new List<Selectable>();
            var x = Mathf.Floor((UiTheme.Width - width) * 0.5f);
            foreach (var item in items)
            {
                if (!item.gameObject.activeSelf)
                {
                    continue;
                }
                var y = top + shown.Count * TitleKit.Step - TitleKit.Step / 2;
                UiKit.Place((RectTransform)item.transform, x, y, width, TitleKit.Step);
                shown.Add(item);
            }
            for (var i = rules.Count; i < shown.Count - 1; i++)
            {
                rules.Add(TitleKit.Sprite(ruleLayer, "Rule", ruleSprite, UiTheme.Width, 0f));
            }
            for (var i = 0; i < rules.Count; i++)
            {
                var visible = i < shown.Count - 1;
                rules[i].gameObject.SetActive(visible);
                if (visible)
                {
                    var rect = rules[i].rectTransform;
                    var h = rect.sizeDelta.y;
                    // Between item i and i + 1: half a step under item i's middle.
                    var y = top + i * TitleKit.Step + TitleKit.Step / 2 - Mathf.Floor(h * 0.5f);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
                }
            }
            return shown;
        }
    }
}
