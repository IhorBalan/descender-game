using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The sword's feel: cuts ahead pressed inside the window are a combo whose last cut
    /// is slower, hits harder and pushes farther; a push slides a dummy as far as its
    /// strength says and it comes home; a cut that lands shoves him back a little; and a
    /// tuned rule says whether a dash or a jump ends a swing early.
    /// </summary>
    public class ComboTests
    {
        private const float Floor = 2f;
        private const float HalfHero = 0.4375f;
        private const float Pixel = 1f / PlayerTuning.PixelsPerUnit;
        private const float HeroAt = 10f;

        private readonly List<GameObject> made = new List<GameObject>();
        private float captureBefore;
        private Station room;
        private LabRoot lab;
        private PlayerController hero;
        private Rigidbody2D heroBody;
        private ScriptedInput input;
        private MoveProbe probe;
        private int cut;
        private bool finisher;
        private int cutSteps;

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

            room = new Station();
            room.id = "test";
            room.title = "test";
            room.room = new Rect(0f, 0f, 56f, 30f);
            room.spawn = new Vector2(40f, Floor);
            Put(0f, 0f, 56f, Floor);
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
        }

        private void Put(float x, float y, float width, float height)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Solid;
            piece.rect = new Rect(x, y, width, height);
            room.pieces.Add(piece);
        }

        private IEnumerator Begin(Vector2 at)
        {
            var stations = new List<Station>();
            stations.Add(room);
            lab = LabBuilder.Build(stations);
            made.Add(lab.gameObject);
            var go = new GameObject("Hero");
            go.layer = LayerMask.NameToLayer("Player");
            made.Add(go);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.875f, 2f);
            box.offset = new Vector2(0f, 1f);
            hero = go.AddComponent<PlayerController>();
            heroBody = go.GetComponent<Rigidbody2D>();
            input = new ScriptedInput();
            hero.InputSource = input;
            probe = new MoveProbe(hero, input);
            yield return null;
            lab.Enter(hero, 0);
            hero.PlaceAt(at);
            yield return probe.Settle();
            Assert.That(hero.IsGrounded, Is.True, "he stands where the test put him");
        }

        private Piece PutDummy(float x, float y, float width, float height, string label = "")
        {
            var piece = new Piece();
            piece.kind = PieceKind.Dummy;
            piece.rect = new Rect(x, y, width, height);
            piece.label = label;
            room.pieces.Add(piece);
            return piece;
        }

        private Transform Built(Piece piece) => lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));

        private Dummy DummyOf(Piece piece) => Built(piece).GetComponent<Dummy>();

        /// <summary>One press of attack, then frames until the swing is over: which cut it was and how long.</summary>
        private IEnumerator Cut()
        {
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "the press started a swing");
            cut = hero.ComboCut;
            finisher = hero.IsFinisher;
            cutSteps = 0;
            while (hero.State == PlayerState.Attack && cutSteps < 200)
            {
                yield return probe.Step();
                cutSteps++;
            }
        }

        [UnityTest]
        public IEnumerator CutsInsideTheWindowAreAComboAndTheLastIsSlower()
        {
            yield return Begin(new Vector2(HeroAt, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var plain = tuning.attackTime / MoveProbe.Dt;
            var heavy = tuning.attackTime * tuning.finisherTimeScale / MoveProbe.Dt;
            var window = tuning.comboWindow;
            Assert.That(tuning.comboCuts, Is.EqualTo(3));
            Assert.That(heavy - plain, Is.GreaterThan(4f), "the last cut is slower by more than the tolerance");
            Object.Destroy(tuning);
            var stoodAt = heroBody.position.x;

            yield return Cut();
            Assert.That(cut, Is.EqualTo(1));
            Assert.That(cutSteps, Is.EqualTo(plain).Within(2f));
            yield return Cut();
            Assert.That(cut, Is.EqualTo(2));
            Assert.That(finisher, Is.False);
            Assert.That(cutSteps, Is.EqualTo(plain).Within(2f));
            yield return Cut();
            Assert.That(cut, Is.EqualTo(3));
            Assert.That(finisher, Is.True, "the third is the last");
            Assert.That(cutSteps, Is.EqualTo(heavy).Within(2f), "and slower");
            yield return Cut();
            Assert.That(cut, Is.EqualTo(1), "after the last it starts over");
            Assert.That(cutSteps, Is.EqualTo(plain).Within(2f));

            // Past the window the next press is a first cut again.
            yield return probe.Step(Mathf.CeilToInt(window / MoveProbe.Dt) + 5);
            yield return Cut();
            Assert.That(cut, Is.EqualTo(1), "the window closed");
            var moved = (heroBody.position.x - stoodAt) / Pixel;
            Assert.That(moved, Is.EqualTo(0f).Within(0.5f), "a cut at nothing does not move him");
        }

        [UnityTest]
        public IEnumerator TheLastCutHitsHarderAndPushesFartherAndALandedCutShovesHimBack()
        {
            var piece = PutDummy(HeroAt + 0.75f, Floor, 1f, 2f);
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(piece);
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var push = tuning.attackPush;
            var heavyPush = tuning.finisherPush;
            var more = tuning.finisherDamage;
            var recoil = tuning.attackRecoil * tuning.attackRecoil / (2f * tuning.groundFriction);
            Object.Destroy(tuning);
            Assert.That(more, Is.GreaterThan(0));
            Assert.That(heavyPush, Is.GreaterThan(push));
            var stoodAt = heroBody.position.x;

            yield return Cut();
            Assert.That(dummy.Hits, Is.EqualTo(1));
            Assert.That(dummy.Last.Damage, Is.EqualTo(hero.AttackDamage));
            Assert.That(dummy.Last.Push.x, Is.EqualTo(push).Within(0.001f), "pushed the way he faces");
            Assert.That(dummy.Last.Push.y, Is.EqualTo(0f));
            var back = (stoodAt - heroBody.position.x) / Pixel;
            Assert.That(back, Is.EqualTo(recoil).Within(1.5f), "the cut that landed shoved him back");
            Assert.That(back, Is.GreaterThan(0.5f));

            yield return Cut();
            Assert.That(dummy.Hits, Is.EqualTo(2));
            Assert.That(dummy.Last.Damage, Is.EqualTo(hero.AttackDamage));
            yield return Cut();
            Assert.That(finisher, Is.True);
            Assert.That(dummy.Hits, Is.EqualTo(3));
            Assert.That(dummy.Last.Damage, Is.EqualTo(hero.AttackDamage + more), "the last cut hits harder");
            Assert.That(dummy.Last.Push.x, Is.EqualTo(heavyPush).Within(0.001f), "and pushes harder");
            Assert.That(dummy.Total, Is.EqualTo(3 * hero.AttackDamage + more));
            Assert.That(Built(piece).position.x, Is.GreaterThan(piece.rect.center.x), "the dummy has been pushed away");
        }

        [UnityTest]
        public IEnumerator APushSlidesADummyAsFarAsItsStrengthSaysAndItComesHome()
        {
            var piece = PutDummy(HeroAt + 6f, Floor, 1f, 2f, "target");
            yield return Begin(new Vector2(HeroAt, Floor));
            var dummy = DummyOf(piece);
            var home = (Vector2)Built(piece).position;
            const float Strength = 100f;
            var slide = Dummy.SlideOf(Strength);
            Assert.That(slide, Is.EqualTo(12.5f).Within(0.001f));

            var from = new Vector2(HeroAt, Floor);
            var strike = new Strike(StrikeKind.Sword, 1, from, new Vector2(Strength, 0f));
            Assert.That(Strikes.Deliver(dummy, strike), Is.True);
            Assert.That(dummy.IsSliding, Is.True);
            yield return probe.Until(() => !dummy.IsSliding, 100);
            Assert.That(dummy.IsSliding, Is.False, "friction stopped it");
            Assert.That(dummy.Pushed, Is.EqualTo(slide).Within(1.5f), "as far as the push's strength says");
            Assert.That(dummy.Farthest, Is.EqualTo(dummy.Pushed).Within(0.001f));
            Assert.That((Built(piece).position.x - home.x) / Pixel, Is.EqualTo(dummy.Pushed).Within(0.01f));
            Assert.That(Built(piece).position.y, Is.EqualTo(home.y).Within(0.001f));
            Assert.That(dummy.ReadOut(), Does.EndWith(" px"), "the read-out says how far");

            yield return probe.Until(() => dummy.Pushed <= 0f, 300);
            Assert.That(Built(piece).position.x, Is.EqualTo(home.x).Within(0.001f), "after a rest it goes home");
            Assert.That(dummy.ReadOut(), Does.EndWith("a second"));
            Assert.That(dummy.Hits, Is.EqualTo(1), "and keeps its count");

            Strikes.Deliver(dummy, new Strike(StrikeKind.Sword, 1, from, new Vector2(0f, -Strength)));
            yield return probe.Step(3);
            Assert.That(Built(piece).position.y, Is.LessThan(home.y), "a push goes the way the strike says");
            dummy.Clear();
            Assert.That(Built(piece).position.y, Is.EqualTo(home.y).Within(0.001f), "forgetting puts it back");
        }

        [UnityTest]
        public IEnumerator ADashEndsASwingEarlyAndAJumpDoesWhenTheTuningSays()
        {
            yield return Begin(new Vector2(HeroAt, Floor));
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            var whole = Mathf.RoundToInt(tuning.attackTime / MoveProbe.Dt);
            var open = Mathf.CeilToInt(tuning.attackCancelFrom / MoveProbe.Dt) + 1;
            Assert.That(tuning.dashCancelsAttack, Is.True);
            Assert.That(tuning.jumpCancelsAttack, Is.False);
            Assert.That(open + 3, Is.LessThan(whole), "there is swing left to cut short");

            // As tuned: a dash ends the swing, a jump waits.
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            yield return probe.Step(open);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack));
            yield return probe.Press(i => i.DashPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Dash, 6);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Dash), "the dash cut the swing short");
            yield return probe.Settle();
            yield return probe.Step(60);

            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            yield return probe.Step(open);
            yield return probe.PressJump(false);
            yield return probe.Step(2);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "a jump does not, as tuned");
            Assert.That(hero.IsGrounded, Is.True);
            yield return probe.Settle();
            yield return probe.Step(30);

            // The other way round.
            tuning.dashCancelsAttack = false;
            tuning.jumpCancelsAttack = true;
            hero.UseTuning(tuning);
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            yield return probe.Step(open);
            yield return probe.Press(i => i.DashPressed = true);
            yield return probe.Step(2);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "switched off, a dash waits for the swing");
            yield return probe.Settle();
            yield return probe.Step(60);

            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            yield return probe.Step(open);
            yield return probe.PressJump(false);
            yield return probe.Until(() => !hero.IsGrounded, 6);
            Assert.That(hero.IsGrounded, Is.False, "switched on, a jump cuts the swing short");
            Assert.That(hero.State, Is.Not.EqualTo(PlayerState.Attack));
            yield return probe.Until(() => hero.IsGrounded, 300);
            Object.Destroy(tuning);
        }
    }
}
