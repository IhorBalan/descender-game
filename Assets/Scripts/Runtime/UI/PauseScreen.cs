using Descender.Game;
using Descender.Save;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The pause menu, in the title's gilded look over a dark veil the game shows
    /// faintly through: PAUSED over a jewelled rule, a line with the slot, the
    /// difficulty and the time played, then one column: resume, restart at the last
    /// checkpoint, save to the current slot, the map, the promises (quest log), the
    /// pack (inventory), the settings and quit to the title, with the gold bar gliding
    /// to the focused one. Quit asks to be pressed twice. Opens when
    /// <see cref="GameFlow"/> pauses and resumes the game when it closes. The map,
    /// promises and pack pages belong to the in-game UI; they raise events here for
    /// those screens to answer, and are faded until the in-game UI turns them on.
    /// </summary>
    public sealed class PauseScreen : MenuScreen
    {
        public const string QuitText = "Quit to Title";
        public const string QuitConfirmText = "Quit?  Press again";

        private const float Middle = TitleKit.Width * 0.5f;
        private const float HeadingY = 200f;
        private const float RuleY = 262f;
        private const float StatusY = 306f;
        private const float ItemsY = 382f;

        [SerializeField]
        private SettingsScreen settings;

        private GameObject frame;
        private CanvasGroup stageGroup;
        private TextMeshProUGUI status;
        private RectTransform keys;
        private TitleList menu;
        private TitleFocus focus;
        private Button resumeButton;
        private Button saveButton;
        private Button quitButton;
        private Button mapButton;
        private Button promisesButton;
        private Button packButton;
        private Button settingsButton;
        private bool closingToResume;
        private bool quitAsked;
        private int keysFor = -1;
        private float openedAt;
        private GameObject lastSelected;

        /// <summary>The in-game screens open on these; an open page hides the frame with <see cref="ShowFrame"/>.</summary>
        public event System.Action MapRequested;
        public event System.Action PromisesRequested;
        public event System.Action PackRequested;

        /// <summary>Whether the map, promises and pack buttons can be pressed; the in-game UI turns them on.</summary>
        public bool MapAvailable { get; set; }
        public bool PromisesAvailable { get; set; }
        public bool PackAvailable { get; set; }

        /// <summary>The menu's column, for tests.</summary>
        public TitleList Menu => menu;

        /// <summary>The bar behind the focused item.</summary>
        public TitleFocus Focus => focus;

        /// <summary>The line under the heading: the slot, the difficulty and the time played.</summary>
        public string StatusText => status != null ? status.text : "";

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
            var stage = TitleHints.MakeStage(root, theme, true, 23, out stageGroup);
            var frameRect = UiKit.Rect(stage, "Frame");
            UiKit.Fill(frameRect);
            frame = frameRect.gameObject;

            TitleKit.Label(frameRect, "PAUSED", 51f, TitleKit.Gold, TitleKit.CapsSpacing, Middle, HeadingY);
            TitleKit.Sprite(frameRect, "Rule", theme.ruleTagline, Middle, RuleY);
            status = TitleKit.Label(
                frameRect,
                "",
                TitleKit.SmallSize,
                TitleKit.Muted,
                TitleKit.SmallSpacing,
                Middle,
                StatusY
            );

            menu = new TitleList(frameRect, "Menu", ItemsY, 820f, theme.focusBar, theme.ruleItem);
            resumeButton = menu.Add("Resume", Close);
            menu.Add("Restart at Checkpoint", OnRestartPressed);
            saveButton = menu.Add("Save Game", OnSavePressed);
            mapButton = menu.Add("Map", OnMapPressed);
            promisesButton = menu.Add("Promises", OnPromisesPressed);
            packButton = menu.Add("Pack", OnPackPressed);
            settingsButton = menu.Add("Settings", OnSettingsPressed);
            quitButton = menu.Add(QuitText, OnQuitPressed);
            MenuKit.LinkVertical(menu.Layout());
            focus = TitleFocus.Make(frameRect, theme.focusEmblem);
            focus.Follow(menu, true);

            keys = UiKit.Rect(frameRect, "Keys");
            UiKit.Fill(keys);
            if (settings != null)
            {
                settings.Closed += OnSettingsClosed;
            }
        }

        protected override void OnOpened()
        {
            closingToResume = true;
            openedAt = Time.unscaledTime;
            stageGroup.alpha = 0f;
            var slot = SaveManager.CurrentSlot;
            var flow = GameFlow.Instance;
            var seconds = flow != null ? (int)flow.PlayTime : 0;
            var played = $"{seconds / 60}:{seconds % 60:00}";
            status.text =
                slot >= 0
                    ? $"SLOT {slot + 1}   ·   {SaveManager.DifficultyName().ToUpperInvariant()}   ·   {played}"
                    : $"NO SAVE SLOT   ·   {played}";
            saveButton.interactable = slot >= 0;
            TitleKit.SetText(saveButton, "Save Game");
            ResetQuit();
            mapButton.interactable = MapAvailable;
            promisesButton.interactable = PromisesAvailable;
            packButton.interactable = PackAvailable;
            keysFor = -1;
            ShowFrame(true);
        }

        protected override void OnClosed()
        {
            if (closingToResume && GameFlow.Instance != null)
            {
                GameFlow.Instance.Resume();
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
            var selected = Selected;
            if (selected != lastSelected)
            {
                if (lastSelected != null && menu.Contains(selected))
                {
                    Audio.Sfx.Play("menu_move", -6f);
                }
                lastSelected = selected;
                if (selected != quitButton.gameObject)
                {
                    ResetQuit();
                }
            }
            var gamepad = UsingGamepad ? 1 : 0;
            if (keysFor != gamepad)
            {
                keysFor = gamepad;
                TitleHints.Build(keys, theme, gamepad == 1, "Confirm", "Resume");
            }
        }

        private void Update()
        {
            if (IsOpen)
            {
                stageGroup.alpha = TitleScreen.Smooth((Time.unscaledTime - openedAt) / 0.3f);
            }
        }

        /// <summary>Hides the menu while one of the in-game pages (map, promises, pack) covers it.</summary>
        public void ShowFrame(bool shown)
        {
            frame.SetActive(shown);
            if (shown && IsOpen)
            {
                Select(resumeButton);
                lastSelected = resumeButton.gameObject;
            }
        }

        private void ResetQuit()
        {
            if (quitAsked)
            {
                quitAsked = false;
                TitleKit.SetText(quitButton, QuitText);
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
            if (GameFlow.Instance != null)
            {
                GameFlow.Instance.Restart();
            }
        }

        private void OnSavePressed()
        {
            var flow = GameFlow.Instance;
            if (flow != null && flow.SaveGame(out var place))
            {
                TitleKit.SetText(saveButton, $"Saved at {place}");
            }
        }

        /// <summary>The first press asks; the second, with the question still showing, quits.</summary>
        private void OnQuitPressed()
        {
            if (!quitAsked)
            {
                quitAsked = true;
                TitleKit.SetText(quitButton, QuitConfirmText);
                return;
            }
            closingToResume = false;
            Close();
            if (GameFlow.Instance != null)
            {
                GameFlow.Instance.QuitToTitle();
            }
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
            lastSelected = settingsButton.gameObject;
        }
    }
}
