using System.Collections.Generic;
using Descender.Input;
using Descender.Player;
using Descender.UI;
using Descender.World;
using TMPro;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// Resting here refills health and healing and sets the respawn point. The hero
    /// rests automatically on touch. Standing at it and pressing Interact makes camp
    /// (<see cref="CampRequested"/>; the game flow opens the camp screens). Origin at
    /// the base, like the hero's, so the respawn point puts his feet on the floor. The
    /// lit frame marks the active one. Port of scripts/checkpoint.gd.
    /// </summary>
    public sealed class Checkpoint : MonoBehaviour, IPlacementReceiver
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const string RestedFlagPrefix = "rested:";
        public static readonly Vector2 PromptOffset = new Vector2(0f, 64f);
        public static readonly Vector2 BoxSize = new Vector2(24f, 32f);
        public static readonly Vector2 BoxOffset = new Vector2(0f, 16f);

        private static readonly List<Checkpoint> all = new List<Checkpoint>();

        [Tooltip("Its number in the level's list; the save's checkpoint is the same index.")]
        [SerializeField]
        private int index;

        [SerializeField]
        private SpriteRenderer sprite;

        [Tooltip("Unlit and lit.")]
        [SerializeField]
        private Sprite[] frames = new Sprite[0];

        private bool active;
        private bool playerNear;
        private Canvas prompt;

        /// <summary>The hero rested here.</summary>
        public event System.Action<Checkpoint> Rested;

        /// <summary>The hero pressed Interact here; the game flow opens the camp.</summary>
        public event System.Action<Checkpoint> CampRequested;

        /// <summary>Any checkpoint was rested at; the game flow records the respawn point.</summary>
        public static event System.Action<Checkpoint> AnyRested;

        /// <summary>Any checkpoint's camp was asked for.</summary>
        public static event System.Action<Checkpoint> AnyCampRequested;

        /// <summary>The hero walked up to or away from a checkpoint; the UI shows the prompt.</summary>
        public static event System.Action<Checkpoint, bool> NearChanged;

        public static IReadOnlyList<Checkpoint> All => all;

        public int Index
        {
            get => index;
            set => index = value;
        }

        public bool Active => active;
        public bool IsPlayerNear => playerNear;
        public Vector2 PromptPosition => (Vector2)transform.position + PromptOffset / Ppu;

        public void Configure(WorldPlacement placement)
        {
            if (placement.CheckpointIndex >= 0)
            {
                index = placement.CheckpointIndex;
            }
        }

        /// <summary>The story flag a checkpoint sets once the hero has rested at it.</summary>
        public static string RestedFlag(int index) => $"{RestedFlagPrefix}{index}";

        public static void Reset()
        {
            all.Clear();
            AnyRested = null;
            AnyCampRequested = null;
            NearChanged = null;
        }

        private void Awake()
        {
            Pickup.Box(gameObject, BoxSize, BoxOffset);
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteRenderer>();
            }
            if (sprite == null)
            {
                sprite = Fx.Shapes.Block(
                    transform,
                    "Sprite",
                    new Vector2(8f, 24f),
                    new Vector2(0f, 12f),
                    new Color(0.5f, 0.45f, 0.4f)
                );
            }
            sprite.sortingOrder = -1;
            SetActive(false);
            BuildPrompt();
        }

        /// <summary>"{interact} camp" over the fire, shown while the hero stands here; the same canvas an Interactable uses.</summary>
        private void BuildPrompt()
        {
            var go = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            prompt = go.GetComponent<Canvas>();
            prompt.renderMode = RenderMode.WorldSpace;
            prompt.sortingOrder = 50;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60f, 12f);
            rect.localScale = Vector3.one / Ppu;
            rect.localPosition = new Vector3(0f, PromptOffset.y / Ppu, 0f);
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
            label.gameObject.AddComponent<PromptLabel>().Template = "{interact} camp";
            prompt.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        private void Update()
        {
            if (!playerNear)
            {
                return;
            }
            var router = InputRouter.Instance;
            if (router != null && router.InteractPressed)
            {
                CampRequested?.Invoke(this);
                AnyCampRequested?.Invoke(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (
                other.attachedRigidbody == null
                || !other.attachedRigidbody.TryGetComponent<PlayerController>(out var hero)
            )
            {
                return;
            }
            // Near even while dead: a body that falls in here and respawns here does not
            // enter again, and the camp still has to open (the flow keeps a dead hero out).
            playerNear = true;
            prompt.gameObject.SetActive(true);
            NearChanged?.Invoke(this, true);
            // A dead hero falling past, or a respawn landing on an active checkpoint, is not a new rest.
            if (active || !hero.IsAlive)
            {
                return;
            }
            hero.RestAt(transform.position);
            var flow = Game.GameFlow.Instance;
            if (flow != null)
            {
                flow.ActiveCheckpoint = index;
                flow.RespawnPoint = transform.position;
            }
            Audio.Sfx.Play("checkpoint");
            foreach (var other2 in all)
            {
                other2.SetActive(other2 == this);
            }
            Story.StoryFlags.Set(RestedFlag(index));
            Rested?.Invoke(this);
            AnyRested?.Invoke(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<PlayerController>() != null)
            {
                playerNear = false;
                prompt.gameObject.SetActive(false);
                NearChanged?.Invoke(this, false);
            }
        }

        public void SetActive(bool value)
        {
            active = value;
            var frame = value ? 1 : 0;
            if (frame < frames.Length && frames[frame] != null)
            {
                sprite.sprite = frames[frame];
                sprite.color = Color.white;
            }
            else if (frames.Length == 0)
            {
                sprite.color = value ? new Color(1f, 0.9f, 0.6f) : new Color(0.5f, 0.45f, 0.4f);
            }
        }
    }
}
