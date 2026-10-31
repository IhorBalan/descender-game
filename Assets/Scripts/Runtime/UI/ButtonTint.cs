using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Colours a stone button's text as the Godot theme did: muted when the button
    /// is disabled, warm when it is focused, plain otherwise. Added by
    /// <see cref="MenuKit.Button"/>; watches the button's state every frame since
    /// uGUI gives no event for interactable changes.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonTint : MonoBehaviour
    {
        private static readonly Color Focused = new Color(1f, 0.89f, 0.72f);

        private Button button;
        private Text label;
        private MenuTheme theme;

        public void Bind(Text text, MenuTheme menuTheme)
        {
            button = GetComponent<Button>();
            label = text;
            theme = menuTheme;
        }

        private void LateUpdate()
        {
            if (label == null || theme == null)
            {
                return;
            }
            if (!button.interactable)
            {
                label.color = theme.textDisabled;
                return;
            }
            var system = EventSystem.current;
            var selected = system != null && system.currentSelectedGameObject == gameObject;
            label.color = selected ? Focused : theme.text;
        }
    }
}
