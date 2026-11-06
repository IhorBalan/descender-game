namespace Descender.Player
{
    /// <summary>What the hero is doing this tick; the sprite and the HUD read it.</summary>
    public enum PlayerState
    {
        Idle,
        Run,
        Jump,
        Fall,
        Dash,
        Attack,
        Heal,
        Hurt,
        Dead,
        Cast,
        Slam,
    }
}
