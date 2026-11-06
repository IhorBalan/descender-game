using Descender.Economy;
using Descender.Fx;
using Descender.Player;
using Descender.World;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// An item lying in the world: a boss's unique loot, or a consumable an enemy left.
    /// Drawn as a gem in the item's colour with a glow that breathes. Touching it puts
    /// the item in the pack; a unique sets its story flag (`loot:&lt;item&gt;`) so it is
    /// never dropped again. Port of scripts/loot_drop.gd.
    /// </summary>
    public sealed class LootDrop : MonoBehaviour, IPlacementReceiver
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float BobHeight = 2f;
        public const float BobSpeed = 2.5f;
        public static readonly Vector2 BoxSize = new Vector2(20f, 24f);
        public static readonly Vector2 BoxOffset = new Vector2(0f, 10f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color Shine = new Color(1f, 1f, 1f, 0.85f);

        [SerializeField]
        private string itemId = "";

        private Collider2D box;
        private Transform gem;
        private SpriteRenderer glow;
        private float time;
        private bool done;
        private bool drawn;

        public string ItemId => itemId;
        public bool IsUnique => Items.IsUnique(itemId);

        public event System.Action<string> Collected;

        public static LootDrop Spawn(string itemId, Vector2 at)
        {
            var drop = new GameObject($"LootDrop ({itemId})").AddComponent<LootDrop>();
            drop.transform.position = at;
            drop.Set(itemId);
            return drop;
        }

        /// <summary>A relic the map lays down: the placement's item id.</summary>
        public void Configure(WorldPlacement placement)
        {
            if (!string.IsNullOrEmpty(placement.Item))
            {
                Set(placement.Item);
            }
        }

        public void Set(string value)
        {
            itemId = value;
            if (drawn)
            {
                Destroy(gem.gameObject);
                drawn = false;
            }
            Draw();
        }

        private void Awake()
        {
            time = Random.value * Mathf.PI * 2f;
            box = Pickup.Box(gameObject, BoxSize, BoxOffset);
        }

        private void Start()
        {
            Draw();
        }

        private void Draw()
        {
            if (drawn)
            {
                return;
            }
            drawn = true;
            var color = Items.ColorOf(itemId);
            var size = IsUnique ? 6f : 4f;
            gem = new GameObject("Gem").transform;
            gem.SetParent(transform, false);
            var faint = new Color(color.r, color.g, color.b, 0.25f);
            glow = Shapes.Block(gem, "Glow", Vector2.one * size * 4.4f, Vector2.zero, faint);
            glow.sprite = Shapes.Disc;
            glow.sortingOrder = 5;
            var shadow = Shapes.Block(gem, "Shadow", new Vector2(size * 1.6f, size * 2f), new Vector2(1f, -1f), Shadow);
            shadow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            shadow.transform.localScale = new Vector3(0.7f, 1f, 1f);
            shadow.sortingOrder = 6;
            var face = Shapes.Block(gem, "Face", new Vector2(size * 1.6f, size * 2f), Vector2.zero, color);
            face.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            face.transform.localScale = new Vector3(0.7f, 1f, 1f);
            face.sortingOrder = 7;
            Shapes.Block(gem, "Shine", new Vector2(1f, 2f), new Vector2(-1f, size * 0.6f), Shine).sortingOrder = 8;
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (gem == null)
            {
                return;
            }
            gem.localPosition = new Vector3(0f, Pickup.Bob(time, BobSpeed, BobHeight, 10f), 0f);
            var color = glow.color;
            color.a = 0.25f + Mathf.Sin(time * 3f) * 0.1f;
            glow.color = color;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var hero = Pickup.HeroFrom(other);
            if (done || hero == null)
            {
                return;
            }
            done = true;
            box.enabled = false;
            hero.CollectLoot(itemId);
            Audio.Sfx.Play(IsUnique ? "heal" : "pickup");
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, 10f / Ppu), Items.ColorOf(itemId));
            Collected?.Invoke(itemId);
            Destroy(gameObject);
        }
    }
}
