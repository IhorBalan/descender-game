using System;
using System.Collections.Generic;
using Descender.Story;

namespace Descender.Economy
{
    public sealed class Ware
    {
        public string Item;
        public Dictionary<string, int> Price = new Dictionary<string, int>();
    }

    public sealed class Shop
    {
        public string Id;
        public string Name;
        public string Greeting;
        public Ware[] Stock;
    }

    /// <summary>
    /// The traders and what they sell. An NPC placement names one with `shop`; once
    /// its unique lines are spent, talking to it plays a repeat line and then opens
    /// the wares. Each ware names an item and its price; equipment is sold once per
    /// save (flag `bought:<shop>:<item>`), consumables as often as the purse allows.
    /// The Seal of the Vault scales every price. See docs/design/economy.md.
    /// </summary>
    public static class Shops
    {
        public static readonly Shop[] List =
        {
            new Shop
            {
                Id = "pell",
                Name = "Old Pell's forge",
                Greeting = "Church iron, lad, what there is of it. Light pays: it's what they want up there,"
                    + " so it's what I want down here.",
                Stock = new[]
                {
                    W("iron_sword", "light", 120),
                    W("hinge_blade", "light", 90),
                    W("quilted_coat", "light", 100),
                    W("scrap_mail", "light", 220),
                    W("flask", "light", 25),
                },
            },
            new Shop
            {
                Id = "vael",
                Name = "Vael the Bargainer",
                Greeting = "Light, bones, memories. I take all three. Buyers don't give names; neither do I.",
                Stock = new[]
                {
                    W("halo_shard", "bones", 1),
                    W("blade_oil", "bones", 1),
                    W("zeal_draught", "bones", 1),
                    W("flask", "light", 30),
                    W("cold_coin", "bones", 3),
                    W("imp_tooth", "bones", 4),
                    W("basin_shard", "bones", 5),
                },
            },
        };

        public static bool Has(string id) => Get(id) != null;

        public static Shop Get(string id)
        {
            foreach (var shop in List)
            {
                if (shop.Id == id)
                {
                    return shop;
                }
            }
            return null;
        }

        /// <summary>The story flag set once a trader has sold a piece of equipment.</summary>
        public static string BoughtFlag(string shopId, string itemId) => $"bought:{shopId}:{itemId}";

        /// <summary>Whether a ware is gone from the stall: equipment the hero already bought.</summary>
        public static bool SoldOut(string shopId, string itemId) =>
            Items.IsEquipment(itemId) && StoryFlags.Has(BoughtFlag(shopId, itemId));

        /// <summary>What a ware costs right now, with the hero's charms counted in (never below one of each currency).</summary>
        public static Dictionary<string, int> PriceOf(Ware ware)
        {
            var result = new Dictionary<string, int>();
            var scale = Inventory.Factor("prices");
            foreach (var pair in ware.Price)
            {
                result[pair.Key] = Math.Max((int)Math.Round(pair.Value * scale, MidpointRounding.AwayFromZero), 1);
            }
            return result;
        }

        /// <summary>
        /// Sells a ware to the hero: spends the price, puts the item in the pack and
        /// marks equipment sold. Returns "" on success, else the reason.
        /// </summary>
        public static string Buy(string shopId, Ware ware)
        {
            var itemId = ware?.Item ?? "";
            if (!Items.Has(itemId))
            {
                return "Nothing there";
            }
            if (SoldOut(shopId, itemId))
            {
                return "Sold";
            }
            var price = PriceOf(ware);
            if (!Inventory.Spend(price))
            {
                return $"Not enough: it costs {Inventory.PriceText(price)}";
            }
            Inventory.Add(itemId);
            if (Items.IsEquipment(itemId))
            {
                StoryFlags.Set(BoughtFlag(shopId, itemId));
            }
            return "";
        }

        private static Ware W(string item, string currency, int count)
        {
            var ware = new Ware { Item = item };
            ware.Price[currency] = count;
            return ware;
        }
    }
}
