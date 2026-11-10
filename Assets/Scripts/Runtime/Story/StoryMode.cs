namespace Descender.Story
{
    /// <summary>
    /// Whether the story is in the game. While it is parked, the places where the story
    /// speaks do not stand in the world: the story triggers, the NPCs who only talk, and
    /// the relics (items that carry dialogue). Traders, loot, abilities and everything else
    /// a placement can be are mechanics and stay. The script, the dialogue file, the
    /// dialogue code and the placements themselves are untouched, so setting
    /// <see cref="Parked"/> to false brings all of it back; `docs/story/PARKED.md` lists
    /// where each part lives.
    /// </summary>
    public static class StoryMode
    {
        /// <summary>True while the story is set aside. Tests of the dialogue turn it off and put it back.</summary>
        public static bool Parked { get; set; } = true;

        /// <summary>Whether a placement is a story element: a trigger, a talking NPC or a relic.</summary>
        public static bool IsStory(string type, bool hasDialogue, bool trades)
        {
            switch (type)
            {
                case "trigger":
                    return true;
                case "npc":
                    return !trades;
                case "item":
                    return hasDialogue;
                default:
                    return false;
            }
        }
    }
}
