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

        [Test]
        public void AFlyerHauntsAtItsChaseSpeedWithinItsAggroAndOneThatDivesHangsOverHim()
        {
            var flyer = ScriptableObject.CreateInstance<FlyerProfile>();
            flyer.health = 2;
            flyer.hover = 25f;
            flyer.chase = 55f;
            flyer.aggro = 110f;
            flyer.dropChance = 0.5f;
            var size = new Vector2(16f, 16f);

            var plain = FoeKinds.FromFlyer("shade", flyer, size, false);
            Assert.That(plain.body.flies, Is.True);
            Assert.That(plain.body.size, Is.EqualTo(size));
            Assert.That(plain.body.health, Is.EqualTo(2));
            Assert.That(plain.body.speed, Is.EqualTo(25f), "it drifts at its hover speed");
            Assert.That(Parts(plain), Is.EqualTo(new[] { typeof(Haunt), typeof(Hover) }));
            var haunt = (Haunt)plain.behaviours[0];
            Assert.That(haunt.sight, Is.EqualTo(110f));
            Assert.That(haunt.pace, Is.EqualTo(55f));
            Assert.That(haunt.above, Is.EqualTo(0f), "it comes at him");
            Assert.That(plain.drops.moteChance, Is.EqualTo(0.5f));
            Assert.That(plain.drops.powerChance, Is.EqualTo(0f));
            Assert.That(plain.drops.loot, Is.SameAs(flyer.loot));

            var diver = FoeKinds.FromFlyer("watcher", flyer, size, true);
            Assert.That(Parts(diver), Is.EqualTo(new[] { typeof(Dive), typeof(Haunt), typeof(Hover) }));
            var over = (Haunt)diver.behaviours[1];
            Assert.That(over.above, Is.EqualTo(48f), "it hangs over him: 64 pixels above his feet");
            var dive = (Dive)diver.behaviours[0];
            Assert.That(dive.windup, Is.EqualTo(Flyer.AimTime));
            Assert.That(dive.speed, Is.EqualTo(Flyer.DiveSpeed));
            Assert.That(dive.depth, Is.GreaterThanOrEqualTo(over.above), "from over him it can come down");
            Object.DestroyImmediate(flyer);
        }

        [Test]
        public void TheHangedThingHangsDropsOnAHeroBeneathItAndIsNotSwungAway()
        {
            var recipe = FoeKinds.FromHanger("hanged", 3);
            Assert.That(recipe.body.flies, Is.True, "it hangs where it is put");
            Assert.That(recipe.body.size, Is.EqualTo(Hanger.BodySize));
            Assert.That(recipe.body.health, Is.EqualTo(3));
            Assert.That(recipe.body.pushShare, Is.EqualTo(0f), "a cut does not swing it away");
            Assert.That(Parts(recipe), Is.EqualTo(new[] { typeof(Dive), typeof(Hover) }));
            var drop = (Dive)recipe.behaviours[0];
            Assert.That(drop.range, Is.EqualTo(Hanger.DropRangeX));
            Assert.That(drop.depth, Is.EqualTo(Hanger.DropRangeY));
            Assert.That(drop.windup, Is.EqualTo(Hanger.CreakTime));
            Assert.That(drop.recover, Is.EqualTo(Hanger.RestTime), "it lies where it lands");
            Assert.That(drop.damage, Is.EqualTo(Hanger.DropDamage), "the drop takes two hearts");
            Assert.That(recipe.body.contactDamage, Is.EqualTo(Hanger.ContactDamage));
            Assert.That(recipe.drops.loot, Is.SameAs(Hanger.Loot));
        }
    }
}
