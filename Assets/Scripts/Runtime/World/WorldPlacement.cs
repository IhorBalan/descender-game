using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// Spawns an enemy or pickup prefab when the region loads, unless its flag
    /// says it is gone for good (a boss, a relic) or it was killed since the
    /// current checkpoint. The id is "region/name" as in scripts/world/
    /// world_placement.gd; the flag, when set, is the id too.
    /// </summary>
    public sealed class WorldPlacement : MonoBehaviour
    {
        [SerializeField]
        private string id = "";

        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private bool flipped;

        [SerializeField]
        private bool onceOnly;

        private GameObject spawned;

        public string Id => id;
        public GameObject Prefab => prefab;

        /// <summary>The flag that marks a once-only placement as taken.</summary>
        public string Flag => "placed:" + id;

        private void Start()
        {
            Spawn();
        }

        public void Spawn()
        {
            if (prefab == null || spawned != null)
            {
                return;
            }
            if (onceOnly && Story.StoryFlags.Has(Flag))
            {
                return;
            }
            spawned = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            if (flipped)
            {
                var scale = spawned.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                spawned.transform.localScale = scale;
            }
        }

        /// <summary>Called by the spawned thing when it is taken or killed for good.</summary>
        public void MarkTaken()
        {
            if (onceOnly)
            {
                Story.StoryFlags.Set(Flag);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = prefab == null ? Color.red : new Color(0.6f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.75f);
        }
    }
}
