using System.Collections.Generic;
using Descender.Combat;
using UnityEngine;

namespace Descender.CameraRig
{
    /// <summary>
    /// An area of a level that sets where the camera sits while the hero stands in it:
    /// a framing offset (look down before a drop), a zoom (an arena), an axis held still
    /// (a corridor) and a look-ahead of its own. The <see cref="GameCamera"/> blends to
    /// the zone's settings over its blend time and back to the room's on leaving. Where
    /// zones overlap, the smallest one the hero is in wins. The area is a rectangle
    /// centred on the object; the zone makes its own trigger on the Trigger layer.
    /// </summary>
    public sealed class CameraZone : MonoBehaviour
    {
        private static readonly List<CameraZone> Inside = new List<CameraZone>();
        private static readonly List<Rect> Areas = new List<Rect>();

        [SerializeField]
        [Tooltip("Width and height in units, centred on this object.")]
        private Vector2 size = new Vector2(10f, 6f);

        [SerializeField]
        [Tooltip("Added to the room's framing, in units: (0, -4) looks down.")]
        private Vector2 framing;

        [SerializeField, Min(0.25f)]
        [Tooltip("1 keeps the room's view; 1.5 shows half as much again.")]
        private float zoom = 1f;

        [SerializeField]
        [Tooltip("Hold the view's x at the zone's middle (plus the framing).")]
        private bool lockX;

        [SerializeField]
        [Tooltip("Hold the view's y at the zone's middle (plus the framing).")]
        private bool lockY;

        [SerializeField]
        private bool overrideLookAhead;

        [SerializeField, Min(0f)]
        private float lookAheadPixels;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds to blend in, and back out on leaving.")]
        private float blendTime = 0.5f;

        private BoxCollider2D box;

        public Vector2 Framing => framing;
        public float Zoom => zoom;
        public bool LockX => lockX;
        public bool LockY => lockY;
        public float BlendTime => blendTime;

        /// <summary>The zone's rectangle in world units.</summary>
        public Rect Area => new Rect((Vector2)transform.position - size / 2f, size);

        /// <summary>The zone the camera should obey now: the smallest one the hero stands in, or null.</summary>
        public static CameraZone Current
        {
            get
            {
                Inside.RemoveAll(zone => zone == null || !zone.isActiveAndEnabled);
                Areas.Clear();
                foreach (var zone in Inside)
                {
                    Areas.Add(zone.Area);
                }
                var index = CameraSettings.Innermost(Areas);
                return index >= 0 ? Inside[index] : null;
            }
        }

        /// <summary>Sets the zone up from code. A negative look-ahead keeps the room's.</summary>
        public void Configure(
            Vector2 area,
            Vector2 offset,
            float zoomBy,
            bool holdX,
            bool holdY,
            float lookAhead = -1f,
            float blend = 0.5f
        )
        {
            size = area;
            framing = offset;
            zoom = Mathf.Max(zoomBy, 0.25f);
            lockX = holdX;
            lockY = holdY;
            overrideLookAhead = lookAhead >= 0f;
            lookAheadPixels = Mathf.Max(lookAhead, 0f);
            blendTime = Mathf.Max(blend, 0f);
            Fit();
        }

        /// <summary>The settings the camera has in this zone, given the room's.</summary>
        public CameraSettings Settings(CameraSettings room)
        {
            var result = room;
            result.framing = room.framing + framing;
            result.zoom = room.zoom * zoom;
            if (overrideLookAhead)
            {
                result.lookAheadPixels = lookAheadPixels;
            }
            result.lockWeight = new Vector2(lockX ? 1f : 0f, lockY ? 1f : 0f);
            result.lockAt = Area.center + framing;
            return result;
        }

        private void Awake()
        {
            Fit();
        }

        private void Fit()
        {
            gameObject.layer = Layers.Trigger;
            if (box == null && !TryGetComponent(out box))
            {
                box = gameObject.AddComponent<BoxCollider2D>();
            }
            box.isTrigger = true;
            box.size = size;
            box.offset = Vector2.zero;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Pickups.Pickup.HeroFrom(other) != null && !Inside.Contains(this))
            {
                Inside.Add(this);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (body != null && body.TryGetComponent<Player.PlayerController>(out _))
            {
                Inside.Remove(this);
            }
        }

        private void OnDisable()
        {
            Inside.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.3f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
