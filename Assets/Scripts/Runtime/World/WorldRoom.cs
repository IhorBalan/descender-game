using System.Collections.Generic;
using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A room of the world: a rectangle the camera is clamped to while the hero is
    /// inside, that the map page draws once he has stood in it, that a boss fight
    /// seals and that the doors join (port of scripts/world/world_room.gd). Its
    /// position is its top-left corner and `size` its extent, in units; it makes its
    /// own trigger BoxCollider2D on the Trigger layer when the scene loads. A joined room
    /// is part of one continuous land with the joined rooms it touches (Paradise's zones):
    /// the camera keeps to them all as one, so it never stops at a shared edge and jumps
    /// across it as the hero walks from one to the next.
    /// </summary>
    public sealed class WorldRoom : MonoBehaviour
    {
        private static readonly List<WorldRoom> Loaded = new List<WorldRoom>();
        private static readonly List<Rect> Joinable = new List<Rect>();
        private static readonly List<bool> Taken = new List<bool>();

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

        [Tooltip(
            "One continuous land with the joined rooms it touches: the camera keeps to them all, never stopping at a shared edge."
        )]
        [SerializeField]
        private bool joined;

        private BoxCollider2D box;

        public int Id => id;
        public string Kind => kind;
        public int Area => area;
        public int Depth => depth;
        public string RoomName => roomName;
        public string Ability => ability;
        public Vector2 Size => size;
        public bool Joined => joined;

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

        /// <summary>
        /// What the camera keeps to in this room: its bounds, or for a joined room the bounds
        /// of every loaded joined room it touches, or touches through one that does.
        /// </summary>
        public Rect CameraBounds()
        {
            if (!joined)
            {
                return Bounds;
            }
            Joinable.Clear();
            foreach (var other in Loaded)
            {
                if (other != this && other != null && other.joined)
                {
                    Joinable.Add(other.Bounds);
                }
            }
            return JoinedBounds(Bounds, Joinable);
        }

        /// <summary>
        /// `start` grown by every rect of `others` it touches, edge to edge or overlapping,
        /// and by those touching what it has grown to: one continuous land as one rect.
        /// </summary>
        public static Rect JoinedBounds(Rect start, IReadOnlyList<Rect> others)
        {
            var result = start;
            // Reused, as the camera asks every frame: no garbage.
            Taken.Clear();
            for (var i = 0; i < others.Count; i++)
            {
                Taken.Add(false);
            }
            var taken = Taken;
            var grew = true;
            while (grew)
            {
                grew = false;
                for (var i = 0; i < others.Count; i++)
                {
                    if (!taken[i] && Touch(result, others[i]))
                    {
                        taken[i] = true;
                        result = Rect.MinMaxRect(
                            Mathf.Min(result.xMin, others[i].xMin),
                            Mathf.Min(result.yMin, others[i].yMin),
                            Mathf.Max(result.xMax, others[i].xMax),
                            Mathf.Max(result.yMax, others[i].yMax)
                        );
                        grew = true;
                    }
                }
            }
            return result;
        }

        private static bool Touch(Rect a, Rect b)
        {
            const float slack = 0.01f;
            return a.xMin <= b.xMax + slack
                && b.xMin <= a.xMax + slack
                && a.yMin <= b.yMax + slack
                && b.yMin <= a.yMax + slack;
        }

        private void OnEnable()
        {
            Loaded.Add(this);
        }

        private void OnDisable()
        {
            Loaded.Remove(this);
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
                camera.FrameRoom(this);
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
