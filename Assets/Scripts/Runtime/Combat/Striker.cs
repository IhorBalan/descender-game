using Descender.Player;
using UnityEngine;

namespace Descender.Combat
{
    /// <summary>
    /// Something that hits back: while the hero's middle is inside its reach it tries
    /// to hurt him, at once when he comes in and then every few seconds. It counts its
    /// tries and the ones that landed, so the time he cannot be hurt after a hit shows.
    /// </summary>
    public sealed class Striker : MonoBehaviour
    {
        private Vector2 reach = new Vector2(4f, 3f);
        private float every = 1f;
        private int damage = 1;
        private float wait;
        private bool inside;

        /// <summary>How many times it has tried to hurt him.</summary>
        public int Tries { get; private set; }

        /// <summary>How many of those hurt him.</summary>
        public int Landed { get; private set; }

        /// <summary>The box around it he is hit in, in units.</summary>
        public Vector2 Reach => reach;

        /// <summary>The seconds between its tries.</summary>
        public float Every => every;

        /// <summary>Its reach in units around its middle, the seconds between tries, and the damage of a hit.</summary>
        public void Configure(Vector2 reachUnits, float seconds, int hurts)
        {
            reach = reachUnits;
            every = Mathf.Max(seconds, 0.05f);
            damage = hurts;
        }

        /// <summary>True when the hero's middle is inside its reach.</summary>
        public bool Reaches(PlayerController hero)
        {
            if (hero == null || !hero.IsAlive)
            {
                return false;
            }
            // His position is his feet; his middle is a unit above them.
            var from = (Vector2)hero.transform.position + Vector2.up - (Vector2)transform.position;
            return Mathf.Abs(from.x) <= reach.x / 2f && Mathf.Abs(from.y) <= reach.y / 2f;
        }

        private void Update()
        {
            var hero = PlayerController.Current;
            if (!Reaches(hero))
            {
                inside = false;
                return;
            }
            if (!inside)
            {
                inside = true;
                wait = 0f;
            }
            wait -= Time.deltaTime;
            if (wait > 0f)
            {
                return;
            }
            wait = every;
            Tries++;
            if (hero.Hurt(damage, transform.position))
            {
                Landed++;
            }
        }
    }
}
