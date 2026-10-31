using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// A timed power-up lying in a room: a Halo (the next hit is turned aside), a Lit
    /// Blade (the sword hits harder) or Zeal (faster dashes and feet). Touching it gives
    /// the hero the power for <see cref="HeroPowers.Duration"/> seconds; it is lost on
    /// death, and the level puts the pickup back with the enemies when the hero
    /// respawns. Origin at the base. Port of scripts/power_up.gd.
    /// </summary>
    public sealed class PowerUp : MonoBehaviour
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float BobHeight = 2f;
        public const float BobSpeed = 4f;
        public const float SpriteScale = 2f;
        public const float SpriteLift = 16f;
        public static readonly Vector2 BoxSize = new Vector2(20f, 28f);
        public static readonly Vector2 BoxOffset = new Vector2(0f, 14f);
        public static readonly Color[] Colors =
        {
            new Color(1.0f, 0.85f, 0.4f),
            new Color(1.0f, 0.7f, 0.3f),
            new Color(0.6f, 0.9f, 1.0f),
        };

        [SerializeField]
        private Power power = Power.Halo;

        [Tooltip("power_halo, power_blade, power_zeal, in the enum's order.")]
        [SerializeField]
        private Sprite[] sprites = new Sprite[0];

        private Collider2D box;
        private SpriteRenderer view;
        private float time;
        private bool done;

        /// <summary>The prefab <see cref="Spawn"/> clones; the level or the bootstrap sets it. Shapes stand in when it is null.</summary>
        public static PowerUp Prefab { get; set; }

        public Power Power => power;

        public event System.Action<Power> Collected;

        /// <summary>What the HUD says when a power-up lands.</summary>
        public static string MessageFor(Power power) => HeroPowers.Message(power);

        public static Color ColorOf(Power power) => Colors[Mathf.Clamp((int)power, 0, Colors.Length - 1)];

        /// <summary>A power-up on the floor at <paramref name="at"/> (its base), where a dying enemy left one.</summary>
        public static PowerUp Spawn(Power power, Vector2 at)
        {
            PowerUp pickup;
            if (Prefab != null)
            {
                pickup = Instantiate(Prefab, at, Quaternion.identity);
            }
            else
            {
                pickup = new GameObject("PowerUp").AddComponent<PowerUp>();
                pickup.transform.position = at;
            }
            pickup.Set(power);
            return pickup;
        }

        public void Set(Power value)
        {
            power = value;
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
                view = Shapes.Block(transform, "Sprite", new Vector2(6f, 6f), new Vector2(0f, SpriteLift), Color.white);
                view.transform.localScale = Vector3.one * SpriteScale;
            }
            view.sortingOrder = 5;
            Dress();
        }

        private void Dress()
        {
            var index = (int)power;
            if (index >= 0 && index < sprites.Length && sprites[index] != null)
            {
                view.sprite = sprites[index];
                view.color = Color.white;
            }
            else
            {
                view.sprite = Shapes.Disc;
                view.color = ColorOf(power);
            }
            gameObject.name = $"PowerUp ({power})";
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
            hero.GrantPower(power);
            Audio.Sfx.Play("pickup");
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, SpriteLift / Ppu), ColorOf(power));
            Collected?.Invoke(power);
            Destroy(gameObject);
        }
    }
}
