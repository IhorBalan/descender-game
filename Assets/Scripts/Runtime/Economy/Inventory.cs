using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Descender.Economy
{
    /// <summary>
    /// What the hero carries: his purse (light, the common currency every enemy
    /// leaks, and tally bones, the rare one the imps count on), the items in his
    /// pack by id and count, and what he wears in each slot. Static like
    /// <see cref="Descender.Story.StoryFlags"/>, so the pickups, the traders and the
    /// pause menu reach one state; the game session writes it into the save under
    /// "inventory" and reads it back. See docs/design/economy.md.
    /// </summary>
    public static class Inventory
    {
        public sealed class Currency
        {
            public string Id;
            public string Name;
            public Color Color;
        }

        public static readonly Currency[] Currencies =
        {
            new Currency { Id = "light", Name = "light", Color = new Color(1.0f, 0.95f, 0.7f) },
            new Currency { Id = "bones", Name = "tally bones", Color = new Color(0.95f, 0.92f, 0.85f) },
        };

        private static readonly Dictionary<string, int> Purse = new Dictionary<string, int>();
        /// <summary>Item id -> how many the pack holds.</summary>
        private static readonly Dictionary<string, int> Pack = new Dictionary<string, int>();
        /// <summary>Slot -> the id of the piece worn there.</summary>
        private static readonly Dictionary<ItemSlot, string> Worn = new Dictionary<ItemSlot, string>();

        static Inventory()
        {
            Reset();
        }

        /// <summary>A new game starts with an empty pack.</summary>
        public static void Reset()
        {
            Purse.Clear();
            foreach (var currency in Currencies)
            {
                Purse[currency.Id] = 0;
            }
            Pack.Clear();
            Worn.Clear();
        }

        public static Currency CurrencyOf(string id)
        {
            foreach (var currency in Currencies)
            {
                if (currency.Id == id)
                {
                    return currency;
                }
            }
            return null;
        }

        // --- Purse --------------------------------------------------------------

        public static int Amount(string currency) => Purse.TryGetValue(currency ?? "", out var value) ? value : 0;

        public static void AddCurrency(string currency, int count)
        {
            if (CurrencyOf(currency) == null || count <= 0)
            {
                return;
            }
            Purse[currency] = Amount(currency) + count;
        }

        /// <summary>Whether the purse covers a price ({currency: count}).</summary>
        public static bool CanAfford(IReadOnlyDictionary<string, int> price)
        {
            foreach (var pair in price)
            {
                if (Amount(pair.Key) < pair.Value)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Takes a price out of the purse. Returns false, and takes nothing, when it cannot be paid.</summary>
        public static bool Spend(IReadOnlyDictionary<string, int> price)
        {
            if (!CanAfford(price))
            {
                return false;
            }
            foreach (var pair in price)
            {
                Purse[pair.Key] = Amount(pair.Key) - pair.Value;
            }
            return true;
        }

        /// <summary>A price as a line: "60 light", "3 tally bones", "1 tally bone".</summary>
        public static string PriceText(IReadOnlyDictionary<string, int> price)
        {
            var parts = new List<string>();
            foreach (var pair in price)
            {
                var name = CurrencyOf(pair.Key)?.Name ?? pair.Key;
                if (pair.Value == 1 && name.EndsWith("s", StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - 1);
                }
                parts.Add($"{pair.Value} {name}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }

        // --- Items --------------------------------------------------------------

        public static int Count(string id) => Pack.TryGetValue(id ?? "", out var value) ? value : 0;

        public static bool Has(string id) => Count(id) > 0;

        public static void Add(string id, int quantity = 1)
        {
            if (!Items.Has(id) || quantity <= 0)
            {
                return;
            }
            Pack[id] = Count(id) + quantity;
        }

        /// <summary>
        /// Takes items out of the pack (unequipping the piece if the last one is worn).
        /// Returns false, and takes nothing, when there are not enough.
        /// </summary>
        public static bool Remove(string id, int quantity = 1)
        {
            if (Count(id) < quantity)
            {
                return false;
            }
            var left = Count(id) - quantity;
            if (left > 0)
            {
                Pack[id] = left;
            }
            else
            {
                Pack.Remove(id);
                var slot = Items.SlotOf(id);
                if (Worn.TryGetValue(slot, out var worn) && worn == id)
                {
                    Worn.Remove(slot);
                }
            }
            return true;
        }

        /// <summary>The ids in the pack, equipment first, then consumables, each in table order.</summary>
        public static List<string> SortedIds()
        {
            var result = new List<string>();
            foreach (var kind in new[] { ItemKind.Equipment, ItemKind.Consumable })
            {
                foreach (var def in Items.Ordered)
                {
                    if (def.Kind == kind && Has(def.Id))
                    {
                        result.Add(def.Id);
                    }
                }
            }
            return result;
        }

        // --- Equipment ----------------------------------------------------------

        /// <summary>The id worn in a slot, or "".</summary>
        public static string EquippedId(ItemSlot slot) => Worn.TryGetValue(slot, out var id) ? id : "";

        public static bool IsEquipped(string id) => EquippedId(Items.SlotOf(id)) == id;

        /// <summary>
        /// Wears a piece from the pack in its slot, taking off whatever was there.
        /// Returns false for anything that is not equipment in the pack.
        /// </summary>
        public static bool Equip(string id)
        {
            if (!Has(id) || !Items.IsEquipment(id))
            {
                return false;
            }
            Worn[Items.SlotOf(id)] = id;
            return true;
        }

        public static void Unequip(ItemSlot slot) => Worn.Remove(slot);

        /// <summary>The worn pieces' additive stat, summed (0 when nothing worn gives it).</summary>
        public static float Bonus(string stat)
        {
            var total = 0f;
            foreach (var id in Worn.Values)
            {
                total += Items.Stat(id, stat, 0f);
            }
            return total;
        }

        /// <summary>The worn pieces' multiplying stat, multiplied (1 when nothing changes it).</summary>
        public static float Factor(string stat)
        {
            var total = 1f;
            foreach (var id in Worn.Values)
            {
                total *= Items.Stat(id, stat, 1f);
            }
            return total;
        }

        // --- Save ---------------------------------------------------------------

        /// <summary>The pack as the save keeps it: {"purse": {...}, "items": {...}, "equipped": {"0": id}}.</summary>
        public static JObject ToJson()
        {
            var purse = new JObject();
            foreach (var pair in Purse)
            {
                purse[pair.Key] = pair.Value;
            }
            var items = new JObject();
            foreach (var pair in Pack)
            {
                items[pair.Key] = pair.Value;
            }
            var worn = new JObject();
            foreach (var pair in Worn)
            {
                worn[((int)pair.Key).ToString()] = pair.Value;
            }
            return new JObject { ["purse"] = purse, ["items"] = items, ["equipped"] = worn };
        }

        /// <summary>
        /// Replaces the pack with the one from a save; anything the table no longer
        /// knows is dropped, and a worn piece that is not in the pack is taken off.
        /// </summary>
        public static void FromJson(JObject data)
        {
            Reset();
            if (data == null)
            {
                return;
            }
            if (data["purse"] is JObject savedPurse)
            {
                foreach (var currency in Currencies)
                {
                    Purse[currency.Id] = Math.Max(savedPurse.Value<int?>(currency.Id) ?? 0, 0);
                }
            }
            if (data["items"] is JObject savedItems)
            {
                foreach (var property in savedItems.Properties())
                {
                    Add(property.Name, property.Value.Type == JTokenType.Integer ? property.Value.Value<int>() : 0);
                }
            }
            if (data["equipped"] is JObject worn)
            {
                foreach (var property in worn.Properties())
                {
                    var id = property.Value.ToString();
                    if (int.TryParse(property.Name, out var slot) && (int)Items.SlotOf(id) == slot)
                    {
                        Equip(id);
                    }
                }
            }
        }
    }
}
