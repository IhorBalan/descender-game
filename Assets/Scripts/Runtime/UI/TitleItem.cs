using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// One title screen item: its text centred on a whole pixel, ivory while it has
    /// the focus, parchment otherwise and faded when it cannot be chosen. The mouse
    /// moves the focus onto it, so hovering lights it as the keys and the pad do.
    /// Added by <see cref="TitleKit.Item"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class TitleItem : MonoBehaviour, IPointerEnterHandler
    {
        private Button button;
        private PixelText label;
        private bool caps;

        public string Text => label != null ? label.Text : "";

        public void Bind(PixelText text, bool spacedCaps)
        {
            button = GetComponent<Button>();
            label = text;
            caps = spacedCaps;
        }

        public void SetText(string text)
        {
            if (label == null)
            {
                return;
            }
            var shown = caps ? text.ToUpperInvariant() : text;
            var width = ((RectTransform)transform).sizeDelta.x;
            TitleKit.Center(label, shown, width, TitleKit.ItemTextTop);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.interactable && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (label == null)
            {
                return;
            }
            if (!button.interactable)
            {
                label.color = TitleKit.Faded;
                return;
            }
            var system = EventSystem.current;
            var selected = system != null && system.currentSelectedGameObject == gameObject;
            label.color = selected ? TitleKit.Ivory : TitleKit.Parchment;
        }
    }
}
