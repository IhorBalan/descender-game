using System.Collections.Generic;
using Descender.Player;
using Descender.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The camp at a checkpoint: pressing talk at one the hero has rested at opens
    /// this, over the paused game, in the title's gilded look on a veil the game
    /// shows faintly through. CAMP and its number over a jewelled rule, a line with
    /// the checkpoint, the level and any skill points, then one column: the pack, the
    /// skills, fast travel, save and leave, with the gold bar gliding to the focused
    /// one and a line under it that describes it. The pack and skills pages open over
    /// the camp and hand the focus back when they close. Fast travel is offered
    /// through <see cref="SetFastTravel"/> and asks the session to carry the hero with
    /// <see cref="FastTravelRequested"/>.
    /// </summary>
    public sealed class CampScreen : UiScreen
    {
        public const string SaveText = "Save Game";
        public const string TravelClosedNote = "No other camp he has rested at answers from here.";

        private const float Middle = TitleKit.Width * 0.5f;
        private const float HeadingY = 200f;
        private const float RuleY = 262f;
        private const float StatusY = 306f;
        private const float ItemsY = 400f;
        private const float NoteY = 760f;

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

        private RectTransform frame;
        private CanvasGroup stageGroup;
        private TextMeshProUGUI heading;
        private TextMeshProUGUI status;
        private TitleList menu;
        private Button packButton;
        private Button skillsButton;
        private Button travelButton;
        private Button saveButton;
        private TextMeshProUGUI description;
        private RectTransform keys;
        private readonly Dictionary<GameObject, string> noteOf = new Dictionary<GameObject, string>();
        private readonly List<int> rested = new List<int>();
        private GameObject lastSelected;
        private bool canTravel;
        private int keysFor = -1;
        private float openedAt;

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

        /// <summary>The camp's column, for tests.</summary>
        public TitleList Menu => menu;

        /// <summary>The line describing the focused item.</summary>
        public string NoteText => description != null ? description.text : "";

        protected override void Build(RectTransform root)
        {
            var theme = UiTheme.Current.MenuTheme;
            var stage = TitleHints.MakeStage(root, theme, true, 41, out stageGroup);
            frame = UiKit.Rect(stage, "Frame");
            UiKit.Fill(frame);
            heading = TitleKit.Label(frame, "CAMP", 51f, TitleKit.Gold, TitleKit.CapsSpacing, Middle, HeadingY);
            status = TitleKit.Label(frame, "", 21f, TitleKit.Muted, 6f, Middle, StatusY, 1400f);
            menu = new TitleList(
                frame,
                "Menu",
                ItemsY,
                820f,
                theme != null ? theme.focusBar : null,
                theme != null ? theme.ruleItem : null
            );
            packButton = Item("The Pack", OnPackPressed, "pack");
            skillsButton = Item("Skills", OnSkillsPressed, "skills");
            travelButton = Item("Fast Travel", OnTravelPressed, "travel");
            saveButton = Item(SaveText, () => SaveRequested?.Invoke(), "save");
            var leave = menu.Add("Leave", Close, true, TitleList.BackSound);
            noteOf[leave.gameObject] = Notes["leave"];
            MenuKit.LinkVertical(menu.Layout());
            if (theme != null)
            {
                TitleKit.Sprite(frame, "Rule", theme.ruleTagline, Middle, RuleY);
                TitleFocus.Make(frame, theme.focusEmblem).Follow(menu, true);
            }
            description = TitleKit.Label(frame, "", 21f, TitleKit.Parchment, 4f, Middle, NoteY, 1300f);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.rectTransform.sizeDelta = new Vector2(1300f, 70f);
            keys = UiKit.Rect(frame, "Keys");
            UiKit.Fill(keys);
            EnsurePages();
        }

        private Button Item(string text, UnityEngine.Events.UnityAction onClick, string note)
        {
            var button = menu.Add(text, onClick);
            noteOf[button.gameObject] = Notes[note];
            return button;
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
            heading.text = index < 0 ? "CAMP" : $"CAMP  {index + 1}";
            status.text = MenuText.CampStatusText(index, count);
            saveButton.interactable = SaveManager.CurrentSlot >= 0;
            TitleKit.SetText(saveButton, SaveText);
            SetFastTravel(false, new int[0]);
            Open();
        }

        protected override void OnOpen()
        {
            openedAt = Time.unscaledTime;
            stageGroup.alpha = 0f;
            keysFor = -1;
            ShowFrame(true);
            ShowNote("", TitleKit.Parchment);
            // A point waiting is what the camp is for; the skills take the focus.
            UiKit.Select(Economy.Skills.PointsLeft() > 0 ? skillsButton : packButton);
        }

        protected override void OnClose()
        {
            pack.Close();
            skills.Close();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen)
            {
                return;
            }
            stageGroup.alpha = TitleScreen.Smooth((Time.unscaledTime - openedAt) / 0.3f);
            if (!frame.gameObject.activeSelf)
            {
                return;
            }
            var system = EventSystem.current;
            var selected = system != null ? system.currentSelectedGameObject : null;
            if (selected != lastSelected)
            {
                if (lastSelected != null && menu.Contains(selected))
                {
                    Audio.Sfx.Play("menu_move", -6f);
                }
                lastSelected = selected;
                if (selected != null && noteOf.TryGetValue(selected, out var note))
                {
                    ShowNote(note, TitleKit.Parchment);
                }
            }
            var router = Input.InputRouter.Instance;
            var gamepad = router != null && router.UsingGamepad ? 1 : 0;
            var theme = UiTheme.Current.MenuTheme;
            if (keysFor != gamepad && theme != null)
            {
                keysFor = gamepad;
                TitleHints.Build(keys, theme, gamepad == 1, "Confirm", "Leave");
            }
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
                TitleKit.SetText(saveButton, $"Saved at Camp {CheckpointIndex + 1}");
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

        /// <summary>The camp's heading, column, note and hints; hidden behind a page, which keeps the ground and embers.</summary>
        private void ShowFrame(bool shown)
        {
            frame.gameObject.SetActive(shown);
            lastSelected = null;
        }

        private void ShowNote(string text, Color color)
        {
            description.text = text;
            description.color = color;
        }
    }
}
