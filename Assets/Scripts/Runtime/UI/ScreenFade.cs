using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// A black cover over the whole screen for fading the world in and out. Builds
    /// its own overlay canvas above every other screen and never takes input. Runs
    /// on unscaled time so it works while the game is paused. Port of
    /// scripts/screen_fade.gd; lives in Main.unity.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        public const float FadeTime = 0.5f;

        /// <summary>Above the menus (which sit below 100) and the HUD.</summary>
        public const int SortingOrder = 1000;

        private Image cover;
        private Coroutine fade;

        public bool IsCovered => cover != null && cover.color.a >= 0.999f;

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var cover = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(transform, false);
            var rect = cover.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            this.cover = cover.GetComponent<Image>();
            this.cover.color = new Color(0f, 0f, 0f, 0f);
            this.cover.raycastTarget = false;
        }

        /// <summary>Fades to black; yield the result to wait for the screen to be covered.</summary>
        public Coroutine FadeOut(float duration = FadeTime) => FadeTo(1f, duration);

        /// <summary>Fades back in from black.</summary>
        public Coroutine FadeIn(float duration = FadeTime) => FadeTo(0f, duration);

        /// <summary>Covers the screen at once, for a world that then fades in.</summary>
        public void CoverNow()
        {
            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }
            SetAlpha(1f);
        }

        private Coroutine FadeTo(float alpha, float duration)
        {
            if (fade != null)
            {
                StopCoroutine(fade);
            }
            fade = StartCoroutine(Fade(alpha, duration));
            return fade;
        }

        private IEnumerator Fade(float target, float duration)
        {
            var start = cover.color.a;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetAlpha(target);
            fade = null;
        }

        private void SetAlpha(float alpha)
        {
            cover.color = new Color(0f, 0f, 0f, alpha);
            cover.enabled = alpha > 0f;
        }
    }
}
