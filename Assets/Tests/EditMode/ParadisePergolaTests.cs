using System.Linq;
using Descender.EditorTools.Paradise;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    public sealed class ParadisePergolaTests
    {
        private const float MostStep = 0.35f;
        private const float HeroHeight = 1.8f;

        private static ParadiseLayout.Zone Zone(int number) => ParadiseLayout.All().First(z => z.number == number);

        [TestCase(1)]
        [TestCase(2)]
        public void BothColumnsOfEveryPergolaStandOnLevelGround(int number)
        {
            var zone = Zone(number);
            var pergolas = ParadiseGarden.PergolasOf(number);
            Assert.That(pergolas, Is.Not.Empty);
            foreach (var x in pergolas)
            {
                var left = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                var right = ParadiseDressing.SurfaceY(zone, x + ParadiseGarden.PergolaSpan, float.NaN);
                Assert.That(float.IsNaN(left), Is.False, $"left column at {x}");
                Assert.That(float.IsNaN(right), Is.False, $"right column at {x}");
                Assert.That(Mathf.Abs(left - right), Is.LessThan(MostStep), $"pergola at {x}");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ThePergolasAreInTheZonesPlanAndClearOfTheHerosHead(int number)
        {
            var plan = ParadiseGarden.GardenPlan(Zone(number));
            Assert.That(plan.pergolas, Is.EquivalentTo(ParadiseGarden.PergolasOf(number)));
            Assert.That(ParadiseGarden.PergolaRise, Is.GreaterThan(HeroHeight));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void WallHangingsKeepClearOfTheIvyTheFallsAndThePergolas(int number)
        {
            var zone = Zone(number);
            var plan = ParadiseGarden.GardenPlan(zone);
            // Wall hangings hang just under the coping; those under a deck hang lower.
            var onWalls = plan
                .hanging.Where(h =>
                    (h.name == "banner" || h.name == "lantern")
                    && Mathf.Abs(ParadiseDressing.SurfaceY(zone, h.at.x) - h.at.y - 0.95f) < 0.01f
                )
                .ToList();
            Assert.That(onWalls, Is.Not.Empty);
            var ivy = plan.walls.Where(w => w.name.StartsWith("ivy_drape")).Select(w => w.at.x).ToList();
            foreach (var h in onWalls)
            {
                var x = h.at.x;
                Assert.That(ivy.All(i => Mathf.Abs(i - x) >= ParadiseGarden.WallHangingClearance), $"ivy at {x}");
                Assert.That(
                    plan.water.All(w => Mathf.Abs(w.x - x) >= ParadiseGarden.WallHangingClearance),
                    $"fall at {x}"
                );
                Assert.That(
                    plan.pergolas.All(p => x <= p - 1f || x >= p + ParadiseGarden.PergolaSpan + 1f),
                    $"pergola at {x}"
                );
            }
        }
    }
}
