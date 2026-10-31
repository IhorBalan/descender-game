namespace Descender.Player
{
    /// <summary>
    /// The permanent pickups (see UpgradePickup and <see cref="Upgrades"/>): Kept
    /// Light is one more heart, a Vial of the Basin one more healing charge, Church
    /// Iron is carried to Old Pell for the sword. Each is a story flag in the save.
    /// </summary>
    public enum UpgradeKind
    {
        Heart = 0,
        Vial = 1,
        Iron = 2,
    }
}
