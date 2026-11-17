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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender > Tiles V2 > Build sample scenes: makes the two Tiles V2 scenes under
    /// Assets/Scenes/Sandbox from the sample map (Assets/Art/TilesV2/sample_map.txt).
    /// Neither is in the build's scene list. TilesV2Sample.unity is the map alone with a
    /// camera of the tile set's view, for a look in the editor and for the captures.
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

        /// <summary>The world: Paradise, the Earth and the nine circles on one map, played as a journey.</summary>
        public const string DescentPath = "Assets/Scenes/Sandbox/TilesV2Descent.unity";
        public const string WorldMapPath = TilesV2Manifest.Folder + "/world_map.txt";

        /// <summary>The Paradise level: seven zones from the Waking Hollow to the Low Gate, played from its start with nothing.</summary>
        public const string ParadisePath = "Assets/Scenes/Sandbox/TilesV2Paradise.unity";
        public const string ParadiseMapPath = TilesV2Manifest.Folder + "/paradise_map.txt";
        public const string CheckpointPath = "Assets/Prefabs/Pickups/Checkpoint.prefab";
        public const string AbilityPath = "Assets/Prefabs/Pickups/AbilityPickup.prefab";
        public const string UpgradePath = "Assets/Prefabs/Pickups/Upgrade.prefab";

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
            BuildDescent();
            BuildParadise();
            Build();
            Debug.Log(
                $"Tiles V2: built {ScenePath} and {PlaygroundPath} from {MapPath}, {DescentPath} from {WorldMapPath}, {ParadisePath} from {ParadiseMapPath}."
            );
        }

        /// <summary>Builds and saves the sample scene; returns it, open, its world built.</summary>
        public static Scene Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = MakeWorld(MapPath, false);
            var at = world.TryView(FirstView, out var view) ? view : world.Area.center;
            MakeCamera(world.Framed(at), false, world.Set);
            Save(scene, ScenePath);
            return scene;
        }

        /// <summary>Builds and saves the playground scene; returns it, open, its world built.</summary>
        public static Scene BuildPlayground() => BuildPlayable(MapPath, PlaygroundPath, false);

        /// <summary>
        /// Builds and saves the world scene: the playground's parts on the world map, as a
        /// journey (the hero starts with nothing; the game's checkpoint and ability pickup
        /// stand where the map marks them). Returns it, open, its world built.
        /// </summary>
        public static Scene BuildDescent() => BuildPlayable(WorldMapPath, DescentPath, true);

        /// <summary>The Paradise level's glow: what is bright there blooms a little (a Volume profile of the standard's own).</summary>
        public const string ParadiseGlowPath = "Assets/Art/TilesV2/TilesV2ParadiseGlow.asset";

        /// <summary>The name of the object that holds the level's glow in its scene.</summary>
        public const string GlowName = "Glow";

        /// <summary>
        /// Builds and saves the Paradise level's scene: a journey of its own, on its own
        /// map, and with a glow: its camera renders post-processing and a global Volume
        /// blooms what is bright (lamps, fruit, fire, white stone in the sun).
        /// </summary>
        public static Scene BuildParadise() => BuildPlayable(ParadiseMapPath, ParadisePath, true, true);

        /// <summary>The glow's profile, made if it is not there: bloom over a high threshold, soft and wide.</summary>
        public static VolumeProfile ParadiseGlow()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ParadiseGlowPath);
            if (profile != null)
            {
                return profile;
            }
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ParadiseGlowPath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(GlowThreshold);
            bloom.intensity.Override(GlowIntensity);
            bloom.scatter.Override(GlowScatter);
            bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(bloom, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public const float GlowThreshold = 0.95f;
        public const float GlowIntensity = 0.85f;
        public const float GlowScatter = 0.5f;

        /// <summary>Builds a scene of the standard's that is not there yet (the world's, the Paradise level's).</summary>
        public static void Ensure(string scenePath)
        {
            if (System.IO.File.Exists(scenePath))
            {
                return;
            }
            if (scenePath == ParadisePath)
            {
                BuildParadise();
            }
            else if (scenePath == DescentPath)
            {
                BuildDescent();
            }
        }

        private static Scene BuildPlayable(string mapPath, string scenePath, bool journey, bool glows = false)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = MakeWorld(mapPath, true);
            var camera = MakeCamera(world.Framed(world.Spawn), true, world.Set);
            if (glows)
            {
                // Loaded here, not handed in: a new scene unloads an asset that only a local holds.
                var glow = ParadiseGlow();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = new GameObject(GlowName).AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = glow;
            }

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
            if (journey)
            {
                playground.ConfigureJourney(
                    Load<GameObject>(CheckpointPath),
                    Load<GameObject>(AbilityPath),
                    Load<GameObject>(UpgradePath)
                );
            }
            Save(scene, scenePath);
            return scene;
        }

        // The world of a map, built so its start and its places can be read.
        private static TilesV2World MakeWorld(string mapPath, bool dayRuns)
        {
            var set = TilesV2Builder.LoadSet();
            if (set == null)
            {
                throw new FileNotFoundException("Tiles V2: build the tiles first.", TilesV2Manifest.SetPath);
            }
            var world = new GameObject("TilesV2 World").AddComponent<TilesV2World>();
            world.Configure(Load<TextAsset>(mapPath), set, set.dayStart, dayRuns, true);
            world.Build();
            return world;
        }

        // A pixel-perfect camera of the tile set's view; `game` adds what the playground's needs.
        private static Camera MakeCamera(Vector2 at, bool game, TilesV2Set set)
        {
            var holder = new GameObject("Main Camera") { tag = "MainCamera" };
            holder.transform.position = new Vector3(at.x, at.y, -10f);
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = set.View.y / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            camera.GetUniversalAdditionalCameraData();
            var pixelPerfect = holder.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = 16;
            pixelPerfect.refResolutionX = set.viewWidth;
            pixelPerfect.refResolutionY = set.viewHeight;
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
