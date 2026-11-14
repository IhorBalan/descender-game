using System.IO;
using Descender.Audio;
using Descender.CameraRig;
using Descender.Input;
using Descender.Player;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender > Tiles V2 > Build sample scenes: makes the two Tiles V2 scenes under
    /// Assets/Scenes/Sandbox from the sample map (Assets/Art/TilesV2/sample_map.txt).
    /// Neither is in the build's scene list. TilesV2Sample.unity is the map alone with a
    /// camera at 320 x 180, for a look in the editor and for the captures.
    /// TilesV2Playground.unity is the same map made playable on its own: the game's camera,
    /// its input and sounds, and the hero's prefab, run by <see cref="TilesV2Playground"/>.
    /// A scene holds only a <see cref="TilesV2World"/> pointing at the map and the tile
    /// set; the world is built from them when the scene opens, never saved in it.
    /// </summary>
    public static class TilesV2Sample
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/TilesV2Sample.unity";
        public const string PlaygroundPath = "Assets/Scenes/Sandbox/TilesV2Playground.unity";
        public const string MapPath = TilesV2Manifest.Folder + "/sample_map.txt";

        /// <summary>The map's place the sample scene's camera looks at.</summary>
        public const string FirstView = "surface";

        private const string HeroPath = "Assets/Prefabs/Player.prefab";
        private const string ActionsPath = "Assets/Settings/Descender.inputactions";
        private const string SfxLibraryPath = "Assets/Data/SfxLibrary.asset";

        [MenuItem("Descender/Tiles V2/Build sample scenes")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            TilesV2Builder.Build();
            BuildPlayground();
            Build();
            Debug.Log($"Tiles V2: built {ScenePath} and {PlaygroundPath} from {MapPath}.");
        }

        /// <summary>Builds and saves the sample scene; returns it, open, its world built.</summary>
        public static Scene Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = MakeWorld(false);
            var at = world.TryView(FirstView, out var view) ? view : world.Area.center;
            MakeCamera(at, false);
            Save(scene, ScenePath);
            return scene;
        }

        /// <summary>Builds and saves the playground scene; returns it, open, its world built.</summary>
        public static Scene BuildPlayground()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = MakeWorld(true);
            var camera = MakeCamera(world.Spawn + new Vector2(0f, 2f), true);

            var systems = new GameObject("Systems");
            var router = systems.AddComponent<InputRouter>();
            var actions = Load<InputActionAsset>(ActionsPath);
            var wiring = new SerializedObject(router);
            wiring.FindProperty("actions").objectReferenceValue = actions;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
            if (library != null)
            {
                systems.AddComponent<Sfx>().Library = library;
            }

            var hero = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(HeroPath), scene);
            hero.name = "Hero";
            hero.transform.position = world.Spawn;

            var playground = new GameObject("Playground").AddComponent<TilesV2Playground>();
            playground.Configure(world, hero.GetComponent<PlayerController>(), camera.GetComponent<GameCamera>());
            Save(scene, PlaygroundPath);
            return scene;
        }

        // The world of the sample map, built so its start and its places can be read.
        private static TilesV2World MakeWorld(bool dayRuns)
        {
            var set = TilesV2Builder.LoadSet();
            if (set == null)
            {
                throw new FileNotFoundException("Tiles V2: build the tiles first.", TilesV2Manifest.SetPath);
            }
            var world = new GameObject("TilesV2 World").AddComponent<TilesV2World>();
            world.Configure(Load<TextAsset>(MapPath), set, set.dayStart, dayRuns, true);
            world.Build();
            return world;
        }

        private static Camera MakeCamera(Vector2 at, bool game)
        {
            var holder = new GameObject("Main Camera") { tag = "MainCamera" };
            holder.transform.position = new Vector3(at.x, at.y, -10f);
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 180f / 16f / 2f; // 180 px tall at 16 px a unit
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            camera.GetUniversalAdditionalCameraData();
            var pixelPerfect = holder.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = 16;
            pixelPerfect.refResolutionX = 320;
            pixelPerfect.refResolutionY = 180;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            if (game)
            {
                holder.AddComponent<AudioListener>();
                holder.AddComponent<GameCamera>();
            }
            return camera;
        }

        private static void Save(Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
        }

        private static T Load<T>(string path)
            where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException($"Tiles V2: {path} is missing.", path);
            }
            return asset;
        }
    }
}
