using System;
using Descender.Save;

namespace Descender.Triggers
{
    /// <summary>
    /// What a trigger asks for before it fires: an ability, an item or key, a story flag,
    /// or the lack of an item or a flag. Everything left empty asks for nothing.
    /// </summary>
    [Serializable]
    public sealed class TriggerNeeds
    {
        public bool needsAbility;
        public Ability ability;

        /// <summary>An item of the pack or a key the hero must hold.</summary>
        public string item = "";

        /// <summary>A story flag that must be set.</summary>
        public string flag = "";

        /// <summary>An item or key the hero must not hold.</summary>
        public string withoutItem = "";

        /// <summary>A story flag that must not be set.</summary>
        public string withoutFlag = "";

        /// <summary>Whether this hero, his pack and keys and the story flags meet the needs.</summary>
        public bool Met(Player.PlayerController hero)
        {
            return Met(wanted => hero != null && hero.Has(wanted), Keys.Holding, Story.StoryFlags.Has);
        }

        /// <summary>The rule itself, with what the hero has given as three questions.</summary>
        public bool Met(Func<Ability, bool> hasAbility, Func<string, bool> hasItem, Func<string, bool> hasFlag)
        {
            if (needsAbility && !hasAbility(ability))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(item) && !hasItem(item))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(flag) && !hasFlag(flag))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(withoutItem) && hasItem(withoutItem))
            {
                return false;
            }
            return string.IsNullOrEmpty(withoutFlag) || !hasFlag(withoutFlag);
        }

        public static TriggerNeeds Nothing() => new TriggerNeeds();

        public static TriggerNeeds Item(string id)
        {
            var needs = new TriggerNeeds();
            needs.item = id;
            return needs;
        }

        public static TriggerNeeds WithoutItem(string id)
        {
            var needs = new TriggerNeeds();
            needs.withoutItem = id;
            return needs;
        }

        public static TriggerNeeds WithAbility(Ability wanted)
        {
            var needs = new TriggerNeeds();
            needs.needsAbility = true;
            needs.ability = wanted;
            return needs;
        }
    }
}
