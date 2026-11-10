using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// One title screen item: its text ivory with a soft warm glow while it has the
    /// focus, parchment otherwise and faded when it cannot be chosen. The mouse
    /// moves the focus onto it, so hovering lights it as the keys and the pad do.
    /// Added by <see cref="TitleKit.Item"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class TitleItem : MonoBehaviour, IPointerEnterHandler
    {
        private Button button;
        private TextMeshProUGUI label;
        private bool caps;
        private bool lit;

        public string Text => label != null ? label.text : "";

        public void Bind(TextMeshProUGUI text, bool spacedCaps)
        {
            button = GetComponent<Button>();
            label = text;
            caps = spacedCaps;
        }

        public void SetText(string text)
        {
            if (label != null)
            {
                label.text = caps ? text.ToUpperInvariant() : text;
            }
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
            var system = EventSystem.current;
            var focused = button.interactable && system != null && system.currentSelectedGameObject == gameObject;
            label.color =
                !button.interactable ? TitleKit.Faded
                : focused ? TitleKit.Ivory
                : TitleKit.Parchment;
            if (focused != lit)
            {
                lit = focused;
                var glow = TitleKit.GlowMaterial;
                if (glow != null && TitleKit.Font != null)
                {
                    label.fontSharedMaterial = focused ? glow : TitleKit.Font.material;
                }
            }
        }
    }
}
