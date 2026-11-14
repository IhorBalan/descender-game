using System.Collections;
using Descender.EditorTools.TilesV2;
using Descender.Input;
using Descender.Player;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The Tiles V2 playground played: the scene loads on its own, the world is built from
    /// the sample map, the hero stands at the map's start, walks up the meadow's slopes
    /// onto the hill, comes down the far side into the pond and swims, with the tile
    /// light on him all the way, and the lava hurts him. The scene is not in the build's list, so it is loaded by
    /// its path, which only the editor can do; it is unloaded again so the tests after
    /// this one get an empty world.
    /// </summary>
    public class TilesV2PlaygroundTests
    {
        private float captureBefore;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            LiveTuning.Forget();
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = captureBefore;
            var scene = SceneManager.GetSceneByPath(TilesV2Sample.PlaygroundPath);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheHeroWalksTheMeadowsSlopesAndSwimsInThePond()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                TilesV2Sample.PlaygroundPath,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
#endif
            yield return null;
            var playground = Object.FindAnyObjectByType<TilesV2Playground>();
            Assert.That(playground, Is.Not.Null, "the playground scene did not load");
            var world = playground.World;
            var hero = playground.Hero;
            Assert.That(world.IsBuilt, Is.True);
            Assert.That(world.Light.Light.Length, Is.GreaterThan(0), "the world is lit");
            Assert.That(world.Day.Running, Is.True, "the day runs");

            var input = new ScriptedInput();
            hero.InputSource = input;
            var probe = new MoveProbe(hero, input);
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, "he stands on the meadow");
            Assert.That(hero.transform.position.x, Is.EqualTo(world.Spawn.x).Within(0.5f));
            Assert.That(hero.transform.position.y, Is.EqualTo(world.Spawn.y).Within(0.2f), "at the map's start");
            Assert.That(hero.Has(Save.Ability.Wings), Is.True, "with every ability");
            var start = hero.transform.position;

            // East: two slopes up onto the hill, a tile each.
            input.MoveX = 1f;
            yield return probe.Until(() => hero.transform.position.x > start.x + 9f, 600);
            Assert.That(hero.transform.position.x, Is.GreaterThan(start.x + 9f), "the slopes stopped him");
            yield return probe.Until(() => hero.IsGrounded, 60);
            Assert.That(hero.transform.position.y, Is.EqualTo(start.y + 2f).Within(0.3f), "two tiles up, on the hill");
            Assert.That(LightOn(world, hero).maxColorComponent, Is.GreaterThan(0.5f), "in daylight");

            // On east, down the far side and into the pond.
            yield return probe.Until(() => hero.IsInWater, 900);
            Assert.That(hero.IsInWater, Is.True, "he reached the pond and swims");
            Assert.That(hero.IsAlive, Is.True);
            input.Release();

            // Dropped over the lava at the bottom of the map, he loses a heart.
            var whole = hero.Health;
            hero.PlaceAt(OverLava(world));
            yield return probe.Until(() => hero.Health < whole, 300);
            Assert.That(hero.Health, Is.LessThan(whole), "lava does not hurt");
        }

        // A point two tiles over the middle of the first stretch of lava's surface.
        private static Vector2 OverLava(TilesV2World world)
        {
            var lava = world.Set.LiquidOf("lava").tile;
            foreach (var map in world.Liquids)
            {
                var bounds = map.cellBounds;
                var top = bounds.yMax - 1;
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    if (map.GetTile(new Vector3Int(x, top, 0)) == lava)
                    {
                        // Two cells in from the stretch's left end, clear of its bank.
                        return map.CellToWorld(new Vector3Int(x + 2, top, 0)) + new Vector3(0.5f, 3f, 0f);
                    }
                }
            }
            Assert.Fail("the sample map has no lava");
            return Vector2.zero;
        }

        private static Color LightOn(TilesV2World world, PlayerController hero)
        {
            var cell = world.Terrain.WorldToCell(hero.transform.position + Vector3.up);
            return world.Light.At(cell);
        }
    }
}
