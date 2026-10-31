using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// A mote of light some enemies leave behind. Touching it restores one heart. It
    /// fades after a while so the level does not fill up with them, blinking for its
    /// last seconds. Drawn from shapes. Port of scripts/heal_mote.gd.
    /// </summary>
    public sealed class HealMote : MonoBehaviour
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const int Heal = 1;
        public const float Lifetime = 8f;
        public const float BlinkTime = 2f;
        public const float BobHeight = 2f;
        public const float BobSpeed = 4f;
        public const float Radius = 6f;
        public static readonly Color Glow = new Color(1.0f, 0.95f, 0.6f);
        public static readonly Color Core = new Color(1.0f, 1.0f, 0.9f);

        private readonly Collider2D[] hits = new Collider2D[2];
        private Collider2D disc;
        private SpriteRenderer halo;
        private SpriteRenderer mote;
        private SpriteRenderer core;
        private Vector2 rest;
        private float time;
        private bool done;

        public event System.Action Collected;

        /// <summary>How many motes are lying about; for the tests.</summary>
        public static int Count { get; private set; }

        public static HealMote Spawn(Vector2 at)
        {
            var mote = new GameObject("HealMote").AddComponent<HealMote>();
            mote.transform.position = at;
            return mote;
        }

        private void Awake()
        {
            Count++;
            rest = transform.position;
            disc = Pickup.Disc(gameObject, Radius);
            halo = Shapes.Block(
                transform,
                "Halo",
                new Vector2(7f, 7f),
                Vector2.zero,
                new Color(Glow.r, Glow.g, Glow.b, 0.35f)
            );
            mote = Shapes.Block(transform, "Light", new Vector2(4f, 4f), Vector2.zero, Glow);
            core = Shapes.Block(transform, "Core", new Vector2(2f, 2f), Vector2.zero, Core);
            halo.sprite = Shapes.Disc;
            mote.sprite = Shapes.Disc;
            core.sprite = Shapes.Disc;
            halo.sortingOrder = 5;
            mote.sortingOrder = 6;
            core.sortingOrder = 7;
        }

        private void OnDestroy()
        {
            Count = Mathf.Max(Count - 1, 0);
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (time >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = new Vector3(rest.x, rest.y + Pickup.Bob(time, BobSpeed, BobHeight), 0f);
            var left = Lifetime - time;
            var visible = left > BlinkTime || left % 0.2f < 0.12f;
            halo.enabled = visible;
            mote.enabled = visible;
            core.enabled = visible;
            var pulse = 1f + Mathf.Sin(time * 6f) * 0.2f;
            mote.transform.localScale = Vector3.one * pulse;
            halo.transform.localScale = Vector3.one * (0.8f + pulse * 0.3f);
        }

        private void FixedUpdate()
        {
            // A hero who was at full health when he stepped on the mote and got hurt while
            // standing on it takes it now, without stepping off and on.
            var found = Physics2D.OverlapCircle(
                transform.position,
                Radius / Ppu,
                Combat.Layers.Any(Combat.Layers.PlayerMask),
                hits
            );
            for (var i = 0; i < found; i++)
            {
                Take(Pickup.HeroFrom(hits[i]));
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Take(Pickup.HeroFrom(other));
        }

        private void Take(PlayerController hero)
        {
            if (done || hero == null || !hero.RestoreHealth(Heal))
            {
                return;
            }
            done = true;
            disc.enabled = false;
            Audio.Sfx.Play("pickup", -4f);
            Feedback.Spark(transform.position, Glow);
            Collected?.Invoke();
            Destroy(gameObject);
        }
    }
}
