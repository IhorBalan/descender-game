using System.Collections.Generic;
using System.IO;
using Descender.EditorTools.Paradise;
using Descender.Story;
using NUnit.Framework;

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
            Assert.That(zones, Is.Not.Empty);
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
        }

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
