using System.Linq;
using Descender.EditorTools.Paradise;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    public sealed class ParadiseFramingTests
    {
        private const string Nature = "Assets/Art/Paradise/Kit/Nature";
        private const float PixelsPerUnit = 48f;

        /// <summary>How far past the walk a rising piece may reach, and how far over it a hanging one stops.</summary>
        private const float RiseOverWalk = 1.6f;
        private const float HangStopsOverWalk = 3.4f;

        [TestCase(-20f, -28f)]
        [TestCase(-60f, -49f)]
        [TestCase(0f, 11f)]
        public void FramingIsWhereItWasPlacedWhenTheHeroStandsOnTheWalk(float ground, float target)
        {
            var anchor = ParadiseGarden.FrontAnchor(target, ground);
            var camera = ground + ParadiseGarden.CameraOverWalk;
            // What ForegroundParallax does to its y every frame.
            var seen = anchor - (camera - anchor) * ParadiseGarden.FrontDepth;
            Assert.That(seen, Is.EqualTo(target).Within(0.001f));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void FramingAlternatesAndStandsOnlyWhereThereIsGround(int number)
        {
            var zone = ParadiseLayout.All().First(z => z.number == number);
            var front = ParadiseGarden.GardenPlan(zone).foreground;
            Assert.That(front, Is.Not.Empty);
            for (var i = 0; i < front.Count; i++)
            {
                Assert.That(front[i].name, Does.StartWith(i % 2 == 0 ? "fg_soft_rise_" : "fg_soft_hang_"));
                Assert.That(float.IsNaN(ParadiseDressing.SurfaceY(zone, front[i].at.x, float.NaN)), Is.False);
            }
        }

        [Test]
        public void FramingFadesAwayOnARouteFarFromItsWalk()
        {
            const float walkView = -60f;
            var near = ParadiseGarden.FrontFadeNear;
            var far = ParadiseGarden.FrontFadeFar;
            Assert.That(ForegroundParallax.FadeAt(walkView, walkView, near, far), Is.EqualTo(1f));
            Assert.That(ForegroundParallax.FadeAt(walkView - near, walkView, near, far), Is.EqualTo(1f));
            Assert.That(ForegroundParallax.FadeAt(walkView - far, walkView, near, far), Is.Zero);
            Assert.That(ForegroundParallax.FadeAt(walkView + 14f, walkView, near, far), Is.Zero);
            var between = ForegroundParallax.FadeAt(walkView - (near + far) / 2f, walkView, near, far);
            Assert.That(between, Is.InRange(0.1f, 0.9f));
        }

        [Test]
        public void TheUndersideFringeNeverReachesARoundedCornersWallSide()
        {
            Assert.That(ParadiseGarden.UndersideReach, Is.LessThanOrEqualTo(30f));
        }

        [TestCase("fg_soft_rise_0")]
        [TestCase("fg_soft_rise_1")]
        [TestCase("fg_soft_rise_2")]
        public void ARisingPieceCannotReachTheHerosBody(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Nature}/{name}.png");
            var reach = texture.height / PixelsPerUnit - ParadiseGarden.FrontRiseFoot;
            Assert.That(reach, Is.LessThanOrEqualTo(RiseOverWalk + 0.01f));
        }

        [TestCase("fg_soft_hang_0")]
        [TestCase("fg_soft_hang_1")]
        [TestCase("fg_soft_hang_2")]
        public void AHangingPieceStopsAboveTheHero(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Nature}/{name}.png");
            var lowest = ParadiseGarden.FrontHangTop - texture.height / PixelsPerUnit;
            Assert.That(lowest, Is.GreaterThanOrEqualTo(HangStopsOverWalk - 0.01f));
        }

        [Test]
        public void HangingFramingSwingsFromItsTopAndNoFramingIsHazedAgain()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity",
                OpenSceneMode.Additive
            );
            try
            {
                var pieces = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                    .Where(r => r.name.StartsWith("fg_soft_"))
                    .ToList();
                Assert.That(pieces, Is.Not.Empty);
                foreach (var piece in pieces)
                {
                    var hangs = piece.name.Contains("hang");
                    Assert.That(piece.sharedMaterial.GetFloat("_SwayHang"), Is.EqualTo(hangs ? 1f : 0f), piece.name);
                    Assert.That(piece.sharedMaterial.GetFloat("_HazeAmount"), Is.EqualTo(0f), piece.name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
