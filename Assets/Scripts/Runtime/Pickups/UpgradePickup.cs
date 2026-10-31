using Descender.Fx;
using Descender.Player;
using Descender.World;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// A permanent upgrade lying in the world: Kept Light (one more heart), a Vial of
    /// the Basin (one more healing charge) or Church Iron (carried to Old Pell, who
    /// hammers it into the sword). Touching it raises the hero's maximum for the rest of
    /// the save; the pickup is a story flag (see <see cref="Upgrades.FlagFor"/>), so the
    /// level leaves it out once it is taken. Origin at the base. Port of scripts/upgrade.gd.
    /// </summary>
    public sealed class UpgradePickup : MonoBehaviour, IPlacementReceiver
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float BobHeight = 2f;
        public const float BobSpeed = 2.5f;
        public const float SpriteScale = 2f;
        public const float SpriteLift = 16f;
        public static readonly Vector2 BoxSize = new Vector2(20f, 28f);
        public static readonly Vector2 BoxOffset = new Vector2(0f, 14f);
        public static readonly Color Glow = new Color(1.0f, 0.9f, 0.6f);

        [SerializeField]
        private UpgradeKind kind = UpgradeKind.Heart;

        [Tooltip("The story flag this pickup sets; the level fills it in.")]
        [SerializeField]
        private string flag = "";

        [Tooltip("upgrade_heart, upgrade_vial, upgrade_iron, in the enum's order.")]
        [SerializeField]
        private Sprite[] sprites = new Sprite[0];

        private Collider2D box;
        private SpriteRenderer view;
        private float time;
        private bool done;

        public UpgradeKind Kind => kind;
        public string Flag => flag;

        public event System.Action<UpgradeKind> Collected;

        /// <summary>The map's upgrade (heart, vial, iron) and the flag that leaves it out once taken.</summary>
        public void Configure(WorldPlacement placement)
        {
            var kindName = (placement.Upgrade ?? "").ToLowerInvariant();
            var found = kind;
            for (var i = 0; i < 3; i++)
            {
                if (Upgrades.FlagName((UpgradeKind)i) == kindName)
                {
                    found = (UpgradeKind)i;
                }
            }
            var storyFlag = placement.Unless;
            if (string.IsNullOrEmpty(storyFlag))
            {
                var slash = placement.Id.IndexOf('/');
                var region = slash >= 0 ? placement.Id.Substring(0, slash) : placement.Id;
                var name = slash >= 0 ? placement.Id.Substring(slash + 1) : "0";
                storyFlag = Upgrades.FlagFor(found, region, name);
            }
            Set(found, storyFlag);
        }

        /// <summary>What it is and the flag it sets; call before the first frame, or after to redress it.</summary>
        public void Set(UpgradeKind value, string storyFlag)
        {
            kind = value;
            flag = storyFlag;
            if (view != null)
            {
                Dress();
            }
        }

        private void Awake()
        {
            time = Random.value * Mathf.PI * 2f;
            box = Pickup.Box(gameObject, BoxSize, BoxOffset);
            view = GetComponentInChildren<SpriteRenderer>();
            if (view == null)
            {
                view = Shapes.Block(transform, "Sprite", new Vector2(6f, 6f), new Vector2(0f, SpriteLift), Glow);
                view.transform.localScale = Vector3.one * SpriteScale;
            }
            view.sortingOrder = 5;
            Dress();
        }

        private void Dress()
        {
            var index = (int)kind;
            if (index >= 0 && index < sprites.Length && sprites[index] != null)
            {
                view.sprite = sprites[index];
                view.color = Color.white;
            }
            gameObject.name = $"Upgrade ({kind})";
        }

        private void Update()
        {
            time += Time.deltaTime;
            view.transform.localPosition = new Vector3(0f, Pickup.Bob(time, BobSpeed, BobHeight, SpriteLift), 0f);
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
            hero.ApplyUpgrade(kind, flag);
            Audio.Sfx.Play("heal");
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, SpriteLift / Ppu), Glow);
            Collected?.Invoke(kind);
            Destroy(gameObject);
        }
    }
}
