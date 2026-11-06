using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Descender.EditorTools.Setup
{
    /// <summary>
    /// Makes the Play button start the game whatever scene is open. A fresh checkout opens an
    /// untitled scene with only a camera, and Play on it shows an empty blue screen; a region or
    /// zone scene on its own has no GameFlow, no hero and no world streamer. So while
    /// Descender &gt; Play from Main scene is ticked (the default), Play loads the game's entry scene
    /// instead: ParadiseMain for a scene in the new Paradise folder, Main for everything else. The
    /// scene you were editing comes back when play mode ends, as Unity always does. Untick it to play
    /// exactly the scene that is open. A PlayMode test run is left alone: it enters play mode in
    /// the Test Framework's own bootstrap scene, and starting in Main instead leaves it waiting
    /// forever for tests that never begin.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMain
    {
        public const string MenuPath = "Descender/Play from Main scene";
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string ParadiseScenesFolder = "Assets/Scenes/Paradise/";
        public const string ParadiseEntryPath = "Assets/Scenes/Paradise/ParadiseMain.unity";

        /// <summary>
        /// Where the Unity Test Framework saves the bootstrap scene of a PlayMode test run
        /// (<c>Assets/InitTestScene&lt;guid&gt;.unity</c>). It holds the test controller, so play
        /// mode must start in it or the tests never begin.
        /// </summary>
        public const string TestRunnerScenePrefix = "Assets/InitTestScene";
        private const string PrefKey = "Descender.PlayFromMain";

        static PlayFromMain()
        {
            EditorSceneManager.sceneOpened += (_, _) => Apply();
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Apply();
            EditorSceneManager.newSceneCreated += (_, _, _) => Apply();
            // The test runner makes its bootstrap scene untitled and names it on save.
            EditorSceneManager.sceneSaved += _ => Apply();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += Apply;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set
            {
                EditorPrefs.SetBool(PrefKey, value);
                Apply();
            }
        }

        [MenuItem(MenuPath, priority = 0)]
        public static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        public static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>The scene Play should load for the scene that is active now.</summary>
        public static string EntryFor(string activeScenePath)
        {
            return activeScenePath != null && activeScenePath.StartsWith(ParadiseScenesFolder)
                ? ParadiseEntryPath
                : MainScenePath;
        }

        /// <summary>Whether a scene path is the Test Framework's PlayMode bootstrap scene.</summary>
        public static bool IsTestRunnerScene(string scenePath)
        {
            return scenePath != null && scenePath.StartsWith(TestRunnerScenePrefix, StringComparison.Ordinal);
        }

        private static bool TestRunnerSceneIsOpen()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (IsTestRunnerScene(SceneManager.GetSceneAt(i).path))
                {
                    return true;
                }
            }
            return false;
        }

        // The last moment before play mode: a PlayMode test run must start in its own scene, not
        // in the game's entry scene, whatever an earlier scene event set.
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && TestRunnerSceneIsOpen())
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            if (!Enabled || TestRunnerSceneIsOpen())
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var entry = EntryFor(SceneManager.GetActiveScene().path);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry);
        }
    }
}
