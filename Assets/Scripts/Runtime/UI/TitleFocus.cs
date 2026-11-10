using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The gold bar behind the focused title screen item. It glides to the item the
    /// focus moves to, eased and on whole pixels, breathes softly, and carries the
    /// diamond emblem at its left on the main column. It moves into the column that
    /// is showing (<see cref="Follow"/>) and hides when the focus is elsewhere.
    /// Times are unscaled, so it moves while the game is paused.
    /// </summary>
    public sealed class TitleFocus : MonoBehaviour
    {
        /// <summary>How fast the bar closes on its item: the share of the gap left after a second is e^-Speed.</summary>
        public const float Speed = 16f;

        /// <summary>The longest frame the glide takes in one go, so a hitch slows it rather than skipping it.</summary>
        public const float MaxStep = 1f / 20f;

        private Image bar;
        private Image emblem;
        private TitleList list;
        private float y;
        private bool placed;

        /// <summary>The bar's middle line now, in pixels from the stage's top; whole once it has arrived.</summary>
        public float Middle => y;

        /// <summary>Whether the bar is showing.</summary>
        public bool Showing => bar != null && bar.enabled;

        /// <summary>Makes the bar under a stage; `emblemSprite` may be null.</summary>
        public static TitleFocus Make(Transform stage, Sprite emblemSprite)
        {
            var image = TitleKit.Sprite(stage, "Focus", null, UiTheme.Width, 0f);
            var focus = image.gameObject.AddComponent<TitleFocus>();
            focus.bar = image;
            focus.emblem = TitleKit.Sprite(image.transform, "Emblem", emblemSprite, 0f, 0f);
            focus.emblem.enabled = emblemSprite != null;
            image.enabled = false;
            return focus;
        }

        /// <summary>One frame of the glide: closer to the target, landing on it once less than half a pixel away.</summary>
        public static float Ease(float current, float target, float deltaTime)
        {
            var next = current + (target - current) * (1f - Mathf.Exp(-Speed * deltaTime));
            return Mathf.Abs(target - next) < 0.5f ? target : next;
        }

        /// <summary>Puts the bar in a column, sized from its sprite, jumping to that column's focus.</summary>
        public void Follow(TitleList column, bool withEmblem)
        {
            list = column;
            transform.SetParent(column.Root, false);
            transform.SetSiblingIndex(column.BarSiblingIndex);
            bar.sprite = column.Bar;
            var size = column.Bar != null ? column.Bar.rect.size : Vector2.zero;
            var rect = bar.rectTransform;
            UiKit.Place(rect, Mathf.Floor((UiTheme.Width - size.x) * 0.5f), 0f, size.x, size.y);
            var emblemRect = emblem.rectTransform;
            var emblemSize = emblemRect.sizeDelta;
            // The emblem's diamond (15 px into its sprite) sits 16 px into the bar, where its glow begins.
            UiKit.Place(emblemRect, 1f, Mathf.Floor((size.y - emblemSize.y) * 0.5f), emblemSize.x, emblemSize.y);
            emblem.gameObject.SetActive(withEmblem && emblem.sprite != null);
            placed = false;
        }

        private void LateUpdate()
        {
            var system = EventSystem.current;
            var selected = system != null ? system.currentSelectedGameObject : null;
            var showing = list != null && bar.sprite != null && list.Contains(selected);
            bar.enabled = showing;
            emblem.enabled = showing;
            if (!showing)
            {
                placed = false;
                return;
            }
            var target = list.MiddleOf(selected);
            y = placed ? Ease(y, target, Mathf.Min(Time.unscaledDeltaTime, MaxStep)) : target;
            placed = true;
            var rect = bar.rectTransform;
            var top = Mathf.Round(y) - Mathf.Floor(rect.sizeDelta.y * 0.5f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
            var t = Time.unscaledTime;
            bar.color = new Color(1f, 1f, 1f, Quantize(0.88f + 0.12f * Mathf.Sin(t * 2f)));
            emblem.color = new Color(1f, 1f, 1f, Quantize(0.8f + 0.2f * Mathf.Sin(t * 3f + 1f)));
        }

        /// <summary>Alpha in sixteenths, so the glow steps as pixel art does instead of sliding.</summary>
        private static float Quantize(float alpha) => Mathf.Round(alpha * 16f) / 16f;
    }
}
