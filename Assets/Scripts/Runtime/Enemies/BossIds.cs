namespace Descender.Enemies
{
    /// <summary>The bosses by id, top to bottom; a profile asset, an intro and a defeat entry each.</summary>
    public static class BossIds
    {
        public static readonly string[] All =
        {
            "seraph_warden",
            "the_penitent",
            "limbo_lord",
            "palace_of_velvet",
            "the_bloated_one",
            "golden_vault",
            "the_fury",
            "cursed_bishop",
            "the_red_arena",
            "the_false_king",
            "final_boss_the_betrayer",
            "gardener",
        };

        /// <summary>The story flag set when a boss dies; a dead boss stays dead in the save.</summary>
        public static string FlagFor(string id) => $"boss:{id}";
    }
}
