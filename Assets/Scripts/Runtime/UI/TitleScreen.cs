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
        private const float LogoY = 245f;

        // The halo's ring is centred this far under the logo's middle; its star then sits
        // about 60 px from the top, so nothing of it leaves the frame.
        private const float RingBelowLogo = 30f;

        // Where the ring's centre is in the halo image, as a share of its height (tools/title_textures.py).
        private const float HaloRing = 0.62f;
        private const float TaglineY = 352f;
        private const float TaglineGap = 23f;
        private const float RuleY = 408f;
        private const float ItemsY = 478f;
        private const float HintsY = TitleHints.Y;
        private const float ColumnWidth = 820f;
        private const float WideColumnWidth = 1300f;
        private const float CornerSize = 21f;

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
            var haloHeight = TitleKit.SizeOf(theme.titleHalo).y;
            var haloY = LogoY + RingBelowLogo - (HaloRing - 0.5f) * haloHeight;
            halo = TitleKit.Sprite(top, "Halo", theme.titleHalo, Middle, haloY);
            TitleKit.Sprite(top, "Logo", theme.titleLogo, Middle, LogoY);
            TitleKit.Sprite(top, "Rule", theme.ruleTagline, Middle, RuleY);
            BuildTagline(top);

            var version = TitleKit.Label(
                stage,
                "v" + Application.version,
                CornerSize,
                TitleKit.Faded,
                4f,
                0f,
                44f,
                200f
            );
            version.alignment = TextAlignmentOptions.Right;
            TitleKit.Center(version.rectTransform, TitleKit.Width - 148f, 44f, 200f, 44f);

            status = TitleKit.Label(stage, "", TitleKit.SmallSize, TitleKit.Bad, 2f, 0f, 44f, 1200f);
            status.alignment = TextAlignmentOptions.Left;
            TitleKit.Center(status.rectTransform, 48f + 600f, 44f, 1200f, 40f);

            keys = UiKit.Rect(stage, "Keys");
            UiKit.Fill(keys);
            resume = TitleKit.Label(stage, "", 19f, TitleKit.Muted, 6f, Middle, HintsY);

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
                    var half = TitleKit.WidthOf(line) * 0.5f + 20f + TitleKit.SizeOf(theme.ruleSide).x * 0.5f;
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
            TitleKit.Label(
                parent,
                "",
                TitleKit.HeadingSize,
                TitleKit.Gold,
                TitleKit.SmallSpacing,
                Middle,
                TaglineY + TaglineGap * 0.5f
            );

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
                19f,
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
            TitleKit.Label(page, "CREDITS", 51f, TitleKit.Gold, TitleKit.CapsSpacing, Middle, 160f);
            TitleKit.Sprite(page, "Rule", theme.ruleTagline, Middle, 218f);
            for (var i = 0; i < Credits.Length; i++)
            {
                var color = i == 0 ? TitleKit.Ivory : TitleKit.Parchment;
                TitleKit.Label(page, Credits[i], 24f, color, 4f, Middle, 286f + i * 44f);
            }
            creditsList = new TitleList(page, "Back", 820f, ColumnWidth, theme.focusBar, theme.ruleItem);
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
                TitleHints.Build(keys, theme, gamepad == 1, "Confirm", "Back");
            }
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
