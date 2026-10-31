using System.Collections.Generic;
using Descender.Input;
using Descender.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The settings screen, in four pages: Audio (master, music and effect volumes,
    /// the dialogue blips), Display (fullscreen, window size, v-sync, screen shake),
    /// Controls (keyboard and gamepad bindings) and Game (text speed, hit flashes,
    /// hit pause, rumble, which pad's prompts the hints show). Shared by the title
    /// and the pause menu, which open it and listen for Closed. Q/E and the shoulder
    /// buttons turn pages. Pressing a binding button waits for the next key or pad
    /// button; Esc cancels, and so does waiting out the listen time. Every change is
    /// applied at once and written to the settings file. Port of
    /// scripts/ui/settings_menu.gd.
    /// </summary>
    public sealed class SettingsScreen : MenuScreen
    {
        public const float ListenTime = 5f;

        private readonly Button[] tabs = new Button[4];
        private readonly GameObject[] pages = new GameObject[4];
        private readonly Selectable[] firstControls = new Selectable[4];
        private Button backButton;
        private Text hint;
        private Page page = Page.Audio;
        private bool gamepadBindings;

        private Slider volumeSlider;
        private Slider musicSlider;
        private Slider sfxSlider;
        private Text volumeValue;
        private Text musicValue;
        private Text sfxValue;
        private Button blipsToggle;
        private Button fullscreenToggle;
        private Button scaleButton;
        private Button vsyncToggle;
        private Button shakeButton;
        private Button deviceButton;
        private Button resetKeysButton;
        private readonly Dictionary<string, Button> keyButtons = new Dictionary<string, Button>();
        private Button textSpeedButton;
        private Button hitFlashToggle;
        private Button hitStopToggle;
        private Button rumbleToggle;
        private Button promptsButton;
        private readonly HashSet<Button> cycleButtons = new HashSet<Button>();

        private InputAction pageLeft;
        private InputAction pageRight;
        private InputAction navigate;
        private float lastNavigateX;
        private string listeningAction = "";
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private bool refreshing;

        protected override int SortingOrder => 30;

        private static SettingsData Settings => SettingsData.Current;

        protected override void Build(RectTransform root)
        {
            var panel = MenuKit.MakePanel(root, theme);
            MenuKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 164f));

            var tabRow = MenuKit.MakeRow(root, "Tabs", 2f);
            MenuKit.Strip(tabRow.GetComponent<RectTransform>(), true, 12f, MenuKit.ButtonHeight);
            var names = new[] { "Audio", "Display", "Controls", "Game" };
            var tabItems = new List<Selectable>();
            for (var i = 0; i < tabs.Length; i++)
            {
                var which = (Page)i;
                tabs[i] = MenuKit.MakeButton(tabRow.transform, theme, names[i], () => OnTabPressed(which));
                MenuKit.Size(tabs[i], 52f);
                tabItems.Add(tabs[i]);
            }
            backButton = MenuKit.MakeButton(tabRow.transform, theme, "Back", Close);
            MenuKit.Size(backButton, 40f);
            tabItems.Add(backButton);
            MenuKit.LinkHorizontal(tabItems);

            hint = MenuKit.MakeLabel(root, theme, "", MenuTheme.MutedFontSize);
            hint.color = theme.textMuted;
            MenuKit.Strip(hint.rectTransform, false, 12f, 12f);

            BuildAudio(root);
            BuildDisplay(root);
            BuildControls(root);
            BuildGame(root);
        }

        private static VerticalLayoutGroup PageColumn(RectTransform root, string name)
        {
            var column = MenuKit.MakeColumn(root, name, 3f);
            column.childAlignment = TextAnchor.UpperLeft;
            column.childForceExpandWidth = false;
            var rect = column.GetComponent<RectTransform>();
            MenuKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(280f, 0f));
            return column;
        }

        /// <summary>A row with a label on the left and a control on the right.</summary>
        private Transform SettingRow(Transform column, string label)
        {
            var row = MenuKit.MakeRow(column, label, 4f);
            row.childAlignment = TextAnchor.MiddleLeft;
            var text = MenuKit.MakeLabel(row.transform, theme, label, MenuTheme.BaseFontSize, TextAnchor.MiddleLeft);
            MenuKit.Size(text, 110f);
            return row.transform;
        }

        private void BuildAudio(RectTransform root)
        {
            var column = PageColumn(root, "Audio");
            pages[(int)Page.Audio] = column.gameObject;
            volumeSlider = VolumeRow(column.transform, "Master volume", OnVolumeChanged, out volumeValue);
            musicSlider = VolumeRow(column.transform, "Music volume", OnMusicChanged, out musicValue);
            sfxSlider = VolumeRow(column.transform, "Effects volume", OnSfxChanged, out sfxValue);
            blipsToggle = MenuKit.MakeToggle(SettingRow(column.transform, "Dialogue blips"), theme, OnBlipsToggled);
            var defaults = MenuKit.MakeButton(column.transform, theme, "Defaults", OnAudioDefaultsPressed);
            MenuKit.Size(defaults, 60f);
            var items = new List<Selectable> { volumeSlider, musicSlider, sfxSlider, blipsToggle, defaults };
            LinkPage(items);
            firstControls[(int)Page.Audio] = volumeSlider;
        }

        private Slider VolumeRow(
            Transform column,
            string label,
            UnityEngine.Events.UnityAction<float> onChanged,
            out Text value
        )
        {
            var row = SettingRow(column, label);
            var slider = MenuKit.MakeSlider(row, theme, onChanged);
            value = MenuKit.MakeLabel(row, theme, "100%", MenuTheme.BaseFontSize, TextAnchor.MiddleRight);
            MenuKit.Size(value, 30f);
            return slider;
        }

        private void BuildDisplay(RectTransform root)
        {
            var column = PageColumn(root, "Display");
            pages[(int)Page.Display] = column.gameObject;
            fullscreenToggle = MenuKit.MakeToggle(
                SettingRow(column.transform, "Fullscreen"),
                theme,
                OnFullscreenToggled
            );
            scaleButton = Cycle(SettingRow(column.transform, "Window size"), () => OnScaleStepped(1));
            vsyncToggle = MenuKit.MakeToggle(SettingRow(column.transform, "V-sync"), theme, OnVsyncToggled);
            shakeButton = Cycle(SettingRow(column.transform, "Screen shake"), () => OnShakeStepped(1));
            var defaults = MenuKit.MakeButton(column.transform, theme, "Defaults", OnDisplayDefaultsPressed);
            MenuKit.Size(defaults, 60f);
            LinkPage(new List<Selectable> { fullscreenToggle, scaleButton, vsyncToggle, shakeButton, defaults });
            firstControls[(int)Page.Display] = fullscreenToggle;
        }

        private void BuildControls(RectTransform root)
        {
            var column = PageColumn(root, "Controls");
            column.spacing = 2f;
            pages[(int)Page.Controls] = column.gameObject;
            var top = MenuKit.MakeRow(column.transform, "KeysRow", 4f);
            deviceButton = MenuKit.MakeButton(top.transform, theme, "Keyboard", OnDevicePressed);
            MenuKit.Size(deviceButton, 70f);
            resetKeysButton = MenuKit.MakeButton(top.transform, theme, "Reset to defaults", OnResetKeysPressed);
            MenuKit.Size(resetKeysButton, 90f);
            MenuKit.LinkHorizontal(new List<Selectable> { deviceButton, resetKeysButton }, false);

            // Two actions per row: label, key, label, key.
            var left = new List<Selectable> { deviceButton };
            var right = new List<Selectable> { resetKeysButton };
            Transform row = null;
            for (var i = 0; i < SettingsData.Rebindable.Length; i++)
            {
                var (action, label) = SettingsData.Rebindable[i];
                if (i % 2 == 0)
                {
                    row = MenuKit.MakeRow(column.transform, "Keys" + i, 4f).transform;
                }
                var text = MenuKit.MakeLabel(row, theme, label, MenuTheme.BaseFontSize, TextAnchor.MiddleLeft);
                MenuKit.Size(text, 50f, 11f);
                var name = action;
                var button = MenuKit.MakeButton(row, theme, "", () => OnKeyButtonPressed(name));
                MenuKit.Size(button, 86f, 11f);
                keyButtons[action] = button;
                (i % 2 == 0 ? left : right).Add(button);
            }
            MenuKit.LinkVertical(left, false);
            MenuKit.LinkVertical(right, false);
            for (var i = 0; i < left.Count; i++)
            {
                var other = i < right.Count ? right[i] : right[right.Count - 1];
                var navigation = left[i].navigation;
                navigation.selectOnRight = other;
                left[i].navigation = navigation;
                if (i < right.Count)
                {
                    var back = right[i].navigation;
                    back.selectOnLeft = left[i];
                    right[i].navigation = back;
                }
            }
            LinkToTabs(left[0]);
            LinkToTabs(right[0]);
            firstControls[(int)Page.Controls] = deviceButton;
        }

        private void BuildGame(RectTransform root)
        {
            var column = PageColumn(root, "Game");
            pages[(int)Page.Game] = column.gameObject;
            textSpeedButton = Cycle(SettingRow(column.transform, "Text speed"), () => OnTextSpeedStepped(1));
            hitFlashToggle = MenuKit.MakeToggle(SettingRow(column.transform, "Hit flashes"), theme, OnHitFlashToggled);
            hitStopToggle = MenuKit.MakeToggle(SettingRow(column.transform, "Hit pause"), theme, OnHitStopToggled);
            rumbleToggle = MenuKit.MakeToggle(SettingRow(column.transform, "Gamepad rumble"), theme, OnRumbleToggled);
            promptsButton = Cycle(SettingRow(column.transform, "Button prompts"), () => OnPromptsStepped(1));
            var defaults = MenuKit.MakeButton(column.transform, theme, "Defaults", OnGameDefaultsPressed);
            MenuKit.Size(defaults, 60f);
            LinkPage(
                new List<Selectable>
                {
                    textSpeedButton,
                    hitFlashToggle,
                    hitStopToggle,
                    rumbleToggle,
                    promptsButton,
                    defaults,
                }
            );
            firstControls[(int)Page.Game] = textSpeedButton;
        }

        /// <summary>A button that steps a setting: pressed goes forward, left and right either way.</summary>
        private Button Cycle(Transform row, UnityEngine.Events.UnityAction onPressed)
        {
            var button = MenuKit.MakeButton(row, theme, "", onPressed);
            MenuKit.Size(button, 110f);
            cycleButtons.Add(button);
            return button;
        }

        private void LinkPage(List<Selectable> items)
        {
            MenuKit.LinkVertical(items, false);
            LinkToTabs(items[0]);
        }

        private void LinkToTabs(Selectable first)
        {
            var navigation = first.navigation;
            navigation.selectOnUp = tabs[(int)page];
            first.navigation = navigation;
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
            }
            gamepadBindings = UsingGamepad;
            ShowPage(page);
            Select(firstControls[(int)page]);
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
            Close();
        }

        public override void OnTop()
        {
            if (listeningAction.Length > 0)
            {
                return;
            }
            if (pageLeft != null && pageLeft.WasPressedThisFrame())
            {
                TurnPage(-1);
            }
            else if (pageRight != null && pageRight.WasPressedThisFrame())
            {
                TurnPage(1);
            }
            var x = navigate != null ? navigate.ReadValue<Vector2>().x : 0f;
            var step = Mathf.Abs(x) > 0.5f && Mathf.Abs(lastNavigateX) <= 0.5f ? (int)Mathf.Sign(x) : 0;
            lastNavigateX = x;
            if (step == 0 || Selected == null)
            {
                return;
            }
            if (Selected.TryGetComponent<Button>(out var button) && cycleButtons.Contains(button))
            {
                StepCycle(button, step);
            }
        }

        private void TurnPage(int step)
        {
            var count = pages.Length;
            ShowPage((Page)(((int)page + step + count) % count));
            Select(firstControls[(int)page]);
        }

        private void ShowPage(Page which)
        {
            page = which;
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == (int)which);
                var navigation = tabs[i].navigation;
                navigation.selectOnDown = firstControls[(int)which];
                tabs[i].navigation = navigation;
            }
            var back = backButton.navigation;
            back.selectOnDown = firstControls[(int)which];
            backButton.navigation = back;
            foreach (var first in firstControls)
            {
                if (first != null)
                {
                    var navigation = first.navigation;
                    navigation.selectOnUp = tabs[(int)which];
                    first.navigation = navigation;
                }
            }
            Refresh();
        }

        private void StepCycle(Button button, int step)
        {
            if (button == scaleButton)
            {
                OnScaleStepped(step);
            }
            else if (button == shakeButton)
            {
                OnShakeStepped(step);
            }
            else if (button == textSpeedButton)
            {
                OnTextSpeedStepped(step);
            }
            else if (button == promptsButton)
            {
                OnPromptsStepped(step);
            }
        }

        private void Refresh()
        {
            refreshing = true;
            var settings = Settings;
            volumeSlider.value = Mathf.Round(settings.volume * 100f);
            musicSlider.value = Mathf.Round(settings.music * 100f);
            sfxSlider.value = Mathf.Round(settings.sfx * 100f);
            volumeValue.text = Percent(settings.volume);
            musicValue.text = Percent(settings.music);
            sfxValue.text = Percent(settings.sfx);
            MenuKit.SetToggle(blipsToggle, theme, settings.blips);
            MenuKit.SetToggle(fullscreenToggle, theme, settings.fullscreen);
            MenuKit.SetToggle(vsyncToggle, theme, settings.vsync);
            var (width, height) = settings.WindowSizeFor(settings.scale);
            MenuKit.SetText(scaleButton, $"< {settings.scale}x  {width} x {height} >");
            MenuKit.SetText(shakeButton, $"< {SettingsData.ShakeName(settings.shake)} >");
            MenuKit.SetText(textSpeedButton, $"< {settings.TextSpeedName()} >");
            MenuKit.SetToggle(hitFlashToggle, theme, settings.hitFlash);
            MenuKit.SetToggle(hitStopToggle, theme, settings.hitStop);
            MenuKit.SetToggle(rumbleToggle, theme, settings.rumble);
            MenuKit.SetText(promptsButton, $"< {SettingsData.LayoutNames[(int)settings.prompts]} >");
            MenuKit.SetText(deviceButton, gamepadBindings ? "Gamepad" : "Keyboard");
            foreach (var pair in keyButtons)
            {
                if (pair.Key == listeningAction)
                {
                    MenuKit.SetText(pair.Value, gamepadBindings ? "Press a button..." : "Press a key...");
                }
                else
                {
                    MenuKit.SetText(pair.Value, BindingNames(pair.Key));
                }
            }
            if (listeningAction.Length == 0)
            {
                var pad = UsingGamepad;
                hint.text = pad
                    ? "D-pad move   A changes   LB RB switch pages   B goes back"
                    : "Arrows move   Enter changes   Q E switch pages   Esc goes back";
            }
            else
            {
                var what = gamepadBindings ? "gamepad button" : "key";
                hint.text = $"Press the new {what}, or wait to keep the old one";
            }
            refreshing = false;
        }

        private static string Percent(float linear) => $"{Mathf.RoundToInt(linear * 100f)}%";

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

        private void OnTabPressed(Page which)
        {
            ShowPage(which);
            Select(tabs[(int)which]);
        }

        private void OnVolumeChanged(float value)
        {
            if (refreshing)
            {
                return;
            }
            Settings.volume = value / 100f;
            Apply();
            volumeValue.text = Percent(Settings.volume);
        }

        private void OnMusicChanged(float value)
        {
            if (refreshing)
            {
                return;
            }
            Settings.music = value / 100f;
            Apply();
            musicValue.text = Percent(Settings.music);
        }

        private void OnSfxChanged(float value)
        {
            if (refreshing)
            {
                return;
            }
            Settings.sfx = value / 100f;
            Apply();
            sfxValue.text = Percent(Settings.sfx);
        }

        private void OnBlipsToggled()
        {
            Settings.blips = !Settings.blips;
            Save();
        }

        private void OnAudioDefaultsPressed()
        {
            Settings.ResetAudio();
            Apply();
        }

        private void OnFullscreenToggled()
        {
            Settings.fullscreen = !Settings.fullscreen;
            Apply();
        }

        private void OnVsyncToggled()
        {
            Settings.vsync = !Settings.vsync;
            Apply();
        }

        private void OnScaleStepped(int step)
        {
            var range = SettingsData.MaxWindowScale - SettingsData.MinWindowScale + 1;
            var index = Settings.scale - SettingsData.MinWindowScale;
            Settings.scale = SettingsData.MinWindowScale + ((index + step) % range + range) % range;
            Apply();
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
            Apply();
        }

        private void OnDisplayDefaultsPressed()
        {
            Settings.ResetDisplay();
            Apply();
        }

        private void OnTextSpeedStepped(int step)
        {
            var count = SettingsData.TextSpeeds.Length;
            Settings.textSpeed = ((Settings.textSpeed + step) % count + count) % count;
            Save();
        }

        private void OnHitFlashToggled()
        {
            Settings.hitFlash = !Settings.hitFlash;
            Save();
        }

        private void OnHitStopToggled()
        {
            Settings.hitStop = !Settings.hitStop;
            Save();
        }

        private void OnRumbleToggled()
        {
            Settings.rumble = !Settings.rumble;
            Save();
        }

        private void OnPromptsStepped(int step)
        {
            var count = SettingsData.LayoutNames.Length;
            Settings.prompts = (PromptLayout)((((int)Settings.prompts + step) % count + count) % count);
            Save();
        }

        private void OnGameDefaultsPressed()
        {
            Settings.ResetGame();
            Save();
        }

        private void OnDevicePressed()
        {
            gamepadBindings = !gamepadBindings;
            StopListening();
        }

        private void OnResetKeysPressed()
        {
            StopListening();
            InputRouter.Instance?.ResetBindings();
            Refresh();
        }

        private void OnKeyButtonPressed(string actionName)
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
            if (completed)
            {
                InputRouter.Instance?.SaveBindings();
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
            Refresh();
        }

        private void Apply()
        {
            SettingsApplier.Apply(Settings);
            Save();
        }

        private void Save()
        {
            Settings.Save();
            Refresh();
        }

        private enum Page
        {
            Audio = 0,
            Display = 1,
            Controls = 2,
            Game = 3,
        }
    }
}
