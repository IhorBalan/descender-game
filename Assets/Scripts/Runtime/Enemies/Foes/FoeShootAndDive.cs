using System;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The ranged attack: with the hero's middle within `range` of its own (and no nearer
    /// than `nearest`) it faces him,
    /// warns for `windup`, throws one bolt (<see cref="EnemyShot"/>) and recovers, then
    /// waits `cooldown`. The bolt is aimed at where he is when it leaves, or with `level`
    /// set flies flat the way the foe faces; it breaks on the world. It has the step from
    /// the warning to the end of the recovery. A hit that holds the foe calls it off.
    /// </summary>
    [Serializable]
    public sealed class Shoot : FoeBehaviour
    {
        [Tooltip("How far his middle may be from its own, level pixels, for it to shoot.")]
        public float range = 140f;

        [Tooltip("Closer than this, level pixels, it does not shoot: he is too near.")]
        public float nearest;

        [Tooltip("Flies flat the way the foe faces instead of at him.")]
        public bool level;
        public float windup = 0.5f;
        public float recover = 0.3f;
        public float cooldown = 1.4f;

        [Tooltip("The bolt's speed, level pixels a second.")]
        public float speed = 110f;

        [Tooltip("How far the bolt flies, level pixels.")]
        public float reach = 200f;
        public int damage = 1;

        private int stage;
        private float left;
        private float wait;

        /// <summary>How many bolts it has thrown.</summary>
        public int Shots { get; private set; }

        /// <summary>The last bolt it threw; null once that is gone.</summary>
        public EnemyShot Last { get; private set; }

        public bool IsAttacking => stage != 0;
        public override bool IsBusy => stage != 0;

        public override bool Step(Foe foe, float dt)
        {
            if (stage == 0)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || !foe.HeroOffset(out var at) || at.magnitude > range)
                {
                    return false;
                }
                if (at.magnitude < nearest)
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
                Fire(foe);
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

        private void Fire(Foe foe)
        {
            var bounds = foe.Box.bounds;
            var ahead = bounds.extents.x + 6f / EnemyPhysics.Ppu;
            var hand = new Vector2(bounds.center.x + foe.Facing * ahead, bounds.center.y);
            var aim = new Vector2(foe.Facing, 0f);
            if (!level && foe.HeroOffset(out var at))
            {
                // From the hand, not the middle of the body, to his middle.
                aim = at / EnemyPhysics.Ppu + (Vector2)bounds.center - hand;
            }
            Last = EnemyShot.Spawn(EnemyShot.Kind.Bolt, hand, aim, damage);
            Last.Tune(speed, reach);
            Shots++;
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
    /// A flying body's patrol: drifts back and forth across `span` about where it was
    /// put, at the body's pace, turning at walls, and comes back to its height when
    /// something has moved it.
    /// </summary>
    [Serializable]
    public sealed class Hover : FoeBehaviour
    {
        [Tooltip("How wide it drifts, level pixels, centred on where it was put.")]
        public float span = 64f;

        private bool homed;
        private Vector2 home;

        public override bool Step(Foe foe, float dt)
        {
            Vector2 at = foe.transform.position;
            if (!homed)
            {
                homed = true;
                home = at;
            }
            var off = (at.x - home.x) * EnemyPhysics.Ppu;
            if (Mathf.Abs(off) >= span / 2f)
            {
                foe.Face(off > 0f ? -1 : 1);
            }
            else if (EnemyPhysics.IsOnWall(foe.Box, foe.Facing, foe.Hits))
            {
                foe.Turn();
            }
            var pace = foe.Body.speed;
            foe.Move = span > 0f ? foe.Facing * pace : 0f;
            foe.Lift = Mathf.Clamp((home.y - at.y) * EnemyPhysics.Ppu / dt, -pace, pace);
            return true;
        }
    }

    /// <summary>
    /// A flier's attack: with the hero below it, within `range` to either side and no
    /// more than `depth` down, it stops and warns for `windup`, dives at where he was
    /// when the warning ended, rests for `recover` where the dive ends (at the ground,
    /// a wall, or the end of its reach), and climbs back to where it began before it
    /// waits `cooldown`. Its touch is what hurts. A hit that holds it sends it back up.
    /// </summary>
    [Serializable]
    public sealed class Dive : FoeBehaviour
    {
        [Tooltip("How far to either side of it he may be, level pixels.")]
        public float range = 40f;

        [Tooltip("How far below it he may be, level pixels.")]
        public float depth = 140f;
        public float windup = 0.5f;
        public float speed = 220f;
        public float recover = 0.3f;
        public float riseSpeed = 70f;
        public float cooldown = 1.2f;

        private int stage;
        private float left;
        private float wait;
        private float togo;
        private Vector2 way;
        private Vector2 home;

        public bool IsWindingUp => stage == 1;
        public bool IsDiving => stage == 2;
        public bool IsAttacking => stage != 0;
        public override bool IsBusy => stage != 0;

        public override bool Step(Foe foe, float dt)
        {
            Vector2 here = foe.transform.position;
            if (stage == 0)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || !foe.HeroOffset(out var at))
                {
                    return false;
                }
                if (Mathf.Abs(at.x) > range || at.y >= 0f || -at.y > depth)
                {
                    return false;
                }
                foe.Face(at.x >= 0f ? 1 : -1);
                home = here;
                stage = 1;
                left = windup;
                foe.Telegraph(true);
                return true;
            }
            if (stage == 1)
            {
                left -= dt;
                if (left <= 0f)
                {
                    foe.Telegraph(false);
                    var aim = foe.HeroOffset(out var at) ? at : new Vector2(0f, -depth);
                    way = aim.sqrMagnitude > 0f ? aim.normalized : Vector2.down;
                    togo = Mathf.Min(aim.magnitude, depth + range);
                    stage = 2;
                }
                return true;
            }
            if (stage == 2)
            {
                var stopped = foe.IsGrounded || EnemyPhysics.IsOnWall(foe.Box, way.x >= 0f ? 1 : -1, foe.Hits);
                togo -= speed * dt;
                if (togo <= 0f || stopped)
                {
                    stage = 3;
                    left = recover;
                    return true;
                }
                foe.Move = way.x * speed;
                foe.Lift = way.y * speed;
                return true;
            }
            if (stage == 3)
            {
                left -= dt;
                if (left <= 0f)
                {
                    stage = 4;
                }
                return true;
            }
            var back = (home - here) * EnemyPhysics.Ppu;
            if (back.magnitude <= riseSpeed * dt)
            {
                foe.Move = back.x / dt;
                foe.Lift = back.y / dt;
                stage = 0;
                wait = cooldown;
                return true;
            }
            foe.Move = back.normalized.x * riseSpeed;
            foe.Lift = back.normalized.y * riseSpeed;
            return true;
        }

        public override void OnStruck(Foe foe, bool held)
        {
            if (held && stage != 0)
            {
                stage = 4;
            }
        }
    }
}
