using System.Collections.Generic;
using Descender.EditorTools.World;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// What a placement spawns: an item with dialogue is a relic to examine, one
    /// without is loot, and both catalogs (the shared one and the garden's copy)
    /// hold a prefab with the right receiver for every placement type.
    /// </summary>
    public class PlacementCatalogTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made)
            {
                Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        private GameObject Thing(string name)
        {
            var thing = new GameObject(name);
            made.Add(thing);
            return thing;
        }

        [Test]
        public void AnItemWithDialogueResolvesToTheRelic()
        {
            var catalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            made.Add(catalog);
            var loot = Thing("Loot");
            var relic = Thing("Relic");
            catalog.Set("item", loot);
            Assert.That(catalog.Resolve("item", "", true), Is.SameAs(loot), "no relic entry: the item stands in");
            catalog.Set("relic", relic);
            Assert.That(catalog.Resolve("item", "", true), Is.SameAs(relic));
            Assert.That(catalog.Resolve("item", "", false), Is.SameAs(loot));
            Assert.That(catalog.Resolve("item", ""), Is.SameAs(loot));
            Assert.That(catalog.Resolve("trigger", "", true), Is.Null, "dialogue only changes what an item is");

            var shared = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(PlacementCatalogSetup.CatalogPath);
            Assert.That(shared, Is.Not.Null, PlacementCatalogSetup.CatalogPath + " is missing");
            Assert.That(Receiver(shared.Resolve("item", "", true)), Is.InstanceOf<UI.Relic>());
            Assert.That(Receiver(shared.Resolve("item", "", false)), Is.InstanceOf<Pickups.LootDrop>());
        }

        [TestCase(PlacementCatalogSetup.CatalogPath, "Checkpoint")]
        [TestCase(PlacementCatalogSetup.ParadiseCatalogPath, "Checkpoint Garden")]
        public void BothCatalogsResolveEveryPlacementType(string path, string savePrefab)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(path);
            Assert.That(catalog, Is.Not.Null, path + " is missing");
            Assert.That(catalog.Find("save"), Is.Not.Null, $"{path} has no save");
            Assert.That(catalog.Find("save").name, Is.EqualTo(savePrefab));
            Expect<Pickups.Checkpoint>(catalog, path, "save", catalog.Resolve("save", ""));
            Expect<Pickups.AbilityPickup>(catalog, path, "ability", catalog.Resolve("ability", ""));
            Expect<Pickups.UpgradePickup>(catalog, path, "upgrade", catalog.Resolve("upgrade", ""));
            Expect<Pickups.PowerUp>(catalog, path, "power", catalog.Resolve("power", ""));
            Expect<Pickups.LootDrop>(catalog, path, "item", catalog.Resolve("item", "", false));
            Expect<UI.Relic>(catalog, path, "relic", catalog.Resolve("item", "", true));
            Expect<UI.StoryTrigger>(catalog, path, "trigger", catalog.Resolve("trigger", "", true));
            // No npc:<character> prefab exists, so a named character falls back to the plain NPC.
            Expect<UI.Npc>(catalog, path, "npc", catalog.Resolve("npc", "pell", true));
            Expect<Enemies.Boss>(catalog, path, "boss", catalog.Resolve("enemy", Enemies.BossIds.All[0]));
        }

        private static void Expect<T>(PlacementCatalog catalog, string path, string key, GameObject resolved)
            where T : class, IPlacementReceiver
        {
            Assert.That(catalog.Find(key), Is.Not.Null, $"{path} has no '{key}' entry");
            Assert.That(resolved, Is.Not.Null, $"{path} resolves nothing for '{key}'");
            Assert.That(Receiver(resolved), Is.InstanceOf<T>(), $"{path}: the '{key}' prefab has the wrong receiver");
        }

        private static IPlacementReceiver Receiver(GameObject prefab)
        {
            return prefab != null ? prefab.GetComponentInChildren<IPlacementReceiver>(true) : null;
        }
    }
}
