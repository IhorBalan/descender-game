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

    /// <summary>Which of the hero's strikes a zone that waits to be hit answers to.</summary>
    public enum StruckBy
    {
        /// <summary>Any of them.</summary>
        Any,

        /// <summary>Only a cut of the sword.</summary>
        Sword,

        /// <summary>Only the slam's shock.</summary>
        Slam,

        /// <summary>Only a bolt of Holy Light.</summary>
        Bolt,
    }

    /// <summary>What a <see cref="StruckBy"/> lets through.</summary>
    public static class StruckByRules
    {
        /// <summary>True when a strike of a kind is one this choice answers to.</summary>
        public static bool Answers(this StruckBy by, StrikeKind kind)
        {
            switch (by)
            {
                case StruckBy.Sword:
                    return kind == StrikeKind.Sword;
                case StruckBy.Slam:
                    return kind == StrikeKind.Slam;
                case StruckBy.Bolt:
                    return kind == StrikeKind.Bolt;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// An item, a switch, a door's lock, a checkpoint: an area that, when something
    /// happens to it (<see cref="TriggerEvent"/>) and its <see cref="TriggerNeeds"/> are
    /// met, runs its list of <see cref="TriggerAction"/>s, once or every time. The area
    /// is the colliders on the object. A zone that waits to be hit sits on the Enemy
    /// layer as a plain collider, so the hero's attacks find it and he walks through it;
    /// any other is a trigger on the Trigger layer. Such a zone can ask for one kind of
    /// strike (<see cref="StruckBy"/>): a target only a bolt sets off, a plate only the
    /// slam does. With a <c>Shape</c> beside it, it is a placeholder pickup.
    /// </summary>
    public sealed class TriggerZone : MonoBehaviour, IStruck
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
        [Tooltip("For a zone that waits to be hit: the strike it answers to.")]
        private StruckBy struckBy = StruckBy.Any;

        [SerializeField]
        private TriggerNeeds needs = new TriggerNeeds();

        [SerializeReference]
        private List<TriggerAction> actions = new List<TriggerAction>();

        private PlayerController near;

        public TriggerEvent On => on;
        public bool Once => once;
        public StruckBy By => struckBy;
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
            IEnumerable<TriggerAction> run,
            StruckBy by = StruckBy.Any
        )
        {
            struckBy = by;
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
            // Nothing but the world touches the Enemy layer, so a zone to strike can stay a
            // plain collider, which is what the attacks' overlap queries find.
            foreach (var area in GetComponents<Collider2D>())
            {
                area.isTrigger = on != TriggerEvent.Hit;
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

        /// <summary>A strike that does not say what it is counts as the sword's.</summary>
        public bool Hit(int damage, Vector2 from) => Struck(new Strike(StrikeKind.Sword, damage, from));

        /// <summary>A strike from the hero: it fires the zone when it is the kind the zone answers to.</summary>
        public bool Struck(Strike strike)
        {
            return on == TriggerEvent.Hit && Answers(strike.Kind) && TryFire(PlayerController.Current);
        }

        /// <summary>True when a strike of a kind is one this zone answers to.</summary>
        public bool Answers(StrikeKind kind) => struckBy.Answers(kind);

        private void Update()
        {
            if (on != TriggerEvent.Interact || near == null)
            {
                return;
            }
            // The press that turns a page of dialogue is not a press on the zone.
            var input = Input.InputRouter.Instance;
            if (input != null && input.InteractPressed && !UI.DialogueBox.AnyOpen)
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
