using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// A full-screen page over the game: the pack, a trader's wares, the camp, the
    /// pause menu. Each builds its canvas once, on first open, from code
    /// (<see cref="Build"/>), and is pushed on the <see cref="UiStack"/> while open,
    /// which pauses the game for a pausing screen and switches the input to the UI
    /// map. Cancel on the top screen calls <see cref="OnCancel"/>, which closes it.
    /// The menu thread's title, settings and pause screens derive from this too.
    /// </summary>
    public abstract class UiScreen : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Whether the game stands still (Time.timeScale 0) while this screen is open.")]
        private bool pausesGame = true;

        private Canvas canvas;
        private bool built;

        public event System.Action Opened;
        public event System.Action Closed;

        public bool IsOpen { get; private set; }

        public bool PausesGame
        {
            get => pausesGame;
            protected set => pausesGame = value;
        }

        /// <summary>The rect every control of the screen hangs under.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>Canvases of open screens sort above the HUD (5) and the dialogue box (6).</summary>
        protected virtual int SortOrder => 10;

        /// <summary>Makes the controls under `root`, once.</summary>
        protected abstract void Build(RectTransform root);

        /// <summary>Called after the screen is shown; refresh and take the focus here.</summary>
        protected virtual void OnOpen() { }

        protected virtual void OnClose() { }

        /// <summary>Cancel (Esc, B) while this is the top screen. Closes it unless overridden.</summary>
        public virtual void OnCancel() => Close();

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            EnsureBuilt();
            IsOpen = true;
            canvas.enabled = true;
            UiStack.Push(this);
            OnOpen();
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            if (canvas != null)
            {
                canvas.enabled = false;
            }
            UiStack.Pop(this);
            OnClose();
            Closed?.Invoke();
        }

        /// <summary>Builds the canvas now, so a test can look at the controls before opening.</summary>
        public void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;
            canvas = UiKit.MakeInteractiveCanvas($"{name}Canvas", SortOrder, transform);
            Root = canvas.GetComponent<RectTransform>();
            Build(Root);
            canvas.enabled = false;
        }

        protected virtual void Update()
        {
            if (IsOpen && UiStack.Top == this && UiInput.CancelPressed)
            {
                OnCancel();
            }
        }

        protected virtual void OnDestroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                UiStack.Pop(this);
            }
        }
    }
}
