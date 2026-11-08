using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Sunlight through drifting clouds: the light that falls on Paradise's lit sprites is
    /// split as daylight is, into the sky's fill (the global light, which ZoneLook scales by
    /// <see cref="SkyShare"/>) and the sun's direct share, a large sprite light seen through
    /// a soft, seamlessly tiling cloud pattern. The pattern drifts with the breeze, so cloud
    /// shadows slide across the ground, the architecture and the hero, while the unlit sky
    /// and the hazed distance are untouched. Sits on the region's root; the pattern is made
    /// once at start, procedurally, so there is no texture to ship.
    /// </summary>
    public sealed class SunAndClouds : MonoBehaviour
    {
        private const int CookieWidth = 512;
        private const int CookieHeight = 256;

        [Tooltip("The sun's share of the light on lit sprites; the global light keeps the rest.")]
        [SerializeField, Range(0f, 0.8f)]
        private float sunShare = 0.38f;

        [SerializeField]
        private Color sunColor = new Color(1f, 0.95f, 0.84f);

        [Tooltip("One period of the cloud pattern, in units.")]
        [SerializeField]
        private Vector2 period = new Vector2(56f, 28f);

        [Tooltip("How fast the cloud shadows drift, in units a second.")]
        [SerializeField]
        private Vector2 drift = new Vector2(0.55f, 0.04f);

        [Tooltip("How much of the ground lies in cloud shadow (0 to 1).")]
        [SerializeField, Range(0f, 1f)]
        private float cover = 0.4f;

        private static Sprite cookie;
        private Light2D sun;
        private Vector2 offset;

        /// <summary>The share of the light the sky's fill keeps: 1 without a sun.</summary>
        public static float SkyShare { get; private set; } = 1f;

        private void OnEnable()
        {
            SkyShare = 1f - sunShare;
        }

        private void OnDisable()
        {
            SkyShare = 1f;
        }

        private void Start()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light2D>();
            sun.lightType = Light2D.LightType.Sprite;
            sun.lightCookieSprite = Cookie(cover);
            sun.color = sunColor;
            sun.intensity = sunShare;
            sun.blendStyleIndex = 0;
            // The sprite holds two periods each way, so it covers the view wherever it wraps.
            var size = sun.lightCookieSprite.bounds.size;
            go.transform.localScale = new Vector3(period.x * 2f / size.x, period.y * 2f / size.y, 1f);
        }

        private void LateUpdate()
        {
            var camera = CameraRig.GameCamera.Main;
            if (sun == null || camera == null)
            {
                return;
            }
            offset += drift * Time.deltaTime;
            offset = new Vector2(Mathf.Repeat(offset.x, period.x), Mathf.Repeat(offset.y, period.y));
            // The pattern is fixed to the world (plus its drift); the light jumps by whole
            // periods to stay over the camera, which the tiling hides.
            var c = (Vector2)camera.transform.position;
            var x = Mathf.Floor((c.x - offset.x) / period.x) * period.x + offset.x + period.x;
            var y = Mathf.Floor((c.y - offset.y) / period.y) * period.y + offset.y + period.y;
            sun.transform.position = new Vector3(x, y, 0f);
        }

        /// <summary>The cloud pattern: soft-edged shadows (dark) in sun (white), two periods each way.</summary>
        private static Sprite Cookie(float cover)
        {
            if (cookie != null)
            {
                return cookie;
            }
            var texture = new Texture2D(CookieWidth, CookieHeight, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Cloud shadows",
            };
            var pixels = new Color32[CookieWidth * CookieHeight];
            var threshold = Mathf.Lerp(0.75f, 0.25f, cover);
            for (var y = 0; y < CookieHeight; y++)
            {
                for (var x = 0; x < CookieWidth; x++)
                {
                    // One period is half the texture each way; the noise repeats on it exactly.
                    var u = x / (float)(CookieWidth / 2);
                    var v = y / (float)(CookieHeight / 2);
                    var n = Fbm(u, v);
                    var shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.12f, threshold + 0.12f, n));
                    var b = (byte)Mathf.RoundToInt(255f * (1f - shade));
                    pixels[y * CookieWidth + x] = new Color32(b, b, b, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cookie = Sprite.Create(
                texture,
                new Rect(0f, 0f, CookieWidth, CookieHeight),
                new Vector2(0.5f, 0.5f),
                CookieWidth / 4f
            );
            cookie.name = "Cloud shadows";
            return cookie;
        }

        /// <summary>Fractal noise that tiles on whole numbers of u and v: soft cumulus-shaped patches.</summary>
        private static float Fbm(float u, float v)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var frequency = 2;
            for (var octave = 0; octave < 4; octave++)
            {
                sum += amplitude * TileNoise(u * frequency, v * frequency, frequency);
                amplitude *= 0.5f;
                frequency *= 2;
            }
            return sum / 0.9375f;
        }

        /// <summary>Value noise on a grid of `cells` cells per period, wrapping at the period.</summary>
        private static float TileNoise(float x, float y, int cells)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Lattice(x0, y0, cells);
            var b = Lattice(x0 + 1, y0, cells);
            var c = Lattice(x0, y0 + 1, cells);
            var d = Lattice(x0 + 1, y0 + 1, cells);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Lattice(int x, int y, int cells)
        {
            x = ((x % cells) + cells) % cells;
            y = ((y % cells) + cells) % cells;
            var h = (uint)(x * 374761393 + y * 668265263 + cells * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
        }
    }
}
