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
    /// A gilded row (<see cref="BindGilded"/>) shows its glow while focused and takes
    /// the title's ivory and faded colours.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonLook
        : MonoBehaviour,
            ISelectHandler,
            IDeselectHandler,
            IPointerEnterHandler,
            IPointerExitHandler
    {
        private Button button;
        private TextMeshProUGUI label;
        private bool selected;
        private bool hovered;
        private bool wasInteractable = true;
        private Image glow;
        private bool gilded;

        public event System.Action Focused;

        public Color BaseColor { get; private set; } = UiTheme.Text;

        public void Bind(Button target, TextMeshProUGUI text)
        {
            button = target;
            label = text;
            Refresh();
        }

        /// <summary>Binds a gilded row: `light` shows while it is focused, its text ivory then, faded when it cannot be used.</summary>
        public void BindGilded(Button target, TextMeshProUGUI text, Image light)
        {
            gilded = true;
            glow = light;
            BaseColor = TitleKit.Parchment;
            Bind(target, text);
        }

        /// <summary>Whether the row shows as focused now (selected or under the mouse).</summary>
        public bool Lit => selected || hovered;

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
            var lit = button.interactable && (selected || hovered);
            if (glow != null)
            {
                glow.enabled = lit;
            }
            if (!button.interactable)
            {
                label.color = gilded ? TitleKit.Faded : UiTheme.TextDisabled;
            }
            else if (lit)
            {
                label.color = gilded ? TitleKit.Ivory : UiTheme.TextFocus;
            }
            else
            {
                label.color = BaseColor;
            }
        }
    }
}
