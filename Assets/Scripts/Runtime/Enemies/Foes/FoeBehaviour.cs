using System;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// One habit of a <see cref="Foe"/>, with its numbers. Each physics step the foe asks
    /// its behaviours in order; the first whose <see cref="Step"/> returns true has the
    /// step and the rest are not asked. A behaviour moves the foe by setting
    /// <see cref="Foe.Move"/> and turning it. A new habit is one more class.
    /// </summary>
    [Serializable]
    public abstract class FoeBehaviour
    {
        /// <summary>Acts for this step if it wants to; true when it did.</summary>
        public abstract bool Step(Foe foe, float dt);

        /// <summary>A copy for one foe, so behaviours that keep time do not share it.</summary>
        public virtual FoeBehaviour Copy() => (FoeBehaviour)MemberwiseClone();
    }

    /// <summary>Walks its platform at the body's pace and turns at a wall and, unless told not to, at an edge.</summary>
    [Serializable]
    public sealed class Patrol : FoeBehaviour
    {
        [Tooltip("Turns where the floor ends; off, it walks off the edge.")]
        public bool turnsAtEdges = true;

        public override bool Step(Foe foe, float dt)
        {
            if (!foe.IsGrounded)
            {
                return true;
            }
            var wall = EnemyPhysics.IsOnWall(foe.Box, foe.Facing, foe.Hits);
            var edge = turnsAtEdges && !EnemyPhysics.FloorAhead(foe.Box, foe.Facing, foe.Hits);
            if (wall || edge)
            {
                foe.Turn();
            }
            foe.Move = foe.Facing * foe.Body.speed;
            return true;
        }
    }
}
