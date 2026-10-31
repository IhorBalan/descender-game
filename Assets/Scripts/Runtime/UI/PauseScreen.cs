using System.Collections.Generic;
using Descender.Game;
using Descender.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The pause menu, in two columns: resume, restart at the last checkpoint, save to
    /// the current slot and quit to the title on the left; the map, the promises
    /// (quest log), the pack (inventory) and the settings on the right. Quit asks to
    /// be pressed twice. A line under the heading says which slot the game is in and
    /// how long it has been played. Opens when <see cref="GameFlow"/> pauses and
    /// resumes the game when it closes. The map, promises and pack pages belong to
    /// the in-game UI; they raise events here for those screens to answer. Port of
    /// scripts/ui/pause_menu.gd.
    /// </summary>
    public sealed class PauseScreen : MenuScreen
    {
        public const string QuitText = "Quit to Title";
        public const string QuitConfirmText = "Quit?  Press again";

        [SerializeField]
        private SettingsScreen settings;

        private GameObject frame;
        private Text status;
        private Text hint;
        private Button resumeButton;
        private Button restartButton;
        private Button saveButton;
        private Button quitButton;
        private Button mapButton;
        private Button promisesButton;
        private Button packButton;
        private Button settingsButton;
        private bool closingToResume;

        /// <summary>The in-game screens open on these; an open page hides the frame with <see cref="ShowFrame"/>.</summary>
        public event System.Action MapRequested;
        public event System.Action PromisesRequested;
        public event System.Action PackRequested;

        /// <summary>Whether the map, promises and pack buttons can be pressed; the in-game UI turns them on.</summary>
        public bool MapAvailable { get; set; }
        public bool PromisesAvailable { get; set; }
        public bool PackAvailable { get; set; }

        protected override int SortingOrder => 20;

        private void OnEnable()
        {
            var flow = GameFlow.Instance;
            if (flow == null)
            {
                return;
            }
            flow.Paused += Open;
            flow.Resumed += OnResumed;
        }

        private void OnDisable()
        {
            var flow = GameFlow.Instance;
            if (flow == null)
            {
                return;
            }
            flow.Paused -= Open;
            flow.Resumed -= OnResumed;
        }

        protected override void Build(RectTransform root)
        {
            var dim = MenuKit.MakeImage(root, "Dim", null);
            dim.color = theme.dim;
            MenuKit.Stretch(dim.rectTransform);

            var frameRect = MenuKit.MakeRect(root, "Frame");
            frame = frameRect.gameObject;
            MenuKit.Stretch(frameRect);
            var panel = MenuKit.MakePanel(frameRect, theme);
            MenuKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 120f));
            var heading = MenuKit.MakeHeading(panel.transform, theme, "PAUSED", MenuTheme.HeadingFontSize);
            MenuKit.Strip(heading.rectTransform, true, 6f, 20f);
            var divider = MenuKit.MakeImage(panel.transform, "Divider", theme.divider);
            MenuKit.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(120f, 5f));
            status = MenuKit.MakeLabel(panel.transform, theme, "", MenuTheme.MutedFontSize);
            status.color = theme.textMuted;
            MenuKit.Strip(status.rectTransform, true, 30f, 10f);

            var columns = MenuKit.MakeRow(panel.transform, "Menu", 8f);
            columns.childAlignment = TextAnchor.UpperCenter;
            MenuKit.Place(
                columns.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -44f),
                new Vector2(200f, 56f)
            );
            var left = MenuKit.MakeColumn(columns.transform, "Left");
            MenuKit.Size(left, 92f, 56f);
            var right = MenuKit.MakeColumn(columns.transform, "Right");
            MenuKit.Size(right, 92f, 56f);
            resumeButton = MenuKit.MakeButton(left.transform, theme, "Resume", Close);
            restartButton = MenuKit.MakeButton(left.transform, theme, "Restart at checkpoint", OnRestartPressed);
            saveButton = MenuKit.MakeButton(left.transform, theme, "Save Game", OnSavePressed);
            quitButton = MenuKit.MakeButton(left.transform, theme, QuitText, OnQuitPressed);
            mapButton = MenuKit.MakeButton(right.transform, theme, "Map", OnMapPressed);
            promisesButton = MenuKit.MakeButton(right.transform, theme, "Promises", OnPromisesPressed);
            packButton = MenuKit.MakeButton(right.transform, theme, "Pack", OnPackPressed);
            settingsButton = MenuKit.MakeButton(right.transform, theme, "Settings", OnSettingsPressed);
            var leftItems = new List<Selectable> { resumeButton, restartButton, saveButton, quitButton };
            var rightItems = new List<Selectable> { mapButton, promisesButton, packButton, settingsButton };
            MenuKit.LinkVertical(leftItems);
            MenuKit.LinkVertical(rightItems);
            for (var i = 0; i < leftItems.Count; i++)
            {
                var navigation = leftItems[i].navigation;
                navigation.selectOnRight = rightItems[i];
                navigation.selectOnLeft = rightItems[i];
                leftItems[i].navigation = navigation;
                navigation = rightItems[i].navigation;
                navigation.selectOnLeft = leftItems[i];
                navigation.selectOnRight = leftItems[i];
                rightItems[i].navigation = navigation;
            }

            hint = MenuKit.MakeLabel(panel.transform, theme, "", MenuTheme.MutedFontSize);
            hint.color = theme.textMuted;
            MenuKit.Strip(hint.rectTransform, false, 6f, 10f);
            if (settings != null)
            {
                settings.Closed += OnSettingsClosed;
            }
        }

        protected override void OnOpened()
        {
            closingToResume = true;
            ShowFrame(true);
            var slot = SaveManager.CurrentSlot;
            var flow = GameFlow.Instance;
            var seconds = flow != null ? (int)flow.PlayTime : 0;
            var played = $"{seconds / 60}:{seconds % 60:00}";
            status.text =
                slot >= 0 ? $"Slot {slot + 1}   {SaveManager.DifficultyName()}   {played}" : $"No save slot   {played}";
            saveButton.interactable = slot >= 0;
            MenuKit.SetText(saveButton, "Save Game");
            MenuKit.SetText(quitButton, QuitText);
            mapButton.interactable = MapAvailable;
            promisesButton.interactable = PromisesAvailable;
            packButton.interactable = PackAvailable;
            hint.text = MenuKit.Hint(UsingGamepad, "move", "choose", "resume");
            Select(resumeButton);
        }

        protected override void OnClosed()
        {
            if (closingToResume)
            {
                GameFlow.Instance?.Resume();
            }
        }

        public override void OnTop()
        {
            // The pad's Start button resumes too; it is on the Player map, which is off while paused.
            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (Selected != quitButton.gameObject)
            {
                MenuKit.SetText(quitButton, QuitText);
            }
        }

        /// <summary>Hides the menu while one of the in-game pages (map, promises, pack) covers it.</summary>
        public void ShowFrame(bool shown)
        {
            frame.SetActive(shown);
            if (shown && IsOpen)
            {
                Select(resumeButton);
            }
        }

        private void OnResumed()
        {
            if (IsOpen)
            {
                closingToResume = false;
                Close();
            }
        }

        private void OnRestartPressed()
        {
            closingToResume = false;
            Close();
            GameFlow.Instance?.Restart();
        }

        private void OnSavePressed()
        {
            var flow = GameFlow.Instance;
            if (flow != null && flow.SaveGame(out var place))
            {
                MenuKit.SetText(saveButton, $"Saved at {place}");
            }
        }

        /// <summary>The first press asks; the second, with the question still showing, quits.</summary>
        private void OnQuitPressed()
        {
            var label = quitButton.GetComponentInChildren<Text>();
            if (label != null && label.text != QuitConfirmText)
            {
                MenuKit.SetText(quitButton, QuitConfirmText);
                return;
            }
            closingToResume = false;
            Close();
            GameFlow.Instance?.QuitToTitle();
        }

        private void OnMapPressed() => MapRequested?.Invoke();

        private void OnPromisesPressed() => PromisesRequested?.Invoke();

        private void OnPackPressed() => PackRequested?.Invoke();

        private void OnSettingsPressed()
        {
            if (settings == null)
            {
                return;
            }
            frame.SetActive(false);
            settings.Open();
        }

        private void OnSettingsClosed()
        {
            if (!IsOpen)
            {
                return;
            }
            frame.SetActive(true);
            Select(settingsButton);
        }
    }
}
