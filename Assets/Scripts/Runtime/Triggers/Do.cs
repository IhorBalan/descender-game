using Descender.Save;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// The actions, made in one line each, for zones set up from code:
    /// <c>Do.Give("key")</c>, <c>Do.Door(door, SwitchTo.On)</c>.
    /// </summary>
    public static class Do
    {
        public static GrantAbility Grant(Ability ability)
        {
            var action = new GrantAbility();
            action.ability = ability;
            return action;
        }

        public static GiveItem Give(string item)
        {
            var action = new GiveItem();
            action.item = item;
            return action;
        }

        public static TakeItem Take(string item)
        {
            var action = new TakeItem();
            action.item = item;
            return action;
        }

        public static SetFlag Flag(string flag)
        {
            var action = new SetFlag();
            action.flag = flag;
            return action;
        }

        public static SetActive Active(GameObject target, SwitchTo to)
        {
            var action = new SetActive();
            action.target = target;
            action.to = to;
            return action;
        }

        public static SetDoor Door(Triggers.Door door, SwitchTo to)
        {
            var action = new SetDoor();
            action.door = door;
            action.to = to;
            return action;
        }

        public static SetTimed Timed(TimedPlatform platform, SwitchTo to)
        {
            var action = new SetTimed();
            action.platform = platform;
            action.to = to;
            return action;
        }

        public static SetForce Wind(ForceZone zone, SwitchTo to)
        {
            var action = new SetForce();
            action.zone = zone;
            action.to = to;
            return action;
        }

        public static SetMover Mover(Triggers.Mover mover, SwitchTo to)
        {
            var action = new SetMover();
            action.mover = mover;
            action.to = to;
            return action;
        }

        public static GiveCurrency Currency(string currency, int amount)
        {
            var action = new GiveCurrency();
            action.currency = currency;
            action.amount = amount;
            return action;
        }

        public static GiveUpgrade Upgrade(Player.UpgradeKind kind, string flag)
        {
            var action = new GiveUpgrade();
            action.kind = kind;
            action.flag = flag;
            return action;
        }

        public static GrantPower Power(Player.Power power)
        {
            var action = new GrantPower();
            action.power = power;
            return action;
        }

        public static StartEncounter Fight(Enemies.Encounter encounter)
        {
            var action = new StartEncounter();
            action.encounter = encounter;
            return action;
        }

        public static ShowMessage Say(string text)
        {
            var action = new ShowMessage();
            action.text = text;
            return action;
        }
    }
}
