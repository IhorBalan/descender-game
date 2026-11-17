using System.Collections;
using Descender.EditorTools.TilesV2;
using Descender.Player;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The Paradise level played for a moment: the hero wakes at the Hollow's dawn, and the
    /// day turns to a zone's own hour as he comes into it, and stays where a hand sets it
    /// until he goes on. The scene is not in the build's list, so it is loaded by its
    /// path, which only the editor can do; it is unloaded again after.
    /// </summary>
    public class TilesV2ParadisePlayTests
    {
        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            LiveTuning.Forget();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var scene = SceneManager.GetSceneByPath(TilesV2Sample.ParadisePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheDayTurnsToAZonesOwnHourAsTheHeroComesIn()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                TilesV2Sample.ParadisePath,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
#endif
            yield return null;
            var playground = Object.FindAnyObjectByType<TilesV2Playground>();
            Assert.That(playground, Is.Not.Null, "the Paradise scene did not load");
            var world = playground.World;
            var hero = playground.Hero;
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }
            Assert.That(playground.Things.Moods.Count, Is.EqualTo(69));
            Assert.That(
                playground.Mood,
                Is.EqualTo(world.Map.Hour).Within(0.01f),
                "he wakes in the Hollow, at its hour"
            );
            Assert.That(world.Day.Hour, Is.EqualTo(world.Map.Hour).Within(0.01f));
            Assert.That(world.Day.Running, Is.False, "the sun stands still");

            // Set down in the Tallied Fields, the day turns to their hour: not at once, and all the way.
            Assert.That(world.TryView("eden_fields", out var fields), Is.True);
            Assert.That(playground.Things.TryMood(fields, out var gold), Is.True);
            Assert.That(gold, Is.GreaterThan(world.Map.Hour + 5f), "the Fields are late in the day");
            hero.PlaceAt(fields);
            yield return null;
            yield return null;
            Assert.That(world.Day.Hour, Is.GreaterThan(world.Map.Hour), "the day did not start to turn");
            Assert.That(world.Day.Hour, Is.LessThan(gold), "the day turned at once");
            // The turn is timed by the clock on the wall, not by frames.
            var until = Time.realtimeSinceStartup + 8f * TilesV2Playground.MoodTime;
            while (Mathf.Abs(world.Day.Hour - gold) > 0.001f && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            Assert.That(world.Day.Hour, Is.EqualTo(gold).Within(0.001f), "the day did not come to the Fields' hour");
            Assert.That(playground.Zone, Is.EqualTo("The Tallied Fields"));

            // An hour set by hand stays while he stays in the zone.
            world.Day.Hour = 12f;
            for (var i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.That(world.Day.Hour, Is.EqualTo(12f).Within(0.001f), "the zone took the hour back");
        }
    }
}
