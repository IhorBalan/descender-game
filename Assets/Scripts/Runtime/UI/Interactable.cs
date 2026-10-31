using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// Something in the level the hero can talk to or examine: an NPC, a relic or an
    /// invisible story trigger. Lists the dialogue ids to try, in priority order; the
    /// <see cref="DialogueRunner"/> picks the first whose flags match and plays it.
    /// Shows the interact key, or the pad button, above itself while the hero stands
    /// close. The object's origin is at the base, like the hero's, so a placement
    /// puts it on the floor. The trigger area is a BoxCollider2D made in Awake, in
    /// level pixels. Port of scripts/interactable.gd.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        [SerializeField]
        [Tooltip(
            "Dialogue ids to try, first match wins. These are the unique lines; mark them `once` in the dialogue file."
        )]
        private List<string> dialogueIds = new List<string>();

        [SerializeField]
        [Tooltip(
            "Repeat lines, used when nothing in the dialogue ids applies any more. Each talk takes the next one, round robin."
        )]
        private List<string> rotateIds = new List<string>();

        [SerializeField]
        [Tooltip("The hero's half of the area, in level pixels.")]
        private Vector2 areaSize = new Vector2(28f, 40f);

        [SerializeField]
        [Tooltip("Where the key prompt floats, in level pixels above the base.")]
        private float promptHeight = 48f;

        [SerializeField]
        [Tooltip("Triggers have no prompt and fire on touch instead of on a key press.")]
        private bool automatic;

        private BoxCollider2D area;
        private Canvas promptCanvas;
        private int rotation;
        private Player.PlayerController nearHero;

        /// <summary>Fired when the hero presses interact in range (or, for a trigger, walks in).</summary>
        public event System.Action<Interactable> Interacted;

        public IReadOnlyList<string> DialogueIds => dialogueIds;
        public IReadOnlyList<string> RotateIds => rotateIds;
        public bool IsAutomatic => automatic;
        public bool IsPlayerNear => nearHero != null;
        public Player.PlayerController NearHero => nearHero;

        /// <summary>Sets the ids from a placement at runtime (the world importer does this).</summary>
        public void Configure(IEnumerable<string> unique, IEnumerable<string> repeat, bool isAutomatic = false)
        {
            dialogueIds = new List<string>(unique ?? new string[0]);
            rotateIds = new List<string>(repeat ?? new string[0]);
            automatic = isAutomatic;
        }

        protected virtual void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Trigger");
            if (!TryGetComponent(out area))
            {
                area = gameObject.AddComponent<BoxCollider2D>();
            }
            area.isTrigger = true;
            area.size = areaSize / Ppu;
            area.offset = new Vector2(0f, areaSize.y / 2f / Ppu);
            if (!automatic)
            {
                BuildPrompt();
            }
        }

        private void BuildPrompt()
        {
            var go = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            promptCanvas = go.GetComponent<Canvas>();
            promptCanvas.renderMode = RenderMode.WorldSpace;
            promptCanvas.sortingOrder = 50;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(40f, 12f);
            rect.localScale = Vector3.one / Ppu;
            rect.localPosition = new Vector3(0f, promptHeight / Ppu, 0f);
            var label = UiKit.Label(
                rect,
                "Text",
                "",
                UiTheme.SmallFontSize,
                UiTheme.Text,
                TextAlignmentOptions.Center,
                true
            );
            UiKit.Fill(label.rectTransform);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.gameObject.AddComponent<PromptLabel>().Template = "{interact}";
            promptCanvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (automatic || nearHero == null)
            {
                return;
            }
            // The press falls through to an open dialogue box, which the runner checks.
            var input = Input.InputRouter.Instance;
            if (input != null && input.InteractPressed && !DialogueBox.AnyOpen)
            {
                Interacted?.Invoke(this);
            }
        }

        /// <summary>The repeat lines starting from the next one in turn, so the first that applies is the one to play. Advances the turn.</summary>
        public List<string> RotationIds()
        {
            var result = new List<string>();
            var count = rotateIds.Count;
            for (var i = 0; i < count; i++)
            {
                result.Add(rotateIds[(rotation + i) % count]);
            }
            if (count > 0)
            {
                rotation = (rotation + 1) % count;
            }
            return result;
        }

        /// <summary>Called by the runner when this one's dialogue opens and closes.</summary>
        public virtual void SetTalking(bool talking) { }

        /// <summary>Called by the runner once the dialogue has been read to the end.</summary>
        public virtual void OnDialogueFinished(JObject entry) { }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<Player.PlayerController>(out var hero) || !hero.IsAlive)
            {
                return;
            }
            nearHero = hero;
            if (automatic)
            {
                Interacted?.Invoke(this);
            }
            else if (promptCanvas != null)
            {
                promptCanvas.gameObject.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (nearHero != null && other.gameObject == nearHero.gameObject)
            {
                nearHero = null;
                if (promptCanvas != null)
                {
                    promptCanvas.gameObject.SetActive(false);
                }
            }
        }
    }
}
