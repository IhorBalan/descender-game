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
    /// prefab and the world atlas when they exist), and puts Main first in the
    /// build settings followed by every Region_*.unity. Safe to run again: it only
    /// adds what is missing and fills references that are empty.
    /// </summary>
    public static class MainSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string ActionsPath = "Assets/Settings/Descender.inputactions";
        public const string HeroPrefabPath = "Assets/Prefabs/Player.prefab";
        public const string WorldAtlasPath = "Assets/Data/World.asset";
        public const string MusicFolder = "Assets/Audio/music";

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
            if (FindRoot(scene, "UI") == null)
            {
                new GameObject("UI");
            }

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
            var regions = SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Descender: {ScenePath} is set up and first in the build settings with {regions} region scene(s)"
                    + (notes.Count > 0 ? $" ({string.Join(", ", notes)})." : ".")
            );
        }

        /// <summary>
        /// Main first, then every Assets/Scenes/Region_*.unity in name order. Returns the region count.
        /// </summary>
        public static int SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            var regions = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith(WorldStreamer.ScenePrefix, System.StringComparison.Ordinal))
                {
                    regions.Add(path);
                }
            }
            regions.Sort(System.StringComparer.Ordinal);
            foreach (var path in regions)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            return regions.Count;
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
