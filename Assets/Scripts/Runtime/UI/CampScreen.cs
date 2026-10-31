using System.Collections.Generic;
using Descender.Economy;
using Descender.Player;
using Descender.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The camp at a checkpoint: pressing talk at one the hero has rested at opens
    /// this, over the paused game. The pack, the skills and fast travel on the
    /// left; save and leave on the right. A line under the heading says which
    /// checkpoint this is and the level, and a line above the hint describes the
    /// picked button. The pack and skills pages open over the camp and hand the
    /// focus back when they close. Fast travel is offered through
    /// <see cref="SetFastTravel"/> and asks the session to carry the hero with
    /// <see cref="FastTravelRequested"/>. Port of scripts/ui/checkpoint_menu.gd.
    /// </summary>
    public sealed class CampScreen : UiScreen
    {
        public const string SaveText = "Save Game";
        public const string TravelClosedNote = "No other camp he has rested at answers from here.";

        private static readonly Dictionary<string, string> Notes = new Dictionary<string, string>
        {
            ["pack"] = "What he carries: wear a blade, a coat or a charm, or drink a flask.",
            ["skills"] = "Where his levels go: a heart, an edge, quicker feet.",
            ["travel"] = "The angels' system can carry him to another camp he has rested at.",
            ["save"] = "Writes the game to its slot, resuming at this camp.",
            ["leave"] = "Back to the descent.",
        };

        [SerializeField]
        private InventoryScreen pack;

        [SerializeField]
        private SkillScreen skills;

        private TextMeshProUGUI heading;
        private TextMeshProUGUI status;
        private Image divider;
        private RectTransform menu;
        private Button packButton;
        private Button skillsButton;
        private Button travelButton;
        private Button saveButton;
        private Button leaveButton;
        private TextMeshProUGUI description;
        private PromptLabel hint;
        private readonly List<int> rested = new List<int>();
        private bool canTravel;

        /// <summary>The checkpoint the camp was opened at, by index in the level.</summary>
        public int CheckpointIndex { get; private set; } = -1;

        public IHeroMenuActions Hero { get; set; }

        /// <summary>The save button was pressed; the session saves and calls <see cref="ShowSaved"/>.</summary>
        public event System.Action SaveRequested;

        /// <summary>A skill point was spent (the HUD line follows).</summary>
        public event System.Action SkillLearned;

        /// <summary>A rested checkpoint was picked to travel to (by index in the level).</summary>
        public event System.Action<int> FastTravelRequested;

        /// <summary>The fast travel button was pressed and travel is possible; the map thread's screen opens on this.</summary>
        public event System.Action<IReadOnlyList<int>, int> TravelPicked;

        public InventoryScreen Pack => pack;
        public SkillScreen Skills => skills;
        public bool CanTravel => canTravel && rested.Contains(CheckpointIndex) && rested.Count > 1;

        protected override void Build(RectTransform root)
        {
            UiKit.Dim(root);
            heading = UiKit.Heading(root, "CAMP");
            UiKit.PlaceRow(heading.rectTransform, 12f, 22f);
            heading.verticalAlignment = VerticalAlignmentOptions.Middle;
            status = UiKit.Label(root, "Status", "", UiTheme.FontSize, UiTheme.Muted, TextAlignmentOptions.Center);
            UiKit.PlaceRow(status.rectTransform, 34f, 12f);
            status.verticalAlignment = VerticalAlignmentOptions.Middle;
            divider = UiKit.Box(root, "Divider", Color.white);
            if (UiTheme.Current.Divider != null)
            {
                divider.sprite = UiTheme.Current.Divider;
            }
            else
            {
                divider.color = UiTheme.Muted;
            }
            UiKit.PlaceCentered(divider.rectTransform, 48f, 120f, 5f);

            menu = UiKit.Rect(root, "Menu");
            UiKit.PlaceCentered(menu, UiTheme.Height / 2f - 32f, 208f, 70f);
            var left = UiKit.Rect(menu, "Left");
            UiKit.Place(left, 0f, 0f, 100f, 70f);
            var right = UiKit.Rect(menu, "Right");
            UiKit.Place(right, 108f, 0f, 100f, 70f);
            packButton = Column(left, 0, "Pack", "The Pack", OnPackPressed);
            skillsButton = Column(left, 1, "Skills", "Skills", OnSkillsPressed);
            travelButton = Column(left, 2, "Travel", "Fast Travel", OnTravelPressed);
            saveButton = Column(right, 0, "Save", SaveText, () => SaveRequested?.Invoke());
            leaveButton = Column(right, 1, "Leave", "Leave", Close);
            UiKit.LinkColumns(
                new Selectable[] { packButton, skillsButton, travelButton },
                new Selectable[] { saveButton, leaveButton }
            );
            Note(packButton, "pack");
            Note(skillsButton, "skills");
            Note(travelButton, "travel");
            Note(saveButton, "save");
            Note(leaveButton, "leave");

            description = UiKit.Paragraph(
                root,
                "Description",
                "",
                UiTheme.FontSize,
                UiTheme.Muted,
                TextAlignmentOptions.Center
            );
            UiKit.PlaceBottomRow(description.rectTransform, 26f, 18f, 16f);
            description.verticalAlignment = VerticalAlignmentOptions.Middle;
            var hintLabel = UiKit.Label(
                root,
                "Hint",
                "",
                UiTheme.SmallFontSize,
                UiTheme.Muted,
                TextAlignmentOptions.Center
            );
            UiKit.PlaceBottomRow(hintLabel.rectTransform, 2f, 8f);
            hintLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            hint = hintLabel.gameObject.AddComponent<PromptLabel>();
            hint.Template = "{navigate} pick   {ui_accept} chooses   {ui_cancel} leaves the camp";

            EnsurePages();
        }

        private Button Column(
            RectTransform column,
            int row,
            string name,
            string text,
            UnityEngine.Events.UnityAction onClick
        )
        {
            var button = UiKit.Button(column, name, text, onClick);
            UiKit.Place(
                button.GetComponent<RectTransform>(),
                0f,
                row * (UiTheme.ButtonHeight + 2f),
                100f,
                UiTheme.ButtonHeight
            );
            return button;
        }

        private void Note(Button button, string key)
        {
            button.GetComponent<UiButtonLook>().Focused += () => ShowNote(Notes[key], UiTheme.Note);
        }

        /// <summary>The pack and skills pages, made under the camp when the scene did not give them.</summary>
        private void EnsurePages()
        {
            if (pack == null)
            {
                pack = new GameObject("Pack").AddComponent<InventoryScreen>();
                pack.transform.SetParent(transform, false);
            }
            if (skills == null)
            {
                skills = new GameObject("Skills").AddComponent<SkillScreen>();
                skills.transform.SetParent(transform, false);
            }
            pack.Closed += () => OnPageClosed(packButton);
            skills.Closed += () => OnPageClosed(skillsButton);
            skills.Learned += OnSkillLearned;
        }

        /// <summary>
        /// Opens the camp at a checkpoint (`index` in the level's list, of `count`).
        /// Saving is offered only when the game was started from a slot.
        /// </summary>
        public void Open(IHeroMenuActions hero, int index, int count)
        {
            EnsureBuilt();
            Hero = hero;
            pack.Hero = hero;
            skills.Hero = hero;
            CheckpointIndex = index;
            heading.text = index < 0 ? "CAMP" : $"CAMP {index + 1}";
            status.text = MenuText.CampStatusText(index, count);
            saveButton.interactable = SaveManager.CurrentSlot >= 0;
            UiKit.SetText(saveButton, SaveText);
            SetFastTravel(false, new int[0]);
            Open();
        }

        protected override void OnOpen()
        {
            ShowFrame(true);
            ShowNote("", UiTheme.Note);
            // A point waiting is what the camp is for; the skills take the focus.
            UiKit.Select(Economy.Skills.PointsLeft() > 0 ? skillsButton : packButton);
        }

        protected override void OnClose()
        {
            pack.Close();
            skills.Close();
        }

        /// <summary>Offers fast travel from this camp: whether there is a map, and the checkpoints rested at.</summary>
        public void SetFastTravel(bool hasMap, IEnumerable<int> restedAt)
        {
            canTravel = hasMap;
            rested.Clear();
            rested.AddRange(restedAt);
            if (travelButton != null)
            {
                travelButton.interactable = CanTravel;
            }
        }

        /// <summary>Called back by the session with whether the save landed.</summary>
        public void ShowSaved(bool saved)
        {
            if (saved)
            {
                UiKit.SetText(saveButton, $"Saved at camp {CheckpointIndex + 1}");
            }
            else
            {
                ShowNote("There is no slot to save to.", UiTheme.Warn);
            }
        }

        /// <summary>The map screen picked a camp; passes it on and closes.</summary>
        public void TravelTo(int index)
        {
            FastTravelRequested?.Invoke(index);
        }

        private void OnPackPressed()
        {
            ShowFrame(false);
            pack.Standalone = false;
            pack.Open();
        }

        private void OnSkillsPressed()
        {
            ShowFrame(false);
            skills.Standalone = false;
            skills.Open();
        }

        private void OnSkillLearned(string id)
        {
            status.text = MenuText.CampStatusText(CheckpointIndex, -1);
            SkillLearned?.Invoke();
        }

        private void OnTravelPressed()
        {
            if (!CanTravel)
            {
                ShowNote(TravelClosedNote, UiTheme.Warn);
                return;
            }
            TravelPicked?.Invoke(rested, CheckpointIndex);
        }

        private void OnPageClosed(Button button)
        {
            if (!IsOpen)
            {
                return;
            }
            status.text = MenuText.CampStatusText(CheckpointIndex, -1);
            ShowFrame(true);
            UiKit.Select(button);
        }

        /// <summary>The menu with its heading, status line and hint; hidden behind a page.</summary>
        private void ShowFrame(bool shown)
        {
            menu.gameObject.SetActive(shown);
            heading.gameObject.SetActive(shown);
            divider.gameObject.SetActive(shown);
            status.gameObject.SetActive(shown);
            description.gameObject.SetActive(shown);
            hint.gameObject.SetActive(shown);
        }

        private void ShowNote(string text, Color color)
        {
            description.text = text;
            description.color = color;
        }
    }
}
