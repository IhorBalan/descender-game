using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A rectangle of a region the camera is clamped to while the hero is inside.
    /// A trigger BoxCollider2D on the Trigger layer; the hero's body entering it
    /// hands the bounds to the camera. Port of scripts/world/world_room.gd.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WorldRoom : MonoBehaviour
    {
        [SerializeField]
        private string id = "";

        private BoxCollider2D box;

        public string Id => id;

        /// <summary>The room's bounds in world units.</summary>
        public Rect Bounds
        {
            get
            {
                var b = box.bounds;
                return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<Player.PlayerController>(out _))
            {
                return;
            }
            var camera = CameraRig.GameCamera.Main;
            if (camera != null)
            {
                camera.Bounds = Bounds;
            }
        }

        private void OnDrawGizmosSelected()
        {
            var b = GetComponent<BoxCollider2D>();
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
        }
    }
}
