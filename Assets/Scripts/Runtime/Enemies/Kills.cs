using System.Collections.Generic;

namespace Descender.Enemies
{
    /// <summary>
    /// The placements killed since the hero last respawned, so a region re-entered
    /// through a door does not bring them back, while a death does (the Godot level
    /// rebuilt its enemies on respawn). Plain static state: cleared by the hero's
    /// respawn and by a new game.
    /// </summary>
    public static class Kills
    {
        private static readonly HashSet<string> Dead = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>Raised when the set is cleared: the enemies come back.</summary>
        public static event System.Action Cleared;

        public static int Count => Dead.Count;

        public static void Record(string placementId)
        {
            if (!string.IsNullOrEmpty(placementId))
            {
                Dead.Add(placementId);
            }
        }

        public static bool IsDead(string placementId) => !string.IsNullOrEmpty(placementId) && Dead.Contains(placementId);

        public static void Clear()
        {
            if (Dead.Count == 0)
            {
                return;
            }
            Dead.Clear();
            Cleared?.Invoke();
        }

        /// <summary>Forgets everything, including listeners; for a new game and the tests.</summary>
        public static void Reset()
        {
            Dead.Clear();
            Cleared = null;
        }
    }
}
