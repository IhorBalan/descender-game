using Descender.Economy;
using Descender.Player;

namespace Descender.UI
{
    /// <summary>
    /// The skills page of the camp: the level, the experience towards the next and
    /// the points to spend, then one row per skill with its rank. Submit on a skill
    /// the hero may learn puts a point into it (see Skills.Raise); the line under
    /// the list describes the picked skill, or says why it cannot be raised.
    /// Spending asks for a hero, so his maximums follow; without one the page only
    /// shows. Port of scripts/ui/skill_menu.gd.
    /// </summary>
    public sealed class SkillScreen : ListScreen
    {
        /// <summary>The hero, for learning; set by the camp when it opens.</summary>
        public IHeroMenuActions Hero { get; set; }

        /// <summary>A point went into a skill.</summary>
        public event System.Action<string> Learned;

        protected override string HeadingText => "SKILLS";
        protected override string BackText => "Back";
        protected override string HintTemplate =>
            "{navigate} pick   {ui_accept} spends a point   {ui_cancel} goes back";
        protected override bool Dims => Standalone;

        public bool Standalone { get; set; } = true;

        protected override void FillList()
        {
            LineOne.text = MenuText.SkillStatusText();
            LineTwo.text = MenuText.SkillPointsText();
            foreach (var id in Skills.Ids())
            {
                var skillId = id;
                List.AddRow(
                    MenuText.SkillLine(id),
                    MenuText.SkillColor(id),
                    () => OnSkillPressed(skillId),
                    () => ShowNote(Skills.Description(skillId), UiTheme.Note)
                );
            }
        }

        private void OnSkillPressed(string id)
        {
            var reason = Hero == null ? "Skills are learned at a camp" : Learn(Hero, id);
            if (reason.Length == 0)
            {
                Refresh();
                ShowNote($"{Skills.DisplayName(id)}, rank {Skills.Rank(id)} of {Skills.MaxRank(id)}", UiTheme.Done);
                Learned?.Invoke(id);
            }
            else
            {
                ShowNote(reason, UiTheme.Warn);
            }
        }

        /// <summary>
        /// Puts a point into a skill for the hero. The maximums follow and are filled,
        /// since he is resting at a camp. Returns "" when the point was spent, else why not.
        /// </summary>
        public static string Learn(IHeroMenuActions hero, string id)
        {
            var reason = Skills.Raise(id);
            if (reason.Length == 0)
            {
                hero.RefreshEquipment();
                hero.RestAtCamp();
            }
            return reason;
        }
    }
}
