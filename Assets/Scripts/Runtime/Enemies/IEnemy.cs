namespace Descender.Enemies
{
    /// <summary>
    /// What every enemy body shares for the level around it: the placement that spawned
    /// it (so a kill is remembered in <see cref="Kills"/> until the hero respawns) and
    /// the moment it dies.
    /// </summary>
    public interface IEnemy
    {
        /// <summary>The WorldPlacement id ("region/Name") that spawned it, or "" for one made in code.</summary>
        string PlacementId { get; set; }

        bool IsDead { get; }

        event System.Action<IEnemy> Died;
    }
}
