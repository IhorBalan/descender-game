using System.Collections;
using System.Collections.Generic;
using Descender.Lab;
using Descender.Player;
using Descender.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The mechanics lab built for real: the hero stands at every station's spawn, a pad
    /// takes him to the next station with that station's abilities, and a pod throws him.
    /// </summary>
    public class LabTests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private Vector2 gravityBefore;
        private float captureBefore;
        private LabRoot lab;
        private PlayerController hero;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            gravityBefore = Physics2D.gravity;
            Physics2D.gravity = Vector2.zero;
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
            hero.InputSource = new Descender.Input.ScriptedInput();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = captureBefore;
            Physics2D.gravity = gravityBefore;
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
        public IEnumerator TheHeroStandsAtEveryStation()
        {
            yield return Steps(2);
            for (var i = 0; i < lab.Stations.Count; i++)
            {
                var station = lab.Stations[i];
                lab.Enter(hero, i);
                yield return Steps(20);
                Assert.That(lab.Index, Is.EqualTo(i));
                Assert.That(hero.IsGrounded, Is.True, $"{station.id}: the hero is not standing");
                Vector2 at = hero.transform.position;
                Assert.That(at.x, Is.EqualTo(station.spawn.x).Within(0.1f), $"{station.id}: he slid");
                Assert.That(at.y, Is.EqualTo(station.spawn.y).Within(0.1f), $"{station.id}: he fell or was pushed");
                Assert.That(hero.Abilities, Is.EquivalentTo(station.abilities), $"{station.id}: the abilities");
            }
        }

        [UnityTest]
        public IEnumerator APadTakesHimToTheNextStationWithItsAbilities()
        {
            yield return Steps(2);
            var wings = lab.IndexOf("wings");
            lab.Enter(hero, wings - 1);
            yield return Steps(5);
            Assert.That(hero.Has(Ability.Wings), Is.False);

            var pad = FindPad(wings);
            Assert.That(pad, Is.Not.Null, "the station before Wings has no pad to it");
            hero.transform.position = pad.transform.position + Vector3.down;
            yield return Steps(5);

            Assert.That(lab.Index, Is.EqualTo(wings));
            Assert.That(hero.Has(Ability.Wings), Is.True);
            Vector2 at = hero.transform.position;
            Assert.That(Vector2.Distance(at, lab.Stations[wings].spawn), Is.LessThan(0.5f));
        }

        [UnityTest]
        public IEnumerator APodThrowsHimAsHighAsItSays()
        {
            yield return Steps(2);
            var index = lab.IndexOf("pods");
            lab.Enter(hero, index);
            yield return Steps(5);
            var pod = lab.GetComponentInChildren<World.Paradise.BouncePod>();
            Assert.That(pod, Is.Not.Null);
            var floor = lab.Stations[index].spawn.y;
            // Dropped onto the pod from just above it.
            hero.transform.position = new Vector3(pod.transform.position.x, pod.transform.position.y + 2f, 0f);
            var apex = float.NegativeInfinity;
            for (var i = 0; i < 150; i++)
            {
                yield return null;
                apex = Mathf.Max(apex, hero.transform.position.y);
            }
            var thrown = apex - (pod.transform.position.y + 0.5f);
            Assert.That(thrown, Is.GreaterThan(pod.HeightTiles - 1.5f), "the pod threw him too low");
            Assert.That(thrown, Is.LessThan(pod.HeightTiles + 1.5f), "the pod threw him too high");
            Assert.That(apex, Is.GreaterThan(floor + 3f));
        }

        private LabPad FindPad(int target)
        {
            foreach (var pad in lab.GetComponentsInChildren<LabPad>())
            {
                if (pad.Target == target && pad.transform.parent.name == "Station " + lab.Stations[target - 1].id)
                {
                    return pad;
                }
            }
            return null;
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
