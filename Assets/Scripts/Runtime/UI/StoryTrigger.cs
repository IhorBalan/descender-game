using Descender.World;

namespace Descender.UI
{
    /// <summary>
    /// An invisible place where the story speaks: the hero walking in makes the
    /// <see cref="DialogueRunner"/> play the first of the placement's entries whose
    /// flags allow it. It fires on entry only, so it stays quiet while he stands
    /// inside, and an entry that sets its own `unless` flag never plays twice. The
    /// area is the base class's, the hero's height. The prefab a "trigger"
    /// WorldPlacement spawns.
    /// </summary>
    public sealed class StoryTrigger : Interactable, IPlacementReceiver
    {
        protected override void Awake()
        {
            // A trigger is automatic whatever the prefab says: no prompt, it fires on touch.
            Configure(DialogueIds, RotateIds, true);
            base.Awake();
        }

        /// <summary>The placement's dialogue and repeat lines.</summary>
        public void Configure(WorldPlacement placement)
        {
            Configure(placement.Dialogue, placement.Rotate, true);
        }
    }
}
