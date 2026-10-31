using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A room of the world: a rectangle the camera is clamped to while the hero is
    /// inside, that the map page draws once he has stood in it, that a boss fight
    /// seals and that the doors join (port of scripts/world/world_room.gd). Its
    /// position is its top-left corner and `size` its extent, in units; it makes its
    /// own trigger BoxCollider2D on the Trigger layer when the scene loads.
    /// </summary>
    public sealed class WorldRoom : MonoBehaviour
    {
        [SerializeField]
        private int id;

        [Tooltip("normal, boss, save, secret, start, shop, gate, hub, ability: as the map tool kinds them.")]
        [SerializeField]
        private string kind = "normal";

        [SerializeField]
        private int area;

        [SerializeField]
        private int depth;

        [SerializeField]
        private string roomName = "";

        [Tooltip("The ability the room holds, for an ability room.")]
        [SerializeField]
        private string ability = "";

        [Tooltip("The room's extent in units from its top-left corner (30 x 17 tiles by default).")]
        [SerializeField]
        private Vector2 size = new Vector2(30f, 17f);

        private BoxCollider2D box;

        public int Id => id;
        public string Kind => kind;
        public int Area => area;
        public int Depth => depth;
        public string RoomName => roomName;
        public string Ability => ability;
        public Vector2 Size => size;

        /// <summary>The room's bounds in world units.</summary>
        public Rect Bounds
        {
            get
            {
                var corner = (Vector2)transform.position;
                return new Rect(corner.x, corner.y - size.y, size.x, size.y);
            }
        }

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Trigger");
            box = GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider2D>();
            }
            box.isTrigger = true;
            box.size = size;
            box.offset = new Vector2(size.x / 2f, -size.y / 2f);
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

        private void OnDrawGizmos()
        {
            Gizmos.color = kind == "boss" ? new Color(1f, 0.5f, 0.3f, 0.6f) : new Color(1f, 1f, 1f, 0.25f);
            var b = Bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
