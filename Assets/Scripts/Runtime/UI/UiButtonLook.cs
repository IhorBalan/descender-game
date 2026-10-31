using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Keeps a stone button's label in the theme colours: the focus colour while it
    /// is selected or hovered, the disabled colour while it cannot be used, its own
    /// colour otherwise. A row that wants another colour sets <see cref="BaseColor"/>.
    /// Also raises <see cref="Focused"/>, which the menus use for their description line.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonLook : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Button button;
        private TextMeshProUGUI label;
        private bool selected;
        private bool hovered;
        private bool wasInteractable = true;

        public event System.Action Focused;

        public Color BaseColor { get; private set; } = UiTheme.Text;

        public void Bind(Button target, TextMeshProUGUI text)
        {
            button = target;
            label = text;
            Refresh();
        }

        public void SetBaseColor(Color color)
        {
            BaseColor = color;
            Refresh();
        }

        private void Update()
        {
            if (button != null && button.interactable != wasInteractable)
            {
                Refresh();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            Refresh();
            Focused?.Invoke();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Refresh();
        }

        private void Refresh()
        {
            if (label == null || button == null)
            {
                return;
            }
            wasInteractable = button.interactable;
            if (!button.interactable)
            {
                label.color = UiTheme.TextDisabled;
            }
            else if (selected || hovered)
            {
                label.color = UiTheme.TextFocus;
            }
            else
            {
                label.color = BaseColor;
            }
        }
    }
}
