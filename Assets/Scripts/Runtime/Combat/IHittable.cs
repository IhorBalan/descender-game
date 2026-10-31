using UnityEngine;

namespace Descender.Combat
{
    /// <summary>
    /// Something the hero's sword, slam or Holy Light can hurt: every enemy. The
    /// hitter finds it with TryGetComponent on the collider's attached body, so the
    /// component sits on the enemy's root beside its Rigidbody2D.
    /// </summary>
    public interface IHittable
    {
        /// <summary>Takes damage from a position; returns false when the hit did nothing (already dead).</summary>
        bool Hit(int damage, Vector2 from);
    }
}
