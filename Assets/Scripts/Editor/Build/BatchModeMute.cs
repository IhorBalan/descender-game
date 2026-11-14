using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.Build
{
    /// <summary>
    /// Keeps every batch-mode editor run silent: the pre-push hook's tests, a <c>-runTests</c> run
    /// and any <c>-executeMethod</c> tool alike. It turns on the editor's own master mute (the Game
    /// view's Mute Audio), which sits below every listener and mixer and, unlike a static, outlives
    /// the domain reload on entering play mode. <c>MuteDuringTests</c> in the PlayMode tests only
    /// covers a Test Framework run, and only between its RunStarted and RunFinished: the editor still
    /// played the last scene's music while it left play mode after a run. The setting the editor had
    /// is restored when it quits, so an editor opened by hand afterwards is not left muted.
    /// </summary>
    [InitializeOnLoad]
    public static class BatchModeMute
    {
        // The mute the editor had before this run muted it, across domain reloads: -1 none, 0 off, 1 on.
        private const string PreviousKey = "Descender.BatchModeMute.Previous";

        static BatchModeMute()
        {
            if (Application.isBatchMode)
            {
                Mute();
            }
        }

        /// <summary>Mutes the editor's audio for the rest of this run, remembering the setting to restore.</summary>
        public static void Mute()
        {
            SessionState.SetInt(
                PreviousKey,
                Remember(SessionState.GetInt(PreviousKey, -1), EditorUtility.audioMasterMute)
            );
            EditorUtility.audioMasterMute = true;
            EditorApplication.quitting -= Restore;
            EditorApplication.quitting += Restore;
        }

        /// <summary>Puts back the mute setting <see cref="Mute"/> found, if it muted anything.</summary>
        public static void Restore()
        {
            var previous = SessionState.GetInt(PreviousKey, -1);
            if (previous < 0)
            {
                return;
            }
            EditorUtility.audioMasterMute = previous == 1;
            SessionState.EraseInt(PreviousKey);
        }

        /// <summary>
        /// The setting to keep for <see cref="Restore"/>: the one remembered already (-1 for none), else
        /// the current one. After a domain reload the editor is already muted by this class, and that
        /// mute must not be remembered as the user's.
        /// </summary>
        public static int Remember(int remembered, bool currentMute)
        {
            if (remembered >= 0)
            {
                return remembered;
            }
            return currentMute ? 1 : 0;
        }
    }
}
