using System.Collections.Generic;
using System.Linq;
using Descender.CameraRig;
using Descender.EditorTools.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The camera moments at the landmarks of zones 1 and 2: each landmark has one, they
    /// stay in their zone, never hide the way ahead, and are built into the zone scenes as
    /// camera zones; the Paradise camera zooms smoothly, filling the screen.
    /// </summary>
    public sealed class ParadiseCameraMomentTests
    {
        /// <summary>The design's rule (docs/design/paradise.md): the hero sees at least 8 tiles ahead.</summary>
        private const float TilesAhead = 8f;

        private static ParadiseLayout.Zone Zone(int number) => ParadiseLayout.All().First(z => z.number == number);

        private static ParadiseLayout.Moment Moment(ParadiseLayout.Zone zone, string name) =>
            zone.moments.Single(m => m.name == name);

        private static Vector2 Marked(ParadiseLayout.Zone zone, string label) =>
            zone.markers.Single(m => m.label == label).at;

        private static Vector2 Placed(ParadiseLayout.Zone zone, string name) =>
            zone.placements.Single(p => p.Name == name).at;

        [Test]
        public void EachLandmarkOfTheHollowAndTheOrchardHasItsMoment()
        {
            var hollow = Zone(1);
            Assert.That(Moment(hollow, "Waking").area.Contains(ParadiseLayout.Start), "he wakes in it");
            Assert.That(
                Moment(hollow, "Waking").area.Contains(Marked(hollow, "Secret: Wings ledge")),
                "the ledge seen on the first screen"
            );
            Assert.That(Moment(hollow, "RisingArcade").area.Contains(Marked(hollow, "Landmark: the Rising Arcade")));
            Assert.That(Moment(hollow, "ShrineRest").area.Contains(hollow.saves[0] + Vector2.up));

            var orchard = Zone(2);
            Assert.That(Moment(orchard, "Mereth").area.Contains(Placed(orchard, "mereth") + Vector2.up));
            var tree = Moment(orchard, "LanternTree");
            Assert.That(tree.area.Contains(Marked(orchard, "Landmark: the lantern tower")));
            Assert.That(tree.area.Contains(orchard.saves.Single(s => s.x > 880f && s.x < 945f) + Vector2.up));
            Assert.That(tree.zoom, Is.GreaterThan(1f), "the tree is shown whole: the view pulls back");
            Assert.That(tree.framing.y, Is.GreaterThan(0f), "and looks up its tower");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MomentsStayInTheirZoneAndNeverHideTheWayAhead(int number)
        {
            var zone = Zone(number);
            Assert.That(zone.moments, Is.Not.Empty);
            var room = zone.rooms.Single();
            foreach (var moment in zone.moments)
            {
                var area = moment.area;
                Assert.That(area.xMin, Is.GreaterThanOrEqualTo(zone.xMin), moment.name);
                Assert.That(area.xMax, Is.LessThanOrEqualTo(zone.xMax), moment.name);
                Assert.That(area.yMin, Is.GreaterThanOrEqualTo(room.yMin), moment.name);
                Assert.That(area.yMax, Is.LessThanOrEqualTo(room.yMax), moment.name);
                // Pushed in a little at most, so he still sees 8 tiles ahead; pulled back
                // little enough that the walk does not shrink away.
                var halfWidth = ParadiseLayout.ViewHalfHeight * 16f / 9f * moment.zoom;
                Assert.That(halfWidth, Is.GreaterThanOrEqualTo(TilesAhead), $"{moment.name} sees ahead");
                Assert.That(moment.zoom, Is.LessThanOrEqualTo(1.35f), $"{moment.name} keeps the hero readable");
                Assert.That(moment.blend, Is.GreaterThanOrEqualTo(0.5f), $"{moment.name} eases in, never cuts");
            }
            // One moment at a time: none overlaps another, so walking through is one change.
            for (var i = 0; i < zone.moments.Count; i++)
            {
                for (var j = i + 1; j < zone.moments.Count; j++)
                {
                    var a = zone.moments[i].area;
                    var b = zone.moments[j].area;
                    var overlap = a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;
                    Assert.That(overlap, Is.False, $"{zone.moments[i].name} and {zone.moments[j].name}");
                }
            }
        }

        [TestCase(1, "Paradise_Z1_WakingHollow")]
        [TestCase(2, "Paradise_Z2_Orchard")]
        public void TheMomentsAreBuiltIntoTheZoneSceneAsCameraZones(int number, string sceneName)
        {
            var zone = Zone(number);
            var scene = EditorSceneManager.OpenScene(
                $"{ParadiseLayout.SceneFolder}/{sceneName}.unity",
                OpenSceneMode.Additive
            );
            try
            {
                var group = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Single(t => t.name == ParadiseGreybox.MomentsGroup);
                var built = group.GetComponentsInChildren<CameraZone>(true).ToDictionary(z => z.name);
                Assert.That(built.Keys, Is.EquivalentTo(zone.moments.Select(m => m.name)));
                foreach (var moment in zone.moments)
                {
                    var camera = built[moment.name];
                    Assert.That((Vector2)camera.transform.position, Is.EqualTo(moment.area.center), moment.name);
                    var size = new SerializedObject(camera).FindProperty("size").vector2Value;
                    Assert.That(size, Is.EqualTo(moment.area.size), moment.name);
                    Assert.That(camera.Zoom, Is.EqualTo(moment.zoom), moment.name);
                    Assert.That(camera.Framing, Is.EqualTo(moment.framing), moment.name);
                    Assert.That(camera.LockX || camera.LockY, Is.False, $"{moment.name} follows the hero");
                    Assert.That(camera.BlendTime, Is.EqualTo(moment.blend), moment.name);
                    Assert.That(camera.GetComponent<BoxCollider2D>().isTrigger, Is.True, moment.name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheSunAndItsFlecksReachPastTheViewAtEveryMoment()
        {
            var widest = ParadiseLayout.All().SelectMany(z => z.moments).Max(m => m.zoom);
            var half = new Vector2(ParadiseLayout.ViewHalfHeight * 16f / 9f, ParadiseLayout.ViewHalfHeight) * widest;
            var region = EditorSceneManager.OpenScene(
                "Assets/Scenes/Paradise/Region_paradise_new.unity",
                OpenSceneMode.Additive
            );
            try
            {
                var sun = new SerializedObject(
                    region
                        .GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<World.Paradise.SunAndClouds>(true))
                        .Single()
                );
                // The clouds' pattern holds two periods, the flecks' three: each light is never
                // more than half a period off the view, so this far past its middle is always lit.
                var clouds = World.Paradise.SunAndClouds.Reach(sun.FindProperty("period").vector2Value, 2);
                var flecks = World.Paradise.SunAndClouds.Reach(
                    sun.FindProperty("fleckPeriod").vector2Value,
                    World.Paradise.SunAndClouds.FleckPeriods
                );
                foreach (var reach in new[] { clouds, flecks })
                {
                    Assert.That(reach.x, Is.GreaterThanOrEqualTo(half.x), "past the sides of the widest view");
                    Assert.That(reach.y, Is.GreaterThanOrEqualTo(half.y), "past its top and bottom");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(region, true);
            }
        }

        [Test]
        public void TheParadiseCameraFillsTheScreenAndZoomsSmoothly()
        {
            var scene = EditorSceneManager.OpenScene(ParadiseGreybox.EntryScenePath, OpenSceneMode.Additive);
            try
            {
                var camera = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                    .Single(c => c.GetComponent<GameCamera>() != null);
                // No pixel-perfect box: it held the view to whole multiples of 320x180, black
                // bars between them and a zone's pull-back stepping from one to the next.
                var boxes = camera.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name);
                Assert.That(boxes, Has.No.Member("PixelPerfectCamera"));
                Assert.That(camera.orthographic, Is.True);
                Assert.That(
                    camera.orthographicSize,
                    Is.EqualTo(ParadiseLayout.ViewHalfHeight),
                    "one screen: 11.25 tiles"
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
