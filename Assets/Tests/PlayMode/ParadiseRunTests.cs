using System.Collections;
using System.Collections.Generic;
using Descender.EditorTools.Paradise;
using Descender.Input;
using Descender.Player;
using Descender.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The scripted playtest of M1: the hero runs the whole Paradise main path, zone 1 to
    /// the Low Gate's outer door, in the built zone scenes, driven by MainPathPilot
    /// through a ScriptedInput. The run fails on the first move the hero cannot make,
    /// naming the surfaces and where he stood, and passes when he reaches the gate
    /// with the feather and the light dash picked up on the way and the opening,
    /// Making-house and gate beats played. It logs the run's time against the design's
    /// 20-minute target. Game time is stepped one physics step per frame
    /// (Time.captureDeltaTime), so the run takes as long as the editor needs, not 20
    /// real minutes.
    /// </summary>
    public class ParadiseRunTests
    {
        /// <summary>The design's length for the straight run (docs/delivery/STATE.md, M1).</summary>
        public const float TargetMinutes = 20f;

        /// <summary>A run this much over the target means the path has grown; the test says so.</summary>
        public const float OverTargetAllowed = 1.25f;

        private const float GateX = 7470f;

        /// <summary>Real seconds the run may take before it is called off with its trace (the editor stepping slowly).</summary>
        private const float RealTimeLimit = 1200f;

        private readonly List<string> loaded = new List<string>();
        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            Save.SaveManager.UseDirectory(System.IO.Path.Combine(Application.temporaryCachePath, "paradise_run"));
            captureBefore = Time.captureDeltaTime;
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
            Story.StoryFlags.Reset();
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            if (loaded.Count == 0)
            {
                yield break;
            }
            // The last loaded scene cannot be unloaded, so the tests after this one get an empty one.
            SceneManager.SetActiveScene(SceneManager.CreateScene("After the run"));
            foreach (var name in loaded)
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            loaded.Clear();
        }

        [UnityTest]
        [Timeout(2400000)]
        public IEnumerator TheHeroRunsTheMainPathToTheLowGate()
        {
            var zones = ParadiseLayout.All();
            Assert.That(ParadiseChecker.Check(zones), Is.Empty, "the layout breaks its own rules; fix that first");
            // A scene of its own: an earlier test may have left Main.unity (its streamer, router and
            // flow) loaded, and the run wants the zones alone.
            yield return AloneInAFreshScene();

            var box = Thing("Dialogue Box").AddComponent<DialogueBox>();
            var runner = Thing("Dialogue Runner").AddComponent<DialogueRunner>();
            runner.Bind(box, null, null);
            var dialoguePath = "Assets/Data/dialogue/world_1.json";
            var dialogueJson = System.IO.File.ReadAllText(dialoguePath);
            runner.Load(dialogueJson, dialoguePath);
            var played = new List<string>();
            runner.DialogueStarted += entry => played.Add(entry.Value<string>("id") ?? "?");

            foreach (var zone in zones)
            {
                yield return SceneManager.LoadSceneAsync(zone.scene, LoadSceneMode.Additive);
                loaded.Add(zone.scene);
            }
            // The placements spawn on their first frame.
            yield return null;
            yield return null;

            var hero = MakeHero(ParadiseLayout.Start);
            var input = new ScriptedInput();
            hero.InputSource = input;
            hero.DeathY = -110f;
            var legs = MainPathPilot.Plan(zones, ParadiseLayout.Start, GateX);
            var pilot = new MainPathPilot(hero, input, legs);

            Time.captureDeltaTime = Time.fixedDeltaTime;
            var dt = Time.fixedDeltaTime;
            var elapsed = 0f;
            var talking = 0f;
            var zoneReached = 0;
            var lastLog = 0f;
            // Let him land before the first step.
            for (var i = 0; i < 10; i++)
            {
                yield return null;
            }
            var trace = new Queue<string>();
            var lastLeg = -1;
            var startedAt = Time.realtimeSinceStartup;
            var talkingNow = 0f;
            var calledOff = (string)null;
            while (!pilot.Done && pilot.Failure == null)
            {
                if (Time.realtimeSinceStartup - startedAt > RealTimeLimit)
                {
                    calledOff = $"called off after {RealTimeLimit:0} real seconds at {elapsed / 60f:0.0} game minutes";
                    break;
                }
                talkingNow = hero.Talking ? talkingNow + dt : 0f;
                if (talkingNow > 30f)
                {
                    var state = box.IsOpen ? "open" : "closed";
                    calledOff = $"the hero has been talking for {talkingNow:0} s with the box {state}";
                    break;
                }
                if (pilot.LegIndex != lastLeg)
                {
                    lastLeg = pilot.LegIndex;
                    trace.Enqueue($"[{elapsed:0.00}] leg {lastLeg + 1}: {pilot.Current}");
                }
                trace.Enqueue(
                    $"[{elapsed:0.00}] at ({hero.transform.position.x:0.00}, {hero.transform.position.y:0.00})"
                        + $" v ({hero.Velocity.x:0.0}, {hero.Velocity.y:0.0}) {hero.State}"
                        + $"{(hero.IsGrounded ? " grounded" : "")}{(hero.Talking ? " talking" : "")}"
                        + $" move {input.MoveX:0} jump {(input.JumpPressed ? "press" : input.JumpHeld ? "held" : "-")}"
                        + $"{(input.DashPressed ? " dash" : "")}"
                );
                while (trace.Count > 120)
                {
                    trace.Dequeue();
                }
                if (box.IsOpen)
                {
                    for (var i = 0; i < 200 && box.IsOpen; i++)
                    {
                        box.Advance();
                    }
                    talking += dt;
                }
                pilot.Step(dt);
                elapsed += dt;
                var leg = pilot.Current;
                if (leg != null && leg.zone > zoneReached)
                {
                    zoneReached = leg.zone;
                    Debug.Log(
                        $"Paradise run: zone {zoneReached} at {elapsed / 60f:0.0} min, x {hero.transform.position.x:0}."
                    );
                }
                if (elapsed - lastLog >= 120f)
                {
                    lastLog = elapsed;
                    Debug.Log(
                        $"Paradise run: {elapsed / 60f:0.0} min, x {hero.transform.position.x:0}, "
                            + $"leg {pilot.LegIndex + 1} of {legs.Count}."
                    );
                }
                yield return null;
            }
            Time.captureDeltaTime = captureBefore;

            var minutes = elapsed / 60f;
            var report =
                $"Paradise run: {minutes:0.0} min to the Low Gate (target {TargetMinutes:0}), "
                + $"{talking / 60f:0.0} min of it talking; {legs.Count} legs, {pilot.AirJumps} air jumps, "
                + $"{pilot.Dashes} dashes, {pilot.StuckJumps} unplanned hops, {pilot.Retries} retried jumps; "
                + $"{played.Count} dialogues: {string.Join(", ", played)}.";
            Debug.Log(report);
            if (pilot.Failure != null || calledOff != null)
            {
                Debug.Log("Paradise run, the last frames before the failure:\n" + string.Join("\n", trace));
            }
            Assert.That(calledOff, Is.Null, $"{report} Where he was: {pilot.Current}, at {hero.transform.position}.");
            Assert.That(pilot.Failure, Is.Null, report);
            Assert.That(pilot.Done, Is.True, report);
            Assert.That(hero.transform.position.x, Is.GreaterThanOrEqualTo(GateX - 1f), report);
            Assert.That(hero.Has(Save.Ability.Wings), Is.True, "the feather in zone 3 was not picked up");
            Assert.That(hero.Has(Save.Ability.LightDash), Is.True, "the light dash in zone 5 was not picked up");
            Assert.That(Story.StoryFlags.Has("opening_seen"), Is.True, "the opening did not play");
            Assert.That(Story.StoryFlags.Has("makinghouse_seen"), Is.True, "the Making-house beat did not play");
            Assert.That(Story.StoryFlags.Has("reached_gate"), Is.True, "the gate beat did not play");
            Assert.That(minutes, Is.LessThan(TargetMinutes * OverTargetAllowed), report);
        }

        [UnityTest]
        public IEnumerator ThePlanCoversEveryZoneAndEndsAtTheGate()
        {
            var zones = ParadiseLayout.All();
            var legs = MainPathPilot.Plan(zones, ParadiseLayout.Start, GateX);
            Assert.That(legs, Is.Not.Empty);
            var seen = new HashSet<int>();
            foreach (var leg in legs)
            {
                seen.Add(leg.zone);
                Assert.That(leg.ontoLine, Is.Not.Null, leg.ToString());
                Assert.That(leg.dir == 1 || leg.dir == -1, leg.ToString());
            }
            Assert.That(seen.Count, Is.EqualTo(zones.Count), "every zone has at least one leg");
            Assert.That(legs[legs.Count - 1].landing.x, Is.EqualTo(GateX).Within(0.01f));
            var jumps = legs.FindAll(l => l.kind == MainPathPilot.Kind.Jump).Count;
            var pods = legs.FindAll(l => l.kind == MainPathPilot.Kind.Pod).Count;
            Assert.That(jumps, Is.GreaterThan(20), "the region asks for jumps");
            Assert.That(pods, Is.GreaterThanOrEqualTo(1), "the pods of the main path are legs");
            yield break;
        }

        /// <summary>Makes an empty scene the active one and unloads every other scene.</summary>
        private IEnumerator AloneInAFreshScene()
        {
            var fresh = SceneManager.CreateScene("Paradise run");
            SceneManager.SetActiveScene(fresh);
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != fresh && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            loaded.Add(fresh.name);
        }

        private PlayerController MakeHero(Vector2 at)
        {
            var go = Thing("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            go.transform.position = new Vector3(at.x, at.y + 0.1f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            return go.AddComponent<PlayerController>();
        }

        private GameObject Thing(string name)
        {
            var thing = new GameObject(name);
            made.Add(thing);
            return thing;
        }
    }
}
