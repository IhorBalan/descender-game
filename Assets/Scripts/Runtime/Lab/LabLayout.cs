using System.Collections.Generic;
using Descender.Player;
using Descender.Save;
using UnityEngine;

namespace Descender.Lab
{
    public enum PieceKind
    {
        /// <summary>Solid world: a floor, a wall, a block.</summary>
        Solid,

        /// <summary>A one-way ledge: jumped up through, dropped down through.</summary>
        Ledge,

        /// <summary>A line with a label, drawn behind the play layer; no collider.</summary>
        Mark,

        /// <summary>A wall of light only the light dash passes.</summary>
        Barrier,

        /// <summary>A bounce pod that throws the hero `value` tiles up.</summary>
        Pod,

        /// <summary>Spikes: hurt and put the hero back on safe ground.</summary>
        Hazard,

        /// <summary>A pad that takes the hero to station number `value`.</summary>
        Pad,
    }

    /// <summary>One thing in a station, as a rectangle in world units (x and y are its lower left corner).</summary>
    public sealed class Piece
    {
        public PieceKind kind;
        public Rect rect;
        public string label = "";
        public float value;
    }

    /// <summary>A room of the lab where one mechanic is tried.</summary>
    public sealed class Station
    {
        public string id = "";
        public string title = "";

        /// <summary>The room, walls included, in world units.</summary>
        public Rect room;

        /// <summary>Where the hero's feet go.</summary>
        public Vector2 spawn;

        /// <summary>The abilities the hero has here, and no others.</summary>
        public Ability[] abilities = new Ability[0];

        public readonly List<Piece> pieces = new List<Piece>();
    }

    /// <summary>
    /// The mechanics lab as data: a row of rooms, one per mechanic, made of simple pieces
    /// whose sizes come from <see cref="PlayerTuning"/>, so a station always asks for what
    /// the tuned move can just do. <see cref="LabBuilder"/> turns it into objects. A new
    /// station is one more method here and one more line in <see cref="Make"/>.
    /// </summary>
    public static class LabLayout
    {
        /// <summary>A room is this big, walls included; the screen is 20 by 11.25.</summary>
        public const float RoomWidth = 56f;
        public const float RoomHeight = 30f;

        /// <summary>From one room's left wall to the next room's.</summary>
        public const float Pitch = 70f;

        /// <summary>The floor is this thick and so are the walls.</summary>
        public const float Wall = 2f;

        /// <summary>Where the hero stands when he arrives, from the room's left edge.</summary>
        public const float SpawnX = 8f;

        /// <summary>The station ids in order, for the console; <see cref="Make"/> builds exactly these.</summary>
        public static readonly string[] Ids =
        {
            "run",
            "jump",
            "gaps",
            "wings",
            "dash",
            "wall",
            "glide",
            "ledges",
            "slam",
            "pods",
            "spikes",
        };

        public static List<Station> Make(PlayerTuning tuning)
        {
            var jump = PlayerTuning.Units(tuning.jumpHeight);
            var second = PlayerTuning.Units(tuning.doubleJumpHeight);
            var run = PlayerTuning.Units(tuning.runSpeed);
            // How far a full jump carries at run speed, take-off to landing at the same height.
            var leap = run * (tuning.jumpTimeToApex + tuning.jumpTimeToLand);
            var dash = PlayerTuning.Units(tuning.dashSpeed) * tuning.dashTime;
            var lightDash = PlayerTuning.Units(tuning.dashSpeed) * tuning.lightDashTime;

            var stations = new List<Station>();
            Run(Open(stations, "run", "Run and stop"), run);
            Jump(Open(stations, "jump", "Jump: tap and hold"), jump);
            Gaps(Open(stations, "gaps", "Gaps: coyote time and the buffer"), leap);
            Wings(Open(stations, "wings", "Wings: the double jump", Ability.Wings), jump, second, leap);
            Dash(Open(stations, "dash", "Dash and light dash", Ability.LightDash), leap, dash, lightDash);
            Shaft(Open(stations, "wall", "Wall grip and wall jump", Ability.WallGrip));
            Glide(Open(stations, "glide", "Glide", Ability.Wings, Ability.Glide));
            Ledges(Open(stations, "ledges", "Ledges: up through, down through"), jump);
            Slam(Open(stations, "slam", "Smite: the slam", Ability.Smite), jump);
            Pods(Open(stations, "pods", "Bounce pods"));
            Spikes(Open(stations, "spikes", "Spikes and safe ground"));
            for (var i = 0; i < stations.Count; i++)
            {
                Pads(stations, i);
            }
            return stations;
        }

