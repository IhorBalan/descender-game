using System.Collections.Generic;
using Descender.Input;
using Descender.Save;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The settings screen, in the title's gilded look on its dark ground with the
    /// embers rising. Four tabs along the top (Game, Controls, Display, Audio), turned
    /// with Q and E or the shoulder buttons; each has sections in the left column
    /// (Gameplay and Interface; Movement and Actions; Screen and Effects; Volume and
    /// Voices), the chosen section's settings in the middle, and what the focused one
    /// does on the right. Up and down move, left and right change a value, Enter
    /// changes it too, Esc goes from the settings to the sections and from there out.
    /// F (Y on a pad) puts the tab's settings back to their defaults. Pressing a key
    /// binding waits for the next key or pad button; Esc cancels, and so does waiting
    /// out the listen time. Every change is applied at once and written to the
    /// settings file. Shared by the title and the pause menu, which open it and
    /// listen for Closed.
    /// </summary>
    public sealed class SettingsScreen : MenuScreen
    {
        public const float ListenTime = 5f;

        /// <summary>How far a slider moves for one press of left or right.</summary>
        public const float SliderStep = 0.05f;

        /// <summary>How long left or right is held before a slider starts to run, and how often it then steps.</summary>
        public const float RepeatDelay = 0.35f;
        public const float RepeatEvery = 0.05f;

        // Places on the 1920 x 1080 stage, in pixels from its top left.
        private const float Middle = TitleKit.Width * 0.5f;
        private const float HeaderY = 92f;
        private const float HeaderRuleY = 136f;
        private const float TabsY = 190f;
        private const float TabsRuleY = 236f;
        private const float SideX = 290f;
        private const float RowsX = 940f;
        private const float RowsTop = 397f;
        private const float HeadingY = 300f;
        private const float InfoLeft = 1420f;
        private const float InfoWidth = 380f;
        private const float FooterRuleY = 958f;
        private const float FooterY = 1010f;
        private const float ColumnsY = 590f;

        private static readonly float[] TabX = { 420f, 640f, 860f, 1080f };

        private readonly List<Tab> tabs = new List<Tab>();
        private readonly Dictionary<GameObject, SettingsRow> rows = new Dictionary<GameObject, SettingsRow>();
        private readonly Dictionary<string, SettingsRow> keyRows = new Dictionary<string, SettingsRow>();
        private RectTransform stage;
        private CanvasGroup stageGroup;
        private TitleFocus focus;
        private Image tabGlow;
        private Image sideBar;
        private TextMeshProUGUI heading;
        private TextMeshProUGUI infoTitle;
        private TextMeshProUGUI infoBody;
        private RectTransform footer;
        private int tab;
        private Section section;
        private GameObject lastSelected;
        private int footerFor = -1;
        private float openedAt;
        private float shownAt;
        private float sideY;
        private bool gamepadBindings;

        private InputAction pageLeft;
        private InputAction pageRight;
        private InputAction navigate;
        private InputAction reset;
        private int heldStep;
        private float nextRepeat;
        private string listeningAction = "";
        private InputActionRebindingExtensions.RebindingOperation rebind;

        protected override int SortingOrder => 30;

        private static SettingsData Settings => SettingsData.Current;

        private SettingsLook Look => theme.settingsLook;

        /// <summary>The tab on show: 0 Game, 1 Controls, 2 Display, 3 Audio.</summary>
        public int TabIndex => tab;

        /// <summary>The section on show, by name ("Gameplay", "Volume"...).</summary>
        public string SectionName => section != null ? section.Name : "";

        /// <summary>The rows of the section on show, in order.</summary>
        public IReadOnlyList<SettingsRow> Rows => section != null ? section.Rows : new List<SettingsRow>();

        /// <summary>The section entries of the tab on show, in order.</summary>
        public IReadOnlyList<SettingsRow> Sections
        {
            get
            {
                var list = new List<SettingsRow>();
                foreach (var each in tabs[tab].Sections)
                {
                    list.Add(each.Entry);
                }
                return list;
            }
        }

        /// <summary>What the description panel says now.</summary>
        public string InfoTitle => infoTitle != null ? infoTitle.text : "";

        /// <summary>The bar behind the focused setting.</summary>
        public TitleFocus Focus => focus;

        protected override void Build(RectTransform root)
        {
            if (TitleKit.Font == null)
            {
                TitleKit.Font = theme.titleFont;
            }
            TitleScreen.ScaleSmooth(root.GetComponent<Canvas>());
            var ground = UiKit.Box(root, "Background", TitleKit.Background);
            UiKit.Fill(ground.rectTransform);
            stage = UiKit.Rect(root, "Stage");
            stage.anchorMin = new Vector2(0.5f, 0.5f);
            stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(TitleKit.Width, TitleKit.Height);
            stageGroup = stage.gameObject.AddComponent<CanvasGroup>();
            TitleEmbers.Make(stage, theme.mote, 11);

            BuildFrame();
            BuildTabs();
            BuildGame();
            BuildControls();
            BuildDisplay();
            BuildAudio();
            focus = TitleFocus.Make(stage, null);
        }

        /// <summary>The heading, the rules and the column lines, and the description panel.</summary>
        private void BuildFrame()
        {
            TitleKit.Sprite(stage, "Emblem", Look.emblem, 142f, HeaderY);
            var title = TitleKit.Label(stage, "SETTINGS", 42f, TitleKit.Ivory, 40f, 0f, HeaderY, 900f);
            title.alignment = TextAlignmentOptions.Left;
            TitleKit.Center(title.rectTransform, 206f + 450f, HeaderY, 900f, 70f);
            TitleKit.Sprite(stage, "Rule", Look.ruleLong, Middle, HeaderRuleY);
            TitleKit.Sprite(stage, "Rule", Look.rulePlain, Middle, TabsRuleY);
            TitleKit.Sprite(stage, "Line", Look.ruleVertical, 482f, ColumnsY);
            TitleKit.Sprite(stage, "Line", Look.ruleVertical, 1384f, ColumnsY);
            TitleKit.Sprite(stage, "Rule", Look.ruleLong, Middle, FooterRuleY);

            sideBar = TitleKit.Sprite(stage, "Section", Look.sideBar, SideX, RowsTop);

            heading = TitleKit.Label(stage, "", 21f, TitleKit.Muted, 50f, 0f, HeadingY, 800f);
            heading.alignment = TextAlignmentOptions.Left;
            TitleKit.Center(heading.rectTransform, 556f + 400f, HeadingY, 800f, 50f);
            var under = TitleKit.Sprite(stage, "Rule", Look.ruleRow, RowsX, HeadingY + 34f);
            under.rectTransform.sizeDelta = new Vector2(SettingsRow.RowWidth, under.rectTransform.sizeDelta.y);

            infoTitle = TitleKit.Label(stage, "", 28f, TitleKit.Ivory, 2f, 0f, RowsTop, InfoWidth);
            infoTitle.alignment = TextAlignmentOptions.Left;
            TitleKit.Center(infoTitle.rectTransform, InfoLeft + InfoWidth * 0.5f, RowsTop - 4f, InfoWidth, 50f);
            var infoRule = TitleKit.Sprite(stage, "Rule", Look.ruleRow, InfoLeft + InfoWidth * 0.5f, RowsTop + 30f);
            infoRule.rectTransform.sizeDelta = new Vector2(InfoWidth + 20f, infoRule.rectTransform.sizeDelta.y);
            infoBody = TitleKit.Label(stage, "", 20f, TitleKit.Parchment, 2f, 0f, 0f, InfoWidth);
            infoBody.alignment = TextAlignmentOptions.TopLeft;
            infoBody.textWrappingMode = TextWrappingModes.Normal;
            infoBody.lineSpacing = 18f;
            TitleKit.Center(infoBody.rectTransform, InfoLeft + InfoWidth * 0.5f, RowsTop + 50f + 150f, InfoWidth, 300f);

            footer = UiKit.Rect(stage, "Footer");
            UiKit.Fill(footer);
        }

        private void BuildTabs()
        {
            var names = new[] { "Game", "Controls", "Display", "Audio" };
            tabGlow = TitleKit.Sprite(stage, "Chosen", Look.tabGlow, TabX[0], TabsY + 30f);
            TitleKit.KeyCap(stage, theme.keyCap, "Q", 230f, TabsY);
            TitleKit.KeyCap(stage, theme.keyCap, "E", 1190f, TabsY);
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var rect = UiKit.Rect(stage, names[i]);
                var hit = rect.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => ShowTab(index, true));
                TitleKit.Center(rect, TabX[i], TabsY, 200f, 60f);
                var label = TitleKit.Label(rect, names[i], 24f, TitleKit.Parchment, 4f, 100f, 30f, 200f);
                tabs.Add(new Tab { Name = names[i], Label = label });
            }
        }

        private void BuildGame()
        {
            var game = tabs[0];
            game.Reset = () => Settings.ResetGame();
            var play = AddSection(
                game,
                "Gameplay",
                Look.iconGameplay,
                "How the descent feels: how fast words are written, and the flash, the pause and the rumble that mark every blow."
            );
            Cycle(
                play,
                "Text speed",
                "How fast the words of a conversation are written out, from slow to all at once.",
                () => Settings.TextSpeedName(),
                OnTextSpeedStepped
            );
            Toggle(
                play,
                "Hit flashes",
                "Struck foes and the hero flash white for a moment. Turn it off if bright flashes trouble you.",
                () => Settings.hitFlash,
                () => Settings.hitFlash = !Settings.hitFlash,
                false
            );
            Toggle(
                play,
                "Hit pause",
                "The action holds still for a split second when a blow lands, so every hit is felt.",
                () => Settings.hitStop,
                () => Settings.hitStop = !Settings.hitStop,
                false
            );
            Toggle(
                play,
                "Gamepad rumble",
                "The controller rumbles on hits, hard landings and deaths.",
                () => Settings.rumble,
                () => Settings.rumble = !Settings.rumble,
                false
            );
            var face = AddSection(game, "Interface", Look.iconInterface, "How the game shows you its controls.");
            Cycle(
                face,
                "Button prompts",
                "Which buttons the hints show: those of the pad in use, or always Xbox's or PlayStation's.",
                () => SettingsData.LayoutNames[(int)Settings.prompts],
                OnPromptsStepped
            );
        }

        private void BuildControls()
        {
            var controls = tabs[1];
            controls.Reset = () =>
            {
                StopListening();
                if (InputRouter.Instance != null)
                {
                    InputRouter.Instance.ResetBindings();
                }
            };
            var move = AddSection(
                controls,
                "Movement",
                Look.iconMovement,
                "The keys or buttons that move Ansel through the world. Choose one and press the new key."
            );
            var act = AddSection(
                controls,
                "Actions",
                Look.iconActions,
                "The keys or buttons for fighting, healing, talking and pausing."
            );
            foreach (var group in new[] { move, act })
            {
                Cycle(
                    group,
                    "Bindings for",
                    "Whose bindings these rows show and change: the keyboard's or the gamepad's.",
                    () => gamepadBindings ? "Gamepad" : "Keyboard",
                    _ => OnDeviceStepped()
                );
            }
            for (var i = 0; i < SettingsData.Rebindable.Length; i++)
            {
                var (action, label) = SettingsData.Rebindable[i];
                var group = i < 4 || action == "Jump" || action == "Dash" ? move : act;
                var name = action;
                var help =
                    $"Press to choose a new key or button for {label.ToLowerInvariant()}. Esc keeps the old one, and so does waiting {ListenTime:0} seconds.";
                var row = AddRow(group, SettingsRow.Kind.Key, label, help);
                row.Bind(() => KeyText(name), null, null);
                row.Button.onClick.AddListener(() => OnKeyPressed(name));
                keyRows[name] = row;
            }
        }

        private void BuildDisplay()
        {
            var display = tabs[2];
            display.Reset = () => Settings.ResetDisplay();
            var screen = AddSection(
                display,
                "Screen",
                Look.iconScreen,
                "The window, its size, and how it keeps time with your display."
            );
            Toggle(
                screen,
                "Fullscreen",
                "Fill the whole screen, or play in a window.",
                () => Settings.fullscreen,
                () => Settings.fullscreen = !Settings.fullscreen,
                true
            );
            Cycle(
                screen,
                "Window size",
                "How large the window is when the game is not in fullscreen, in whole multiples of its picture.",
                WindowText,
                OnScaleStepped
            );
            Toggle(
                screen,
                "V-sync",
                "Wait for the display before each frame, so the picture never tears.",
                () => Settings.vsync,
                () => Settings.vsync = !Settings.vsync,
                true
            );
            var effects = AddSection(
                display,
                "Effects",
                Look.iconEffects,
                "How the screen itself answers the fighting."
            );
            Cycle(
                effects,
                "Screen shake",
                "How hard the screen shakes when blows land and bosses roar. Lower it, or turn it off, if the motion bothers you.",
                () => SettingsData.ShakeName(Settings.shake),
                OnShakeStepped
            );
        }

        private void BuildAudio()
        {
            var audio = tabs[3];
            audio.Reset = () => Settings.ResetAudio();
            var volume = AddSection(
                audio,
                "Volume",
                Look.iconVolume,
                "The loudness of everything, of the music and of the sound of the fighting."
            );
            Slider(
                volume,
                "Master volume",
                "The loudness of everything the game plays.",
                () => Settings.volume,
                v => Settings.volume = v
            );
            Slider(
                volume,
                "Music volume",
                "The loudness of the music under each region and boss.",
                () => Settings.music,
                v => Settings.music = v
            );
            Slider(
                volume,
                "Effects volume",
                "The loudness of blows, steps, pickups and the menus.",
                () => Settings.sfx,
                v => Settings.sfx = v
            );
            var voices = AddSection(audio, "Voices", Look.iconVoices, "The voices of the people Ansel meets.");
            Toggle(
                voices,
                "Dialogue blips",
                "The short sounds that speak each line as it is written.",
                () => Settings.blips,
                () => Settings.blips = !Settings.blips,
                false
            );
        }

        private Section AddSection(Tab owner, string name, Sprite icon, string help)
        {
            var index = owner.Sections.Count;
            if (owner.Side == null)
            {
                owner.Side = UiKit.Rect(stage, owner.Name + " Sections");
                UiKit.Fill(owner.Side);
            }
            var entry = SettingsRow.Make(owner.Side, Look, SettingsRow.Kind.Section, name, help, icon);
            TitleKit.Center(
                (RectTransform)entry.transform,
                SideX,
                RowsTop + index * 70f,
                SettingsRow.SectionWidth,
                TitleKit.Step
            );
            var list = new TitleList(stage, name, RowsTop, SettingsRow.RowWidth, Look.rowBar, Look.ruleRow, RowsX);
            list.Root.gameObject.SetActive(false);
            var made = new Section
            {
                Name = name,
                Help = help,
                Entry = entry,
                List = list,
                Owner = owner,
            };
            entry.Button.onClick.AddListener(() => EnterRows(made));
            rows[entry.gameObject] = entry;
            owner.Sections.Add(made);
            return made;
        }

        private SettingsRow AddRow(Section owner, SettingsRow.Kind kind, string label, string help)
        {
            var row = SettingsRow.Make(stage, Look, kind, label, help);
            owner.List.Adopt(row.Button);
            owner.Rows.Add(row);
            rows[row.gameObject] = row;
            return row;
        }

        private void Cycle(Section owner, string label, string help, System.Func<string> text, System.Action<int> step)
        {
            var row = AddRow(owner, SettingsRow.Kind.Cycle, label, help);
            row.Bind(text, null, direction => Change(() => step(direction)));
            row.Button.onClick.AddListener(() => row.Step(1));
        }

        private void Toggle(
            Section owner,
            string label,
            string help,
            System.Func<bool> get,
            System.Action flip,
            bool apply
        )
        {
            var row = AddRow(owner, SettingsRow.Kind.Toggle, label, help);
            row.Bind(() => get() ? "On" : "Off", () => get() ? 1f : 0f, _ => Change(flip, apply));
            row.Button.onClick.AddListener(() => row.Step(1));
        }

        private void Slider(Section owner, string label, string help, System.Func<float> get, System.Action<float> set)
        {
            var row = AddRow(owner, SettingsRow.Kind.Slider, label, help);
            row.Bind(
                () => Mathf.RoundToInt(get() * 100f).ToString(),
                get,
                direction =>
                    Change(() => set(Mathf.Clamp01(Mathf.Round((get() + direction * SliderStep) * 100f) / 100f))),
                place => Change(() => set(Mathf.Round(place * 100f) / 100f))
            );
        }

        /// <summary>Makes a change, applies it when it touches the display or the sound, and saves it.</summary>
        private void Change(System.Action change, bool apply = true)
        {
            change();
            if (apply)
            {
                SettingsApplier.Apply(Settings);
            }
            Settings.Save();
            Refresh();
        }

        protected override void OnOpened()
        {
            StopListening();
            var router = InputRouter.Instance;
            if (router != null && router.Actions != null)
            {
                var ui = router.Actions.FindActionMap("UI", true);
                pageLeft = ui.FindAction("PageLeft", true);
                pageRight = ui.FindAction("PageRight", true);
                navigate = ui.FindAction("Navigate", true);
                reset = ui.FindAction("Reset", false);
            }
            gamepadBindings = UsingGamepad;
            openedAt = Time.unscaledTime;
            stageGroup.alpha = 0f;
            footerFor = -1;
            ShowTab(tab, true);
        }

        protected override void OnClosed()
        {
            StopListening();
            Settings.Save();
        }

        public override void OnCancel()
        {
            if (listeningAction.Length > 0)
            {
                StopListening();
                return;
            }
            Audio.Sfx.Play(TitleList.BackSound);
            if (section != null && section.List.Contains(Selected))
            {
                Select(section.Entry.Button);
                return;
            }
            Close();
        }

        public override void OnTop()
        {
            if (footerFor != (UsingGamepad ? 1 : 0))
            {
                BuildFooter();
            }
            if (listeningAction.Length > 0)
            {
                return;
            }
            if (pageLeft != null && pageLeft.WasPressedThisFrame())
            {
                TurnTab(-1);
            }
            else if (pageRight != null && pageRight.WasPressedThisFrame())
            {
                TurnTab(1);
            }
            else if (reset != null && reset.WasPressedThisFrame())
            {
                ResetTab();
            }
            var selected = Selected;
            if (selected != lastSelected)
            {
                OnFocusMoved(selected);
            }
            StepHeld(navigate != null ? navigate.ReadValue<Vector2>().x : 0f);
        }

        /// <summary>Left and right change the focused setting: once on a press, and a slider runs while held.</summary>
        private void StepHeld(float x)
        {
            var direction = Mathf.Abs(x) > 0.5f ? (int)Mathf.Sign(x) : 0;
            var now = Time.unscaledTime;
            var fire = false;
            if (direction != heldStep)
            {
                heldStep = direction;
                nextRepeat = now + RepeatDelay;
                fire = direction != 0;
            }
            else if (direction != 0 && now >= nextRepeat)
            {
                nextRepeat = now + RepeatEvery;
                fire =
                    Selected != null
                    && rows.TryGetValue(Selected, out var held)
                    && held.Type == SettingsRow.Kind.Slider;
            }
            if (fire && Selected != null && rows.TryGetValue(Selected, out var row) && row.Adjusts)
            {
                StepRow(row, direction);
            }
        }

        /// <summary>Moves a setting one step, as left or right does.</summary>
        public void StepRow(SettingsRow row, int direction)
        {
            row.Step(direction);
            Audio.Sfx.Play("menu_move", -8f);
        }

        private void OnFocusMoved(GameObject selected)
        {
            var moved = lastSelected != null;
            lastSelected = selected;
            if (selected == null || !rows.TryGetValue(selected, out var row))
            {
                return;
            }
            if (moved)
            {
                Audio.Sfx.Play("menu_move", -6f);
            }
            if (row.Type == SettingsRow.Kind.Section)
            {
                var chosen = FindSection(row);
                if (chosen != null && chosen != section)
                {
                    ShowSection(chosen, false);
                }
            }
            Describe(row);
        }

        private Section FindSection(SettingsRow entry)
        {
            foreach (var each in tabs[tab].Sections)
            {
                if (each.Entry == entry)
                {
                    return each;
                }
            }
            return null;
        }

        private void Describe(SettingsRow row)
        {
            infoTitle.text = row.Label;
            infoBody.text = row.Help;
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            var now = Time.unscaledTime;
            stageGroup.alpha = TitleScreen.Smooth((now - openedAt) / 0.4f);
            if (section != null)
            {
                section.List.Group.alpha = TitleScreen.Smooth((now - shownAt) / TitleScreen.ColumnFadeSeconds);
                var target = RowsTop + section.Owner.Sections.IndexOf(section) * 70f;
                sideY = TitleFocus.Ease(sideY, target, Mathf.Min(Time.unscaledDeltaTime, TitleFocus.MaxStep));
                sideBar.rectTransform.anchoredPosition = new Vector2(SideX, -sideY);
            }
            var glow = tabGlow.rectTransform;
            var x = TitleFocus.Ease(
                glow.anchoredPosition.x,
                TabX[tab],
                Mathf.Min(Time.unscaledDeltaTime, TitleFocus.MaxStep)
            );
            glow.anchoredPosition = new Vector2(x, glow.anchoredPosition.y);
        }

        /// <summary>Shows a tab with its first section, the focus on that section's first setting.</summary>
        public void ShowTab(int index, bool focusRows)
        {
            tab = (index % tabs.Count + tabs.Count) % tabs.Count;
            for (var i = 0; i < tabs.Count; i++)
            {
                tabs[i].Side.gameObject.SetActive(i == tab);
                tabs[i].Label.color = i == tab ? TitleKit.Ivory : TitleKit.Parchment;
            }
            var first = tabs[tab].Sections[0];
            sideY = RowsTop;
            ShowSection(first, focusRows);
        }

        /// <summary>Turns to the tab `step` along, as Q and E do.</summary>
        public void TurnTab(int step)
        {
            StopListening();
            Audio.Sfx.Play("menu_move", -4f);
            ShowTab(tab + step, true);
        }

        /// <summary>Puts the tab's settings back to their defaults, as F does.</summary>
        public void ResetTab()
        {
            tabs[tab].Reset?.Invoke();
            Audio.Sfx.Play(TitleList.ConfirmSound, -3f);
            SettingsApplier.Apply(Settings);
            Settings.Save();
            Refresh();
        }

        private void ShowSection(Section shown, bool focusRows)
        {
            if (section != null)
            {
                section.List.Root.gameObject.SetActive(false);
            }
            section = shown;
            foreach (var each in tabs[tab].Sections)
            {
                each.Entry.Current = each == shown;
            }
            var list = shown.List;
            list.Root.gameObject.SetActive(true);
            var items = list.Layout();
            MenuKit.LinkVertical(items);
            // Rows whose value left and right cannot change go left to the sections.
            foreach (var row in shown.Rows)
            {
                var navigation = row.Button.navigation;
                navigation.selectOnLeft = row.Adjusts ? null : shown.Entry.Button;
                row.Button.navigation = navigation;
            }
            var entries = new List<Selectable>();
            foreach (var each in tabs[tab].Sections)
            {
                entries.Add(each.Entry.Button);
            }
            MenuKit.LinkVertical(entries);
            foreach (var each in tabs[tab].Sections)
            {
                var navigation = each.Entry.Button.navigation;
                navigation.selectOnRight = each.Rows[0].Button;
                each.Entry.Button.navigation = navigation;
            }
            heading.text = shown.Name.ToUpperInvariant();
            shownAt = Time.unscaledTime;
            list.Group.alpha = 0f;
            focus.Follow(list, false);
            var target = focusRows ? shown.Rows[0] : shown.Entry;
            Select(target.Button);
            lastSelected = target.gameObject;
            Describe(target);
            Refresh();
        }

        private void EnterRows(Section chosen)
        {
            if (chosen != section)
            {
                ShowSection(chosen, true);
                return;
            }
            Select(chosen.Rows[0].Button);
        }

        private void Refresh()
        {
            foreach (var row in rows.Values)
            {
                row.Refresh();
            }
            footerFor = -1;
        }

        /// <summary>The hints along the bottom: going back and resetting on the left, changing on the right.</summary>
        private void BuildFooter()
        {
            var pad = UsingGamepad;
            footerFor = pad ? 1 : 0;
            UiKit.Clear(footer);
            if (listeningAction.Length > 0)
            {
                var what = gamepadBindings ? "button" : "key";
                TitleKit.Label(
                    footer,
                    $"Press the new {what}, or Esc to keep the old one",
                    21f,
                    TitleKit.Muted,
                    4f,
                    Middle,
                    FooterY
                );
                return;
            }
            var x = 120f;
            x = Hint(x, pad ? "B" : "Esc", "Back") + 54f;
            Hint(x, pad ? "Y" : "F", "Reset to Defaults");
            var change = "Change";
            var probe = TitleKit.Label(footer, change, 21f, TitleKit.Muted, 4f, 0f, FooterY, 400f);
            var w = TitleKit.WidthOf(probe);
            Object.Destroy(probe.gameObject);
            var cap = pad ? "A" : "Enter";
            var capProbe = TitleKit.Label(footer, cap, TitleKit.SmallSize, TitleKit.Muted, 4f, 0f, FooterY, 400f);
            var capWidth = Mathf.Max(TitleKit.KeyHeight, TitleKit.WidthOf(capProbe) + 22f);
            Object.Destroy(capProbe.gameObject);
            Hint(TitleKit.Width - 120f - w - 12f - capWidth, cap, change);
        }

        /// <summary>A key cap and what it does, from `x`; returns where it ends.</summary>
        private float Hint(float x, string key, string does)
        {
            x += TitleKit.KeyCap(footer, theme.keyCap, key, x, FooterY) + 12f;
            var label = TitleKit.Label(footer, does, 21f, TitleKit.Muted, 4f, 0f, FooterY, 400f);
            var w = TitleKit.WidthOf(label);
            TitleKit.Center(label.rectTransform, x + w * 0.5f, FooterY, w + 4f, 44f);
            return x + w;
        }

        private static string WindowText()
        {
            var (width, height) = Settings.WindowSizeFor(Settings.scale);
            return $"{Settings.scale}x   {width} x {height}";
        }

        private string KeyText(string action)
        {
            if (action == listeningAction)
            {
                return gamepadBindings ? "Press a button..." : "Press a key...";
            }
            return BindingNames(action);
        }

        /// <summary>The keys or pad buttons bound to an action for the shown device, comma separated.</summary>
        private string BindingNames(string actionName)
        {
            var action = FindPlayerAction(actionName);
            if (action == null)
            {
                return "";
            }
            var group = gamepadBindings ? "Gamepad" : "Keyboard";
            var names = new List<string>();
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.groups == null || !binding.groups.Contains(group))
                {
                    continue;
                }
                var text = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                if (!string.IsNullOrEmpty(text))
                {
                    names.Add(text);
                }
            }
            return names.Count > 0 ? string.Join(", ", names) : "none";
        }

        private static InputAction FindPlayerAction(string actionName)
        {
            var router = InputRouter.Instance;
            if (router == null || router.Actions == null)
            {
                return null;
            }
            return router.Actions.FindActionMap("Player", true).FindAction(actionName, false);
        }

        private void OnScaleStepped(int step)
        {
            var range = SettingsData.MaxWindowScale - SettingsData.MinWindowScale + 1;
            var index = Settings.scale - SettingsData.MinWindowScale;
            Settings.scale = SettingsData.MinWindowScale + ((index + step) % range + range) % range;
        }

        private void OnShakeStepped(int step)
        {
            var levels = SettingsData.ShakeLevels;
            var index = System.Array.IndexOf(levels, Settings.shake);
            if (index < 0)
            {
                index = levels.Length - 1;
            }
            Settings.shake = levels[((index + step) % levels.Length + levels.Length) % levels.Length];
        }

        private void OnTextSpeedStepped(int step)
        {
            var count = SettingsData.TextSpeeds.Length;
            Settings.textSpeed = ((Settings.textSpeed + step) % count + count) % count;
        }

        private void OnPromptsStepped(int step)
        {
            var count = SettingsData.LayoutNames.Length;
            Settings.prompts = (PromptLayout)((((int)Settings.prompts + step) % count + count) % count);
        }

        private void OnDeviceStepped()
        {
            StopListening();
            gamepadBindings = !gamepadBindings;
        }

        private void OnKeyPressed(string actionName)
        {
            var action = FindPlayerAction(actionName);
            if (action == null)
            {
                return;
            }
            var group = gamepadBindings ? "Gamepad" : "Keyboard";
            var index = -1;
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite && binding.groups != null && binding.groups.Contains(group))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                return;
            }
            StopListening();
            listeningAction = actionName;
            var wasEnabled = action.enabled;
            action.Disable();
            rebind = action
                .PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(ListenTime)
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => OnRebindDone(operation, action, wasEnabled, true))
                .OnCancel(operation => OnRebindDone(operation, action, wasEnabled, false));
            if (gamepadBindings)
            {
                rebind.WithControlsExcluding("<Keyboard>");
            }
            else
            {
                rebind.WithControlsExcluding("<Gamepad>");
            }
            rebind.Start();
            Refresh();
        }

        private void OnRebindDone(
            InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action,
            bool wasEnabled,
            bool completed
        )
        {
            operation.Dispose();
            rebind = null;
            if (wasEnabled)
            {
                action.Enable();
            }
            if (completed && InputRouter.Instance != null)
            {
                InputRouter.Instance.SaveBindings();
            }
            listeningAction = "";
            Refresh();
        }

        private void StopListening()
        {
            if (rebind != null)
            {
                // Cancel calls OnRebindDone, which clears the state and refreshes.
                rebind.Cancel();
                return;
            }
            listeningAction = "";
            if (rows.Count > 0)
            {
                Refresh();
            }
        }

        /// <summary>A tab along the top: its sections, and what F puts back.</summary>
        private sealed class Tab
        {
            public string Name;
            public TextMeshProUGUI Label;
            public RectTransform Side;
            public System.Action Reset;
            public readonly List<Section> Sections = new List<Section>();
        }

        /// <summary>A section in the left column and the settings it shows in the middle.</summary>
        private sealed class Section
        {
            public string Name;
            public string Help;
            public SettingsRow Entry;
            public TitleList List;
            public Tab Owner;
            public readonly List<SettingsRow> Rows = new List<SettingsRow>();
        }
    }
}
