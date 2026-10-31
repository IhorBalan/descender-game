using System.Collections.Generic;
using Descender.World;
using UnityEngine;

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

        public static bool IsDead(string placementId) =>
            !string.IsNullOrEmpty(placementId) && Dead.Contains(placementId);

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
            Hook();
        }

        /// <summary>
        /// Tells the placements to leave out what is dead, and to stand their enemies and
        /// drops up again when the set clears (the hero respawned). Runs once at load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Hook()
        {
            WorldPlacement.IsGone = IsDead;
            Cleared -= RespawnPlacements;
            Cleared += RespawnPlacements;
        }

        private static void RespawnPlacements()
        {
            foreach (var placement in Object.FindObjectsByType<WorldPlacement>(FindObjectsSortMode.None))
            {
                placement.Spawn();
            }
        }
    }
}
