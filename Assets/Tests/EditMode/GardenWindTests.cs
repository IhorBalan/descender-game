using Descender.World.Paradise;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The garden's wind field (GardenWind, mirrored in ParadiseSway.hlsl) stays in the
    /// range petals and plants are tuned for, really gusts (calm stretches and strong
    /// ones), and carries its gusts downwind at GustSpeed.
    /// </summary>
    public class GardenWindTests
    {
        private const float Breeze = 0.35f;

        [Test]
        public void StrengthStaysInRange()
        {
            for (var phase = 0f; phase < 600f; phase += 7.3f)
            {
                for (var x = -50f; x < 1400f; x += 1.7f)
                {
                    var s = GardenWind.Strength(x, Breeze, 1f, phase);
                    Assert.That(s, Is.InRange(0f, 1.5f), $"x={x} phase={phase}");
                }
            }
        }

        [Test]
        public void GustsComeAndGo()
        {
            var calm = 0;
            var gusty = 0;
            for (var x = 0f; x < 1000f; x += 0.5f)
            {
                var g = GardenWind.Gust(x, 0f);
                calm += g < 0.05f ? 1 : 0;
                gusty += g > 0.5f ? 1 : 0;
            }
            Assert.That(calm, Is.GreaterThan(200), "no calm between gusts");
            Assert.That(gusty, Is.GreaterThan(100), "no strong gusts");
        }

        [Test]
        public void GustsTravelDownwind()
        {
            // Where the gust is at phase p, it is GustSpeed units further downwind a second later.
            const float phase = 120f;
            foreach (var direction in new[] { 1f, -1f })
            {
                for (var x = 0f; x < 200f; x += 3.1f)
                {
                    var now = GardenWind.Gust(x - direction * phase, phase);
                    var later = GardenWind.Gust(
                        x + direction * GardenWind.GustSpeed - direction * (phase + GardenWind.GustSpeed),
                        phase + GardenWind.GustSpeed
                    );
                    Assert.That(later, Is.EqualTo(now).Within(0.05f));
                }
            }
        }

        [Test]
        public void NoWindWithoutTheComponent()
        {
            Assert.That(Object.FindAnyObjectByType<GardenWind>(), Is.Null);
            Assert.That(GardenWind.At(42f), Is.EqualTo(0f));
        }
    }
}
