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
    /// A backdrop pinned to the view while a camera zone pulls the view back: it grows about
    /// the view's middle with the zoom, so it looks the same on screen and its edges never
    /// come into sight, while a layer that scrolls with the level keeps its size.
    /// </summary>
    [Category("Rendering")]
    public class BackdropZoomTests
    {
        private const float ViewHalfHeight = 5.625f;
        private const float PullBack = 1.3f;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private GameCamera cameraBefore;
        private GameCamera view;
        private PlayerController hero;

        [SetUp]
        public void SetUp()
        {
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;
            cameraBefore = GameCamera.Main;

            // A floor, so the hero stands and the view stays still while it zooms.
            var floor = Make("Floor");
            floor.layer = Combat.Layers.World;
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);

            var go = Make("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            hero.InputSource = new ScriptedInput();

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
        public IEnumerator APinnedBackdropKeepsItsLookOnScreenWhileTheViewPullsBack()
        {
            var pinned = Layer("Pinned", Vector2.zero, new Vector2(2f, 3f));
            var level = Layer("Level", Vector2.one, Vector2.zero);
            yield return Steps(60);
            var rest = pinned.localScale;
            var levelRest = level.localScale;
            var onScreen = pinned.position - view.transform.position;
            Assert.That(onScreen.x, Is.EqualTo(2f).Within(0.01f), "pinned where its offset puts it");
            Assert.That(onScreen.y, Is.EqualTo(3f).Within(0.01f));

            // A zone around the hero pulls the view back; the room's blend runs out.
            var zone = Make("Pull back");
            zone.transform.position = hero.transform.position;
            zone.AddComponent<CameraZone>()
                .Configure(new Vector2(400f, 400f), Vector2.zero, PullBack, false, false, -1f, 0.2f);
            for (var i = 0; i < 120 && !Mathf.Approximately(view.Zoom, PullBack); i++)
            {
                yield return null;
            }
            yield return Steps(5);

            Assert.That(view.Zoom, Is.EqualTo(PullBack).Within(0.001f), "the zone pulled the view back");
            var pulled = pinned.position - view.transform.position;
            Assert.That(pulled.x, Is.EqualTo(onScreen.x * PullBack).Within(0.01f), "the same place on screen");
            Assert.That(pulled.y, Is.EqualTo(onScreen.y * PullBack).Within(0.01f));
            Assert.That(pinned.localScale.x, Is.EqualTo(rest.x * PullBack).Within(0.001f), "the same size on screen");
            Assert.That(pinned.localScale.y, Is.EqualTo(rest.y * PullBack).Within(0.001f));
            Assert.That(level.localScale, Is.EqualTo(levelRest), "a layer moving with the level keeps its size");
        }

        [Test]
        public void OnlyALayerPinnedToTheViewGrowsWithTheZoom()
        {
            Assert.That(BackdropLayer.PinnedZoom(new Vector2(0.4f, 0f), 1.3f), Is.EqualTo(1.3f));
            Assert.That(BackdropLayer.PinnedZoom(Vector2.zero, 0.9f), Is.EqualTo(0.9f), "pushed in too");
            Assert.That(BackdropLayer.PinnedZoom(new Vector2(0.4f, 0.5f), 1.3f), Is.EqualTo(1f));
            Assert.That(BackdropLayer.PinnedZoom(Vector2.zero, 1f), Is.EqualTo(1f), "the room's view");
        }

        private Transform Layer(string name, Vector2 scroll, Vector2 offset)
        {
            var go = Make(name);
            var texture = new Texture2D(64, 32);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 32f), new Vector2(0.5f, 0.5f), 16f);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            var layer = go.AddComponent<BackdropLayer>();
            var serialized = new SerializedObject(layer);
            serialized.FindProperty("scroll").vector2Value = scroll;
            serialized.FindProperty("viewOffset").vector2Value = offset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
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
