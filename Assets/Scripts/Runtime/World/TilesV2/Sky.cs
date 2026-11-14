using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The Tiles V2 sky, pinned to the view (docs/art/tiles-v2.md): two colours that change
    /// with the hour, drawn as dithered bands on a texture the size of the camera's view in
    /// art pixels (painted here, so the bands stay pixel art whatever the colours), the
    /// stars that come out at night, the sun or the moon on its arc across the view, and
    /// the tint of the far backdrop layers (ridges, clouds). DayNight tells it the hour's
    /// values; tools/tiles_v2/tv2_backdrop.py paints the same bands for the preview.
    /// </summary>
    [ExecuteAlways]
    public sealed class Sky : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;

        // How far across and up the view the sun and the moon travel, as shares of the view.
        private const float ArcWidth = 0.84f;
        private const float ArcBase = -0.12f;
        private const float ArcHeight = 0.5f;

        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        [SerializeField]
        private SpriteRenderer gradient;

        [SerializeField]
        private SpriteRenderer stars;

        [SerializeField]
        private SpriteRenderer sun;

        [SerializeField]
        private SpriteRenderer moon;

        [SerializeField, Tooltip("The far backdrop layers the hour tints: ridges and clouds.")]
        private SpriteRenderer[] tinted = new SpriteRenderer[0];

        [SerializeField, Min(2)]
        private int bands = 7;

        [SerializeField, Min(8), Tooltip("The sky's size in art pixels: the camera's view.")]
        private int width = 640;

        [SerializeField, Min(8)]
        private int height = 360;

        private Texture2D texture;
        private Sprite sprite;
        private byte[] bandOf;
        private Color32[] pixels;
        private Color32[] palette;

        /// <summary>The colours of the sky's bands as last painted, top first; empty before the first.</summary>
        public Color32[] Palette => palette ?? System.Array.Empty<Color32>();

        /// <summary>The sky's size in art pixels.</summary>
        public Vector2Int Pixels => new Vector2Int(width, height);

        private void LateUpdate()
        {
            var view = Camera.main;
            if (view != null && view.orthographic)
            {
                var tall = view.orthographicSize * 2f;
                Follow(view.transform.position, new Vector2(tall * view.aspect, tall));
            }
        }

        private void OnDisable()
        {
            Release();
        }

        /// <summary>Sets the sky up (the world builder's one entry point); `pixels` is the camera's view.</summary>
        public void Configure(
            SpriteRenderer gradientPicture,
            SpriteRenderer starsPicture,
            SpriteRenderer sunPicture,
            SpriteRenderer moonPicture,
            SpriteRenderer[] tintedLayers,
            int bandCount,
            Vector2Int pixelSize
        )
        {
            gradient = gradientPicture;
            stars = starsPicture;
            sun = sunPicture;
            moon = moonPicture;
            tinted = tintedLayers ?? new SpriteRenderer[0];
            bands = Mathf.Max(bandCount, 2);
            width = Mathf.Max(pixelSize.x, 8);
            height = Mathf.Max(pixelSize.y, 8);
            Release();
        }

        /// <summary>
        /// Which of the `bands` colours pixel (x, y from the top) of a sky `height` pixels
        /// tall shows, 0 at the top: the bands meet in an ordered 4 x 4 dither.
        /// </summary>
        public static int Band(int x, int fromTop, int height, int bands)
        {
            var t = fromTop / (float)(height - 1) * (bands - 1);
            var whole = (int)t;
            var step = (t - whole) * 16f > Bayer[(fromTop % 4) * 4 + x % 4] + 0.5f ? 1 : 0;
            return Mathf.Min(whole + step, bands - 1);
        }

        /// <summary>Shows a moment of the day: `along` is how far the sun (or the moon) is on its arc, 0 to 1.</summary>
        public void Show(TilesV2Set.DayValues day, float along, bool sunUp)
        {
            Paint(day.top, day.low);
            if (stars != null)
            {
                stars.color = new Color(1f, 1f, 1f, Mathf.Clamp01(day.stars));
                stars.enabled = day.stars > 0.004f;
            }
            foreach (var layer in tinted)
            {
                if (layer != null)
                {
                    layer.color = new Color(day.tint.r, day.tint.g, day.tint.b, 1f);
                }
            }
            var size = new Vector2(width, height) / PixelsPerUnit;
            var at = new Vector3(
                (along - 0.5f) * ArcWidth * size.x,
                (ArcBase + ArcHeight * Mathf.Sin(Mathf.PI * along)) * size.y,
                0f
            );
            Put(sun, sunUp, at);
            Put(moon, !sunUp, at);
        }

        /// <summary>Centres the sky on a view and grows it to cover one larger than its own.</summary>
        public void Follow(Vector2 centre, Vector2 viewSize)
        {
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            var scale = Mathf.Max(1f, viewSize.x * PixelsPerUnit / width, viewSize.y * PixelsPerUnit / height);
            transform.localScale = new Vector3(scale, scale, 1f);
            // The sun and the moon keep their size in a view the sky is stretched over.
            var body = new Vector3(1f / scale, 1f / scale, 1f);
            if (sun != null)
            {
                sun.transform.localScale = body;
            }
            if (moon != null)
            {
                moon.transform.localScale = body;
            }
        }

        private static void Put(SpriteRenderer body, bool up, Vector3 at)
        {
            if (body == null)
            {
                return;
            }
            body.enabled = up;
            // The sprite's pivot is its bottom middle: stand it so its middle is on the arc.
            var half = body.sprite != null ? body.sprite.bounds.size.y / 2f : 0f;
            body.transform.localPosition = new Vector3(
                Mathf.Round(at.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round((at.y - half) * PixelsPerUnit) / PixelsPerUnit,
                0f
            );
        }

        // Paints the bands between two colours; nothing when they are the ones showing.
        private void Paint(Color top, Color low)
        {
            if (gradient == null)
            {
                return;
            }
            // Called every frame while the day runs: nothing is made here unless the colours moved.
            var same = palette != null && palette.Length == bands && texture != null;
            for (var k = 0; k < bands && same; k++)
            {
                Color32 colour = Color.Lerp(top, low, k / (float)(bands - 1));
                same = palette[k].r == colour.r && palette[k].g == colour.g && palette[k].b == colour.b;
            }
            if (same)
            {
                return;
            }
            var colours = palette != null && palette.Length == bands ? palette : new Color32[bands];
            for (var k = 0; k < bands; k++)
            {
                Color32 colour = Color.Lerp(top, low, k / (float)(bands - 1));
                colour.a = 255;
                colours[k] = colour;
            }
            palette = colours;
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Sky",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, width, height),
                    new Vector2(0.5f, 0.5f),
                    PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect
                );
                sprite.name = "Sky";
                sprite.hideFlags = HideFlags.HideAndDontSave;
                pixels = new Color32[width * height];
                bandOf = new byte[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        // Texture rows run bottom first.
                        bandOf[y * width + x] = (byte)Band(x, height - 1 - y, height, bands);
                    }
                }
            }
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = colours[bandOf[i]];
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            gradient.sprite = sprite;
        }

        private void Release()
        {
            if (sprite != null)
            {
                DestroyImmediate(sprite);
            }
            if (texture != null)
            {
                DestroyImmediate(texture);
            }
            sprite = null;
            texture = null;
            palette = null;
            bandOf = null;
            pixels = null;
        }
    }
}
