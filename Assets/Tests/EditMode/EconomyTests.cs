using System.Collections.Generic;
using Descender.Economy;
using Descender.Story;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Descender.Tests
{
    public class EconomyTests
    {
        [SetUp]
        public void Reset()
        {
            StoryFlags.Reset();
            Inventory.Reset();
            Skills.Reset();
        }

        [Test]
        public void EveryItemHasKnownStatsALineAndASlotWhereItNeedsOne()
        {
            foreach (var def in Items.Ordered)
            {
                Assert.IsNotEmpty(def.Text, def.Id);
                Assert.IsNotEmpty(def.Name, def.Id);
                if (def.Kind == ItemKind.Equipment)
                {
                    Assert.AreNotEqual(ItemSlot.None, def.Slot, def.Id);
                }
                foreach (var stat in def.Stats.Keys)
                {
                    var known =
                        System.Array.IndexOf(Items.AdditiveStats, stat) >= 0
                        || System.Array.IndexOf(Items.FactorStats, stat) >= 0;
                    Assert.IsTrue(known, $"{def.Id}: unknown stat {stat}");
                }
            }
            Assert.AreEqual(Items.Ordered.Length, Items.Table.Count, "ids are unique");
        }

        [Test]
        public void SummariesReadAsTheGodotOnesDid()
        {
            Assert.AreEqual("+1 damage", Items.Summary("iron_sword"));
            Assert.AreEqual("reach x1.4", Items.Summary("hinge_blade"));
            Assert.AreEqual("+1 guard, slower feet", Items.Summary("quilted_coat"));
            Assert.AreEqual("dash back x2 sooner", Items.Summary("velvet_gloves"));
            Assert.AreEqual("prices x0.6", Items.Summary("vault_seal"));
            Assert.AreEqual("", Items.Summary("flask"));
        }

        [Test]
        public void ThePurseSpendsOnlyWhatItHolds()
        {
            Inventory.AddCurrency("light", 50);
            Inventory.AddCurrency("light", -5);
            Inventory.AddCurrency("gold", 5);
            Assert.AreEqual(50, Inventory.Amount("light"));
            Assert.AreEqual(0, Inventory.Amount("gold"));
            var price = new Dictionary<string, int> { ["light"] = 60 };
            Assert.IsFalse(Inventory.Spend(price));
            Assert.AreEqual(50, Inventory.Amount("light"));
            price["light"] = 20;
            Assert.IsTrue(Inventory.Spend(price));
            Assert.AreEqual(30, Inventory.Amount("light"));
            Assert.AreEqual("1 tally bone", Inventory.PriceText(new Dictionary<string, int> { ["bones"] = 1 }));
            Assert.AreEqual("3 tally bones", Inventory.PriceText(new Dictionary<string, int> { ["bones"] = 3 }));
            Assert.AreEqual("free", Inventory.PriceText(new Dictionary<string, int>()));
        }

        [Test]
        public void ThePackWearsOnePieceASlot()
        {
            Inventory.Add("iron_sword");
            Inventory.Add("hinge_blade");
            Inventory.Add("flask", 2);
            Inventory.Add("nope");
            Assert.IsTrue(Inventory.Equip("iron_sword"));
            Assert.IsTrue(Inventory.IsEquipped("iron_sword"));
            Assert.AreEqual(1f, Inventory.Bonus("damage"));
            Assert.IsTrue(Inventory.Equip("hinge_blade"));
            Assert.IsFalse(Inventory.IsEquipped("iron_sword"));
            Assert.AreEqual(1.35f, Inventory.Factor("reach"), 0.0001f);
            Assert.IsFalse(Inventory.Equip("flask"));
            Assert.IsTrue(Inventory.Remove("hinge_blade"));
            Assert.AreEqual("", Inventory.EquippedId(ItemSlot.Weapon));
            Assert.IsFalse(Inventory.Remove("flask", 3));
            CollectionAssert.AreEqual(new[] { "iron_sword", "flask" }, Inventory.SortedIds());
        }

        [Test]
        public void ThePackGoesThroughASave()
        {
            Inventory.AddCurrency("light", 12);
            Inventory.Add("scrap_mail");
            Inventory.Equip("scrap_mail");
            var saved = JObject.Parse(Inventory.ToJson().ToString());
            Inventory.Reset();
            Inventory.FromJson(saved);
            Assert.AreEqual(12, Inventory.Amount("light"));
            Assert.IsTrue(Inventory.IsEquipped("scrap_mail"));
            Inventory.FromJson(
                JObject.Parse(
                    @"{""purse"": {""light"": -4}, ""items"": {""ghost"": 1}, ""equipped"": {""0"": ""scrap_mail""}}"
                )
            );
            Assert.AreEqual(0, Inventory.Amount("light"));
            Assert.IsFalse(Inventory.Has("ghost"));
            Assert.AreEqual(
                "",
                Inventory.EquippedId(ItemSlot.Weapon),
                "a worn piece that is not in the pack is taken off"
            );
        }

        [Test]
        public void TradersSellEquipmentOnceAndScalePrices()
        {
            var pell = Shops.Get("pell");
            Assert.IsNotNull(pell);
            foreach (var shop in Shops.List)
            {
                foreach (var ware in shop.Stock)
                {
                    Assert.IsTrue(Items.Has(ware.Item), $"{shop.Id} sells unknown {ware.Item}");
                }
            }
            var sword = pell.Stock[0];
            Assert.IsTrue(Shops.Buy("pell", sword).StartsWith("Not enough"));
            Inventory.AddCurrency("light", 500);
            Assert.AreEqual("", Shops.Buy("pell", sword));
            Assert.IsTrue(Inventory.Has("iron_sword"));
            Assert.AreEqual("Sold", Shops.Buy("pell", sword));
            Inventory.Add("vault_seal");
            Inventory.Equip("vault_seal");
            Assert.AreEqual(54, Shops.PriceOf(pell.Stock[1])["light"], "90 x 0.6");
            Assert.AreEqual(1, Shops.PriceOf(Shops.Get("vael").Stock[0])["bones"], "never below one");
        }

        [Test]
        public void TheLootRollFollowsTheTable()
        {
            var table = new LootTable
            {
                LightMin = 3,
                LightMax = 5,
                BoneChance = 0.5f,
                FlaskChance = 0.1f,
                Unique = "wardens_halo",
                Xp = 3,
            };
            var drops = table.Roll(() => 0.2f, (lo, hi) => hi);
            Assert.AreEqual(3, drops.Count);
            Assert.AreEqual(5, drops[0].Amount);
            Assert.AreEqual("bones", drops[1].Currency);
            Assert.AreEqual("wardens_halo", drops[2].ItemId);
            StoryFlags.Set(Items.LootFlag("wardens_halo"));
            drops = table.Roll(() => 0.9f, (lo, hi) => lo);
            Assert.AreEqual(1, drops.Count, "no bone, no flask, the unique already taken");
            Inventory.Add("lords_tally");
            Inventory.Equip("lords_tally");
            Assert.AreEqual(6, table.Roll(() => 0.9f, (lo, hi) => lo)[0].Amount, "charms scale the light");
            Assert.AreEqual(0f, LootTable.OffsetOf(1, 3));
            Assert.AreEqual(-LootTable.Spread, LootTable.OffsetOf(0, 3));
        }

        [Test]
        public void LevelsRiseAndPointsAreSpent()
        {
            Assert.AreEqual(1, Skills.Level());
            Assert.AreEqual(0, Skills.PointsLeft());
            Assert.AreEqual(Skills.XpBase, Skills.XpForLevel(2));
            for (var level = 1; level < Skills.MaxLevel - 1; level++)
            {
                Assert.Greater(Skills.XpToNext(level + 1), Skills.XpToNext(level));
            }
            Assert.AreEqual(0, Skills.XpToNext(Skills.MaxLevel));
            Assert.AreEqual(1, Skills.AddXp(Skills.XpBase));
            Assert.AreEqual(2, Skills.Level());
            Assert.AreEqual(1, Skills.PointsLeft());
            Assert.AreEqual((0, Skills.XpToNext(2)), Skills.LevelProgress());
            Assert.IsNotEmpty(Skills.Raise("mender"), "a skill above the level is closed");
            Assert.AreEqual("", Skills.Raise("vigour"));
            Assert.AreEqual(1f, Skills.Bonus("hearts"));
            Assert.IsTrue(Skills.Raise("edge").StartsWith("No skill point"));
            Skills.AddXp(Skills.XpForLevel(Skills.MaxLevel) * 2);
            Assert.AreEqual(Skills.MaxLevel, Skills.Level());
            Skills.Raise("swift_feet");
            Skills.Raise("swift_feet");
            Assert.AreEqual(1.08f * 1.08f, Skills.Factor("speed"), 0.0001f);
            Skills.Raise("vigour");
            Skills.Raise("vigour");
            StringAssert.Contains("to the end", Skills.Raise("vigour"));
            Assert.AreEqual("+1 heart per rank", Skills.Summary("vigour"));
            Assert.AreEqual("feet x1.08 per rank", Skills.Summary("swift_feet"));
        }

        [Test]
        public void SkillsGoThroughASaveAndOverspendingIsGivenBack()
        {
            Skills.AddXp(Skills.XpForLevel(5));
            Skills.Raise("vigour");
            Skills.Raise("edge");
            var saved = JObject.Parse(Skills.ToJson().ToString());
            Skills.Reset();
            Skills.FromJson(saved);
            Assert.AreEqual(5, Skills.Level());
            Assert.AreEqual(1, Skills.Rank("vigour"));
            Skills.FromJson(JObject.Parse(@"{""xp"": 0, ""ranks"": {""vigour"": 3, ""nope"": 1}}"));
            Assert.AreEqual(0, Skills.PointsLeft());
            Assert.AreEqual(0, Skills.Rank("nope"));
            Assert.AreEqual(0, Skills.Rank("vigour"));
        }
    }
}
