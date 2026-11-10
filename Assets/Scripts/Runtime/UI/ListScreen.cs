using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The shape the pack, the shop and the skills share, in the title's gilded look:
    /// a heading over a jewelled rule, two lines under it, a scrolling list of rows
    /// that light a gold bar while focused, a description line that follows the
    /// focused row, a back item and the key hints along the bottom, on the 1920 x 1080
    /// stage. Standing alone it veils the game; inside the camp it lies over the
    /// camp's ground. A derived screen fills the list in <see cref="Refresh"/> and
    /// names its texts.
    /// </summary>
    public abstract class ListScreen : UiScreen
    {
        private const float Middle = TitleKit.Width * 0.5f;
        private const float HeadingY = 150f;
        private const float RuleY = 212f;
        private const float LineOneY = 262f;
        private const float LineTwoY = 298f;
        private const float ListTop = 340f;
        private const float ListBottom = 820f;
        private const float ListWidth = 1100f;
        private const float DescriptionY = 866f;
        private const float BackY = 930f;

        private RectTransform keys;
        private TitleList backList;
        private CanvasGroup stageGroup;
        private int keysFor = -1;
        private float openedAt;

        protected TextMeshProUGUI Heading { get; private set; }
        protected TextMeshProUGUI LineOne { get; private set; }
        protected TextMeshProUGUI LineTwo { get; private set; }
        protected UiScrollList List { get; private set; }
        protected TextMeshProUGUI Empty { get; private set; }
        protected TextMeshProUGUI Description { get; private set; }
        protected Button Back { get; private set; }

        /// <summary>The stage the page is laid out on, 1920 x 1080.</summary>
        protected RectTransform Stage { get; private set; }

        protected abstract string HeadingText { get; }
        protected abstract string BackText { get; }

        /// <summary>What confirming does on a row, for the key hints ("Wear or Use", "Buy").</summary>
        protected abstract string ConfirmText { get; }

        /// <summary>The line shown in the middle when the list is empty, or "" for none.</summary>
        protected virtual string EmptyText => "";

        /// <summary>Whether the game is veiled behind the list (a page inside the camp lies on the camp's ground).</summary>
        protected virtual bool Dims => true;

        /// <summary>The rows shown, in order, for a test.</summary>
        public System.Collections.Generic.IReadOnlyList<Selectable> RowsShown => List.Rows;

        protected static MenuTheme Theme => UiTheme.Current.MenuTheme;

        protected override void Build(RectTransform root)
        {
            var theme = Theme;
            if (Dims)
            {
                Stage = TitleHints.MakeStage(root, theme, true, 31, out stageGroup);
            }
            else
            {
                // Inside the camp, whose ground is already down: no second ground, no second embers.
                TitleScreen.ScaleSmooth(root.GetComponent<Canvas>());
                Stage = UiKit.Rect(root, "Stage");
                Stage.anchorMin = new Vector2(0.5f, 0.5f);
                Stage.anchorMax = new Vector2(0.5f, 0.5f);
                Stage.pivot = new Vector2(0.5f, 0.5f);
                Stage.sizeDelta = new Vector2(TitleKit.Width, TitleKit.Height);
                stageGroup = Stage.gameObject.AddComponent<CanvasGroup>();
            }
            if (TitleKit.Font == null && theme != null)
            {
                TitleKit.Font = theme.titleFont;
            }
            Heading = TitleKit.Label(Stage, HeadingText, 51f, TitleKit.Gold, TitleKit.CapsSpacing, Middle, HeadingY);
            if (theme != null)
            {
                TitleKit.Sprite(Stage, "Rule", theme.ruleTagline, Middle, RuleY);
            }
            LineOne = TitleKit.Label(Stage, "", 21f, TitleKit.Muted, 6f, Middle, LineOneY, 1400f);
            LineTwo = TitleKit.Label(Stage, "", 21f, TitleKit.Parchment, 6f, Middle, LineTwoY, 1400f);

            List = UiKit.ScrollList(Stage, "Scroll");
            var scroll = List.GetComponent<RectTransform>();
            TitleKit.Center(scroll, Middle, (ListTop + ListBottom) * 0.5f, ListWidth, ListBottom - ListTop);
            List.GetComponent<ScrollRect>().scrollSensitivity = UiScrollList.RowHeight;
            Empty = TitleKit.Label(Stage, EmptyText, 24f, TitleKit.Faded, 4f, Middle, (ListTop + ListBottom) * 0.5f);
            Empty.gameObject.SetActive(false);

            Description = TitleKit.Label(Stage, "", 21f, TitleKit.Parchment, 4f, Middle, DescriptionY, 1300f);
            Description.textWrappingMode = TextWrappingModes.Normal;
            Description.rectTransform.sizeDelta = new Vector2(1300f, 70f);

            backList = new TitleList(
                Stage,
                "Back",
                BackY,
                820f,
                theme != null ? theme.focusBar : null,
                theme != null ? theme.ruleItem : null
            );
            Back = backList.Add(BackText, Close, true, TitleList.BackSound);
            backList.Layout();
            var focus = TitleFocus.Make(Stage, null);
            focus.Follow(backList, false);
            Back.gameObject.AddComponent<UiButtonLook>().Focused += () => ShowNote("", UiTheme.Note);

            keys = UiKit.Rect(Stage, "Keys");
            UiKit.Fill(keys);
        }

        protected override void OnOpen()
        {
            openedAt = Time.unscaledTime;
            stageGroup.alpha = 0f;
            keysFor = -1;
            Refresh();
            ShowNote("", UiTheme.Note);
            if (!List.FocusNear(0))
            {
                UiKit.Select(Back);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen)
            {
                return;
            }
            stageGroup.alpha = TitleScreen.Smooth((Time.unscaledTime - openedAt) / 0.3f);
            var router = Input.InputRouter.Instance;
            var gamepad = router != null && router.UsingGamepad ? 1 : 0;
            if (keysFor != gamepad && Theme != null)
            {
                keysFor = gamepad;
                TitleHints.Build(keys, Theme, gamepad == 1, ConfirmText, BackText);
            }
        }

        /// <summary>Rebuilds the rows and the lines over them, keeping the focus where it was.</summary>
        public void Refresh()
        {
            EnsureBuilt();
            var focused = List.FocusedIndex();
            List.Clear();
            FillList();
            UiKit.LinkListToBack(List.Rows, Back);
            if (Empty != null)
            {
                Empty.gameObject.SetActive(EmptyText.Length > 0 && List.RowCount == 0);
            }
            if (IsOpen && focused >= 0 && !List.FocusNear(focused))
            {
                UiKit.Select(Back);
            }
        }

        /// <summary>Adds the rows for the state right now, and writes the lines over them.</summary>
        protected abstract void FillList();

        /// <summary>The texts of the rows shown, their columns joined by three spaces, for a test.</summary>
        public System.Collections.Generic.List<string> Lines()
        {
            EnsureBuilt();
            var result = new System.Collections.Generic.List<string>();
            foreach (var row in List.Rows)
            {
                var texts = new System.Collections.Generic.List<string>();
                foreach (var label in row.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    texts.Add(label.text);
                }
                result.Add(string.Join("   ", texts));
            }
            return result;
        }

        protected void ShowNote(string text, Color color)
        {
            if (Description == null)
            {
                return;
            }
            Description.text = text;
            Description.color = color;
        }
    }
}
