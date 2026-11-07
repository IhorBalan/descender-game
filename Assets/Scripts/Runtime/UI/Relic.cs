using Descender.Story;
using Descender.World;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// A story item the hero can examine: the token, the ledger page, the cards.
    /// Pressing interact in range reads its lines (`item_&lt;id&gt;`); when they end the
    /// flag `item:&lt;id&gt;` is set and the relic is gone for the rest of the save. One
    /// whose flag is already set removes itself as soon as its placement configures
    /// it. The prefab an "item" WorldPlacement with dialogue spawns (one without is
    /// loot to pick up). Port of scripts/lore_item.gd.
    /// </summary>
    public sealed class Relic : Interactable, IPlacementReceiver
    {
        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;
        public const float BobHeight = 1.5f;
        public const float BobSpeed = 2.5f;

        /// <summary>How far above the base the sprite floats, in level pixels.</summary>
        public const float RestHeight = 8f;

        [SerializeField]
        private string itemId = "";

        [SerializeField]
        [Tooltip("The sprite that bobs; the prefab's child.")]
        private Transform body;

        private float time;

        public string ItemId => itemId;

        /// <summary>The story flag that remembers a relic was examined.</summary>
        public static string FlagFor(string id) => "item:" + id;

        /// <summary>The placement's item and lines. A relic the save already has does not stand.</summary>
        public void Configure(WorldPlacement placement)
        {
            itemId = placement.Item ?? "";
            Configure(placement.Dialogue, placement.Rotate);
            if (itemId.Length > 0 && StoryFlags.Has(FlagFor(itemId)))
            {
                Remove();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            time = Random.value * Mathf.PI * 2f;
        }

        // LateUpdate, not Update: the base class reads the interact press in its own Update.
        private void LateUpdate()
        {
            if (body == null)
            {
                return;
            }
            time += Time.deltaTime;
            body.localPosition = new Vector3(0f, (RestHeight + Mathf.Sin(time * BobSpeed) * BobHeight) / Ppu, 0f);
        }

        public override void OnDialogueFinished(JObject entry)
        {
            if (itemId.Length > 0)
            {
                StoryFlags.Set(FlagFor(itemId));
            }
            Remove();
        }

        private void Remove()
        {
            // Inactive at once, so a second press cannot examine it before it is destroyed.
            gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
