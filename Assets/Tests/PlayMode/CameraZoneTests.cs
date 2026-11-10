using System.Collections;
using System.Collections.Generic;
using Descender.CameraRig;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Camera zones in the lab's camera station, with the hero driven through a
    /// ScriptedInput: the view looks down at the edge and comes back without a pop, the
    /// corridor holds its height through a jump, and the arena pulls back.
    /// </summary>
    public class CameraZoneTests
    {
        private const float ViewHalfHeight = 5.625f;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private GameCamera cameraBefore;
        private LabRoot lab;
        private PlayerController hero;
        private ScriptedInput input;
        private GameCamera view;
        private Camera lens;
        private Station station;
        private int stationIndex;
        private float top;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;
            cameraBefore = GameCamera.Main;

            lab = LabBuilder.Build();
            made.Add(lab.gameObject);
            var go = new GameObject("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            made.Add(go);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            input = new ScriptedInput();
            hero.InputSource = input;

            var cameraObject = new GameObject("Test camera");
            made.Add(cameraObject);
            lens = cameraObject.AddComponent<Camera>();
            lens.orthographic = true;
            lens.orthographicSize = ViewHalfHeight;
            lens.aspect = 16f / 9f;
            view = cameraObject.AddComponent<GameCamera>();
            view.Target = hero.transform;

            stationIndex = lab.IndexOf("camera");
            station = lab.Stations[stationIndex];
            top = LabLayout.FloorTop + LabLayout.PlateauHeight;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = captureBefore;
            // Hand the title of main camera back to the one that had it.
            view.enabled = false;
            if (cameraBefore != null)
            {
                cameraBefore.enabled = false;
                cameraBefore.enabled = true;
            }
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.Destroy(thing);
                }
            }
            made.Clear();
        }

        [UnityTest]
        public IEnumerator TheViewLooksDownAtTheEdgeAndComesBackWithoutAPop()
        {
            yield return Steps(2);
            lab.Enter(hero, stationIndex);
            var startX = station.room.x + 21f;
            hero.PlaceAt(new Vector2(startX, top));
            yield return Steps(30);
            Assert.That(view.Zone, Is.Null, "between the corridor and the edge no zone holds the view");
            Assert.That(view.Settings.framing.y, Is.EqualTo(0f).Within(0.001f));
            var levelY = view.transform.position.y;

            // Into the edge zone: the framing goes down to the zone's and the view with it.
            var edge = Built("edge").GetComponent<CameraZone>();
            var biggestStep = 0f;
            var before = view.Settings.framing.y;
            input.MoveX = 1f;
            for (var i = 0; i < 200 && hero.transform.position.x < station.room.x + 24f; i++)
            {
                yield return null;
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(view.Settings.framing.y - before));
                before = view.Settings.framing.y;
            }
            input.MoveX = 0f;
            for (var i = 0; i < 60; i++)
            {
                yield return null;
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(view.Settings.framing.y - before));
                before = view.Settings.framing.y;
            }
            Assert.That(hero.IsGrounded, Is.True, "he stopped before the drop");
            Assert.That(view.Zone, Is.SameAs(edge));
            Assert.That(view.Settings.framing.y, Is.EqualTo(-LabLayout.LookDown).Within(0.001f));
            Assert.That(view.transform.position.y, Is.EqualTo(levelY - LabLayout.LookDown).Within(0.5f));

            // And back out: the room's framing returns.
            input.MoveX = -1f;
            for (var i = 0; i < 200 && hero.transform.position.x > startX; i++)
            {
                yield return null;
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(view.Settings.framing.y - before));
                before = view.Settings.framing.y;
            }
            input.MoveX = 0f;
            hero.PlaceAt(new Vector2(startX, top));
            for (var i = 0; i < 60; i++)
            {
                yield return null;
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(view.Settings.framing.y - before));
                before = view.Settings.framing.y;
            }
            Assert.That(view.Zone, Is.Null);
            Assert.That(view.Settings.framing.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(view.transform.position.y, Is.EqualTo(levelY).Within(0.5f));

            // Half a second of blend at fifty steps a second, eased: never more than this in one step.
            var most = LabLayout.LookDown * 1.5f * Time.fixedDeltaTime / edge.BlendTime;
            Assert.That(biggestStep, Is.GreaterThan(0f));
            Assert.That(biggestStep, Is.LessThanOrEqualTo(most + 0.01f), "the framing popped");
        }

        [UnityTest]
        public IEnumerator TheCorridorHoldsTheHeightThroughAJump()
        {
            yield return Steps(2);
            lab.Enter(hero, stationIndex);
            hero.PlaceAt(new Vector2(station.room.x + 16f, top));
            yield return Steps(60);
            var corridor = Built("corridor").GetComponent<CameraZone>();
            Assert.That(view.Zone, Is.SameAs(corridor));
            Assert.That(view.Settings.lockWeight.y, Is.EqualTo(1f).Within(0.001f));
            var heldY = corridor.Area.center.y;
            Assert.That(view.transform.position.y, Is.EqualTo(heldY).Within(0.1f));

            input.JumpPressed = true;
            input.JumpHeld = true;
            yield return null;
            input.ClearPresses();
            var highest = hero.transform.position.y;
            for (var i = 0; i < 40; i++)
            {
                yield return null;
                highest = Mathf.Max(highest, hero.transform.position.y);
                Assert.That(view.transform.position.y, Is.EqualTo(heldY).Within(0.1f), "the view moved with the jump");
            }
            input.JumpHeld = false;
            Assert.That(highest, Is.GreaterThan(top + 1f), "he did jump");
        }

        [UnityTest]
        public IEnumerator TheArenaPullsTheViewBackAndLeavingBringsItIn()
        {
            yield return Steps(2);
            lab.Enter(hero, stationIndex);
            yield return Steps(10);
            Assert.That(lens.orthographicSize, Is.EqualTo(ViewHalfHeight).Within(0.001f));

            hero.PlaceAt(new Vector2(station.room.x + 40f, LabLayout.FloorTop));
            yield return Steps(60);
            Assert.That(view.Zone, Is.SameAs(Built("arena").GetComponent<CameraZone>()));
            Assert.That(view.Settings.zoom, Is.EqualTo(LabLayout.ArenaZoom).Within(0.001f));
            Assert.That(lens.orthographicSize, Is.EqualTo(ViewHalfHeight * LabLayout.ArenaZoom).Within(0.001f));

            hero.PlaceAt(station.spawn);
            yield return Steps(60);
            Assert.That(view.Zone, Is.Null);
            Assert.That(lens.orthographicSize, Is.EqualTo(ViewHalfHeight).Within(0.001f));
        }

        /// <summary>The object a piece of the camera station was built as.</summary>
        private GameObject Built(string id)
        {
            var index = station.pieces.IndexOf(LabLayout.Find(station, id));
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"the camera station has no piece '{id}'");
            return lab.ParentOf(station).GetChild(index).gameObject;
        }

        private static IEnumerator Steps(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
