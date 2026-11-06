using System.Collections.Generic;
using Descender.EditorTools.Paradise;
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
