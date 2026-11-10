using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The gold bar behind the focused title screen item. It glides to the item the
    /// focus moves to, eased, breathes softly, and carries the diamond emblem at its
    /// left on the main column. It moves into the column that is showing
    /// (<see cref="Follow"/>) and hides when the focus is elsewhere. Times are
    /// unscaled, so it moves while the game is paused.
    /// </summary>
    public sealed class TitleFocus : MonoBehaviour
    {
        /// <summary>How fast the bar closes on its item: the share of the gap left after a second is e^-Speed.</summary>
        public const float Speed = 16f;

        /// <summary>The longest frame the glide takes in one go, so a hitch slows it rather than skipping it.</summary>
        public const float MaxStep = 1f / 20f;

        /// <summary>How far left of the bar's middle the emblem sits.</summary>
        public const float EmblemOffset = 300f;

        private Image bar;
        private Image emblem;
        private TitleList list;
        private float y;
        private bool placed;

        /// <summary>The bar's middle line now, in pixels from the stage's top.</summary>
        public float Middle => y;

        /// <summary>Whether the bar is showing.</summary>
        public bool Showing => bar != null && bar.enabled;

        /// <summary>Makes the bar under a stage; `emblemSprite` may be null.</summary>
        public static TitleFocus Make(Transform stage, Sprite emblemSprite)
        {
            var image = TitleKit.Sprite(stage, "Focus", null, TitleKit.Width * 0.5f, 0f);
            var focus = image.gameObject.AddComponent<TitleFocus>();
            focus.bar = image;
            focus.emblem = TitleKit.Sprite(image.transform, "Emblem", emblemSprite, 0f, 0f);
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
            bar.color = column.Bar != null ? Color.white : Color.clear;
            var size = column.Bar != null ? column.Bar.rect.size : Vector2.zero;
            TitleKit.Center(bar.rectTransform, TitleKit.Width * 0.5f, y, size.x, size.y);
            // The emblem's parent is the bar, whose own top left is the origin here.
            var emblemSize = emblem.rectTransform.sizeDelta;
            TitleKit.Center(
                emblem.rectTransform,
                size.x * 0.5f - EmblemOffset,
                size.y * 0.5f,
                emblemSize.x,
                emblemSize.y
            );
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
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
            var t = Time.unscaledTime;
            bar.color = new Color(1f, 1f, 1f, 0.88f + 0.12f * Mathf.Sin(t * 2f));
            emblem.color = new Color(1f, 1f, 1f, 0.85f + 0.15f * Mathf.Sin(t * 3f + 1f));
        }
    }
}