        /// <summary>The station with an id, or null.</summary>
        public static Station Find(List<Station> stations, string id)
        {
            return stations.Find(s => s.id == id);
        }

        /// <summary>The y of the floor's top in every room.</summary>
        public const float FloorTop = Wall;

        /// <summary>A new empty room with its floor, walls and ceiling, after the ones already made.</summary>
        private static Station Open(List<Station> stations, string id, string title, params Ability[] abilities)
        {
            var left = stations.Count * Pitch;
            var station = new Station
            {
                id = id,
                title = title,
                room = new Rect(left, 0f, RoomWidth, RoomHeight),
                spawn = new Vector2(left + SpawnX, FloorTop),
                abilities = abilities,
            };
            Add(station, PieceKind.Solid, 0f, 0f, RoomWidth, Wall, "floor");
            Add(station, PieceKind.Solid, 0f, RoomHeight - Wall, RoomWidth, Wall, "ceiling");
            Add(station, PieceKind.Solid, 0f, Wall, Wall, RoomHeight - 2f * Wall, "left wall");
            Add(station, PieceKind.Solid, RoomWidth - Wall, Wall, Wall, RoomHeight - 2f * Wall, "right wall");
            Add(station, PieceKind.Mark, SpawnX - 4f, FloorTop + 8f, 8f, 0.1f, title);
            stations.Add(station);
            return station;
        }

        /// <summary>A piece at (x, y) from the room's lower left corner; y for things on the floor is FloorTop.</summary>
        private static Piece Add(
            Station station,
            PieceKind kind,
            float x,
            float y,
            float width,
            float height,
            string label = "",
            float value = 0f
        )
        {
            var piece = new Piece
            {
                kind = kind,
                rect = new Rect(station.room.x + x, station.room.y + y, width, height),
                label = label,
                value = value,
            };
            station.pieces.Add(piece);
            return piece;
        }

        /// <summary>A line across the back of the room at a height above the floor, with what it marks.</summary>
        private static void Line(Station station, float x, float above, float width, string label)
        {
            Add(station, PieceKind.Mark, x, FloorTop + above, width, 0.1f, label);
        }

        private static void Run(Station station, float run)
        {
            // A ruler: a mark every second of running.
            for (var i = 1; i <= 6; i++)
            {
                var x = SpawnX + i * run;
                if (x < RoomWidth - Wall - 1f)
                {
                    Add(station, PieceKind.Mark, x, FloorTop, 0.1f, 1.5f, $"{i} s");
                }
            }
        }

        private static void Jump(Station station, float jump)
        {
            for (var h = 1; h < jump; h++)
            {
                Line(station, 12f, h, 6f, $"{h}");
            }
            Line(station, 12f, jump, 6f, $"full jump {jump:0.#}");
            // A step just under a full jump, and one just over: the first is made, the second is not.
            Add(station, PieceKind.Solid, 22f, FloorTop, 6f, jump - 0.5f, $"{jump - 0.5f:0.#} high");
            Add(station, PieceKind.Solid, 34f, FloorTop, 6f, jump + 0.5f, $"{jump + 0.5f:0.#} high: too high");
        }

        private static void Gaps(Station station, float leap)
        {
            // Pits two deep (a jump gets out) and wider each time; the last is a full leap, which
            // needs the take-off at the very edge.
            var x = 14f;
            foreach (var share in new[] { 0.5f, 0.75f, 1f })
            {
                var width = Mathf.Round(leap * share * 2f) / 2f;
                Pit(station, x, width, $"{width:0.#} wide");
                x += width + 7f;
            }
        }

        /// <summary>A pit cut into a raised floor: the floor is raised two units on both sides of it.</summary>
        private static void Pit(Station station, float x, float width, string label)
        {
            Add(station, PieceKind.Solid, x - 3f, FloorTop, 3f, 2f);
            Add(station, PieceKind.Solid, x + width, FloorTop, 3f, 2f);
            Add(station, PieceKind.Mark, x, FloorTop + 2f, width, 0.1f, label);
        }

        private static void Wings(Station station, float jump, float second, float leap)
        {
            Line(station, 12f, jump, 5f, $"jump {jump:0.#}");
            Line(station, 12f, jump + second, 5f, $"jump and Wings {jump + second:0.#}");
            // A wall only the double jump clears, then a pit only the double jump crosses.
            var wall = jump + second - 1f;
            Add(station, PieceKind.Solid, 20f, FloorTop, 3f, wall, $"{wall:0.#} high");
            var width = Mathf.Round(leap * 1.4f);
            Pit(station, 32f, width, $"{width:0.#} wide");
        }

