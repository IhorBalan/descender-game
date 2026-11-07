using System.Collections.Generic;
using Descender.EditorTools.Paradise;
using Descender.Save;
using NUnit.Framework;

namespace Descender.Tests
{
    /// <summary>
    /// The jump limits of docs/design/paradise.md, section 2, by where the hero is in the
    /// region: they grow at the feather (Wings) and again at the light dash.
    /// </summary>
    public class ParadiseRulesTests
    {
        [Test]
        public void LimitsFollowTheAbilitiesGranted()
        {
            Assert.That(ParadiseLayout.Grants.Count, Is.EqualTo(2));
            Assert.That(ParadiseLayout.Grants[0].ability, Is.EqualTo(Ability.Wings));
            Assert.That(ParadiseLayout.Grants[0].x, Is.EqualTo(2560f));
            Assert.That(ParadiseLayout.Grants[1].ability, Is.EqualTo(Ability.LightDash));
            Assert.That(ParadiseLayout.Grants[1].x, Is.EqualTo(4700f));

            AssertLimits(ParadiseRules.For(0f), 4f, 4f, "at the start");
            AssertLimits(ParadiseRules.For(2559.9f), 4f, 4f, "just before the feather");
            AssertLimits(ParadiseRules.For(2560f), 7f, 7f, "at the feather");
            AssertLimits(ParadiseRules.For(4699.9f), 7f, 7f, "just before the light dash");
            // The design's table leaves the step with Wings and the dash blank; 7 is carried forward.
            AssertLimits(ParadiseRules.For(4700f), 9f, 7f, "at the light dash");
            AssertLimits(ParadiseRules.For(7500f), 9f, 7f, "at the Low Gate");
        }

        [Test]
        public void TheDashWithoutWingsChangesNothing()
        {
            var grants = new List<ParadiseLayout.Grant> { new ParadiseLayout.Grant(Ability.LightDash, 100f) };
            AssertLimits(ParadiseRules.For(200f, grants), 4f, 4f, "with the dash alone");
        }

        private static void AssertLimits(ParadiseRules.Limits limits, float gap, float step, string where)
        {
            Assert.That(limits.gap, Is.EqualTo(gap), $"gap {where}");
            Assert.That(limits.step, Is.EqualTo(step), $"step {where}");
        }
    }
}
