using System;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// Something the hero breaks: it takes a set number of strikes of the kind it
    /// answers to (<see cref="StruckBy"/>), then runs its <see cref="TriggerAction"/>s
    /// and is gone. A strike of another kind does nothing to it. To stand in his way
    /// and still be found by his attacks, which look on the Enemy layer, the solid
    /// collider sits on the World layer and a child with a collider of the same size
    /// sits on the Enemy layer; either leads a strike here.
    /// </summary>
    public sealed class Breakable : MonoBehaviour, IStruck
    {
        [SerializeField]
        [Min(1)]
        [Tooltip("How many strikes break it.")]
        private int hits = 1;

        [SerializeField]
        [Tooltip("The strike it answers to.")]
        private StruckBy struckBy = StruckBy.Any;

        [SerializeReference]
        private List<TriggerAction> actions = new List<TriggerAction>();

        private int taken;

        /// <summary>How many strikes it takes in all.</summary>
        public int Hits => hits;

        /// <summary>How many more it takes.</summary>
        public int HitsLeft => Mathf.Max(hits - taken, 0);

        public StruckBy By => struckBy;
        public bool IsBroken { get; private set; }

        /// <summary>Raised when it breaks, after its actions have run.</summary>
        public event Action<Breakable> Broke;

        /// <summary>Sets it up from code, as the inspector does by hand.</summary>
        public void Configure(int hitsToBreak, StruckBy by, IEnumerable<TriggerAction> run)
        {
            hits = Mathf.Max(hitsToBreak, 1);
            struckBy = by;
            actions = run != null ? new List<TriggerAction>(run) : new List<TriggerAction>();
        }

        /// <summary>A strike that does not say what it is counts as the sword's.</summary>
        public bool Hit(int damage, Vector2 from) => Struck(new Strike(StrikeKind.Sword, damage, from));

        /// <summary>One strike of the right kind is one hit, whatever its damage. False when it did nothing.</summary>
        public bool Struck(Strike strike)
        {
            if (IsBroken || !struckBy.Answers(strike.Kind))
            {
                return false;
            }
            taken++;
            Feedback.Spark(transform.position);
            if (taken >= hits)
            {
                Break();
            }
            return true;
        }

        /// <summary>Whole again, as it was made.</summary>
        public void Restore()
        {
            taken = 0;
            IsBroken = false;
            gameObject.SetActive(true);
        }

        private void Break()
        {
            IsBroken = true;
            var context = new TriggerContext(null, PlayerController.Current);
            foreach (var action in actions)
            {
                action?.Run(context);
            }
            Broke?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
