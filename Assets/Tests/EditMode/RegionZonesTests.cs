using System.Collections.Generic;
using Descender.EditorTools.Paradise;
using Descender.World;
using NUnit.Framework;
using UnityEditor;

namespace Descender.Tests
{
    /// <summary>
    /// The new Paradise as a wide region cut into zone scenes: which zones a view keeps
    /// loaded, and its own world (ParadiseWorld.asset, one 7,500 x 120 band) kept apart
    /// from the prototype's World.asset, which Main.unity still plays unchanged.
    /// </summary>
    public class RegionZonesTests
    {
        private static readonly List<RegionZones.Zone> Zones = new List<RegionZones.Zone>
        {
            new RegionZones.Zone
            {
                scene = "Z1",
                xMin = 0f,
                xMax = 400f,
            },
            new RegionZones.Zone
            {
                scene = "Z2",
                xMin = 400f,
                xMax = 1300f,
            },
            new RegionZones.Zone
            {
                scene = "Z3",
                xMin = 1300f,
                xMax = 2600f,
            },
        };

        [Test]
        public void TheZoneUnderTheViewIsWanted()
        {
            Assert.That(RegionZones.Wanted(Zones, 800f, 10f, 60f), Is.EqualTo(new[] { "Z2" }));
        }

        [Test]
        public void ANeighbourWithinTheMarginIsWantedToo()
        {
            Assert.That(RegionZones.Wanted(Zones, 1260f, 10f, 60f), Is.EqualTo(new[] { "Z2", "Z3" }));
            Assert.That(RegionZones.Wanted(Zones, 350f, 10f, 60f), Is.EqualTo(new[] { "Z1", "Z2" }));
        }

        [Test]
        public void FarZonesAreNotWanted()
        {
            Assert.That(RegionZones.Wanted(Zones, 2000f, 10f, 60f), Is.EqualTo(new[] { "Z3" }));
        }

        [Test]
        public void TheNewParadiseHasItsOwnWorld()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>("Assets/Data/Paradise/ParadiseWorld.asset");
            Assert.That(atlas, Is.Not.Null, "run Descender > Paradise > Build greybox");
            Assert.That(atlas.regions.Length, Is.EqualTo(1));
            Assert.That(atlas.regions[0].key, Is.EqualTo("paradise_new"));
            Assert.That(atlas.regions[0].rows, Is.EqualTo(120));
            Assert.That(atlas.regions[0].width, Is.EqualTo(7500));
            Assert.That(atlas.startRegion, Is.EqualTo("paradise_new"));
            // The shrines of all seven zones in the world's save order: zone by zone, west to
            // east within a zone (D-0010), so a new zone never renumbers an older one. The
            // design asks for at least 15; the layout has 16.
            Assert.That(
                atlas.checkpoints.Length,
                Is.GreaterThanOrEqualTo(15),
                "run Descender > Paradise > Build new zones"
            );
            var zones = ParadiseLayout.All();
            var zoneIndex = 0;
            for (var i = 0; i < atlas.checkpoints.Length; i++)
            {
                var checkpoint = atlas.checkpoints[i];
                Assert.That(checkpoint.region, Is.EqualTo("paradise_new"));
                Assert.That(checkpoint.position.x, Is.InRange(0f, 7500f));
                while (checkpoint.position.x > zones[zoneIndex].xMax)
                {
                    zoneIndex++;
                }
                if (i > 0 && atlas.checkpoints[i - 1].position.x >= zones[zoneIndex].xMin)
                {
                    Assert.That(
                        checkpoint.position.x,
                        Is.GreaterThan(atlas.checkpoints[i - 1].position.x),
                        $"checkpoint {i} is west of checkpoint {i - 1} in the same zone"
                    );
                }
            }
            Assert.That(atlas.checkpoints[0].position.x, Is.LessThan(400f), "zone 1's shrine is checkpoint 0");
        }

        [Test]
        public void ThePrototypeWorldIsUntouched()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>("Assets/Data/World.asset");
            Assert.That(atlas.regions[0].key, Is.EqualTo("paradise"));
            Assert.That(atlas.regions[0].rows, Is.EqualTo(51), "Main.unity still plays the prototype Paradise");
        }
    }
}
