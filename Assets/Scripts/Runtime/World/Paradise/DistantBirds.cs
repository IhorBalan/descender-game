using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Now and then a flock of birds crosses Paradise's sky far away, between the mountains
    /// and the floating islands: a loose, shifting V that flaps and glides in turns, hazed
    /// toward the sky like everything that far. Far away means nearly fixed to the view: the
    /// flock lives in view space and drifts across it. Sits on the region's root; the bird
    /// silhouettes are drawn once, procedurally.
    /// </summary>
    public sealed class DistantBirds : MonoBehaviour
    {
        private const int Frames = 4;

        [SerializeField]
        private Vector2 interval = new Vector2(14f, 34f);

        [SerializeField]
        private Vector2Int flockSize = new Vector2Int(5, 11);

        [Tooltip("Across the view, in units a second.")]
        [SerializeField]
        private Vector2 speed = new Vector2(1.2f, 2.2f);

        [SerializeField]
        private Color tint = new Color(0.32f, 0.38f, 0.5f, 0.75f);

        [SerializeField]
        private int sortingOrder = -983;

        private static Sprite[] wings;
        private readonly List<Bird> birds = new List<Bird>();
        private float wait;
        private float flockX;
        private float flockY;
        private float flockSpeed;
        private int direction;

        private sealed class Bird
        {
            public SpriteRenderer renderer;
            public Vector2 slot;
            public float phase;
            public float rate;
            public float glide;
        }

        private void Start()
        {
            wait = Random.Range(3f, 8f);
        }

        private void LateUpdate()
        {
            var camera = CameraRig.GameCamera.Main;
            if (camera == null)
            {
                return;
            }
            var half = camera.HalfExtents;
            if (birds.Count == 0)
            {
                wait -= Time.deltaTime;
                if (wait <= 0f)
                {
                    Launch(half);
                }
                return;
            }
            flockX += direction * flockSpeed * Time.deltaTime;
            flockY += Mathf.Sin(Time.time * 0.21f) * 0.05f * Time.deltaTime;
            var at = (Vector2)camera.transform.position;
            var t = Time.time;
            foreach (var b in birds)
            {
                // The formation breathes: each bird wanders a little around its slot.
                var wander =
                    new Vector2(Mathf.Sin(t * 0.7f + b.phase * 3f), Mathf.Sin(t * 0.9f + b.phase * 5f)) * 0.18f;
                var p = at + new Vector2(flockX, flockY) + b.slot + wander;
                b.renderer.transform.position = new Vector3(p.x, p.y, 0f);
                // Flap in bursts, then glide with the wings level.
                var flapping = Mathf.Sin(t * 0.35f + b.glide) > -0.35f;
                var frame = flapping ? Mathf.FloorToInt(Mathf.Repeat((t + b.phase) * b.rate, 1f) * Frames) : 1;
                b.renderer.sprite = wings[frame];
                b.renderer.flipX = direction < 0;
            }
            if (Mathf.Abs(flockX) > half.x + 4f)
            {
                foreach (var b in birds)
                {
                    Destroy(b.renderer.gameObject);
                }
                birds.Clear();
                wait = Random.Range(interval.x, interval.y);
            }
        }

        private void Launch(Vector2 half)
        {
            if (wings == null)
            {
                wings = MakeWings();
            }
            direction = Random.value < 0.75f ? 1 : -1;
            flockX = -direction * (half.x + 3f);
            flockY = Random.Range(half.y * 0.25f, half.y * 0.75f);
            flockSpeed = Random.Range(speed.x, speed.y);
            var count = Random.Range(flockSize.x, flockSize.y + 1);
            var scale = Random.Range(0.75f, 1.05f);
            for (var i = 0; i < count; i++)
            {
                // A loose V: two arms trailing behind the leader.
                var arm = i == 0 ? 0 : (i % 2 == 0 ? 1 : -1);
                var rank = (i + 1) / 2;
                var slot = new Vector2(-direction * rank * 0.55f, arm * rank * 0.3f) + Random.insideUnitCircle * 0.2f;
                var go = new GameObject("Bird");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * scale * Random.Range(0.85f, 1.1f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = wings[0];
                r.color = tint;
                r.sortingOrder = sortingOrder;
                birds.Add(
                    new Bird
                    {
                        renderer = r,
                        slot = slot,
                        phase = Random.value,
                        rate = Random.Range(2.6f, 3.6f),
                        glide = Random.value * 6f,
                    }
                );
            }
        }

        /// <summary>A gull-like silhouette in four wing positions, white (the renderer tints it).</summary>
        private static Sprite[] MakeWings()
        {
            const int w = 48;
            const int h = 28;
            var result = new Sprite[Frames];
            // Wing tip height for each frame: up, level, down, level.
            float[] lift = { 0.9f, 0.15f, -0.6f, 0.2f };
            for (var f = 0; f < Frames; f++)
            {
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
                        // Each wing a curve from the body out to its tip, thick at the root.
                        var u = (x + 0.5f) / w * 2f - 1f;
                        var v = (y + 0.5f) / h * 2f - 1f;
                        var s = Mathf.Abs(u);
                        var curve = lift[f] * s * (1.4f - 0.4f * s) * 0.8f - 0.1f;
                        var thickness = Mathf.Lerp(0.2f, 0.05f, s);
                        var d = Mathf.Abs(v - curve) - thickness;
                        var body = new Vector2(u * 3f, (v + 0.08f) * 1.6f).magnitude - 0.28f;
                        var a = Mathf.Clamp01(0.5f - Mathf.Min(d, body) * h * 0.5f);
                        if (s > 0.98f)
                        {
                            a = 0f;
                        }
                        pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                result[f] = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 128f);
            }
            return result;
        }
    }
}
