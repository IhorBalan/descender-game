using Descender.Enemies;
using Descender.Fx;
using Descender.Pickups;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>The small, pure pieces of the hero, enemy and pickup port.</summary>
    public class CombatTests
    {
        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Kills.Reset();
        }

        [Test]
        public void KillsRememberPlacementsUntilCleared()
        {
            var cleared = 0;
            Kills.Cleared += () => cleared++;
            Kills.Record("limbo:imp:3");
            Kills.Record("");
            Assert.That(Kills.IsDead("limbo:imp:3"), Is.True);
            Assert.That(Kills.IsDead(""), Is.False);
            Assert.That(Kills.Count, Is.EqualTo(1));
            Kills.Clear();
            Assert.That(Kills.Count, Is.EqualTo(0));
            Assert.That(cleared, Is.EqualTo(1));
        }

        [Test]
        public void KillsTellThePlacementsWhatIsGone()
        {
            Kills.Hook();
            Assert.That(World.WorldPlacement.IsGone, Is.Not.Null);
            Kills.Record("limbo/imp 3");
            Assert.That(World.WorldPlacement.IsGone("limbo/imp 3"), Is.True);
            Assert.That(World.WorldPlacement.IsGone("limbo/imp 4"), Is.False);
            Kills.Clear();
            Assert.That(World.WorldPlacement.IsGone("limbo/imp 3"), Is.False);
        }

        [Test]
        public void UpgradeFlagsFollowTheGodotNames()
        {
            Assert.That(Upgrades.FlagFor(UpgradeKind.Heart, "limbo", "2"), Is.EqualTo("upgrade:heart:limbo:2"));
            Assert.That(Upgrades.FlagPrefix(UpgradeKind.Iron), Is.EqualTo("upgrade:iron:"));
            Story.StoryFlags.Set("sword:1");
            Story.StoryFlags.Set("sword:2");
            Story.StoryFlags.Set(Upgrades.FlagFor(UpgradeKind.Iron, "lust", "0"));
            Story.StoryFlags.Set(Upgrades.FlagFor(UpgradeKind.Iron, "lust", "1"));
            Assert.That(Upgrades.SwordBonus(), Is.EqualTo(2));
            // Pell's first forging is from his own stock, so one bar of two is spent.
            Assert.That(Upgrades.IronCarried(), Is.EqualTo(1));
            Assert.That(Upgrades.Count(UpgradeKind.Heart), Is.EqualTo(0));
        }

        [Test]
        public void WalkerBehavioursParseFromTheMapNames()
        {
            Assert.That(Walker.Parse("chase"), Is.EqualTo(WalkerBehaviour.Chase));
            Assert.That(Walker.Parse("Pounce"), Is.EqualTo(WalkerBehaviour.Pounce));
            Assert.That(Walker.Parse(""), Is.EqualTo(WalkerBehaviour.None));
            Assert.That(Walker.Parse("nonsense"), Is.EqualTo(WalkerBehaviour.None));
        }

        [Test]
        public void BossIdsComeFromTheirLabels()
        {
            Assert.That(Boss.IdFor("Seraph Warden"), Is.EqualTo("seraph_warden"));
            Assert.That(Boss.IdFor("The Bloated One"), Is.EqualTo("the_bloated_one"));
            Assert.That(Checkpoint.RestedFlag(3), Is.EqualTo("rested:3"));
        }

        [Test]
        public void SpriteAnimatorKnowsItsStrips()
        {
            var holder = new GameObject("Animated");
            try
            {
                var animator = SpriteAnimator.CreateChild(holder.transform);
                var frame = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0f), 16f);
                animator.SetStrips(
                    new[]
                    {
                        new SpriteAnimator.Strip
                        {
                            name = "idle",
                            fps = 6f,
                            loop = true,
                            frames = new[] { frame, frame },
                        },
                        new SpriteAnimator.Strip
                        {
                            name = "attack",
                            fps = 12f,
                            loop = false,
                            frames = new[] { frame },
                        },
                    }
                );
                Assert.That(animator.Has("idle"), Is.True);
                Assert.That(animator.Has("glide"), Is.False);
                Assert.That(animator.Length("idle"), Is.EqualTo(2f / 6f).Within(0.001f), "two frames at six a second");
                animator.Play("attack");
                Assert.That(animator.Animation, Is.EqualTo("attack"));
                Assert.That(animator.Renderer.sprite, Is.EqualTo(frame));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void PowerUpColoursAndMessagesCoverEveryPower()
        {
            foreach (var power in HeroPowers.All)
            {
                Assert.That(PowerUp.MessageFor(power), Does.Not.Contain("Power-up"));
                Assert.That(PowerUp.ColorOf(power).a, Is.EqualTo(1f));
            }
        }
    }
}
