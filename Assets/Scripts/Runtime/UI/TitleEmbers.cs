using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Embers of gold light that drift up over the title screen's dark ground,
    /// each a soft glow of its own size, swaying and flickering, fading in at the
    /// bottom and out as they rise. A fixed pool made once; nothing is allocated
    /// per frame.
    /// </summary>
    public sealed class TitleEmbers : MonoBehaviour
    {
        public const int Count = 40;

        private static readonly Color[] Colors =
        {
            new Color32(255, 206, 128, 255),
            new Color32(244, 164, 72, 255),
            new Color32(255, 232, 190, 255),
            new Color32(226, 120, 48, 255),
        };

        private readonly RectTransform[] motes = new RectTransform[Count];
        private readonly Image[] images = new Image[Count];
        private readonly float[] x = new float[Count];
        private readonly float[] y = new float[Count];
        private readonly float[] speed = new float[Count];
        private readonly float[] phase = new float[Count];
        private readonly float[] strength = new float[Count];
        private System.Random random;

        /// <summary>Makes the pool under a stage, behind whatever is added after it.</summary>
        public static TitleEmbers Make(Transform stage, Sprite mote, int seed)
        {
            var rect = UiKit.Rect(stage, "Embers");
            UiKit.Fill(rect);
            var embers = rect.gameObject.AddComponent<TitleEmbers>();
            embers.random = new System.Random(seed);
            for (var i = 0; i < Count; i++)
            {
                var image = UiKit.Box(rect, "Mote", Colors[i % Colors.Length]);
                image.sprite = mote;
                embers.images[i] = image;
                embers.motes[i] = image.rectTransform;
                embers.Respawn(i, true);
                var size = 8f + (float)embers.random.NextDouble() * 18f;
                TitleKit.Center(image.rectTransform, embers.x[i], embers.y[i], size, size);
            }
            return embers;
        }

        /// <summary>A mote's alpha at a height: none at the bottom edge, full low down, gone towards the top.</summary>
        public static float FadeAt(float fromTop)
        {
            var rise = Mathf.Clamp01((TitleKit.Height - fromTop) / 120f);
            var fall = Mathf.Clamp01((fromTop - 60f) / 420f);
            return Mathf.Min(rise, fall);
        }

        private void Respawn(int i, bool anywhere)
        {
            x[i] = (float)random.NextDouble() * TitleKit.Width;
            y[i] = anywhere
                ? 200f + (float)random.NextDouble() * (TitleKit.Height - 200f)
                : TitleKit.Height + (float)random.NextDouble() * 60f;
            speed[i] = 14f + (float)random.NextDouble() * 34f;
            phase[i] = (float)random.NextDouble() * 6.28f;
            strength[i] = 0.3f + (float)random.NextDouble() * 0.6f;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var t = Time.unscaledTime;
            for (var i = 0; i < Count; i++)
            {
                y[i] -= speed[i] * dt;
                if (y[i] < 60f)
                {
                    Respawn(i, false);
                }
                var sway = Mathf.Sin(t * 0.6f + phase[i]) * 14f;
                motes[i].anchoredPosition = new Vector2(x[i] + sway, -y[i]);
                var flicker = 0.75f + 0.25f * Mathf.Sin(t * 2.1f + phase[i] * 3f);
                var color = images[i].color;
                color.a = FadeAt(y[i]) * strength[i] * flicker;
                images[i].color = color;
            }
        }
    }
}
