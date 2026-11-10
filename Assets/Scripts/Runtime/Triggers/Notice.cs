using System;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// A short message to the player from a trigger. It goes to the HUD's message line
    /// when there is a HUD; the last one is kept and an event is raised either way, so a
    /// test or another screen can follow them.
    /// </summary>
    public static class Notice
    {
        /// <summary>The last message shown, or "".</summary>
        public static string Last { get; private set; } = "";

        public static event Action<string> Shown;

        public static void Reset() => Last = "";

        public static void Show(string text, float seconds = UI.Hud.DefaultMessageTime)
        {
            Last = text ?? "";
            var hud = UnityEngine.Object.FindAnyObjectByType<UI.Hud>();
            if (hud != null)
            {
                hud.ShowMessage(Last, seconds);
            }
            Shown?.Invoke(Last);
        }
    }
}
