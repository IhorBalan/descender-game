using Descender.Motion;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The procedural run of the CharacterAnimationLab (<see cref="RunGait"/>): eight
    /// distinct key poses; legs and arms that keep their lengths; a foot on the ground that
    /// does not slide while the body travels over it; the legs taking turns with two
    /// flights a cycle; knees that bend forward; the hips rising and falling a little,
    /// lowest as the weight comes down; the hips and shoulders turning against each other
    /// and the arms swinging against the legs; a foot that moves on without a jump where
    /// it lands and where it leaves the ground; and speed, stride and cycle kept together.
    /// </summary>
    public class RunGaitTests
    {
        private const int Samples = 720;

        private static RunGaitSettings Settings()
        {
            var settings = new RunGaitSettings();
            settings.Resolve();
            return settings;
        }

        private static RunPose At(float phase, RunGaitSettings settings, RunBody body)
        {
            var pose = new RunPose();
            RunGait.Evaluate(phase, settings, body, pose);
            return pose;
        }

        [Test]
        public void TheEightKeyPosesAreDistinct()
        {
            var settings = Settings();
            var body = new RunBody();
            var poses = new RunPose[RunGait.KeyCount];
            for (var k = 0; k < RunGait.KeyCount; k++)
            {
                var key = (RunKey)k;
                Assert.That(RunGait.KeyAt(RunGait.PhaseOf(key)), Is.EqualTo(key));
                poses[k] = At(RunGait.PhaseOf(key), settings, body);
                Assert.That(poses[k].key, Is.EqualTo(key));
            }
            for (var a = 0; a < RunGait.KeyCount; a++)
            {
                for (var b = a + 1; b < RunGait.KeyCount; b++)
                {
                    Assert.That(Difference(poses[a], poses[b]), Is.GreaterThan(0.1f), $"{(RunKey)a} and {(RunKey)b}");
                }
            }
        }

        [Test]
        public void LimbsKeepTheirLengths()
        {
            var settings = Settings();
            var body = new RunBody();
            var foot = new Vector2(body.foot, body.ankleHeight).magnitude;
            for (var i = 0; i < Samples; i++)
            {
                var pose = At(i / (float)Samples, settings, body);
                foreach (var leg in new[] { pose.leftLeg, pose.rightLeg })
                {
                    Assert.That(Vector2.Distance(leg.root, leg.middle), Is.EqualTo(body.thigh).Within(1e-4f), "thigh");
                    Assert.That(Vector2.Distance(leg.middle, leg.end), Is.EqualTo(body.shin).Within(1e-4f), "shin");
                    Assert.That(Vector2.Distance(leg.end, leg.tip), Is.EqualTo(foot).Within(1e-4f), "foot");
                }
                foreach (var arm in new[] { pose.leftArm, pose.rightArm })
                {
                    Assert.That(
                        Vector2.Distance(arm.root, arm.middle),
                        Is.EqualTo(body.upperArm).Within(1e-4f),
                        "upper arm"
                    );
                    Assert.That(
                        Vector2.Distance(arm.middle, arm.end),
                        Is.EqualTo(body.forearm).Within(1e-4f),
                        "forearm"
                    );
                }
                Assert.That(Vector2.Distance(pose.hips, pose.chest), Is.EqualTo(body.spine).Within(1e-4f), "spine");
            }
        }

        [Test]
        public void AFootOnTheGroundDoesNotSlide()
        {
            var settings = Settings();
            var body = new RunBody();
            var pose = new RunPose();
            var travelled = 0f;
            var phase = 0f;
            var planted = new float?[2];
            var stances = 0;
            for (var frame = 0; frame < 60 * 6; frame++)
            {
                RunGait.Evaluate(phase, settings, body, pose);
                for (var side = 0; side < 2; side++)
                {
                    var leg = side == 0 ? pose.leftLeg : pose.rightLeg;
                    if (!leg.contact)
                    {
                        planted[side] = null;
                        continue;
                    }
                    // The toes really are on the ground (the leg reaches them), at one place for the whole stance.
                    Assert.That(leg.tip.y, Is.EqualTo(0f).Within(1e-4f), "the toes are on the ground");
                    var world = travelled + leg.tip.x;
                    if (planted[side] == null)
                    {
                        planted[side] = world;
                        stances++;
                    }
                    Assert.That(world, Is.EqualTo(planted[side].Value).Within(1e-3f), $"frame {frame}: the foot slid");
                }
                var step = 1f / 60f;
                phase = Mathf.Repeat(phase + step / settings.cycleDuration, 1f);
                travelled += settings.runningSpeed * step;
            }
            Assert.That(stances, Is.GreaterThan(10), "both feet came down, again and again");
        }

        [Test]
        public void TheLegsTakeTurnsWithTwoFlightsACycle()
        {
            var settings = Settings();
            var body = new RunBody();
            var flight = 0;
            var leftDown = 0;
            var rightDown = 0;
            for (var i = 0; i < Samples; i++)
            {
                var pose = At(i / (float)Samples, settings, body);
                Assert.That(pose.leftLeg.contact && pose.rightLeg.contact, Is.False, "a run never has both feet down");
                flight += pose.InFlight ? 1 : 0;
                leftDown += pose.leftLeg.contact ? 1 : 0;
                rightDown += pose.rightLeg.contact ? 1 : 0;
            }
            Assert.That(leftDown, Is.EqualTo(rightDown).Within(2), "each foot is down as long");
            Assert.That(flight / (float)Samples, Is.EqualTo(1f - 2f * RunGaitSettings.Duty).Within(0.01f));
            Assert.That(At(RunGait.PhaseOf(RunKey.ContactRight), settings, body).rightLeg.contact, Is.True);
            Assert.That(At(RunGait.PhaseOf(RunKey.ContactLeft), settings, body).leftLeg.contact, Is.True);
            Assert.That(At(RunGait.PhaseOf(RunKey.FlightRight), settings, body).InFlight, Is.True);
            Assert.That(At(RunGait.PhaseOf(RunKey.FlightLeft), settings, body).InFlight, Is.True);
            // The left leg does what the right did, half a cycle later.
            var right = At(0.1f, settings, body).rightLeg;
            var left = At(0.6f, settings, body).leftLeg;
            Assert.That(
                left.end - left.root,
                Is.EqualTo(right.end - right.root).Using(new Vector2EqualityComparer(1e-3f))
            );
        }

        [Test]
        public void KneesBendForward()
        {
            var settings = Settings();
            var body = new RunBody();
            for (var i = 0; i < Samples; i++)
            {
                var pose = At(i / (float)Samples, settings, body);
                foreach (var leg in new[] { pose.leftLeg, pose.rightLeg })
                {
                    var line = leg.end - leg.root;
                    var knee = leg.middle - leg.root;
                    Assert.That(
                        line.x * knee.y - line.y * knee.x,
                        Is.GreaterThanOrEqualTo(-1e-5f),
                        "the knee is on the front of the leg's line"
                    );
                }
            }
            var down = At(RunGait.PhaseOf(RunKey.DownRight), settings, body).rightLeg;
            Assert.That(
                Vector2.Distance(down.root, down.end),
                Is.LessThan(body.Leg * 0.95f),
                "the leg bearing the weight bends"
            );
        }

        [Test]
        public void TheHipsRiseAndFallALittle()
        {
            var settings = Settings();
            var body = new RunBody();
            var low = float.MaxValue;
            var high = float.MinValue;
            var lowest = 0f;
            for (var i = 0; i < Samples; i++)
            {
                var phase = i / (float)Samples;
                var y = At(phase, settings, body).hips.y;
                if (y < low)
                {
                    low = y;
                    lowest = phase;
                }
                high = Mathf.Max(high, y);
            }
            Assert.That(high - low, Is.InRange(settings.verticalBounce, settings.verticalBounce * 2f));
            var nearDown = Mathf.Min(
                Mathf.Abs(lowest - RunGait.PhaseOf(RunKey.DownRight)),
                Mathf.Abs(lowest - RunGait.PhaseOf(RunKey.DownLeft))
            );
            Assert.That(nearDown, Is.LessThan(0.04f), "lowest as the weight comes down");
            settings.verticalBounce = 0f;
            Assert.That(At(0.1f, settings, body).hips.y, Is.EqualTo(At(0.4f, settings, body).hips.y).Within(1e-5f));
        }

        [Test]
        public void TheBodyTurnsAgainstItselfAndTheArmsSwingAgainstTheLegs()
        {
            var settings = Settings();
            var body = new RunBody();
            var pose = At(RunGait.PhaseOf(RunKey.ContactRight), settings, body);
            Assert.That(pose.rightLeg.tip.x, Is.GreaterThan(pose.leftLeg.tip.x), "the right foot is ahead");
            Assert.That(pose.rightLeg.root.x, Is.GreaterThan(pose.leftLeg.root.x), "the right hip is turned forward");
            Assert.That(
                pose.leftArm.root.x,
                Is.GreaterThan(pose.rightArm.root.x),
                "the left shoulder is turned forward"
            );
            Assert.That(pose.leftArm.end.x, Is.GreaterThan(pose.rightArm.end.x), "the left hand is forward");
            Assert.That(pose.lean, Is.GreaterThan(0f), "the torso leans forward");
            Assert.That(pose.chest.x, Is.GreaterThan(pose.hips.x));
            settings.armSwing = 0f;
            settings.counterRotation = 0f;
            var still = At(RunGait.PhaseOf(RunKey.ContactRight), settings, body);
            Assert.That(still.leftArm.middle.x, Is.EqualTo(still.rightArm.middle.x).Within(1e-4f));
        }

        [Test]
        public void TheWholeBodyLeansAndTheLimbsStretchOut()
        {
            var settings = Settings();
            var body = new RunBody();
            // The feet fall behind the hips on average: the body's axis leans forward, more with more lean.
            float MeanFoot()
            {
                var sum = 0f;
                for (var i = 0; i < Samples; i++)
                {
                    var pose = At(i / (float)Samples, settings, body);
                    sum += pose.leftLeg.end.x + pose.rightLeg.end.x;
                }
                return sum / (2f * Samples);
            }
            var leaning = MeanFoot();
            Assert.That(leaning, Is.LessThan(-0.05f));
            settings.bodyLean = 0f;
            Assert.That(MeanFoot(), Is.GreaterThan(leaning + 0.05f));
            settings.bodyLean = 8f;
            // The leg pushing off and the leg trailing in the flight are straight; the arm swung back is nearly so.
            var pushOff = At(RunGaitSettings.Duty - 0.002f, settings, body).rightLeg;
            Assert.That(Vector2.Distance(pushOff.root, pushOff.end), Is.GreaterThan(body.Leg * 0.97f), "push-off");
            var trailing = At(RunGait.PhaseOf(RunKey.FlightRight), settings, body).rightLeg;
            Assert.That(Vector2.Distance(trailing.root, trailing.end), Is.GreaterThan(body.Leg * 0.97f), "flight");
            var back = At(RunGait.PhaseOf(RunKey.ContactRight), settings, body).rightArm;
            Assert.That(
                Vector2.Distance(back.root, back.end),
                Is.GreaterThan((body.upperArm + body.forearm) * 0.9f),
                "arm back"
            );
        }

        [Test]
        public void AFootMovesOnWithoutAJumpWhereItLandsAndLeaves()
        {
            var settings = Settings();
            var body = new RunBody();
            const float Eps = 1e-4f;
            foreach (var edge in new[] { 0f, RunGaitSettings.Duty })
            {
                foreach (var smoothing in new[] { 0f, 1f })
                {
                    settings.smoothing = smoothing;
                    var before = At(edge - Eps, settings, body).rightLeg;
                    var after = At(edge + Eps, settings, body).rightLeg;
                    Assert.That(Vector2.Distance(before.end, after.end), Is.LessThan(0.01f), $"ankle at {edge}");
                    Assert.That(Vector2.Distance(before.tip, after.tip), Is.LessThan(0.01f), $"toes at {edge}");
                }
            }
        }

        [Test]
        public void SpeedStrideAndCycleAreKeptTogether()
        {
            var settings = new RunGaitSettings
            {
                runningSpeed = 6f,
                strideLength = 3f,
                derived = RunDerived.CycleDuration,
            };
            settings.Resolve();
            Assert.That(settings.cycleDuration, Is.EqualTo(0.5f).Within(1e-5f));
            settings.derived = RunDerived.StrideLength;
            settings.cycleDuration = 0.6f;
            settings.Resolve();
            Assert.That(settings.strideLength, Is.EqualTo(3.6f).Within(1e-5f));
            settings.derived = RunDerived.RunningSpeed;
            settings.strideLength = 4f;
            settings.cycleDuration = 0.8f;
            settings.Resolve();
            Assert.That(settings.runningSpeed, Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void TwoBoneIkReachesWhatIsInReachAndPointsAtWhatIsNot()
        {
            RunGait.SolveTwoBone(Vector2.zero, new Vector2(0.3f, -0.8f), 0.5f, 0.5f, 1f, out var middle, out var end);
            Assert.That(end, Is.EqualTo(new Vector2(0.3f, -0.8f)).Using(new Vector2EqualityComparer(1e-4f)));
            Assert.That(middle.magnitude, Is.EqualTo(0.5f).Within(1e-4f));
            RunGait.SolveTwoBone(Vector2.zero, new Vector2(0f, -3f), 0.5f, 0.5f, 1f, out middle, out end);
            Assert.That(end.y, Is.EqualTo(-1f).Within(1e-3f));
            Assert.That(Vector2.Distance(middle, end), Is.EqualTo(0.5f).Within(1e-4f));
        }

        private static float Difference(RunPose a, RunPose b)
        {
            var sum = 0f;
            sum += Vector2.Distance(a.leftLeg.end, b.leftLeg.end) + Vector2.Distance(a.rightLeg.end, b.rightLeg.end);
            sum +=
                Vector2.Distance(a.leftLeg.middle, b.leftLeg.middle)
                + Vector2.Distance(a.rightLeg.middle, b.rightLeg.middle);
            sum += Vector2.Distance(a.leftArm.end, b.leftArm.end) + Vector2.Distance(a.rightArm.end, b.rightArm.end);
            sum += Vector2.Distance(a.hips, b.hips);
            return sum;
        }

        private sealed class Vector2EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector2>
        {
            private readonly float within;

            public Vector2EqualityComparer(float within) => this.within = within;

            public bool Equals(Vector2 a, Vector2 b) => Vector2.Distance(a, b) <= within;

            public int GetHashCode(Vector2 v) => 0;
        }
    }
}
