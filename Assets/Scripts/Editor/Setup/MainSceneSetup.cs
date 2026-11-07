using System.Collections.Generic;
using System.IO;
using Descender.Audio;
using Descender.CameraRig;
using Descender.Game;
using Descender.Input;
using Descender.Player;
using Descender.UI;
using Descender.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Descender.EditorTools.Setup
{
    /// <summary>
    /// Descender > Setup > Main scene: makes sure Assets/Scenes/Main.unity has the
    /// camera, the systems (InputRouter, WorldStreamer, GameFlow, Music) and the
    /// fade, wires their references (the input actions, the music clips, the hero
    /// prefab and the world atlas when they exist), adds the dialogue box and runner
    /// under the UI root (and to the Paradise entry scene, a copy of Main), and puts
    /// Main first in the build settings followed by every Region_*.unity and the
    /// Paradise scenes. Safe to run again: it only adds what is missing and fills
    /// references that are empty.
    /// </summary>
    public static class MainSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string ActionsPath = "Assets/Settings/Descender.inputactions";
        public const string HeroPrefabPath = "Assets/Prefabs/Player.prefab";
        public const string WorldAtlasPath = "Assets/Data/World.asset";
        public const string MusicFolder = "Assets/Audio/music";
        public const string MenuThemePath = "Assets/Settings/MenuTheme.asset";
        public const string DialoguePath = "Assets/Data/dialogue/" + GameFlow.LevelId + ".json";
        public const string ParadiseSceneFolder = "Assets/Scenes/Paradise";
        public const string ParadiseEntryScenePath = ParadiseSceneFolder + "/ParadiseMain.unity";
        public const string DialogueBoxName = "Dialogue Box";
        public const string DialogueRunnerName = "Dialogue Runner";

        [MenuItem("Descender/Setup/Main scene")]
        public static void Run()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Descender: stop playing before setting up the main scene.");
                return;
            }
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var notes = new List<string>();

            var cameraObject = FindRoot(scene, "Main Camera") ?? new GameObject("Main Camera");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = Ensure<Camera>(cameraObject);
            camera.orthographic = true;
            camera.orthographicSize = 180f / 16f / 2f; // 180 px tall at 16 px a unit
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.09f);
            camera.GetUniversalAdditionalCameraData();
            Ensure<AudioListener>(cameraObject);
            var pixelPerfect = Ensure<PixelPerfectCamera>(cameraObject);
            pixelPerfect.assetsPPU = 16;
            pixelPerfect.refResolutionX = 320;
            pixelPerfect.refResolutionY = 180;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            var gameCamera = Ensure<GameCamera>(cameraObject);

            var systems = FindRoot(scene, "Systems") ?? new GameObject("Systems");
            var input = Ensure<InputRouter>(systems);
            var inputObject = new SerializedObject(input);
            var actions = inputObject.FindProperty("actions");
            if (actions.objectReferenceValue == null)
            {
                actions.objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
                inputObject.ApplyModifiedPropertiesWithoutUndo();
            }
            var streamer = Ensure<WorldStreamer>(systems);
            var streamerObject = new SerializedObject(streamer);
            var atlas = streamerObject.FindProperty("atlas");
            if (atlas != null && atlas.objectReferenceValue == null)
            {
                atlas.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(WorldAtlasPath);
                streamerObject.ApplyModifiedPropertiesWithoutUndo();
                notes.Add(atlas.objectReferenceValue != null ? "world atlas set" : "no world atlas yet");
            }
            var music = Ensure<Music>(systems);
            var musicObject = new SerializedObject(music);
            var tracks = musicObject.FindProperty("tracks");
            tracks.ClearArray();
            foreach (var clip in LoadAll<AudioClip>(MusicFolder))
            {
                tracks.InsertArrayElementAtIndex(tracks.arraySize);
                tracks.GetArrayElementAtIndex(tracks.arraySize - 1).objectReferenceValue = clip;
            }
            musicObject.ApplyModifiedPropertiesWithoutUndo();

            var fadeObject = FindRoot(scene, "ScreenFade") ?? new GameObject("ScreenFade");
            var fade = Ensure<ScreenFade>(fadeObject);
            var ui = FindRoot(scene, "UI") ?? new GameObject("UI");
            Ensure<MenuStack>(ui);
            var menuTheme = AssetDatabase.LoadAssetAtPath<MenuTheme>(MenuThemePath);
            var settingsScreen = Ensure<SettingsScreen>(ui);
            var titleScreen = Ensure<TitleScreen>(ui);
            var pauseScreen = Ensure<PauseScreen>(ui);
            foreach (var screen in new MenuScreen[] { settingsScreen, titleScreen, pauseScreen })
            {
                var screenObject = new SerializedObject(screen);
                SetIfEmpty(screenObject, "theme", menuTheme);
                SetIfEmpty(screenObject, "settings", settingsScreen);
                screenObject.ApplyModifiedPropertiesWithoutUndo();
            }

            notes.AddRange(EnsureDialogue(ui));

            var flow = Ensure<GameFlow>(systems);
            var flowObject = new SerializedObject(flow);
            SetIfEmpty(flowObject, "gameCamera", gameCamera);
            SetIfEmpty(flowObject, "music", music);
            SetIfEmpty(flowObject, "fade", fade);
            var hero = flowObject.FindProperty("heroPrefab");
            if (hero.objectReferenceValue == null)
            {
                hero.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PlayerController>(HeroPrefabPath);
                notes.Add(hero.objectReferenceValue != null ? "hero prefab set" : "no hero prefab yet");
            }
            flowObject.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (EnsureDialogueIn(ParadiseEntryScenePath))
            {
                notes.Add($"dialogue added to {ParadiseEntryScenePath}");
            }
            var regions = SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Descender: {ScenePath} is set up and first in the build settings with {regions} region scene(s)"
                    + (notes.Count > 0 ? $" ({string.Join(", ", notes)})." : ".")
            );
        }

        /// <summary>
        /// The dialogue pair under the UI root: a DialogueBox and a DialogueRunner bound to
        /// it, the runner reading Assets/Data/dialogue/&lt;GameFlow.LevelId&gt;.json. Its HUD and
        /// shop stay empty until those screens are in the scene; the runner works without.
        /// Adds what is missing and fills empty references only. Returns what it did.
        /// </summary>
        public static List<string> EnsureDialogue(GameObject ui)
        {
            var notes = new List<string>();
            var box = ui.GetComponentInChildren<DialogueBox>(true);
            if (box == null)
            {
                box = Child(ui, DialogueBoxName).AddComponent<DialogueBox>();
                notes.Add("dialogue box added");
            }
            var runner = ui.GetComponentInChildren<DialogueRunner>(true);
            if (runner == null)
            {
                runner = Child(ui, DialogueRunnerName).AddComponent<DialogueRunner>();
                notes.Add("dialogue runner added");
            }
            var runnerObject = new SerializedObject(runner);
            var file = runnerObject.FindProperty("dialogueFile");
            if (file.objectReferenceValue == null)
            {
                file.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(DialoguePath);
                notes.Add(file.objectReferenceValue != null ? "dialogue file set" : $"no {DialoguePath} yet");
            }
            SetIfEmpty(runnerObject, "box", box);
            if (runnerObject.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(runner);
            }
            return notes;
        }

        /// <summary>
        /// The same dialogue pair in another entry scene (ParadiseMain is a copy of Main with
        /// its own camera and atlas, so only this part of the setup applies to it). Saves the
        /// scene only when something was added, and returns whether it was. Leaves the scene open.
        /// </summary>
        public static bool EnsureDialogueIn(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                return false;
            }
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var ui = FindRoot(scene, "UI");
            if (ui == null)
            {
                Debug.LogWarning($"Descender: {scenePath} has no UI root, so no dialogue was added to it.");
                return false;
            }
            if (EnsureDialogue(ui).Count == 0)
            {
                return false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        /// <summary>
        /// Main first, then every Region_*.unity under Assets/Scenes and every scene of the
        /// rebuilt Paradise (Assets/Scenes/Paradise: its entry scene and the zones its region
        /// streams by name). Scenes already listed keep their order and their tick; new ones
        /// follow in name order. Returns the region count.
        /// </summary>
        public static int SetBuildScenes()
        {
            var wanted = new List<string>();
            var regions = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var region = Path.GetFileName(path)
                    .StartsWith(WorldStreamer.ScenePrefix, System.StringComparison.Ordinal);
                if (region || path.StartsWith(ParadiseSceneFolder + "/", System.StringComparison.Ordinal))
                {
                    wanted.Add(path);
                    regions += region ? 1 : 0;
                }
            }
            wanted.Sort(System.StringComparer.Ordinal);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var listed in EditorBuildSettings.scenes)
            {
                if (wanted.Remove(listed.path))
                {
                    scenes.Add(new EditorBuildSettingsScene(listed.path, listed.enabled));
                }
            }
            foreach (var path in wanted)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            return regions;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }
            return null;
        }

        private static T Ensure<T>(GameObject target)
            where T : Component
        {
            return target.TryGetComponent<T>(out var found) ? found : target.AddComponent<T>();
        }

        private static void SetIfEmpty(SerializedObject target, string field, Object value)
        {
            var property = target.FindProperty(field);
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static List<T> LoadAll<T>(string folder)
            where T : Object
        {
            var found = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    found.Add(asset);
                }
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return found;
        }
    }
}
