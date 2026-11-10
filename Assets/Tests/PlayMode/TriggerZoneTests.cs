using System.Collections;
using System.Collections.Generic;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using Descender.Save;
using Descender.Triggers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Triggers with a hero: the lab's items station played through a ScriptedInput (the
    /// key and the door, the switch to strike, the lift), zones that fire on leaving and
    /// on interact, and the actions that act on the hero.
    /// </summary>
    public class TriggerZoneTests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private LabRoot lab;
        private PlayerController hero;
        private ScriptedInput input;
        private Station items;
        private int itemsIndex;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            Keys.Reset();
            Notice.Reset();
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            lab = LabBuilder.Build();
            made.Add(lab.gameObject);
            var go = new GameObject("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            made.Add(go);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            input = new ScriptedInput();
            hero.InputSource = input;
            itemsIndex = lab.IndexOf("items");
            items = lab.Stations[itemsIndex];
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = captureBefore;
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.Destroy(thing);
                }
            }
            made.Clear();
            Keys.Reset();
            Notice.Reset();
            Story.StoryFlags.Reset();
        }

        [UnityTest]
        public IEnumerator TheHeroTakesTheKeyOpensTheDoorAndWalksThrough()
        {
            yield return Steps(2);
            lab.Enter(hero, itemsIndex);
            yield return Steps(5);
            var door = Built("door").GetComponent<Door>();
            var doorX = door.transform.position.x;

            // Without the key: he walks into the door and stays on this side of it.
            hero.PlaceAt(new Vector2(items.room.x + 26.5f, LabLayout.FloorTop));
            input.MoveX = 1f;
            yield return Steps(100);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(hero.transform.position.x, Is.LessThan(doorX), "the closed door stops him");
            Assert.That(Notice.Last, Is.EqualTo(LabLayout.LockedText));

            // From before the key: he walks over it, to the door and through.
            input.MoveX = 0f;
            hero.PlaceAt(new Vector2(items.room.x + 22.5f, LabLayout.FloorTop));
            yield return Steps(5);
            Assert.That(Keys.Has(LabLayout.Key), Is.False);
            input.MoveX = 1f;
            var heldKey = false;
            for (var i = 0; i < 150; i++)
            {
                yield return null;
                heldKey |= Keys.Has(LabLayout.Key);
            }
            input.MoveX = 0f;

            Assert.That(heldKey, Is.True, "he took the key on the way");
            Assert.That(Built("key").activeSelf, Is.False, "the key is gone from the floor");
            Assert.That(door.IsOpen, Is.True);
            Assert.That(Keys.Has(LabLayout.Key), Is.False, "the lock kept the key");
            Assert.That(Notice.Last, Is.EqualTo(LabLayout.UnlockedText));
            Assert.That(hero.transform.position.x, Is.GreaterThan(doorX + 1f), "he is through the door");
        }

        [UnityTest]
        public IEnumerator EnteringTheStationAgainPutsItBack()
        {
            yield return Steps(2);
            lab.Enter(hero, itemsIndex);
            yield return Steps(5);
            hero.PlaceAt(Built("key").transform.position + Vector3.down * 0.5f);
            yield return Steps(5);
            Assert.That(Keys.Has(LabLayout.Key), Is.True);
            Assert.That(Built("key").activeSelf, Is.False);

            lab.Enter(hero, itemsIndex);
            yield return Steps(20);

            Assert.That(Keys.Has(LabLayout.Key), Is.False, "the key is back on the floor, not in his hand");
            Assert.That(Built("key").activeSelf, Is.True);
            Assert.That(hero.Has(Ability.Wings), Is.False);
            Assert.That(hero.IsGrounded, Is.True, "he stands on the rebuilt floor");
            Assert.That(hero.transform.position.y, Is.EqualTo(items.spawn.y).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator ThePickupGrantsWings()
        {
            yield return Steps(2);
            lab.Enter(hero, itemsIndex);
            yield return Steps(5);
            Assert.That(hero.Has(Ability.Wings), Is.False);
            input.MoveX = 1f;
            yield return Steps(80);
            input.MoveX = 0f;
            Assert.That(hero.Has(Ability.Wings), Is.True, "walking from the start crosses the pickup");
        }

        [UnityTest]
        public IEnumerator StrikingTheSwitchBringsTheBridgeAndTakesItAway()
        {
            yield return Steps(2);
            lab.Enter(hero, itemsIndex);
            yield return Steps(5);
            var bridge = Built("bridge");
            var strike = Built("strike");
            Assert.That(bridge.activeSelf, Is.False);
            hero.PlaceAt(new Vector2(strike.transform.position.x - 1.2f, LabLayout.FloorTop));
            yield return Steps(5);

            yield return Attack();
            Assert.That(bridge.activeSelf, Is.True, "one strike brings the bridge");
            yield return Attack();
            Assert.That(bridge.activeSelf, Is.False, "the next takes it away");
        }

        [UnityTest]
        public IEnumerator TheLiftRunsWhileItIsSwitchedOn()
        {
            yield return Steps(2);
            lab.Enter(hero, itemsIndex);
            yield return Steps(5);
            var press = Built("press").GetComponent<TriggerZone>();
            var lift = Built("lift").GetComponent<Mover>();
            var startY = lift.transform.position.y;
            Assert.That(press.Interact(), Is.False, "nobody stands at the switch");

            hero.PlaceAt(new Vector2(press.transform.position.x, LabLayout.FloorTop));
            yield return Steps(5);
            Assert.That(press.Near, Is.SameAs(hero));
            Assert.That(press.Interact(), Is.True);
            Assert.That(lift.Running, Is.True);
            yield return Steps(50);
            var risen = lift.transform.position.y - startY;
            Assert.That(risen, Is.EqualTo(LabBuilder.LiftSpeed * 50f * Time.fixedDeltaTime).Within(0.3f));

            Assert.That(press.Interact(), Is.True);
            Assert.That(lift.Running, Is.False);
            var stoppedAt = lift.transform.position.y;
            yield return Steps(20);
            Assert.That(lift.transform.position.y, Is.EqualTo(stoppedAt).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator AZoneFiresOnLeaving()
        {
            yield return Steps(2);
            lab.Enter(hero, lab.IndexOf("run"));
            yield return Steps(5);
            var go = new GameObject("Leave zone");
            made.Add(go);
            go.transform.position = hero.transform.position + Vector3.up;
            go.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
            var zone = go.AddComponent<TriggerZone>();
            zone.Configure(TriggerEvent.Leave, true, false, null, new TriggerAction[] { Do.Flag("left") });
            yield return Steps(5);
            Assert.That(zone.Near, Is.SameAs(hero));
            Assert.That(Story.StoryFlags.Has("left"), Is.False);

            input.MoveX = 1f;
            yield return Steps(40);
            input.MoveX = 0f;
            Assert.That(zone.Near, Is.Null);
            Assert.That(Story.StoryFlags.Has("left"), Is.True);
        }

        [UnityTest]
        public IEnumerator TheActionsOnTheHero()
        {
            yield return Steps(2);
            lab.Enter(hero, lab.IndexOf("run"));
            yield return Steps(5);
            var go = new GameObject("Zone");
            made.Add(go);
            go.transform.position = new Vector3(hero.transform.position.x + 20f, 6f, 0f);
            var zone = go.AddComponent<TriggerZone>();
            var context = new TriggerContext(zone, hero);

            Do.Grant(Ability.LightDash).Run(context);
            Assert.That(hero.Has(Ability.LightDash), Is.True);

            var full = hero.Health;
            var hurt = new Hurt();
            hurt.amount = 1;
            hurt.Run(context);
            Assert.That(hero.Health, Is.LessThan(full));
            var wounded = hero.Health;
            var heal = new Heal();
            heal.amount = 1;
            heal.Run(context);
            Assert.That(hero.Health, Is.EqualTo(wounded + 1));

            var power = new GrantPower();
            power.power = Power.Blade;
            power.Run(context);
            Assert.That(hero.HasPower(Power.Blade), Is.True);

            var hearts = hero.MaxHealth;
            var upgrade = new GiveUpgrade();
            upgrade.kind = UpgradeKind.Heart;
            upgrade.flag = "upgrade:test";
            upgrade.Run(context);
            Assert.That(hero.MaxHealth, Is.GreaterThan(hearts));
            Assert.That(Story.StoryFlags.Has("upgrade:test"), Is.True);

            new SetRespawn().Run(context);
            Assert.That(hero.RespawnPosition.x, Is.EqualTo(go.transform.position.x).Within(0.001f));
            Assert.That(hero.RespawnPosition.y, Is.EqualTo(go.transform.position.y).Within(0.001f));
        }

        /// <summary>The object a piece of the items station was built as.</summary>
        private GameObject Built(string id)
        {
            var index = items.pieces.IndexOf(LabLayout.Find(items, id));
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"the items station has no piece '{id}'");
            return lab.ParentOf(items).GetChild(index).gameObject;
        }

        private IEnumerator Attack()
        {
            input.AttackPressed = true;
            yield return null;
            input.ClearPresses();
            yield return Steps(40);
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
