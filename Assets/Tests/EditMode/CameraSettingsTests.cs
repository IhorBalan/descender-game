using System.Collections.Generic;
using Descender.CameraRig;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The maths of camera zones: blending two sets of settings never jumps, a held axis
    /// fades in without its place moving, the innermost zone wins, and a zone's settings
    /// are the room's with its own on top.
    /// </summary>
    public class CameraSettingsTests
    {
        private GameObject zoneObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(zoneObject);
        }

        private static CameraSettings Room()
        {
            var room = new CameraSettings();
            room.framing = new Vector2(0f, 1f);
            room.zoom = 1f;
            room.lookAheadPixels = 24f;
            return room;
        }

        private static CameraSettings LookDown()
        {
            var zone = Room();
            zone.framing = new Vector2(0f, -5f);
            zone.zoom = 1.5f;
            zone.lookAheadPixels = 0f;
            return zone;
        }

        [Test]
        public void ABlendStartsAtOneSetAndEndsAtTheOther()
        {
            var start = CameraSettings.Blend(Room(), LookDown(), 0f);
            var end = CameraSettings.Blend(Room(), LookDown(), 1f);
            var past = CameraSettings.Blend(Room(), LookDown(), 3f);

            Assert.That(start.framing, Is.EqualTo(Room().framing));
            Assert.That(start.zoom, Is.EqualTo(1f));
            Assert.That(start.lookAheadPixels, Is.EqualTo(24f));
            Assert.That(end.framing, Is.EqualTo(LookDown().framing));
            Assert.That(end.zoom, Is.EqualTo(1.5f));
            Assert.That(end.lookAheadPixels, Is.EqualTo(0f));
            Assert.That(past.framing, Is.EqualTo(end.framing), "a blend that ran over stays at the end");
        }

        [Test]
        public void ABlendNeverJumps()
        {
            // Eased at both ends, the fastest it moves is one and a half times the even pace.
            const int steps = 100;
            var whole = Mathf.Abs(LookDown().framing.y - Room().framing.y);
            var most = whole * 1.5f / steps + 0.0001f;
            var before = CameraSettings.Blend(Room(), LookDown(), 0f);
            for (var i = 1; i <= steps; i++)
            {
                var now = CameraSettings.Blend(Room(), LookDown(), i / (float)steps);
                Assert.That(Mathf.Abs(now.framing.y - before.framing.y), Is.LessThanOrEqualTo(most), $"step {i}");
                Assert.That(now.framing.y, Is.LessThanOrEqualTo(before.framing.y), "always toward the zone");
                Assert.That(now.zoom, Is.GreaterThanOrEqualTo(before.zoom));
                before = now;
            }
            var first = CameraSettings.Blend(Room(), LookDown(), 1f / steps);
            Assert.That(Mathf.Abs(first.framing.y - Room().framing.y), Is.LessThan(whole / steps), "it starts gently");
        }

        [Test]
        public void AHeldAxisFadesInWithoutItsPlaceMoving()
        {
            var held = Room();
            held.lockWeight = new Vector2(0f, 1f);
            held.lockAt = new Vector2(0f, 10f);
            var following = new Vector2(3f, 0f);
            var before = 0f;
            for (var i = 0; i <= 20; i++)
            {
                var now = CameraSettings.Blend(Room(), held, i / 20f);
                Assert.That(now.lockAt.y, Is.EqualTo(10f), "the place does not slide in from nowhere");
                var at = now.Hold(following);
                Assert.That(at.x, Is.EqualTo(3f), "the free axis follows the hero");
                Assert.That(at.y, Is.GreaterThanOrEqualTo(before));
                before = at.y;
            }
            Assert.That(before, Is.EqualTo(10f).Within(0.0001f), "fully held at the end");

            var back = CameraSettings.Blend(held, Room(), 0.5f);
            Assert.That(back.lockAt.y, Is.EqualTo(10f), "and it lets go from the same place");
            Assert.That(back.lockWeight.y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void BetweenTwoHeldPlacesThePlaceSlides()
        {
            var low = Room();
            low.lockWeight = Vector2.one;
            low.lockAt = new Vector2(0f, 0f);
            var high = Room();
            high.lockWeight = Vector2.one;
            high.lockAt = new Vector2(8f, 4f);

            var middle = CameraSettings.Blend(low, high, 0.5f);

            Assert.That(middle.lockAt.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(middle.lockAt.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(middle.Hold(new Vector2(100f, 100f)), Is.EqualTo(middle.lockAt));
        }

        [Test]
        public void TheInnermostZoneIsTheSmallest()
        {
            var areas = new List<Rect>();
            Assert.That(CameraSettings.Innermost(areas), Is.EqualTo(-1));
            areas.Add(new Rect(0f, 0f, 40f, 20f));
            areas.Add(new Rect(10f, 0f, 4f, 4f));
            areas.Add(new Rect(5f, 0f, 20f, 10f));
            Assert.That(CameraSettings.Innermost(areas), Is.EqualTo(1));
        }

        [Test]
        public void AZoneLaysItsSettingsOverTheRooms()
        {
            zoneObject = new GameObject("Zone");
            zoneObject.transform.position = new Vector3(20f, 6f, 0f);
            var zone = zoneObject.AddComponent<CameraZone>();
            zone.Configure(new Vector2(8f, 4f), new Vector2(0f, -3f), 2f, false, true);

            var settings = zone.Settings(Room());

            Assert.That(zone.Area, Is.EqualTo(new Rect(16f, 4f, 8f, 4f)));
            Assert.That(settings.framing, Is.EqualTo(new Vector2(0f, -2f)), "the room's framing and the zone's");
            Assert.That(settings.zoom, Is.EqualTo(2f));
            Assert.That(settings.lookAheadPixels, Is.EqualTo(24f), "the room's look-ahead is kept");
            Assert.That(settings.lockWeight, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(settings.lockAt.y, Is.EqualTo(3f), "held at the zone's middle, shifted by its framing");
            var box = zoneObject.GetComponent<BoxCollider2D>();
            Assert.That(box.isTrigger, Is.True);
            Assert.That(box.size, Is.EqualTo(new Vector2(8f, 4f)));
            Assert.That(zoneObject.layer, Is.EqualTo(LayerMask.NameToLayer("Trigger")));
        }

        [Test]
        public void AZoneCanSetItsOwnLookAhead()
        {
            zoneObject = new GameObject("Zone");
            var zone = zoneObject.AddComponent<CameraZone>();
            zone.Configure(new Vector2(8f, 4f), Vector2.zero, 1f, false, false, 0f, 0.25f);

            Assert.That(zone.Settings(Room()).lookAheadPixels, Is.EqualTo(0f));
            Assert.That(zone.BlendTime, Is.EqualTo(0.25f));
        }
    }
}
