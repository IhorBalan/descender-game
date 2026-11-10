using System;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Player;
using UnityEngine;

namespace Descender.Triggers
{
    public enum TriggerEvent
    {
        /// <summary>The hero walks into the zone.</summary>
        Enter,

        /// <summary>The hero walks out of it.</summary>
        Leave,

        /// <summary>The hero presses interact while standing in it.</summary>
        Interact,

        /// <summary>The hero's sword or light strikes it.</summary>
        Hit,
    }

    /// <summary>
    /// An item, a switch, a door's lock, a checkpoint: an area that, when something
    /// happens to it (<see cref="TriggerEvent"/>) and its <see cref="TriggerNeeds"/> are
    /// met, runs its list of <see cref="TriggerAction"/>s, once or every time. The area
    /// is the colliders on the object, which the zone makes triggers; a zone that waits
    /// to be hit sits on the Enemy layer so the hero's attacks find it, any other on the
    /// Trigger layer. With a <c>Shape</c> beside it, it is a placeholder pickup.
    /// </summary>
    public sealed class TriggerZone : MonoBehaviour, IHittable
    {
        [SerializeField]
        private TriggerEvent on = TriggerEvent.Enter;

        [SerializeField]
        [Tooltip("Fires the first time only.")]
        private bool once = true;

        [SerializeField]
        [Tooltip("The object disappears when it fires: a pickup.")]
        private bool hideWhenFired;

        [SerializeField]
        private TriggerNeeds needs = new TriggerNeeds();

        [SerializeReference]
        private List<TriggerAction> actions = new List<TriggerAction>();

        private PlayerController near;

        public TriggerEvent On => on;
        public bool Once => once;
        public TriggerNeeds Needs => needs;
        public IReadOnlyList<TriggerAction> Actions => actions;

        /// <summary>How many times it has fired.</summary>
        public int TimesFired { get; private set; }

        /// <summary>The hero standing in the zone, or null.</summary>
        public PlayerController Near => near;

        /// <summary>Raised after the actions have run.</summary>
        public event Action<TriggerZone> Fired;

        /// <summary>Sets the zone up from code, as the inspector does by hand.</summary>
        public void Configure(
            TriggerEvent when,
            bool onlyOnce,
            bool hide,
            TriggerNeeds needed,
            IEnumerable<TriggerAction> run
        )
        {
            on = when;
            once = onlyOnce;
            hideWhenFired = hide;
            needs = needed ?? new TriggerNeeds();
            actions = run != null ? new List<TriggerAction>(run) : new List<TriggerAction>();
            ApplyLayer();
        }

        private void Awake()
        {
            ApplyLayer();
        }

        private void ApplyLayer()
        {
            gameObject.layer = on == TriggerEvent.Hit ? Layers.Enemy : Layers.Trigger;
            foreach (var area in GetComponents<Collider2D>())
            {
                area.isTrigger = true;
            }
        }

        /// <summary>
        /// Fires for a hero if it still can and its needs are met: runs the actions in
        /// order. Returns whether it fired.
        /// </summary>
        public bool TryFire(PlayerController hero)
        {
            if ((once && TimesFired > 0) || !needs.Met(hero))
            {
                return false;
            }
            TimesFired++;
            var context = new TriggerContext(this, hero);
            foreach (var action in actions)
            {
                action?.Run(context);
            }
            Fired?.Invoke(this);
            if (hideWhenFired)
            {
                gameObject.SetActive(false);
            }
            return true;
        }

        /// <summary>The interact press, for the hero standing in the zone. False when nobody is there.</summary>
        public bool Interact()
        {
            return on == TriggerEvent.Interact && near != null && TryFire(near);
        }

        /// <summary>A strike from the hero's sword or light.</summary>
        public bool Hit(int damage, Vector2 from)
        {
            return on == TriggerEvent.Hit && TryFire(PlayerController.Current);
        }

        private void Update()
        {
            if (on != TriggerEvent.Interact || near == null)
            {
                return;
            }
            var input = Input.InputRouter.Instance;
            if (input != null && input.InteractPressed)
            {
                Interact();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var hero = Pickups.Pickup.HeroFrom(other);
            if (hero == null)
            {
                return;
            }
            near = hero;
            if (on == TriggerEvent.Enter)
            {
                TryFire(hero);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (near == null || body == null || body.gameObject != near.gameObject)
            {
                return;
            }
            var hero = near;
            near = null;
            if (on == TriggerEvent.Leave)
            {
                TryFire(hero);
            }
        }

        private void OnDisable()
        {
            near = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = on == TriggerEvent.Hit ? new Color(1f, 0.5f, 0.3f) : new Color(0.4f, 0.8f, 1f);
            foreach (var area in GetComponents<Collider2D>())
            {
                Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
            }
        }
    }
}
