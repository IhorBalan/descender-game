namespace Descender.Enemies
{
    /// <summary>
    /// The habit a placement gives a <see cref="Walker"/> on top of patrolling; see the
    /// Walker's summary. Named as the Godot placements name them, in lower case.
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
