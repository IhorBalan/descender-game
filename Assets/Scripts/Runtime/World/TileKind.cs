namespace Descender.World
{
    /// <summary>
    /// What a terrain tile does to the hero, the TileSet's custom data "kind" in
    /// Godot (scripts/world_map.gd, KIND_*). Hidden is a secret wall: solid until a
    /// Smite breaks it.
    /// </summary>
    public enum TileKind
    {
        None = 0,
        Solid = 1,
        Ledge = 2,
        Hazard = 3,
        Hidden = 4,
    }
}
