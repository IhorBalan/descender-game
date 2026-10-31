using System.Collections.Generic;
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
    /// would resume while that button is focused. Port of scripts/ui/title.gd without
    /// the Maps menu (the map store went with the move to Unity). Opens itself when
    /// <see cref="GameFlow"/> shows the title and closes when the world is entered.
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

        [SerializeField]
        private SettingsScreen settings;

        private GameObject menu;
        private GameObject slots;
        private GameObject difficulty;
        private GameObject credits;
        private Text slotsHeading;
        private Text hint;
        private Text status;
        private Button newGameButton;
        private Button continueButton;
        private Button loadButton;
        private Button settingsButton;
        private Button creditsButton;
        private Button quitButton;
        private readonly Button[] slotButtons = new Button[SaveSlots.SlotCount];
        private Button deleteButton;
        private Button slotsBackButton;
        private readonly Button[] difficultyButtons = new Button[SaveManager.DifficultyNames.Length];
        private Button difficultyBackButton;
        private Button creditsBackButton;
        private SlotMode slotMode = SlotMode.NewGame;
        private int newGameSlot = -1;
        private int confirmSlot = -1;
        private GameObject lastSelected;

        protected override int SortingOrder => 10;

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
            var backdrop = MenuKit.MakeImage(root, "Background", theme.titleBackdrop);
            MenuKit.Stretch(backdrop.rectTransform);
            backdrop.preserveAspect = false;

            var heading = MenuKit.MakeHeading(root, theme, "DESCENDER", MenuTheme.TitleFontSize);
            MenuKit.Strip(heading.rectTransform, true, 6f, 32f);
            var divider = MenuKit.MakeImage(root, "Divider", theme.divider);
            MenuKit.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(120f, 5f));
            var tagline = MenuKit.MakeLabel(root, theme, Tagline, MenuTheme.MutedFontSize);
            tagline.color = theme.textMuted;
            MenuKit.Strip(tagline.rectTransform, true, 42f, 12f);

            var version = MenuKit.MakeLabel(
                root,
                theme,
                "v" + Application.version,
                MenuTheme.MutedFontSize,
                TextAnchor.MiddleRight
            );
            version.color = theme.textMuted;
            MenuKit.Place(version.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -6f), new Vector2(60f, 12f));

            status = MenuKit.MakeLabel(root, theme, "", MenuTheme.MutedFontSize, TextAnchor.MiddleLeft);
            status.color = theme.bad;
            MenuKit.Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(240f, 12f));

            hint = MenuKit.MakeLabel(root, theme, "", MenuTheme.BaseFontSize);
            hint.color = theme.textMuted;
            MenuKit.Strip(hint.rectTransform, false, 4f, 12f);

            BuildMenu(root);
            BuildSlots(root);
            BuildDifficulty(root);
            BuildCredits(root);
            if (settings != null)
            {
                settings.Closed += ShowMenu;
            }
        }

        private void BuildMenu(RectTransform root)
        {
            var column = MenuKit.MakeColumn(root, "Menu");
            menu = column.gameObject;
            var rect = column.GetComponent<RectTransform>();
            MenuKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(88f, 0f));
            newGameButton = MenuKit.MakeButton(column.transform, theme, "New Game", OnNewGamePressed);
            continueButton = MenuKit.MakeButton(column.transform, theme, "Continue", OnContinuePressed);
            loadButton = MenuKit.MakeButton(column.transform, theme, "Load Game", OnLoadPressed);
            settingsButton = MenuKit.MakeButton(column.transform, theme, "Settings", OnSettingsPressed);
            creditsButton = MenuKit.MakeButton(column.transform, theme, "Credits", ShowCredits);
            quitButton = MenuKit.MakeButton(column.transform, theme, "Quit", Quit);
            MenuKit.LinkVertical(
                new List<Selectable>
                {
                    newGameButton,
                    continueButton,
                    loadButton,
                    settingsButton,
                    creditsButton,
                    quitButton,
                }
            );
        }

        private void BuildSlots(RectTransform root)
        {
            var column = MenuKit.MakeColumn(root, "Slots", 3f);
            slots = column.gameObject;
            var rect = column.GetComponent<RectTransform>();
            MenuKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(200f, 0f));
            rect.pivot = new Vector2(0.5f, 1f);
            slotsHeading = MenuKit.MakeLabel(column.transform, theme, "Load game", MenuTheme.SubheadingFontSize);
            slotsHeading.color = theme.subheading;
            MenuKit.Size(slotsHeading, -1f, 14f);
            var items = new List<Selectable>();
            for (var slot = 0; slot < slotButtons.Length; slot++)
            {
                var index = slot;
                slotButtons[slot] = MenuKit.MakeButton(
                    column.transform,
                    theme,
                    "",
                    () => OnSlotPressed(index),
                    TextAnchor.MiddleLeft
                );
                items.Add(slotButtons[slot]);
            }
            deleteButton = MenuKit.MakeButton(column.transform, theme, "Delete a save", OnDeletePressed);
            slotsBackButton = MenuKit.MakeButton(column.transform, theme, "Back", ShowMenu);
            items.Add(deleteButton);
            items.Add(slotsBackButton);
            MenuKit.LinkVertical(items);
            slots.SetActive(false);
        }

        private void BuildDifficulty(RectTransform root)
        {
            var column = MenuKit.MakeColumn(root, "Difficulty", 3f);
            difficulty = column.gameObject;
            var rect = column.GetComponent<RectTransform>();
            MenuKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(200f, 0f));
            rect.pivot = new Vector2(0.5f, 1f);
            var heading = MenuKit.MakeLabel(
                column.transform,
                theme,
                "New game: choose a difficulty",
                MenuTheme.SubheadingFontSize
            );
            heading.color = theme.subheading;
            MenuKit.Size(heading, -1f, 14f);
            var items = new List<Selectable>();
            for (var which = 0; which < difficultyButtons.Length; which++)
            {
                var index = which;
                var text = $"{SaveManager.DifficultyNames[which]}   {SaveManager.DifficultyNotes[which]}";
                difficultyButtons[which] = MenuKit.MakeButton(
                    column.transform,
                    theme,
                    text,
                    () => OnDifficultyPressed(index),
                    TextAnchor.MiddleLeft
                );
                items.Add(difficultyButtons[which]);
            }
            var note = MenuKit.MakeLabel(
                column.transform,
                theme,
                "The difficulty is kept with the save.",
                MenuTheme.MutedFontSize
            );
            note.color = theme.textMuted;
            MenuKit.Size(note, -1f, 10f);
            difficultyBackButton = MenuKit.MakeButton(column.transform, theme, "Back", OnDifficultyBackPressed);
            items.Add(difficultyBackButton);
            MenuKit.LinkVertical(items);
            difficulty.SetActive(false);
        }

        private void BuildCredits(RectTransform root)
        {
            var page = MenuKit.MakeRect(root, "Credits");
            credits = page.gameObject;
            MenuKit.Stretch(page);
            var backdrop = MenuKit.MakeImage(page, "Background", theme.titleBackdrop);
            MenuKit.Stretch(backdrop.rectTransform);
            var heading = MenuKit.MakeHeading(page, theme, "CREDITS", MenuTheme.HeadingFontSize);
            MenuKit.Strip(heading.rectTransform, true, 6f, 20f);
            var text = MenuKit.MakeLabel(page, theme, string.Join("\n", Credits));
            MenuKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMax = new Vector2(0f, -30f);
            text.rectTransform.offsetMin = new Vector2(0f, 40f);
            creditsBackButton = MenuKit.MakeButton(page, theme, "Back", ShowMenu);
            creditsBackButton.GetComponent<LayoutElement>().enabled = false;
            MenuKit.Place(
                creditsBackButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 17f),
                new Vector2(40f, 13f)
            );
            credits.SetActive(false);
        }

        protected override void OnOpened()
        {
            status.text = "";
            ShowMenu();
        }

        public override void OnCancel()
        {
            if (difficulty.activeSelf)
            {
                ShowSlots(SlotMode.NewGame);
            }
            else if (slots.activeSelf || credits.activeSelf)
            {
                ShowMenu();
            }
        }

        public override void OnTop()
        {
            var selected = Selected;
            if (selected != lastSelected)
            {
                lastSelected = selected;
                RefreshHint();
            }
        }

        private void ShowMenu()
        {
            if (!IsOpen)
            {
                return;
            }
            slots.SetActive(false);
            difficulty.SetActive(false);
            credits.SetActive(false);
            menu.SetActive(true);
            var anySave = SaveManager.Slots.LatestSlot() >= 0;
            continueButton.interactable = anySave;
            loadButton.interactable = anySave;
            Select(newGameButton);
            RefreshHint();
        }

        private void ShowSlots(SlotMode mode)
        {
            slotMode = mode;
            confirmSlot = -1;
            slotsHeading.text = mode switch
            {
                SlotMode.NewGame => "New game: choose a slot",
                SlotMode.Load => "Load game",
                _ => "Delete a save: choose a slot",
            };
            // Deleting is offered from the load picker, and the delete picker goes back to loading.
            deleteButton.gameObject.SetActive(mode != SlotMode.NewGame);
            MenuKit.SetText(deleteButton, mode == SlotMode.Delete ? "Load instead" : "Delete a save");
            var items = new List<Selectable>();
            for (var slot = 0; slot < slotButtons.Length; slot++)
            {
                var button = slotButtons[slot];
                MenuKit.SetText(button, $"{slot + 1}   {SaveManager.Summary(slot, GameFlow.LevelNameOf)}");
                button.interactable = mode == SlotMode.NewGame || SaveManager.Slots.HasSave(slot);
                items.Add(button);
            }
            if (mode != SlotMode.NewGame)
            {
                items.Add(deleteButton);
            }
            items.Add(slotsBackButton);
            MenuKit.LinkVertical(items);
            menu.SetActive(false);
            difficulty.SetActive(false);
            slots.SetActive(true);
            var first = MenuKit.FirstEnabled(items);
            Select(first != null ? first : slotsBackButton);
            RefreshHint();
        }

        private void ShowDifficulty(int slot)
        {
            newGameSlot = slot;
            slots.SetActive(false);
            difficulty.SetActive(true);
            Select(difficultyButtons[(int)Difficulty.Normal]);
        }

        private void ShowCredits()
        {
            menu.SetActive(false);
            credits.SetActive(true);
            Select(creditsBackButton);
        }

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
            menu.SetActive(false);
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
            MenuKit.SetText(slotButtons[slot], $"{slot + 1}   {question}  Press again");
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
            if (hint == null)
            {
                return;
            }
            if (menu.activeSelf && Selected == continueButton.gameObject && continueButton.interactable)
            {
                var slot = SaveManager.Slots.LatestSlot();
                hint.text = $"Resumes slot {slot + 1}:   {SaveManager.Summary(slot, GameFlow.LevelNameOf)}";
                return;
            }
            hint.text = MenuKit.Hint(UsingGamepad, "move", "choose", "go back");
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
