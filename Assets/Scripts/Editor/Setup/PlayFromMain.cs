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
    /// exactly the scene that is open.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMain
    {
        public const string MenuPath = "Descender/Play from Main scene";
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string ParadiseScenesFolder = "Assets/Scenes/Paradise/";
        public const string ParadiseEntryPath = "Assets/Scenes/Paradise/ParadiseMain.unity";
        private const string PrefKey = "Descender.PlayFromMain";

        static PlayFromMain()
        {
            EditorSceneManager.sceneOpened += (_, _) => Apply();
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Apply();
            EditorSceneManager.newSceneCreated += (_, _, _) => Apply();
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

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var entry = EntryFor(SceneManager.GetActiveScene().path);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry);
        }
    }
}
