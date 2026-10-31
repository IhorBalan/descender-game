using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Descender.World
{
    /// <summary>
    /// Loads region scenes additively and moves the hero between them through
    /// doors. Lives in Main.unity. Region scene names are "Region_" + id, and
    /// every one must be in the build's scene list.
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        public static WorldStreamer Instance { get; private set; }

        public const string ScenePrefix = "Region_";

        public string CurrentRegion { get; private set; } = "";
        public RegionRoot Current { get; private set; }
        public bool Busy { get; private set; }

        public event System.Action<RegionRoot> RegionLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static string SceneName(string regionId) => ScenePrefix + regionId;

        /// <summary>Loads a region (unloading the previous one) and puts the hero at a door or a point.</summary>
        public void Enter(Player.PlayerController hero, string regionId, string doorId, Vector2? fallback = null)
        {
            if (!Busy)
            {
                StartCoroutine(EnterRoutine(hero, regionId, doorId, fallback));
            }
        }

        public void Travel(Player.PlayerController hero, WorldDoor door)
        {
            Enter(hero, door.TargetRegion, door.TargetDoor);
        }

        private IEnumerator EnterRoutine(Player.PlayerController hero, string regionId, string doorId, Vector2? fallback)
        {
            Busy = true;
            hero.BeginTravel();
            var previous = CurrentRegion;
            if (previous != regionId)
            {
                yield return SceneManager.LoadSceneAsync(SceneName(regionId), LoadSceneMode.Additive);
                CurrentRegion = regionId;
                Current = FindRoot(regionId);
                if (!string.IsNullOrEmpty(previous))
                {
                    yield return SceneManager.UnloadSceneAsync(SceneName(previous));
                }
            }
            var door = Current != null ? Current.FindDoor(doorId) : null;
            if (door != null)
            {
                hero.PlaceAt(door.SpawnPoint);
            }
            else if (fallback.HasValue)
            {
                hero.PlaceAt(fallback.Value);
            }
            else
            {
                Debug.LogWarning($"Region {regionId} has no door '{doorId}'.");
            }
            CameraRig.GameCamera.Main?.Snap();
            RegionLoaded?.Invoke(Current);
            hero.EndTravel();
            Busy = false;
        }

        private static RegionRoot FindRoot(string regionId)
        {
            var scene = SceneManager.GetSceneByName(SceneName(regionId));
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<RegionRoot>(out var region))
                {
                    return region;
                }
            }
            Debug.LogError($"Scene {SceneName(regionId)} has no RegionRoot.");
            return null;
        }
    }
}
