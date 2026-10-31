using UnityEngine;

namespace Descender.Fx
{
    /// <summary>
    /// Plain sprites made in code for the things Godot drew by hand: a white square
    /// and a white disc at 16 pixels a unit, tinted by the renderer. Made once and
    /// shared; nothing here is an asset.
    /// </summary>
    public static class Shapes
    {
        private static Sprite square;
        private static Sprite disc;
        private static Material unlit;

        /// <summary>A 4 by 4 pixel white square, pivot in the middle.</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    var pixels = new Color32[16];
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = new Color32(255, 255, 255, 255);
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    square = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
                }
                return square;
            }
        }

        /// <summary>A 16 pixel white disc, pivot in the middle.</summary>
        public static Sprite Disc
        {
            get
            {
                if (disc == null)
                {
                    const int size = 16;
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                    };
                    var pixels = new Color32[size * size];
                    var radius = size / 2f - 0.5f;
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            var dx = x + 0.5f - size / 2f;
                            var dy = y + 0.5f - size / 2f;
                            var inside = dx * dx + dy * dy <= radius * radius;
                            pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                        }
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    disc = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
                }
                return disc;
            }
        }

        /// <summary>An unlit sprite material for particles, lines and glows.</summary>
        public static Material Unlit
        {
            get
            {
                if (unlit == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    unlit = new Material(shader) { name = "Descender Unlit" };
                }
                return unlit;
            }
        }

        /// <summary>A child object with a renderer showing a tinted square of this size (pixels) at an offset (pixels).</summary>
        public static SpriteRenderer Block(Transform parent, string name, Vector2 sizePixels, Vector2 offsetPixels, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = offsetPixels / 16f;
            child.transform.localScale = new Vector3(sizePixels.x / 4f, sizePixels.y / 4f, 1f);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = color;
            renderer.sharedMaterial = Unlit;
            return renderer;
        }
    }
}
