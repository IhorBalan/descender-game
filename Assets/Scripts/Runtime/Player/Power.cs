namespace Descender.Player
{
    /// <summary>
    /// The timed power-ups a shrine or a consumable gives the hero: the Halo turns
    /// the next hit aside (and lasts until it does), the Lit Blade makes the sword
    /// hit harder and Zeal quickens the feet and the dash, each for
    /// <see cref="HeroPowers.Duration"/> seconds. The order is the Godot enum's, which
    /// the HUD draws them in.
    /// </summary>
    public enum Power
    {
        Halo = 0,
        Blade = 1,
        Zeal = 2,
    }

    /// <summary>Numbers and words the power-ups share between the hero and the HUD.</summary>
    public static class HeroPowers
    {
        /// <summary>How long a timed power-up lasts, in seconds (Player.POWER_TIME).</summary>
        public const float Duration = 20f;

        public static readonly Power[] All = { Power.Halo, Power.Blade, Power.Zeal };

        /// <summary>The HUD line shown when a power-up lands (PowerUp.MESSAGES).</summary>
        public static string Message(Power power)
        {
            switch (power)
            {
                case Power.Halo:
                    return "Halo: the next hit is turned aside";
                case Power.Blade:
                    return "Lit Blade: the sword hits harder for a while";
                case Power.Zeal:
                    return "Zeal: faster dashes and feet for a while";
                default:
                    return "Power-up";
            }
        }
    }
}
