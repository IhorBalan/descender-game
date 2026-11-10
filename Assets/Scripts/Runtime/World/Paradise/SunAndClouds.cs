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
    /// and the hazed distance are untouched. The sun also has a direction: its light sits
    /// thousands of units away, up and to the right, with its cloud pattern drawn over the
    /// view (the sprite's pivot is that far from its picture), so the kit's normal maps
    /// turn toward it. A flat surface still gets exactly the sun's share. The sky lights
    /// (warm bounce from the cloud sea, a low rim of sun) stay equally far from the camera.
    /// Sits on the region's root; ParadiseNormals builds the lights, and the pattern is made
    /// once at start, procedurally, so there is no texture to ship.
    /// </summary>
    public sealed class SunAndClouds : MonoBehaviour
    {
        private const int CookieWidth = 512;
        private const int CookieHeight = 256;

        /// <summary>How far away the sun's light sits: far enough that its direction is one across the view.</summary>
        public const float SunDistance = 6000f;

        /// <summary>Toward the sun from anywhere in the garden: x and y on the screen, z toward the viewer.</summary>
        public static readonly Vector3 SunDirection = new Vector3(0.52f, 0.6f, 0.61f).normalized;

        [Tooltip("The sun's share of the light on lit sprites; the global light keeps the rest.")]
        [SerializeField, Range(0f, 0.8f)]
        private float sunShare = 0.38f;

        [Tooltip("The share of the light on a flat surface that comes from the sky lights.")]
        [SerializeField, Range(0f, 0.4f)]
        private float skyLightShare;

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

        [Tooltip("The sun's own light (type Sprite), with its normal-map settings; built by ParadiseNormals.")]
        [SerializeField]
        private Light2D sun;

        [Tooltip("Parent of the far sky lights, kept on the camera.")]
        [SerializeField]
        private Transform skyLights;

        [Tooltip(
            "The sun through a canopy of leaves (type Sprite, built like the sun): flecks of light instead of cloud shadow, as strong as the sun where they fall. ZoneLook.Canopy fades it in as the sun fades out."
        )]
        [SerializeField]
        private Light2D flecks;

        [Tooltip("One period of the flecks' pattern, in units.")]
        [SerializeField]
        private Vector2 fleckPeriod = new Vector2(24f, 12f);

        [Tooltip("How much of the ground the flecks light (0 to 1).")]
        [SerializeField, Range(0f, 1f)]
        private float fleckCover = 0.3f;

        [Tooltip("How far the flecks waver as the leaves stir, in units, and how fast (cycles a second).")]
        [SerializeField]
        private Vector2 fleckSway = new Vector2(0.14f, 0.35f);

        private static Sprite cookie;
        private static Sprite fleckCookie;
        private Vector2 offset;
        private Vector2 fleckOffset;
        private float sunIntensity;

        /// <summary>The share of the light the sky's fill keeps: 1 without a sun.</summary>
        public static float SkyShare { get; private set; } = 1f;

        private void OnEnable()
        {
            SkyShare = 1f - sunShare - skyLightShare;
        }

        private void OnDisable()
        {
            SkyShare = 1f;
        }

        private void Start()
        {
            if (sun == null)
            {
                return;
            }
            sun.lightCookieSprite = Cookie(cover, period);
            sun.color = sunColor;
            // A flat surface faces the viewer and catches SunDirection.z of the light.
            sunIntensity = sunShare / SunDirection.z;
            sun.intensity = sunIntensity;
            // The sprite holds two periods each way, so it covers the view wherever it wraps.
            var size = sun.lightCookieSprite.bounds.size;
            sun.transform.localScale = new Vector3(period.x * 2f / size.x, period.y * 2f / size.y, 1f);
            if (flecks != null)
            {
                flecks.lightCookieSprite = FleckCookie(fleckCover, fleckPeriod);
                flecks.color = sunColor;
                flecks.intensity = 0f;
                flecks.enabled = false;
                var fleckSize = flecks.lightCookieSprite.bounds.size;
                flecks.transform.localScale = new Vector3(
                    fleckPeriod.x * 2f / fleckSize.x,
                    fleckPeriod.y * 2f / fleckSize.y,
                    1f
                );
            }
        }

        /// <summary>
        /// How the sun's light splits between open sky and the canopy's flecks under a canopy
        /// that closes `canopy` of the sky: the cloud-shadowed sun's intensity and the flecks'.
        /// A fleck is the full sun where it falls, so the two are each a share of the same sun.
        /// </summary>
        public static Vector2 SunThroughCanopy(float sunIntensity, float canopy)
        {
            canopy = Mathf.Clamp01(canopy);
            return new Vector2(sunIntensity * (1f - canopy), sunIntensity * canopy);
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
            // periods to stay over the camera, which the tiling hides. The jump changes the
            // sun's direction by well under a degree.
            var c = (Vector2)camera.transform.position;
            var far = (Vector2)SunDirection * SunDistance;
            sun.transform.position = new Vector3(
                Centre(c.x, offset.x, period.x) + far.x,
                Centre(c.y, offset.y, period.y) + far.y,
                0f
            );
            if (skyLights != null)
            {
                skyLights.position = new Vector3(c.x, c.y, 0f);
            }
            // Under a canopy the sun comes through only in flecks; each light is off when it
            // gives nothing, so open sky costs no more than before.
            var split = SunThroughCanopy(sunIntensity, flecks != null ? ZoneLook.Canopy : 0f);
            sun.intensity = split.x;
            sun.enabled = split.x > 0.001f;
            if (flecks == null)
            {
                return;
            }
            flecks.intensity = split.y;
            flecks.enabled = split.y > 0.001f;
            if (!flecks.enabled)
            {
                return;
            }
            // The flecks drift with the clouds' breeze, slower, and waver as the leaves stir.
            fleckOffset += drift * 0.25f * Time.deltaTime;
            fleckOffset = new Vector2(
                Mathf.Repeat(fleckOffset.x, fleckPeriod.x),
                Mathf.Repeat(fleckOffset.y, fleckPeriod.y)
            );
            var t = Time.time * fleckSway.y * Mathf.PI * 2f;
            var stir =
                new Vector2(Mathf.Sin(t) + 0.4f * Mathf.Sin(t * 2.3f + 1f), 0.5f * Mathf.Sin(t * 0.7f + 2f))
                * fleckSway.x;
            flecks.transform.position = new Vector3(
                Centre(c.x, fleckOffset.x, fleckPeriod.x) + far.x + stir.x,
                Centre(c.y, fleckOffset.y, fleckPeriod.y) + far.y + stir.y,
                0f
            );
        }

        /// <summary>
        /// Where the pattern's picture is centred along one axis with the view at `view`: a
        /// whole number of periods from its drift, and never more than half a period from
        /// the view, so the two periods it holds always reach past both sides of the screen.
        /// </summary>
        public static float Centre(float view, float drifted, float period) =>
            Mathf.Floor((view - drifted) / period) * period + drifted + period * 0.5f;

        /// <summary>The cloud pattern: soft-edged shadows (dark) in sun (white), two periods each way.</summary>
        private static Sprite Cookie(float cover, Vector2 period)
        {
            if (cookie == null)
            {
                var threshold = Mathf.Lerp(0.75f, 0.25f, cover);
                cookie = Pattern(
                    "Cloud shadows",
                    period,
                    (u, v) =>
                        1f
                        - Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(threshold - 0.12f, threshold + 0.12f, Fbm(u, v, 2))
                        )
                );
            }
            return cookie;
        }

        /// <summary>
        /// The sun through leaves: soft flecks of light (white) in the canopy's shade (black), two
        /// periods each way. Sunflecks are little round images of the sun, soft at the edge: a few
        /// broad ones and many small, gathered where the leaves are thinnest (a broad mask).
        /// </summary>
        private static Sprite FleckCookie(float cover, Vector2 period)
        {
            if (fleckCookie == null)
            {
                fleckCookie = Pattern("Leaf flecks", period, (u, v) => Fleck(u, v, cover));
            }
            return fleckCookie;
        }

        /// <summary>How lit the flecks' pattern is at (u, v), in periods: 0 shade to 1 full sun.</summary>
        public static float Fleck(float u, float v, float cover)
        {
            // `cover` opens the canopy's thin places wider (0.3 lights about a tenth of the ground).
            var open = Mathf.Lerp(0.55f, 0.3f, cover);
            var gaps = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(open, open + 0.26f, Fbm(u, v, 2)));
            var broad = Discs(u, v, 8, 4, 0.2f, 0.42f, 0.45f, 10);
            var small = Discs(u, v, 22, 11, 0.14f, 0.34f, 0.3f, 20);
            return Mathf.Max(broad * gaps, small * Mathf.Clamp01(gaps * 1.3f));
        }

        /// <summary>
        /// Soft discs scattered one to a cell on a grid of nx by ny cells a period (some cells
        /// empty), each its own size and brightness, a little wider than tall; tiles on the period.
        /// </summary>
        private static float Discs(float u, float v, int nx, int ny, float rMin, float rMax, float keepAbove, int salt)
        {
            var gx = u * nx;
            var gy = v * ny;
            var cx = Mathf.FloorToInt(gx);
            var cy = Mathf.FloorToInt(gy);
            var best = 0f;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var ix = cx + dx;
                    var iy = cy + dy;
                    if (Cell(ix, iy, nx, ny, salt + 4) <= keepAbove)
                    {
                        continue;
                    }
                    var px = ix + 0.2f + 0.6f * Cell(ix, iy, nx, ny, salt + 1);
                    var py = iy + 0.2f + 0.6f * Cell(ix, iy, nx, ny, salt + 2);
                    var r = Mathf.Lerp(rMin, rMax, Cell(ix, iy, nx, ny, salt + 3));
                    var ex = (gx - px) / 1.3f;
                    var ey = gy - py;
                    var d = Mathf.Sqrt(ex * ex + ey * ey);
                    var spot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r * 0.3f, d));
                    best = Mathf.Max(best, spot * (0.6f + 0.4f * Cell(ix, iy, nx, ny, salt + 5)));
                }
            }
            return best;
        }

        /// <summary>A number from 0 to 1 for a cell, the same for the cell a period on either way.</summary>
        private static float Cell(int x, int y, int nx, int ny, int salt)
        {
            x = ((x % nx) + nx) % nx;
            y = ((y % ny) + ny) % ny;
            var h = (uint)(x * 374761393 + y * 668265263 + salt * 1013904223 + nx * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
        }

        /// <summary>A tiling light pattern, two periods each way, as a sprite pinned back along the sun.</summary>
        private static Sprite Pattern(string name, Vector2 period, System.Func<float, float, float> lit)
        {
            var texture = new Texture2D(CookieWidth, CookieHeight, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
            };
            var pixels = new Color32[CookieWidth * CookieHeight];
            for (var y = 0; y < CookieHeight; y++)
            {
                for (var x = 0; x < CookieWidth; x++)
                {
                    // One period is half the texture each way; the noise repeats on it exactly.
                    var u = x / (float)(CookieWidth / 2);
                    var v = y / (float)(CookieHeight / 2);
                    var b = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(lit(u, v)));
                    pixels[y * CookieWidth + x] = new Color32(b, b, b, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, CookieWidth, CookieHeight),
                // The picture lies back along the sun's direction from the light's own
                // position: the pivot, in picture sizes (two periods), is that far outside it.
                new Vector2(0.5f, 0.5f)
                    + Vector2.Scale((Vector2)SunDirection * SunDistance, new Vector2(0.5f / period.x, 0.5f / period.y)),
                CookieWidth / 4f
            );
            sprite.name = name;
            return sprite;
        }

        /// <summary>
        /// Fractal noise that tiles on whole numbers of u and v, from `first` cells a period up:
        /// at 2, soft cumulus-shaped patches; at 16, fine grain.
        /// </summary>
        private static float Fbm(float u, float v, int first)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var frequency = first;
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
