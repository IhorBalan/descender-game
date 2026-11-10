using System.Collections;
using System.Collections.Generic;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using Descender.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Live tuning on a hero in the lab's first room, driven through a ScriptedInput: a
    /// tuned run speed is the speed he runs at while the tuning he came with keeps its
    /// value, a forced ability works without becoming his, and the panel shows it.
    /// </summary>
    public class LiveTuningTests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private LabRoot lab;
        private PlayerController hero;
        private ScriptedInput input;

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
            LiveTuning.Forget();
        }

        [UnityTest]
        public IEnumerator ATunedRunSpeedIsTheSpeedHeRunsAtAndHisOwnTuningKeepsItsValue()
        {
            yield return Steps(2);
            lab.Enter(hero, lab.IndexOf("run"));
            yield return Steps(30);
            Assert.That(hero.IsGrounded, Is.True);
            var came = hero.Tuning;

            input.MoveX = 1f;
            yield return Steps(40);
            Assert.That(hero.VelocityPixels.x, Is.EqualTo(100f).Within(1f));

            var answer = TuneCommands.Tune(new[] { "runSpeed", "160" });
            Assert.That(answer, Is.EqualTo("runSpeed 160 (asset 100), was 100"));
            Assert.That(hero.Tuning, Is.SameAs(LiveTuning.Working));
            Assert.That(hero.Tuning, Is.Not.SameAs(came));
            yield return Steps(40);
            Assert.That(hero.VelocityPixels.x, Is.EqualTo(160f).Within(1f));
            Assert.That(came.runSpeed, Is.EqualTo(100f), "the tuning he came with is not written");

            Assert.That(TuneCommands.Tune(new[] { "reset" }), Is.EqualTo("Put 1 back to the asset's."));
            yield return Steps(40);
            Assert.That(hero.VelocityPixels.x, Is.EqualTo(100f).Within(1f));
        }

        [UnityTest]
        public IEnumerator AForcedAbilityWorksAndIsNotHisToKeep()
        {
            yield return Steps(2);
            lab.Enter(hero, lab.IndexOf("run"));
            hero.SetAbilities(new Save.Ability[0]);
            yield return Steps(30);
            Assert.That(hero.IsGrounded, Is.True);

            // Without wings a second press in the air does nothing.
            yield return Jump();
            yield return UntilFalling();
            yield return Press();
            Assert.That(hero.VelocityPixels.y, Is.LessThan(0f));
            yield return UntilGrounded();

            Assert.That(TuneCommands.Ability(new[] { "wings", "on" }), Is.EqualTo("wings on (forced, not his)"));
            Assert.That(hero.Has(Save.Ability.Wings), Is.True);
            Assert.That(hero.Owns(Save.Ability.Wings), Is.False, "a save records what he owns");
            yield return Jump();
            yield return UntilFalling();
            yield return Press();
            Assert.That(hero.VelocityPixels.y, Is.GreaterThan(0f), "the forced wings carry him up again");
            yield return UntilGrounded();

            hero.Grant(Save.Ability.Wings);
            TuneCommands.Ability(new[] { "wings", "off" });
            Assert.That(hero.Has(Save.Ability.Wings), Is.False, "forced off wins over owning it");
            Assert.That(hero.Owns(Save.Ability.Wings), Is.True);
            TuneCommands.Ability(new[] { "wings", "reset" });
            Assert.That(hero.Has(Save.Ability.Wings), Is.True);
        }

        [UnityTest]
        public IEnumerator ThePanelShowsTheHerosNumbersAndTurnsItsPages()
        {
            yield return Steps(2);
            lab.Enter(hero, lab.IndexOf("run"));
            yield return Steps(5);
            TuneCommands.Tune(new[] { "runSpeed", "160" });

            var panelObject = new GameObject("Tuning panel");
            made.Add(panelObject);
            var panel = panelObject.AddComponent<TuningOverlay>();
            yield return null;
            var rows = panelObject.GetComponentInChildren<TextMeshProUGUI>();
            Assert.That(rows, Is.Not.Null);
            Assert.That(rows.text, Does.StartWith("Run (px/s, px/s²)  1/7"));
            Assert.That(rows.text, Does.Contain("* runSpeed 160 (asset 100)"));

            Assert.That(panel.TurnTo("dash"), Is.True);
            Assert.That(rows.text, Does.StartWith("Dash  4/7"));
            Assert.That(rows.text, Does.Contain("dashSpeed 260"));
            panel.Turn(panel.Group - 4);
            Assert.That(panel.Group, Is.EqualTo(6), "turning back past the first page comes round to the last");
            Assert.That(panel.TurnTo("swim"), Is.False);
        }

        private IEnumerator Jump()
        {
            Assert.That(hero.IsGrounded, Is.True);
            yield return Press();
        }

        /// <summary>One press of jump, held, read by the hero over a step.</summary>
        private IEnumerator Press()
        {
            input.JumpPressed = true;
            input.JumpHeld = true;
            yield return null;
            input.ClearPresses();
            yield return Steps(2);
        }

        private IEnumerator UntilFalling()
        {
            for (var i = 0; i < 200 && hero.VelocityPixels.y >= -20f; i++)
            {
                yield return null;
            }
            Assert.That(hero.VelocityPixels.y, Is.LessThan(-20f));
            Assert.That(hero.IsGrounded, Is.False);
        }

        private IEnumerator UntilGrounded()
        {
            input.JumpHeld = false;
            for (var i = 0; i < 300 && !hero.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.That(hero.IsGrounded, Is.True);
            yield return Steps(5);
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
