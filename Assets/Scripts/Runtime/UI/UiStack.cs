using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Descender.UI
{
    /// <summary>
    /// The screens open right now, bottom to top. Pushing the first screen switches
    /// the input to the UI map and, for a pausing screen, stops time; popping the
    /// last puts both back. The focus the game had before is restored when the
    /// stack empties, and each screen's own focus when the one over it closes.
    /// Static, like the autoloads, so a screen reaches it without a reference.
    /// </summary>
    public static class UiStack
    {
        private static readonly List<UiScreen> Screens = new List<UiScreen>();
        private static readonly List<GameObject> Focus = new List<GameObject>();
        private static float timeScaleBefore = 1f;

        public static event System.Action Changed;

        public static UiScreen Top => Screens.Count > 0 ? Screens[Screens.Count - 1] : null;

        public static bool IsAnyOpen => Screens.Count > 0;

        public static int Count => Screens.Count;

        /// <summary>Whether a screen that stops time is open.</summary>
        public static bool IsPausing
        {
            get
            {
                foreach (var screen in Screens)
                {
                    if (screen.PausesGame)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool Contains(UiScreen screen) => Screens.Contains(screen);

        public static void Push(UiScreen screen)
        {
            if (Screens.Contains(screen))
            {
                return;
            }
            if (Screens.Count == 0)
            {
                timeScaleBefore = Time.timeScale;
                Input.InputRouter.Instance?.UseUi();
            }
            Focus.Add(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null);
            Screens.Add(screen);
            Apply();
        }

        public static void Pop(UiScreen screen)
        {
            var index = Screens.IndexOf(screen);
            if (index < 0)
            {
                return;
            }
            var focus = Focus[index];
            Screens.RemoveAt(index);
            Focus.RemoveAt(index);
            if (index == Screens.Count && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(focus);
            }
            if (Screens.Count == 0)
            {
                Input.InputRouter.Instance?.UsePlayer();
            }
            Apply();
        }

        /// <summary>Closes every open screen, top first (a scene change, a death).</summary>
        public static void CloseAll()
        {
            while (Screens.Count > 0)
            {
                Screens[Screens.Count - 1].Close();
            }
        }

        /// <summary>Forgets everything without closing, for tests and scene unloads.</summary>
        public static void Reset()
        {
            Screens.Clear();
            Focus.Clear();
            Time.timeScale = 1f;
            timeScaleBefore = 1f;
        }

        private static void Apply()
        {
            Time.timeScale = IsPausing ? 0f : timeScaleBefore;
            Changed?.Invoke();
        }
    }
}
