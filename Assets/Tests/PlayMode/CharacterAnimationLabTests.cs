using System.Collections;
using Descender.EditorTools.Motion;
using Descender.Motion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The CharacterAnimationLab scene played: it loads on its own with the runner, its
    /// overlay and the lab; the runner has its whole skeleton of bones; played frame by
    /// frame at 60 a second it travels at its running speed, its bones keep their lengths,
    /// it wears the knight's parts (taken off with K),
    /// a foot on the ground stays where it came down and the overlay marks each contact;
    /// and the overlay can be hidden. The scene is loaded by its path, which only the
    /// editor can do, and unloaded again.
    /// </summary>
    public class CharacterAnimationLabTests
    {
        private static readonly string[] BoneNames =
        {
            "Hips",
            "Spine",
            "Chest",
            "Neck",
            "Head",
            "Shoulder.L",
            "UpperArm.L",
            "LowerArm.L",
            "Hand.L",
            "Shoulder.R",
            "UpperArm.R",
            "LowerArm.R",
            "Hand.R",
            "Thigh.L",
            "Shin.L",
            "Foot.L",
            "Thigh.R",
            "Shin.R",
            "Foot.R",
        };

        private int frameRateBefore;

        [SetUp]
        public void SetUp() => frameRateBefore = Application.targetFrameRate;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.targetFrameRate = frameRateBefore;
            var scene = SceneManager.GetSceneByPath(CharacterAnimationLabScene.ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator TheRunnerRunsWithoutSlidingAndTheOverlayFollows()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                CharacterAnimationLabScene.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
#endif
            yield return null;
            var lab = Object.FindAnyObjectByType<CharacterAnimationLab>();
            Assert.That(lab, Is.Not.Null, "the lab scene did not load");
            var runner = lab.Skeleton;
            var overlay = lab.Overlay;
            Assert.That(runner, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            foreach (var name in BoneNames)
            {
                Assert.That(runner.Bone(name), Is.Not.Null, name);
            }
            Assert.That(runner.Bones.Count, Is.EqualTo(BoneNames.Length));
            Assert.That(runner.Bone("Shin.L").parent, Is.EqualTo(runner.Bone("Thigh.L")), "the bones are a hierarchy");

            // The scene's runner plays the run as tuned in code (the scene is made again when it changes).
            var tuned = new RunGaitSettings();
            tuned.Resolve();
            Assert.That(runner.Settings.torsoLean, Is.EqualTo(tuned.torsoLean), "torso lean");
            Assert.That(runner.Settings.bodyLean, Is.EqualTo(tuned.bodyLean), "body lean");
            Assert.That(runner.Settings.armSwing, Is.EqualTo(tuned.armSwing), "arm swing");
            Assert.That(runner.Settings.strideLength, Is.EqualTo(tuned.strideLength).Within(1e-4f), "stride");

            // Played by hand, a sixtieth of a second a frame.
            runner.Paused = true;
            var start = runner.transform.position.x;
            var settings = runner.Settings;
            var body = runner.Body;
            float? planted = null;
            var checkedFrames = 0;
            float shoulderLow = float.MaxValue,
                shoulderHigh = float.MinValue;
            float armLow = float.MaxValue,
                armHigh = float.MinValue;
            for (var frame = 0; frame < 120; frame++)
            {
                runner.Advance(CharacterAnimationLab.Step);
                yield return null;
                var thigh = Vector3.Distance(runner.Bone("Thigh.R").position, runner.Bone("Shin.R").position);
                var shin = Vector3.Distance(runner.Bone("Shin.R").position, runner.Bone("Foot.R").position);
                Assert.That(thigh, Is.EqualTo(body.thigh).Within(1e-3f));
                Assert.That(shin, Is.EqualTo(body.shin).Within(1e-3f));
                // The shoulder (the pauldron's bone) sits on the arm's joint and keeps to the torso,
                // turning only with a share of the arm's swing.
                var shoulder = runner.Bone("Shoulder.L");
                var upperArm = runner.Bone("UpperArm.L");
                Assert.That(
                    Vector3.Distance(shoulder.position, upperArm.position),
                    Is.LessThan(1e-3f),
                    $"frame {frame}: the shoulder off the arm's joint"
                );
                var spine = runner.Pose.chest - runner.Pose.hips;
                var spineAngle = Mathf.Atan2(spine.y, spine.x) * Mathf.Rad2Deg;
                var toShoulder = Mathf.DeltaAngle(spineAngle, shoulder.eulerAngles.z);
                var toArm = Mathf.DeltaAngle(spineAngle + 180f, upperArm.eulerAngles.z); // from hanging down the spine
                shoulderLow = Mathf.Min(shoulderLow, toShoulder);
                shoulderHigh = Mathf.Max(shoulderHigh, toShoulder);
                armLow = Mathf.Min(armLow, toArm);
                armHigh = Mathf.Max(armHigh, toArm);
                var leg = runner.Pose.rightLeg;
                if (leg.contact)
                {
                    var toes = runner.World(leg.tip).x;
                    planted ??= toes;
                    Assert.That(toes, Is.EqualTo(planted.Value).Within(1e-3f), $"frame {frame}: the right foot slid");
                    checkedFrames++;
                }
                else
                {
                    planted = null;
                }
            }
            Assert.That(checkedFrames, Is.GreaterThan(20));
            Assert.That(
                shoulderHigh - shoulderLow,
                Is.EqualTo((armHigh - armLow) * RunSkeleton.ShoulderFollow).Within(1f),
                "the shoulder turns with the torso and a share of the arm's swing, no more"
            );
            Assert.That(shoulderHigh - shoulderLow, Is.GreaterThan(5f), "the shoulder follows the arm a little");
            Assert.That(
                runner.transform.position.x - start,
                Is.EqualTo(settings.runningSpeed * 2f).Within(0.01f),
                "two seconds at the running speed"
            );
            Assert.That(overlay.Contacts, Is.GreaterThanOrEqualTo(4), "the overlay marked the contacts");

            // The knight is worn: every part on its bones, limbs on both sides, the shapes hidden; K takes him off.
            var skin = lab.Skin;
            Assert.That(skin, Is.Not.Null, "the knight's parts");
            Assert.That(skin.Count, Is.EqualTo(17), "five parts on one bone each and six limb parts on both sides");
            Assert.That(overlay.Visible, Is.False, "with the knight on the bones start hidden");
            Assert.That(
                skin.WornOn("pauldron", "Shoulder.L").sortingOrder,
                Is.GreaterThan(skin.WornOn("sleeve", "UpperArm.L").sortingOrder),
                "the pauldron over the sleeve"
            );
            Assert.That(
                skin.WornOn("thigh", "Thigh.L").sortingOrder,
                Is.LessThan(skin.WornOn("torso", "Hips").sortingOrder),
                "the thigh under the tassets"
            );
            Assert.That(runner.ShapesShown, Is.False);
            foreach (var bone in new[] { "Thigh.L", "Thigh.R" })
            {
                var thighPart = skin.WornOn("thigh", bone);
                Assert.That(thighPart, Is.Not.Null, bone);
                Assert.That(
                    Vector3.Distance(thighPart.transform.position, runner.Bone(bone).position),
                    Is.LessThan(1e-3f),
                    "it hangs from the hip"
                );
            }
            Assert.That(
                skin.WornOn("thigh", "Thigh.R").sortingOrder,
                Is.LessThan(skin.WornOn("thigh", "Thigh.L").sortingOrder),
                "the far leg is behind"
            );
            Assert.That(skin.WornOn("sword", "Hand.R"), Is.Not.Null, "the sword is in his far hand");
            Assert.That(skin.WornOn("head", "Head"), Is.Not.Null);

            // The painted run is up first, a frame for each key pose, standing on the runner's root.
            var painted = lab.Painted;
            Assert.That(painted, Is.Not.Null, "the painted run");
            Assert.That(painted.Count, Is.EqualTo(RunGait.KeyCount));
            Assert.That(lab.Current, Is.EqualTo(CharacterAnimationLab.Look.Painted));
            Assert.That(painted.View.enabled, Is.True, "the painted run shows");
            Assert.That(skin.WornOn("head", "Head").enabled, Is.False, "the parts are hidden under it");
            Assert.That(runner.ShapesShown, Is.False, "and so are the shapes");
            Assert.That(
                Vector3.Distance(painted.View.transform.position, runner.transform.position),
                Is.LessThan(1e-4f),
                "it stands on the runner's root"
            );
            for (var key = 0; key < RunGait.KeyCount; key++)
            {
                runner.ShowAt(key / (float)RunGait.KeyCount);
                painted.Show();
                Assert.That(painted.Frame, Is.EqualTo(key), $"key pose {key}");
                Assert.That(painted.View.sprite, Is.Not.Null);
            }

            // K: the parts on the bones, then the bare bones, then the painted run again.
            lab.ShowNext();
            Assert.That(lab.Current, Is.EqualTo(CharacterAnimationLab.Look.Parts));
            Assert.That(painted.View.enabled, Is.False);
            Assert.That(skin.WornOn("head", "Head").enabled, Is.True);
            Assert.That(runner.ShapesShown, Is.False);
            lab.ShowNext();
            Assert.That(lab.Current, Is.EqualTo(CharacterAnimationLab.Look.Bones));
            Assert.That(skin.WornOn("head", "Head").enabled, Is.False);
            Assert.That(runner.ShapesShown, Is.True, "without the knight the shapes show");
            lab.ShowNext();
            Assert.That(lab.Current, Is.EqualTo(CharacterAnimationLab.Look.Painted));
            Assert.That(painted.View.enabled, Is.True);
            Assert.That(runner.ShapesShown, Is.False);

            overlay.Visible = false;
            yield return null;
            Assert.That(overlay.Visible, Is.False);
            overlay.Visible = true;
        }
    }
}
