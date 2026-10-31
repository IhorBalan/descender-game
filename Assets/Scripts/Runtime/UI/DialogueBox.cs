using System.Collections.Generic;
using Descender.Save;
using Descender.Story;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.UI
{
    /// <summary>
    /// The text box at the bottom of the screen: a speaker name and one box of text
    /// at a time, typed out at the speed the settings choose (or all at once). The
    /// typing rests on punctuation and asks for a soft blip pitched by who is
    /// speaking (<see cref="Blip"/>), unless the blips are off; the name takes the
    /// speaker's colour, and a stage note (no speaker) is grey and silent. Interact
    /// (or jump) finishes the typing, then turns the page; the last page closes the
    /// box. A line with `choices` shows them once typed out: left and right pick,
    /// interact confirms, and the picked choice's `then` goes out with the entry.
    /// The <see cref="DialogueRunner"/> opens the box and the hero stands still while
    /// <see cref="AnyOpen"/>. Port of scripts/ui/dialogue_box.gd.
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour
    {
        public const int SortOrder = 6;
        public const string ChoiceGap = "      ";
        public const float BlipVolumeDb = -14f;

        public static readonly Color NameColor = new Color(1f, 0.95f, 0.7f);
        public static readonly Color TextColor = new Color(0.95f, 0.95f, 1f);

        /// <summary>A line nobody speaks: a stage note in parentheses, or a description.</summary>
        public static readonly Color NoteColor = new Color(0.7f, 0.7f, 0.78f);
        public static readonly Color PanelColor = new Color(0.05f, 0.03f, 0.12f, 0.96f);
        public static readonly Color PanelBorder = new Color(1f, 0.95f, 0.7f, 0.9f);

        /// <summary>Who speaks in which colour; anyone else gets <see cref="NameColor"/>.</summary>
        public static readonly Dictionary<string, Color> SpeakerColors = new Dictionary<string, Color>
        {
            ["Ansel"] = new Color(1f, 0.95f, 0.7f),
            ["Voice"] = new Color(0.75f, 0.85f, 1f),
            ["The Voice"] = new Color(0.75f, 0.85f, 1f),
            ["Tobiah"] = new Color(0.95f, 0.6f, 0.55f),
            ["Gorrow"] = new Color(0.95f, 0.55f, 0.45f),
            ["Vael"] = new Color(0.85f, 0.65f, 0.95f),
            ["The Penitent"] = new Color(0.8f, 0.8f, 0.85f),
            ["Warden"] = new Color(0.8f, 0.9f, 1f),
            ["The Gardener"] = new Color(1f, 0.85f, 0.5f),
            ["Limbo Lord"] = new Color(0.95f, 0.55f, 0.45f),
            ["Keeper"] = new Color(0.95f, 0.55f, 0.45f),
            ["The Bloated One"] = new Color(0.95f, 0.55f, 0.45f),
            ["Warden of the Vault"] = new Color(0.95f, 0.55f, 0.45f),
            ["Cursed Bishop"] = new Color(0.8f, 0.8f, 0.85f),
            ["Champion"] = new Color(0.95f, 0.55f, 0.45f),
            ["False King"] = new Color(0.95f, 0.55f, 0.45f),
            ["Hanne"] = new Color(1f, 0.8f, 0.75f),
        };

        /// <summary>How the blip is pitched for each speaker; anyone else is 1, and no speaker is silence.</summary>
        public static readonly Dictionary<string, float> SpeakerPitch = new Dictionary<string, float>
        {
            ["Ansel"] = 1f,
            ["Voice"] = 0.65f,
            ["The Voice"] = 0.65f,
            ["Tobiah"] = 0.85f,
            ["Gorrow"] = 0.7f,
            ["Vael"] = 1.2f,
            ["Hanne"] = 1.35f,
            ["Old Pell"] = 0.8f,
            ["Corporal Dace"] = 0.9f,
            ["Sister Ava"] = 1.1f,
            ["Mereth"] = 1.05f,
            ["Ithuriel"] = 0.95f,
            ["The Ferryman"] = 0.75f,
            ["The Gardener"] = 0.6f,
            ["Warden"] = 0.8f,
            ["The Penitent"] = 0.9f,
        };

        private Canvas canvas;
        private RectTransform panel;
        private TextMeshProUGUI nameLabel;
        private TextMeshProUGUI textLabel;
        private TextMeshProUGUI moreLabel;
        private TextMeshProUGUI choiceLabel;
        private readonly DialogueTyper typer = new DialogueTyper();
        private JObject entry;
        private int index;
        private List<DialogueChoice> choices = new List<DialogueChoice>();
        private int choice;
        private float blipPitch = 1f;
        private InputAction moveLeft;
        private InputAction moveRight;

        /// <summary>The entry has been read to the end (or a choice taken): the entry, with `then` and `chosen` filled in.</summary>
        public event System.Action<JObject> Finished;

        /// <summary>A typing tick wants a blip at a pitch; the audio plays it on the Blips group.</summary>
        public event System.Action<float> Blip;

        /// <summary>How many dialogue boxes are open, so the hero knows to stand still.</summary>
        public static int OpenCount { get; private set; }

        public static bool AnyOpen => OpenCount > 0;

        public bool IsOpen { get; private set; }

        /// <summary>The id of the entry being shown, or "" when the box is closed.</summary>
        public string CurrentId => entry?.Value<string>("id") ?? "";

        public int PageCount => DialogueData.LineCount(entry);
        public int PageIndex => index;
        public bool HasChoice => choices.Count > 0;
        public int SelectedChoice => choice;
        public bool IsPausing => IsOpen && typer.IsPausing;
        public bool IsTyped => typer.IsDone;
        public int VisibleCharacters => typer.Visible;

        /// <summary>Whether the choices are up: the line has them and is typed out.</summary>
        public bool IsChoosing => HasChoice && typer.IsDone;

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                OpenCount--;
            }
        }

        private void Build()
        {
            canvas = UiKit.MakeCanvas("DialogueCanvas", SortOrder, transform);
            var root = canvas.GetComponent<RectTransform>();
            var border = UiKit.Box(root, "Panel", PanelBorder);
            panel = border.rectTransform;
            UiKit.Place(panel, 8f, 128f, 304f, 48f);
            var inner = UiKit.Box(panel, "Fill", PanelColor);
            UiKit.Fill(inner.rectTransform, 1f, 1f, 1f, 1f);
            nameLabel = UiKit.Label(panel, "Name", "", UiTheme.SmallFontSize, NameColor);
            UiKit.Place(nameLabel.rectTransform, 6f, 3f, 194f, 10f);
            nameLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            textLabel = UiKit.Paragraph(panel, "Text", "", UiTheme.SmallFontSize, TextColor);
            UiKit.Place(textLabel.rectTransform, 6f, 14f, 292f, 32f);
            choiceLabel = UiKit.Label(panel, "Choice", "", UiTheme.SmallFontSize, NameColor);
            UiKit.Place(choiceLabel.rectTransform, 6f, 36f, 274f, 10f);
            choiceLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            moreLabel = UiKit.Label(panel, "More", "v", UiTheme.SmallFontSize, NameColor, TextAlignmentOptions.Right);
            UiKit.Place(moreLabel.rectTransform, 284f, 36f, 16f, 10f);
            moreLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            panel.gameObject.SetActive(false);
            choiceLabel.gameObject.SetActive(false);
            moreLabel.gameObject.SetActive(false);
        }

        /// <summary>Opens the box on an entry (see DialogueData.Entry); nothing happens for one with no lines.</summary>
        public void Play(JObject data)
        {
            if (DialogueData.LineCount(data) == 0)
            {
                return;
            }
            entry = data;
            index = 0;
            if (!IsOpen)
            {
                IsOpen = true;
                OpenCount++;
            }
            panel.gameObject.SetActive(true);
            ShowLine();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Tick(Time.deltaTime);
            var input = Input.InputRouter.Instance;
            if (input == null)
            {
                return;
            }
            if (input.InteractPressed || input.JumpPressed)
            {
                Advance();
            }
            else if (IsChoosing && Pressed(ref moveRight, "MoveRight"))
            {
                Select(choice + 1);
            }
            else if (IsChoosing && Pressed(ref moveLeft, "MoveLeft"))
            {
                Select(choice - 1);
            }
        }

        private static bool Pressed(ref InputAction cached, string name)
        {
            if (cached == null)
            {
                cached = Prompts.ActionFor(name);
            }
            return cached != null && cached.WasPressedThisFrame();
        }

        /// <summary>The typing's clock: `delta` seconds of typing, resting and blinking. Stepped by Update; a test steps it by hand.</summary>
        public void Tick(float delta)
        {
            if (!IsOpen)
            {
                return;
            }
            var blip = typer.Tick(delta, SettingsData.Current.CharsPerSecond());
            textLabel.maxVisibleCharacters = typer.Visible;
            if (blip && blipPitch > 0f && SettingsData.Current.blips)
            {
                Blip?.Invoke(blipPitch);
            }
            if (IsChoosing)
            {
                choiceLabel.gameObject.SetActive(true);
                moreLabel.gameObject.SetActive(false);
                return;
            }
            moreLabel.gameObject.SetActive(typer.MoreLit);
        }

        /// <summary>The colour a speaker's name is shown in.</summary>
        public static Color SpeakerColor(string who) => SpeakerColors.TryGetValue(who ?? "", out var color) ? color : NameColor;

        /// <summary>The blip pitch for a speaker: 0 for a line nobody speaks, which types in silence.</summary>
        public static float PitchOf(string who)
        {
            if (string.IsNullOrEmpty(who))
            {
                return 0f;
            }
            return SpeakerPitch.TryGetValue(who, out var pitch) ? pitch : 1f;
        }

        /// <summary>Finishes the typing, or turns to the next page, or closes the box.</summary>
        public void Advance()
        {
            if (!IsOpen)
            {
                return;
            }
            if (!typer.IsDone)
            {
                typer.Skip();
                textLabel.maxVisibleCharacters = 99999;
                return;
            }
            if (HasChoice)
            {
                Choose(choice);
                return;
            }
            index++;
            if (index >= DialogueData.LineCount(entry))
            {
                Close();
            }
            else
            {
                ShowLine();
            }
        }

        /// <summary>Takes a choice: the entry goes out with the choice's `then` and index.</summary>
        public void Choose(int picked)
        {
            if (!HasChoice || picked < 0 || picked >= choices.Count)
            {
                return;
            }
            entry["then"] = choices[picked].Then;
            entry["chosen"] = picked;
            Close();
        }

        private void Select(int picked)
        {
            choice = Mathf.Clamp(picked, 0, choices.Count - 1);
            choiceLabel.text = ChoiceText();
        }

        /// <summary>The choices on one line, the cursor before the picked one.</summary>
        public string ChoiceText()
        {
            var parts = new List<string>();
            for (var i = 0; i < choices.Count; i++)
            {
                parts.Add((i == choice ? "> " : "  ") + choices[i].Text);
            }
            return string.Join(ChoiceGap, parts);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            OpenCount--;
            panel.gameObject.SetActive(false);
            choiceLabel.gameObject.SetActive(false);
            choices = new List<DialogueChoice>();
            var finished = entry;
            entry = null;
            Finished?.Invoke(finished);
        }

        private void ShowLine()
        {
            var line = DialogueData.Line(entry, index);
            nameLabel.text = line.Who;
            nameLabel.gameObject.SetActive(line.Who.Length > 0);
            nameLabel.color = SpeakerColor(line.Who);
            textLabel.text = line.Text;
            textLabel.color = line.Who.Length > 0 ? TextColor : NoteColor;
            textLabel.maxVisibleCharacters = 0;
            typer.Start(line.Text);
            blipPitch = PitchOf(line.Who);
            moreLabel.gameObject.SetActive(false);
            choices = DialogueData.Choices(entry, index);
            choice = 0;
            choiceLabel.gameObject.SetActive(false);
            choiceLabel.text = ChoiceText();
        }
    }
}
