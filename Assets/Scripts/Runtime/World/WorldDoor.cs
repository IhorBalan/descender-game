using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A passage between regions. A trigger on the Trigger layer; when the hero
    /// enters, the streamer loads the target region and puts the hero at its
    /// door's spawn point, offset into the room. Ids are "region:door" as in the
    /// Godot scenes (scripts/world/world_door.gd).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WorldDoor : MonoBehaviour
    {
        [SerializeField] private string id = "";
        [SerializeField] private string targetRegion = "";
        [SerializeField] private string targetDoor = "";
        [SerializeField] private Vector2 spawnOffset = new Vector2(1.5f, 0f);

        public string Id => id;
        public string TargetRegion => targetRegion;
        public string TargetDoor => targetDoor;

        /// <summary>Where a hero arriving through this door stands.</summary>
        public Vector2 SpawnPoint => (Vector2)transform.position + spawnOffset;

        public string TargetId => $"{targetRegion}:{targetDoor}";

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<Player.PlayerController>(out var hero) || hero.IsTravelling)
            {
                return;
            }
            WorldStreamer.Instance?.Travel(hero, this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.8f);
            Gizmos.DrawSphere(SpawnPoint, 0.25f);
        }
    }
}
