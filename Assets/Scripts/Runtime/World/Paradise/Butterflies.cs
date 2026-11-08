using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Butterflies around the garden's flowers: each keeps to its own flower bed, wandering in
    /// an unsteady, looping flight, wings beating fast and uneven; now and then it settles and
    /// rests with its wings slowly opening and closing; if the hero comes close it starts up
    /// and flutters away, then drifts back. Only the ones near the view are updated. Drawn
    /// with a procedural sprite, so no art is needed.
    /// </summary>
    public sealed class Butterflies : MonoBehaviour
    {
        [SerializeField]
        private List<Vector2> beds = new List<Vector2>();

        [SerializeField]
        private int sortingOrder = -84;

        private static Sprite wing;
        private readonly List<Butterfly> flock = new List<Butterfly>();

        private sealed class Butterfly
        {
            public SpriteRenderer renderer;
            public Vector2 home;
            public Vector2 position;
            public float seed;
            public float rest;
            public float scare;
        }

        /// <summary>Where the flower beds are (the butterflies' homes), for editor tools.</summary>
        public void Configure(List<Vector2> flowerBeds)
        {
            beds = new List<Vector2>(flowerBeds);
        }

        private void Start()
        {
            if (wing == null)
            {
                wing = MakeWing();
            }
            Color[] tints = { new Color(1f, 0.98f, 0.92f), new Color(1f, 0.93f, 0.62f), new Color(0.86f, 0.92f, 1f) };
            for (var i = 0; i < beds.Count; i++)
            {
                var go = new GameObject("Butterfly");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = wing;
                r.color = tints[i % tints.Length];
                r.sortingOrder = sortingOrder;
                var home = beds[i] + new Vector2(0f, 0.9f);
                flock.Add(
                    new Butterfly
                    {
                        renderer = r,
                        home = home,
                        position = home,
                        seed = i * 7.31f + 0.37f,
                    }
                );
            }
        }

        private void Update()
        {
            var camera = CameraRig.GameCamera.Main;
            if (camera == null)
            {
                return;
            }
            var view = camera.HalfExtents + new Vector2(3f, 3f);
            var c = (Vector2)camera.transform.position;
            var hero = Player.PlayerController.Current;
            var t = Time.time;
            var dt = Time.deltaTime;
            foreach (var b in flock)
            {
                var near = Mathf.Abs(b.home.x - c.x) < view.x && Mathf.Abs(b.home.y - c.y) < view.y;
                b.renderer.enabled = near;
                if (!near)
                {
                    continue;
                }
                if (hero != null)
                {
                    var away = b.position - ((Vector2)hero.transform.position + Vector2.up * 0.8f);
                    if (away.sqrMagnitude < 2.2f)
                    {
                        b.scare = 1.6f;
                        b.rest = 0f;
                    }
                }
                b.scare = Mathf.Max(0f, b.scare - dt);
                float beat;
                if (b.rest > 0f)
                {
                    // Settled: wings opening and closing slowly.
                    b.rest -= dt;
                    beat = 0.55f + 0.45f * Mathf.Sin(t * 2.2f + b.seed);
                }
                else
                {
                    // An unsteady loop around its bed: two incommensurate wanders and a bob.
                    var s = b.seed;
                    var target =
                        b.home
                        + new Vector2(
                            Mathf.Sin(t * 0.53f + s) * 1.4f + Mathf.Sin(t * 1.27f + s * 2f) * 0.5f,
                            Mathf.Sin(t * 0.71f + s * 3f) * 0.6f + Mathf.Sin(t * 3.1f + s) * 0.12f
                        );
                    if (b.scare > 0f && hero != null)
                    {
                        var flee = (b.position - (Vector2)hero.transform.position).normalized;
                        target = b.position + (flee + Vector2.up) * 2f;
                    }
                    b.position = Vector2.Lerp(b.position, target, 1f - Mathf.Exp(-dt * (b.scare > 0f ? 4f : 1.6f)));
                    // A gust pushes it off its line; it works back against it.
                    b.position.x += GardenWind.At(b.position.x) * 0.6f * dt;
                    // Fast, uneven beats: never a clean sine.
                    var f = 9f + 3f * Mathf.Sin(t * 0.9f + s);
                    beat = Mathf.Abs(Mathf.Sin(t * f + Mathf.Sin(t * 2.3f + s) * 0.8f));
                    if (b.scare <= 0f && Random.value < dt * 0.05f)
                    {
                        b.rest = Random.Range(2f, 5f);
                    }
                }
                var dir = Mathf.Sin(t * 0.53f + b.seed) >= 0f ? 1f : -1f;
                b.renderer.transform.position = new Vector3(b.position.x, b.position.y, 0f);
                // The wings seen edge-on as they close.
                b.renderer.transform.localScale = new Vector3(0.18f + 0.82f * beat, 1f, 1f) * 0.85f;
                b.renderer.flipX = dir < 0f;
            }
        }

        /// <summary>A pair of wings seen from the side and above: rounded fore- and hindwings, a slim body.</summary>
        private static Sprite MakeWing()
        {
            const int w = 40;
            const int h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var u = (x + 0.5f) / w * 2f - 1f;
                    var v = (y + 0.5f) / h * 2f - 1f;
                    var s = Mathf.Abs(u);
                    var fore = new Vector2((s - 0.5f) / 0.5f, (v - 0.25f) / 0.62f).magnitude;
                    var hind = new Vector2((s - 0.38f) / 0.36f, (v + 0.42f) / 0.42f).magnitude;
                    var body = new Vector2(u / 0.07f, v / 0.7f).magnitude;
                    var d = Mathf.Min(Mathf.Min(fore, hind), body);
                    var a = Mathf.Clamp01((1f - d) * 6f);
                    // Darker veins toward the body, the wing edges lighter.
                    var shade = (byte)Mathf.RoundToInt(Mathf.Lerp(200f, 255f, s));
                    pixels[y * w + x] = new Color32(shade, shade, shade, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 128f);
        }
    }
}
