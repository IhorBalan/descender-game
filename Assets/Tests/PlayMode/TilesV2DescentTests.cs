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
    /// shrine; a wall that is not one comes down under two cuts and its room comes onto the
    /// chart; the Pit takes him between the antechambers he has rested in; and a platform
    /// that gives way does, and comes back; a sign says its line to him when he stands by
    /// it; a heart found is one more heart; and the chart, open, holds him still and comes
    /// closer; and a light of Paradise burns when he walks up to it, the door of the Temple
    /// standing until the last one does. The scene is loaded by its path, which only the
    /// editor can do, and unloaded again.
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

            // A wall that is not one: two cuts bring it down, and the room behind it comes onto the chart.
            var things = playground.Things;
            TilesV2Things.Thing secret = null;
            foreach (var thing in things.All)
            {
                secret ??= thing.Kind == TilesV2Things.Secret ? thing : null;
            }
            Assert.That(secret, Is.Not.Null, "the world has no secret");
            var wall = secret.Made.GetComponent<Triggers.Breakable>();
            var room = new Vector2Int(secret.Hidden.xMin + 1, secret.Hidden.yMin + 1);
            Assert.That(playground.Chart.IsHidden(room), Is.True, "the chart knows the room before the hero does");
            Assert.That(wall.GetComponent<BoxCollider2D>().enabled, Is.True, "the wall stands");
            var cut = new Combat.Strike(Combat.StrikeKind.Sword, 1, wall.transform.position);
            wall.Struck(cut);
            Assert.That(wall.IsBroken, Is.False, "one cut is not enough");
            wall.Struck(cut);
            Assert.That(wall.IsBroken, Is.True);
            Assert.That(things.SecretsFound, Is.EqualTo(1));
            Assert.That(playground.Chart.IsHidden(room), Is.False, "broken, the room is on the chart");

            // Rested by the Pit in two circles, the one takes him to the other.
            var pits = new System.Collections.Generic.List<TilesV2Things.Thing>();
            foreach (var thing in things.All)
            {
                if (thing.Kind == TilesV2Things.Checkpoint && thing.InPit && pits.Count < 2)
                {
                    pits.Add(thing);
                }
            }
            Assert.That(pits.Count, Is.EqualTo(2));
            foreach (var rest in pits)
            {
                hero.PlaceAt((Vector2)rest.Made.transform.position + new Vector2(0f, 0.05f));
                yield return probe.Settle();
                yield return probe.Step(5);
                Assert.That(playground.Rested, Does.Contain(rest.Number), "standing on it is resting at it");
            }
            playground.TravelTo(pits[0]);
            yield return probe.Settle();
            Assert.That(
                Vector2.Distance(hero.transform.position, pits[0].Made.transform.position),
                Is.LessThan(3f),
                "the Pit took him back"
            );
            Assert.That(hero.IsGrounded, Is.True);

            // A platform that gives way: it carries him, then it is gone, then it is back.
            TilesV2Things.Thing slab = null;
            foreach (var thing in things.All)
            {
                slab = thing.Kind == TilesV2Things.Platform && thing.Word == "crumble" ? thing : slab;
            }
            Assert.That(slab, Is.Not.Null, "the world has no platform that gives way");
            var crumble = slab.Made.GetComponent<Triggers.Crumble>();
            hero.PlaceAt((Vector2)slab.Made.transform.position + new Vector2(1f, 1.05f));
            yield return probe.Until(() => hero.IsGrounded, 60);
            Assert.That(hero.IsGrounded, Is.True, "it carries him");
            Assert.That(hero.transform.position.y, Is.EqualTo(slab.Made.transform.position.y + 1f).Within(0.1f));
            yield return probe.Until(() => !crumble.IsSolid, 180);
            Assert.That(crumble.IsSolid, Is.False, "it gave way under him");
            hero.PlaceAt(world.Spawn);
            yield return probe.Until(() => crumble.IsSolid, 400);
            Assert.That(crumble.IsSolid, Is.True, "and came back");
            Assert.That(things.Winds, Is.GreaterThan(2), "and the winds blow");

            // A sign: standing by it he reads it, and walking off he does not.
            TilesV2Things.Thing post = null;
            TilesV2Things.Thing heart = null;
            foreach (var thing in things.All)
            {
                post ??= thing.Kind == TilesV2Things.Sign ? thing : null;
                heart ??=
                    thing.Kind == TilesV2Things.Upgrade && thing.Word == UpgradeKind.Heart.ToString() ? thing : null;
            }
            Assert.That(post, Is.Not.Null, "the world has no sign");
            var sign = post.Made.GetComponent<TilesV2Sign>();
            hero.PlaceAt((Vector2)post.Made.transform.position + new Vector2(1f, 0.05f));
            yield return probe.Settle();
            yield return probe.Step(3);
            Assert.That(TilesV2Sign.Reading, Is.SameAs(sign), "he stands by the sign and does not read it");
            Assert.That(sign.Text, Is.Not.Empty);

            // A heart behind a wall that is not one: walking into it is one more heart.
            Assert.That(heart, Is.Not.Null, "the world has no heart to find");
            var hearts = hero.MaxHealth;
            hero.PlaceAt((Vector2)heart.Made.transform.position + new Vector2(0f, 0.05f));
            yield return probe.Until(() => hero.MaxHealth > hearts, 120);
            Assert.That(hero.MaxHealth, Is.EqualTo(hearts + 1), "the heart gave nothing");
            yield return null;
            Assert.That(TilesV2Sign.Reading, Is.Null, "the sign is behind him");
            Assert.That(heart.IsGone, Is.True, "and it is taken once");

            // The chart, open: he stands still, and it comes closer no further than it goes.
            Assert.That(playground.IsChartShown, Is.False);
            playground.ShowChart(true);
            Assert.That(playground.IsChartShown, Is.True);
            Assert.That(hero.Frozen, Is.True, "he walks while he reads the chart");
            Assert.That(playground.ChartZoom, Is.EqualTo(1), "it opens on the whole of it");
            for (var i = 0; i < 5; i++)
            {
                playground.ZoomChart(true);
            }
            Assert.That(playground.ChartZoom, Is.EqualTo(TilesV2Playground.ClosestChart));
            playground.ZoomChart(false);
            Assert.That(playground.ChartZoom, Is.EqualTo(TilesV2Playground.ClosestChart / 2));
            yield return null;
            playground.ShowChart(false);
            Assert.That(hero.Frozen, Is.False, "closed, he walks again");

            // A light of Paradise: walking up to it lights it, and the door waits for the others.
            TilesV2Things.Thing light = null;
            TilesV2Things.Thing door = null;
            foreach (var thing in things.All)
            {
                light ??= thing.Kind == TilesV2Things.Beacon ? thing : null;
                door ??= thing.Kind == TilesV2Things.Sanctum ? thing : null;
            }
            Assert.That(light, Is.Not.Null, "Paradise has no light");
            Assert.That(door, Is.Not.Null, "the Temple has no door");
            Assert.That(things.BeaconsLit, Is.EqualTo(0));
            hero.PlaceAt((Vector2)light.Made.transform.position + new Vector2(-1.5f, 0.05f));
            yield return probe.Settle();
            input.MoveX = 1f;
            yield return probe.Until(() => things.BeaconsLit > 0, 120);
            input.Release();
            Assert.That(things.BeaconsLit, Is.EqualTo(1), "he stood by the light and it stayed dark");
            Assert.That(light.Made.GetComponent<TilesV2Beacon>().IsLit, Is.True);
            Assert.That(playground.Title, Is.EqualTo("A light wakes"));
            Assert.That(door.Made.activeSelf, Is.True, "one light is not three");
            foreach (var beacon in things.GetComponentsInChildren<TilesV2Beacon>())
            {
                beacon.Light();
            }
            Assert.That(things.BeaconsLit, Is.EqualTo(things.Beacons));
            Assert.That(door.Made.activeSelf, Is.False, "every light burns and the door is still shut");
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
