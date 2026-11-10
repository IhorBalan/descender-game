using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The developer console: a panel over the top of the screen where commands are
    /// typed, for debugging and playtesting. The grave key (`) opens and closes it;
    /// Escape closes it too. The game stands still while it is open and resumes when
    /// it closes (a menu that was already open stays paused). Commands live in
    /// <see cref="Commands"/> so other scripts can add their own; the built-ins are
    /// `help`, `clear` and `show_boxes on|off`, which draws every collider over the
    /// game (see <see cref="CollisionBoxes"/>). Arrow up and down walk the history.
    /// Port of scripts/dev_console.gd; lives in Main.unity.
    /// </summary>
    public sealed class DevConsole : MonoBehaviour
    {
        public const int SortOrder = 100;
        public const string Prompt = "> ";
        public const int HistoryLimit = 50;

        /// <summary>The panel's share of the screen height.</summary>
        public const float HeightFraction = 0.55f;
        public const int FontSize = 8;
        public const float LineHeight = 9f;

        public static readonly Color Background = new Color(0.04f, 0.03f, 0.08f, 0.92f);
        public static readonly Color Border = new Color(0.35f, 0.3f, 0.55f);
        public static readonly Color FieldColor = new Color(0.1f, 0.08f, 0.2f);
        public static readonly Color TextColor = new Color(0.9f, 0.9f, 1f);
        public static readonly Color EchoColor = new Color(0.7f, 0.68f, 0.85f);
        public static readonly Color ErrorColor = new Color(1f, 0.45f, 0.4f);
        public static readonly Color MutedColor = new Color(0.55f, 0.52f, 0.7f);

        private Canvas canvas;
        private TextMeshProUGUI output;
        private TMP_InputField input;
        private readonly List<string> lines = new List<string>();
        private readonly List<string> history = new List<string>();
        private int historyIndex;
        private int maxLines = 8;
        private float timeScaleBefore = 1f;
        private bool inputWasUi;
        private CollisionBoxes boxes;
        private TuningOverlay tuning;

        public ConsoleCommands Commands { get; } = new ConsoleCommands();

        public bool IsOpen { get; private set; }

        /// <summary>Whether any console is open, so screens leave Cancel to it.</summary>
        public static bool AnyOpen { get; private set; }

        /// <summary>Whether the collision overlay is on.</summary>
        public bool BoxesShown => boxes != null;

        /// <summary>Whether the tuning panel is on.</summary>
        public bool TuningShown => tuning != null;

        private void Awake()
        {
            Build();
            Commands.Cleared += ClearOutput;
            Commands.Register(
                "show_boxes",
                ShowBoxesCommand,
                "show_boxes on|off",
                "Draws the collision boxes: hero green, enemies red, items blue, walkable world black, hazards orange."
            );
            Commands.Register(
                "lab",
                LabCommand,
                "lab [station|list]",
                "Opens the mechanics lab at a station (the first by default); `lab list` names the stations."
            );
            Commands.Register(
                "tune",
                TuneCommand,
                "tune [field] [value]",
                "Shows or sets the hero's numbers while playing; also `tune list`, `tune reset`, `tune save`, `tune panel`."
            );
            Commands.Register(
                "ability",
                TuneCommands.Ability,
                "ability [name|all] [on|off|reset]",
                "Turns an ability of the hero's on or off until the game closes; a save keeps what he really owns."
            );
        }

        private void Build()
        {
            canvas = UiKit.MakeInteractiveCanvas("ConsoleCanvas", SortOrder, transform);
            var root = canvas.GetComponent<RectTransform>();
            var panelHeight = Mathf.Round(UiTheme.Height * HeightFraction);
            var panel = UiKit.Box(root, "Panel", Background);
            UiKit.PlaceRow(panel.rectTransform, 0f, panelHeight);
            var line = UiKit.Box(panel.transform, "Border", Border);
            UiKit.PlaceBottomRow(line.rectTransform, 0f, 1f);

            var fieldHeight = FontSize + 4f;
            output = UiKit.Label(panel.transform, "Output", "", FontSize, TextColor, TextAlignmentOptions.BottomLeft);
            UiKit.Fill(output.rectTransform, 4f, 2f, 4f, fieldHeight + 3f);
            output.textWrappingMode = TextWrappingModes.Normal;
            output.overflowMode = TextOverflowModes.Truncate;
            maxLines = Mathf.Max(1, (int)((panelHeight - fieldHeight - 5f) / LineHeight));

            var field = UiKit.Box(panel.transform, "Input", FieldColor);
            UiKit.PlaceBottomRow(field.rectTransform, 1f, fieldHeight);
            field.raycastTarget = true;
            var top = UiKit.Box(field.transform, "Top", Border);
            UiKit.PlaceRow(top.rectTransform, 0f, 1f);
            var textArea = UiKit.Rect(field.transform, "TextArea");
            UiKit.Fill(textArea, 3f, 1f, 3f, 1f);
            textArea.gameObject.AddComponent<RectMask2D>();
            var text = UiKit.Label(textArea, "Text", "", FontSize, TextColor);
            UiKit.Fill(text.rectTransform);
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            var placeholder = UiKit.Label(
                textArea,
                "Placeholder",
                "type a command, `help` lists them",
                FontSize,
                MutedColor
            );
            UiKit.Fill(placeholder.rectTransform);
            placeholder.verticalAlignment = VerticalAlignmentOptions.Middle;
            input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = UiTheme.Current.FontAsset;
            input.pointSize = FontSize;
            input.caretColor = TextColor;
            input.customCaretColor = true;
            input.caretBlinkRate = 0.85f;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.onSubmit.AddListener(OnSubmitted);
            canvas.enabled = false;
        }

        private void Update()
        {
            // The grave key is read from the keyboard itself: the Console action sits in
            // the Player map, which a menu turns off, and the console must open anywhere.
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
                if (IsOpen)
                {
                    // The key that opened the console is not typed into it.
                    input.text = "";
                }
                return;
            }
            if (!IsOpen)
            {
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                Recall(historyIndex - 1);
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                Recall(historyIndex + 1);
            }
            else if (!input.isFocused)
            {
                input.ActivateInputField();
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>Shows the console over the game and stops time under it; the player's keys go quiet while typing.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            IsOpen = true;
            AnyOpen = true;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            inputWasUi = UiStack.IsAnyOpen;
            var router = Input.InputRouter.Instance;
            if (router != null)
            {
                router.UseUi();
            }
            canvas.enabled = true;
            input.text = "";
            historyIndex = history.Count;
            input.ActivateInputField();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            AnyOpen = false;
            input.DeactivateInputField();
            canvas.enabled = false;
            Time.timeScale = UiStack.IsPausing ? 0f : timeScaleBefore;
            if (!inputWasUi && !UiStack.IsAnyOpen)
            {
                var router = Input.InputRouter.Instance;
                if (router != null)
                {
                    router.UsePlayer();
                }
            }
        }

        /// <summary>Runs one typed line and returns what it printed (see <see cref="ConsoleCommands.Run"/>).</summary>
        public string Run(string line) => Commands.Run(line);

        /// <summary>Prints a line to the output, in a colour.</summary>
        public void PrintLine(string text, Color color)
        {
            var hex = ColorUtility.ToHtmlStringRGBA(color);
            lines.Add($"<color=#{hex}>{Escape(text)}</color>");
            while (lines.Count > maxLines)
            {
                lines.RemoveAt(0);
            }
            output.text = string.Join("\n", lines);
        }

        public void PrintLine(string text) => PrintLine(text, TextColor);

        /// <summary>The printed lines, for a test.</summary>
        public IReadOnlyList<string> Lines => lines;

        private void ClearOutput()
        {
            lines.Clear();
            output.text = "";
        }

        private static string Escape(string text) => text.Replace("<", "<​");

        private void OnSubmitted(string line)
        {
            input.text = "";
            var trimmed = (line ?? "").Trim();
            if (trimmed.Length == 0)
            {
                input.ActivateInputField();
                return;
            }
            if (history.Count == 0 || history[history.Count - 1] != trimmed)
            {
                history.Add(trimmed);
                if (history.Count > HistoryLimit)
                {
                    history.RemoveAt(0);
                }
            }
            historyIndex = history.Count;
            PrintLine(Prompt + trimmed, EchoColor);
            var answer = Run(trimmed);
            var failed = ConsoleCommands.IsError(answer);
            foreach (var row in answer.Split('\n'))
            {
                if (row.Length > 0)
                {
                    PrintLine(row, failed ? ErrorColor : TextColor);
                }
            }
            input.ActivateInputField();
        }

        /// <summary>Arrow up and down walk the history into the input field.</summary>
        private void Recall(int index)
        {
            historyIndex = Mathf.Clamp(index, 0, history.Count);
            input.text = historyIndex < history.Count ? history[historyIndex] : "";
            input.caretPosition = input.text.Length;
        }

        /// <summary>`lab`: what the console says, and the game it starts.</summary>
        public static string LabCommand(string[] args)
        {
            var station = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (station == "list")
            {
                return "Stations: " + string.Join(", ", Lab.LabLayout.Ids);
            }
            if (station.Length > 0 && System.Array.IndexOf(Lab.LabLayout.Ids, station) < 0)
            {
                return $"No station '{station}'. Stations: " + string.Join(", ", Lab.LabLayout.Ids);
            }
            var flow = Game.GameFlow.Instance;
            if (flow == null)
            {
                return "No game is running here.";
            }
            flow.StartLab(station);
            return station.Length > 0 ? $"Opening the lab at '{station}'." : "Opening the lab.";
        }

        private string TuneCommand(string[] args)
        {
            if (args.Length == 0 || args[0].ToLowerInvariant() != "panel")
            {
                return TuneCommands.Tune(args);
            }
            if (args.Length > 2)
            {
                return TuneCommands.TuneUsage;
            }
            if (args.Length == 1)
            {
                SetTuningShown(!TuningShown);
                return TuningShown ? "Tuning panel on" : "Tuning panel off";
            }
            if (Player.LiveTuning.TryOnOff(args[1], out var on))
            {
                SetTuningShown(on);
                return on ? "Tuning panel on" : "Tuning panel off";
            }
            if (TuningOverlay.GroupStarting(args[1]) < 0)
            {
                var groups = string.Join(", ", Player.LiveTuning.Groups);
                return $"No tuning group starts with '{args[1]}'. Groups: {groups}";
            }
            SetTuningShown(true);
            tuning.TurnTo(args[1]);
            return "Tuning panel on";
        }

        /// <summary>Shows or hides the tuning panel (what `tune panel` does).</summary>
        public void SetTuningShown(bool shown)
        {
            if (shown == TuningShown)
            {
                return;
            }
            if (shown)
            {
                tuning = new GameObject("TuningOverlay").AddComponent<TuningOverlay>();
                tuning.transform.SetParent(transform, false);
            }
            else
            {
                Destroy(tuning.gameObject);
                tuning = null;
            }
        }

        private string ShowBoxesCommand(string[] args)
        {
            if (args.Length == 0)
            {
                if (BoxesShown)
                {
                    return $"show_boxes is on: {boxes.ShapeCount} shapes tracked";
                }
                return "show_boxes is off";
            }
            switch (args[0].ToLowerInvariant())
            {
                case "on":
                    SetBoxesShown(true);
                    return "Collision boxes on";
                case "off":
                    SetBoxesShown(false);
                    return "Collision boxes off";
            }
            return "Usage: show_boxes on|off";
        }

        /// <summary>Turns the collision overlay on or off (what `show_boxes` does).</summary>
        public void SetBoxesShown(bool shown)
        {
            if (shown == BoxesShown)
            {
                return;
            }
            if (shown)
            {
                boxes = new GameObject("CollisionBoxes").AddComponent<CollisionBoxes>();
                boxes.transform.SetParent(transform, false);
            }
            else
            {
                Destroy(boxes.gameObject);
                boxes = null;
            }
        }
    }
}
