using UnityEngine;

namespace Descender.Combat
{
    /// <summary>
    /// The collision layers ProjectBootstrap names, looked up once, and the masks the
    /// hero, enemies, shots and pickups query with. Read these instead of a magic int;
    /// the matrix itself (who collides with whom) is <see cref="ApplyCollisionMatrix"/>.
    /// </summary>
    public static class Layers
    {
        public static readonly int World = LayerMask.NameToLayer("World");
        public static readonly int Player = LayerMask.NameToLayer("Player");
        public static readonly int Enemy = LayerMask.NameToLayer("Enemy");
        public static readonly int LightBarrier = LayerMask.NameToLayer("LightBarrier");
        public static readonly int Ledge = LayerMask.NameToLayer("Ledge");
        public static readonly int Hazard = LayerMask.NameToLayer("Hazard");
        public static readonly int Pickup = LayerMask.NameToLayer("Pickup");
        public static readonly int Trigger = LayerMask.NameToLayer("Trigger");

        /// <summary>Solid world only: walls to grip, ends of shots, the rope's ceiling.</summary>
        public static readonly int WorldMask = LayerMask.GetMask("World");

        /// <summary>What feet stand on: the world and the one-way ledges.</summary>
        public static readonly int GroundMask = LayerMask.GetMask("World", "Ledge");

        /// <summary>The world and the walls of angel light, which stop everything but a light dash.</summary>
        public static readonly int SolidMask = LayerMask.GetMask("World", "LightBarrier");

        public static readonly int PlayerMask = LayerMask.GetMask("Player");
        public static readonly int EnemyMask = LayerMask.GetMask("Enemy");
        public static readonly int LightBarrierMask = LayerMask.GetMask("LightBarrier");

        /// <summary>
        /// Who touches whom, as the Godot masks had it: bodies collide with the world (and
        /// enemies and the hero with ledges), the hero alone meets light barriers, hazards,
        /// pickups and triggers, and the hero and the enemies pass through each other (hits
        /// are queries). Idempotent; the bootstrap and the hero's Awake both call it, so a
        /// scene that was never set up in the editor still plays right.
        /// </summary>
        public static void ApplyCollisionMatrix()
        {
            var layers = new[] { World, Player, Enemy, LightBarrier, Ledge, Hazard, Pickup, Trigger };
            foreach (var a in layers)
            {
                foreach (var b in layers)
                {
                    if (a >= 0 && b >= 0)
                    {
                        Physics2D.IgnoreLayerCollision(a, b, true);
                    }
                }
            }
            Touch(World, World);
            Touch(World, Player);
            Touch(World, Enemy);
            Touch(Player, LightBarrier);
            Touch(Player, Ledge);
            Touch(Player, Hazard);
            Touch(Player, Pickup);
            Touch(Player, Trigger);
            Touch(Enemy, Ledge);
        }

        private static void Touch(int a, int b)
        {
            if (a >= 0 && b >= 0)
            {
                Physics2D.IgnoreLayerCollision(a, b, false);
            }
        }

        /// <summary>A filter over a mask that ignores trigger colliders.</summary>
        public static ContactFilter2D Solid(int mask) =>
            new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = mask,
                useTriggers = false,
            };

        /// <summary>A filter over a mask that sees trigger colliders too (hurtboxes).</summary>
        public static ContactFilter2D Any(int mask) =>
            new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = mask,
                useTriggers = true,
            };
    }
}
