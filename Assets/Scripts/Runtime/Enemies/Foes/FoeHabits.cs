using System;
using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The leap: with the hero ahead or behind between `nearest` and `farthest`, and no
    /// more than `above` over or `below` under its middle, it faces him and warns for
    /// `windup`, springs at him at `spring` (along and up), hurting him once if he is in
    /// front of it on the way, and recovers where it lands. Long and high it is a pounce;
    /// short, low and fast it is a lunge. A hit that holds the foe calls it off.
    /// </summary>
    [Serializable]
    public sealed class Pounce : FoeBehaviour
    {
        [Tooltip("How near and how far to either side his middle may be from its own, level pixels.")]
        public float nearest = 36f;
        public float farthest = 96f;

        [Tooltip("How far above and below its middle his may be, level pixels.")]
        public float above = 48f;
        public float below = 8f;
        public float windup = 0.4f;

        [Tooltip("The speed it leaves the ground at, along and up, level pixels a second.")]
        public Vector2 spring = new Vector2(150f, 190f);

        [Tooltip("The longest it stays in the air, seconds.")]
        public float airTime = 1f;
        public float recover = 0.4f;
        public float cooldown = 1.6f;

        [Tooltip("The box in front of its body that hurts while it is in the air, level pixels.")]
        public Vector2 reach = new Vector2(22f, 20f);
        public int damage = 1;

        private int stage;
        private float left;
        private float wait;
        private float aloft;
        private bool landed;

        public bool IsWindingUp => stage == 1;
        public bool IsLeaping => stage == 2;
        public override bool IsBusy => stage != 0;

        public override bool Step(Foe foe, float dt)
        {
            if (stage == 0)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || !foe.IsGrounded || !foe.HeroOffset(out var at))
                {
                    return false;
                }
                var along = Mathf.Abs(at.x);
                if (along < nearest || along > farthest || at.y > above || at.y < -below)
                {
                    return false;
                }
                foe.Face(at.x >= 0f ? 1 : -1);
                stage = 1;
                left = windup;
                foe.Telegraph(true);
                return true;
            }
            if (stage == 2)
            {
                aloft += dt;
                if (!landed)
                {
                    landed = foe.HurtAhead(reach, damage);
                }
                // The first steps are still at the ground it left.
                if ((foe.IsGrounded && aloft > 0.1f) || aloft >= airTime)
                {
                    stage = 3;
                    left = recover;
                    return true;
                }
                foe.Move = foe.Facing * spring.x;
                return true;
            }
            left -= dt;
            if (left > 0f)
            {
                return true;
            }
            if (stage == 1)
            {
                foe.Telegraph(false);
                stage = 2;
                aloft = 0f;
                landed = false;
                foe.Spring(spring.y);
                foe.Move = foe.Facing * spring.x;
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
    /// Keeps its distance: with the hero within `within` to either side and near its
    /// height, it turns its back and walks away at the body's pace, standing where a
    /// wall or an edge stops it. Put after a ranged attack that does not fire that near.
    /// </summary>
    [Serializable]
    public sealed class Retreat : FoeBehaviour
    {
        [Tooltip("How near to either side his middle must be to its own for it to back away, level pixels.")]
        public float within = 48f;
        public float heightTolerance = 48f;

        public bool IsRetreating { get; private set; }

        public override bool Step(Foe foe, float dt)
        {
            IsRetreating = false;
            if (!foe.HeroOffset(out var at))
            {
                return false;
            }
            if (Mathf.Abs(at.x) > within || Mathf.Abs(at.y) > heightTolerance)
            {
                return false;
            }
            IsRetreating = true;
            foe.Face(at.x >= 0f ? -1 : 1);
            foe.Move = foe.IsBlockedAhead ? 0f : foe.Facing * foe.Body.speed;
            return true;
        }
    }

    /// <summary>
    /// The ground attack: standing, with the hero ahead or behind between `nearest` and
    /// `farthest` and near its height, it faces him and warns for `windup`, strikes the
    /// floor and sends a wave (<see cref="EnemyShot"/>) along it the way it faces, and
    /// recovers. The wave runs until a wall, the end of the floor or its time stops it,
    /// and is jumped. A hit that holds the foe calls it off.
    /// </summary>
    [Serializable]
    public sealed class Quake : FoeBehaviour
    {
        [Tooltip("How near and how far to either side his middle may be from its own, level pixels.")]
        public float nearest = 30f;
        public float farthest = 110f;
        public float heightTolerance = 24f;
        public float windup = 0.7f;
        public float recover = 0.9f;
        public float cooldown = 2.5f;

        [Tooltip("A taller, faster wave that runs further.")]
        public bool big;
        public int damage = 1;

        private int stage;
        private float left;
        private float wait;

        /// <summary>How many waves it has sent.</summary>
        public int Waves { get; private set; }

        /// <summary>The last wave it sent; null once that is gone.</summary>
        public EnemyShot Last { get; private set; }

        public bool IsWindingUp => stage == 1;
        public override bool IsBusy => stage != 0;

        public override bool Step(Foe foe, float dt)
        {
            if (stage == 0)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || !foe.IsGrounded || !foe.HeroOffset(out var at))
                {
                    return false;
                }
                var along = Mathf.Abs(at.x);
                if (along < nearest || along > farthest || Mathf.Abs(at.y) > heightTolerance)
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
            if (left > 0f)
            {
                return true;
            }
            if (stage == 1)
            {
                foe.Telegraph(false);
                Send(foe);
                stage = 2;
                left = recover;
            }
            else
            {
                stage = 0;
                wait = cooldown;
            }
            return true;
        }

        private void Send(Foe foe)
        {
            var bounds = foe.Box.bounds;
            var ahead = bounds.extents.x + 6f / EnemyPhysics.Ppu;
            var feet = new Vector2(bounds.center.x + foe.Facing * ahead, bounds.min.y);
            Last = EnemyShot.Spawn(EnemyShot.Kind.Wave, feet, new Vector2(foe.Facing, 0f), damage, big);
            Waves++;
            Feedback.Shake(0.35f);
            Audio.Sfx.Play("land", -2f);
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
}
