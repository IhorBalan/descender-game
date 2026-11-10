using System.Collections.Generic;
using Descender.Economy;
using Descender.Save;
using Descender.Story;
using Descender.Triggers;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// Triggers as data: what a zone asks for, when it fires, and what each action that
    /// needs no hero does. The actions on the hero are in the PlayMode TriggerZoneTests.
    /// </summary>
    public class TriggerTests
    {
        private readonly List<Object> made = new List<Object>();
        private readonly List<string> log = new List<string>();

        /// <summary>An action that writes its name down, to see the order and the count.</summary>
        private sealed class Note : TriggerAction
        {
            public string name = "";
            public List<string> into;

            public override void Run(TriggerContext context) => into.Add(name);
        }

        [SetUp]
        public void SetUp()
        {
            StoryFlags.Reset();
            Inventory.Reset();
            Keys.Reset();
            Notice.Reset();
            log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.DestroyImmediate(thing);
                }
            }
            made.Clear();
            StoryFlags.Reset();
            Inventory.Reset();
            Keys.Reset();
            Notice.Reset();
        }

        private GameObject Thing(string name)
        {
            var thing = new GameObject(name);
            made.Add(thing);
            return thing;
        }

        private Note Write(string name)
        {
            var note = new Note();
            note.name = name;
            note.into = log;
            return note;
        }

        private TriggerZone Zone(
            TriggerEvent on,
            bool once,
            bool hide,
            TriggerNeeds needs,
            params TriggerAction[] actions
        )
        {
            var go = Thing("Zone");
            go.AddComponent<BoxCollider2D>();
            var zone = go.AddComponent<TriggerZone>();
            zone.Configure(on, once, hide, needs, actions);
            return zone;
        }

        private static bool Met(TriggerNeeds needs, bool ability, bool item, bool flag)
        {
            return needs.Met(wanted => ability, id => item, name => flag);
        }

        [Test]
        public void NeedsNothingIsAlwaysMet()
        {
            Assert.That(Met(TriggerNeeds.Nothing(), false, false, false), Is.True);
            Assert.That(TriggerNeeds.Nothing().Met(null), Is.True, "and with nobody there");
        }

        [Test]
        public void NeedsAskForAnAbilityAnItemAndAFlag()
        {
            var ability = TriggerNeeds.WithAbility(Ability.Wings);
            Assert.That(Met(ability, false, true, true), Is.False);
            Assert.That(Met(ability, true, false, false), Is.True);
            Assert.That(ability.Met(null), Is.False, "nobody has no abilities");

            var item = TriggerNeeds.Item("key");
            Assert.That(Met(item, true, false, true), Is.False);
            Assert.That(Met(item, false, true, false), Is.True);

            var flag = new TriggerNeeds();
            flag.flag = "gate_open";
            Assert.That(Met(flag, true, true, false), Is.False);
            Assert.That(Met(flag, false, false, true), Is.True);
        }

        [Test]
        public void NeedsAskForTheLackOfAnItemOrAFlag()
        {
            var noItem = TriggerNeeds.WithoutItem("key");
            Assert.That(Met(noItem, false, true, false), Is.False);
            Assert.That(Met(noItem, false, false, false), Is.True);

            var noFlag = new TriggerNeeds();
            noFlag.withoutFlag = "done";
            Assert.That(Met(noFlag, false, false, true), Is.False);
            Assert.That(Met(noFlag, false, false, false), Is.True);
        }

        [Test]
        public void NeedsReadTheKeysThePackAndTheFlags()
        {
            var needs = TriggerNeeds.Item("lab_key");
            needs.flag = "seen";
            Assert.That(needs.Met(null), Is.False);
            Keys.Add("lab_key");
            Assert.That(needs.Met(null), Is.False, "the flag is still missing");
            StoryFlags.Set("seen");
            Assert.That(needs.Met(null), Is.True);

            var sword = TriggerNeeds.Item("iron_sword");
            Assert.That(sword.Met(null), Is.False);
            Inventory.Add("iron_sword");
            Assert.That(sword.Met(null), Is.True, "an item of the pack counts as held");
        }

        [Test]
        public void KeysAreHeldTakenAndReset()
        {
            Assert.That(Keys.Has("a"), Is.False);
            Keys.Add("a");
            Keys.Add("a");
            Keys.Add("");
            Assert.That(Keys.Count, Is.EqualTo(1));
            Assert.That(Keys.Holding("a"), Is.True);
            Assert.That(Keys.Remove("a"), Is.True);
            Assert.That(Keys.Remove("a"), Is.False);
            Keys.Add("b");
            Keys.Reset();
            Assert.That(Keys.Count, Is.Zero);
        }

        [Test]
        public void AZoneRunsItsActionsInOrderAndTellsWhoListens()
        {
            var zone = Zone(TriggerEvent.Enter, false, false, null, Write("one"), Write("two"));
            var heard = 0;
            zone.Fired += fired => heard++;

            Assert.That(zone.TryFire(null), Is.True);
            Assert.That(zone.TryFire(null), Is.True);

            Assert.That(log, Is.EqualTo(new[] { "one", "two", "one", "two" }));
            Assert.That(zone.TimesFired, Is.EqualTo(2));
            Assert.That(heard, Is.EqualTo(2));
        }

        [Test]
        public void AOnceZoneFiresOnce()
        {
            var zone = Zone(TriggerEvent.Enter, true, false, null, Write("one"));
            Assert.That(zone.TryFire(null), Is.True);
            Assert.That(zone.TryFire(null), Is.False);
            Assert.That(log.Count, Is.EqualTo(1));
        }

        [Test]
        public void AZoneWaitsForItsNeedsAndIsNotUsedUpByATry()
        {
            var zone = Zone(TriggerEvent.Enter, true, false, TriggerNeeds.Item("key"), Write("opened"));
            Assert.That(zone.TryFire(null), Is.False);
            Assert.That(log, Is.Empty);
            Keys.Add("key");
            Assert.That(zone.TryFire(null), Is.True);
            Assert.That(log.Count, Is.EqualTo(1));
        }

        [Test]
        public void APickupZoneIsGoneOnceFired()
        {
            var zone = Zone(TriggerEvent.Enter, true, true, null, Write("taken"));
            zone.TryFire(null);
            Assert.That(zone.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AHitZoneAnswersAStrikeAndTheOthersDoNot()
        {
            var struck = Zone(TriggerEvent.Hit, false, false, null, Write("struck"));
            var walked = Zone(TriggerEvent.Enter, false, false, null, Write("walked"));

            Assert.That(struck.Hit(1, Vector2.zero), Is.True);
            Assert.That(walked.Hit(1, Vector2.zero), Is.False);
            Assert.That(walked.Interact(), Is.False, "nobody stands in it, and it is not that kind");

            Assert.That(log, Is.EqualTo(new[] { "struck" }));
            Assert.That(struck.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Enemy")), "where attacks look");
            Assert.That(walked.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Trigger")));
            Assert.That(walked.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(struck.GetComponent<BoxCollider2D>().isTrigger, Is.False, "attacks look for plain colliders");
        }

        [Test]
        public void GiveAndTakeAKeyOrAnItemOfThePack()
        {
            var nobody = new TriggerContext(null, null);
            Do.Give("lab_key").Run(nobody);
            Assert.That(Keys.Has("lab_key"), Is.True, "a name that is no item is a key");
            Assert.That(Inventory.Has("lab_key"), Is.False);
            Do.Take("lab_key").Run(nobody);
            Assert.That(Keys.Has("lab_key"), Is.False);

            Do.Give("iron_sword").Run(nobody);
            Assert.That(Inventory.Count("iron_sword"), Is.EqualTo(1));
            Assert.That(Keys.Has("iron_sword"), Is.False);
            Do.Take("iron_sword").Run(nobody);
            Assert.That(Inventory.Has("iron_sword"), Is.False);
        }

        [Test]
        public void GiveCurrencyFillsThePurse()
        {
            var action = new GiveCurrency();
            action.currency = "light";
            action.amount = 7;
            action.Run(new TriggerContext(null, null));
            Assert.That(Inventory.Amount("light"), Is.EqualTo(7));
        }

        [Test]
        public void SetFlagSetsAStoryFlag()
        {
            Do.Flag("lever_pulled").Run(new TriggerContext(null, null));
            Assert.That(StoryFlags.Has("lever_pulled"), Is.True);
        }

        [Test]
        public void SetActiveShowsHidesAndFlips()
        {
            var target = Thing("Bridge");
            var nobody = new TriggerContext(null, null);
            Do.Active(target, SwitchTo.Off).Run(nobody);
            Assert.That(target.activeSelf, Is.False);
            Do.Active(target, SwitchTo.Toggle).Run(nobody);
            Assert.That(target.activeSelf, Is.True);
            Do.Active(target, SwitchTo.Toggle).Run(nobody);
            Assert.That(target.activeSelf, Is.False);
            Do.Active(target, SwitchTo.On).Run(nobody);
            Assert.That(target.activeSelf, Is.True);
            Do.Active(null, SwitchTo.On).Run(nobody);
        }

        [Test]
        public void SetDoorOpensAndClosesIt()
        {
            var go = Thing("Door");
            var box = go.AddComponent<BoxCollider2D>();
            var sprite = go.AddComponent<SpriteRenderer>();
            var door = go.AddComponent<Door>();
            var nobody = new TriggerContext(null, null);

            Do.Door(door, SwitchTo.On).Run(nobody);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(box.enabled, Is.False, "an open door is not solid");
            Assert.That(sprite.color.a, Is.EqualTo(Door.OpenAlpha).Within(0.001f));

            Do.Door(door, SwitchTo.Off).Run(nobody);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(box.enabled, Is.True);
            Assert.That(sprite.color.a, Is.EqualTo(1f).Within(0.001f));

            Do.Door(door, SwitchTo.Toggle).Run(nobody);
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void SetMoverStartsAndStopsIt()
        {
            var mover = Thing("Lift").AddComponent<Mover>();
            var nobody = new TriggerContext(null, null);
            Assert.That(mover.Running, Is.False);
            Do.Mover(mover, SwitchTo.On).Run(nobody);
            Assert.That(mover.Running, Is.True);
            Do.Mover(mover, SwitchTo.Toggle).Run(nobody);
            Assert.That(mover.Running, Is.False);
            Do.Mover(mover, SwitchTo.Toggle).Run(nobody);
            Do.Mover(mover, SwitchTo.Off).Run(nobody);
            Assert.That(mover.Running, Is.False);
        }

        [Test]
        public void ShowMessageIsKeptAndTold()
        {
            var heard = "";
            System.Action<string> listen = text => heard = text;
            Notice.Shown += listen;
            try
            {
                Do.Say("The key turns.").Run(new TriggerContext(null, null));
            }
            finally
            {
                Notice.Shown -= listen;
            }
            Assert.That(Notice.Last, Is.EqualTo("The key turns."));
            Assert.That(heard, Is.EqualTo("The key turns."));
        }

        [Test]
        public void SpawnPutsACopyAtTheZone()
        {
            var prefab = Thing("Prefab");
            var zone = Zone(TriggerEvent.Enter, true, false, null);
            zone.transform.position = new Vector3(4f, 2f, 0f);
            var spawn = new Spawn();
            spawn.prefab = prefab;
            spawn.offset = new Vector2(1f, 0f);

            spawn.Run(new TriggerContext(zone, null));

            Assert.That(spawn.Last, Is.Not.Null);
            made.Add(spawn.Last);
            Assert.That(spawn.Last, Is.Not.SameAs(prefab));
            Assert.That(spawn.Last.transform.position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(spawn.Last.transform.position.y, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void TheHeroActionsDoNothingWithNobodyThere()
        {
            var nobody = new TriggerContext(null, null);
            Do.Grant(Ability.Wings).Run(nobody);
            new Heal().Run(nobody);
            new Hurt().Run(nobody);
            new GrantPower().Run(nobody);
            new GiveUpgrade().Run(nobody);
            new SetRespawn().Run(nobody);
            new Spawn().Run(nobody);
            Assert.That(nobody.Position, Is.EqualTo(Vector2.zero));
        }
    }
}
