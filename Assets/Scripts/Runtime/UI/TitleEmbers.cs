using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Motes of gold light that rise slowly over the title screen's dark ground,
    /// one or two pixels each, swaying and flickering in sixteenths of alpha, on
    /// whole pixels. A fixed pool made once; nothing is allocated per frame.
    /// </summary>
    public sealed class TitleEmbers : MonoBehaviour
    {
        public const int Count = 28;

        private static readonly Color[] Colors =
        {
            new Color32(255, 214, 140, 255),
            new Color32(240, 170, 80, 255),
            new Color32(255, 240, 200, 255),
            new Color32(200, 110, 50, 255),
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
        public static TitleEmbers Make(Transform stage, int seed)
        {
            var rect = UiKit.Rect(stage, "Embers");
            UiKit.Fill(rect);
            var embers = rect.gameObject.AddComponent<TitleEmbers>();
            embers.random = new System.Random(seed);
            for (var i = 0; i < Count; i++)
            {
                var image = UiKit.Box(rect, "Mote", Colors[i % Colors.Length]);
                var size = i % 5 == 0 ? 2f : 1f;
                image.rectTransform.sizeDelta = new Vector2(size, size);
                embers.images[i] = image;
                embers.motes[i] = image.rectTransform;
                embers.Respawn(i, true);
            }
            return embers;
        }

        /// <summary>A mote's alpha at a height: none at the bottom edge, full in the middle, gone near the top.</summary>
        public static float FadeAt(float fromTop)
        {
            var rise = Mathf.Clamp01((UiTheme.Height - fromTop) / 30f);
            var fall = Mathf.Clamp01((fromTop - 10f) / 60f);
            return Mathf.Min(rise, fall);
        }

        private void Respawn(int i, bool anywhere)
        {
            x[i] = (float)random.NextDouble() * UiTheme.Width;
            y[i] = anywhere
                ? 40f + (float)random.NextDouble() * 140f
                : UiTheme.Height + (float)random.NextDouble() * 20f;
            speed[i] = 3f + (float)random.NextDouble() * 6f;
            phase[i] = (float)random.NextDouble() * 6.28f;
            strength[i] = 0.35f + (float)random.NextDouble() * 0.5f;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var t = Time.unscaledTime;
            for (var i = 0; i < Count; i++)
            {
                y[i] -= speed[i] * dt;
                if (y[i] < 10f)
                {
                    Respawn(i, false);
                }
                var sway = Mathf.Sin(t * 0.7f + phase[i]) * 3f;
                motes[i].anchoredPosition = new Vector2(Mathf.Round(x[i] + sway), -Mathf.Round(y[i]));
                var flicker = 0.7f + 0.3f * Mathf.Sin(t * 2.3f + phase[i] * 3f);
                var alpha = Mathf.Round(FadeAt(y[i]) * strength[i] * flicker * 16f) / 16f;
                var color = images[i].color;
                color.a = alpha;
                images[i].color = color;
            }
        }
    }
}
