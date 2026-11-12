using System;
using Descender.Combat;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The rush: with the hero ahead or behind within `range` and near its height, it
    /// faces him and warns for `windup`, runs `distance` that way at `speed` (stopping at
    /// a wall or an edge), hurting him once if he is in its way, and recovers. A hit that
    /// holds the foe calls it off.
    /// </summary>
    [Serializable]
    public sealed class Charge : FoeBehaviour
    {
        [Tooltip("How far to either side his middle may be from its own, level pixels.")]
        public float range = 120f;
        public float heightTolerance = 30f;
        public float windup = 0.6f;
        public float speed = 200f;

        [Tooltip("How far it runs, level pixels.")]
        public float distance = 110f;
        public float recover = 0.6f;
        public float cooldown = 1f;

        [Tooltip("The box in front of its body that hurts while it runs, level pixels.")]
        public Vector2 reach = new Vector2(14f, 24f);
        public int damage = 1;

        private int stage;
        private float left;
        private float wait;
        private float togo;
        private bool landed;

        public bool IsWindingUp => stage == 1;
        public bool IsCharging => stage == 2;
        public override bool IsBusy => stage != 0;

        public override bool Step(Foe foe, float dt)
        {
            if (stage == 0)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || !foe.HeroOffset(out var at))
                {
                    return false;
                }
                if (Mathf.Abs(at.x) > range || Mathf.Abs(at.y) > heightTolerance)
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
                if (!landed)
                {
                    landed = foe.HurtAhead(reach, damage);
                }
                togo -= speed * dt;
                if (togo <= 0f || foe.IsBlockedAhead)
                {
                    stage = 3;
                    left = recover;
                    return true;
                }
                foe.Move = foe.Facing * speed;
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
                togo = distance;
                landed = false;
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

    /// <summary>One phase of a boss: its attacks in the order they come round, the rest between them, and the share of its health at which the next phase begins.</summary>
    [Serializable]
    public sealed class BossPhase
    {
        [Tooltip("The share of its full health at or below which this phase ends; 0 for the last.")]
        public float until;

        [Tooltip("Seconds between one attack and the next, in which it comes for the hero.")]
        public float rest = 0.6f;

        public FoeBehaviour[] attacks = new FoeBehaviour[0];

        public BossPhase() { }

        public BossPhase(float endsAt, float rests, params FoeBehaviour[] inOrder)
        {
            until = endsAt;
            rest = rests;
            attacks = inOrder;
        }

        public BossPhase Copy()
        {
            var copy = new BossPhase();
            copy.until = until;
            copy.rest = rest;
            copy.attacks = new FoeBehaviour[attacks.Length];
            for (var i = 0; i < attacks.Length; i++)
            {
                copy.attacks[i] = attacks[i].Copy();
            }
            return copy;
        }
    }

    /// <summary>
    /// A boss's pattern, the template for every boss: phases, each a list of attacks
    /// (any behaviours that say when they are busy: a swing, a charge, a shot, a
    /// summoning) that come round in order with a rest between, the boss walking up to
    /// the hero meanwhile. An attack that cannot begin within `patience` is passed over.
    /// When its health falls to a phase's threshold and no attack is under way, it stops
    /// for `changeTime`, warning, takes no harm, and begins the next phase. Give the
    /// attacks no cooldown of their own: the pattern paces them.
    /// </summary>
    [Serializable]
    public sealed class Pattern : FoeBehaviour
    {
        public BossPhase[] phases = new BossPhase[0];

        [Tooltip("Seconds it stands warning between two phases.")]
        public float changeTime = 1f;

        [Tooltip("Seconds an attack may wait for its chance before the next is tried.")]
        public float patience = 2f;

        [Tooltip("How close to the edge of its body, level pixels, it comes between attacks.")]
        public float stopAt = 20f;

        [Tooltip("Strikes do nothing while it changes phase.")]
        public bool guardedWhileChanging = true;

        private int phase;
        private int current;
        private float rest;
        private float changing;
        private float idle;
        private bool started;

        /// <summary>The phase it is in, from 0.</summary>
        public int Phase => phase;

        /// <summary>Which attack of the phase is next or under way, from 0.</summary>
        public int Attack => current;

        /// <summary>How many times it has changed phase.</summary>
        public int Changes { get; private set; }

        public bool IsChanging => changing > 0f;

        public override bool IsBusy => IsChanging || (Current != null && Current.IsBusy);

        /// <summary>The attack that is next or under way; null in a phase with none.</summary>
        public FoeBehaviour Current
        {
            get
            {
                if (phase >= phases.Length || phases[phase].attacks.Length == 0)
                {
                    return null;
                }
                return phases[phase].attacks[current % phases[phase].attacks.Length];
            }
        }

        public override bool Step(Foe foe, float dt)
        {
            if (phases.Length == 0)
            {
                return false;
            }
            if (changing > 0f)
            {
                changing -= dt;
                if (changing <= 0f)
                {
                    foe.Telegraph(false);
                }
                return true;
            }
            var now = phases[phase];
            var attack = Current;
            var busy = attack != null && attack.IsBusy;
            // A hair over the threshold, so a share like 0.7 of 10 is met at 7.
            var spent = foe.Health <= now.until * Mathf.Max(foe.Body.health, 1) + 0.001f;
            if (!busy && spent && phase < phases.Length - 1)
            {
                phase++;
                current = 0;
                started = false;
                rest = 0f;
                idle = 0f;
                changing = changeTime;
                Changes++;
                foe.Telegraph(true);
                return true;
            }
            if (attack == null)
            {
                Approach(foe);
                return true;
            }
            if (rest > 0f && !busy)
            {
                rest -= dt;
                Approach(foe);
                return true;
            }
            attack.Step(foe, dt);
            if (attack.IsBusy)
            {
                started = true;
                idle = 0f;
                return true;
            }
            if (started)
            {
                Next(now);
                return true;
            }
            // It has not had its chance: come nearer, and after a while try the next.
            idle += dt;
            if (idle >= patience)
            {
                Next(now);
                rest = 0f;
            }
            Approach(foe);
            return true;
        }

        private void Next(BossPhase now)
        {
            current = (current + 1) % Mathf.Max(now.attacks.Length, 1);
            started = false;
            idle = 0f;
            rest = now.rest;
        }

        private void Approach(Foe foe)
        {
            if (!foe.HeroOffset(out var at))
            {
                return;
            }
            foe.Face(at.x >= 0f ? 1 : -1);
            var close = Mathf.Abs(at.x) <= stopAt + foe.Body.size.x / 2f;
            foe.Move = close || foe.IsBlockedAhead ? 0f : foe.Facing * foe.Body.speed;
        }

        public override bool Blocks(Foe foe, Strike strike) => guardedWhileChanging && IsChanging;

        public override string Mark(Foe foe) => phases.Length > 1 ? "phase " + (phase + 1) + "/" + phases.Length : "";

        public override void OnStruck(Foe foe, bool held)
        {
            if (phase >= phases.Length)
            {
                return;
            }
            foreach (var attack in phases[phase].attacks)
            {
                attack.OnStruck(foe, held);
            }
            if (held && started)
            {
                // The attack was called off: on to the next.
                Next(phases[phase]);
            }
        }

        public override FoeBehaviour Copy()
        {
            var copy = (Pattern)MemberwiseClone();
            copy.phases = new BossPhase[phases.Length];
            for (var i = 0; i < phases.Length; i++)
            {
                copy.phases[i] = phases[i].Copy();
            }
            return copy;
        }
    }
}
