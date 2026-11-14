using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Descender.Combat;
using Descender.Economy;
using Descender.Enemies;
using Descender.Pickups;
using Descender.Player;
using Descender.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Foes in the regions: a placement whose character is registered makes a foe from
    /// the kind's recipe, feet at the placement and facing as it says, and one that is
    /// not is left to the catalog; its death is remembered, it leaves its drops, its
    /// object goes, and the placement makes another when the kills are forgotten; a foe
    /// made any other way leaves nothing and stays.
    /// </summary>
    public class FoeWorldTests
    {
        private const float Pixel = 1f / PlayerTuning.PixelsPerUnit;

        private readonly List<GameObject> made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Inventory.Reset();
            Skills.Reset();
            Kills.Reset();
            FoeWorld.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.Destroy(thing);
                }
            }
            made.Clear();
            foreach (var mote in Object.FindObjectsByType<HealMote>())
            {
                Object.Destroy(mote.gameObject);
            }
            foreach (var light in Object.FindObjectsByType<LightDrop>())
            {
                Object.Destroy(light.gameObject);
            }
            Kills.Reset();
            FoeWorld.Reset();
        }

        /// <summary>A placement from its fields as JSON (the serialized names); it spawns on its Start, a frame later.</summary>
        private WorldPlacement Place(Vector2 at, string json)
        {
            var thing = new GameObject("Placement");
            made.Add(thing);
            thing.SetActive(false);
            thing.transform.position = at;
            var placement = thing.AddComponent<WorldPlacement>();
            JsonUtility.FromJsonOverwrite(json, placement);
            thing.SetActive(true);
            return placement;
        }

        /// <summary>A catalog whose "enemy:imp" is a walker of a profile; the template stays switched off.</summary>
        private PlacementCatalog CatalogOfAWalker(WalkerProfile profile)
        {
            var template = new GameObject("imp");
            made.Add(template);
            template.SetActive(false);
            template.AddComponent<BoxCollider2D>();
            var walker = template.AddComponent<Walker>();
            var field = typeof(Walker).GetField("profile", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(walker, profile);
            var catalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            catalog.Set("enemy:imp", template);
            return catalog;
        }

        /// <summary>A catalog whose "enemy:shade" is a flyer of a profile and whose "enemy:hanged" is a hanger.</summary>
        private PlacementCatalog CatalogOfAFlyerAndAHanger(FlyerProfile profile)
        {
            var shade = new GameObject("shade");
            made.Add(shade);
            shade.SetActive(false);
            shade.AddComponent<BoxCollider2D>();
            var flyer = shade.AddComponent<Flyer>();
            var field = typeof(Flyer).GetField("profile", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(flyer, profile);
            var hanged = new GameObject("hanged");
            made.Add(hanged);
            hanged.SetActive(false);
            hanged.AddComponent<BoxCollider2D>();
            hanged.AddComponent<Hanger>();
            var catalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            catalog.Set("enemy:shade", shade);
            catalog.Set("enemy:hanged", hanged);
            return catalog;
        }

        private WorldPlacement Place(Vector2 at, string json, PlacementCatalog catalog)
        {
            var thing = new GameObject("Placement");
            made.Add(thing);
            thing.SetActive(false);
            thing.transform.position = at;
            var placement = thing.AddComponent<WorldPlacement>();
            JsonUtility.FromJsonOverwrite(json, placement);
            typeof(WorldPlacement)
                .GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(placement, catalog);
            thing.SetActive(true);
            return placement;
        }

        /// <summary>A body that hangs where it is put, so the tests need no floor.</summary>
        private static FoeRecipe Hanging(FoeDrops drops = null)
        {
            var body = new FoeBody
            {
                size = new Vector2(16f, 24f),
                health = 1,
                contactDamage = 0,
                flies = true,
            };
            var recipe = new FoeRecipe("imp", body);
            recipe.drops = drops;
            return recipe;
        }

        [UnityTest]
        public IEnumerator APlacementOfARegisteredCharacterMakesAFoeAtItsFeetFacingAsItSays()
        {
            WorldPlacement asked = null;
            FoeRecipe Make(WorldPlacement placement)
            {
                asked = placement;
                return Hanging();
            }
            FoeWorld.Register("imp", Make);
            var json = "{\"id\":\"test/Imp\",\"type\":\"enemy\",\"character\":\"imp\",";
            json += "\"behaviour\":\"chase\",\"facesLeft\":true}";
            var imp = Place(new Vector2(5f, 3f), json);
            var slug = Place(new Vector2(9f, 3f), "{\"id\":\"test/Slug\",\"type\":\"enemy\",\"character\":\"slug\"}");
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.That(FoeWorld.Has("imp"), Is.True);
            Assert.That(FoeWorld.Has("slug"), Is.False);
            Assert.That(imp.Spawned != null, Is.True, "the registered character stands");
            Assert.That(asked, Is.SameAs(imp), "made from what its placement says");
            Assert.That(asked.Behaviour, Is.EqualTo("chase"));
            var foe = imp.Spawned.GetComponent<Foe>();
            Assert.That(foe != null, Is.True, "as a foe");
            Assert.That(foe.transform.parent, Is.SameAs(imp.transform));
            Assert.That(foe.PlacementId, Is.EqualTo("test/Imp"));
            Assert.That(foe.Facing, Is.EqualTo(-1), "facing as the placement says");
            Assert.That(foe.Box.bounds.min.y, Is.EqualTo(3f).Within(Pixel), "its feet where the placement is");
            Assert.That(foe.Box.bounds.center.x, Is.EqualTo(5f).Within(Pixel));
            Assert.That(foe.Box.bounds.size.y, Is.EqualTo(1.5f).Within(Pixel), "the size its body says");
            Assert.That(slug.Spawned == null, Is.True, "any other character is left to the catalog");
        }

        [UnityTest]
        public IEnumerator ItsDeathIsRememberedItLeavesItsDropsAndThePlacementMakesAnotherWhenTheKillsAreForgotten()
        {
            var drops = new FoeDrops { moteChance = 1f, powerChance = 0f };
            drops.loot.LightMin = 2;
            drops.loot.LightMax = 2;
            FoeWorld.Register("imp", placement => Hanging(drops));
            var imp = Place(new Vector2(5f, 3f), "{\"id\":\"test/Imp\",\"type\":\"enemy\",\"character\":\"imp\"}");
            yield return null;
            yield return new WaitForFixedUpdate();
            var foe = imp.Spawned.GetComponent<Foe>();
            var motes = HealMote.Count;
            IEnemy told = null;
            foe.Died += dead => told = dead;

            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, new Vector2(4f, 3f))), Is.True);
            Assert.That(foe.IsDead, Is.True);
            Assert.That(told, Is.SameAs(foe));
            Assert.That(Kills.IsDead("test/Imp"), Is.True, "its death is remembered");
            Assert.That(Loot.LastDropCount, Is.GreaterThan(0), "it leaves its loot");
            Assert.That(HealMote.Count, Is.EqualTo(motes + 1), "and its mote");
            yield return null;
            Assert.That(imp.Spawned == null, Is.True, "its object goes");
            imp.Spawn();
            Assert.That(imp.Spawned == null, Is.True, "and it stays dead");

            Kills.Clear();
            Assert.That(imp.Spawned != null, Is.True, "until the kills are forgotten");
            var again = imp.Spawned.GetComponent<Foe>();
            Assert.That(again, Is.Not.SameAs(foe));
            Assert.That(again.IsDead, Is.False);
            Assert.That(again.Health, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator APlacementWhosePrefabIsAWalkerMakesAFoeFromItsProfileUnlessThatIsOff()
        {
            var profile = ScriptableObject.CreateInstance<WalkerProfile>();
            profile.body = new Vector2(20f, 36f);
            profile.health = 4;
            profile.speed = 22f;
            var catalog = CatalogOfAWalker(profile);
            var json = "{\"id\":\"test/Imp\",\"type\":\"enemy\",\"character\":\"imp\",\"behaviour\":\"chase\"}";
            var remade = Place(new Vector2(5f, 3f), json, catalog);
            yield return null;

            Assert.That(remade.Spawned != null, Is.True);
            Assert.That(remade.Spawned.GetComponent<Walker>() == null, Is.True, "no walker stands there");
            var foe = remade.Spawned.GetComponent<Foe>();
            Assert.That(foe != null, Is.True, "a foe does");
            Assert.That(foe.name, Is.EqualTo("imp"));
            Assert.That(foe.Health, Is.EqualTo(4), "with the profile's numbers");
            Assert.That(foe.Body.size, Is.EqualTo(new Vector2(20f, 36f)));
            Assert.That(foe.Body.speed, Is.EqualTo(22f));
            Assert.That(foe.Behaviours[1], Is.InstanceOf<Chase>(), "and the placement's habit");
            Assert.That(foe.Drops, Is.Not.Null);
            Assert.That(foe.PlacementId, Is.EqualTo("test/Imp"));

            FoeWorld.Remakes = false;
            var old = Place(new Vector2(9f, 3f), json.Replace("test/Imp", "test/Old"), catalog);
            yield return null;
            Assert.That(old.Spawned != null, Is.True);
            Assert.That(old.Spawned.GetComponent<Foe>() == null, Is.True, "off, the catalog's prefab stands");
            Assert.That(old.Spawned.GetComponent<Walker>() != null, Is.True);
            Object.Destroy(catalog);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator APlacementWhosePrefabIsAFlyerOrAHangerMakesAFlyingFoe()
        {
            var profile = ScriptableObject.CreateInstance<FlyerProfile>();
            profile.health = 2;
            // It keeps still, so where it stands can be read.
            profile.hover = 0f;
            var catalog = CatalogOfAFlyerAndAHanger(profile);
            var shadeJson = "{\"id\":\"test/Shade\",\"type\":\"enemy\",\"character\":\"shade\",";
            shadeJson += "\"behaviour\":\"dive\"}";
            var shade = Place(new Vector2(5f, 8f), shadeJson, catalog);
            var hangedJson = "{\"id\":\"test/Hanged\",\"type\":\"enemy\",\"character\":\"hanged\"}";
            var hanged = Place(new Vector2(12f, 8f), hangedJson, catalog);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(shade.Spawned.GetComponent<Flyer>() == null, Is.True, "no flyer stands there");
            var flier = shade.Spawned.GetComponent<Foe>();
            Assert.That(flier != null, Is.True, "a foe does");
            Assert.That(flier.Body.flies, Is.True);
            Assert.That(flier.Health, Is.EqualTo(2));
            Assert.That(flier.Behaviours[0], Is.InstanceOf<Dive>(), "with the placement's habit");
            Vector2 middle = flier.Box.bounds.center;
            Assert.That(middle.x, Is.EqualTo(5f).Within(Pixel), "its middle where the placement is");
            Assert.That(middle.y, Is.EqualTo(8f).Within(Pixel));

            Assert.That(hanged.Spawned.GetComponent<Hanger>() == null, Is.True, "no hanger hangs there");
            var hung = hanged.Spawned.GetComponent<Foe>();
            Assert.That(hung != null, Is.True, "a foe does");
            Assert.That(hung.Body.flies, Is.True);
            Assert.That(hung.Behaviours[0], Is.InstanceOf<Dive>());
            Assert.That(hung.Box.bounds.min.y, Is.EqualTo(8f).Within(Pixel), "its feet where the placement is");
            Assert.That(hung.Box.bounds.center.x, Is.EqualTo(12f).Within(Pixel));
            Object.Destroy(catalog);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator AFoeMadeAnyOtherWayLeavesNothingAndStays()
        {
            var foe = Foe.Make(Hanging(), null, new Vector2(5f, 3f), new Vector2(1f, 1.5f));
            made.Add(foe.gameObject);
            yield return new WaitForFixedUpdate();
            var motes = HealMote.Count;
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 1, new Vector2(4f, 3f))), Is.True);
            Assert.That(foe.IsDead, Is.True);
            yield return null;
            Assert.That(foe != null, Is.True, "it stays for whoever keeps it");
            Assert.That(foe.gameObject.activeSelf, Is.False, "switched off");
            Assert.That(HealMote.Count, Is.EqualTo(motes), "and leaves nothing");
            Assert.That(Kills.Count, Is.EqualTo(0));
        }
    }
}
