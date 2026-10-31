using Descender.Fx;
using Descender.Player;
using Descender.Save;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// A feather shed by the angels. Touching it unlocks one of the hero's half-finished
    /// angel powers for the rest of the run: pickups are not rebuilt on respawn and the
    /// ability stays unlocked. Origin at the base. Port of scripts/ability_pickup.gd.
    /// </summary>
    public sealed class AbilityPickup : MonoBehaviour
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float BobHeight = 2f;
        public const float BobSpeed = 3f;
        public const float SpriteLift = 8f;
        public static readonly Vector2 BoxSize = new Vector2(16f, 16f);
        public static readonly Vector2 BoxOffset = new Vector2(0f, 8f);
        public static readonly Color Glow = new Color(1.0f, 0.95f, 0.6f);

        [SerializeField]
        private Ability ability = Ability.Wings;

        [SerializeField]
        private SpriteRenderer sprite;

        private Collider2D box;
        private float time;
        private bool done;

        public Ability Ability
        {
            get => ability;
            set => ability = value;
        }

        public event System.Action<Ability> Collected;

        private void Awake()
        {
            time = Random.value * Mathf.PI * 2f;
            box = Pickup.Box(gameObject, BoxSize, BoxOffset);
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteRenderer>();
            }
            if (sprite == null)
            {
                sprite = Shapes.Block(transform, "Sprite", new Vector2(6f, 10f), new Vector2(0f, SpriteLift), Glow);
            }
            sprite.sortingOrder = 5;
        }

        private void Update()
        {
            time += Time.deltaTime;
            sprite.transform.localPosition = new Vector3(0f, Pickup.Bob(time, BobSpeed, BobHeight, SpriteLift), 0f);
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
            hero.Grant(ability);
            Audio.Sfx.Play("ability");
            Feedback.Spark((Vector2)transform.position + new Vector2(0f, SpriteLift / Ppu), Glow);
            Collected?.Invoke(ability);
            Destroy(gameObject);
        }
    }
}
