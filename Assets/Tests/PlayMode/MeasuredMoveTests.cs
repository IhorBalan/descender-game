using System.Collections;
using System.Collections.Generic;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Every move of the hero held to its number: in the lab's rooms a
    /// <see cref="MoveProbe"/> drives him and measures the jump, the cut jump, the double
    /// jump, the dash and the light dash, coyote time, the jump buffer, the wall jump, the
    /// glide and the slam. Each expectation is worked out from the tuning asset, so a
    /// tuned number moves its test with it; the margins are one physics step of travel.
    /// </summary>
    public class MeasuredMoveTests
    {
        private const string TuningPath = "Assets/Data/PlayerTuning.asset";

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private PlayerTuning tuning;
        private LabRoot lab;
        private PlayerController hero;
        private ScriptedInput input;
        private MoveProbe probe;
        private int smotes;

        private static float Dt => MoveProbe.Dt;

        private float Rise => hero.VelocityPixels.y;

        private void OnSmote(Vector2 at, Vector2 reach) => smotes++;

        [SetUp]
        public void SetUp()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            LiveTuning.Forget();
            smotes = 0;
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;
#if UNITY_EDITOR
            tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
#endif
            Assert.That(tuning, Is.Not.Null, $"the tuning asset is at {TuningPath}");

            lab = LabBuilder.Build(tuning);
            made.Add(lab.gameObject);
            var go = new GameObject("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            made.Add(go);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            hero.UseTuning(tuning);
            input = new ScriptedInput();
            hero.InputSource = input;
            probe = new MoveProbe(hero, input);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = captureBefore;
            PlayerController.Smote -= OnSmote;
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.Destroy(thing);
                }
            }
            made.Clear();
        }

        /// <summary>Into a room of the lab with exactly these abilities, standing still.</summary>
        private IEnumerator Enter(string id, params Save.Ability[] abilities)
        {
            yield return null;
            lab.Enter(hero, lab.IndexOf(id));
            hero.SetAbilities(abilities);
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, $"{id}: he stands");
            probe.Begin();
        }

        /// <summary>A jump from the ground to the ground again, the button held or tapped.</summary>
        private IEnumerator JumpAndLand(bool hold)
        {
            probe.Begin();
            yield return probe.PressJump(hold);
            yield return probe.Step(2);
            Assert.That(hero.IsGrounded, Is.False, "the press started a jump");
            yield return probe.Until(() => hero.IsGrounded, 300);
            Assert.That(hero.IsGrounded, Is.True, "he came down");
        }

        [UnityTest]
        public IEnumerator AFullJumpReachesJumpHeightInItsTime()
        {
            yield return Enter("run");
            yield return JumpAndLand(true);

            // The first step travels at the full launch speed, so the top is up to a step over.
            var step = tuning.JumpVelocity * Dt;
            Assert.That(probe.Apex, Is.InRange(tuning.jumpHeight - 1f, tuning.jumpHeight + step + 1f));
            var time = tuning.jumpTimeToApex + tuning.jumpTimeToLand;
            // The float at the top adds a little to the time.
            Assert.That(probe.AirTime, Is.InRange(time - 2f * Dt, time + 8f * Dt));
            Assert.That(probe.Distance, Is.LessThan(0.5f), "straight up and down");
        }

        [UnityTest]
        public IEnumerator ATappedJumpIsCutShort()
        {
            yield return Enter("run");
            yield return JumpAndLand(true);
            var full = probe.Apex;
            yield return probe.Settle();

            yield return JumpAndLand(false);
            Assert.That(probe.Apex, Is.GreaterThan(4f), "a tap still leaves the ground");
            Assert.That(probe.Apex, Is.LessThan(full * 0.5f), "and is cut well under the full jump");
        }

        [UnityTest]
        public IEnumerator TheDoubleJumpAddsDoubleJumpHeight()
        {
            yield return Enter("run", Save.Ability.Wings);
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => Rise <= 0f, 200);
            Assert.That(hero.IsGrounded, Is.False);
            var first = probe.Apex;

            yield return probe.PressJump();
            var from = probe.Height;
            yield return probe.Step(2);
            Assert.That(Rise, Is.GreaterThan(0f), "the wings carry him up again");
            yield return probe.Until(() => Rise <= 0f, 200);
            var added = probe.Apex - from;
            var step = tuning.DoubleJumpVelocity * Dt;
            Assert.That(added, Is.InRange(tuning.doubleJumpHeight - 2f, tuning.doubleJumpHeight + step + 1f));
            Assert.That(probe.Apex, Is.GreaterThan(first + tuning.doubleJumpHeight - 4f));

            // Only one: a third press in the air does nothing.
            yield return probe.PressJump();
            yield return probe.Step(2);
            Assert.That(Rise, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator ADashCoversDashSpeedTimesDashTime()
        {
            yield return Enter("run");
            yield return Dash(tuning.dashSpeed * tuning.dashTime);
        }

        [UnityTest]
        public IEnumerator ALightDashCoversItsOwnLength()
        {
            yield return Enter("run", Save.Ability.LightDash);
            Assert.That(tuning.lightDashTime, Is.GreaterThan(tuning.dashTime));
            yield return Dash(tuning.dashSpeed * tuning.lightDashTime);
        }

        /// <summary>Faces right, dashes from a standstill, and holds the ground covered to a length.</summary>
        private IEnumerator Dash(float length)
        {
            input.MoveX = 1f;
            yield return probe.Step(3);
            yield return probe.Settle();
            Assert.That(hero.Facing, Is.EqualTo(1));

            probe.Begin();
            yield return probe.Press(i => i.DashPressed = true);
            yield return probe.Until(() => hero.IsDashing, 5);
            Assert.That(hero.IsDashing, Is.True, "the press started a dash");
            var covered = 0f;
            for (var i = 0; i < 100 && hero.IsDashing; i++)
            {
                covered = probe.Distance;
                yield return probe.Step();
            }
            Assert.That(hero.IsDashing, Is.False, "the dash ended");
            Assert.That(covered, Is.EqualTo(length).Within(tuning.dashSpeed * Dt + 0.5f));
            Assert.That(probe.At.x, Is.GreaterThan(probe.From.x), "to the right, the way he faced");
            Assert.That(Mathf.Abs(probe.Height), Is.LessThan(0.5f), "level");
        }

        [UnityTest]
        public IEnumerator AJumpJustAfterALedgeStillJumpsAndALateOneDoesNot()
        {
            // The glide room starts on a tower: walking right leaves its edge with a long fall under it.
            yield return Enter("glide");
            yield return WalkOffTheEdge();
            yield return probe.PressJump();
            yield return probe.Step(2);
            Assert.That(Rise, Is.GreaterThan(0f), "a press right after the edge is still a jump");

            yield return Enter("glide");
            yield return WalkOffTheEdge();
            var late = Mathf.CeilToInt(tuning.coyoteTime / Dt) + 2;
            yield return probe.Step(late);
            yield return probe.PressJump();
            yield return probe.Step(3);
            Assert.That(Rise, Is.LessThan(0f), "a press after coyote time is not");
            Assert.That(hero.IsGrounded, Is.False, "he is still falling, so the press was not a landing");
        }

        private IEnumerator WalkOffTheEdge()
        {
            input.MoveX = 1f;
            yield return probe.Until(() => !hero.IsGrounded, 300);
            Assert.That(hero.IsGrounded, Is.False, "he walked off the tower");
            Assert.That(Rise, Is.LessThanOrEqualTo(0f));
        }

        [UnityTest]
        public IEnumerator AJumpPressedJustBeforeLandingJumpsOnLandingAndAnEarlyOneDoesNot()
        {
            yield return Enter("run");
            yield return JumpAndLand(true);
            var air = probe.AirSteps;
            var buffer = Mathf.CeilToInt(tuning.inputBufferTime / Dt);
            Assert.That(air, Is.GreaterThan(buffer + 12), "the jump is long enough to press early in");
            yield return probe.Settle();

            // Three steps before the landing: inside the buffer.
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => probe.AirSteps >= air - 3, 300);
            Assert.That(hero.IsGrounded, Is.False);
            yield return probe.PressJump();
            yield return probe.Step(6);
            Assert.That(hero.IsGrounded, Is.False, "he is off the ground again");
            Assert.That(Rise, Is.GreaterThan(0f), "the buffered press jumped as he landed");
            yield return probe.Until(() => hero.IsGrounded, 300);
            yield return probe.Settle();

            // Four steps more than the buffer before the landing: forgotten by then.
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => probe.AirSteps >= air - buffer - 4, 300);
            Assert.That(hero.IsGrounded, Is.False);
            yield return probe.PressJump();
            yield return probe.Until(() => hero.IsGrounded, 60);
            yield return probe.Step(4);
            Assert.That(hero.IsGrounded, Is.True, "he landed and stayed down");
            Assert.That(Mathf.Abs(Rise), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator TheWallJumpLeavesTheWall()
        {
            yield return Enter("wall", Save.Ability.WallGrip);
            var station = lab.Stations[lab.IndexOf("wall")];
            // The foot of the shaft: the wall to grip is two units to the right.
            hero.PlaceAt(new Vector2(station.room.x + 22f, LabLayout.FloorTop));
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True);

            input.MoveX = 1f;
            yield return probe.PressJump();
            yield return probe.Until(() => hero.IsGrippingWall, 200);
            Assert.That(hero.IsGrippingWall, Is.True, "falling against the wall, he grips it");
            Assert.That(hero.WallDir, Is.EqualTo(1));
            yield return probe.Step(15);
            Assert.That(hero.IsGrippingWall, Is.True);
            Assert.That(Rise, Is.EqualTo(-tuning.wallSlideSpeed).Within(0.5f), "he slides at the wall slide speed");

            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            Assert.That(hero.IsGrippingWall, Is.False, "the jump lets go of the wall");
            var away = hero.VelocityPixels;
            Assert.That(away.x, Is.LessThan(-0.8f * tuning.wallJumpVelocity.x), "away from it");
            Assert.That(away.y, Is.GreaterThan(0.8f * tuning.wallJumpVelocity.y), "and up");
            yield return probe.Step(5);
            Assert.That(probe.At.x, Is.LessThan(probe.From.x - 8f), "holding towards the wall does not bring him back");
        }

        [UnityTest]
        public IEnumerator TheGlideFallsAtGlideFallSpeed()
        {
            yield return Enter("run", Save.Ability.Glide);
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => Rise < 0f, 200);
            Assert.That(hero.IsGrounded, Is.False);

            // A press on the way down opens the wings; holding keeps them open.
            yield return probe.PressJump();
            yield return probe.Step(20);
            Assert.That(hero.IsGliding, Is.True);
            Assert.That(hero.IsGrounded, Is.False);
            Assert.That(Rise, Is.EqualTo(-tuning.glideFallSpeed).Within(0.5f));

            // Letting go closes them and he falls faster.
            input.JumpHeld = false;
            yield return probe.Step(6);
            Assert.That(hero.IsGliding, Is.False);
            Assert.That(Rise, Is.LessThan(-tuning.glideFallSpeed - 20f));
        }

        [UnityTest]
        public IEnumerator TheSlamReachesSlamSpeed()
        {
            yield return Enter("run", Save.Ability.Smite);
            PlayerController.Smote += OnSmote;
            probe.Begin();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => Rise <= 0f, 200);
            Assert.That(hero.IsGrounded, Is.False);

            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Step(2);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Slam));
            Assert.That(Rise, Is.EqualTo(-tuning.slamSpeed).Within(0.5f));
            Assert.That(smotes, Is.EqualTo(0));

            yield return probe.Until(() => hero.IsGrounded, 100);
            yield return probe.Step(2);
            input.DownHeld = false;
            Assert.That(smotes, Is.EqualTo(1), "the shock goes off once, on landing");
            yield return probe.Until(() => hero.State != PlayerState.Slam, 100);
            Assert.That(hero.State, Is.Not.EqualTo(PlayerState.Slam), "he gets up again");
            Assert.That(Mathf.Abs(probe.Distance), Is.LessThan(0.5f), "straight down");
        }
    }
}
