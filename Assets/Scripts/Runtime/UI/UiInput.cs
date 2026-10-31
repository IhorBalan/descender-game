using UnityEngine.InputSystem;

namespace Descender.UI
{
    /// <summary>
    /// The UI map's actions (Navigate, Submit, Cancel, PageLeft, PageRight) looked up
    /// once from the <see cref="Input.InputRouter"/>'s asset, for screens that poll
    /// them in Update. Button navigation and Submit go through the EventSystem's
    /// InputSystemUIInputModule, which the boot flow sets up; this is for what the
    /// module does not do, such as closing on Cancel or turning pages.
    /// </summary>
    public static class UiInput
    {
        private static InputActionAsset asset;
        private static InputAction cancel;
        private static InputAction submit;
        private static InputAction navigate;
        private static InputAction pageLeft;
        private static InputAction pageRight;

        public static bool CancelPressed => Action(ref cancel, "Cancel")?.WasPressedThisFrame() ?? false;
        public static bool SubmitPressed => Action(ref submit, "Submit")?.WasPressedThisFrame() ?? false;
        public static bool PageLeftPressed => Action(ref pageLeft, "PageLeft")?.WasPressedThisFrame() ?? false;
        public static bool PageRightPressed => Action(ref pageRight, "PageRight")?.WasPressedThisFrame() ?? false;

        /// <summary>The navigate vector, as the UI map reads it (the d-pad, the stick, the arrows).</summary>
        public static UnityEngine.Vector2 Navigate =>
            Action(ref navigate, "Navigate")?.ReadValue<UnityEngine.Vector2>() ?? UnityEngine.Vector2.zero;

        /// <summary>The UI action map, or null before the router exists.</summary>
        public static InputActionMap Map
        {
            get
            {
                var router = Input.InputRouter.Instance;
                var current = router != null ? router.Actions : null;
                if (current != asset)
                {
                    asset = current;
                    cancel = submit = navigate = pageLeft = pageRight = null;
                }
                return asset != null ? asset.FindActionMap("UI", false) : null;
            }
        }

        private static InputAction Action(ref InputAction cached, string name)
        {
            var map = Map;
            if (map == null)
            {
                return null;
            }
            if (cached == null)
            {
                cached = map.FindAction(name, false);
            }
            return cached;
        }
    }
}
