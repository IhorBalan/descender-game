using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The shape the pack, the shop and the skills share: a heading, two lines under
    /// it, a scrolling list of rows in the middle, a description line that follows
    /// the focused row, a back button and the hint along the bottom. A derived
    /// screen fills the list in <see cref="Refresh"/> and names its texts. Layout as
    /// scenes/inventory_menu.tscn and its siblings had it, in 320 x 180 pixels.
    /// </summary>
    public abstract class ListScreen : UiScreen
    {
        protected TextMeshProUGUI Heading { get; private set; }
        protected TextMeshProUGUI LineOne { get; private set; }
        protected TextMeshProUGUI LineTwo { get; private set; }
        protected UiScrollList List { get; private set; }
        protected TextMeshProUGUI Empty { get; private set; }
        protected TextMeshProUGUI Description { get; private set; }
        protected Button Back { get; private set; }
        protected PromptLabel Hint { get; private set; }

        protected abstract string HeadingText { get; }
        protected abstract string BackText { get; }
        protected abstract string HintTemplate { get; }

        /// <summary>The line shown in the middle when the list is empty, or "" for none.</summary>
        protected virtual string EmptyText => "";

        /// <summary>Whether the game dims behind the list (a page inside the camp does not).</summary>
        protected virtual bool Dims => true;

        protected override void Build(RectTransform root)
        {
            if (Dims)
            {
                UiKit.Dim(root);
            }
            Heading = UiKit.Heading(root, HeadingText, UiTheme.HeadingSize);
            UiKit.PlaceRow(Heading.rectTransform, 8f, 20f);
            Heading.verticalAlignment = VerticalAlignmentOptions.Middle;
            LineOne = UiKit.Label(
                root,
                "LineOne",
                "",
                UiTheme.FontSize,
                UiTheme.Highlight,
                TextAlignmentOptions.Center
            );
            UiKit.PlaceRow(LineOne.rectTransform, 28f, 10f);
            LineOne.verticalAlignment = VerticalAlignmentOptions.Middle;
            LineTwo = UiKit.Label(root, "LineTwo", "", UiTheme.FontSize, UiTheme.Pale, TextAlignmentOptions.Center);
            UiKit.PlaceRow(LineTwo.rectTransform, 38f, 10f, 16f);
            LineTwo.verticalAlignment = VerticalAlignmentOptions.Middle;
            List = UiKit.ScrollList(root, "Scroll");
            var scroll = List.GetComponent<RectTransform>();
            UiKit.Fill(scroll, 40f, 52f, 40f, 44f);
            Empty = UiKit.Label(
                root,
                "Empty",
                EmptyText,
                UiTheme.FontSize,
                UiTheme.Locked,
                TextAlignmentOptions.Center
            );
            UiKit.PlaceCentered(Empty.rectTransform, UiTheme.Height / 2f - 6f, 200f, 12f);
            Empty.gameObject.SetActive(false);
            Description = UiKit.Paragraph(
                root,
                "Description",
                "",
                UiTheme.FontSize,
                UiTheme.Note,
                TextAlignmentOptions.Center
            );
            UiKit.PlaceBottomRow(Description.rectTransform, 26f, 18f, 16f);
            Description.verticalAlignment = VerticalAlignmentOptions.Middle;
            Back = UiKit.Button(root, "Back", BackText, Close);
            UiKit.PlaceCentered(Back.GetComponent<RectTransform>(), UiTheme.Height - 26f, 60f, UiTheme.ButtonHeight);
            Back.GetComponent<UiButtonLook>().Focused += () => ShowNote("", UiTheme.Note);
            var hint = UiKit.Label(
                root,
                "Hint",
                "",
                UiTheme.SmallFontSize,
                UiTheme.Locked,
                TextAlignmentOptions.Center
            );
            UiKit.PlaceBottomRow(hint.rectTransform, 2f, 8f);
            hint.verticalAlignment = VerticalAlignmentOptions.Middle;
            Hint = hint.gameObject.AddComponent<PromptLabel>();
            Hint.Template = HintTemplate;
        }

        protected override void OnOpen()
        {
            Refresh();
            ShowNote("", UiTheme.Note);
            if (!List.FocusNear(0))
            {
                UiKit.Select(Back);
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

        /// <summary>The texts of the rows shown, for a test.</summary>
        public System.Collections.Generic.List<string> Lines()
        {
            EnsureBuilt();
            var result = new System.Collections.Generic.List<string>();
            foreach (var row in List.Rows)
            {
                result.Add(UiKit.LabelOf(row).text);
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
