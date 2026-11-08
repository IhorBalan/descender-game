using System.Collections.Generic;
using System.Globalization;
using Descender.EditorTools.Paradise;
using Descender.Input;
using Descender.Player;
using Descender.Save;
using UnityEngine;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Drives the hero along the Paradise main path through a ScriptedInput, the way a
    /// player who knows the way would: it runs toward the next surface's take-off
    /// point, jumps at the edge, steers for the landing, and in the air adds the Wings'
    /// second jump or the light dash when the fall would come up short. The path is
    /// ParadiseLayout's, measured with ParadiseChecker.Transfer, so the run checks the
    /// built scenes against the same points the construction rules do. A leg the hero
    /// cannot make after a few tries, a death or a long stall ends the run with
    /// <see cref="Failure"/> naming the leg and where he stood.
    /// </summary>
    internal sealed class MainPathPilot
    {
        public enum Kind
        {
            /// <summary>The next point is on ground he can walk to (touching surfaces, a drop, a wall down).</summary>
            Walk,

            /// <summary>A jump from the take-off to the landing (a gap, a step up, a wall up).</summary>
            Jump,

            /// <summary>A bounce pod at the take-off throws him; he steers for the landing.</summary>
            Pod,
        }

        /// <summary>One move of the plan: from a point on one surface to a point on the next.</summary>
        public sealed class Leg
        {
            public int zone;
            public string from;
            public string onto;
            public Kind kind;
            public Vector2 takeoff;
            public Vector2 landing;

            /// <summary>The direction of travel, 1 east or -1 west.</summary>
            public int dir;

            /// <summary>The top edge the hero should stand on once the leg is made.</summary>
            public List<Vector2> ontoLine;

            /// <summary>The top edge he leaves (null for the first leg).</summary>
            public List<Vector2> fromLine;

            /// <summary>Whether a point is standing on the destination, at or past the landing.</summary>
            public bool Reached(Vector2 at)
            {
                var west = ontoLine[0].x - 0.5f;
                var east = ontoLine[ontoLine.Count - 1].x + 0.5f;
                if (at.x < west || at.x > east)
                {
                    return false;
                }
                if ((at.x - landing.x) * dir < -0.6f)
                {
                    return false;
                }
                var height = Mathf.Abs(at.x - landing.x) < 0.6f ? landing.y : ParadiseChecker.HeightAt(ontoLine, at.x);
                return Mathf.Abs(at.y - height) < 0.75f;
            }

            /// <summary>Whether a point is standing on the surface the leg leaves.</summary>
            public bool OnFrom(Vector2 at)
            {
                if (fromLine == null)
                {
                    return false;
                }
                var west = fromLine[0].x - 0.5f;
                var east = fromLine[fromLine.Count - 1].x + 0.5f;
                return at.x >= west
                    && at.x <= east
                    && Mathf.Abs(at.y - ParadiseChecker.HeightAt(fromLine, at.x)) < 0.75f;
            }

            public override string ToString()
            {
                return $"zone {zone}: {kind} from '{from}' at {Point(takeoff)} to '{onto}' at {Point(landing)}";
            }
        }

        private enum Phase
        {
            Approach,
            Air,
            Backtrack,
        }

        /// <summary>How far before the edge he jumps, by attempt: the first is the usual, the rest vary it.</summary>
        private static readonly float[] GapLeads = { 0.35f, 0.8f, 0.1f, 0.6f };

        /// <summary>For a wall: never under half the hero's width, or he stands against it before he jumps.</summary>
        private static readonly float[] StepLeads = { 0.8f, 1.3f, 0.6f, 1.0f };

        /// <summary>In the air he aims this far past the landing point, so his feet, not his middle, clear the edge.</summary>
        private const float LandingOvershoot = 0.7f;

        /// <summary>A leg that takes longer than this (seconds of game time) has failed.</summary>
        public const float LegTimeLimit = 90f;

        private readonly PlayerController hero;
        private readonly ScriptedInput input;
        private readonly List<Leg> legs;
        private readonly float fallGravity;
        private readonly float runSpeed;

        private Phase phase = Phase.Approach;
        private int attempts;
        private float legTime;
        private float airTime;
        private float holdLeft;
        private float stuckTime;
        private float stuckX;
        private float pressCooldown;
        private float backtrackX;
        private int stillSteps;

        public MainPathPilot(PlayerController hero, ScriptedInput input, List<Leg> legs)
        {
            this.hero = hero;
            this.input = input;
            this.legs = legs;
            var tuning = hero.Tuning;
            fallGravity = PlayerTuning.Units(tuning.FallGravity);
            runSpeed = PlayerTuning.Units(tuning.runSpeed);
        }

        public int LegIndex { get; private set; }
        public bool Done { get; private set; }
        public string Failure { get; private set; }
        public int AirJumps { get; private set; }
        public int Dashes { get; private set; }
        public int StuckJumps { get; private set; }
        public int Retries { get; private set; }

        /// <summary>Times he came down on an earlier surface of the plan and took it up again from there.</summary>
        public int Resumes { get; private set; }

        public Leg Current => LegIndex < legs.Count ? legs[LegIndex] : null;

        /// <summary>
        /// The legs of the whole region: surface to surface along each zone's main path,
        /// the step from each zone's last surface to the next zone's first, and inside a
        /// surface a leg for each wall between the point he arrives at and the point he
        /// leaves from. Ends with a walk to `endX` on the last surface.
        /// </summary>
        public static List<Leg> Plan(IReadOnlyList<ParadiseLayout.Zone> zones, Vector2 start, float endX)
        {
            var stops = new List<Stop>();
            foreach (var zone in zones)
            {
                ParadiseLayout.Pod pending = null;
                foreach (var name in zone.mainPath)
                {
                    var line = ParadiseChecker.SurfaceLine(zone, name);
                    if (line == null)
                    {
                        pending = zone.pods.Find(p => p.name == name);
                        continue;
                    }
                    stops.Add(
                        new Stop
                        {
                            zone = zone.number,
                            name = name,
                            line = line,
                            pod = pending,
                        }
                    );
                    pending = null;
                }
            }

            // Where he arrives on each surface and where he leaves it.
            var entries = new Vector2[stops.Count];
            var exits = new Vector2[stops.Count];
            var apart = new bool[stops.Count];
            entries[0] = start;
            for (var i = 0; i + 1 < stops.Count; i++)
            {
                apart[i] = ParadiseChecker.Transfer(stops[i].line, stops[i + 1].line, out exits[i], out entries[i + 1]);
            }
            var lastLine = stops[stops.Count - 1].line;
            exits[stops.Count - 1] = new Vector2(endX, ParadiseChecker.HeightAt(lastLine, endX));

            var legs = new List<Leg>();
            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                AddWallLegs(legs, stop, entries[i], exits[i]);
                if (i + 1 == stops.Count)
                {
                    legs.Add(
                        new Leg
                        {
                            zone = stop.zone,
                            from = stop.name,
                            onto = stop.name,
                            kind = Kind.Walk,
                            takeoff = exits[i],
                            landing = exits[i],
                            dir = exits[i].x >= entries[i].x ? 1 : -1,
                            fromLine = stop.line,
                            ontoLine = stop.line,
                        }
                    );
                    break;
                }
                var next = stops[i + 1];
                var takeoff = exits[i];
                var landing = entries[i + 1];
                var dir =
                    Mathf.Abs(landing.x - takeoff.x) > 0.01f
                        ? (landing.x > takeoff.x ? 1 : -1)
                        : (takeoff.x >= entries[i].x ? 1 : -1);
                Kind kind;
                if (next.pod != null)
                {
                    // A pod throws him straight up, so he has to drift onto the landing while he
                    // rises: aim inside the landing surface, on the side of it the pod is nearest.
                    kind = Kind.Pod;
                    takeoff = next.pod.at;
                    var west = Mathf.Min(next.line[0].x, next.line[next.line.Count - 1].x);
                    var east = Mathf.Max(next.line[0].x, next.line[next.line.Count - 1].x);
                    dir = (west + east) * 0.5f >= takeoff.x ? 1 : -1;
                    var inside = Mathf.Clamp(takeoff.x + dir * 1.5f, west + 0.5f, east - 0.5f);
                    landing = new Vector2(inside, ParadiseChecker.HeightAt(next.line, inside));
                }
                else if (apart[i] && (Mathf.Abs(landing.x - takeoff.x) > 0.3f || landing.y > takeoff.y + 0.3f))
                {
                    kind = Kind.Jump;
                }
                else if (!apart[i] && landing.y > takeoff.y + 0.3f)
                {
                    kind = Kind.Jump;
                }
                else
                {
                    kind = Kind.Walk;
                }
                legs.Add(
                    new Leg
                    {
                        zone = next.zone,
                        from = stop.name,
                        onto = next.name,
                        kind = kind,
                        takeoff = takeoff,
                        landing = landing,
                        dir = dir,
                        fromLine = stop.line,
                        ontoLine = next.line,
                    }
                );
            }
            return legs;
        }

        /// <summary>
        /// The walls of one surface between the entry and the exit, in the direction of
        /// travel: a wall up is a jump leg onto its top, a wall down a walk off its edge.
        /// </summary>
        private static void AddWallLegs(List<Leg> legs, Stop stop, Vector2 entry, Vector2 exit)
        {
            var line = stop.line;
            var dir = exit.x >= entry.x ? 1 : -1;
            var west = Mathf.Min(entry.x, exit.x);
            var east = Mathf.Max(entry.x, exit.x);
            var walls = new List<(Vector2 low, Vector2 high, bool up)>();
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                if (Mathf.Abs(b.x - a.x) > 0.01f || Mathf.Abs(b.y - a.y) < 0.3f)
                {
                    continue;
                }
                if (a.x <= west + 0.01f || a.x >= east - 0.01f)
                {
                    continue;
                }
                // Walking east meets `a` first; walking west meets `b` first.
                var first = dir > 0 ? a : b;
                var second = dir > 0 ? b : a;
                walls.Add((first, second, second.y > first.y));
            }
            if (dir < 0)
            {
                walls.Reverse();
            }
            foreach (var wall in walls)
            {
                legs.Add(
                    new Leg
                    {
                        zone = stop.zone,
                        from = stop.name,
                        onto = stop.name,
                        kind = wall.up ? Kind.Jump : Kind.Walk,
                        takeoff = wall.low,
                        landing = wall.high,
                        dir = dir,
                        fromLine = line,
                        ontoLine = line,
                    }
                );
            }
        }

        /// <summary>One frame of piloting, called after the hero's Update has read the last frame's input.</summary>
        public void Step(float dt)
        {
            input.ClearPresses();
            if (Done || Failure != null)
            {
                input.Release();
                return;
            }
            if (!hero.IsAlive)
            {
                Fail("the hero died");
                return;
            }
            if (hero.Talking)
            {
                input.MoveX = 0f;
                input.JumpHeld = false;
                return;
            }
            var leg = Current;
            if (leg == null)
            {
                Done = true;
                input.Release();
                return;
            }

            Vector2 at = hero.transform.position;
            var velocity = hero.Velocity;
            var grounded = hero.IsGrounded;
            legTime += dt;
            Tick(ref holdLeft, dt);
            Tick(ref pressCooldown, dt);
            if (legTime > LegTimeLimit)
            {
                Fail($"no progress for {LegTimeLimit:0} s");
                return;
            }
            if (grounded && phase != Phase.Air && leg.Reached(at))
            {
                Next();
                return;
            }

            input.JumpHeld = holdLeft > 0f;
            switch (phase)
            {
                case Phase.Approach:
                    Approach(leg, at, grounded, velocity, dt);
                    break;
                case Phase.Backtrack:
                    input.MoveX = Mathf.Sign(backtrackX - at.x);
                    if (Mathf.Abs(at.x - backtrackX) < 0.3f || (at.x - backtrackX) * leg.dir < 0f)
                    {
                        phase = Phase.Approach;
                        ResetStuck(at);
                    }
                    break;
                case Phase.Air:
                    Air(leg, at, grounded, velocity, dt);
                    break;
            }
        }

        private void Approach(Leg leg, Vector2 at, bool grounded, Vector2 velocity, float dt)
        {
            if (leg.kind == Kind.Pod && !grounded && velocity.y > 2f)
            {
                // The pod threw him.
                phase = Phase.Air;
                airTime = 0f;
                input.MoveX = leg.dir;
                return;
            }
            if (!grounded)
            {
                // Off the ground without a planned jump (a hop, a drop): keep going the way of travel.
                input.MoveX = leg.dir;
                ResetStuck(at);
                return;
            }
            if (at.y < leg.takeoff.y - 3f && !leg.OnFrom(at) && Lost(leg, at))
            {
                return;
            }
            if (leg.kind == Kind.Walk)
            {
                // Past the junction the leg completes on the next check; keep going the way of travel.
                input.MoveX = leg.dir;
            }
            else
            {
                var dx = leg.takeoff.x - at.x;
                input.MoveX = Mathf.Abs(dx) < 0.1f ? 0f : Mathf.Sign(dx);
            }
            if (
                leg.kind == Kind.Jump
                && grounded
                && (at.x - leg.takeoff.x) * leg.dir >= -Lead(leg)
                && Mathf.Abs(at.y - leg.takeoff.y) < 1.5f
            )
            {
                Jump();
                phase = Phase.Air;
                airTime = 0f;
                input.MoveX = leg.dir;
                return;
            }
            // Walking into something he did not plan for (a lip, a wall off the measured line): hop.
            if (grounded && input.MoveX != 0f)
            {
                if (Mathf.Abs(at.x - stuckX) > 0.05f)
                {
                    ResetStuck(at);
                }
                else
                {
                    stuckTime += dt;
                    if (stuckTime > 0.3f)
                    {
                        Jump();
                        StuckJumps++;
                        ResetStuck(at);
                    }
                }
            }
            else
            {
                ResetStuck(at);
            }
        }

        private void Air(Leg leg, Vector2 at, bool grounded, Vector2 velocity, float dt)
        {
            airTime += dt;
            // Steer for a point past the landing and never back against the way of travel: turning
            // round in the air over an edge is how he misses it.
            var aimX = leg.landing.x + leg.dir * LandingOvershoot;
            input.MoveX = (aimX - at.x) * leg.dir > 0f ? leg.dir : 0f;
            input.JumpHeld = velocity.y > 0f || airTime < 0.05f || holdLeft > 0f;
            // Touching down shows up a physics step before the ground check does: the body comes to
            // rest, then the next step finds the ground under it. A resting hero is landed too, or
            // else an air jump pressed in that step takes off again and he never reads as grounded.
            // A dash also holds him level, and so does the top of a jump for a step, so rest is two
            // still steps outside a dash.
            stillSteps = !grounded && !hero.IsDashing && Mathf.Abs(velocity.y) < 0.01f ? stillSteps + 1 : 0;
            var resting = stillSteps >= 2 && airTime > 0.1f;
            if ((grounded || resting) && airTime > 0.1f)
            {
                if (leg.Reached(at))
                {
                    Next();
                }
                else if (leg.kind != Kind.Walk && leg.OnFrom(at) && (at.x - leg.landing.x) * leg.dir < 0f)
                {
                    Retry(leg, at);
                }
                else if (!Lost(leg, at))
                {
                    phase = Phase.Approach;
                    ResetStuck(at);
                }
                return;
            }
            if (grounded || velocity.y >= 0f || pressCooldown > 0f)
            {
                return;
            }
            // Falling: will he reach the landing?
            var remaining = (leg.landing.x - at.x) * leg.dir;
            var below = at.y < leg.landing.y + 0.25f;
            var reach = below ? 0f : Reach(at.y - leg.landing.y, velocity.y);
            if (!below && reach >= remaining + 0.3f)
            {
                return;
            }
            if (!below && remaining > 1f && hero.Has(Ability.LightDash) && hero.DashReady)
            {
                input.DashPressed = true;
                Dashes++;
                pressCooldown = 0.3f;
            }
            else if (hero.Has(Ability.Wings) && hero.AirJumpAvailable)
            {
                input.JumpPressed = true;
                holdLeft = 0.5f;
                AirJumps++;
                pressCooldown = 0.3f;
            }
        }

        /// <summary>How far he runs while falling `drop` units from a vertical speed `vy` (units/s, falling).</summary>
        private float Reach(float drop, float vy)
        {
            var time = (vy + Mathf.Sqrt(vy * vy + 2f * fallGravity * drop)) / fallGravity;
            return runSpeed * time;
        }

        private float Lead(Leg leg)
        {
            var step = leg.fromLine == leg.ontoLine || Mathf.Abs(leg.landing.x - leg.takeoff.x) < 0.3f;
            var leads = step ? StepLeads : GapLeads;
            return leads[Mathf.Min(attempts, leads.Length - 1)];
        }

        private void Jump()
        {
            input.JumpPressed = true;
            input.JumpHeld = true;
            holdLeft = 0.6f;
        }

        private void Retry(Leg leg, Vector2 at)
        {
            attempts++;
            Retries++;
            if (attempts >= 4)
            {
                Fail($"landed back on '{leg.from}' after {attempts} tries");
                return;
            }
            backtrackX = leg.takeoff.x - leg.dir * 4f;
            phase = Phase.Backtrack;
        }

        /// <summary>
        /// Landed somewhere that is neither the leg's landing nor the surface it leaves: back on an
        /// earlier surface of the plan, the run resumes from the leg that leaves it; anywhere else well
        /// below the take-off is off the path, and the run fails there rather than pacing under the
        /// jump until the leg times out. Returns whether the leg changed or the run failed.
        /// </summary>
        private bool Lost(Leg leg, Vector2 at)
        {
            for (var j = LegIndex - 1; j >= 0; j--)
            {
                if (StandsOn(legs[j].ontoLine, at))
                {
                    Resumes++;
                    LegIndex = j;
                    Next();
                    return true;
                }
            }
            if (at.y < leg.takeoff.y - 3f)
            {
                Fail("fell off the path");
                return true;
            }
            return false;
        }

        private static bool StandsOn(List<Vector2> line, Vector2 at)
        {
            var west = Mathf.Min(line[0].x, line[line.Count - 1].x) - 0.5f;
            var east = Mathf.Max(line[0].x, line[line.Count - 1].x) + 0.5f;
            return at.x >= west && at.x <= east && Mathf.Abs(at.y - ParadiseChecker.HeightAt(line, at.x)) < 0.75f;
        }

        private void Next()
        {
            LegIndex++;
            attempts = 0;
            legTime = 0f;
            phase = Phase.Approach;
            holdLeft = 0f;
            ResetStuck(hero.transform.position);
            if (LegIndex >= legs.Count)
            {
                Done = true;
                input.Release();
            }
        }

        private void Fail(string why)
        {
            var leg = Current;
            Vector2 at = hero.transform.position;
            Failure =
                $"{why} on leg {LegIndex + 1} of {legs.Count} ({leg}); the hero stands at {Point(at)}"
                + $" with Wings {(hero.Has(Ability.Wings) ? "yes" : "no")}"
                + $", light dash {(hero.Has(Ability.LightDash) ? "yes" : "no")}.";
            input.Release();
        }

        private void ResetStuck(Vector2 at)
        {
            stuckX = at.x;
            stuckTime = 0f;
        }

        private static void Tick(ref float timer, float dt)
        {
            if (timer > 0f)
            {
                timer = Mathf.Max(0f, timer - dt);
            }
        }

        private static string Point(Vector2 p) => $"({Tiles(p.x)}, {Tiles(p.y)})";

        private static string Tiles(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        private sealed class Stop
        {
            public int zone;
            public string name;
            public List<Vector2> line;
            public ParadiseLayout.Pod pod;
        }
    }
}
