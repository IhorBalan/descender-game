using System.Collections;
using System.Collections.Generic;
using Descender.CameraRig;
using Descender.Input;
using Descender.Player;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The camera walking with the hero from one room into the next: joined rooms (one
    /// continuous land) never stop the view at their shared edge or cut it across, while a
    /// room on its own still holds the view inside it.
    /// </summary>
    public class CameraFollowTests
    {
        private const float ViewHalfHeight = 5.625f;
        private const float Border = 40f;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private GameCamera cameraBefore;
        private GameCamera view;
        private PlayerController hero;
        private ScriptedInput input;

        [SetUp]
        public void SetUp()
        {
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;
            cameraBefore = GameCamera.Main;

            var floor = Make("Floor");
            floor.layer = Combat.Layers.World;
            floor.transform.position = new Vector3(Border, -0.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(120f, 1f);

            var go = Make("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            go.transform.position = new Vector3(20f, 0f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            input = new ScriptedInput();
            hero.InputSource = input;

            var cameraObject = Make("Test camera");
            var lens = cameraObject.AddComponent<Camera>();
            lens.orthographic = true;
            lens.orthographicSize = ViewHalfHeight;
            lens.aspect = 16f / 9f;
            view = cameraObject.AddComponent<GameCamera>();
            view.Target = hero.transform;
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
        public IEnumerator WalkingIntoAJoinedRoomTheViewNeitherStopsNorCuts()
        {
            Room("West", 0f, true);
            Room("East", Border, true);
            yield return Steps(60);
            Assert.That(view.Bounds.xMax, Is.EqualTo(2f * Border), "the two rooms are one land to the camera");

            input.MoveX = 1f;
            yield return Steps(30);
            var biggest = 0f;
            var stride = 0f;
            var stalls = 0;
            var lastView = view.transform.position.x;
            var lastHero = hero.transform.position.x;
            var crossed = false;
            for (var i = 0; i < 300 && hero.transform.position.x < Border + 15f; i++)
            {
                yield return null;
                var moved = view.transform.position.x - lastView;
                var walked = hero.transform.position.x - lastHero;
                biggest = Mathf.Max(biggest, Mathf.Abs(moved));
                stride = Mathf.Max(stride, walked);
                if (walked > 0.05f && moved < 0.2f * walked)
                {
                    stalls++;
                }
                crossed |= hero.transform.position.x > Border + 1f;
                lastView = view.transform.position.x;
                lastHero = hero.transform.position.x;
            }
            input.MoveX = 0f;
            Assert.That(crossed, Is.True, "he walked into the east room");
            Assert.That(stalls, Is.Zero, "the view stopped while he walked on");
            // No step of the view much bigger than his: a cut across the edge would be 20 units.
            Assert.That(biggest, Is.LessThanOrEqualTo(1.5f * stride), "the view cut across");
        }

        [UnityTest]
        public IEnumerator ARoomOnItsOwnStillHoldsTheViewInside()
        {
            Room("West", 0f, false);
            Room("East", Border, false);
            yield return Steps(60);
            input.MoveX = 1f;
            var half = view.HalfExtents.x;
            for (var i = 0; i < 300 && hero.transform.position.x < Border - 1f; i++)
            {
                yield return null;
                Assert.That(view.transform.position.x, Is.LessThanOrEqualTo(Border - half + 0.001f), "inside its room");
            }
            input.MoveX = 0f;
        }

        private void Room(string name, float left, bool joined)
        {
            // Made inactive, so its Awake sizes the trigger from the fields set here.
            var go = Make(name);
            go.SetActive(false);
            go.transform.position = new Vector3(left, 10f, 0f);
            var room = go.AddComponent<WorldRoom>();
            var serialized = new SerializedObject(room);
            serialized.FindProperty("size").vector2Value = new Vector2(Border, 20f);
            serialized.FindProperty("joined").boolValue = joined;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            return go;
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
