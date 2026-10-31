using TMPro;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// A line of hint text with button prompts in it, drawn for whichever device
    /// the player last used and redrawn when that changes. <see cref="Template"/> is
    /// written for <see cref="Prompts.Render"/>. Used for the hints along the bottom
    /// of the menus, the HUD's message line and the talk prompt over an NPC.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class PromptLabel : MonoBehaviour
    {
        [SerializeField]
        [TextArea]
        private string template = "";

        private TMP_Text text;
        private bool drawnForGamepad;
        private Save.PromptLayout drawnLayout;
        private bool drawn;

        public string Template
        {
            get => template;
            set
            {
                template = value ?? "";
                Render();
            }
        }

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Render();
        }

        private void Update()
        {
            if (!drawn || drawnForGamepad != Prompts.UsingGamepad || drawnLayout != Prompts.Layout)
            {
                Render();
            }
        }

        public void Render()
        {
            if (text == null)
            {
                text = GetComponent<TMP_Text>();
            }
            drawnForGamepad = Prompts.UsingGamepad;
            drawnLayout = Prompts.Layout;
            drawn = true;
            text.text = Prompts.Render(template);
        }
    }
}
