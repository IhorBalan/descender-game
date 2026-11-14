namespace Descender.Enemies
{
    /// <summary>
    /// The habit a placement gives a walker on top of patrolling, which
    /// <see cref="FoeKinds.FromWalker"/> turns into parts. Named as the Godot placements
    /// name them, in lower case.
    /// </summary>
    public enum WalkerBehaviour
    {
        None,
        Chase,
        Ambush,
        Pounce,
        Hurl,
        Slam,
        Lunge,
    }
}
