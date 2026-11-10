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
    }
}
