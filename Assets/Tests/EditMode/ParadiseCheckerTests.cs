using System.Collections.Generic;
using Descender.EditorTools.Paradise;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The construction-rule checker (ParadiseChecker) on small hand-made zones, one rule
    /// at a time, and then on the real layout, which must stay clean. The hand-made zones
    /// sit west of the feather, so the before-Wings limits apply: gap 4, step 4.
    /// </summary>
    public class ParadiseCheckerTests
    {
        [Test]
        public void AZoneWithinTheRulesHasNoViolations()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 24f, aY: -60f, bY: -56f);
            Assert.That(ParadiseChecker.CheckZone(zone), Is.Empty, "a 4-tile gap and a 4-tile step are allowed");
        }

        [Test]
        public void AGapOverTheLimitIsReported()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 25f, aY: -60f, bY: -60f);
            var violations = ParadiseChecker.CheckZone(zone);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: gap of 5 tiles from 'A' to 'B'"));
            Assert.That(violations[0], Does.Contain("(limit 4)"));
        }

        [Test]
        public void AStepOverTheLimitIsReported()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 22f, aY: -60f, bY: -55f);
            var violations = ParadiseChecker.CheckZone(zone);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: step up of 5 tiles from 'A' to 'B'"));
            Assert.That(violations[0], Does.Contain("(limit 4)"));

            // A wall inside one surface is a step like any other.
            var walled = NewZone(40f);
            walled.Ground("A", -100f, S(0f, -60f), S(20f, -60f), S(20f, -54f), S(40f, -54f));
            walled.MainPath("A");
            walled.saves.Add(new Vector2(10f, -60f));
            violations = ParadiseChecker.CheckZone(walled);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: step up of 6 tiles inside 'A' at x 20"));
        }

        [Test]
        public void APodAllowsItsThrowHeight()
        {
            // A 10-tile wall: too high to jump, cleared by a pod that throws 12.
            var zone = TwoSurfaces(aEnd: 20f, bStart: 20f, aY: -60f, bY: -50f);
            zone.Bounce("Pod", 18f, -59.6f, 12f);
            zone.mainPath.Clear();
            zone.MainPath("A", "Pod", "B");
            Assert.That(ParadiseChecker.CheckZone(zone), Is.Empty);

            // The same wall without the pod on the main path.
            zone.mainPath.Clear();
            zone.MainPath("A", "B");
            var violations = ParadiseChecker.CheckZone(zone);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.Contain("step up of 10 tiles from 'A' to 'B'"));

            // A pod that throws less than the wall's height does not excuse it.
            var weak = TwoSurfaces(aEnd: 20f, bStart: 20f, aY: -60f, bY: -50f);
            weak.Bounce("Pod", 18f, -59.6f, 6f);
            weak.mainPath.Clear();
            weak.MainPath("A", "Pod", "B");
            violations = ParadiseChecker.CheckZone(weak);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.Contain("step up of 9.6 tiles from pod 'Pod' on 'A' to 'B'"));
            Assert.That(violations[0], Does.Contain("(the pod throws 6)"));
        }

        [Test]
        public void ALongDropOntoHardGroundIsReported()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 22f, aY: -30f, bY: -60f);
            zone.saves.Clear();
            var violations = ParadiseChecker.CheckZone(zone);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: drop of 30 tiles from 'A' onto 'B'"));
            Assert.That(violations[0], Does.Contain("(limit 22"));

            zone.Soft("B");
            Assert.That(ParadiseChecker.CheckZone(zone), Is.Empty, "a soft landing has no fall limit");

            var twoScreens = TwoSurfaces(aEnd: 20f, bStart: 22f, aY: -38f, bY: -60f);
            twoScreens.saves.Clear();
            Assert.That(ParadiseChecker.CheckZone(twoScreens), Is.Empty, "a drop of exactly 22 tiles is allowed");
        }

        [Test]
        public void AShrineOffTheMainPathIsReported()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 24f, aY: -60f, bY: -60f);
            zone.saves.Clear();
            zone.saves.Add(new Vector2(10f, -59.2f));
            Assert.That(ParadiseChecker.CheckZone(zone), Is.Empty, "a shrine within a tile of the surface is on it");

            // In the air above the path, and on ground the main path does not cross.
            zone.Ground("Side", -100f, S(26f, -40f), S(36f, -40f));
            zone.saves.Add(new Vector2(10f, -58f));
            zone.saves.Add(new Vector2(30f, -40f));
            var violations = ParadiseChecker.CheckZone(zone);
            Assert.That(violations, Has.Count.EqualTo(2), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: the save shrine at (10, -58) is not within 1 tile"));
            Assert.That(violations[1], Does.StartWith("Zone 1: the save shrine at (30, -40) is not within 1 tile"));
        }

        [Test]
        public void ASixHundredTileStretchWithoutAShrineIsReported()
        {
            // Shrines 600 apart, 300 from the start and 400 from the end: allowed.
            var zones = new List<ParadiseLayout.Zone> { Flat(1, 0f, 700f, 300f), Flat(2, 700f, 1300f, 900f) };
            Assert.That(ParadiseChecker.CheckShrineSpacing(zones), Is.Empty);
            Assert.That(ParadiseChecker.Check(zones), Is.Empty);

            // Between two shrines.
            zones = new List<ParadiseLayout.Zone> { Flat(1, 0f, 700f, 300f), Flat(2, 700f, 1300f, 950f) };
            var violations = ParadiseChecker.Check(zones);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(
                violations[0],
                Is.EqualTo("Zone 2: 650 tiles without a save shrine, from x 300 to x 950 (limit 600).")
            );

            // From the start to the first shrine.
            zones = new List<ParadiseLayout.Zone> { Flat(1, 0f, 700f, 601f), Flat(2, 700f, 1300f, 1200f) };
            violations = ParadiseChecker.CheckShrineSpacing(zones);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: 601 tiles without a save shrine, from x 0 to x 601"));

            // From the last shrine to the end of the zone list.
            zones = new List<ParadiseLayout.Zone> { Flat(1, 0f, 700f, 500f), Flat(2, 700f, 1300f) };
            violations = ParadiseChecker.CheckShrineSpacing(zones);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 2: 800 tiles without a save shrine, from x 500 to x 1300"));
        }

        [Test]
        public void AMainPathThatIsMissingOrNamesNothingIsReported()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 24f, aY: -60f, bY: -60f);
            zone.mainPath.Clear();
            Assert.That(ParadiseChecker.CheckZone(zone), Is.EqualTo(new[] { "Zone 1: declares no main path." }));

            zone.MainPath("A", "Nowhere", "B");
            var violations = ParadiseChecker.CheckZone(zone);
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1: the main path names 'Nowhere'"));
        }

        [Test]
        public void TheStepBetweenTwoZonesIsChecked()
        {
            var west = Flat(1, 0f, 100f, 50f);
            var east = NewZone(200f);
            east.number = 2;
            east.xMin = 100f;
            east.Ground("Floor", -100f, S(106f, -60f), S(200f, -60f));
            east.MainPath("Floor");
            var violations = ParadiseChecker.Check(new List<ParadiseLayout.Zone> { west, east });
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0], Does.StartWith("Zone 1 to 2: gap of 6 tiles from 'Floor' to 'Floor'"));
        }

        [Test]
        public void TheBuiltZonesPassTheConstructionRules()
        {
            var violations = ParadiseChecker.Check(ParadiseLayout.All());
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void TheBuilderRefusesAZoneWithViolations()
        {
            var zone = TwoSurfaces(aEnd: 20f, bStart: 25f, aY: -60f, bY: -60f);
            var error = Assert.Throws<System.InvalidOperationException>(() =>
                ParadiseGreybox.CheckConstructionRules(zone)
            );
            Assert.That(error.Message, Does.Contain("gap of 5 tiles from 'A' to 'B'"));

            foreach (var built in ParadiseLayout.All())
            {
                Assert.DoesNotThrow(() => ParadiseGreybox.CheckConstructionRules(built));
            }
        }

        private static ParadiseLayout.P S(float x, float y) => new ParadiseLayout.P(x, y, true);

        private static ParadiseLayout.Zone NewZone(float xMax)
        {
            return new ParadiseLayout.Zone
            {
                number = 1,
                scene = "Test",
                xMin = 0f,
                xMax = xMax,
            };
        }

        /// <summary>
        /// Zone 1 from x 0 to 40: flat ground A from 0 to aEnd at height aY, flat ground B from
        /// bStart to 40 at bY, both on the main path, and a save shrine on A.
        /// </summary>
        private static ParadiseLayout.Zone TwoSurfaces(float aEnd, float bStart, float aY, float bY)
        {
            var zone = NewZone(40f);
            zone.Ground("A", -100f, S(0f, aY), S(aEnd, aY));
            zone.Ground("B", -100f, S(bStart, bY), S(40f, bY));
            zone.MainPath("A", "B");
            zone.saves.Add(new Vector2(10f, aY));
            return zone;
        }

        /// <summary>A zone that is one flat floor, with save shrines at the given x.</summary>
        private static ParadiseLayout.Zone Flat(int number, float xMin, float xMax, params float[] shrines)
        {
            var zone = NewZone(xMax);
            zone.number = number;
            zone.xMin = xMin;
            zone.Ground("Floor", -100f, S(xMin, -60f), S(xMax, -60f));
            zone.MainPath("Floor");
            foreach (var x in shrines)
            {
                zone.saves.Add(new Vector2(x, -60f));
            }
            return zone;
        }
    }
}
