using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Descender.World
{
    /// <summary>
    /// A wide region authored in Unity, cut into zone scenes along X (Paradise: seven
    /// zones over 7,500 tiles, docs/design/paradise.md). Sits on the region's root.
    /// The zones within <see cref="margin"/> of the camera are kept loaded and the rest
    /// unloaded, one scene operation at a time, so memory holds two or three zones
    /// whatever the region's width. Entering the world waits for <see cref="Prepare"/>,
    /// which loads the zones around the arrival point first, so the hero always lands
    /// on ground. Zone scenes keep their content in world coordinates.
    /// </summary>
    public sealed class RegionZones : MonoBehaviour
    {
        [System.Serializable]
        public struct Zone
        {
            [Tooltip("The zone's scene, in the build's scene list.")]
            public string scene;

            [Tooltip("The zone's left edge in world units.")]
            public float xMin;

            [Tooltip("The zone's right edge in world units.")]
            public float xMax;
        }

        [SerializeField]
        private List<Zone> zones = new List<Zone>();

        [Tooltip("How far beyond the view's edges a zone is loaded, in world units.")]
        [SerializeField, Min(0f)]
        private float margin = 60f;

        private readonly HashSet<string> loaded = new HashSet<string>();

        // Zones whose scene is not in the build: reported once, then never asked for again.
        private readonly HashSet<string> missing = new HashSet<string>();
        private bool working;

        public IReadOnlyList<Zone> Zones => zones;
        public float Margin => margin;

        /// <summary>Whether a zone's scene is in memory.</summary>
        public bool IsLoaded(string scene) => loaded.Contains(scene);

        /// <summary>The zones wanted for a view centred at x: those within the margin.</summary>
        public static List<string> Wanted(IReadOnlyList<Zone> zones, float x, float halfWidth, float margin)
        {
            var wanted = new List<string>();
            var left = x - halfWidth - margin;
            var right = x + halfWidth + margin;
            foreach (var zone in zones)
            {
                if (zone.xMax >= left && zone.xMin <= right)
                {
                    wanted.Add(zone.scene);
                }
            }
            return wanted;
        }

        /// <summary>Loads the zones around a point before the hero is put there.</summary>
        public IEnumerator Prepare(Vector2 point)
        {
            while (working)
            {
                yield return null;
            }
            working = true;
            foreach (var scene in Wanted(zones, point.x, HalfView(), margin))
            {
                yield return Load(scene);
            }
            working = false;
        }

        private void Update()
        {
            if (working)
            {
                return;
            }
            var camera = CameraRig.GameCamera.Main;
            if (camera == null)
            {
                return;
            }
            var wanted = Wanted(zones, camera.transform.position.x, HalfView(), margin);
            foreach (var scene in wanted)
            {
                if (!loaded.Contains(scene) && !missing.Contains(scene))
                {
                    StartCoroutine(Step(Load(scene)));
                    return;
                }
            }
            foreach (var scene in new List<string>(loaded))
            {
                if (!wanted.Contains(scene))
                {
                    StartCoroutine(Step(Unload(scene)));
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            // The region is going: its zones go with it.
            foreach (var scene in loaded)
            {
                var s = SceneManager.GetSceneByName(scene);
                if (s.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(s);
                }
            }
            loaded.Clear();
        }

        private IEnumerator Step(IEnumerator operation)
        {
            working = true;
            yield return operation;
            working = false;
        }

        private IEnumerator Load(string scene)
        {
            if (loaded.Contains(scene) || SceneManager.GetSceneByName(scene).isLoaded)
            {
                loaded.Add(scene);
                yield break;
            }
            if (missing.Contains(scene))
            {
                yield break;
            }
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"Zone scene {scene} is not in the build's scene list.", this);
                missing.Add(scene);
                yield break;
            }
            loaded.Add(scene);
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        }

        private IEnumerator Unload(string scene)
        {
            loaded.Remove(scene);
            var s = SceneManager.GetSceneByName(scene);
            if (s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        private static float HalfView()
        {
            var camera = CameraRig.GameCamera.Main;
            return camera != null ? camera.HalfExtents.x : 10f;
        }
    }
}
