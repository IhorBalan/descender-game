using System.Collections.Generic;
using System.IO;
using Descender.EditorTools.Paradise;
using Descender.Save;
using Descender.Story;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The Paradise layout as data (ParadiseLayout): every zone names a main path made of
    /// things it really has, and the zones tile the region from its west edge.
    /// </summary>
    public class ParadiseLayoutTests
    {
        [Test]
        public void EveryZoneDeclaresAMainPathOfKnownNames()
        {
            var zones = ParadiseLayout.All();
            foreach (var zone in zones)
            {
                Assert.That(zone.mainPath, Is.Not.Empty, $"zone {zone.number} declares no main path");
                var known = WalkableNames(zone);
                foreach (var name in zone.mainPath)
                {
                    Assert.That(
                        known.ContainsKey(name),
                        $"zone {zone.number}: '{name}' is no ground, deck, ledge or pod"
                    );
                    Assert.That(known[name], Is.EqualTo(1), $"zone {zone.number}: the name '{name}' is used twice");
                }
            }

            Assert.That(
                zones[0].mainPath,
                Is.EqualTo(
                    new[]
                    {
                        "Hollow",
                        "Steps",
                        "RisingArcade",
                        "BrokenSpan1",
                        "BrokenSpan2",
                        "BrokenSpan3",
                        "ShrineTerrace",
                    }
                )
            );
            Assert.That(
                zones[1].mainPath,
                Is.EqualTo(
                    new[] { "Grove", "OrchardRows", "DellWallPod", "LanternRise", "Run1", "Run2", "Run3", "ExitClimb" }
                )
            );
            var dellWallPod = zones[1].pods.Find(pod => pod.name == "DellWallPod");
            Assert.That(
                dellWallPod.at.x,
                Is.EqualTo(853f),
                "the main path crosses the dell's east wall by the pod at x 853"
            );
            Assert.That(dellWallPod.tiles, Is.EqualTo(14f));
        }

        [Test]
        public void ZonesTileTheRegionWithoutGapsOrOverlaps()
        {
            var zones = ParadiseLayout.All();
            Assert.That(zones, Has.Count.EqualTo(4), "zones 1 to 4 are laid out");
            Assert.That(zones[0].xMin, Is.EqualTo(0f), "the region starts at x 0");
            var scenes = new HashSet<string>();
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                Assert.That(zone.number, Is.EqualTo(i + 1), "zones are listed in order");
                Assert.That(zone.xMax, Is.GreaterThan(zone.xMin), $"zone {zone.number} has no width");
                Assert.That(scenes.Add(zone.scene), $"zone {zone.number} reuses the scene name '{zone.scene}'");
                if (i > 0)
                {
                    Assert.That(
                        zone.xMin,
                        Is.EqualTo(zones[i - 1].xMax),
                        $"zone {zone.number} does not start where zone {zones[i - 1].number} ends"
                    );
                }
            }

            var start = ParadiseLayout.Start;
            Assert.That(start.x, Is.InRange(zones[0].xMin, zones[0].xMax), "the start is in zone 1");
            Assert.That(ParadiseChecker.OnMainPath(zones[0], start), "the start stands on zone 1's main path");

            Assert.That(zones[2].scene, Is.EqualTo("Paradise_Z3_GreatVines"));
            Assert.That(zones[2].xMin, Is.EqualTo(1300f));
            Assert.That(zones[2].xMax, Is.EqualTo(2600f));
            Assert.That(zones[3].scene, Is.EqualTo("Paradise_Z4_TalliedFields"));
            Assert.That(zones[3].xMin, Is.EqualTo(2600f));
            Assert.That(zones[3].xMax, Is.EqualTo(3700f));
        }

        [Test]
        public void TheCrownVineClimbsFiftyTilesToTheFeather()
        {
            var zone3 = ParadiseLayout.Zone3();
            var feather = zone3.placements.Find(p => p.id == "paradise/z3/feather");
            Assert.That(feather, Is.Not.Null, "zone 3 declares the feather");
            Assert.That(feather.type, Is.EqualTo("ability"));
            Assert.That(feather.abilityIndex, Is.EqualTo((int)Ability.Wings), "the feather grants Wings (ability 0)");
            Assert.That(feather.at.x, Is.EqualTo(2560f), "the feather is where ParadiseLayout.Grants says Wings begin");
            Assert.That(ParadiseLayout.Grants[0].x, Is.EqualTo(feather.at.x));
            Assert.That(feather.replaces, Is.EqualTo("Beat 4: feather"));

            var foot = zone3.pods.Find(p => p.name == "CrownPod1");
            Assert.That(foot, Is.Not.Null, "the climb starts from a pod at the Crown Vine's root");
            Assert.That(
                feather.at.y - foot.at.y,
                Is.GreaterThanOrEqualTo(50f),
                "the Crown Vine climbs at least 50 tiles"
            );
            Assert.That(zone3.mainPath, Does.Contain("CrownTop"), "the climb is the main path");
            Assert.That(ParadiseChecker.OnMainPath(zone3, feather.at), "the feather stands on the main path");

            // The three shrines of zone 3 and the two of zone 4, where the task puts them.
            Assert.That(zone3.saves.ConvertAll(s => s.x), Is.EqualTo(new[] { 1756f, 2250f, 2580f }));
            Assert.That(zone3.saves[2].x, Is.GreaterThan(feather.at.x), "the last shrine is at the Crown Vine's top");
            Assert.That(zone3.saves[2].y - foot.at.y, Is.GreaterThanOrEqualTo(50f));
            var zone4 = ParadiseLayout.Zone4();
            Assert.That(zone4.saves.ConvertAll(s => s.x), Is.EqualTo(new[] { 3150f, 3650f }));
            var ithuriel = zone4.placements.Find(p => p.id == "paradise/z4/ithuriel");
            Assert.That(
                Mathf.Abs(ithuriel.at.x - zone4.saves[0].x),
                Is.LessThanOrEqualTo(8f),
                "Ithuriel sits by the Field Shrine"
            );
        }

        [Test]
        public void NoHazardLiesOnTheMainPath()
        {
            // A hand-made zone: thorns on the main path's floor are reported, thorns on a
            // side ledge, under a branch or on a wall's face are not.
            var zone = new ParadiseLayout.Zone
            {
                number = 9,
                scene = "Test",
                xMin = 0f,
                xMax = 60f,
            };
            zone.Ground("Floor", -100f, S(0f, -60f), S(30f, -60f), S(30f, -57f), S(60f, -57f));
            zone.Deck("Branch", 6f, S(10f, -50f), S(20f, -50f));
            zone.Ground("Side", -100f, S(40f, -70f), S(50f, -70f));
            zone.MainPath("Floor", "Branch");
            zone.Thorns("Across", 12f, -60f, 4f, 1.5f);
            var violations = ParadiseChecker.CheckHazards(zone);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(
                violations[0],
                Is.EqualTo("Zone 9: hazard 'Across' at x 12 to 16 lies across the main path on 'Floor'.")
            );
            Assert.That(
                ParadiseChecker.CheckZone(zone),
                Does.Contain(violations[0]),
                "the builder's check reports it too"
            );

            zone.hazards.Clear();
            zone.Thorns("Beside", 42f, -70f, 4f, 1.5f);
            zone.Thorns("Under", 12f, -56f, 4f, 1.5f);
            zone.Thorns("OnTheWall", 29.5f, -59.5f, 0.5f, 2f);
            Assert.That(ParadiseChecker.CheckHazards(zone), Is.Empty);
            // A deck is a main-path surface like any other.
            zone.Thorns("OnTheBranch", 14f, -50f, 2f, 1f);
            Assert.That(ParadiseChecker.CheckHazards(zone), Has.Count.EqualTo(1));
            Assert.That(ParadiseChecker.CheckHazards(zone)[0], Does.Contain("'OnTheBranch'").And.Contain("'Branch'"));

            // The real layout: zone 3 has the first thorns, none across the path anywhere.
            var zones = ParadiseLayout.All();
            Assert.That(zones[2].hazards, Has.Count.GreaterThanOrEqualTo(2), "zone 3 has the first thorn hazards");
            foreach (var built in zones)
            {
                Assert.That(ParadiseChecker.CheckHazards(built), Is.Empty, $"zone {built.number}");
                foreach (var hazard in built.hazards)
                {
                    Assert.That(hazard.rect.width, Is.GreaterThan(0f).And.LessThanOrEqualTo(20f), hazard.name);
                    Assert.That(hazard.rect.height, Is.GreaterThan(0f), hazard.name);
                    Assert.That(hazard.rect.xMin, Is.GreaterThanOrEqualTo(built.xMin), hazard.name);
                    Assert.That(hazard.rect.xMax, Is.LessThanOrEqualTo(built.xMax), hazard.name);
                }
            }
        }

        [Test]
        public void ZonesThreeAndFourCarryTheirDesignMarkers()
        {
            var zone3 = ParadiseLayout.Zone3();
            var zone4 = ParadiseLayout.Zone4();
            Assert.That(Count(zone3, DesignMarker.Kind.Encounter), Is.EqualTo(2), "two air lanes for the flyers");
            Assert.That(Count(zone3, DesignMarker.Kind.Secret), Is.GreaterThanOrEqualTo(3));
            Assert.That(
                zone3.markers.Exists(m => m.label.Contains("Wings") || m.note.Contains("Wings")),
                "one secret needs Wings"
            );
            Assert.That(zone3.markers.Exists(m => m.label == "Into the Fields"));
            Assert.That(zone3.markers.Exists(m => m.label == "Landmark: the Crown Vine"));

            Assert.That(Count(zone4, DesignMarker.Kind.Encounter), Is.EqualTo(2), "two patrol grounds");
            Assert.That(Count(zone4, DesignMarker.Kind.Secret), Is.GreaterThanOrEqualTo(3));
            Assert.That(zone4.markers.Exists(m => m.label == "Landmark: the Sundial"));
            var gust = zone4.markers.Find(m => m.label == "Gust return");
            Assert.That(gust, Is.Not.Null);
            Assert.That(
                gust.at.x - zone4.xMin,
                Is.LessThanOrEqualTo(120f),
                "the gust return is at the zone's west edge"
            );
            Assert.That(
                zone4.shapes.Exists(s => s.island && s.name.StartsWith("SkyIsland")),
                "high islands only Wings reach"
            );
        }

        private static int Count(ParadiseLayout.Zone zone, DesignMarker.Kind kind)
        {
            return zone.markers.FindAll(m => m.kind == kind).Count;
        }

        private static ParadiseLayout.P S(float x, float y) => new ParadiseLayout.P(x, y, true);

        [Test]
        public void PlacementIdsAreUniqueAndInsideTheirZone()
        {
            var ids = new HashSet<string>();
            foreach (var zone in ParadiseLayout.All())
            {
                var labels = new HashSet<string>();
                foreach (var marker in zone.markers)
                {
                    labels.Add(marker.label);
                }
                foreach (var placement in zone.placements)
                {
                    Assert.That(ids.Add(placement.id), $"the placement id '{placement.id}' is used twice");
                    Assert.That(
                        placement.id,
                        Does.Match($"^paradise/z{zone.number}/[a-z0-9_]+$"),
                        $"zone {zone.number}: ids follow the shrines, paradise/z<zone>/<name>"
                    );
                    Assert.That(
                        ParadiseLayout.PlacementTypes,
                        Does.Contain(placement.type),
                        $"{placement.id}: '{placement.type}' is no placement type"
                    );
                    Assert.That(
                        placement.at.x,
                        Is.InRange(zone.xMin, zone.xMax),
                        $"{placement.id} stands outside zone {zone.number}"
                    );
                    if (!string.IsNullOrEmpty(placement.replaces))
                    {
                        Assert.That(
                            labels.Contains(placement.replaces),
                            $"{placement.id} replaces the marker '{placement.replaces}', which zone {zone.number} does not have"
                        );
                    }
                }
            }

            var zones = ParadiseLayout.All();
            var opening = zones[0].placements.Find(p => p.id == "paradise/z1/opening");
            Assert.That(opening, Is.Not.Null, "zone 1 declares the opening");
            Assert.That(opening.type, Is.EqualTo("trigger"));
            Assert.That(opening.dialogue, Is.EqualTo(new[] { "opening" }));
            Assert.That(opening.at.x, Is.EqualTo(ParadiseLayout.Start.x), "the opening plays at the start");
            Assert.That(opening.replaces, Is.EqualTo("Beat 1: opening"));

            var mereth = zones[1].placements.Find(p => p.id == "paradise/z2/mereth");
            Assert.That(mereth, Is.Not.Null, "zone 2 declares Mereth");
            Assert.That(mereth.type, Is.EqualTo("npc"));
            Assert.That(mereth.placementName, Is.EqualTo("Mereth"));
            Assert.That(mereth.dialogue, Is.EqualTo(new[] { "mereth_intro" }));
            Assert.That(mereth.rotate, Is.EqualTo(new[] { "mereth_again", "mereth_count", "mereth_orchard" }));
            Assert.That(mereth.replaces, Is.EqualTo("Beat 2: Mereth"));

            var token = zones[1].placements.Find(p => p.id == "paradise/z2/token");
            Assert.That(token, Is.Not.Null, "zone 2 declares Tobiah's token");
            Assert.That(token.type, Is.EqualTo("item"));
            Assert.That(token.item, Is.EqualTo("token"));
            Assert.That(token.dialogue, Is.EqualTo(new[] { "item_token" }));
            Assert.That(token.replaces, Is.EqualTo("Beat 3: harvest token"));

            var ithuriel = zones[3].placements.Find(p => p.id == "paradise/z4/ithuriel");
            Assert.That(ithuriel, Is.Not.Null, "zone 4 declares Ithuriel");
            Assert.That(ithuriel.type, Is.EqualTo("npc"));
            Assert.That(ithuriel.placementName, Is.EqualTo("Ithuriel"));
            Assert.That(ithuriel.dialogue, Is.EqualTo(new[] { "ithuriel_intro" }));
            Assert.That(ithuriel.rotate, Is.EqualTo(new[] { "ithuriel_again", "ithuriel_song", "ithuriel_orders" }));
            Assert.That(ithuriel.replaces, Is.EqualTo("Beat 6: Ithuriel"));

            var ledger = zones[3].placements.Find(p => p.id == "paradise/z4/ledger");
            Assert.That(ledger, Is.Not.Null, "zone 4 declares the ledger page");
            Assert.That(ledger.type, Is.EqualTo("item"));
            Assert.That(ledger.item, Is.EqualTo("ledger"));
            Assert.That(ledger.dialogue, Is.EqualTo(new[] { "item_ledger" }));
            Assert.That(ledger.replaces, Is.EqualTo("Beat 5: ledger page"));
        }

        [Test]
        public void EveryPlacementDialogueIdExistsInTheDialogueFile()
        {
            var path = DialogueFileTests.DialoguePath;
            var dialogue = new DialogueData(path, File.ReadAllText(path));
            var checkedIds = 0;
            foreach (var zone in ParadiseLayout.All())
            {
                foreach (var placement in zone.placements)
                {
                    foreach (var id in placement.dialogue)
                    {
                        Assert.That(dialogue.HasEntry(id), $"{placement.id}: no dialogue entry '{id}' in world_1.json");
                        checkedIds++;
                    }
                    foreach (var id in placement.rotate)
                    {
                        Assert.That(dialogue.HasEntry(id), $"{placement.id}: no rotate entry '{id}' in world_1.json");
                        checkedIds++;
                    }
                    if (placement.type == "trigger" || placement.type == "npc")
                    {
                        Assert.That(placement.dialogue, Is.Not.Empty, $"{placement.id} has nothing to say");
                    }
                }
            }
            Assert.That(checkedIds, Is.GreaterThan(0), "the layout names no dialogue at all");
        }

        /// <summary>How many times each name is used among the things a main path can cross.</summary>
        private static Dictionary<string, int> WalkableNames(ParadiseLayout.Zone zone)
        {
            var names = new List<string>();
            foreach (var shape in zone.shapes)
            {
                if (shape.collides)
                {
                    names.Add(shape.name);
                }
            }
            foreach (var bridge in zone.bridges)
            {
                names.Add(bridge.name);
            }
            foreach (var ledge in zone.ledges)
            {
                names.Add(ledge.name);
            }
            foreach (var pod in zone.pods)
            {
                names.Add(pod.name);
            }
            var counts = new Dictionary<string, int>();
            foreach (var name in names)
            {
                counts.TryGetValue(name ?? "", out var count);
                counts[name ?? ""] = count + 1;
            }
            return counts;
        }
    }
}
