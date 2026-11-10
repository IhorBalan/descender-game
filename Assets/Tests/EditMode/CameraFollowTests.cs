using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Descender.CameraRig;
using Descender.EditorTools.Paradise;
using Descender.World;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The camera's aim (after "Scroll Back", GDC 2015): the look-ahead travels only as the
    /// hero runs, the height is held through a hop and eased to each floor, joined rooms
    /// are one land to the camera, and the camera moves before what is placed by it.
    /// </summary>
    public sealed class CameraFollowTests
    {
        private const float Run = 6.25f;
        private const float Dt = 1f / 144f;

        [Test]
        public void TurningOnTheSpotNeverSwingsTheView()
        {
            Assert.That(CameraFollow.LookAhead(1.5f, -1, 1.5f, 0f, 0.6f, Dt), Is.EqualTo(1.5f));
            Assert.That(CameraFollow.LookAhead(-1.5f, 1, 1.5f, 0f, 0.6f, 0.5f), Is.EqualTo(-1.5f));
        }

        [Test]
        public void RunningTheOtherWayTheLookAheadTravelsAcrossAtAShareOfHisSpeed()
        {
            var lookAhead = 1.5f;
            var frames = 0;
            var biggest = 0f;
            while (lookAhead > -1.5f && frames < 10000)
            {
                var next = CameraFollow.LookAhead(lookAhead, -1, 1.5f, -Run, 0.6f, Dt);
                biggest = Mathf.Max(biggest, Mathf.Abs(next - lookAhead));
                lookAhead = next;
                frames++;
            }
            Assert.That(lookAhead, Is.EqualTo(-1.5f), "it gets there and stops");
            Assert.That(biggest, Is.EqualTo(0.6f * Run * Dt).Within(1e-5f), "never faster than its share");
            Assert.That(frames * Dt, Is.InRange(0.6f, 1.2f), "a swing across, not a lurch");
            // And it never goes past the side he faces.
            Assert.That(CameraFollow.LookAhead(-1.5f, -1, 1.5f, -Run, 0.6f, 1f), Is.EqualTo(-1.5f));
        }

        [Test]
        public void AHopNeverMovesTheViewAndALandingSetsTheNewFloor()
        {
            // Standing: the floor is his height.
            Assert.That(CameraFollow.Baseline(-60f, -58f, true, 3f, 0.5f), Is.EqualTo(-58f));
            // A hop of two units: held.
            Assert.That(CameraFollow.Baseline(-60f, -58f, false, 3f, 0.5f), Is.EqualTo(-60f));
            // Above a jump's height it follows him up, three units below him.
            Assert.That(CameraFollow.Baseline(-60f, -55f, false, 3f, 0.5f), Is.EqualTo(-58f));
            // Falling off a ledge it follows him down, half a unit above him.
            Assert.That(CameraFollow.Baseline(-60f, -62f, false, 3f, 0.5f), Is.EqualTo(-61.5f));
        }

        [Test]
        public void InTheAirTheHeightMovesNoFasterThanHeDoes()
        {
            // Up a pod's throw and back down: never a jump between holding and following.
            var baseline = -60f;
            var heroY = -60f;
            for (var i = 0; i < 400; i++)
            {
                var step = i < 200 ? 0.05f : -0.05f;
                heroY += step;
                var next = CameraFollow.Baseline(baseline, heroY, false, 3f, 0.5f);
                Assert.That(Mathf.Abs(next - baseline), Is.LessThanOrEqualTo(Mathf.Abs(step) + 1e-4f), $"at {i}");
                baseline = next;
            }
        }

        [Test]
        public void JoinedRoomsAreOneLandToTheCamera()
        {
            var hollow = new Rect(0f, -105f, 400f, 90f);
            var orchard = new Rect(400f, -105f, 900f, 90f);
            var vines = new Rect(1300f, -105f, 1300f, 100f);
            var apart = new Rect(3000f, -105f, 100f, 90f);
            var joined = WorldRoom.JoinedBounds(hollow, new List<Rect> { vines, apart, orchard });
            Assert.That(joined.xMin, Is.EqualTo(0f));
            Assert.That(joined.xMax, Is.EqualTo(2600f), "the vines join through the orchard");
            Assert.That(joined.yMax, Is.EqualTo(-5f));
            Assert.That(
                WorldRoom.JoinedBounds(hollow, new List<Rect> { apart }),
                Is.EqualTo(hollow),
                "a room apart stays apart"
            );
        }

        [Test]
        public void TheCameraMovesBeforeEverythingPlacedByTheView()
        {
            var camera = typeof(GameCamera).GetCustomAttribute<DefaultExecutionOrder>();
            Assert.That(camera, Is.Not.Null);
            var placed = new[]
            {
                typeof(BackdropLayer),
                typeof(RegionParticles),
                typeof(World.Paradise.ForegroundParallax),
                typeof(World.Paradise.SunAndClouds),
                typeof(World.Paradise.ZoneLook),
                typeof(World.Paradise.DistantBirds),
                typeof(World.Paradise.Butterflies),
            };
            foreach (var type in placed)
            {
                var order = type.GetCustomAttribute<DefaultExecutionOrder>()?.order ?? 0;
                Assert.That(camera.order, Is.LessThan(order), type.Name);
            }
        }

        [Test]
        public void EveryParadiseZonesRoomIsJoined()
        {
            foreach (var zone in ParadiseLayout.All())
            {
                var text = File.ReadAllText(ParadiseGreybox.ScenePath(zone));
                Assert.That(text, Does.Contain("joined: 1"), zone.scene);
                Assert.That(text, Does.Not.Contain("joined: 0"), zone.scene);
            }
        }
    }
}
