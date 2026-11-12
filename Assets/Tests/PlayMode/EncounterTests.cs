using System.Collections;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Enemies;
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
    /// A fight in a room: walking in shuts the doors, each wave is warned of and comes
    /// when the one before is dead, and the doors open when the last is; if the hero dies
    /// it is put back and can begin again.
    /// </summary>
    public class EncounterTests
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
            // Arriving in a room leaves him safe for a while; the tests start once he can be hurt.
            yield return probe.Until(() => hero.CanBeHurt, 100);
            Assert.That(hero.CanBeHurt, Is.True);
        }

        private Transform Built(Piece piece) => lab.ParentOf(room).GetChild(room.pieces.IndexOf(piece));

        private Piece PutFoe(float x, float y, float width, float height, FoeBody with, params FoeBehaviour[] acts)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Foe;
            piece.rect = new Rect(x, y, width, height);
            piece.foe = new FoeRecipe("foe", with, acts);
            room.pieces.Add(piece);
            return piece;
        }

        private Foe FoeOf(Piece piece) => Built(piece).GetComponent<Foe>();

        private static FoeRecipe Imp()
        {
            var body = new FoeBody
            {
                health = 1,
                contactDamage = 0,
                size = new Vector2(12f, 12f),
            };
            return new FoeRecipe("imp", body);
        }

        private Piece PutDoor(float x, string id)
        {
            var piece = new Piece();
            piece.kind = PieceKind.Door;
            piece.rect = new Rect(x, Floor, 1f, 6f);
            piece.id = id;
            room.pieces.Add(piece);
            return piece;
        }

        /// <summary>Two doors, a fight of two waves between them (two imps, then one), and a zone past the first door that begins it.</summary>
        private Piece PutFight()
        {
            PutDoor(14f, "a");
            PutDoor(34f, "b");
            var first = new EncounterWave();
            first.Add(Imp(), new Vector2(20f, Floor));
            first.Add(Imp(), new Vector2(24f, Floor));
            var second = new EncounterWave();
            second.Add(Imp(), new Vector2(22f, Floor));
            var fight = new Piece();
            fight.kind = PieceKind.Encounter;
            fight.rect = new Rect(24f, Floor + 8f, 1f, 1f);
            fight.id = "fight";
            fight.waves = new List<EncounterWave>();
            fight.waves.Add(first);
            fight.waves.Add(second);
            fight.doors = new[] { "a", "b" };
            room.pieces.Add(fight);
            var start = new Piece();
            start.kind = PieceKind.Zone;
            start.rect = new Rect(16.5f, Floor, 1f, 3f);
            start.once = false;
            start.actions = find => new TriggerAction[] { Do.Fight(find("fight").GetComponent<Encounter>()) };
            room.pieces.Add(start);
            return fight;
        }

        private Door DoorOf(string id) => Built(room.pieces.Find(piece => piece.id == id)).GetComponent<Door>();

        private static void Kill(Foe foe)
        {
            Assert.That(foe.Struck(new Strike(StrikeKind.Sword, 9, Vector2.zero)), Is.True);
            Assert.That(foe.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator WalkingInShutsTheDoorsTheWavesComeInOrderAndTheDoorsOpenWhenAllAreDead()
        {
            var piece = PutFight();
            yield return Begin(new Vector2(8f, Floor));
            var fight = Built(piece).GetComponent<Encounter>();
            var cleared = 0;
            fight.Cleared += () => cleared++;
            Assert.That(fight.State, Is.EqualTo(EncounterState.Waiting));
            Assert.That(DoorOf("a").IsOpen, Is.True, "the doors stand open");
            Assert.That(DoorOf("b").IsOpen, Is.True);

            // Through the open door and on to the zone.
            hero.PlaceAt(new Vector2(15.5f, Floor));
            input.MoveX = 1f;
            yield return probe.Until(() => fight.State == EncounterState.Running, 60);
            input.MoveX = 0f;
            Assert.That(fight.State, Is.EqualTo(EncounterState.Running), "walking in begins it");
            Assert.That(DoorOf("a").IsOpen, Is.False, "the doors shut");
            Assert.That(DoorOf("b").IsOpen, Is.False);
            Assert.That(DoorOf("a").GetComponent<Collider2D>().enabled, Is.True, "and are solid");

            Assert.That(fight.IsWarning, Is.True, "the first wave is warned of");
            var steps = 0;
            while (fight.IsWarning && steps < 200)
            {
                Assert.That(fight.Foes, Is.Empty, "no foe before the warning ends");
                yield return probe.Step();
                steps++;
            }
            Assert.That(steps, Is.EqualTo(Encounter.DefaultWarning / MoveProbe.Dt).Within(3f));
            Assert.That(fight.Wave, Is.EqualTo(0));
            Assert.That(fight.Foes, Has.Count.EqualTo(2), "then its two foes");
            Assert.That(fight.Foes[0].transform.position.x, Is.EqualTo(20f).Within(Pixel), "where the wave puts them");
            Assert.That(fight.Foes[1].transform.position.x, Is.EqualTo(24f).Within(Pixel));

            Kill(fight.Foes[0]);
            yield return probe.Step(10);
            Assert.That(fight.Wave, Is.EqualTo(0), "one still lives: the next wave waits");
            Assert.That(fight.IsWarning, Is.False);
            Kill(fight.Foes[1]);
            yield return probe.Until(() => fight.IsWarning, 5);
            Assert.That(fight.Wave, Is.EqualTo(1), "both dead: the second wave is warned of");
            yield return probe.Until(() => !fight.IsWarning, 60);
            Assert.That(fight.Foes, Has.Count.EqualTo(1));
            Assert.That(DoorOf("b").IsOpen, Is.False, "the doors stay shut");

            Kill(fight.Foes[0]);
            yield return probe.Until(() => fight.State == EncounterState.Cleared, 5);
            Assert.That(fight.State, Is.EqualTo(EncounterState.Cleared), "the last is dead: it is won");
            Assert.That(cleared, Is.EqualTo(1), "it says so once");
            Assert.That(DoorOf("a").IsOpen, Is.True, "and the doors open");
            Assert.That(DoorOf("b").IsOpen, Is.True);
            fight.Begin();
            Assert.That(fight.State, Is.EqualTo(EncounterState.Cleared), "a won fight does not begin again");
        }

        [UnityTest]
        public IEnumerator IfHeDiesItIsPutBackAndCanBeginAgain()
        {
            var piece = PutFight();
            yield return Begin(new Vector2(8f, Floor));
            var fight = Built(piece).GetComponent<Encounter>();
            fight.Begin();
            yield return probe.Until(() => fight.Foes.Count == 2, 60);
            Assert.That(fight.Foes, Has.Count.EqualTo(2));
            var first = fight.Foes[0];

            hero.Kill();
            yield return probe.Until(() => fight.State == EncounterState.Waiting, 5);
            Assert.That(fight.State, Is.EqualTo(EncounterState.Waiting), "he died: it is put back");
            Assert.That(fight.Foes, Is.Empty, "its foes are gone");
            Assert.That(DoorOf("a").IsOpen, Is.True, "and the doors are open");
            yield return probe.Step(2);
            Assert.That(first == null, Is.True);

            hero.Respawn(new Vector2(8f, Floor));
            yield return probe.Step(2);
            fight.Begin();
            Assert.That(fight.State, Is.EqualTo(EncounterState.Running), "it begins again");
            Assert.That(fight.Wave, Is.EqualTo(0), "from the first wave");
            Assert.That(DoorOf("a").IsOpen, Is.False);
            yield return probe.Until(() => fight.Foes.Count == 2, 60);
            Assert.That(fight.Alive, Is.EqualTo(2));
        }
    }
}
