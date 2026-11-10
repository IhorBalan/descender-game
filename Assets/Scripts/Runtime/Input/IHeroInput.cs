namespace Descender.Input
{
    /// <summary>
    /// What the hero reads each frame: the held axes and the presses of this frame.
    /// The InputRouter is the one in play; a script (the scripted playtest, a demo)
    /// hands <c>PlayerController.InputSource</c> another, such as a ScriptedInput.
    /// </summary>
    public interface IHeroInput
    {
        /// <summary>-1 left, 1 right, 0 still.</summary>
        float MoveX { get; }
        bool DownHeld { get; }
        bool UpHeld { get; }
        bool JumpHeld { get; }
        bool JumpPressed { get; }
        bool DashPressed { get; }
        bool AttackPressed { get; }
        bool CastPressed { get; }
        bool HealPressed { get; }
    }
}
