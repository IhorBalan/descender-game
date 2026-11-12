using System;
using Descender.Combat;
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

        /// <summary>
        /// The foe took a strike and lived. `held` is true when the hit holds it (it is not
        /// armoured): an attack in progress is called off.
        /// </summary>
        public virtual void OnStruck(Foe foe, bool held) { }

        /// <summary>True when this stops a strike before it does anything: the foe takes nothing and is not held.</summary>
        public virtual bool Blocks(Foe foe, Strike strike) => false;

        /// <summary>A line for the foe's label, for what the player has to see; empty for none.</summary>
        public virtual string Mark(Foe foe) => "";

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

    /// <summary>
    /// The melee attack: with the hero close ahead or behind and near its own height, it
    /// faces him and warns for `windup`, strikes a box in front of it for `strike` (one
    /// hit at most), recovers, and waits `cooldown` before the next. It has the step
    /// from the warning to the end of the recovery. A hit that holds the foe calls it off.
    /// </summary>
    [Serializable]
    public sealed class Swing : FoeBehaviour
    {
        [Tooltip("How far from the edge of its body, level pixels, the hero's middle may be for a swing to start.")]
        public float range = 30f;

        [Tooltip("How far above or below its middle his may be, level pixels.")]
        public float heightTolerance = 24f;

        [Tooltip("The box the strike hurts in, placed in front of its body, level pixels.")]
        public Vector2 reach = new Vector2(22f, 20f);
        public float windup = 0.35f;
        public float strike = 0.15f;
        public float recover = 0.45f;
        public float cooldown = 0.8f;
        public int damage = 1;

        private int stage;
        private float left;
        private float wait;
        private bool landed;

        public bool IsWindingUp => stage == 1;
        public bool IsStriking => stage == 2;
        public bool IsAttacking => stage != 0;

        public override bool Step(Foe foe, float dt)
        {
            if (stage == 0)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || !foe.HeroOffset(out var at))
                {
                    return false;
                }
                var near = Mathf.Abs(at.x) <= range + foe.Body.size.x / 2f && Mathf.Abs(at.y) <= heightTolerance;
                if (!near)
                {
                    return false;
                }
                foe.Face(at.x >= 0f ? 1 : -1);
                stage = 1;
                left = windup;
                foe.Telegraph(true);
                return true;
            }
            left -= dt;
            if (stage == 2 && !landed)
            {
                landed = foe.HurtAhead(reach, damage);
            }
            if (left > 0f)
            {
                return true;
            }
            if (stage == 1)
            {
                foe.Telegraph(false);
                stage = 2;
                left = strike;
                landed = false;
            }
            else if (stage == 2)
            {
                stage = 3;
                left = recover;
            }
            else
            {
                stage = 0;
                wait = cooldown;
            }
            return true;
        }

        public override void OnStruck(Foe foe, bool held)
        {
            if (held && stage != 0)
            {
                stage = 0;
                wait = cooldown;
            }
        }
    }

    /// <summary>
    /// Comes for the hero while it sees him (within `sight` along the ground and
    /// `heightTolerance` up or down) and for `giveUp` seconds after it last did, at
    /// `pace` times its walk, stopping short of him and at walls and edges.
    /// </summary>
    [Serializable]
    public sealed class Chase : FoeBehaviour
    {
        public float sight = 120f;
        public float heightTolerance = 40f;
        public float giveUp = 1.4f;
        public float pace = 1.8f;

        [Tooltip("How close to the edge of its body, level pixels, it comes before it stops.")]
        public float stopAt = 12f;

        private float left;

        public bool IsChasing => left > 0f;

        public override bool Step(Foe foe, float dt)
        {
            if (!foe.HeroOffset(out var at))
            {
                left = 0f;
                return false;
            }
            var sees = Mathf.Abs(at.x) <= sight && Mathf.Abs(at.y) <= heightTolerance;
            left = sees ? giveUp : Mathf.Max(left - dt, 0f);
            if (left <= 0f)
            {
                return false;
            }
            foe.Face(at.x >= 0f ? 1 : -1);
            var close = Mathf.Abs(at.x) <= stopAt + foe.Body.size.x / 2f;
            foe.Move = close || foe.IsBlockedAhead ? 0f : foe.Facing * foe.Body.speed * pace;
            return true;
        }
    }

    /// <summary>
    /// Stands still until the hero's middle is within `range` of its own, warns for
    /// `mark`, and from then on lets the behaviours after it act. A hit wakes it at once.
    /// </summary>
    [Serializable]
    public sealed class Ambush : FoeBehaviour
    {
        public float range = 40f;
        public float mark = 0.6f;

        private bool marking;
        private float left;

        public bool IsAwake { get; private set; }

        public override bool Step(Foe foe, float dt)
        {
            if (IsAwake)
            {
                return false;
            }
            if (!marking)
            {
                if (foe.HeroOffset(out var at) && at.magnitude <= range)
                {
                    marking = true;
                    left = mark;
                    foe.Face(at.x >= 0f ? 1 : -1);
                    foe.Telegraph(true);
                }
                return true;
            }
            left -= dt;
            if (left > 0f)
            {
                return true;
            }
            IsAwake = true;
            foe.Telegraph(false);
            return false;
        }

        public override void OnStruck(Foe foe, bool held)
        {
            if (!IsAwake && marking)
            {
                foe.Telegraph(false);
            }
            IsAwake = true;
        }
    }
}
