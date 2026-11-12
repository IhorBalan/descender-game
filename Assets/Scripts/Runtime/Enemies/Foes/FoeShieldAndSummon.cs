using System;
using System.Collections.Generic;
using Descender.Combat;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// A shield on the side the foe faces: a cut or a bolt that comes from in front does
    /// nothing. What comes from behind lands, so does what comes from above its head (a
    /// cut down onto it), and so does the slam unless the shield is told to stop that
    /// too. It does not act; it is asked about each strike.
    /// </summary>
    [Serializable]
    public sealed class Shield : FoeBehaviour
    {
        public bool stopsSword = true;
        public bool stopsBolt = true;
        public bool stopsSlam;

        /// <summary>How many strikes it has stopped.</summary>
        public int Blocked { get; private set; }

        public override bool Step(Foe foe, float dt) => false;

        public override bool Blocks(Foe foe, Strike strike)
        {
            var stops = stopsSlam;
            if (strike.Kind == StrikeKind.Sword)
            {
                stops = stopsSword;
            }
            else if (strike.Kind == StrikeKind.Bolt)
            {
                stops = stopsBolt;
            }
            var bounds = foe.Box.bounds;
            // From is the attacker's feet: at or above its head, the blow comes down past the shield.
            if (!stops || strike.From.y >= bounds.max.y)
            {
                return false;
            }
            if ((strike.From.x - bounds.center.x) * foe.Facing <= 0f)
            {
                return false;
            }
            Blocked++;
            return true;
        }

        public override string Mark(Foe foe) => foe.Facing > 0 ? "shield >" : "< shield";
    }

    /// <summary>
    /// Makes other foes: with the hero within `range` and fewer than `cap` of its own
    /// alive, it warns for `windup` and one more appears just ahead of it (behind, at a
    /// wall), then it waits `cooldown`. A hit that holds it calls the summoning off.
    /// </summary>
    [Serializable]
    public sealed class Summon : FoeBehaviour
    {
        [Tooltip("What it makes; a walker when left empty.")]
        public FoeRecipe summons;

        [Tooltip("How many of its own may be alive at once.")]
        public int cap = 2;

        [Tooltip("How far his middle may be from its own, level pixels, for it to summon.")]
        public float range = 160f;
        public float windup = 0.8f;
        public float cooldown = 3f;

        [Tooltip("The gap between its body and the new one, level pixels.")]
        public float ahead = 12f;

        private List<Foe> made = new List<Foe>();
        private bool warning;
        private float left;
        private float wait;

        /// <summary>Every foe it has made, alive or not, in the order it made them.</summary>
        public IReadOnlyList<Foe> Made => made;

        /// <summary>How many of them are alive.</summary>
        public int Alive
        {
            get
            {
                var count = 0;
                foreach (var foe in made)
                {
                    if (foe != null && !foe.IsDead)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public override bool IsBusy => warning;

        public override bool Step(Foe foe, float dt)
        {
            if (!warning)
            {
                wait = Mathf.Max(wait - dt, 0f);
                if (wait > 0f || Alive >= cap || !foe.HeroOffset(out var at) || at.magnitude > range)
                {
                    return false;
                }
                foe.Face(at.x >= 0f ? 1 : -1);
                warning = true;
                left = windup;
                foe.Telegraph(true);
                return true;
            }
            left -= dt;
            if (left > 0f)
            {
                return true;
            }
            foe.Telegraph(false);
            warning = false;
            wait = cooldown;
            made.Add(Spawn(foe));
            return true;
        }

        private Foe Spawn(Foe foe)
        {
            var recipe = summons ?? FoeRecipes.Walker();
            var size = recipe.body.size / EnemyPhysics.Ppu;
            var bounds = foe.Box.bounds;
            var side = EnemyPhysics.IsOnWall(foe.Box, foe.Facing, foe.Hits) ? -foe.Facing : foe.Facing;
            var x = bounds.center.x + side * (bounds.extents.x + ahead / EnemyPhysics.Ppu + size.x / 2f);
            // On the floor it stands on, a pixel clear; a flier at its own height.
            var y = recipe.body.flies ? bounds.center.y : bounds.min.y + size.y / 2f + 1f / EnemyPhysics.Ppu;
            var born = Foe.Make(recipe, foe.transform.parent, new Vector2(x, y), size);
            born.Face(side);
            return born;
        }

        public override void OnStruck(Foe foe, bool held)
        {
            if (held && warning)
            {
                warning = false;
                wait = cooldown;
            }
        }

        public override FoeBehaviour Copy()
        {
            var copy = (Summon)MemberwiseClone();
            copy.made = new List<Foe>();
            return copy;
        }
    }
}
