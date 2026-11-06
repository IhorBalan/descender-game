using Descender.Combat;
using Descender.Player;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The queries every enemy body shares, in units: feet on the ground, a wall in
    /// front, floor ahead of the feet (so a walker turns at the edge), and the touch
    /// that hurts the hero. Non-allocating; each enemy passes its own hit buffer.
    /// </summary>
    public static class EnemyPhysics
    {
        public const float Ppu = PlayerTuning.PixelsPerUnit;

        private static readonly ContactFilter2D Ground = Layers.Solid(Layers.GroundMask);
        private static readonly ContactFilter2D Wall = Layers.Solid(Layers.WorldMask);
        private static readonly ContactFilter2D Hero = Layers.Solid(Layers.PlayerMask);
        private static PhysicsMaterial2D slick;

        /// <summary>A frictionless material so a body set by velocity never sticks to walls.</summary>
        public static PhysicsMaterial2D Slick
        {
            get
            {
                if (slick == null)
                {
                    slick = new PhysicsMaterial2D("Slick") { friction = 0f, bounciness = 0f };
                }
                return slick;
            }
        }

        /// <summary>Sets a body up as the hero's is: dynamic, no engine gravity, upright, interpolated.</summary>
        public static void Prepare(Rigidbody2D body, Collider2D collider, bool continuous = true)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = continuous
                ? CollisionDetectionMode2D.Continuous
                : CollisionDetectionMode2D.Discrete;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            collider.sharedMaterial = Slick;
        }

        public static bool IsGrounded(BoxCollider2D box, Collider2D[] hits)
        {
            var bounds = box.bounds;
            var size = new Vector2(Mathf.Max(bounds.size.x - 2f / Ppu, 1f / Ppu), 2f / Ppu);
            return Physics2D.OverlapBox(new Vector2(bounds.center.x, bounds.min.y), size, 0f, Ground, hits) > 0;
        }

        /// <summary>True when solid world touches the side the body faces.</summary>
        public static bool IsOnWall(BoxCollider2D box, int direction, Collider2D[] hits)
        {
            var bounds = box.bounds;
            var size = new Vector2(2f / Ppu, Mathf.Max(bounds.size.y - 4f / Ppu, 1f / Ppu));
            var x = direction > 0 ? bounds.max.x + 0.5f / Ppu : bounds.min.x - 0.5f / Ppu;
            return Physics2D.OverlapBox(new Vector2(x, bounds.center.y), size, 0f, Wall, hits) > 0;
        }

        /// <summary>True when there is floor to walk on just ahead of the feet (the Godot FloorRay).</summary>
        public static bool FloorAhead(BoxCollider2D box, int direction, Collider2D[] hits)
        {
            var bounds = box.bounds;
            var x = bounds.center.x + direction * (bounds.extents.x + 2f / Ppu);
            var centre = new Vector2(x, bounds.min.y - 4f / Ppu);
            return Physics2D.OverlapBox(centre, new Vector2(2f / Ppu, 12f / Ppu), 0f, Ground, hits) > 0;
        }

        /// <summary>Hurts the hero if his body is inside a box (centre and size in units). True when it landed.</summary>
        public static bool HurtHero(Vector2 centre, Vector2 size, int damage, Vector2 from, Collider2D[] hits)
        {
            var count = Physics2D.OverlapBox(centre, size, 0f, Hero, hits);
            for (var i = 0; i < count; i++)
            {
                var body = hits[i].attachedRigidbody;
                if (body != null && body.TryGetComponent<PlayerController>(out var hero))
                {
                    return hero.Hurt(damage, from);
                }
            }
            return false;
        }

        /// <summary>The hero when he is in the scene and alive, else null.</summary>
        public static PlayerController LiveHero()
        {
            var hero = PlayerController.Current;
            return hero != null && hero.IsAlive ? hero : null;
        }
    }
}
