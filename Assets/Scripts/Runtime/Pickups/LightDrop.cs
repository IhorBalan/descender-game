using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// Currency lying where an enemy died: a cluster of light (what a killed thing
    /// leaks) or a tally bone. It bobs where it fell and flies to the hero once he
    /// comes near; touching it puts the amount in the purse. Light left lying is lost
    /// to a death (the level clears the loot with the motes). Drawn from shapes. Port
    /// of scripts/light_drop.gd.
    /// </summary>
    public sealed class LightDrop : MonoBehaviour
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float BobHeight = 1.5f;
        public const float BobSpeed = 3.5f;
        public const float PullRange = 56f;
        public const float PullSpeed = 220f;
        public const float Radius = 7f;
        public static readonly Color LightColor = new Color(1.0f, 0.95f, 0.65f);
        public static readonly Color LightCore = new Color(1.0f, 1.0f, 0.9f);
        public static readonly Color BoneColor = new Color(0.95f, 0.92f, 0.85f);
        public static readonly Color BoneShade = new Color(0.6f, 0.55f, 0.5f);

        private Collider2D disc;
        private Transform[] motes = new Transform[0];
        private Vector2 rest;
        private float time;
        private bool pulled;
        private bool done;

        public string Currency { get; private set; } = "light";
        public int Amount { get; private set; } = 1;
        public bool IsPulled => pulled;

        public event System.Action<string, int> Collected;

        public static LightDrop Spawn(string currency, int amount, Vector2 at)
        {
            var drop = new GameObject($"LightDrop ({currency})").AddComponent<LightDrop>();
            drop.transform.position = at;
            drop.Set(currency, amount);
            return drop;
        }

        /// <summary>What it holds; call before the first frame.</summary>
        public void Set(string currency, int amount)
        {
            Currency = currency;
            Amount = amount;
            Draw();
        }

        private void Awake()
        {
            rest = transform.position;
            time = Random.value * Mathf.PI * 2f;
            disc = Pickup.Disc(gameObject, Radius);
        }

        private void Start()
        {
            if (motes.Length == 0)
            {
                Draw();
            }
        }

        private void Draw()
        {
            foreach (var mote in motes)
            {
                if (mote != null)
                {
                    Destroy(mote.gameObject);
                }
            }
            if (Currency == "bones")
            {
                var shade = Shapes.Block(transform, "Shade", new Vector2(9f, 2f), new Vector2(0.5f, -1f), BoneShade);
                var shaft = Shapes.Block(transform, "Shaft", new Vector2(9f, 2f), new Vector2(0.5f, 0f), BoneColor);
                shade.sortingOrder = 5;
                shaft.sortingOrder = 6;
                Shapes.Block(transform, "Left", new Vector2(2f, 4f), new Vector2(-4f, 0f), BoneColor).sortingOrder = 6;
                Shapes.Block(transform, "Right", new Vector2(2f, 4f), new Vector2(5f, 0f), BoneColor).sortingOrder = 6;
                motes = new Transform[0];
                return;
            }
            // A few motes for a few light, more for more.
            var count = Mathf.Clamp(1 + Amount / 4, 1, 4);
            motes = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var mote = new GameObject($"Mote {i}").transform;
                mote.SetParent(transform, false);
                var faint = new Color(LightColor.r, LightColor.g, LightColor.b, 0.3f);
                var halo = Shapes.Block(mote, "Halo", new Vector2(5f, 5f), Vector2.zero, faint);
                var light = Shapes.Block(mote, "Light", new Vector2(3f, 3f), Vector2.zero, LightColor);
                var core = Shapes.Block(mote, "Core", new Vector2(1f, 1f), Vector2.zero, LightCore);
                halo.sprite = Shapes.Disc;
                light.sprite = Shapes.Disc;
                core.sprite = Shapes.Disc;
                halo.sortingOrder = 5;
                light.sortingOrder = 6;
                core.sortingOrder = 7;
                motes[i] = mote;
            }
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            time += dt;
            var hero = PlayerController.Current;
            if (hero != null && hero.IsAlive)
            {
                var target = (Vector2)hero.transform.position + new Vector2(0f, 14f / Ppu);
                var position = (Vector2)transform.position;
                if (pulled || Vector2.Distance(position, target) <= PullRange / Ppu)
                {
                    pulled = true;
                    position = Vector2.MoveTowards(position, target, PullSpeed / Ppu * dt);
                    transform.position = position;
                    // A drop that has flown onto the hero counts, even if the overlap already
                    // fired while he was out of reach of it.
                    if (Vector2.Distance(position, target) < 4f / Ppu)
                    {
                        Take(hero);
                    }
                }
            }
            if (!pulled)
            {
                transform.position = new Vector3(rest.x, rest.y + Pickup.Bob(time, BobSpeed, BobHeight), 0f);
            }
            for (var i = 0; i < motes.Length; i++)
            {
                var angle = time * 1.5f + i * Mathf.PI * 2f / motes.Length;
                var spread = motes.Length > 1 ? 3f : 0f;
                motes[i].localPosition =
                    new Vector3(Mathf.Cos(angle) * spread, Mathf.Sin(angle) * 0.5f * spread, 0f) / Ppu;
                motes[i].localScale = Vector3.one * (1f + Mathf.Sin(time * 6f + i) * 0.25f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Take(Pickup.HeroFrom(other));
        }

        private void Take(PlayerController hero)
        {
            if (done || hero == null)
            {
                return;
            }
            done = true;
            disc.enabled = false;
            hero.CollectCurrency(Currency, Amount);
            Audio.Sfx.Play("pickup", Currency == "light" ? -8f : -2f);
            Feedback.Spark(transform.position, Currency == "light" ? LightColor : BoneColor);
            Collected?.Invoke(Currency, Amount);
            Destroy(gameObject);
        }
    }
}
