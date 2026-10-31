namespace Descender.World
{
    /// <summary>
    /// Implemented by a prefab a WorldPlacement spawns (an enemy, a checkpoint, a
    /// pickup, an NPC) to read what the placement says about it: its behaviour, its
    /// dialogue, its checkpoint index, its ability. Called right after Instantiate,
    /// on the first such component found in the spawned object's children.
    /// </summary>
    public interface IPlacementReceiver
    {
        void Configure(WorldPlacement placement);
    }
}
