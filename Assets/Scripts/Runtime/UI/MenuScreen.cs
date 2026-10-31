using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// A menu screen in Main.unity: builds its canvas from the theme on first open,
    /// registers with <see cref="MenuStack"/> while open so the Cancel action reaches
    /// the top screen, and puts the focus on a control when it opens. Subclasses
    /// build their controls in <see cref="Build"/> and react in the hooks.
    /// </summary>
    public abstract class MenuScreen : MonoBehaviour
    {
        [SerializeField]
        protected MenuTheme theme;

        private Canvas canvas;
        private bool built;

        public bool IsOpen { get; private set; }

        /// <summary>Raised after the screen closed, for the screen that opened it.</summary>
        public event System.Action Closed;

        protected RectTransform Root { get; private set; }

        /// <summary>Where this screen's canvas sorts; the pause menu above the title, settings above both.</summary>
        protected abstract int SortingOrder { get; }

        public MenuTheme Theme => theme;

        public void Open()
        {
            EnsureBuilt();
            if (!IsOpen)
            {
                IsOpen = true;
                MenuStack.Push(this);
            }
            canvas.enabled = true;
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            canvas.enabled = false;
            MenuStack.Remove(this);
            OnClosed();
            Closed?.Invoke();
        }

        /// <summary>The Cancel action while this screen is on top; closes by default.</summary>
        public virtual void OnCancel() => Close();

        /// <summary>Called every frame while the screen is on top of the stack.</summary>
        public virtual void OnTop() { }

        protected abstract void Build(RectTransform root);

        protected virtual void OnOpened() { }

        protected virtual void OnClosed() { }

        protected void Select(Selectable selectable)
        {
            var system = EventSystem.current;
            if (system == null)
            {
                return;
            }
            system.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
        }

        protected GameObject Selected =>
            EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        protected static bool UsingGamepad =>
            Input.InputRouter.Instance != null && Input.InputRouter.Instance.UsingGamepad;

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;
            if (theme == null)
            {
                Debug.LogError($"{name}: no MenuTheme assigned; assign Assets/Settings/MenuTheme.asset.");
                theme = ScriptableObject.CreateInstance<MenuTheme>();
            }
            canvas = MenuKit.MakeCanvas(transform, GetType().Name, SortingOrder);
            canvas.enabled = false;
            Root = canvas.GetComponent<RectTransform>();
            Build(Root);
        }
    }
}
