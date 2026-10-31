using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.UI
{
    /// <summary>
    /// The open menu screens, top last. Routes the UI map's Cancel action to the top
    /// screen and lets it run per-frame checks. One in Main.unity; the screens
    /// register themselves through <see cref="Push"/> and <see cref="Remove"/>.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class MenuStack : MonoBehaviour
    {
        private static readonly List<MenuScreen> Screens = new List<MenuScreen>();

        private InputAction cancel;

        public static MenuScreen Top => Screens.Count > 0 ? Screens[Screens.Count - 1] : null;

        public static bool AnyOpen => Screens.Count > 0;

        public static void Push(MenuScreen screen)
        {
            Screens.Remove(screen);
            Screens.Add(screen);
        }

        public static void Remove(MenuScreen screen) => Screens.Remove(screen);

        private void OnEnable()
        {
            var router = Input.InputRouter.Instance;
            if (router == null || router.Actions == null)
            {
                return;
            }
            cancel = router.Actions.FindActionMap("UI", true).FindAction("Cancel", true);
            cancel.performed += OnCancel;
        }

        private void OnDisable()
        {
            if (cancel != null)
            {
                cancel.performed -= OnCancel;
                cancel = null;
            }
            Screens.Clear();
        }

        private void Update()
        {
            var top = Top;
            if (top != null)
            {
                top.OnTop();
            }
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            var top = Top;
            if (top != null)
            {
                top.OnCancel();
            }
        }
    }
}
