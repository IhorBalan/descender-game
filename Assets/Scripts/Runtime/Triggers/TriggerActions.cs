using System;
using Descender.Player;
using Descender.Save;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>Gives the hero an ability, as a feather does.</summary>
    [Serializable]
    public sealed class GrantAbility : TriggerAction
    {
        public Ability ability;

        public override void Run(TriggerContext context)
        {
            if (context.Hero != null)
            {
                context.Hero.Grant(ability);
            }
        }
    }

    /// <summary>Gives an item of the pack (loot) or, for any other name, a key.</summary>
    [Serializable]
    public sealed class GiveItem : TriggerAction
    {
        public string item = "";

        public override void Run(TriggerContext context)
        {
            if (!Economy.Items.Has(item))
            {
                Keys.Add(item);
            }
            else if (context.Hero != null)
            {
                context.Hero.CollectLoot(item);
            }
            else
            {
                Economy.Inventory.Add(item);
            }
        }
    }

    /// <summary>Takes a key or one item of the pack.</summary>
    [Serializable]
    public sealed class TakeItem : TriggerAction
    {
        public string item = "";

        public override void Run(TriggerContext context)
        {
            if (!Keys.Remove(item))
            {
                Economy.Inventory.Remove(item);
            }
        }
    }

    /// <summary>Gives light or tally bones.</summary>
    [Serializable]
    public sealed class GiveCurrency : TriggerAction
    {
        public string currency = "";
        public int amount = 1;

        public override void Run(TriggerContext context)
        {
            if (context.Hero != null)
            {
                context.Hero.CollectCurrency(currency, amount);
            }
            else
            {
                Economy.Inventory.AddCurrency(currency, amount);
            }
        }
    }

    /// <summary>Sets a story flag.</summary>
    [Serializable]
    public sealed class SetFlag : TriggerAction
    {
        public string flag = "";

        public override void Run(TriggerContext context) => Story.StoryFlags.Set(flag);
    }

    /// <summary>Shows, hides or flips an object.</summary>
    [Serializable]
    public sealed class SetActive : TriggerAction
    {
        public GameObject target;
        public SwitchTo to = SwitchTo.On;

        public override void Run(TriggerContext context)
        {
            if (target != null)
            {
                target.SetActive(to == SwitchTo.Toggle ? !target.activeSelf : to == SwitchTo.On);
            }
        }
    }

    /// <summary>Opens (On), closes (Off) or flips a door.</summary>
    [Serializable]
    public sealed class SetDoor : TriggerAction
    {
        public Door door;
        public SwitchTo to = SwitchTo.On;

        public override void Run(TriggerContext context)
        {
            if (door != null)
            {
                door.Set(to);
            }
        }
    }

    /// <summary>Brings back (On), takes away (Off) or flips a crumbling or blinking platform.</summary>
    [Serializable]
    public sealed class SetTimed : TriggerAction
    {
        public TimedPlatform platform;
        public SwitchTo to = SwitchTo.On;

        public override void Run(TriggerContext context)
        {
            if (platform != null)
            {
                platform.Set(to);
            }
        }
    }

    /// <summary>Starts (On), stills (Off) or flips a wind, a lift or a downdraught.</summary>
    [Serializable]
    public sealed class SetForce : TriggerAction
    {
        public ForceZone zone;
        public SwitchTo to = SwitchTo.On;

        public override void Run(TriggerContext context)
        {
            if (zone != null)
            {
                zone.Set(to);
            }
        }
    }

    /// <summary>Starts (On), stops (Off) or flips a mover.</summary>
    [Serializable]
    public sealed class SetMover : TriggerAction
    {
        public Mover mover;
        public SwitchTo to = SwitchTo.On;

        public override void Run(TriggerContext context)
        {
            if (mover != null)
            {
                mover.Set(to);
            }
        }
    }

    /// <summary>Shows a line on the HUD.</summary>
    [Serializable]
    public sealed class ShowMessage : TriggerAction
    {
        public string text = "";
        public float seconds = UI.Hud.DefaultMessageTime;

        public override void Run(TriggerContext context) => Notice.Show(text, seconds);
    }

    /// <summary>Gives hearts back.</summary>
    [Serializable]
    public sealed class Heal : TriggerAction
    {
        public int amount = 1;

        public override void Run(TriggerContext context)
        {
            if (context.Hero != null)
            {
                context.Hero.RestoreHealth(amount);
            }
        }
    }

    /// <summary>Hurts the hero, knocking him away from the zone.</summary>
    [Serializable]
    public sealed class Hurt : TriggerAction
    {
        public int amount = 1;

        public override void Run(TriggerContext context)
        {
            if (context.Hero != null)
            {
                context.Hero.Hurt(amount, context.Position);
            }
        }
    }

    /// <summary>Makes a point (the zone itself when none is given) the place the hero comes back to.</summary>
    [Serializable]
    public sealed class SetRespawn : TriggerAction
    {
        public Transform point;

        public override void Run(TriggerContext context)
        {
            Vector2 at = point != null ? (Vector2)point.position : context.Position;
            if (context.Hero != null)
            {
                context.Hero.SetRespawnPoint(at);
            }
            var flow = Game.GameFlow.Instance;
            if (flow != null)
            {
                flow.RespawnPoint = at;
            }
        }
    }

    /// <summary>A timed power-up.</summary>
    [Serializable]
    public sealed class GrantPower : TriggerAction
    {
        public Power power;

        public override void Run(TriggerContext context)
        {
            if (context.Hero != null)
            {
                context.Hero.GrantPower(power);
            }
        }
    }

    /// <summary>A permanent upgrade; the flag, when given, records that it was taken.</summary>
    [Serializable]
    public sealed class GiveUpgrade : TriggerAction
    {
        public UpgradeKind kind;
        public string flag = "";

        public override void Run(TriggerContext context)
        {
            if (context.Hero != null)
            {
                context.Hero.ApplyUpgrade(kind, flag);
            }
        }
    }

    /// <summary>Puts a copy of a prefab at a point (the zone itself when none is given).</summary>
    [Serializable]
    public sealed class Spawn : TriggerAction
    {
        public GameObject prefab;
        public Transform point;
        public Vector2 offset;

        /// <summary>The last copy made, for whoever wants it.</summary>
        public GameObject Last { get; private set; }

        public override void Run(TriggerContext context)
        {
            if (prefab == null)
            {
                return;
            }
            Vector2 at = point != null ? (Vector2)point.position : context.Position;
            Last = UnityEngine.Object.Instantiate(prefab, at + offset, Quaternion.identity);
        }
    }
}
