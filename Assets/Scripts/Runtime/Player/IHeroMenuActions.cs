using UnityEngine;

namespace Descender.Player
{
    /// <summary>
    /// What the in-game screens ask of the hero: the HUD reads the dash cooldown
    /// and the power-up timers, the pack uses a consumable and has the worn pieces
    /// re-read after a change, the camp rests him. The hero behaviour implements
    /// this; the screens never reach into its movement. A screen given no hero
    /// still shows and still wears equipment, but cannot use items.
    /// </summary>
    public interface IHeroMenuActions
    {
        /// <summary>0 while the dash is coming back, 1 when it is ready.</summary>
        float DashReadiness { get; }

        /// <summary>The sword's damage with everything worn and learned, for the forge message.</summary>
        int AttackDamage { get; }

        /// <summary>Where the hero stands, in Unity units.</summary>
        Vector2 Position { get; }

        bool HasPower(Power power);

        /// <summary>Seconds a power-up has left; positive infinity for one that lasts until used.</summary>
        float PowerTimeLeft(Power power);

        /// <summary>
        /// Uses a consumable from the pack (a flask puts a healing charge back, a shard,
        /// an oil or a draught grants its power). Returns "" when it was used, else why not.
        /// </summary>
        string UseItem(string itemId);

        /// <summary>Re-reads the worn pieces and the skills into the hero's numbers.</summary>
        void RefreshEquipment();

        /// <summary>Rests at a camp: health and charges to their maximums.</summary>
        void RestAtCamp();
    }
}
