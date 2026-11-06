using Descender.Combat;
using Descender.Player;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// What the things lying in the world share: a trigger box on the Pickup layer
    /// that only the hero's body overlaps, and the question of whether what touched
    /// it is a live hero.
    /// </summary>
    public static class Pickup
    {
        private const float Ppu = PlayerTuning.PixelsPerUnit;

        /// <summary>Puts a trigger box of <paramref name="sizePixels"/> on the object, its centre <paramref name="offsetPixels"/> up from the origin.</summary>
        public static BoxCollider2D Box(GameObject target, Vector2 sizePixels, Vector2 offsetPixels)
        {
            target.layer = Layers.Pickup;
            var box = target.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = target.AddComponent<BoxCollider2D>();
            }
            box.isTrigger = true;
            box.size = sizePixels / Ppu;
            box.offset = offsetPixels / Ppu;
            return box;
        }

        /// <summary>A trigger disc of <paramref name="radiusPixels"/> about the origin.</summary>
        public static CircleCollider2D Disc(GameObject target, float radiusPixels)
        {
            target.layer = Layers.Pickup;
            var disc = target.GetComponent<CircleCollider2D>();
            if (disc == null)
            {
                disc = target.AddComponent<CircleCollider2D>();
            }
            disc.isTrigger = true;
            disc.radius = radiusPixels / Ppu;
            return disc;
        }

        /// <summary>The live hero whose body this collider is, or null for anything else (a dead hero too).</summary>
        public static PlayerController HeroFrom(Collider2D other)
        {
            if (other == null || other.attachedRigidbody == null)
            {
                return null;
            }
            if (!other.attachedRigidbody.TryGetComponent<PlayerController>(out var hero) || !hero.IsAlive)
            {
                return null;
            }
            return hero;
        }

        /// <summary>A few pixels of bob about <paramref name="restPixels"/>.</summary>
        public static float Bob(float time, float speed, float heightPixels, float restPixels = 0f) =>
            (restPixels + Mathf.Sin(time * speed) * heightPixels) / Ppu;
    }
}
