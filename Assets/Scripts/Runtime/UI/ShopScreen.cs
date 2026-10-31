using Descender.Economy;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// A trader's wares: the stall's name and greeting, the hero's purse and one row
    /// per ware with its price. Submit buys (equipment once, then the row reads
    /// "sold"; consumables as often as the purse allows), the line under the list
    /// says what the picked ware does or why it could not be bought, and Cancel
    /// leaves. The game stands still while it is open; the dialogue runner opens it
    /// when a trading NPC's talk ends. Port of scripts/ui/shop_menu.gd.
    /// </summary>
    public sealed class ShopScreen : ListScreen
    {
        public static readonly Color SoldColor = new Color(0.55f, 0.52f, 0.7f);

        /// <summary>The Shops id open right now, or "".</summary>
        public string ShopId { get; private set; } = "";

        /// <summary>Something was bought; the HUD's purse follows.</summary>
        public event System.Action Bought;

        protected override string HeadingText => "TRADER";
        protected override string BackText => "Leave";
        protected override string HintTemplate => "{navigate} pick   {ui_accept} buys   {ui_cancel} leaves";

        /// <summary>Shows a trader's wares over the paused game.</summary>
        public void Open(string id)
        {
            if (!Shops.Has(id))
            {
                Debug.LogWarning($"ShopScreen: no shop called '{id}'");
                return;
            }
            ShopId = id;
            Open();
        }

        protected override void Build(RectTransform root)
        {
            base.Build(root);
            // The greeting wraps under the heading, and the purse sits in the subheading colour.
            LineOne.textWrappingMode = TMPro.TextWrappingModes.Normal;
            LineOne.color = UiTheme.Text;
            UiKit.PlaceRow(LineOne.rectTransform, 28f, 20f, 16f);
            LineTwo.color = UiTheme.Subheading;
            UiKit.PlaceRow(LineTwo.rectTransform, 48f, 10f);
            UiKit.Fill(List.GetComponent<RectTransform>(), 40f, 60f, 40f, 44f);
        }

        protected override void FillList()
        {
            var shop = Shops.Get(ShopId);
            if (shop == null)
            {
                return;
            }
            Heading.text = shop.Name.ToUpperInvariant();
            LineOne.text = shop.Greeting;
            LineTwo.text = MenuText.PurseText();
            foreach (var ware in shop.Stock)
            {
                var sold = Shops.SoldOut(ShopId, ware.Item);
                var picked = ware;
                List.AddRow(
                    MenuText.ShopLine(ShopId, ware),
                    sold ? SoldColor : UiTheme.Item,
                    () => OnWarePressed(picked),
                    () => ShowNote(MenuText.WareNote(picked.Item), UiTheme.Note)
                );
            }
        }

        private void OnWarePressed(Ware ware)
        {
            var reason = Shops.Buy(ShopId, ware);
            if (reason.Length == 0)
            {
                Refresh();
                ShowNote($"Bought: {Items.DisplayName(ware.Item)}. It is in the pack.", UiTheme.Done);
                Bought?.Invoke();
            }
            else
            {
                ShowNote(reason, UiTheme.Warn);
            }
        }
    }
}
