using System;
using System.Collections.Generic;
using Descender.Fx;
using Descender.Player;
using Descender.Save;
using Descender.Triggers;
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

        /// <summary>A <see cref="TriggerZone"/>: an item, a switch, a lock, drawn as its shape.</summary>
        Zone,

        /// <summary>A door: solid until a trigger opens it.</summary>
        Door,

        /// <summary>A ledge on a <see cref="Mover"/>: it travels by `travel` and back once started.</summary>
        Lift,

        /// <summary>A camera zone: while the hero is in it the view takes its framing, zoom and held axes.</summary>
        Camera,

        /// <summary>A solid block on a <see cref="Mover"/>: ridden like a lift, and it pushes and crushes.</summary>
        Platform,

        /// <summary>A <see cref="Triggers.Crumble"/>: holds for `solidFor` once stood on, gone for `goneFor`.</summary>
        Crumble,

        /// <summary>A <see cref="Triggers.Blinker"/>: solid for `solidFor`, gone for `goneFor`, in turn.</summary>
        Blinker,

        /// <summary>A <see cref="ForceZone"/>: an area whose `wind` is added to the hero's speed.</summary>
        Wind,

        /// <summary>
        /// A dummy that hits back (<see cref="Combat.Striker"/>): the hero inside `reach` around it is
        /// hurt every `value` seconds.
        /// </summary>
        Striker,

        /// <summary>
        /// A <see cref="Triggers.Breakable"/>: a solid block that `value` strikes of the kind in `by`
        /// break; its `actions` run when it does.
        /// </summary>
        Breakable,

        /// <summary>An enemy made of parts (<see cref="Enemies.Foe"/>) from the recipe in `foe`; the rect is its body.</summary>
        Foe,

        /// <summary>
        /// A fight (<see cref="Enemies.Encounter"/>): its `waves` come in order, and the doors
        /// named in `doors` shut while it runs. A zone's action begins it.
        /// </summary>
        Encounter,

        /// <summary>A <see cref="Climbable"/>: a ladder or a vine the hero takes hold of with up or down.</summary>
        Climb,

        /// <summary>A <see cref="Triggers.Water"/>: an area he swims in; its top is the surface.</summary>
        Water,

        /// <summary>A <see cref="Combat.Dummy"/>: takes every hit and shows what hit it.</summary>
        Dummy,
    }

    /// <summary>One thing in a station, as a rectangle in world units (x and y are its lower left corner).</summary>
    public sealed class Piece
    {
        public PieceKind kind;
        public Rect rect;
        public string label = "";
        public float value;

        /// <summary>A name other pieces of the station find this one by; empty for none.</summary>
        public string id = "";

        /// <summary>Starts switched off, for a trigger to switch on.</summary>
        public bool hidden;

        /// <summary>A zone's shape and colour; a clear colour draws nothing.</summary>
        public ShapeKind shape = ShapeKind.Square;
        public Color color = Color.clear;

        /// <summary>A zone: what fires it, whether once, whether it disappears, and what it asks for.</summary>
        public TriggerEvent on = TriggerEvent.Enter;
        public bool once = true;
        public bool hide;
        public TriggerNeeds needs = new TriggerNeeds();

        /// <summary>A zone that waits to be hit: the strike it answers to.</summary>
        public StruckBy by = StruckBy.Any;

        /// <summary>A zone's actions, made when the station is built; the argument finds a built piece by id.</summary>
        public Func<Func<string, GameObject>, TriggerAction[]> actions;

        /// <summary>
        /// A lift or a platform: where it goes from where it starts, how fast in units a
        /// second (0 for the lab's usual speed), and whether it runs from the start.
        /// </summary>
        public Vector2 travel;
        public float speed;
        public bool running;

        /// <summary>
        /// A crumbling or blinking platform: seconds it holds (once stood on, or each
        /// cycle), seconds it is gone, seconds of warning before a blinker changes, and how
        /// far along its cycle a blinker starts. A hidden one is gone until switched on.
        /// </summary>
        public float solidFor = 1f;
        public float goneFor = 2f;
        public float warnFor = 0.4f;
        public float ahead;

        /// <summary>
        /// A wind: the speed it adds in level pixels a second, how fast he picks it up
        /// (pixels a second squared, 0 for at once), and its gusts: seconds blowing and
        /// seconds resting (0 for a steady wind). A hidden one is still until switched on.
        /// </summary>
        public Vector2 wind;
        public float gain = 600f;
        public float gustOn;
        public float gustOff;

        /// <summary>A foe: what it is made of. Its body's size is the piece's rect.</summary>
        public Enemies.FoeRecipe foe;

        /// <summary>An encounter: its waves in order (feet in world units) and the ids of the doors it shuts.</summary>
        public List<Enemies.EncounterWave> waves;
        public string[] doors = new string[0];

        /// <summary>A striker: the box around its middle it hits the hero in, in units.</summary>
        public Vector2 reach;

        /// <summary>A camera zone: the framing offset, the zoom (in `value`) and the axes it holds.</summary>
        public Vector2 framing;
        public bool lockX;
        public bool lockY;
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

        /// <summary>The hero catches ledges here: the trial of the ledge grab.</summary>
        public bool ledgeGrab;

        /// <summary>Its pieces change in play (pickups, doors), so it is built again each time it is entered.</summary>
        public bool rebuilt;

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
            "items",
            "camera",
            "platforms",
            "corners",
            "timing",
            "wind",
            "climb",
            "water",
            "grab",
            "dummy",
            "sword",
            "light",
            "hurt",
            "break",
            "foes",
            "chase",
            "air",
            "guard",
            "arena",
            "boss",
            "habits",
            "gauntlet",
        };

        /// <summary>The key of the items station.</summary>
        public const string Key = "lab_key";

        public const string LockedText = "Locked. The key is over the wall.";
        public const string UnlockedText = "The key turns.";

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
            ItemsAndTriggers(Open(stations, "items", "Items and triggers"), jump, second);
            CameraZones(Open(stations, "camera", "Camera zones"));
            Platforms(Open(stations, "platforms", "Platforms to ride"));
            Corners(Open(stations, "corners", "Corners a jump forgives"), jump, tuning.cornerCorrection);
            Timing(Open(stations, "timing", "Platforms that do not stay"));
            Wind(Open(stations, "wind", "Wind and lift", Ability.Glide), leap, tuning);
            Climb(Open(stations, "climb", "Ladders and vines"), jump);
            Water(Open(stations, "water", "Water to swim in"), jump);
            Grab(Open(stations, "grab", "Ledge grab: a trial"), jump);
            var fighter = new[] { Ability.Wings, Ability.HolyLight, Ability.Smite };
            Dummies(Open(stations, "dummy", "Training dummies", fighter), jump, tuning);
            Sword(Open(stations, "sword", "The sword: up, down, the bounce and the combo"), tuning);
            Light(Open(stations, "light", "Holy Light and Smite", Ability.HolyLight, Ability.Smite), jump, tuning);
            Hurt(Open(stations, "hurt", "Being hurt and healing"), tuning);
            Break(Open(stations, "break", "Things that break", Ability.HolyLight, Ability.Smite), jump);
            Foes(Open(stations, "foes", "Foes: a body, a patrol, a touch"));
            Chase(Open(stations, "chase", "Foes that come for you: the warning, then the swing"));
            Air(Open(stations, "air", "Foes that shoot and dive"));
            Guard(Open(stations, "guard", "Shields and summoners", Ability.HolyLight, Ability.Smite));
            Arena(Open(stations, "arena", "The arena: three waves behind shut doors", fighter));
            Boss(Open(stations, "boss", "A boss in three phases", fighter));
            Habits(Open(stations, "habits", "The old habits: the pounce, the lunge, the hurl and the quake", fighter));
            var runner = new[] { Ability.Wings, Ability.LightDash, Ability.HolyLight, Ability.Smite };
            var gauntlet = OpenWide(stations, "gauntlet", "The gauntlet: everything in one run", GauntletWidth, runner);
            Gauntlet(gauntlet, jump, second);
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
            return OpenWide(stations, id, title, RoomWidth, abilities);
        }

        /// <summary>
        /// A room of another width. The rooms stand a fixed pitch apart, so one wider than
        /// the pitch allows has to be the last.
        /// </summary>
        private static Station OpenWide(
            List<Station> stations,
            string id,
            string title,
            float width,
            params Ability[] abilities
        )
        {
            var left = stations.Count * Pitch;
            var station = new Station
            {
                id = id,
                title = title,
                room = new Rect(left, 0f, width, RoomHeight),
                spawn = new Vector2(left + SpawnX, FloorTop),
                abilities = abilities,
            };
            Add(station, PieceKind.Solid, 0f, 0f, width, Wall, "floor");
            Add(station, PieceKind.Solid, 0f, RoomHeight - Wall, width, Wall, "ceiling");
            Add(station, PieceKind.Solid, 0f, Wall, Wall, RoomHeight - 2f * Wall, "left wall");
            Add(station, PieceKind.Solid, width - Wall, Wall, Wall, RoomHeight - 2f * Wall, "right wall");
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

        /// <summary>
        /// Dummies that take every hit and show its damage: one to stand in front of, one
        /// overhead that only a swing in the air reaches, one down a lane for the bolt, and
        /// a pair a blade's length apart, the nearer in reach and the farther just out of it.
        /// </summary>
        private static void Dummies(Station station, float jump, PlayerTuning tuning)
        {
            Add(station, PieceKind.Dummy, 13f, FloorTop, 1f, 2f, "stand and swing");
            Add(station, PieceKind.Dummy, 18f, FloorTop + Mathf.Floor(jump), 1f, 2f, "jump and swing");
            Add(station, PieceKind.Solid, 22f, FloorTop, 4f, Mathf.Floor(jump) - 1f, "slam from here");
            Add(station, PieceKind.Dummy, 27f, FloorTop, 1f, 2f, "slam beside it");

            // The blade's far edge from his middle, in units.
            var reach = PlayerTuning.Units(tuning.attackOffset.x + tuning.attackShapeSize.x / 2f);
            Add(station, PieceKind.Mark, 34f, FloorTop, 0.1f, 3f, "stand here");
            Add(station, PieceKind.Dummy, 34f + reach - 0.5f, FloorTop, 0.5f, 2f, "in reach");
            Add(station, PieceKind.Dummy, 34f + reach + 0.25f, FloorTop, 0.5f, 2f, "out of reach");
            Add(station, PieceKind.Dummy, 48f, FloorTop, 1f, 2f, "cast from afar");
        }

        /// <summary>
        /// The sword's other two cuts, in a room without the slam so down and attack is
        /// the cut: a dummy overhead that only the upward cut reaches from the ground, and
        /// a row of dummies over spikes to cross by bouncing off each with a downward cut.
        /// </summary>
        private static void Sword(Station station, PlayerTuning tuning)
        {
            // Above the cut ahead, inside the cut upward.
            var ahead = PlayerTuning.Units(tuning.attackOffset.y + tuning.attackShapeSize.y / 2f);
            Add(station, PieceKind.Dummy, 13f, FloorTop + ahead + 0.5f, 1f, 1f, "up and attack");

            // Three cuts in a row: the read-out shows the last one's damage and how far it was pushed.
            Add(station, PieceKind.Dummy, 15f, FloorTop, 1f, 2f, "three cuts: the last is the heavy one");

            // As far apart as a bounce carries him at a run.
            var bounce = PlayerTuning.Units(tuning.pogoHeight);
            var air = tuning.PogoVelocity / tuning.Gravity + Mathf.Sqrt(2f * tuning.pogoHeight / tuning.FallGravity);
            var step = Mathf.Max(Mathf.Floor(PlayerTuning.Units(tuning.runSpeed) * air * 0.8f), 2f);
            Add(station, PieceKind.Solid, 18f, FloorTop, 3f, 2f, "jump off: down and attack");
            Add(station, PieceKind.Hazard, 21f, FloorTop, 3f * step + 2f, 0.5f, "spikes");
            for (var i = 0; i < 3; i++)
            {
                var label = i == 0 ? $"bounce {bounce:0.#} up" : "";
                Add(station, PieceKind.Dummy, 21f + step * (i + 0.75f), FloorTop + 0.5f, 1f, 1f, label);
            }
            Add(station, PieceKind.Solid, 23f + 3f * step, FloorTop, 4f, 2f, "land here");
        }

        /// <summary>
        /// The bolt and the slam by their numbers: a dummy at the end of the bolt's range
        /// with a mark in range and one out of it, dummies just inside and just outside the
        /// slam's shock, a target
        /// only a bolt sets off and a plate only the slam does, each opening a door.
        /// </summary>
        private static void Light(Station station, float jump, PlayerTuning tuning)
        {
            station.rebuilt = true;

            // From the mark the bolt leaves his hand and flies its range; its box is ten pixels long.
            var front = tuning.castOffset.x + tuning.boltRange + Combat.LightBolt.Size.x / 2f;
            var reach = PlayerTuning.Units(front);
            Add(station, PieceKind.Mark, 9.5f, FloorTop, 0.1f, 2f, "out of range");
            Add(station, PieceKind.Mark, 11f, FloorTop, 0.1f, 3f, "in range");
            Add(station, PieceKind.Dummy, 11f + reach - 0.5f, FloorTop, 0.5f, 2f, "cast from the marks");

            // A target over spikes: only a bolt reaches it, and only a bolt sets it off.
            var pale = new Color(1f, 0.95f, 0.7f);
            Add(station, PieceKind.Hazard, 25f, FloorTop, 4f, 0.5f, "spikes");
            var target = Zone(station, 27f, FloorTop + 0.75f, 0.75f, 1f, ShapeKind.Diamond, pale, "bolt only");
            target.id = "target";
            target.on = TriggerEvent.Hit;
            target.by = StruckBy.Bolt;
            target.once = false;
            Add(station, PieceKind.Solid, 31f, FloorTop + 4f, 1f, RoomHeight - 2f * Wall - 4f);
            Add(station, PieceKind.Door, 31f, FloorTop, 1f, 4f, "opens to a bolt").id = "first";
            target.actions = find => List(Do.Door(find("first").GetComponent<Door>(), SwitchTo.Toggle));

            // A plate on the floor under a block to jump from: only the slam sets it off.
            var half = PlayerTuning.Units(tuning.slamReach.x / 2f);
            var violet = new Color(0.7f, 0.5f, 0.95f);
            Add(station, PieceKind.Solid, 34f, FloorTop, 3f, Mathf.Floor(jump) - 1f, "jump off, down and attack");
            var plate = Zone(station, 38f, FloorTop, 2f, 0.25f, ShapeKind.Square, violet, "slam only");
            plate.id = "plate";
            plate.on = TriggerEvent.Hit;
            plate.by = StruckBy.Slam;
            plate.once = false;
            Add(station, PieceKind.Dummy, 39f + half - 0.5f, FloorTop, 0.5f, 2f, "in the shock");
            Add(station, PieceKind.Dummy, 39f + half + 0.5f, FloorTop, 0.5f, 2f, "out of it");
            Add(station, PieceKind.Solid, 46f, FloorTop + 4f, 1f, RoomHeight - 2f * Wall - 4f);
            Add(station, PieceKind.Door, 46f, FloorTop, 1f, 4f, "opens to a slam").id = "second";
            plate.actions = find => List(Do.Door(find("second").GetComponent<Door>(), SwitchTo.Toggle));
        }

        /// <summary>
        /// Health by its rules: a dummy that hits back every second and a half inside the
        /// box drawn around it, spikes that cost a heart and put him back, and a quiet
        /// corner to heal in.
        /// </summary>
        private static void Hurt(Station station, PlayerTuning tuning)
        {
            var striker = Add(station, PieceKind.Striker, 16f, FloorTop, 1f, 2f, "hits back", 1.5f);
            striker.id = "striker";
            striker.reach = new Vector2(6f, 4f);
            var safe = $"safe for {tuning.invulnerableTime:0.#} s after a hit";
            Add(station, PieceKind.Mark, 13f, FloorTop + 5f, 6f, 0.1f, safe);

            Add(station, PieceKind.Hazard, 26f, FloorTop, 4f, 0.5f, "spikes: a heart, and back you go");

            var heal = $"stand still and heal: {tuning.healTime:0.#} s, a hit stops it";
            Add(station, PieceKind.Mark, 36f, FloorTop, 6f, 0.1f, heal);
        }

        /// <summary>
        /// Blocks to break: one the sword breaks in three cuts and that leaves some light,
        /// one only the slam breaks and that leaves a heart, and one only a bolt breaks,
        /// over spikes, that opens the door.
        /// </summary>
        private static void Break(Station station, float jump)
        {
            station.rebuilt = true;
            var pale = new Color(1f, 0.95f, 0.7f);
            var pink = new Color(1f, 0.6f, 0.9f);

            var light = Zone(station, 14.25f, FloorTop + 0.5f, 0.5f, 0.5f, ShapeKind.Square, pale, "light");
            light.id = "light";
            light.hide = true;
            light.hidden = true;
            light.actions = find => List(Do.Currency("light", 5));
            var cut = Add(station, PieceKind.Breakable, 14f, FloorTop, 1f, 2f, "three cuts", 3f);
            cut.id = "cut";
            cut.actions = find => List(Do.Active(find("light"), SwitchTo.On));

            Add(station, PieceKind.Solid, 19f, FloorTop, 3f, Mathf.Floor(jump) - 1f, "jump off, down and attack");
            var heart = Zone(station, 24f, FloorTop + 0.5f, 1f, 1f, ShapeKind.Circle, pink, "heart");
            heart.id = "heart";
            heart.hide = true;
            heart.hidden = true;
            heart.actions = find => List(Do.Upgrade(UpgradeKind.Heart, ""));
            var slab = Add(station, PieceKind.Breakable, 24f, FloorTop, 1f, 1.5f, "slam beside it", 1f);
            slab.id = "slab";
            slab.by = StruckBy.Slam;
            slab.actions = find => List(Do.Active(find("heart"), SwitchTo.On));

            Add(station, PieceKind.Hazard, 31f, FloorTop, 5f, 0.5f, "spikes");
            var far = Add(station, PieceKind.Breakable, 34f, FloorTop + 0.5f, 1f, 1.5f, "bolt only", 1f);
            far.id = "far";
            far.by = StruckBy.Bolt;
            Add(station, PieceKind.Solid, 40f, FloorTop + 4f, 1f, RoomHeight - 2f * Wall - 4f);
            Add(station, PieceKind.Door, 40f, FloorTop, 1f, 4f, "opens when it breaks").id = "door";
            far.actions = find => List(Do.Door(find("door").GetComponent<Door>(), SwitchTo.On));
        }

        /// <summary>
        /// The first foes, in shapes: one that stands, one that walks a platform and turns
        /// at its edges, one that walks the floor between two walls, and a heavy one a cut
        /// barely moves.
        /// </summary>
        private static void Foes(Station station)
        {
            station.rebuilt = true;
            var still = new Enemies.FoeBody { health = 3, color = new Color(0.8f, 0.45f, 0.4f) };
            Foe(station, 13f, FloorTop, 1.125f, 1.5f, new Enemies.FoeRecipe("stands: three cuts", still));

            Add(station, PieceKind.Solid, 18f, FloorTop, 8f, 2f, "it turns at the edges");
            var walker = new Enemies.FoeBody { health = 2 };
            var patrol = new Enemies.FoeRecipe("walker", walker, new Enemies.Patrol());
            Foe(station, 21f, FloorTop + 2f, 1.125f, 1.5f, patrol).id = "walker";

            Add(station, PieceKind.Solid, 30f, FloorTop, 1f, 2f);
            Add(station, PieceKind.Solid, 40f, FloorTop, 1f, 2f, "and at walls");
            Foe(station, 34f, FloorTop, 1.125f, 1.5f, patrol);

            var heavy = new Enemies.FoeBody
            {
                health = 6,
                speed = 16f,
                pushShare = 0.2f,
                shape = ShapeKind.Diamond,
                color = new Color(0.6f, 0.3f, 0.5f),
            };
            Foe(station, 45f, FloorTop, 1.5f, 2f, new Enemies.FoeRecipe("heavy", heavy, new Enemies.Patrol()));
        }

        /// <summary>
        /// Foes that attack, each warning first: a chaser that patrols until it sees you, an
        /// ambusher that stands still until you are near, and an armoured brute a cut does
        /// not stop.
        /// </summary>
        private static void Chase(Station station)
        {
            station.rebuilt = true;
            Add(station, PieceKind.Solid, 26f, FloorTop, 1f, 2f, "it stops at walls");
            Foe(station, 20f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Chaser()).id = "chaser";
            Add(station, PieceKind.Mark, 30f, FloorTop, 6f, 0.1f, "come near: it wakes");
            Foe(station, 33f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Ambusher()).id = "ambusher";
            Add(station, PieceKind.Solid, 39f, FloorTop, 1f, 2f);
            Foe(station, 45f, FloorTop, 1.5f, 2f, Enemies.FoeRecipes.Brute()).id = "brute";
        }

        /// <summary>
        /// Foes that attack from a distance: a shooter on a platform that aims, a diver
        /// that hovers and comes down at you, and a turret whose flat bolts are jumped.
        /// </summary>
        private static void Air(Station station)
        {
            station.rebuilt = true;
            Add(station, PieceKind.Solid, 16f, FloorTop, 6f, 2f, "it aims at you");
            Foe(station, 18.5f, FloorTop + 2f, 1.125f, 1.5f, Enemies.FoeRecipes.Shooter()).id = "shooter";
            Add(station, PieceKind.Mark, 27f, FloorTop, 8f, 0.1f, "walk under it");
            Foe(station, 30.5f, FloorTop + 5f, 1.25f, 0.875f, Enemies.FoeRecipes.Diver()).id = "diver";
            Add(station, PieceKind.Mark, 38f, FloorTop, 6f, 0.1f, "jump the bolt");
            Foe(station, 46f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Turret()).id = "turret";
        }

        /// <summary>
        /// A guard whose shield stops a cut and a bolt from in front, between two low walls,
        /// and a summoner that keeps two walkers alive until it is killed.
        /// </summary>
        private static void Guard(Station station)
        {
            station.rebuilt = true;
            Add(station, PieceKind.Solid, 15f, FloorTop, 1f, 1.5f);
            Add(station, PieceKind.Solid, 27f, FloorTop, 1f, 1.5f, "behind, above, or the slam");
            Foe(station, 20f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Guard()).id = "guard";
            Add(station, PieceKind.Mark, 34f, FloorTop, 8f, 0.1f, "kill the one that makes them");
            Foe(station, 44f, FloorTop, 1.25f, 1.75f, Enemies.FoeRecipes.Summoner()).id = "summoner";
        }

        /// <summary>
        /// A mixed fight: walking in shuts both doors, three waves of every kind of foe come
        /// one after the other, and the doors open when the last is dead.
        /// </summary>
        private static void Arena(Station station)
        {
            station.rebuilt = true;
            var tall = RoomHeight - 2f * Wall;
            Add(station, PieceKind.Door, 13f, FloorTop, 1f, tall, "door").id = "in";
            Add(station, PieceKind.Door, 49f, FloorTop, 1f, tall, "door").id = "out";
            Add(station, PieceKind.Solid, 29f, FloorTop, 4f, 1.5f);

            var first = new Enemies.EncounterWave();
            first.Add(Enemies.FoeRecipes.Walker(), At(station, 24f, FloorTop));
            first.Add(Enemies.FoeRecipes.Chaser(), At(station, 40f, FloorTop));
            var second = new Enemies.EncounterWave();
            second.Add(Enemies.FoeRecipes.Ambusher(), At(station, 22f, FloorTop));
            second.Add(Enemies.FoeRecipes.Shooter(), At(station, 31f, FloorTop + 1.5f));
            second.Add(Enemies.FoeRecipes.Diver(), At(station, 40f, FloorTop + 5f), new Vector2(1.25f, 0.875f));
            var third = new Enemies.EncounterWave();
            third.Add(Enemies.FoeRecipes.Guard(), At(station, 24f, FloorTop));
            third.Add(Enemies.FoeRecipes.Brute(), At(station, 38f, FloorTop), new Vector2(1.5f, 2f));
            third.Add(Enemies.FoeRecipes.Summoner(), At(station, 46f, FloorTop));

            var fight = Add(station, PieceKind.Encounter, 31f, FloorTop + 8f, 1f, 1f);
            fight.id = "fight";
            fight.waves = new List<Enemies.EncounterWave>();
            fight.waves.Add(first);
            fight.waves.Add(second);
            fight.waves.Add(third);
            fight.doors = new[] { "in", "out" };
            // Past the door: walking on begins it, and again after a death.
            var start = Zone(station, 15.5f, FloorTop, 1f, 6f, ShapeKind.Square, Color.clear, "");
            start.id = "start";
            start.once = false;
            start.actions = find => List(Do.Fight(find("fight").GetComponent<Enemies.Encounter>()));
        }

        /// <summary>
        /// The boss template behind shut doors: one foe whose pattern changes at two thirds
        /// and at one third of its health.
        /// </summary>
        private static void Boss(Station station)
        {
            station.rebuilt = true;
            var tall = RoomHeight - 2f * Wall;
            Add(station, PieceKind.Door, 13f, FloorTop, 1f, tall, "door").id = "in";
            Add(station, PieceKind.Door, 49f, FloorTop, 1f, tall, "door").id = "out";
            var wave = new Enemies.EncounterWave();
            wave.Add(Enemies.FoeRecipes.Boss(), At(station, 40f, FloorTop), new Vector2(2f, 2.5f));
            var fight = Add(station, PieceKind.Encounter, 31f, FloorTop + 8f, 1f, 1f);
            fight.id = "fight";
            fight.waves = new List<Enemies.EncounterWave>();
            fight.waves.Add(wave);
            fight.doors = new[] { "in", "out" };
            var start = Zone(station, 15.5f, FloorTop, 1f, 6f, ShapeKind.Square, Color.clear, "");
            start.id = "start";
            start.once = false;
            start.actions = find => List(Do.Fight(find("fight").GetComponent<Enemies.Encounter>()));
        }

        /// <summary>How wide the gauntlet is: three rooms end to end.</summary>
        /// <summary>
        /// What the old walkers did, as parts, each behind a low wall: a pouncer that leaps
        /// from a few steps off, a lunger that runs in and leaps short and low, a hurler on
        /// a platform that throws from far and backs away from near, and a slammer whose
        /// wave along the floor is jumped.
        /// </summary>
        private static void Habits(Station station)
        {
            station.rebuilt = true;
            Foe(station, 17f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Pouncer()).id = "pouncer";
            Add(station, PieceKind.Solid, 23f, FloorTop, 1f, 2f, "it leaps from a few steps off");
            Foe(station, 28f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Lunger()).id = "lunger";
            Add(station, PieceKind.Solid, 33f, FloorTop, 1f, 2f, "short, low and fast");
            Add(station, PieceKind.Solid, 36f, FloorTop, 6f, 2f, "come close: it backs away");
            Foe(station, 38.5f, FloorTop + 2f, 1.125f, 1.5f, Enemies.FoeRecipes.Hurler()).id = "hurler";
            Add(station, PieceKind.Solid, 44f, FloorTop, 1f, 2f, "jump the wave");
            Foe(station, 49f, FloorTop, 1.5f, 2f, Enemies.FoeRecipes.Slammer()).id = "slammer";
        }

        public const float GauntletWidth = 3f * RoomWidth;

        /// <summary>
        /// One run through everything, left to right: a step and spikes to jump, a walker
        /// between walls, a wall only the double jump clears, a chaser, spikes crossed by a
        /// ledge, a block to cut through, a shooter and a diver, a wall of light, a fight
        /// of two waves behind shut doors, and a heart at the end.
        /// </summary>
        private static void Gauntlet(Station station, float jump, float second)
        {
            station.rebuilt = true;
            var tall = RoomHeight - 2f * Wall;
            var pink = new Color(1f, 0.6f, 0.9f);

            Add(station, PieceKind.Solid, 14f, FloorTop, 3f, 2f, "up");
            Add(station, PieceKind.Hazard, 20f, FloorTop, 4f, 0.5f, "jump the spikes");

            Add(station, PieceKind.Solid, 28f, FloorTop, 1f, 1.5f);
            Add(station, PieceKind.Solid, 40f, FloorTop, 1f, 1.5f);
            Foe(station, 33f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Walker());

            var wall = Mathf.Floor(jump + second) - 1f;
            Add(station, PieceKind.Solid, 44f, FloorTop, 3f, wall, "jump and Wings");
            Foe(station, 54f, FloorTop, 1.125f, 1.5f, Enemies.FoeRecipes.Chaser());

            Add(station, PieceKind.Hazard, 60f, FloorTop, 8f, 0.5f, "over the top");
            Add(station, PieceKind.Ledge, 61f, FloorTop + 3f, 6f, 0.25f);

            Add(station, PieceKind.Solid, 74f, FloorTop + 3f, 1f, tall - 3f);
            Add(station, PieceKind.Breakable, 74f, FloorTop, 1f, 3f, "cut through", 3f).id = "block";

            Add(station, PieceKind.Solid, 82f, FloorTop, 6f, 2f);
            Foe(station, 84.5f, FloorTop + 2f, 1.125f, 1.5f, Enemies.FoeRecipes.Shooter());
            Foe(station, 93f, FloorTop + 5f, 1.25f, 0.875f, Enemies.FoeRecipes.Diver());

            Add(station, PieceKind.Barrier, 100f, FloorTop, 1f, tall, "light: dash through");

            Add(station, PieceKind.Door, 106f, FloorTop, 1f, tall, "door").id = "in";
            Add(station, PieceKind.Door, 150f, FloorTop, 1f, tall, "door").id = "out";
            var first = new Enemies.EncounterWave();
            first.Add(Enemies.FoeRecipes.Guard(), At(station, 120f, FloorTop));
            first.Add(Enemies.FoeRecipes.Ambusher(), At(station, 138f, FloorTop));
            var last = new Enemies.EncounterWave();
            last.Add(Enemies.FoeRecipes.Brute(), At(station, 126f, FloorTop), new Vector2(1.5f, 2f));
            last.Add(Enemies.FoeRecipes.Shooter(), At(station, 144f, FloorTop));
            last.Add(Enemies.FoeRecipes.Diver(), At(station, 134f, FloorTop + 5f), new Vector2(1.25f, 0.875f));
            var fight = Add(station, PieceKind.Encounter, 128f, FloorTop + 8f, 1f, 1f);
            fight.id = "fight";
            fight.waves = new List<Enemies.EncounterWave>();
            fight.waves.Add(first);
            fight.waves.Add(last);
            fight.doors = new[] { "in", "out" };
            var start = Zone(station, 108.5f, FloorTop, 1f, 6f, ShapeKind.Square, Color.clear, "");
            start.id = "start";
            start.once = false;
            start.actions = find => List(Do.Fight(find("fight").GetComponent<Enemies.Encounter>()));

            var heart = Zone(station, 158f, FloorTop + 0.5f, 1f, 1f, ShapeKind.Circle, pink, "the end");
            heart.id = "end";
            heart.hide = true;
            heart.actions = find => List(Do.Upgrade(UpgradeKind.Heart, ""), Do.Say("The end of the gauntlet."));
        }

        /// <summary>A point of a room, from its lower left corner, in the world.</summary>
        private static Vector2 At(Station station, float x, float y)
        {
            return new Vector2(station.room.x + x, station.room.y + y);
        }

        /// <summary>A foe standing at (x, y) from the room's lower left corner, its body `width` by `height` units.</summary>
        private static Piece Foe(Station station, float x, float y, float width, float height, Enemies.FoeRecipe recipe)
        {
            var piece = Add(station, PieceKind.Foe, x, y, width, height, recipe.name);
            piece.foe = recipe;
            return piece;
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

        /// <summary>
        /// Pickups, a key and a door, a switch to strike and a switch to press: every one a
        /// TriggerZone with a list of actions, drawn as a shape.
        /// </summary>
        private static void ItemsAndTriggers(Station station, float jump, float second)
        {
            station.rebuilt = true;
            var clear = Color.clear;

            // Walking on shows a line, once.
            var hint = Zone(station, 10f, FloorTop, 1f, 3f, ShapeKind.Square, clear, "");
            hint.actions = find => List(Do.Say("Take the wings, then the key over the wall."));

            // A pickup: touching it grants Wings and it is gone.
            var lilac = new Color(0.9f, 0.8f, 1f);
            var wings = Zone(station, 13f, FloorTop + 0.5f, 1f, 1f, ShapeKind.Circle, lilac, "Wings");
            wings.hide = true;
            wings.actions = find => List(Do.Grant(Ability.Wings), Do.Say("Wings: jump again in the air."));

            // The other pickups: a heart, a timed power and some light.
            var pink = new Color(1f, 0.6f, 0.9f);
            var heart = Zone(station, 14.75f, FloorTop + 0.5f, 1f, 1f, ShapeKind.Circle, pink, "heart");
            heart.hide = true;
            heart.actions = find => List(Do.Upgrade(UpgradeKind.Heart, ""));
            var cyan = new Color(0.6f, 0.9f, 1f);
            var blade = Zone(station, 16.5f, FloorTop + 0.5f, 1f, 1f, ShapeKind.Triangle, cyan, "blade");
            blade.hide = true;
            blade.actions = find => List(Do.Power(Power.Blade));
            var pale = new Color(1f, 0.95f, 0.7f);
            var light = Zone(station, 22f, FloorTop + 0.5f, 0.5f, 0.5f, ShapeKind.Square, pale, "light");
            light.hide = true;
            light.actions = find => List(Do.Currency("light", 5));

            // A wall a jump does not clear and a double jump does, with the key behind it.
            var wall = Mathf.Min(jump + second - 1f, jump + 1.5f);
            Add(station, PieceKind.Solid, 18f, FloorTop, 3f, wall, $"{wall:0.#} high: needs Wings");
            var gold = new Color(1f, 0.85f, 0.3f);
            var key = Zone(station, 24.5f, FloorTop + 0.5f, 1f, 1f, ShapeKind.Diamond, gold, "key");
            key.id = "key";
            key.hide = true;
            key.actions = find => List(Do.Give(Key));

            // The door: a lintel down to four units, the door under it, and its lock in front.
            Add(station, PieceKind.Solid, 30f, FloorTop + 4f, 1f, RoomHeight - 2f * Wall - 4f);
            Add(station, PieceKind.Door, 30f, FloorTop, 1f, 4f, "door").id = "door";
            var locked = Zone(station, 26f, FloorTop, 1.5f, 3f, ShapeKind.Square, clear, "");
            locked.id = "locked";
            locked.once = false;
            locked.needs = TriggerNeeds.WithoutItem(Key);
            locked.actions = find => List(Do.Say(LockedText));
            var unlock = Zone(station, 27.5f, FloorTop, 2.5f, 3f, ShapeKind.Square, clear, "");
            unlock.id = "lock";
            unlock.needs = TriggerNeeds.Item(Key);
            unlock.actions = Unlock;

            // A switch to strike: a bridge comes and goes.
            var bridge = Add(station, PieceKind.Ledge, 37f, FloorTop + 3f, 6f, 0.25f, "bridge");
            bridge.id = "bridge";
            bridge.hidden = true;
            var red = new Color(0.95f, 0.45f, 0.3f);
            var strike = Zone(station, 34f, FloorTop + 0.5f, 1f, 1.5f, ShapeKind.Square, red, "strike");
            strike.id = "strike";
            strike.on = TriggerEvent.Hit;
            strike.once = false;
            strike.actions = find => List(Do.Active(find("bridge"), SwitchTo.Toggle));

            // A switch to press: a lift starts and stops.
            var lift = Add(station, PieceKind.Lift, 47.5f, FloorTop + 1f, 3f, 0.25f, "lift");
            lift.id = "lift";
            lift.travel = new Vector2(0f, 8f);
            var blue = new Color(0.4f, 0.6f, 1f);
            var press = Zone(station, 45f, FloorTop, 1.5f, 2f, ShapeKind.Square, blue, "interact");
            press.id = "press";
            press.on = TriggerEvent.Interact;
            press.once = false;
            press.actions = find => List(Do.Mover(find("lift").GetComponent<Mover>(), SwitchTo.Toggle));
        }

        /// <summary>How high the camera station's plateau stands over the floor.</summary>
        public const float PlateauHeight = 14f;

        /// <summary>How far down the look-down zone shifts the view, in units.</summary>
        public const float LookDown = 5f;

        /// <summary>How much more the arena zone shows.</summary>
        public const float ArenaZoom = 1.5f;

        /// <summary>
        /// A plateau with a low corridor where the view's height is held, an edge where it
        /// looks down before the drop, and an arena below where it pulls back.
        /// </summary>
        private static void CameraZones(Station station)
        {
            var top = FloorTop + PlateauHeight;
            Add(station, PieceKind.Solid, Wall, FloorTop, 24f, PlateauHeight, "plateau");
            station.spawn = new Vector2(station.room.x + SpawnX, top);

            // The corridor: a low roof, and the view's height held at its middle.
            Add(station, PieceKind.Solid, 12f, top + 4f, 8f, 2f, "low roof");
            var corridor = Add(station, PieceKind.Camera, 12f, top, 8f, 4f, "height held", 1f);
            corridor.id = "corridor";
            corridor.lockY = true;

            // The edge: the view drops to show where he will land.
            var edge = Add(station, PieceKind.Camera, 22f, top, 4f, 4f, "look down", 1f);
            edge.id = "edge";
            edge.framing = new Vector2(0f, -LookDown);

            // The arena: the view pulls back to show all of it.
            var arena = Add(station, PieceKind.Camera, 27f, FloorTop, 27f, 12f, "pulled back", ArenaZoom);
            arena.id = "arena";
        }

        /// <summary>
        /// A ferry over spikes, a riser up to a shelf, a ferry a switch starts, and a block
        /// that comes down to the floor: standing under it hurts and puts him beside it.
        /// </summary>
        private static void Platforms(Station station)
        {
            Add(station, PieceKind.Solid, 11f, FloorTop, 3f, 2f, "step on");
            Add(station, PieceKind.Hazard, 14f, FloorTop, 12f, 0.5f, "spikes");
            var ferry = Add(station, PieceKind.Lift, 14f, FloorTop + 1.75f, 3f, 0.25f, "ferry");
            ferry.id = "ferry";
            ferry.travel = new Vector2(9f, 0f);
            ferry.running = true;
            Add(station, PieceKind.Solid, 26f, FloorTop, 4f, 2f, "step off");

            var riser = Add(station, PieceKind.Lift, 30f, FloorTop + 1.75f, 3f, 0.25f, "riser");
            riser.id = "riser";
            riser.travel = new Vector2(0f, 6f);
            riser.running = true;
            Add(station, PieceKind.Solid, 33f, FloorTop, 5f, 8f, "shelf");

            var switched = Add(station, PieceKind.Lift, 38f, FloorTop + 7.75f, 3f, 0.25f, "switched");
            switched.id = "switched";
            switched.travel = new Vector2(7f, 0f);
            var blue = new Color(0.4f, 0.6f, 1f);
            var press = Zone(station, 34f, FloorTop + 8f, 1.5f, 2f, ShapeKind.Square, blue, "interact");
            press.id = "press";
            press.on = TriggerEvent.Interact;
            press.once = false;
            press.actions = find => List(Do.Mover(find("switched").GetComponent<Mover>(), SwitchTo.Toggle));
            Add(station, PieceKind.Solid, 48f, FloorTop + 7f, 4f, 1f, "far shelf");

            var crusher = Add(station, PieceKind.Platform, 41f, FloorTop + 3f, 3f, 1.5f, "crusher");
            crusher.id = "crusher";
            crusher.travel = new Vector2(0f, -3f);
            crusher.running = true;
        }

        /// <summary>
        /// Low ceilings whose corner hangs over a mark by a few pixels (stand on the mark and
        /// jump: up to the correction he slips past, with more he bumps), and two steps a
        /// little higher than a jump: the first is caught at its edge, the second is not.
        /// </summary>
        private static void Corners(Station station, float jump, int correction)
        {
            const float pixel = 1f / PlayerTuning.PixelsPerUnit;
            const float halfHero = 0.4375f;
            var x = 12f;
            foreach (var over in new[] { Mathf.Max(correction - 2, 1), correction, correction * 2 + 2 })
            {
                Add(station, PieceKind.Mark, x - 0.05f, FloorTop, 0.1f, 1.5f, "stand here");
                Add(station, PieceKind.Solid, x + halfHero - over * pixel, FloorTop + 3f, 3f, 1f, $"{over} px over");
                x += 7f;
            }
            Add(station, PieceKind.Solid, 34f, FloorTop, 4f, jump + 0.3f, "caught at the edge");
            // A shelf rather than a step, so the way on is under it.
            Add(station, PieceKind.Solid, 42f, FloorTop + jump + 0.25f, 4f, 0.5f, "too high");
        }

        /// <summary>
        /// A tank a jump gets into and a jump at the surface gets out of, and a column
        /// of water entered at its foot that only strokes take him up, to a shelf.
        /// </summary>
        private static void Water(Station station, float jump)
        {
            var wall = Mathf.Floor(jump) - 0.5f;
            Add(station, PieceKind.Solid, 12f, FloorTop, 1f, wall, "jump in");
            Add(station, PieceKind.Water, 13f, FloorTop, 12f, wall - 0.5f, "sink, swim, jump out at the top");
            Add(station, PieceKind.Solid, 25f, FloorTop, 1f, wall, "");

            // Open at its foot: he walks in under the wall and strokes up.
            var column = 2f * jump + 2f;
            Add(station, PieceKind.Solid, 32f, FloorTop + 3f, 1f, column - 3f, "walk in under");
            Add(station, PieceKind.Water, 33f, FloorTop, 4f, column, "jump again and again");
            Add(station, PieceKind.Solid, 37f, FloorTop, 10f, column - 1f, "shelf");
        }

        /// <summary>
        /// A ladder up the side of a shelf too high to jump, with a ledge at its top to
        /// step off onto, and a wide vine to climb and leap from to a shelf across a gap.
        /// </summary>
        private static void Climb(Station station, float jump)
        {
            var shelf = Mathf.Ceil(jump) + 2f;
            Add(station, PieceKind.Climb, 14f, FloorTop, 1f, shelf, "up to take hold");
            Add(station, PieceKind.Ledge, 14f, FloorTop + shelf - 0.25f, 1f, 0.25f, "");
            Add(station, PieceKind.Solid, 15f, FloorTop, 8f, shelf, "down at the top climbs back");

            var vine = shelf + 3f;
            Add(station, PieceKind.Climb, 27f, FloorTop + 1f, 5f, vine, "jump lets go; a side and jump leaps");
            Add(station, PieceKind.Solid, 35f, FloorTop, 8f, vine - 1f, "leap here");
        }

        /// <summary>
        /// The ledge grab, switched on in this room only: a wall a little higher than a
        /// jump whose corner he catches on the way down, a second one to catch from a
        /// drop off the first, and a slot too low to stand in, where up does nothing.
        /// </summary>
        private static void Grab(Station station, float jump)
        {
            station.ledgeGrab = true;
            var wall = Mathf.Ceil(jump) + 1f;
            Add(station, PieceKind.Solid, 14f, FloorTop, 6f, wall, "jump, hold towards it: up climbs, down lets go");
            Add(station, PieceKind.Solid, 24f, FloorTop, 6f, wall - 3f, "walk off and hold back towards the wall");
            Add(station, PieceKind.Solid, 34f, FloorTop, 6f, wall, "no room to stand");
            Add(station, PieceKind.Solid, 34f, FloorTop + wall + 1f, 6f, 1f, "");
        }

        /// <summary>
        /// A corridor with the wind in his face, a gap wider than a jump that a gust at
        /// his back carries him over, and a shaft whose lift a fall beats and a glide
        /// rides up to a shelf.
        /// </summary>
        private static void Wind(Station station, float leap, PlayerTuning tuning)
        {
            var head = Add(station, PieceKind.Wind, 11f, FloorTop, 9f, 5f, "headwind");
            head.wind = new Vector2(-0.6f * tuning.runSpeed, 0f);

            // The gust makes up what the gap is wider than a jump, and a little more.
            var gap = Mathf.Ceil(leap * 1.4f);
            var gust = Add(station, PieceKind.Wind, 20f, FloorTop, gap + 6f, 8f, "gusts: jump with one");
            gust.wind = new Vector2(0.7f * tuning.runSpeed, 0f);
            gust.gustOn = 1.5f;
            gust.gustOff = 1.5f;
            Add(station, PieceKind.Hazard, 23f, FloorTop, gap, 0.5f, $"{gap:0.#} wide");

            var lift = Add(station, PieceKind.Wind, 36f, FloorTop, 6f, 18f, "lift: glide in it");
            lift.wind = new Vector2(0f, tuning.glideFallSpeed + 0.4f * tuning.runSpeed);
            Add(station, PieceKind.Solid, 42f, FloorTop, 10f, 12f, "shelf");
        }

        /// <summary>
        /// Over a first bed of spikes, three platforms that crumble once stood on; over a
        /// second, four that blink one after another; and past them a bridge that is gone
        /// until the switch brings it.
        /// </summary>
        private static void Timing(Station station)
        {
            const float top = FloorTop + 1.75f;
            Add(station, PieceKind.Solid, 10f, FloorTop, 2f, 2f, "step on");
            Add(station, PieceKind.Hazard, 12f, FloorTop, 9f, 0.5f, "spikes");
            for (var i = 0; i < 3; i++)
            {
                var crumble = Add(station, PieceKind.Crumble, 12.5f + i * 3f, top, 2f, 0.25f, i == 0 ? "crumbles" : "");
                crumble.solidFor = 0.6f;
                crumble.goneFor = 2f;
            }
            Add(station, PieceKind.Solid, 21f, FloorTop, 3f, 2f, "");

            Add(station, PieceKind.Hazard, 24f, FloorTop, 12f, 0.5f, "spikes");
            for (var i = 0; i < 4; i++)
            {
                var blinker = Add(station, PieceKind.Blinker, 24.5f + i * 3f, top, 2f, 0.25f, i == 0 ? "blinks" : "");
                blinker.solidFor = 1.6f;
                blinker.goneFor = 1.6f;
                blinker.ahead = -0.8f * i;
            }
            Add(station, PieceKind.Solid, 36f, FloorTop, 3f, 2f, "");

            Add(station, PieceKind.Hazard, 39f, FloorTop, 7f, 0.5f, "spikes");
            var bridge = Add(station, PieceKind.Blinker, 39f, top, 7f, 0.25f, "switched");
            bridge.id = "bridge";
            bridge.solidFor = 4f;
            bridge.goneFor = 2f;
            bridge.warnFor = 1f;
            bridge.hidden = true;
            var blue = new Color(0.4f, 0.6f, 1f);
            var press = Zone(station, 37f, FloorTop + 2f, 1.5f, 2f, ShapeKind.Square, blue, "interact");
            press.on = TriggerEvent.Interact;
            press.once = false;
            press.actions = find => List(Do.Timed(find("bridge").GetComponent<TimedPlatform>(), SwitchTo.Toggle));
            Add(station, PieceKind.Solid, 46f, FloorTop, 4f, 2f, "");
        }

        /// <summary>The lock: takes the key, opens the door and quiets the "locked" line.</summary>
        private static TriggerAction[] Unlock(Func<string, GameObject> find)
        {
            var open = Do.Door(find("door").GetComponent<Door>(), SwitchTo.On);
            var quiet = Do.Active(find("locked"), SwitchTo.Off);
            return List(Do.Take(Key), open, quiet, Do.Say(UnlockedText));
        }

        /// <summary>The piece of a station with an id, or null.</summary>
        public static Piece Find(Station station, string id)
        {
            return station.pieces.Find(p => p.id == id);
        }

        private static TriggerAction[] List(params TriggerAction[] actions) => actions;

        /// <summary>A trigger zone piece: fires once on enter and asks for nothing until told otherwise.</summary>
        private static Piece Zone(
            Station station,
            float x,
            float y,
            float width,
            float height,
            ShapeKind shape,
            Color color,
            string label
        )
        {
            var piece = Add(station, PieceKind.Zone, x, y, width, height, label);
            piece.shape = shape;
            piece.color = color;
            return piece;
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