        private static void Dash(Station station, float leap, float dash, float lightDash)
        {
            Add(station, PieceKind.Mark, SpawnX + dash, FloorTop, 0.1f, 1.5f, $"dash {dash:0.#}");
            Add(station, PieceKind.Mark, SpawnX + lightDash, FloorTop, 0.1f, 2.5f, $"light dash {lightDash:0.#}");
            // A pit a jump alone does not cross and a jump with a dash does.
            var width = Mathf.Round(leap + dash * 0.75f);
            Pit(station, 22f, width, $"{width:0.#} wide: jump, then dash");
            // A wall of light, floor to ceiling.
            Add(station, PieceKind.Barrier, 42f, FloorTop, 1f, RoomHeight - 2f * Wall, "light: dash through");
        }

        private static void Shaft(Station station)
        {
            // A shaft four wide between two tall blocks, with the way out at the top.
            var height = RoomHeight - 2f * Wall - 6f;
            Add(station, PieceKind.Solid, 16f, FloorTop + 4f, 4f, height - 4f, "grip and jump");
            Add(station, PieceKind.Solid, 24f, FloorTop, 4f, height);
            Add(station, PieceKind.Mark, 28f, FloorTop + height, 8f, 0.1f, "out here");
            Add(station, PieceKind.Solid, 28f, FloorTop, 10f, height);
        }

        private static void Glide(Station station)
        {
            // He starts on a tower; the far shelf is reached only by holding the glide.
            var top = RoomHeight - 2f * Wall - 8f;
            Add(station, PieceKind.Solid, Wall, FloorTop, 10f, top, "jump off, hold jump");
            station.spawn = new Vector2(station.room.x + SpawnX, FloorTop + top);
            Add(station, PieceKind.Solid, 44f, FloorTop, 8f, 6f, "land here");
            Add(station, PieceKind.Hazard, 12f, FloorTop, 32f, 0.5f, "spikes");
        }

        private static void Ledges(Station station, float jump)
        {
            // A ladder of ledges a jump apart, less a little.
            var step = jump - 1f;
            for (var i = 1; i <= 5; i++)
            {
                var x = i % 2 == 0 ? 14f : 20f;
                Add(station, PieceKind.Ledge, x, FloorTop + i * step, 8f, 0.25f, i == 1 ? "down and jump drops" : "");
            }
        }

        private static void Slam(Station station, float jump)
        {
            Add(station, PieceKind.Solid, 16f, FloorTop, 6f, jump - 0.5f, "jump off, down and attack");
            Add(station, PieceKind.Mark, 24f, FloorTop, 8f, 0.1f, "slam here");
        }

        private static void Pods(Station station)
        {
            var x = 14f;
            foreach (var tiles in new[] { 6f, 10f, 16f })
            {
                Add(station, PieceKind.Pod, x, FloorTop, 3f, 1f, $"pod {tiles:0}", tiles);
                // A shelf a tile under the top of the throw, to land on.
                Add(station, PieceKind.Ledge, x + 4f, FloorTop + tiles - 1f, 5f, 0.25f, $"{tiles - 1f:0} up");
                x += 12f;
            }
        }

        private static void Spikes(Station station)
        {
            Add(station, PieceKind.Hazard, 14f, FloorTop, 4f, 0.5f, "spikes");
            Add(station, PieceKind.Hazard, 26f, FloorTop, 8f, 0.5f, "spikes: too wide to jump");
            Add(station, PieceKind.Ledge, 27f, FloorTop + 3f, 6f, 0.25f);
        }

        /// <summary>The pads to the neighbouring stations, by the walls.</summary>
        private static void Pads(List<Station> stations, int index)
        {
            var station = stations[index];
            var floor = station.spawn.y - station.room.y;
            if (index > 0)
            {
                var back = stations[index - 1];
                Add(station, PieceKind.Pad, Wall + 0.5f, floor, 1.5f, 2f, "back: " + back.id, index - 1);
            }
            if (index < stations.Count - 1)
            {
                var next = stations[index + 1];
                var x = RoomWidth - Wall - 2f;
                Add(station, PieceKind.Pad, x, FloorTop, 1.5f, 2f, "next: " + next.id, index + 1);
            }
        }
    }
}
