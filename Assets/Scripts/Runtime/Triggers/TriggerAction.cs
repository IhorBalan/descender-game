using System;
using Descender.Player;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>On, off, or the other of what it is now: for doors, movers and objects.</summary>
    public enum SwitchTo
    {
        On,
        Off,
        Toggle,
    }

    /// <summary>What an action is told: the zone that fired and the hero who fired it (null when nobody did).</summary>
    public readonly struct TriggerContext
    {
        public readonly TriggerZone Zone;
        public readonly PlayerController Hero;

        public TriggerContext(TriggerZone zone, PlayerController hero)
        {
            Zone = zone;
            Hero = hero;
        }

        /// <summary>Where the zone is, or the origin without one.</summary>
        public Vector2 Position => Zone != null ? (Vector2)Zone.transform.position : Vector2.zero;
    }

    /// <summary>
    /// One thing a <see cref="TriggerZone"/> does when it fires. A new kind of action is
    /// one more serializable class in TriggerActions.cs; the zone's inspector lists it by
    /// its class name.
    /// </summary>
    [Serializable]
    public abstract class TriggerAction
    {
        public abstract void Run(TriggerContext context);
    }
}
