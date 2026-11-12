using System;
using System.Collections.Generic;
using Descender.Fx;
using Descender.Player;
using Descender.Triggers;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>One foe of a wave: what it is, where its feet go (world units) and its body's size in units (zero for the recipe's own).</summary>
    public sealed class FoeSpawn
    {
        public FoeRecipe recipe;
        public Vector2 feet;
        public Vector2 size;
    }

    /// <summary>The foes that come together.</summary>
    public sealed class EncounterWave
    {
        public readonly List<FoeSpawn> foes = new List<FoeSpawn>();

        /// <summary>One more foe in the wave; returns the wave, to add the next.</summary>
        public EncounterWave Add(FoeRecipe recipe, Vector2 feet, Vector2 size = default)
        {
            var spawn = new FoeSpawn();
            spawn.recipe = recipe;
            spawn.feet = feet;
            spawn.size = size;
            foes.Add(spawn);
            return this;
        }
    }

    public enum EncounterState
    {
        /// <summary>Not begun, or put back after the hero died.</summary>
        Waiting,

        /// <summary>The doors are shut and a wave is warned of or alive.</summary>
        Running,

        /// <summary>Every wave is dead and the doors are open again.</summary>
        Cleared,
    }

    /// <summary>
    /// A fight in a room: waves of foes at points, each wave warned of by marks where its
    /// foes will stand, and coming when the wave before is dead. Its doors shut when it
    /// begins and open when the last wave is dead. If the hero dies it puts itself back:
    /// the foes go, the doors open, and it can begin again. With no doors it is a plain
    /// spawner. A trigger begins it (<see cref="StartEncounter"/>).
    /// </summary>
    public sealed class Encounter : MonoBehaviour
    {
        /// <summary>How long the marks show before a wave's foes appear.</summary>
        public const float DefaultWarning = 0.7f;
        public static readonly Color WarningColor = new Color(1f, 0.75f, 0.3f, 0.35f);

        private readonly List<EncounterWave> waves = new List<EncounterWave>();
        private readonly List<Door> doors = new List<Door>();
        private readonly List<Foe> foes = new List<Foe>();
        private readonly List<GameObject> marks = new List<GameObject>();
        private float warning = DefaultWarning;
        private float left;

        public EncounterState State { get; private set; }

        /// <summary>The wave that is warned of or alive, from 0; the number of waves once cleared.</summary>
        public int Wave { get; private set; }

        public int Waves => waves.Count;

        /// <summary>True while the marks of the next wave show and its foes have not come.</summary>
        public bool IsWarning { get; private set; }

        /// <summary>The foes of the wave that is out, dead ones included.</summary>
        public IReadOnlyList<Foe> Foes => foes;

        /// <summary>How many of them are alive.</summary>
        public int Alive
        {
            get
            {
                var count = 0;
                foreach (var foe in foes)
                {
                    if (foe != null && !foe.IsDead)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Every wave is dead.</summary>
        public event Action Cleared;

        /// <summary>Its waves in order, the doors it shuts (left open until it begins), and how long each wave is warned of.</summary>
        public void Configure(IEnumerable<EncounterWave> inOrder, IEnumerable<Door> shuts, float warns = DefaultWarning)
        {
            waves.Clear();
            doors.Clear();
            if (inOrder != null)
            {
                waves.AddRange(inOrder);
            }
            if (shuts != null)
            {
                foreach (var door in shuts)
                {
                    if (door != null)
                    {
                        doors.Add(door);
                        door.Open();
                    }
                }
            }
            warning = Mathf.Max(warns, 0f);
        }

        /// <summary>Shuts the doors and warns of the first wave. Nothing while it runs or once it is cleared.</summary>
        public void Begin()
        {
            if (State != EncounterState.Waiting)
            {
                return;
            }
            State = EncounterState.Running;
            SetDoors(false);
            Wave = 0;
            Announce();
        }

        private void FixedUpdate()
        {
            if (State != EncounterState.Running)
            {
                return;
            }
            var hero = PlayerController.Current;
            if (hero != null && !hero.IsAlive)
            {
                PutBack();
                return;
            }
            if (IsWarning)
            {
                left -= Time.fixedDeltaTime;
                if (left <= 0f)
                {
                    Release();
                }
                return;
            }
            if (Alive == 0)
            {
                Wave++;
                Announce();
            }
        }

        /// <summary>Marks where the wave's foes will stand; with no wave left, the fight is won.</summary>
        private void Announce()
        {
            Clear();
            if (Wave >= waves.Count)
            {
                State = EncounterState.Cleared;
                SetDoors(true);
                Cleared?.Invoke();
                return;
            }
            IsWarning = true;
            left = warning;
            foreach (var spawn in waves[Wave].foes)
            {
                var size = SizeOf(spawn);
                var mark = Shape.Make(transform, "Mark", ShapeKind.Square, Vector2.zero, size, WarningColor, "!", 1);
                mark.transform.position = CentreOf(spawn, size);
                marks.Add(mark.gameObject);
            }
        }

        private void Release()
        {
            IsWarning = false;
            foreach (var mark in marks)
            {
                Destroy(mark);
            }
            marks.Clear();
            foreach (var spawn in waves[Wave].foes)
            {
                var size = SizeOf(spawn);
                foes.Add(Foe.Make(spawn.recipe, transform, CentreOf(spawn, size), size));
            }
        }

        /// <summary>As it was before it began: no foes, no marks, the doors open.</summary>
        private void PutBack()
        {
            Clear();
            SetDoors(true);
            State = EncounterState.Waiting;
        }

        private void Clear()
        {
            IsWarning = false;
            foreach (var mark in marks)
            {
                Destroy(mark);
            }
            marks.Clear();
            foreach (var foe in foes)
            {
                if (foe != null)
                {
                    Destroy(foe.gameObject);
                }
            }
            foes.Clear();
        }

        private void SetDoors(bool open)
        {
            foreach (var door in doors)
            {
                if (door != null)
                {
                    door.Set(open);
                }
            }
        }

        private static Vector2 SizeOf(FoeSpawn spawn)
        {
            var recipe = spawn.recipe ?? new FoeRecipe();
            return spawn.size.sqrMagnitude > 0f ? spawn.size : recipe.body.size / EnemyPhysics.Ppu;
        }

        /// <summary>Its middle, a pixel clear of the floor its feet are given.</summary>
        private static Vector2 CentreOf(FoeSpawn spawn, Vector2 size)
        {
            return spawn.feet + new Vector2(0f, size.y / 2f + 1f / EnemyPhysics.Ppu);
        }
    }
}
