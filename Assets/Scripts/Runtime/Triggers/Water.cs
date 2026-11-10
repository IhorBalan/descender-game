using System.Collections.Generic;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// Water to swim in: an area with no collider. With the middle of his body in it the
    /// hero sinks slowly, moves and dashes slower, and jump is a stroke upwards he can
    /// repeat; with his head above its top, the surface, jump is a full jump out.
    /// </summary>
    public sealed class Water : MonoBehaviour
    {
        private static readonly List<Water> All = new List<Water>();

        [SerializeField]
        [Tooltip("The area, in units, centred on this object; its top is the surface.")]
        private Vector2 size = new Vector2(8f, 4f);

        public Vector2 Size => size;

        /// <summary>The area in the world.</summary>
        public Rect Area
        {
            get
            {
                Vector2 centre = transform.position;
                return new Rect(centre - size / 2f, size);
            }
        }

        /// <summary>The height of its top in the world.</summary>
        public float Surface => transform.position.y + size.y / 2f;

        public void Configure(Vector2 area)
        {
            size = area;
        }

        /// <summary>The water a point is in; null for none.</summary>
        public static Water At(Vector2 point)
        {
            foreach (var one in All)
            {
                if (one.Area.Contains(point))
                {
                    return one;
                }
            }
            return null;
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }
    }
}
