using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// Ground that moves: a platform, a lift. The hero standing on one is carried at
    /// its <see cref="Velocity"/>, and keeps some of it when he leaves.
    /// </summary>
    public interface IMovingGround
    {
        /// <summary>How it moves over the physics step now being taken, in units a second.</summary>
        Vector2 Velocity { get; }
    }
}
