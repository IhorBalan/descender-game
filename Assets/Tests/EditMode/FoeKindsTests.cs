using System.Linq;
using Descender.Enemies;
using Descender.Fx;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The old walkers as foe recipes: the body, the swing and the drops carry the
    /// profile's numbers, each habit a placement can name becomes its parts in the
    /// order they are asked, a tall body still pounces at a hero on its own floor, and
    /// a passive one only patrols.
    /// </summary>
    public class FoeKindsTests
    {
        private WalkerProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<WalkerProfile>();
            profile.body = new Vector2(36f, 62f);
            profile.speed = 14f;
            profile.health = 8;
            profile.armour = true;
            profile.attackRange = 52f;
            profile.reach = new Vector2(46f, 48f);
            profile.windup = 0.75f;
            profile.strike = 0.25f;
            profile.recover = 0.9f;
            profile.cooldown = 1.5f;
            profile.attackDamage = 2;
            profile.dropChance = 1f;
            profile.power = PowerKind.Halo;
            profile.powerChance = 0.5f;
            profile.loot.LightMin = 10;
            profile.loot.LightMax = 14;
            profile.puff = new Color(0.45f, 0.12f, 0.12f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        private static System.Type[] Parts(FoeRecipe recipe)
        {
            return recipe.behaviours.Select(part => part.GetType()).ToArray();
        }

        [Test]
        public void TheBodyTheSwingAndTheDropsCarryTheProfilesNumbers()
        {
            var recipe = FoeKinds.FromWalker("brute", profile, "", false);
            Assert.That(recipe.name, Is.EqualTo("brute"));
            Assert.That(recipe.body.size, Is.EqualTo(profile.body));
            Assert.That(recipe.body.health, Is.EqualTo(8));
            Assert.That(recipe.body.speed, Is.EqualTo(14f));
            Assert.That(recipe.body.armour, Is.True);
            Assert.That(recipe.body.contactDamage, Is.EqualTo(1));
            Assert.That(recipe.body.color, Is.EqualTo(new Color(0.45f, 0.12f, 0.12f)));

            var swing = (Swing)recipe.behaviours[0];
            Assert.That(swing.range, Is.EqualTo(52f - 18f), "the old range was from its middle");
            Assert.That(swing.reach, Is.EqualTo(profile.reach));
            Assert.That(swing.windup, Is.EqualTo(0.75f));
            Assert.That(swing.strike, Is.EqualTo(0.25f));
            Assert.That(swing.recover, Is.EqualTo(0.9f));
            Assert.That(swing.cooldown, Is.EqualTo(1.5f));
            Assert.That(swing.damage, Is.EqualTo(2));

            Assert.That(recipe.drops.moteChance, Is.EqualTo(1f));
            Assert.That(recipe.drops.powerChance, Is.EqualTo(0.5f));
            Assert.That(recipe.drops.power, Is.EqualTo(PowerKind.Halo));
            Assert.That(recipe.drops.loot, Is.SameAs(profile.loot));
        }

        [Test]
        public void EachHabitOfTheMapIsItsPartsAfterTheSwing()
        {
            var plain = new[] { typeof(Swing), typeof(Patrol) };
            Assert.That(Parts(FoeKinds.FromWalker("a", profile, "", false)), Is.EqualTo(plain));
            Assert.That(Parts(FoeKinds.FromWalker("a", profile, "nonsense", false)), Is.EqualTo(plain));

            var chase = new[] { typeof(Swing), typeof(Chase), typeof(Patrol) };
            Assert.That(Parts(FoeKinds.FromWalker("a", profile, "chase", false)), Is.EqualTo(chase));

            var ambush = new[] { typeof(Ambush), typeof(Swing), typeof(Chase), typeof(Patrol) };
            Assert.That(Parts(FoeKinds.FromWalker("a", profile, "ambush", false)), Is.EqualTo(ambush));

            var pounce = new[] { typeof(Swing), typeof(Pounce), typeof(Patrol) };
            Assert.That(Parts(FoeKinds.FromWalker("a", profile, "Pounce", false)), Is.EqualTo(pounce));

            var lunge = new[] { typeof(Swing), typeof(Pounce), typeof(Chase), typeof(Patrol) };
            var lunger = FoeKinds.FromWalker("a", profile, "lunge", false);
            Assert.That(Parts(lunger), Is.EqualTo(lunge));
            Assert.That(((Pounce)lunger.behaviours[1]).spring, Is.EqualTo(Walker.LungeVelocity));
            Assert.That(((Chase)lunger.behaviours[2]).sight, Is.EqualTo(Walker.LungeSight));

            var hurl = new[] { typeof(Swing), typeof(Shoot), typeof(Retreat), typeof(Patrol) };
            var hurler = FoeKinds.FromWalker("a", profile, "hurl", false);
            Assert.That(Parts(hurler), Is.EqualTo(hurl));
            var thrown = (Shoot)hurler.behaviours[1];
            var back = (Retreat)hurler.behaviours[2];
            Assert.That(thrown.nearest, Is.EqualTo(back.within), "it throws from where it stops backing away");
            Assert.That(thrown.range, Is.EqualTo(Walker.HurlRange));

            var slam = new[] { typeof(Swing), typeof(Quake), typeof(Patrol) };
            var slammer = FoeKinds.FromWalker("a", profile, "slam", false);
            Assert.That(Parts(slammer), Is.EqualTo(slam));
            var quake = (Quake)slammer.behaviours[1];
            Assert.That(quake.nearest, Is.EqualTo(profile.attackRange), "beyond its swing");
            Assert.That(quake.farthest, Is.EqualTo(Walker.SlamMax));
            Assert.That(slammer.body.shape, Is.EqualTo(ShapeKind.Diamond));
        }

        [Test]
        public void ATallBodyStillPouncesAtAHeroOnItsOwnFloor()
        {
            var pounce = (Pounce)FoeKinds.FromWalker("a", profile, "pounce", false).behaviours[1];
            // His middle is 16 pixels above the floor and its own 31: he is 15 below it.
            Assert.That(pounce.below, Is.GreaterThanOrEqualTo(15f));
            Assert.That(pounce.above + pounce.below, Is.EqualTo(Walker.PounceAbove + Walker.PounceBelow));
            Assert.That(pounce.reach, Is.EqualTo(profile.reach));
            Assert.That(pounce.damage, Is.EqualTo(2));
        }

        [Test]
        public void APassiveOneOnlyPatrolsAndItsTouchDoesNotHurt()
        {
            var recipe = FoeKinds.FromWalker("resting", profile, "chase", true);
            Assert.That(Parts(recipe), Is.EqualTo(new[] { typeof(Patrol) }));
            Assert.That(recipe.body.contactDamage, Is.EqualTo(0));
        }
    }
}
