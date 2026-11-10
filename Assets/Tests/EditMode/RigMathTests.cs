using Descender.World.Paradise;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>The geometry Ansel is posed with (RigMath): arm reach, cloth on the ground, the glance, the combo.</summary>
    public class RigMathTests
    {
        [Test]
        public void TheElbowLetsTheHandReachTheTargetAndHangsDown()
        {
            var shoulder = new Vector2(0f, 1f);
            var target = new Vector2(-0.4f, 1.2f);
            var elbow = RigMath.Elbow(shoulder, target, 0.3f, 0.3f);
            Assert.That(Vector2.Distance(shoulder, elbow), Is.EqualTo(0.3f).Within(1e-4f), "upper arm length");
            Assert.That(Vector2.Distance(elbow, target), Is.EqualTo(0.3f).Within(1e-3f), "the wrist meets the target");
            var midpoint = (shoulder + target) * 0.5f;
            Assert.That(elbow.y, Is.LessThan(midpoint.y), "the elbow bends downward, not up");
        }

        [Test]
        public void OutOfReachTheArmPointsStraightAtTheTarget()
        {
            var elbow = RigMath.Elbow(Vector2.zero, new Vector2(2f, 0f), 0.3f, 0.3f);
            Assert.That(elbow.x, Is.EqualTo(0.3f).Within(0.01f));
            Assert.That(Mathf.Abs(elbow.y), Is.LessThan(0.03f));
        }

        [Test]
        public void AJointBelowTheGroundIsLiftedOntoIt()
        {
            var pivot = new Vector2(0f, 0.2f);
            var tip = new Vector2(0.1f, -0.2f);
            var degrees = RigMath.LiftToGround(pivot, tip, 0f);
            Assert.That(degrees, Is.GreaterThan(0f), "turning counterclockwise lifts a tip ahead of the pivot");
            var lifted = pivot + RigMath.Rotate(tip - pivot, degrees * Mathf.Deg2Rad);
            Assert.That(lifted.y, Is.EqualTo(0f).Within(1e-3f), "the tip rests on the ground");
            Assert.That(lifted.x, Is.GreaterThan(pivot.x), "on the same side it was");
        }

        [Test]
        public void AJointAboveTheGroundOrOutOfReachIsLeftAlone()
        {
            Assert.That(RigMath.LiftToGround(new Vector2(0f, 1f), new Vector2(0f, 0.5f), 0f), Is.EqualTo(0f));
            Assert.That(RigMath.LiftToGround(new Vector2(0f, 1f), new Vector2(0f, 0.8f), 0.5f), Is.EqualTo(0f));
            Assert.That(
                RigMath.LiftToGround(new Vector2(0f, -0.1f), new Vector2(0f, -0.5f), 0f),
                Is.EqualTo(0f),
                "a pivot under the ground cannot rest its tip on it"
            );
        }

        [Test]
        public void TheGlanceTipsUpOrDownTowardWhatIsAheadAndIgnoresWhatIsBehind()
        {
            var eye = new Vector2(0f, 1.5f);
            Assert.That(RigMath.LookAngle(eye, new Vector2(3f, 1.5f), 1, 25f, 30f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(RigMath.LookAngle(eye, new Vector2(2f, 3.5f), 1, 25f, 30f), Is.EqualTo(30f), "clamped up");
            Assert.That(
                RigMath.LookAngle(eye, new Vector2(-2f, 0.5f), -1, 25f, 30f),
                Is.LessThan(0f),
                "down, facing left"
            );
            Assert.That(
                float.IsNaN(RigMath.LookAngle(eye, new Vector2(-2f, 1.5f), 1, 25f, 30f)),
                Is.True,
                "behind him"
            );
        }

        [Test]
        public void TheComboRunsThroughItsCutsAndStartsOverAfterAPause()
        {
            Assert.That(RigMath.NextCut(0, 0.2f, 0.5f, 3), Is.EqualTo(1));
            Assert.That(RigMath.NextCut(1, 0.1f, 0.5f, 3), Is.EqualTo(2));
            Assert.That(RigMath.NextCut(2, 0f, 0.5f, 3), Is.EqualTo(0), "after the last cut, the first");
            Assert.That(RigMath.NextCut(1, 0.8f, 0.5f, 3), Is.EqualTo(0), "too late: the combo starts over");
        }

        [Test]
        public void APoolsSurfaceSpansItsReachAtItsTopEdge()
        {
            var bounds = new Bounds(new Vector3(10f, -0.25f, 0f), new Vector3(8f, 0.5f, 0f));
            var surface = WaterQuad.SurfaceOf(bounds, new Vector4(2f, 3f, 0f, 0f));
            Assert.That(surface.left, Is.EqualTo(8f));
            Assert.That(surface.right, Is.EqualTo(13f));
            Assert.That(surface.y, Is.EqualTo(0f));
        }
    }
}
