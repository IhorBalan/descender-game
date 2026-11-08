using System.Collections.Generic;
using System.Linq;
using Descender.EditorTools.Paradise;
using Descender.EditorTools.World;
using Descender.World;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Descender.Tests
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Sync placements from layout, on a scene made here from a
    /// small layout: it adds the placements that are missing with the catalog the scene's
    /// saves use, puts back what was moved, removes the markers the placements replace,
    /// touches nothing else, and a second run changes nothing.
    /// </summary>
    public class ParadisePlacementSyncTests
    {
        private Scene scene;
        private PlacementCatalog garden;
        private PlacementCatalog shared;

        [SetUp]
        public void MakeScene()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            garden = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(PlacementCatalogSetup.ParadiseCatalogPath);
            shared = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(PlacementCatalogSetup.CatalogPath);
            Assert.That(garden, Is.Not.Null, PlacementCatalogSetup.ParadiseCatalogPath + " is missing");
            Assert.That(shared, Is.Not.Null, PlacementCatalogSetup.CatalogPath + " is missing");
        }

        [TearDown]
        public void LeaveAnEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>A zone with a trigger, an NPC and a relic; the first two replace markers.</summary>
        private static ParadiseLayout.Zone SmallZone()
        {
            var zone = new ParadiseLayout.Zone
            {
                number = 9,
                scene = "Test_Z9",
                xMin = 0f,
                xMax = 100f,
            };
            zone.Mark(DesignMarker.Kind.Story, 10f, -58f, "Beat A: voice");
            zone.Mark(DesignMarker.Kind.Npc, 40f, -58f, "Beat B: keeper");
            zone.Mark(DesignMarker.Kind.Secret, 70f, -58f, "Secret: kept");
            zone.Place("voice", "trigger", 10f, -60f, "Beat A: voice").dialogue = new[] { "opening" };
            var keeper = zone.Place("keeper", "npc", 40f, -60f, "Beat B: keeper");
            keeper.placementName = "Keeper";
            keeper.character = "mereth";
            keeper.facesLeft = true;
            keeper.dialogue = new[] { "mereth_intro" };
            keeper.rotate = new[] { "mereth_again", "mereth_count" };
            var relic = zone.Place("relic", "item", 60f, -60f);
            relic.item = "token";
            relic.dialogue = new[] { "item_token" };
            return zone;
        }

        /// <summary>The scene as BuildZone leaves it before placements: groups, a save, the markers, some ground.</summary>
        private static (Transform placements, Transform markers, GameObject ground) Dress(
            ParadiseLayout.Zone zone,
            PlacementCatalog saveCatalog
        )
        {
            var root = new GameObject($"Zone{zone.number}").transform;
            var placements = new GameObject("Placements").transform;
            placements.SetParent(root, false);
            var markers = new GameObject("Markers").transform;
            markers.SetParent(root, false);
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(5f, -61f, 0f);
            if (saveCatalog != null)
            {
                var save = new GameObject("Save0");
                save.transform.SetParent(placements, false);
                save.transform.position = new Vector3(20f, -60f, 0f);
                var serialized = new SerializedObject(save.AddComponent<WorldPlacement>());
                serialized.FindProperty("id").stringValue = $"paradise/z{zone.number}/save0";
                serialized.FindProperty("type").stringValue = "save";
                serialized.FindProperty("catalog").objectReferenceValue = saveCatalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var marker in zone.markers)
            {
                var go = new GameObject(marker.label);
                go.transform.SetParent(markers, false);
                go.transform.position = marker.at;
                go.AddComponent<DesignMarker>().Configure(marker.kind, marker.label, marker.note);
                new GameObject("Flag").transform.SetParent(go.transform, false);
            }
            return (placements, markers, ground);
        }

        private List<WorldPlacement> Placements()
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldPlacement>(true))
                .ToList();
        }

        private static PlacementCatalog CatalogOf(WorldPlacement placement)
        {
            return new SerializedObject(placement).FindProperty("catalog").objectReferenceValue as PlacementCatalog;
        }

        /// <summary>Every object in the scene by path, with where it stands: what "nothing else changed" compares.</summary>
        private Dictionary<string, Vector3> Snapshot()
        {
            var all = new Dictionary<string, Vector3>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    all[root.name + "/" + AnimationUtility.CalculateTransformPath(transform, root.transform)] =
                        transform.position;
                }
            }
            return all;
        }

        [Test]
        public void SyncAddsMissingPlacementsAndIsIdempotent()
        {
            var zone = SmallZone();
            var (group, markers, ground) = Dress(zone, garden);
            var before = Snapshot();

            Assert.That(ParadiseGreybox.SyncZonePlacements(zone, scene, shared), Is.True, "the first sync adds");

            var placements = Placements();
            Assert.That(placements.Count, Is.EqualTo(4), "the save and the three beats");
            foreach (var declared in zone.placements)
            {
                var placed = placements.Find(p => p.Id == declared.id);
                Assert.That(placed, Is.Not.Null, $"{declared.id} was not added");
                Assert.That(placed.transform.parent, Is.SameAs(group), $"{declared.id} stands beside the saves");
                Assert.That(placed.gameObject.name, Is.EqualTo(declared.Name));
                Assert.That((Vector2)placed.transform.position, Is.EqualTo(declared.at));
                Assert.That(placed.Type, Is.EqualTo(declared.type));
                Assert.That(placed.Dialogue, Is.EqualTo(declared.dialogue));
                Assert.That(placed.Rotate, Is.EqualTo(declared.rotate));
                Assert.That(placed.PlacementName, Is.EqualTo(declared.placementName));
                Assert.That(placed.Character, Is.EqualTo(declared.character));
                Assert.That(placed.FacesLeft, Is.EqualTo(declared.facesLeft));
                Assert.That(placed.Item, Is.EqualTo(declared.item));
                Assert.That(placed.AbilityIndex, Is.EqualTo(declared.abilityIndex));
                Assert.That(placed.Unless, Is.EqualTo(declared.unless));
                var catalog = CatalogOf(placed);
                Assert.That(catalog, Is.SameAs(garden), $"{declared.id} takes the catalog of the scene's saves");
                Assert.That(
                    catalog.Resolve(placed.Type, placed.Character, placed.Dialogue.Length > 0),
                    Is.Not.Null,
                    $"{declared.id}: the catalog resolves no prefab for '{placed.Type}'"
                );
            }

            // The markers the placements replace are gone; the rest of the scene is as it was.
            var labels = markers.GetComponentsInChildren<DesignMarker>(true).Select(m => m.Label).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "Secret: kept" }));
            var after = Snapshot();
            foreach (var entry in before)
            {
                if (entry.Key.Contains("Beat A: voice") || entry.Key.Contains("Beat B: keeper"))
                {
                    Assert.That(after.ContainsKey(entry.Key), Is.False, $"'{entry.Key}' should be gone");
                    continue;
                }
                Assert.That(after.ContainsKey(entry.Key), $"'{entry.Key}' was removed");
                Assert.That(after[entry.Key], Is.EqualTo(entry.Value), $"'{entry.Key}' was moved");
            }
            Assert.That(
                after.Count,
                Is.EqualTo(before.Count - 4 + 3),
                "two markers with a flag each out, three placements in"
            );
            Assert.That(ground.transform.position, Is.EqualTo(new Vector3(5f, -61f, 0f)));

            Assert.That(
                ParadiseGreybox.SyncZonePlacements(zone, scene, shared),
                Is.False,
                "a second sync changes nothing"
            );
            Assert.That(Snapshot(), Is.EqualTo(after));
        }

        [Test]
        public void SyncPutsAMovedOrEditedPlacementBack()
        {
            var zone = SmallZone();
            Dress(zone, garden);
            ParadiseGreybox.SyncZonePlacements(zone, scene, shared);

            var keeper = Placements().Find(p => p.Id == "paradise/z9/keeper");
            keeper.transform.position = new Vector3(45f, -50f, 0f);
            var serialized = new SerializedObject(keeper);
            serialized.FindProperty("placementName").stringValue = "Someone";
            serialized.FindProperty("rotate").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(ParadiseGreybox.SyncZonePlacements(zone, scene, shared), Is.True, "the layout wins");
            Assert.That(Placements().Count, Is.EqualTo(4), "updated in place, not added again");
            Assert.That((Vector2)keeper.transform.position, Is.EqualTo(new Vector2(40f, -60f)));
            Assert.That(keeper.PlacementName, Is.EqualTo("Keeper"));
            Assert.That(keeper.Rotate, Is.EqualTo(new[] { "mereth_again", "mereth_count" }));
            Assert.That(CatalogOf(keeper), Is.SameAs(garden), "an update leaves the catalog alone");
            Assert.That(ParadiseGreybox.SyncZonePlacements(zone, scene, shared), Is.False);
        }

        [Test]
        public void ASceneWithoutASaveTakesTheSharedCatalog()
        {
            var zone = SmallZone();
            var (group, _, _) = Dress(zone, null);

            Assert.That(ParadiseGreybox.SyncZonePlacements(zone, scene, shared), Is.True);

            var placements = Placements();
            Assert.That(placements.Count, Is.EqualTo(3));
            foreach (var placed in placements)
            {
                Assert.That(CatalogOf(placed), Is.SameAs(shared));
                Assert.That(placed.transform.parent, Is.SameAs(group), "the zone's Placements group is found by name");
            }
        }
    }
}
