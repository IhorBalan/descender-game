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
        public static readonly Color SoldColor = TitleKit.Faded;

        /// <summary>The Shops id open right now, or "".</summary>
        public string ShopId { get; private set; } = "";

        /// <summary>Something was bought; the HUD's purse follows.</summary>
        public event System.Action Bought;

        protected override string HeadingText => "TRADER";
        protected override string BackText => "Leave";
        protected override string ConfirmText => "Buy";

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
            // The greeting wraps under the heading, and the purse sits in gold under it.
            LineOne.textWrappingMode = TMPro.TextWrappingModes.Normal;
            LineOne.color = TitleKit.Parchment;
            LineOne.rectTransform.sizeDelta = new Vector2(1300f, 70f);
            LineTwo.color = TitleKit.Gold;
            LineTwo.rectTransform.anchoredPosition += new Vector2(0f, -14f);
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
                    sold ? SoldColor : TitleKit.Parchment,
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
