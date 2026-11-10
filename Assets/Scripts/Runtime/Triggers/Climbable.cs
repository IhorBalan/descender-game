using System.Collections.Generic;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// Something to climb: a ladder, a vine, a net. An area with no collider; the hero
    /// takes hold by pressing up or down while his feet are in it, climbs with no gravity,
    /// and lets go by jumping or by reaching an end. Its top is where he steps off, so a
    /// one-way ledge or a floor's edge goes there.
    /// </summary>
    public sealed class Climbable : MonoBehaviour
    {
        private static readonly List<Climbable> All = new List<Climbable>();

        [SerializeField]
        [Tooltip("The area, in units, centred on this object.")]
        private Vector2 size = new Vector2(1f, 6f);

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

        public void Configure(Vector2 area)
        {
            size = area;
        }

        /// <summary>The one a point is in, taking it `slack` units taller at both ends; null for none.</summary>
        public static Climbable At(Vector2 point, float slack)
        {
            foreach (var one in All)
            {
                var area = one.Area;
                var inX = point.x >= area.xMin && point.x <= area.xMax;
                if (inX && point.y >= area.yMin - slack && point.y <= area.yMax + slack)
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
