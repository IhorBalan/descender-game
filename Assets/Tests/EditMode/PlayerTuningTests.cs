using Descender.Player;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    public class PlayerTuningTests
    {
        private PlayerTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<PlayerTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        [Test]
        public void JumpNumbersMatchTheGodotConstants()
        {
            // player.gd: JUMP_VELOCITY = -2 * 72 / 0.45, GRAVITY = 2 * 72 / 0.45^2, FALL_GRAVITY = 2 * 72 / 0.36^2
            Assert.That(tuning.JumpVelocity, Is.EqualTo(320f).Within(0.01f));
            Assert.That(tuning.Gravity, Is.EqualTo(711.111f).Within(0.01f));
            Assert.That(tuning.FallGravity, Is.EqualTo(1111.111f).Within(0.01f));
            Assert.That(tuning.DoubleJumpVelocity, Is.EqualTo(Mathf.Sqrt(2f * tuning.Gravity * 56f)).Within(0.001f));
        }

        [Test]
        public void JumpReachesItsHeightAtTheApexTime()
        {
            var height =
                tuning.JumpVelocity * tuning.jumpTimeToApex
                - 0.5f * tuning.Gravity * tuning.jumpTimeToApex * tuning.jumpTimeToApex;
            Assert.That(height, Is.EqualTo(tuning.jumpHeight).Within(0.01f));
        }

        [Test]
        public void UnitsDividePixelsBySixteen()
        {
            Assert.That(PlayerTuning.Units(32f), Is.EqualTo(2f));
            Assert.That(PlayerTuning.Units(new Vector2(16f, 8f)), Is.EqualTo(new Vector2(1f, 0.5f)));
        }
    }
}
