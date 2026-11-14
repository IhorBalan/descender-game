using System.Collections;
using Descender.EditorTools.Paradise;
using Descender.Player;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The light in Ansel (HeroLight on HeroRig.prefab), on a hero standing on a floor in a
    /// scene of his own: his outline is made behind all of him and keeps his pose, his cloak
    /// carries the light, a cut sends a surge down it and brightens him, and the afterimages
    /// leave the outline out.
    /// </summary>
    [Category("Rendering")]
    public class HeroLightTests
    {
        private static int made;
        private GameObject hero;
        private GameObject floor;
        private Scene scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene($"Hero light {++made}");
            SceneManager.SetActiveScene(scene);
            floor = new GameObject("Floor") { layer = LayerMask.NameToLayer("World") };
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            hero = new GameObject("Hero") { layer = LayerMask.NameToLayer("Player") };
            hero.transform.position = new Vector3(0f, 0.05f, 0f);
            var box = hero.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            var controller = hero.AddComponent<PlayerController>();
            var prefab = AssetDatabase.LoadAssetAtPath<HeroRig>(ParadiseHero.PrefabPath);
            Assert.That(prefab, Is.Not.Null, "no hero rig prefab");
            var rig = Object.Instantiate(prefab, hero.transform, false);
            rig.name = HeroRigInstaller.RigName;
            rig.Bind(controller);
            // Half a second of game time: he lands on the floor and the rig wakes.
            var until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(hero);
            Object.Destroy(floor);
            yield return null;
            if (scene.isLoaded && SceneManager.sceneCount > 1)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        private HeroLight Light() => hero.GetComponentInChildren<HeroLight>();

        [UnityTest]
        public IEnumerator HisOutlineIsDrawnBehindAllOfHimAndKeepsHisPose()
        {
            var light = Light();
            Assert.That(light, Is.Not.Null, "the rig has no HeroLight");
            Assert.That(light.Outlined, Is.True, "no outline");
            var rig = light.transform;
            var outline = rig.Find(RigCopy.Outline);
            Assert.That(outline, Is.Not.Null, "the outline is not under the rig");

            var lowestBody = int.MaxValue;
            foreach (var r in rig.GetComponentsInChildren<SpriteRenderer>())
            {
                if (RigCopy.IsBody(r.transform, rig))
                {
                    lowestBody = Mathf.Min(lowestBody, r.sortingOrder);
                }
            }
            var drawn = outline.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(drawn, Is.Not.Empty, "the outline draws nothing");
            foreach (var r in drawn)
            {
                Assert.That(r.sharedMaterial, Is.EqualTo(light.Silhouette), $"{r.name} of the outline");
                Assert.That(r.sortingOrder, Is.LessThan(lowestBody), $"{r.name} of the outline is in front of him");
            }

            // Bend a bone the way HeroRig does in LateUpdate: by the next frame the outline has it too.
            var head = FindDeep(rig, "Head", outline);
            var copiedHead = FindDeep(outline, "Head", null);
            Assert.That(head, Is.Not.Null);
            Assert.That(copiedHead, Is.Not.Null);
            yield return null;
            Assert.That(
                Quaternion.Angle(copiedHead.localRotation, head.localRotation),
                Is.LessThan(0.01f),
                "the outline's head is not posed as his"
            );
        }

        [UnityTest]
        public IEnumerator ACutSendsLightDownHisCloak()
        {
            var light = Light();
            foreach (var r in light.Cloth)
            {
                Assert.That(r.sharedMaterial.shader.name, Is.EqualTo("Descender/Hero Cloak Light"), r.name);
            }
            // Wait out any breath of light already on its way.
            var wait = 0;
            while (light.Surge >= 0f && wait++ < 300)
            {
                yield return null;
            }
            var before = light.Energy;
            var controller = hero.GetComponent<PlayerController>();
            var input = new Input.ScriptedInput();
            controller.InputSource = input;
            input.AttackPressed = true;
            yield return null;
            input.ClearPresses();
            var surged = false;
            var brightest = before;
            // Watched for a third of a second of game time: the light rises over a few frames.
            var until = Time.time + 0.35f;
            while (Time.time < until)
            {
                yield return null;
                surged |= light.Surge >= 0f;
                brightest = Mathf.Max(brightest, light.Energy);
            }
            Assert.That(controller.AttackCount, Is.GreaterThan(0), "he did not cut");
            Assert.That(surged, Is.True, "no surge ran down the cloak");
            Assert.That(brightest, Is.GreaterThan(before + 0.2f), "the cut did not brighten him");
            Assert.That(brightest, Is.LessThanOrEqualTo(HeroLight.MaxEnergy + 1e-3f));
        }

        [UnityTest]
        public IEnumerator TheAfterimagesLeaveTheOutlineOut()
        {
            var controller = hero.GetComponent<PlayerController>();
            var input = new Input.ScriptedInput();
            controller.InputSource = input;
            input.DashPressed = true;
            yield return null;
            input.ClearPresses();
            var ghosts = hero.GetComponentInChildren<HeroGhosts>();
            var until = Time.time + 0.3f;
            while (Time.time < until && ghosts.Showing == 0)
            {
                yield return null;
            }
            Assert.That(controller.IsDashing || ghosts.Showing > 0, Is.True, "he did not dash");
            Assert.That(ghosts.Showing, Is.GreaterThan(0), "no afterimage was left");
            foreach (var copy in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (copy.sharedMaterial == ghosts.Material && copy.enabled && copy.gameObject.activeInHierarchy)
                {
                    for (var t = copy.transform; t != null; t = t.parent)
                    {
                        Assert.That(t.name, Is.Not.EqualTo(RigCopy.Outline), "an afterimage carries the outline");
                    }
                }
            }
        }

        private static Transform FindDeep(Transform root, string name, Transform skip)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && (skip == null || !t.IsChildOf(skip)))
                {
                    return t;
                }
            }
            return null;
        }
    }
}
