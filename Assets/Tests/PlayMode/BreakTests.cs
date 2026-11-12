using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Input;
using Descender.Lab;
using Descender.Player;
using Descender.Triggers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Things that break: a block stands in his way until it has taken its number of
    /// cuts, then it is gone and its actions have run; one that asks for the slam or for
    /// a bolt is broken by that strike only.
    /// </summary>
    public class BreakTests
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

        /// <summary>One press of attack with whatever is held, then frames until the swing is over.</summary>
        private IEnumerator Swing()
        {
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Attack, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Attack), "the press started a swing");
            yield return probe.Until(() => hero.State != PlayerState.Attack, 100);
        }

        private Piece PutBreakable(float x, float y, float width, float height, int hits, StruckBy by)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Breakable;
            piece.rect = new Rect(x, y, width, height);
            piece.value = hits;
            piece.by = by;
            room.pieces.Add(piece);
            return piece;
        }

        private Breakable BlockOf(Piece piece) => Built(piece).GetComponent<Breakable>();

        [UnityTest]
        public IEnumerator ABlockStandsInHisWayUntilItHasTakenItsCutsThenItsActionsRun()
        {
            var drop = new Piece();
            drop.kind = PieceKind.Zone;
            drop.rect = new Rect(HeroAt + 6f, Floor, 1f, 1f);
            drop.id = "drop";
            drop.hidden = true;
            room.pieces.Add(drop);
            var piece = PutBreakable(HeroAt + 1f, Floor, 1f, 2f, 3, StruckBy.Any);
            piece.actions = find => new TriggerAction[] { Do.Active(find("drop"), SwitchTo.On) };
            yield return Begin(new Vector2(HeroAt, Floor));
            var block = BlockOf(piece);
            Assert.That(block.Hits, Is.EqualTo(3));
            Assert.That(Built(drop).gameObject.activeSelf, Is.False, "what it leaves is not there yet");

            // He cannot walk through it.
            input.MoveX = 1f;
            yield return probe.Step(40);
            input.MoveX = 0f;
            yield return probe.Settle();
            var stopped = piece.rect.xMin - HalfHero;
            Assert.That(heroBody.position.x, Is.EqualTo(stopped).Within(2f * Pixel), "it stands in his way");

            yield return Swing();
            Assert.That(block.HitsLeft, Is.EqualTo(2), "a cut is a hit");
            Assert.That(block.IsBroken, Is.False);
            yield return probe.Step(30);
            yield return Swing();
            yield return probe.Step(30);
            Assert.That(block.HitsLeft, Is.EqualTo(1));
            Assert.That(Built(piece).gameObject.activeSelf, Is.True);
            yield return Swing();
            Assert.That(block.IsBroken, Is.True, "the third breaks it");
            Assert.That(Built(piece).gameObject.activeSelf, Is.False, "and it is gone");
            Assert.That(Built(drop).gameObject.activeSelf, Is.True, "its actions ran");

            yield return probe.Settle();
            input.MoveX = 1f;
            yield return probe.Step(60);
            input.MoveX = 0f;
            Assert.That(heroBody.position.x, Is.GreaterThan(piece.rect.xMax), "and he walks where it stood");
        }

        [UnityTest]
        public IEnumerator OneOnlyTheSlamBreaksAndOneOnlyABolt()
        {
            room.abilities = new[] { Save.Ability.HolyLight, Save.Ability.Smite };
            // Ahead of him, in the sword's reach and in the slam's shock: only a bolt breaks it.
            var ahead = PutBreakable(HeroAt + 1.25f, Floor, 1f, 2f, 1, StruckBy.Bolt);
            // Behind him, in the slam's shock: only the slam breaks it.
            var behind = PutBreakable(HeroAt - 2f, Floor, 1f, 2f, 1, StruckBy.Slam);
            yield return Begin(new Vector2(HeroAt, Floor));
            var bolts = BlockOf(ahead);
            var slams = BlockOf(behind);
            Assert.That(hero.Facing, Is.EqualTo(1));

            yield return Swing();
            Assert.That(bolts.IsBroken, Is.False, "the sword does not break the bolt's block");
            Assert.That(bolts.HitsLeft, Is.EqualTo(1));
            var cut = new Strike(StrikeKind.Sword, 5, heroBody.position);
            Assert.That(Strikes.Deliver(slams, cut), Is.False, "nor the slam's, however hard it hits");
            Assert.That(slams.Hit(5, heroBody.position), Is.False);

            yield return probe.Settle();
            yield return probe.PressJump();
            yield return probe.Step(2);
            yield return probe.Until(() => heroBody.linearVelocity.y <= 0f, 200);
            input.DownHeld = true;
            yield return probe.Press(i => i.AttackPressed = true);
            yield return probe.Until(() => hero.State == PlayerState.Slam, 10);
            Assert.That(hero.State, Is.EqualTo(PlayerState.Slam));
            yield return probe.Until(() => hero.State != PlayerState.Slam, 300);
            input.DownHeld = false;
            input.JumpHeld = false;
            Assert.That(slams.IsBroken, Is.True, "the slam breaks its block");
            Assert.That(bolts.IsBroken, Is.False, "and not the bolt's, though the shock reaches it");

            yield return probe.Settle();
            yield return probe.Press(i => i.CastPressed = true);
            yield return probe.Until(() => bolts.IsBroken, 60);
            Assert.That(bolts.IsBroken, Is.True, "a bolt breaks its block");
            Assert.That(Built(ahead).gameObject.activeSelf, Is.False);

            bolts.Restore();
            Assert.That(bolts.IsBroken, Is.False, "restored, it is whole again");
            Assert.That(bolts.HitsLeft, Is.EqualTo(1));
            Assert.That(Built(ahead).gameObject.activeSelf, Is.True);
        }
    }
}
