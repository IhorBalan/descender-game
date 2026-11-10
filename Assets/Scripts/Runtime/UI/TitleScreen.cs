using Descender.Game;
using Descender.Save;
using TMPro;
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
    /// The look is a smooth, gilded dark-fantasy title on a flat dark ground: gold
    /// embers rising, the haloed DESCENDER logo, the tagline in spaced capitals over
    /// a jewelled rule, items in Cinzel with only a hairline between them, and one
    /// glowing focus bar that glides to the focused item. It is laid out on a
    /// 1920 x 1080 stage that the canvas scales to the window; the art comes from
    /// tools/title_textures.py and the font from Descender > Setup > Title font,
    /// both through the <see cref="MenuTheme"/>.
    /// </para>
    /// </summary>
    public sealed class TitleScreen : MenuScreen
    {
        public const string Tagline = "A man with half-finished angel powers descends from Heaven";

        /// <summary>The tagline as the screen sets it, in two lines.</summary>
        public static readonly string[] TaglineLines =
        {
            "A MAN WITH HALF-FINISHED ANGEL POWERS",
            "DESCENDS FROM HEAVEN",
        };

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
        public const float IntroSeconds = 1.4f;

        /// <summary>How long a column takes to fade in when the screen moves to it.</summary>
        public const float ColumnFadeSeconds = 0.25f;

        // Middle lines on the 1920 x 1080 stage, in pixels from the top.
        private const float Middle = TitleKit.Width * 0.5f;
        private const float HaloY = 205f;
        private const float LogoY = 215f;
        private const float TaglineY = 330f;
        private const float TaglineGap = 28f;
        private const float RuleY = 398f;
        private const float ItemsY = 470f;
        private const float HintsY = 1005f;
        private const float ColumnWidth = 900f;
        private const float WideColumnWidth = 1400f;

        [SerializeField]
        private SettingsScreen settings;

        private RectTransform stage;
        private CanvasGroup stageGroup;
        private GameObject header;
        private Image halo;
        private GameObject tagline;
        private GameObject credits;
        private TitleList menu;
        private TitleList slots;
        private TitleList difficulty;
        private TitleList creditsList;
        private TitleList shown;
        private TitleFocus focus;
        private TextMeshProUGUI slotsHeading;
        private TextMeshProUGUI status;
        private RectTransform keys;
        private TextMeshProUGUI resume;
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

        /// <summary>Scales a 1920 x 1080 layout to the window, smoothly, keeping all of it in view.</summary>
        public static void ScaleSmooth(Canvas canvas)
        {
            canvas.pixelPerfect = false;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(TitleKit.Width, TitleKit.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
        }

        protected override void Build(RectTransform root)
        {
            TitleKit.Font = theme.titleFont;
            if (theme.titleFont == null)
            {
                Debug.LogWarning("TitleScreen: no title font in the menu theme; run Descender > Setup > Title font.");
            }
            ScaleSmooth(root.GetComponent<Canvas>());
            var ground = UiKit.Box(root, "Background", TitleKit.Background);
            UiKit.Fill(ground.rectTransform);

            stage = UiKit.Rect(root, "Stage");
            stage.anchorMin = new Vector2(0.5f, 0.5f);
            stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(TitleKit.Width, TitleKit.Height);
            stageGroup = stage.gameObject.AddComponent<CanvasGroup>();
            TitleEmbers.Make(stage, theme.mote, 7);

            var top = UiKit.Rect(stage, "Header");
            UiKit.Fill(top);
            header = top.gameObject;
            halo = TitleKit.Sprite(top, "Halo", theme.titleHalo, Middle, HaloY);
            TitleKit.Sprite(top, "Logo", theme.titleLogo, Middle, LogoY);
            TitleKit.Sprite(top, "Rule", theme.ruleTagline, Middle, RuleY);
            BuildTagline(top);

            var version = TitleKit.Label(stage, "v" + Application.version, 26f, TitleKit.Faded, 4f, 0f, 46f, 200f);
            version.alignment = TextAlignmentOptions.Right;
            TitleKit.Center(version.rectTransform, TitleKit.Width - 150f, 46f, 200f, 52f);

            status = TitleKit.Label(stage, "", TitleKit.SmallSize, TitleKit.Bad, 2f, 0f, 46f, 1200f);
            status.alignment = TextAlignmentOptions.Left;
            TitleKit.Center(status.rectTransform, 50f + 600f, 46f, 1200f, 44f);

            keys = UiKit.Rect(stage, "Keys");
            UiKit.Fill(keys);
            resume = TitleKit.Label(stage, "", 24f, TitleKit.Muted, 6f, Middle, HintsY);

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

        /// <summary>The tagline in two spaced lines, the second framed by short hairlines.</summary>
        private void BuildTagline(Transform parent)
        {
            var group = UiKit.Rect(parent, "Tagline");
            UiKit.Fill(group);
            tagline = group.gameObject;
            for (var i = 0; i < TaglineLines.Length; i++)
            {
                var line = TitleKit.Label(
                    group,
                    TaglineLines[i],
                    TitleKit.SmallSize,
                    TitleKit.Muted,
                    TitleKit.SmallSpacing,
                    Middle,
                    TaglineY + i * TaglineGap
                );
                if (i == TaglineLines.Length - 1 && theme.ruleSide != null)
                {
                    var half = TitleKit.WidthOf(line) * 0.5f + 24f + theme.ruleSide.rect.width * 0.5f;
                    TitleKit.Sprite(
                        group,
                        "Line",
                        theme.ruleSide,
                        Middle - half,
                        line.rectTransform.anchoredPosition.y * -1f
                    );
                    var right = TitleKit.Sprite(
                        group,
                        "Line",
                        theme.ruleSide,
                        Middle + half,
                        -line.rectTransform.anchoredPosition.y
                    );
                    right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                }
            }
        }

        private void BuildMenu()
        {
            menu = new TitleList(stage, "Menu", ItemsY, ColumnWidth, theme.focusBar, theme.ruleItem);
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
            slots = new TitleList(stage, "Slots", ItemsY, WideColumnWidth, theme.focusBarWide, theme.ruleItem);
            slotsHeading = Heading(slots.Root);
            for (var slot = 0; slot < slotButtons.Length; slot++)
            {
                var index = slot;
                slotButtons[slot] = slots.Add("", () => OnSlotPressed(index), false);
            }
            deleteButton = slots.Add("Delete a save", OnDeletePressed);
            slotsBackButton = slots.Add("Back", ShowMenu, true, TitleList.BackSound);
            slots.Root.gameObject.SetActive(false);
        }

        /// <summary>A column's heading, in gold where the tagline stands on the main menu.</summary>
        private static TextMeshProUGUI Heading(Transform parent) =>
            TitleKit.Label(parent, "", 26f, TitleKit.Gold, TitleKit.SmallSpacing, Middle, TaglineY + TaglineGap * 0.5f);

        private void BuildDifficulty()
        {
            difficulty = new TitleList(
                stage,
                "Difficulty",
                ItemsY,
                WideColumnWidth,
                theme.focusBarWide,
                theme.ruleItem
            );
            Heading(difficulty.Root).text = "NEW GAME:  CHOOSE A DIFFICULTY";
            for (var which = 0; which < difficultyButtons.Length; which++)
            {
                var index = which;
                var text = $"{SaveManager.DifficultyNames[which]}    {SaveManager.DifficultyNotes[which]}";
                difficultyButtons[which] = difficulty.Add(text, () => OnDifficultyPressed(index), false);
            }
            difficulty.Add("Back", OnDifficultyBackPressed, true, TitleList.BackSound);
            MenuKit.LinkVertical(difficulty.Layout());
            var below = ItemsY + difficulty.Items.Count * TitleKit.Step;
            TitleKit.Label(
                difficulty.Root,
                "The difficulty is kept with the save.",
                24f,
                TitleKit.Faded,
                4f,
                Middle,
                below
            );
            difficulty.Root.gameObject.SetActive(false);
        }

        private void BuildCredits()
        {
            var page = UiKit.Rect(stage, "Credits");
            UiKit.Fill(page);
            credits = page.gameObject;
            TitleKit.Label(page, "CREDITS", 64f, TitleKit.Gold, TitleKit.CapsSpacing, Middle, 160f);
            TitleKit.Sprite(page, "Rule", theme.ruleTagline, Middle, 230f);
            for (var i = 0; i < Credits.Length; i++)
            {
                var color = i == 0 ? TitleKit.Ivory : TitleKit.Parchment;
                TitleKit.Label(page, Credits[i], 30f, color, 4f, Middle, 300f + i * 52f);
            }
            creditsList = new TitleList(page, "Back", 860f, ColumnWidth, theme.focusBar, theme.ruleItem);
            creditsBackButton = creditsList.Add("Back", ShowMenu, true, TitleList.BackSound);
            MenuKit.LinkVertical(creditsList.Layout());
            credits.SetActive(false);
        }

        protected override void OnOpened()
        {
            status.text = "";
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
            halo.color = new Color(1f, 1f, 1f, 0.85f + 0.15f * Mathf.Sin(now * 1.3f));
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
            tagline.SetActive(showTagline);
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
            slotsHeading.text = mode switch
            {
                SlotMode.NewGame => "NEW GAME:  CHOOSE A SLOT",
                SlotMode.Load => "LOAD GAME",
                _ => "DELETE A SAVE:  CHOOSE A SLOT",
            };
            // Deleting is offered from the load picker, and the delete picker goes back to loading.
            deleteButton.gameObject.SetActive(mode != SlotMode.NewGame);
            TitleKit.SetText(deleteButton, mode == SlotMode.Delete ? "Load instead" : "Delete a save");
            for (var slot = 0; slot < slotButtons.Length; slot++)
            {
                var button = slotButtons[slot];
                TitleKit.SetText(button, $"{slot + 1}    {SaveManager.Summary(slot, GameFlow.LevelNameOf)}");
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
            TitleKit.SetText(slotButtons[slot], $"{slot + 1}    {question}  Press again");
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
                status.text = reason;
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
                resume.text = $"Resumes slot {slot + 1}:    {SaveManager.Summary(slot, GameFlow.LevelNameOf)}";
                return;
            }
            var gamepad = UsingGamepad ? 1 : 0;
            if (keysFor != gamepad)
            {
                keysFor = gamepad;
                BuildKeys(gamepad == 1);
            }
        }

        /// <summary>
        /// The key hints along the bottom: the D-pad icon and Navigate, then a key cap
        /// and what it does twice, between two fading hairlines.
        /// </summary>
        private void BuildKeys(bool gamepad)
        {
            UiKit.Clear(keys);
            var row = UiKit.Rect(keys, "Row");
            UiKit.Fill(row);
            var x = 0f;
            var parts = new System.Collections.Generic.List<RectTransform>();
            if (theme.keyDpad != null)
            {
                var pad = TitleKit.Sprite(row, "Dpad", theme.keyDpad, 0f, HintsY);
                pad.rectTransform.anchoredPosition = new Vector2(x + 22f, -HintsY);
                parts.Add(pad.rectTransform);
                x += 44f + 14f;
            }
            x = Hint(row, parts, "Navigate", x) + 56f;
            x = Hint(row, parts, "Confirm", x + KeyCapInto(row, parts, gamepad ? "A" : "Enter", x) + 14f) + 56f;
            x = Hint(row, parts, "Back", x + KeyCapInto(row, parts, gamepad ? "B" : "Esc", x) + 14f);
            // Centre the whole row: everything was laid out from 0, so shift it by half what is left.
            var shift = Middle - x * 0.5f;
            foreach (var part in parts)
            {
                part.anchoredPosition += new Vector2(shift, 0f);
            }
            if (theme.ruleSide == null)
            {
                return;
            }
            var reach = theme.ruleSide.rect.width * 0.5f + 40f;
            TitleKit.Sprite(keys, "Line", theme.ruleSide, shift - reach, HintsY);
            var right = TitleKit.Sprite(keys, "Line", theme.ruleSide, shift + x + reach, HintsY);
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        /// <summary>A key cap at `x`, added to the row's parts; returns its width.</summary>
        private float KeyCapInto(
            Transform row,
            System.Collections.Generic.List<RectTransform> parts,
            string key,
            float x
        )
        {
            var w = TitleKit.KeyCap(row, theme.keyCap, key, x, HintsY);
            parts.Add((RectTransform)row.GetChild(row.childCount - 1));
            return w;
        }

        /// <summary>What a key does, its left edge at `x`; returns where it ends.</summary>
        private static float Hint(
            Transform row,
            System.Collections.Generic.List<RectTransform> parts,
            string text,
            float x
        )
        {
            var label = TitleKit.Label(row, text, 26f, TitleKit.Muted, 4f, 0f, HintsY, 400f);
            var w = TitleKit.WidthOf(label);
            TitleKit.Center(label.rectTransform, x + w * 0.5f, HintsY, w + 4f, 52f);
            parts.Add(label.rectTransform);
            return x + w;
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
