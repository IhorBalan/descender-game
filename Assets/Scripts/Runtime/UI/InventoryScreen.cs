using Descender.Economy;
using Descender.Player;

namespace Descender.UI
{
    /// <summary>
    /// The pack page: the purse, what is worn in each slot, then one row per item.
    /// Submit on a piece of equipment wears it (or takes it off, when it is worn);
    /// on a consumable, uses it through the hero. The kept things of the story
    /// (the scarf, the clock pieces) and the church iron not yet forged are listed
    /// after, since they are flags rather than items. The line under the list
    /// describes the picked item. Port of scripts/ui/inventory_menu.gd.
    /// </summary>
    public sealed class InventoryScreen : ListScreen
    {
        /// <summary>The hero, for using items and re-reading what he wears; may be null before the world loads.</summary>
        public IHeroMenuActions Hero { get; set; }

        /// <summary>A piece was worn or taken off, or an item used.</summary>
        public event System.Action Changed;

        protected override string HeadingText => "THE PACK";
        protected override string BackText => "Back";
        protected override string HintTemplate => "{navigate} pick   {ui_accept} wears or uses   {ui_cancel} goes back";
        protected override string EmptyText => "The pack is empty.";
        protected override bool Dims => Standalone;

        /// <summary>Whether the page dims the game itself (true from the pause menu, false inside the camp).</summary>
        public bool Standalone { get; set; } = true;

        protected override void FillList()
        {
            LineOne.text = MenuText.PurseText();
            LineTwo.text = MenuText.WornText();
            foreach (var id in Inventory.SortedIds())
            {
                var itemId = id;
                var worn = Inventory.IsEquipped(id);
                List.AddRow(
                    MenuText.PackLine(id),
                    worn ? UiTheme.Highlight : UiTheme.Item,
                    () => OnItemPressed(itemId),
                    () => ShowNote(Items.Description(itemId), UiTheme.Note)
                );
            }
            var kept = MenuText.KeptThings();
            if (kept.Count > 0)
            {
                List.AddLine("Kept things:", UiTheme.Note);
                foreach (var name in kept)
                {
                    List.AddLine($"   {name}", UiTheme.Note);
                }
            }
        }

        private void OnItemPressed(string id)
        {
            if (Items.IsEquipment(id))
            {
                var worn = !Inventory.IsEquipped(id);
                if (worn)
                {
                    Inventory.Equip(id);
                }
                else
                {
                    Inventory.Unequip(Items.SlotOf(id));
                }
                Hero?.RefreshEquipment();
                Refresh();
                ShowNote($"{(worn ? "Worn" : "Taken off")}: {Items.DisplayName(id)}", UiTheme.Done);
                Changed?.Invoke();
                return;
            }
            var reason = Hero == null ? "Nothing to use here" : Hero.UseItem(id);
            if (reason.Length == 0)
            {
                Refresh();
                ShowNote($"Used: {Items.DisplayName(id)}", UiTheme.Done);
                Changed?.Invoke();
            }
            else
            {
                ShowNote(reason, UiTheme.Warn);
            }
        }
    }
}
