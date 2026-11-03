using Descender.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.EditorTools.World
{
    /// <summary>
    /// Descender &gt; Play from here: starts a throwaway game with the hero at the centre
    /// of the Scene view, whatever scene is open (a region or a Paradise zone). It saves
    /// or discards the open scenes as you choose, plays Main.unity, and puts the scenes
    /// you had open back when play mode ends. Every ability is granted unless
    /// "Play from here (no abilities)" is used. See DevStart.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromHere
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string ParadiseScenesFolder = "Assets/Scenes/Paradise/";
        public const string ParadiseEntryPath = "Assets/Scenes/Paradise/ParadiseMain.unity";
        private const string RestoreKey = "Descender.PlayFromHere.Restore";

        static PlayFromHere()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Descender/Play from here &p")]
        public static void Run() => Play(true);

        [MenuItem("Descender/Play from here (no abilities)")]
        public static void RunWithoutAbilities() => Play(false);

        public static void Play(bool allAbilities)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                EditorUtility.DisplayDialog("Play from here", "Open a Scene view and centre it where the hero should start.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            var setup = new Setup();
            foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            {
                if (!string.IsNullOrEmpty(scene.path))
                {
                    setup.paths.Add(scene.path);
                    setup.loaded.Add(scene.isLoaded);
                    setup.active.Add(scene.isActive);
                }
            }
            SessionState.SetString(RestoreKey, JsonUtility.ToJson(setup));
            DevStart.Leave(view.pivot, allAbilities);
            // The new Paradise has its own entry scene; everything else plays the prototype.
            var entry = setup.paths.Exists(p => p.StartsWith(ParadiseScenesFolder)) ? ParadiseEntryPath : MainScenePath;
            EditorSceneManager.OpenScene(entry, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            var json = SessionState.GetString(RestoreKey, "");
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            SessionState.EraseString(RestoreKey);
            var setup = JsonUtility.FromJson<Setup>(json);
            if (setup == null || setup.paths.Count == 0)
            {
                return;
            }
            var restored = new SceneSetup[setup.paths.Count];
            for (var i = 0; i < restored.Length; i++)
            {
                restored[i] = new SceneSetup { path = setup.paths[i], isLoaded = setup.loaded[i], isActive = setup.active[i] };
            }
            EditorSceneManager.RestoreSceneManagerSetup(restored);
        }

        [System.Serializable]
        private sealed class Setup
        {
            public System.Collections.Generic.List<string> paths = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<bool> loaded = new System.Collections.Generic.List<bool>();
            public System.Collections.Generic.List<bool> active = new System.Collections.Generic.List<bool>();
        }
    }
}
