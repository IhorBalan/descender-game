using System.Collections;
using System.Collections.Generic;
using Descender.Story;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// Runs the level's dialogue: an interactable asks, the first of its entries
    /// whose flags match plays in the <see cref="DialogueBox"/>, and when the box
    /// closes the entry's flags are set, its promise is announced on the HUD, its
    /// gift, warp and save go out as events for the game session, a trader's wares
    /// open, and its `then` carries straight on. The dialogue file is a TextAsset
    /// (Assets/Data/dialogue/<level>.json). Port of the dialogue part of
    /// scripts/main.gd; lives in Main.unity beside the box and the HUD.
    /// </summary>
    public sealed class DialogueRunner : MonoBehaviour
    {
        public const string EndingFlagPrefix = "ending:";
        public const string GiftLevelId = "gift";

        /// <summary>The upgrade kinds a `gives` field may name.</summary>
        public static readonly string[] UpgradeKinds = { "heart", "vial", "iron" };

        /// <summary>What the HUD says when an ending plays, by its flag.</summary>
        public static readonly Dictionary<string, string> Endings = new Dictionary<string, string>
        {
            ["ending:carry"] = "The end: the Low Gate, from below.",
            ["ending:sit"] = "The end: the floor, counting.",
            ["ending:clock"] = "The end: night in the Garden.",
        };

        [SerializeField]
        private TextAsset dialogueFile;

        [SerializeField]
        private DialogueBox box;

        [SerializeField]
        private Hud hud;

        [SerializeField]
        private ShopScreen shop;

        private Interactable talkingTo;
        private bool shopPending;
        private readonly HashSet<Interactable> registered = new HashSet<Interactable>();

        /// <summary>The level's dialogue, or null when it has none.</summary>
        public DialogueData Dialogue { get; private set; }

        /// <summary>Set by the session while the hero may not talk: a transition, a death, an ending.</summary>
        public bool Blocked { get; set; }

        /// <summary>Whether one of the endings has played.</summary>
        public bool Ended { get; private set; }

        /// <summary>The box opened on an entry; the hero stands still until <see cref="DialogueEnded"/>.</summary>
        public event System.Action<JObject> DialogueStarted;

        public event System.Action<JObject> DialogueEnded;

        /// <summary>An entry handed over an upgrade: its kind (heart, vial, iron) and the flag that remembers it.</summary>
        public event System.Action<string, string> GiftGiven;

        /// <summary>An entry forged the sword once more; the HUD wants the new damage.</summary>
        public event System.Action SwordForged;

        /// <summary>An entry with `warp: start` ended: the hero goes back to the level's start.</summary>
        public event System.Action WarpToStartRequested;

        /// <summary>An entry with `save: true` ended. The handler saves and returns whether it landed.</summary>
        public System.Func<bool> SaveHandler { get; set; }

        /// <summary>An ending flag was set; the game is over.</summary>
        public event System.Action<string> EndingReached;

        private void Awake()
        {
            if (dialogueFile != null)
            {
                Load(dialogueFile);
            }
        }

        private void OnEnable()
        {
            if (box != null)
            {
                box.Finished += OnFinished;
            }
        }

        private void OnDisable()
        {
            if (box != null)
            {
                box.Finished -= OnFinished;
            }
        }

        /// <summary>Points the runner at its box, HUD and shop when they are made in code rather than in the scene.</summary>
        public void Bind(DialogueBox dialogueBox, Hud gameHud, ShopScreen shopScreen)
        {
            if (box != null)
            {
                box.Finished -= OnFinished;
            }
            box = dialogueBox;
            hud = gameHud;
            shop = shopScreen;
            if (box != null && isActiveAndEnabled)
            {
                box.Finished += OnFinished;
            }
        }

        public void Load(TextAsset file) => Load(file.text, file.name);

        public void Load(string json, string path)
        {
            Dialogue = new DialogueData(path, json);
        }

        /// <summary>Forgets the level's dialogue (a level with no file).</summary>
        public void Unload()
        {
            Dialogue = null;
        }

        /// <summary>Listens to an interactable; a region's NPCs, relics and triggers register as they load.</summary>
        public void Register(Interactable interactable)
        {
            if (registered.Add(interactable))
            {
                interactable.Interacted += Interact;
            }
        }

        public void Unregister(Interactable interactable)
        {
            if (registered.Remove(interactable))
            {
                interactable.Interacted -= Interact;
            }
        }

        /// <summary>Lets the box's input through again after an ending, for a new run.</summary>
        public void ResetRun()
        {
            Ended = false;
            Blocked = false;
            shopPending = false;
            talkingTo = null;
        }

        /// <summary>
        /// An NPC, relic or trigger asked for its dialogue: the first entry whose flags
        /// match plays. Returns false when nothing could play (a trader with nothing
        /// left to say opens the wares instead).
        /// </summary>
        public bool Interact(Interactable source)
        {
            if (Dialogue == null || box == null || box.IsOpen || Blocked || Ended)
            {
                return false;
            }
            var entry = Dialogue.Pick(source.DialogueIds);
            var trades = source is Npc npc && npc.Trades;
            if (entry == null)
            {
                entry = Dialogue.Pick(source.RotationIds());
                // A trader whose unique lines are spent shows the wares after a word.
                shopPending = trades;
            }
            if (entry == null)
            {
                if (trades && shop != null)
                {
                    shop.Open(((Npc)source).Shop);
                    return true;
                }
                return false;
            }
            return Play(entry, source);
        }

        /// <summary>Opens the box on an entry; `source` is the thing that spoke, or null for a boss's lines.</summary>
        public bool Play(JObject entry, Interactable source = null)
        {
            if (box == null || box.IsOpen || DialogueData.LineCount(entry) == 0)
            {
                return false;
            }
            talkingTo = source;
            source?.SetTalking(true);
            // The box covers the message line.
            hud?.HideMessage();
            box.Play(entry);
            DialogueStarted?.Invoke(entry);
            return true;
        }

        /// <summary>Plays an entry by id when its flags allow it (a boss's intro, a checkpoint's word).</summary>
        public bool PlayEntry(string id, Interactable source = null)
        {
            var entry = Dialogue?.Entry(id);
            return entry != null && DialogueData.Available(entry) && Play(entry, source);
        }

        private void OnFinished(JObject entry)
        {
            var forged = false;
            var ending = "";
            foreach (var flag in DialogueData.Sets(entry))
            {
                var started = Quests.StartedBy(flag);
                var kept = Quests.FinishedBy(flag);
                if (started != null && !StoryFlags.Has(flag))
                {
                    hud?.QueueMessage($"A promise: {started.Title}", 3f);
                }
                else if (kept != null && !StoryFlags.Has(flag))
                {
                    hud?.QueueMessage($"Promise kept: {kept.Title}", 3f);
                }
                StoryFlags.Set(flag);
                forged = forged || flag.StartsWith(MenuText.SwordFlagPrefix, System.StringComparison.Ordinal);
                if (flag.StartsWith(EndingFlagPrefix, System.StringComparison.Ordinal))
                {
                    ending = flag;
                }
            }
            StoryFlags.Set(DialogueData.SeenFlag(entry?.Value<string>("id") ?? ""));
            if (forged)
            {
                SwordForged?.Invoke();
            }
            if (ending.Length > 0)
            {
                EndGame(ending);
            }
            if (DialogueData.Warp(entry) == "start")
            {
                WarpToStartRequested?.Invoke();
            }
            if (DialogueData.Saves(entry) && SaveHandler != null && SaveHandler())
            {
                hud?.QueueMessage("Game saved", 2f);
            }
            var source = talkingTo;
            talkingTo = null;
            var openShop = "";
            if (source != null)
            {
                source.SetTalking(false);
                if (shopPending && source is Npc npc && npc.Trades)
                {
                    openShop = npc.Shop;
                }
                source.OnDialogueFinished(entry);
            }
            shopPending = false;
            DialogueEnded?.Invoke(entry);
            // The gift, the wares and the next entry land once the hero has been
            // released for this frame, so the press that closed the box is not read twice.
            StartCoroutine(AfterFrame(entry, openShop));
        }

        private IEnumerator AfterFrame(JObject entry, string openShop)
        {
            yield return null;
            var gift = DialogueData.Gives(entry);
            if (System.Array.IndexOf(UpgradeKinds, gift) >= 0)
            {
                var flag = $"upgrade:{gift}:{GiftLevelId}:{entry?.Value<string>("id") ?? ""}";
                if (!StoryFlags.Has(flag))
                {
                    GiftGiven?.Invoke(gift, flag);
                }
            }
            if (openShop.Length > 0 && shop != null && !Ended)
            {
                shop.Open(openShop);
            }
            // An entry's `then` (or the choice just taken) carries straight on.
            var nextId = DialogueData.NextId(entry);
            if (nextId.Length > 0 && Dialogue != null && !Ended)
            {
                var next = Dialogue.Entry(nextId);
                if (next != null)
                {
                    Play(next);
                }
            }
        }

        private void EndGame(string flag)
        {
            Ended = true;
            var text = Endings.TryGetValue(flag, out var line) ? line : "The end.";
            hud?.ShowMessage($"{text}   Press {{restart}} to restart", -1f);
            EndingReached?.Invoke(flag);
        }
    }
}
