namespace Descender.Input
{
    /// <summary>
    /// Hero input set from code instead of a device: a script sets the axes and the
    /// presses, the hero reads them in his Update. A press stays set until
    /// <see cref="ClearPresses"/>, so the owner clears it once the hero has read it
    /// (the frame after setting it) and nothing is dropped between frames.
    /// </summary>
    public sealed class ScriptedInput : IHeroInput
    {
        public float MoveX { get; set; }
        public bool DownHeld { get; set; }
        public bool UpHeld { get; set; }
        public bool JumpHeld { get; set; }
        public bool JumpPressed { get; set; }
        public bool DashPressed { get; set; }
        public bool AttackPressed { get; set; }
        public bool CastPressed { get; set; }
        public bool HealPressed { get; set; }

        public void ClearPresses()
        {
            JumpPressed = false;
            DashPressed = false;
            AttackPressed = false;
            CastPressed = false;
            HealPressed = false;
        }

        /// <summary>Lets go of everything.</summary>
        public void Release()
        {
            MoveX = 0f;
            DownHeld = false;
            UpHeld = false;
            JumpHeld = false;
            ClearPresses();
        }
    }
}
