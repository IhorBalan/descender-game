using System.Collections.Generic;
using Descender.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.UI
{
    /// <summary>
    /// A dev screen down the right of the view: one group of the hero's tuning numbers
    /// at a time, under its header, with the values that differ from the asset in
    /// yellow and the asset's value beside them. The console's `tune panel` shows and
    /// hides it and `tune panel dash` turns to a group; with the console closed the
    /// bracket keys turn the pages. It only reads: values are set with `tune`.
    /// </summary>
    public sealed class TuningOverlay : MonoBehaviour
    {
        public const int SortOrder = 90;
        public const float PanelWidth = 124f;

        /// <summary>How often the rows are written again, in real seconds.</summary>
        public const float RefreshEvery = 0.25f;
        public const string ChangedColor = "#FFD966";
        public const string PageHint = "[ and ] turn the page";

        private TextMeshProUGUI rows;
        private float refreshLeft;

        /// <summary>Which group is shown, as an index into <see cref="LiveTuning.Groups"/>.</summary>
        public int Group { get; private set; }

        private void Awake()
        {
            var canvas = UiKit.MakeCanvas("TuningCanvas", SortOrder, transform);
            var root = canvas.GetComponent<RectTransform>();
            var panel = UiKit.Box(root, "Panel", DevConsole.Background);
            UiKit.Place(panel.rectTransform, UiTheme.Width - PanelWidth, 0f, PanelWidth, UiTheme.Height);
            var align = TextAlignmentOptions.TopLeft;
            rows = UiKit.Label(panel.transform, "Rows", "", DevConsole.FontSize, DevConsole.TextColor, align);
            UiKit.Fill(rows.rectTransform, 3f, 2f, 2f, 2f);
            rows.overflowMode = TextOverflowModes.Truncate;
            Refresh();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && !DevConsole.AnyOpen)
            {
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                {
                    Turn(Group - 1);
                }
                else if (keyboard.rightBracketKey.wasPressedThisFrame)
                {
                    Turn(Group + 1);
                }
            }
            refreshLeft -= Time.unscaledDeltaTime;
            if (refreshLeft <= 0f)
            {
                Refresh();
            }
        }

        /// <summary>Turns to a group, wrapping round at either end.</summary>
        public void Turn(int group)
        {
            var count = LiveTuning.Groups.Count;
            Group = ((group % count) + count) % count;
            Refresh();
        }

        /// <summary>Turns to the first group whose header starts with the text; false when none does.</summary>
        public bool TurnTo(string text)
        {
            var index = GroupStarting(text);
            if (index < 0)
            {
                return false;
            }
            Turn(index);
            return true;
        }

        /// <summary>The index of the first group whose header starts with the text, or -1.</summary>
        public static int GroupStarting(string text)
        {
            var groups = LiveTuning.Groups;
            for (var i = 0; i < groups.Count; i++)
            {
                if (text.Length > 0 && groups[i].StartsWith(text, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void Refresh()
        {
            refreshLeft = RefreshEvery;
            var hero = PlayerController.Current;
            rows.text = Text(hero != null ? hero.Tuning : null, Group);
        }

        /// <summary>
        /// The panel's text for a tuning and a group: the header and page, a row per
        /// field, and the hint. Changed rows are marked only on the live working copy.
        /// </summary>
        public static string Text(PlayerTuning tuning, int group)
        {
            if (tuning == null)
            {
                return TuneCommands.NoHeroText;
            }
            var groups = LiveTuning.Groups;
            var index = Mathf.Clamp(group, 0, groups.Count - 1);
            var live = tuning == LiveTuning.Working;
            var lines = new List<string>();
            lines.Add($"{groups[index]}  {index + 1}/{groups.Count}");
            foreach (var field in LiveTuning.InGroup(groups[index]))
            {
                if (live && LiveTuning.IsChanged(field))
                {
                    lines.Add($"<color={ChangedColor}>* {TuneCommands.Value(field)}</color>");
                }
                else
                {
                    lines.Add($"  {field.Name} {LiveTuning.Read(tuning, field)}");
                }
            }
            lines.Add(PageHint);
            return string.Join("\n", lines);
        }
    }
}
