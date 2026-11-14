using System.Collections;
using Descender.EditorTools.TilesV2;
using Descender.Input;
using Descender.Player;
using Descender.Save;
using Descender.World.TilesV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The Tiles V2 world played (docs/art/tiles-v2-world.md): the scene loads on its own
    /// and builds the whole world; the hero starts on the Earth with no ability and the
    /// region says its name; a ledge carries him and lets him up through it from below; a veil of
    /// light stops him until he has the Light Dash, which he gets by walking into its
    /// shrine; and a fall into a liquid that burns costs him a heart and no more. The scene
    /// is loaded by its path, which only the editor can do, and unloaded again.
    /// </summary>
    public class TilesV2DescentTests
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
            var scene = SceneManager.GetSceneByPath(TilesV2Sample.DescentPath);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Pickups.Checkpoint.Reset();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheJourneyStartsWithNothingAndTheWorldAnswersToTheHero()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                TilesV2Sample.DescentPath,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
#endif
            yield return null;
            var playground = Object.FindAnyObjectByType<TilesV2Playground>();
            Assert.That(playground, Is.Not.Null, "the world scene did not load");
            var world = playground.World;
            var hero = playground.Hero;
            Assert.That(world.IsBuilt, Is.True);
            Assert.That(playground.IsJourney, Is.True);
            Assert.That(playground.Things, Is.Not.Null, "the map's marks stand in the world");
            Assert.That(playground.Things.Checkpoints, Is.GreaterThan(20));
            Assert.That(playground.Things.Abilities, Is.EqualTo(6));
            var input = new ScriptedInput();
            hero.InputSource = input;
            var probe = new MoveProbe(hero, input);
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, "he stands on the Earth");
            foreach (Ability ability in System.Enum.GetValues(typeof(Ability)))
            {
                Assert.That(hero.Has(ability), Is.False, "he starts with nothing: " + ability);
            }
            Assert.That(playground.Title, Is.EqualTo(world.Set.RegionOf("earth").name), "the region says its name");
            Assert.That(playground.Chart.Seen, Is.GreaterThan(100), "the chart fills in round him");

            // A ledge of the stair to Paradise: it carries him, and he jumps up through it.
            var ledge = FirstLedgeOverTheSurface(world);
            hero.PlaceAt(world.Ledges.CellToWorld(ledge) + new Vector3(0.5f, 1.5f, 0f));
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, "the ledge carries him");
            Assert.That(hero.transform.position.y, Is.EqualTo(ledge.y + 1f).Within(0.1f), "on its top");
            // Thrown up from under it (the ledge is a Wings' jump over the ground), he passes through.
            hero.PlaceAt(world.Ledges.CellToWorld(ledge) + new Vector3(0.5f, -2.2f, 0f));
            yield return null;
            hero.Launch(64f);
            yield return probe.Until(() => hero.transform.position.y > ledge.y + 1.05f, 90);
            Assert.That(hero.transform.position.y, Is.GreaterThan(ledge.y + 1.05f), "the ledge stopped him from below");
            yield return probe.Until(() => hero.IsGrounded, 120);
            Assert.That(
                hero.transform.position.y,
                Is.EqualTo(ledge.y + 1f).Within(0.1f),
                "he came up through the ledge and stands on it"
            );

            // The first veil of light: it stops him.
            var veil = Mark(world, TilesV2Things.Barrier, out var barrier);
            var before = veil + new Vector2(-2.5f, -(barrier.Number(0, 4) - 0.02f));
            hero.PlaceAt(before);
            yield return probe.Settle();
            input.MoveX = 1f;
            yield return probe.Until(() => hero.transform.position.x > veil.x + 1.5f, 120);
            input.Release();
            Assert.That(
                hero.transform.position.x,
                Is.LessThan(veil.x),
                "a veil of light let him through without the Light Dash"
            );

            // Its shrine: walking into the ability gives it, and says so.
            var shrine = Ability(world, Save.Ability.LightDash);
            hero.PlaceAt(shrine + new Vector2(-1.5f, 0f));
            yield return probe.Settle();
            input.MoveX = 1f;
            yield return probe.Until(() => hero.Has(Save.Ability.LightDash), 240);
            input.Release();
            Assert.That(hero.Has(Save.Ability.LightDash), Is.True, "the shrine gave nothing");
            Assert.That(playground.Title, Is.EqualTo("Light Dash"));
            Assert.That(hero.Has(Save.Ability.Wings), Is.False, "and only that");
            Assert.That(hero.IsAlive, Is.True);
        }

        // The lowest ledge with open sky over it: a step of the stair that stands on the Earth.
        private static Vector3Int FirstLedgeOverTheSurface(TilesV2World world)
        {
            var map = world.Map;
            for (var row = map.Surface; row >= 0; row--)
            {
                for (var column = 0; column < map.Width; column++)
                {
                    var cell = map.CellOf(column, row);
                    if (world.Ledges.HasTile(cell) && world.Light.Field != null && SkyOver(world, cell))
                    {
                        return cell;
                    }
                }
            }
            Assert.Fail("the world has no ledge under the sky");
            return Vector3Int.zero;
        }

        private static bool SkyOver(TilesV2World world, Vector3Int cell)
        {
            for (var up = 1; up <= 4; up++)
            {
                if (world.Terrain.HasTile(cell + new Vector3Int(0, up, 0)))
                {
                    return false;
                }
            }
            return world.Light.At(cell + Vector3Int.up).maxColorComponent > 0.5f;
        }

        // The top left corner in the world of the first mark of a kind.
        private static Vector2 Mark(TilesV2World world, string kind, out TilesV2Map.Mark found)
        {
            foreach (var mark in world.Map.Marks)
            {
                if (mark.Kind == kind)
                {
                    found = mark;
                    var cell = world.Map.CellOf(mark.Column, mark.Row);
                    return (Vector2)world.transform.position + new Vector2(cell.x, cell.y + 1f);
                }
            }
            Assert.Fail("the world's map marks no " + kind);
            found = default;
            return Vector2.zero;
        }

        // The middle of the foot of the cell an ability's shrine holds it in.
        private static Vector2 Ability(TilesV2World world, Ability ability)
        {
            foreach (var mark in world.Map.Marks)
            {
                if (mark.Kind == TilesV2Things.AbilityKind && mark.Word(0) == ability.ToString())
                {
                    var cell = world.Map.CellOf(mark.Column, mark.Row);
                    return (Vector2)world.transform.position + new Vector2(cell.x + 0.5f, cell.y);
                }
            }
            Assert.Fail("the world's map marks no " + ability);
            return Vector2.zero;
        }
    }
}
