using Descender.Game;
using Descender.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The title screen: new game, continue the latest save, load a slot, settings,
    /// credits, quit. New Game and Load open the slot picker; for a new game an
    /// occupied slot asks to be pressed twice before it is overwritten, then the
    /// difficulty picker chooses how hard the run is. The picker's Delete button
    /// turns it into a delete picker. The hint along the bottom says where Continue
    /// would resume while that button is focused. Opens itself when
    /// <see cref="GameFlow"/> shows the title and closes when the world is entered.
    /// <para>
    /// The look is gilded and pixel perfect: a flat dark ground with gold motes
    /// rising, the haloed DESCENDER logo, items in spaced capitals with only a gold
    /// rule between them, and one focus bar that glides to the focused item. The
    /// canvas keeps a whole-number scale (<see cref="PixelCanvasScale"/>) and the
    /// screen is laid out on a 320 x 180 stage in its middle; the art comes from
    /// tools/title_textures.py through the <see cref="MenuTheme"/>.
    /// </para>
    /// </summary>
    public sealed class TitleScreen : MenuScreen
    {
        public const string Tagline = "A man with half-finished angel powers descends from Heaven";

        /// <summary>The credits, one line each; an empty string is a blank line.</summary>
        public static readonly string[] Credits =
        {
            "A game by Ihor Balan",
            "",
            "Design, code, placeholder art and sound",
            "made with Claude Code",
            "",
            "Map editor by Vadim Tarasenko",
            "",
            "Made with Unity",
        };

        /// <summary>How long the screen takes to rise out of the dark when it opens.</summary>
        public const float IntroSeconds = 1.2f;

        /// <summary>How long a column takes to fade in when the screen moves to it.</summary>
        public const float ColumnFadeSeconds = 0.2f;

        // Rows on the 320 x 180 stage, in pixels from the top.
        private const float HaloTop = -4f;
        private const float LogoTop = 18f;
        private const float TaglineTop = 56f;
        private const float RuleTop = 66f;
        private const float ItemsTop = 84f;
        private const float HintsTop = 166f;
        private const float ColumnWidth = 200f;
        private const float WideColumnWidth = 300f;

        [SerializeField]
        private SettingsScreen settings;

        private RectTransform stage;
        private CanvasGroup stageGroup;
        private GameObject header;
        private Image halo;
        private PixelText tagline;
        private GameObject credits;
        private TitleList menu;
        private TitleList slots;
        private TitleList difficulty;
        private TitleList creditsList;
        private TitleList shown;
        private TitleFocus focus;
        private PixelText slotsHeading;
        private PixelText status;
        private RectTransform keys;
        private PixelText resume;
        private Button newGameButton;
        private Button continueButton;
        private Button loadButton;
        private readonly Button[] slotButtons = new Button[SaveSlots.SlotCount];
        private Button deleteButton;
        private Button slotsBackButton;
        private readonly Button[] difficultyButtons = new Button[SaveManager.DifficultyNames.Length];
        private Button creditsBackButton;
        private SlotMode slotMode = SlotMode.NewGame;
        private int newGameSlot = -1;
        private int confirmSlot = -1;
        private GameObject lastSelected;
        private int keysFor = -1;
        private float openedAt;
        private float shownAt;

        protected override int SortingOrder => 10;

        /// <summary>The column on show: the main menu, the slots, the difficulty or the credits' Back.</summary>
        public TitleList Shown => shown;

        /// <summary>The main menu's column.</summary>
        public TitleList Menu => menu;

        /// <summary>The bar behind the focused item.</summary>
        public TitleFocus Focus => focus;

        private void OnEnable()
        {
            var flow = GameFlow.Instance;
            if (flow == null)
            {
                return;
            }
            flow.TitleShown += Open;
            flow.WorldEntered += OnWorldEntered;
            flow.WorldUnavailable += OnWorldUnavailable;
            if (flow.Current == GameFlow.Phase.Title)
            {
                Open();
            }
        }

        private void OnDisable()
        {
            var flow = GameFlow.Instance;
            if (flow == null)
            {
                return;
            }
            flow.TitleShown -= Open;
            flow.WorldEntered -= OnWorldEntered;
            flow.WorldUnavailable -= OnWorldUnavailable;
        }

        protected override void Build(RectTransform root)
        {
            TitleKit.Font = PixelFont.Read(
                theme.pixelFont,
                theme.pixelFontMetrics != null ? theme.pixelFontMetrics.text : null
            );
            root.gameObject.AddComponent<PixelCanvasScale>();
            var ground = UiKit.Box(root, "Background", TitleKit.Background);
            UiKit.Fill(ground.rectTransform);

            stage = UiKit.Rect(root, "Stage");
            stage.anchorMin = new Vector2(0.5f, 0.5f);
            stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(UiTheme.Width, UiTheme.Height);
            stageGroup = stage.gameObject.AddComponent<CanvasGroup>();
            TitleEmbers.Make(stage, 7);

            var top = UiKit.Rect(stage, "Header");
            UiKit.Fill(top);
            header = top.gameObject;
            halo = TitleKit.Sprite(top, "Halo", theme.titleHalo, UiTheme.Width, HaloTop);
            TitleKit.Sprite(top, "Logo", theme.titleLogo, UiTheme.Width, LogoTop);
            TitleKit.Sprite(top, "Rule", theme.ruleTagline, UiTheme.Width, RuleTop);
            tagline = TitleKit.Label(top, "", TitleKit.Muted);
            TitleKit.Center(tagline, Tagline, UiTheme.Width, TaglineTop);

            var version = TitleKit.Label(stage, "", TitleKit.Faded);
            TitleKit.Center(version, "v" + Application.version, UiTheme.Width, 1f);
            var corner = version.rectTransform;
            corner.anchoredPosition = new Vector2(UiTheme.Width - 4f - corner.sizeDelta.x, corner.anchoredPosition.y);

            status = TitleKit.Label(stage, "", TitleKit.Bad);
            status.rectTransform.anchoredPosition = new Vector2(4f, -1f);

            keys = UiKit.Rect(stage, "Keys");
            UiKit.Place(keys, 0f, HintsTop, UiTheme.Width, 12f);
            resume = TitleKit.Label(stage, "", TitleKit.Muted);

            BuildMenu();
            BuildSlots();
            BuildDifficulty();
            BuildCredits();
            focus = TitleFocus.Make(stage, theme.focusEmblem);
            if (settings != null)
            {
                settings.Closed += ShowMenu;
            }
        }

        private void BuildMenu()
        {
            menu = new TitleList(stage, "Menu", ItemsTop, ColumnWidth, theme.focusBar, theme.ruleItem);
            newGameButton = menu.Add("New Game", OnNewGamePressed);
            continueButton = menu.Add("Continue", OnContinuePressed);
            loadButton = menu.Add("Load Game", OnLoadPressed);
            menu.Add("Settings", OnSettingsPressed);
            menu.Add("Credits", ShowCredits);
            menu.Add("Quit", Quit);
            MenuKit.LinkVertical(menu.Layout());
            menu.Root.gameObject.SetActive(false);
        }

        private void BuildSlots()
        {
            slots = new TitleList(stage, "Slots", ItemsTop, WideColumnWidth, theme.focusBarWide, theme.ruleItem);
            slotsHeading = TitleKit.Label(slots.Root, "", TitleKit.Gold, TitleKit.CapsSpacing);
            for (var slot = 0; slot < slotButtons.Length; slot++)
            {
                var index = slot;
                slotButtons[slot] = slots.Add("", () => OnSlotPressed(index), false);
            }
            deleteButton = slots.Add("Delete a save", OnDeletePressed);
            slotsBackButton = slots.Add("Back", ShowMenu, true, TitleList.BackSound);
            slots.Root.gameObject.SetActive(false);
        }

        private void BuildDifficulty()
        {
            difficulty = new TitleList(
                stage,
                "Difficulty",
                ItemsTop,
                WideColumnWidth,
                theme.focusBarWide,
                theme.ruleItem
            );
            var heading = TitleKit.Label(difficulty.Root, "", TitleKit.Gold, TitleKit.CapsSpacing);
            TitleKit.Center(heading, "NEW GAME:  CHOOSE A DIFFICULTY", UiTheme.Width, TaglineTop);
            for (var which = 0; which < difficultyButtons.Length; which++)
            {
                var index = which;
                var text = $"{SaveManager.DifficultyNames[which]}   {SaveManager.DifficultyNotes[which]}";
                difficultyButtons[which] = difficulty.Add(text, () => OnDifficultyPressed(index), false);
            }
            difficulty.Add("Back", OnDifficultyBackPressed, true, TitleList.BackSound);
            MenuKit.LinkVertical(difficulty.Layout());
            var note = TitleKit.Label(difficulty.Root, "", TitleKit.Faded);
            var below = ItemsTop + difficulty.Items.Count * TitleKit.Step - 2f;
            TitleKit.Center(note, "The difficulty is kept with the save.", UiTheme.Width, below);
            difficulty.Root.gameObject.SetActive(false);
        }

        private void BuildCredits()
        {
            var page = UiKit.Rect(stage, "Credits");
            UiKit.Fill(page);
            credits = page.gameObject;
            // Twice the pixel size, so each font pixel is a clean two by two.
            var heading = TitleKit.Label(page, "", TitleKit.Gold, TitleKit.CapsSpacing, 2);
            TitleKit.Center(heading, "CREDITS", UiTheme.Width, 12f);
            TitleKit.Sprite(page, "Rule", theme.ruleTagline, UiTheme.Width, 40f);
            for (var i = 0; i < Credits.Length; i++)
            {
                var line = TitleKit.Label(page, "", i == 0 ? TitleKit.Ivory : TitleKit.Parchment);
                TitleKit.Center(line, Credits[i], UiTheme.Width, 52f + i * 11f);
            }
            creditsList = new TitleList(page, "Back", 152f, ColumnWidth, theme.focusBar, theme.ruleItem);
            creditsBackButton = creditsList.Add("Back", ShowMenu, true, TitleList.BackSound);
            MenuKit.LinkVertical(creditsList.Layout());
            credits.SetActive(false);
        }

        protected override void OnOpened()
        {
            status.Text = "";
            openedAt = Time.unscaledTime;
            stageGroup.alpha = 0f;
            ShowMenu();
        }

        public override void OnCancel()
        {
            if (shown == difficulty)
            {
                Audio.Sfx.Play(TitleList.BackSound);
                ShowSlots(SlotMode.NewGame);
            }
            else if (shown == slots || shown == creditsList)
            {
                Audio.Sfx.Play(TitleList.BackSound);
                ShowMenu();
            }
        }

        public override void OnTop()
        {
            var selected = Selected;
            if (selected != lastSelected)
            {
                // A step within the column on show ticks; a jump to another column came from a press, already heard.
                if (lastSelected != null && shown != null && shown.Contains(selected))
                {
                    Audio.Sfx.Play("menu_move", -6f);
                }
                lastSelected = selected;
                RefreshHint();
            }
            else if (keys.gameObject.activeSelf && keysFor != (UsingGamepad ? 1 : 0))
            {
                RefreshHint();
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            var now = Time.unscaledTime;
            stageGroup.alpha = Smooth((now - openedAt) / IntroSeconds);
            if (shown != null)
            {
                shown.Group.alpha = Smooth((now - shownAt) / ColumnFadeSeconds);
            }
            var breath = 0.85f + 0.15f * Mathf.Sin(now * 1.3f);
            halo.color = new Color(1f, 1f, 1f, Mathf.Round(breath * 16f) / 16f);
        }

        /// <summary>Eases in and out over 0..1, clamped.</summary>
        public static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        /// <summary>Puts a column on show alone, with the focus bar in it and the focus on `first`.</summary>
        private void Show(TitleList column, Selectable first, bool showHeader, bool showTagline)
        {
            menu.Root.gameObject.SetActive(column == menu);
            slots.Root.gameObject.SetActive(column == slots);
            difficulty.Root.gameObject.SetActive(column == difficulty);
            credits.SetActive(column == creditsList);
            header.SetActive(showHeader);
            tagline.gameObject.SetActive(showTagline);
            shown = column;
            shownAt = Time.unscaledTime;
            column.Group.alpha = 0f;
            focus.Follow(column, column == menu);
            Select(first);
            lastSelected = first != null ? first.gameObject : null;
            RefreshHint();
        }

        private void ShowMenu()
        {
            if (!IsOpen)
            {
                return;
            }
            var anySave = SaveManager.Slots.LatestSlot() >= 0;
            continueButton.interactable = anySave;
            loadButton.interactable = anySave;
            Show(menu, newGameButton, true, true);
        }

        private void ShowSlots(SlotMode mode)
        {
            slotMode = mode;
            confirmSlot = -1;
            var heading = mode switch
            {
                SlotMode.NewGame => "NEW GAME:  CHOOSE A SLOT",
                SlotMode.Load => "LOAD GAME",
                _ => "DELETE A SAVE:  CHOOSE A SLOT",
            };
            TitleKit.Center(slotsHeading, heading, UiTheme.Width, TaglineTop);
            // Deleting is offered from the load picker, and the delete picker goes back to loading.
            deleteButton.gameObject.SetActive(mode != SlotMode.NewGame);
            TitleKit.SetText(deleteButton, mode == SlotMode.Delete ? "Load instead" : "Delete a save");
            for (var slot = 0; slot < slotButtons.Length; slot++)
            {
                var button = slotButtons[slot];
                TitleKit.SetText(button, $"{slot + 1}   {SaveManager.Summary(slot, GameFlow.LevelNameOf)}");
                button.interactable = mode == SlotMode.NewGame || SaveManager.Slots.HasSave(slot);
            }
            var items = slots.Layout();
            MenuKit.LinkVertical(items);
            var first = MenuKit.FirstEnabled(items);
            Show(slots, first != null ? first : slotsBackButton, true, false);
        }

        private void ShowDifficulty(int slot)
        {
            newGameSlot = slot;
            Show(difficulty, difficultyButtons[(int)Difficulty.Normal], true, false);
        }

        private void ShowCredits() => Show(creditsList, creditsBackButton, false, false);

        private void OnNewGamePressed() => ShowSlots(SlotMode.NewGame);

        private void OnDifficultyBackPressed() => ShowSlots(SlotMode.NewGame);

        private void OnLoadPressed() => ShowSlots(SlotMode.Load);

        private void OnContinuePressed()
        {
            if (GameFlow.Instance != null && !GameFlow.Instance.ContinueGame())
            {
                ShowMenu();
            }
        }

        private void OnSettingsPressed()
        {
            if (settings == null)
            {
                return;
            }
            menu.Root.gameObject.SetActive(false);
            settings.Open();
        }

        private void OnSlotPressed(int slot)
        {
            switch (slotMode)
            {
                case SlotMode.NewGame:
                    if (SaveManager.Slots.HasSave(slot) && !Confirm(slot, "Overwrite?"))
                    {
                        return;
                    }
                    ShowDifficulty(slot);
                    return;
                case SlotMode.Load:
                    if (GameFlow.Instance == null || !GameFlow.Instance.LoadGame(slot))
                    {
                        ShowSlots(SlotMode.Load);
                    }
                    return;
                default:
                    if (Confirm(slot, "Delete?"))
                    {
                        SaveManager.Slots.Delete(slot);
                        ShowSlots(SaveManager.Slots.LatestSlot() >= 0 ? SlotMode.Delete : SlotMode.Load);
                    }
                    return;
            }
        }

        /// <summary>A slot's first press asks the question; the second press answers it.</summary>
        private bool Confirm(int slot, string question)
        {
            if (confirmSlot == slot)
            {
                return true;
            }
            confirmSlot = slot;
            TitleKit.SetText(slotButtons[slot], $"{slot + 1}   {question}  Press again");
            return false;
        }

        private void OnDeletePressed() => ShowSlots(slotMode == SlotMode.Delete ? SlotMode.Load : SlotMode.Delete);

        private void OnDifficultyPressed(int which)
        {
            if (newGameSlot < 0 || GameFlow.Instance == null)
            {
                return;
            }
            GameFlow.Instance.StartNewGame(newGameSlot, (Difficulty)which);
        }

        private void OnWorldEntered(Player.PlayerController hero) => Close();

        private void OnWorldUnavailable(string reason)
        {
            if (IsOpen)
            {
                status.Text = reason;
            }
        }

        private void RefreshHint()
        {
            if (keys == null)
            {
                return;
            }
            var onContinue = shown == menu && Selected == continueButton.gameObject && continueButton.interactable;
            keys.gameObject.SetActive(!onContinue);
            resume.gameObject.SetActive(onContinue);
            if (onContinue)
            {
                var slot = SaveManager.Slots.LatestSlot();
                var text = $"Resumes slot {slot + 1}:   {SaveManager.Summary(slot, GameFlow.LevelNameOf)}";
                TitleKit.Center(resume, text, UiTheme.Width, HintsTop + 1f);
                return;
            }
            var gamepad = UsingGamepad ? 1 : 0;
            if (keysFor != gamepad)
            {
                keysFor = gamepad;
                BuildKeys(gamepad == 1);
            }
        }

        /// <summary>The key hints along the bottom: a cap and what it does, three times, between two gold lines.</summary>
        private void BuildKeys(bool gamepad)
        {
            UiKit.Clear(keys);
            var pairs = gamepad
                ? new[] { "D-pad", "Navigate", "A", "Confirm", "B", "Back" }
                : new[] { "Arrows", "Navigate", "Enter", "Confirm", "Esc", "Back" };
            var row = UiKit.Rect(keys, "Row");
            var x = 0f;
            for (var i = 0; i < pairs.Length; i += 2)
            {
                x += TitleKit.KeyCap(row, pairs[i], x, 0f) + 4f;
                var label = TitleKit.Label(row, pairs[i + 1], TitleKit.Muted);
                var w = label.InkWidth;
                UiKit.Place(label.rectTransform, x, -1f, w, label.LineHeight);
                x += w + (i + 2 < pairs.Length ? 14f : 0f);
            }
            var left = Mathf.Floor((UiTheme.Width - x) * 0.5f);
            UiKit.Place(row, left, 0f, x, 12f);
            if (theme.ruleHints == null)
            {
                return;
            }
            var length = theme.ruleHints.rect.width;
            var lead = TitleKit.Sprite(keys, "Line", theme.ruleHints, 0f, 6f);
            lead.rectTransform.anchoredPosition = new Vector2(left - 8f - length, -6f);
            // The right line is the left one mirrored, bright at its inner end too.
            var tail = TitleKit.Sprite(keys, "Line", theme.ruleHints, 0f, 6f);
            tail.rectTransform.anchoredPosition = new Vector2(left + x + 8f + length, -6f);
            tail.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private enum SlotMode
        {
            NewGame,
            Load,
            Delete,
        }
    }
}
