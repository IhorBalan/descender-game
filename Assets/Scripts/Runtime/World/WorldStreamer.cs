using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Descender.World
{
    /// <summary>
    /// Keeps the regions around the hero loaded. The world is one grid, its regions
    /// stacked as bands (WorldAtlas), so the hero walks or falls from one into the
    /// next: the band he is in and its neighbours above and below are loaded as
    /// additive scenes, the rest unloaded. Owns the one global Light2D, tinted with
    /// the current region's ambient colour and eased across a crossing, and shows
    /// only the current region's backdrop. Lives in Main.unity; every region scene
    /// must be in the build's scene list.
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        public const string ScenePrefix = WorldAtlas.ScenePrefix;
        public const float AmbientFade = 2.5f;

        public static WorldStreamer Instance { get; private set; }

        [SerializeField]
        private WorldAtlas atlas;

        private readonly Dictionary<string, RegionRoot> loaded = new Dictionary<string, RegionRoot>();
        private readonly HashSet<string> loading = new HashSet<string>();
        private Light2D globalLight;
        private Color ambientFrom = Color.white;
        private Color ambientTo = Color.white;
        private float ambientT = AmbientFade;
        private int currentIndex = -1;

        public WorldAtlas Atlas => atlas;
        public Player.PlayerController Hero { get; private set; }
        public string CurrentRegion { get; private set; } = "";
        public RegionRoot Current { get; private set; }
        public bool Busy { get; private set; }

        /// <summary>The hero crossed into another region (or the first one was loaded).</summary>
        public event System.Action<RegionRoot> RegionChanged;

        public static string SceneName(string regionId) => WorldAtlas.SceneName(regionId);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            var lightObject = new GameObject("Global Light");
            lightObject.transform.SetParent(transform, false);
            globalLight = lightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;
            globalLight.color = Color.white;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (ambientT < AmbientFade)
            {
                ambientT += Time.deltaTime;
                globalLight.color = Color.Lerp(ambientFrom, ambientTo, Mathf.Clamp01(ambientT / AmbientFade));
            }
            if (Busy || Hero == null || atlas == null || currentIndex < 0)
            {
                return;
            }
            var index = atlas.IndexAt(Hero.transform.position.y);
            if (index != currentIndex)
            {
                StartCoroutine(Shift(index));
            }
        }

        /// <summary>A new game: the regions around the start, the hero at the start point.</summary>
        public void StartNewGame(Player.PlayerController hero)
        {
            EnterAt(hero, atlas != null ? atlas.start : Vector2.zero);
        }

        /// <summary>Continue from a save: the hero at its checkpoint, or the start for an index out of range.</summary>
        public void ContinueAt(Player.PlayerController hero, int checkpointIndex)
        {
            var point = atlas != null ? atlas.CheckpointAt(checkpointIndex).position : Vector2.zero;
            EnterAt(hero, point);
        }

        /// <summary>Loads the regions around a world point and puts the hero there.</summary>
        public void EnterAt(Player.PlayerController hero, Vector2 point)
        {
            if (atlas == null)
            {
                Debug.LogError("WorldStreamer has no WorldAtlas; set it to Assets/Data/World.asset.", this);
                return;
            }
            if (Busy)
            {
                Debug.LogWarning("WorldStreamer.EnterAt ignored: a region change is already in progress.", this);
                return;
            }
            StartCoroutine(EnterRoutine(hero, point));
        }

        /// <summary>Unloads every region, for a return to the title.</summary>
        public void Unload()
        {
            StopAllCoroutines();
            Busy = false;
            foreach (var key in new List<string>(loaded.Keys))
            {
                var scene = SceneManager.GetSceneByName(SceneName(key));
                if (scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
            loaded.Clear();
            loading.Clear();
            Hero = null;
            Current = null;
            CurrentRegion = "";
            currentIndex = -1;
        }

        public bool IsLoaded(string key) => loaded.ContainsKey(key);

        private IEnumerator EnterRoutine(Player.PlayerController hero, Vector2 point)
        {
            Busy = true;
            Hero = hero;
            hero.BeginTravel();
            var index = atlas.IndexAt(point.y);
            yield return LoadAround(index);
            SetCurrent(index);
            hero.PlaceAt(point);
            CameraRig.GameCamera.Main?.Snap();
            hero.EndTravel();
            Busy = false;
        }

        private IEnumerator Shift(int index)
        {
            Busy = true;
            yield return LoadAround(index);
            SetCurrent(index);
            Busy = false;
        }

        /// <summary>Loads the band at `index` and its neighbours, then unloads the others.</summary>
        private IEnumerator LoadAround(int index)
        {
            var wanted = new HashSet<string>();
            for (var i = Mathf.Max(0, index - 1); i <= Mathf.Min(atlas.regions.Length - 1, index + 1); i++)
            {
                wanted.Add(atlas.regions[i].key);
            }
            // The current band first, so the hero has ground under him at once.
            var order = new List<string>();
            if (index >= 0 && index < atlas.regions.Length)
            {
                order.Add(atlas.regions[index].key);
            }
            foreach (var key in wanted)
            {
                if (!order.Contains(key))
                {
                    order.Add(key);
                }
            }
            foreach (var key in order)
            {
                if (loaded.ContainsKey(key) || loading.Contains(key))
                {
                    continue;
                }
                var sceneName = SceneName(key);
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogError($"Scene {sceneName} is not in the build's scene list.", this);
                    continue;
                }
                loading.Add(key);
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                loading.Remove(key);
                var root = FindRoot(key);
                if (root != null)
                {
                    loaded[key] = root;
                    root.SetBackdropVisible(false);
                }
            }
            foreach (var key in new List<string>(loaded.Keys))
            {
                if (wanted.Contains(key))
                {
                    continue;
                }
                loaded.Remove(key);
                var scene = SceneManager.GetSceneByName(SceneName(key));
                if (scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        private void SetCurrent(int index)
        {
            if (index < 0 || index >= atlas.regions.Length)
            {
                return;
            }
            var key = atlas.regions[index].key;
            loaded.TryGetValue(key, out var root);
            var hadCurrent = currentIndex >= 0;
            var changed = currentIndex != index;
            currentIndex = index;
            CurrentRegion = key;
            Current = root;
            foreach (var pair in loaded)
            {
                pair.Value.SetBackdropVisible(pair.Key == key);
            }
            if (root != null)
            {
                if (!hadCurrent)
                {
                    globalLight.color = root.Ambient;
                    ambientT = AmbientFade;
                }
                else if (changed)
                {
                    ambientFrom = globalLight.color;
                    ambientTo = root.Ambient;
                    ambientT = 0f;
                }
            }
            if (changed)
            {
                RegionChanged?.Invoke(root);
            }
        }

        private static RegionRoot FindRoot(string key)
        {
            var scene = SceneManager.GetSceneByName(SceneName(key));
            if (!scene.isLoaded)
            {
                return null;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<RegionRoot>(out var region))
                {
                    return region;
                }
            }
            Debug.LogError($"Scene {SceneName(key)} has no RegionRoot.");
            return null;
        }
    }
}
