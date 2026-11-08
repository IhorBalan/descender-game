using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Checks the Paradise layout against the construction rules (ParadiseRules, from
    /// docs/design/paradise.md, section 2) without a scene: it walks each zone's main
    /// path over the top edges of its ground, decks and ledges and its bounce pods, and
    /// reports every jump the hero cannot make, every killing fall, every thorn patch
    /// or light barrier lying across the path and every stretch without a save
    /// shrine, as one line each naming the zone, the elements and the measured
    /// number. ParadiseGreybox refuses to build a zone it reports on, and
    /// ParadiseCheckerTests keeps the real layout clean.
    ///
    /// Measured on the layout's own points joined by straight lines (the built curves
    /// are smoothed between them), so a number here can differ from the scene by a
    /// fraction of a tile; the limits carry the design's margin for that.
    /// </summary>
    public static class ParadiseChecker
    {
        private const float Epsilon = 0.001f;

        /// <summary>
        /// Every violation in the region: each zone's own (CheckZone), the step from one
        /// zone's last surface onto the next zone's first, and the shrine spacing.
        /// </summary>
        public static List<string> Check(IReadOnlyList<ParadiseLayout.Zone> zones)
        {
            var violations = new List<string>();
            foreach (var zone in zones)
            {
                violations.AddRange(CheckZone(zone));
            }
            for (var i = 0; i + 1 < zones.Count; i++)
            {
                var from = LastSurface(zones[i]);
                var onto = FirstSurface(zones[i + 1]);
                if (from != null && onto != null)
                {
                    var where = $"Zone {zones[i].number} to {zones[i + 1].number}";
                    CheckTransfer(where, from, null, onto, zones[i + 1].soft, violations);
                }
            }
            violations.AddRange(CheckShrineSpacing(zones));
            return violations;
        }

        /// <summary>
        /// One zone's violations: a main path that is missing or names something unknown,
        /// gaps, steps and drops along it, save shrines standing off it, and hazards across it.
        /// </summary>
        public static List<string> CheckZone(ParadiseLayout.Zone zone)
        {
            var violations = new List<string>();
            var where = $"Zone {zone.number}";
            if (zone.mainPath.Count == 0)
            {
                violations.Add($"{where}: declares no main path.");
                return violations;
            }

            Element last = null;
            ParadiseLayout.Pod pod = null;
            foreach (var name in zone.mainPath)
            {
                var element = Resolve(zone, name);
                if (element == null)
                {
                    violations.Add(
                        $"{where}: the main path names '{name}', which is no ground, deck, ledge or pod of the zone."
                    );
                    continue;
                }
                if (element.pod != null)
                {
                    if (last == null || pod != null)
                    {
                        violations.Add($"{where}: pod '{name}' does not follow a surface of the main path.");
                    }
                    else if (!Near(last.line, element.pod.at, ParadiseRules.ShrineReach))
                    {
                        violations.Add(
                            $"{where}: pod '{name}' at x {Tiles(element.pod.at.x)} does not stand on '{last.name}'."
                        );
                    }
                    else
                    {
                        pod = element.pod;
                    }
                    continue;
                }
                CheckWalls(where, element, zone.soft, violations);
                if (last != null)
                {
                    CheckTransfer(where, last, pod, element, zone.soft, violations);
                }
                last = element;
                pod = null;
            }
            if (pod != null)
            {
                violations.Add($"{where}: pod '{pod.name}' ends the main path; a pod throws the hero onto a surface.");
            }

            foreach (var save in zone.saves)
            {
                if (!OnMainPath(zone, save))
                {
                    violations.Add(
                        $"{where}: the save shrine at ({Tiles(save.x)}, {Tiles(save.y)}) is not within "
                            + $"{Tiles(ParadiseRules.ShrineReach)} tile of a main-path surface."
                    );
                }
            }
            violations.AddRange(CheckHazards(zone));
            violations.AddRange(CheckBarriers(zone));
            return violations;
        }

        /// <summary>
        /// Light barriers across the main path before the light dash: a barrier's column of
        /// cells meeting the top edge of a main-path surface at an x where the hero cannot
        /// dash yet (ParadiseLayout.Grants). After the dash a barrier on the path is a door he
        /// dashes through; before it, a wall he cannot pass.
        /// </summary>
        public static List<string> CheckBarriers(ParadiseLayout.Zone zone)
        {
            var violations = new List<string>();
            var where = $"Zone {zone.number}";
            var dashX = float.PositiveInfinity;
            foreach (var grant in ParadiseLayout.Grants)
            {
                if (grant.ability == Descender.Save.Ability.LightDash)
                {
                    dashX = grant.x;
                }
            }
            foreach (var barrier in zone.barriers)
            {
                if (barrier.at.x >= dashX)
                {
                    continue;
                }
                foreach (var name in zone.mainPath)
                {
                    var element = Resolve(zone, name);
                    if (element != null && element.pod == null && Crosses(element.line, barrier.Rect))
                    {
                        violations.Add(
                            $"{where}: barrier '{barrier.name}' at x {Tiles(barrier.at.x)} crosses the main path "
                                + $"on '{name}' before the light dash (x {Tiles(dashX)})."
                        );
                    }
                }
            }
            return violations;
        }

        /// <summary>
        /// Hazards lying across the main path: a thorn rect that meets the top edge of a
        /// main-path surface (its walkable slopes; the walls beside it do not count, thorns
        /// on a trunk's face are beside the path). Thorns belong beside the path, on ground
        /// the straight run does not cross.
        /// </summary>
        public static List<string> CheckHazards(ParadiseLayout.Zone zone)
        {
            var violations = new List<string>();
            var where = $"Zone {zone.number}";
            foreach (var hazard in zone.hazards)
            {
                foreach (var name in zone.mainPath)
                {
                    var element = Resolve(zone, name);
                    if (element != null && element.pod == null && Crosses(element.line, hazard.rect))
                    {
                        violations.Add(
                            $"{where}: hazard '{hazard.name}' at x {Tiles(hazard.rect.xMin)} to {Tiles(hazard.rect.xMax)} "
                                + $"lies across the main path on '{name}'."
                        );
                    }
                }
            }
            return violations;
        }

        /// <summary>
        /// The top edge of the zone's surface called `name` (ground, deck or ledge), west to
        /// east; null for a pod or an unknown name.
        /// </summary>
        public static List<Vector2> SurfaceLine(ParadiseLayout.Zone zone, string name)
        {
            var element = Resolve(zone, name);
            return element?.line;
        }

        /// <summary>Whether a rect meets any walkable (non-wall) segment of a line.</summary>
        private static bool Crosses(List<Vector2> line, Rect rect)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                if (b.x - a.x <= Epsilon)
                {
                    continue;
                }
                var west = Mathf.Max(a.x, rect.xMin);
                var east = Mathf.Min(b.x, rect.xMax);
                if (west > east + Epsilon)
                {
                    continue;
                }
                var yWest = Mathf.Lerp(a.y, b.y, (west - a.x) / (b.x - a.x));
                var yEast = Mathf.Lerp(a.y, b.y, (east - a.x) / (b.x - a.x));
                if (Mathf.Max(yWest, yEast) >= rect.yMin - Epsilon && Mathf.Min(yWest, yEast) <= rect.yMax + Epsilon)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Stretches of more than ParadiseRules.MaxShrineSpacing tiles of x without a save
        /// shrine: from the first zone's west edge to the first shrine, between consecutive
        /// shrines, and from the last shrine to the last zone's east edge.
        /// </summary>
        public static List<string> CheckShrineSpacing(IReadOnlyList<ParadiseLayout.Zone> zones)
        {
            var violations = new List<string>();
            if (zones.Count == 0)
            {
                return violations;
            }
            var stops = new List<float>();
            foreach (var zone in zones)
            {
                foreach (var save in zone.saves)
                {
                    stops.Add(save.x);
                }
            }
            stops.Sort();
            stops.Insert(0, zones[0].xMin);
            stops.Add(zones[zones.Count - 1].xMax);
            for (var i = 0; i + 1 < stops.Count; i++)
            {
                var stretch = stops[i + 1] - stops[i];
                if (stretch > ParadiseRules.MaxShrineSpacing + Epsilon)
                {
                    violations.Add(
                        $"Zone {ZoneAt(zones, stops[i + 1]).number}: {Tiles(stretch)} tiles without a save shrine, "
                            + $"from x {Tiles(stops[i])} to x {Tiles(stops[i + 1])} "
                            + $"(limit {Tiles(ParadiseRules.MaxShrineSpacing)})."
                    );
                }
            }
            return violations;
        }

        /// <summary>Whether a point lies within ParadiseRules.ShrineReach of a surface of the zone's main path.</summary>
        public static bool OnMainPath(ParadiseLayout.Zone zone, Vector2 at)
        {
            foreach (var name in zone.mainPath)
            {
                var element = Resolve(zone, name);
                if (element != null && element.pod == null && Near(element.line, at, ParadiseRules.ShrineReach))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The walkable top of a shape, west to east: its outline from the first point for as
        /// long as x does not turn back, without the east wall running down from its end.
        /// Outlines start at the top-left and run clockwise (ParadiseLayout.Zone.Closed).
        /// </summary>
        public static List<Vector2> TopEdge(List<ParadiseLayout.P> outline)
        {
            var top = new List<Vector2>();
            foreach (var point in outline)
            {
                if (top.Count > 0 && point.x < top[top.Count - 1].x - Epsilon)
                {
                    break;
                }
                top.Add(point.V);
            }
            while (
                top.Count >= 2
                && Mathf.Abs(top[top.Count - 1].x - top[top.Count - 2].x) <= Epsilon
                && top[top.Count - 1].y < top[top.Count - 2].y
            )
            {
                top.RemoveAt(top.Count - 1);
            }
            return top;
        }

        private static Element Resolve(ParadiseLayout.Zone zone, string name)
        {
            foreach (var shape in zone.shapes)
            {
                if (shape.name == name && shape.collides)
                {
                    return Surface(name, shape.outline);
                }
            }
            foreach (var bridge in zone.bridges)
            {
                if (bridge.name == name)
                {
                    return Surface(name, bridge.deck);
                }
            }
            foreach (var ledge in zone.ledges)
            {
                if (ledge.name == name)
                {
                    return Surface(name, ledge.path);
                }
            }
            foreach (var pod in zone.pods)
            {
                if (pod.name == name)
                {
                    return new Element { name = name, pod = pod };
                }
            }
            return null;
        }

        private static Element Surface(string name, List<ParadiseLayout.P> points)
        {
            var line = TopEdge(points);
            return line.Count >= 2 ? new Element { name = name, line = line } : null;
        }

        private static Element FirstSurface(ParadiseLayout.Zone zone)
        {
            foreach (var name in zone.mainPath)
            {
                var element = Resolve(zone, name);
                if (element != null && element.pod == null)
                {
                    return element;
                }
            }
            return null;
        }

        private static Element LastSurface(ParadiseLayout.Zone zone)
        {
            for (var i = zone.mainPath.Count - 1; i >= 0; i--)
            {
                var element = Resolve(zone, zone.mainPath[i]);
                if (element != null && element.pod == null)
                {
                    return element;
                }
            }
            return null;
        }

        /// <summary>Walls inside one surface (two points at the same x): a step up or a drop like any other.</summary>
        private static void CheckWalls(string where, Element surface, HashSet<string> soft, List<string> violations)
        {
            for (var i = 0; i + 1 < surface.line.Count; i++)
            {
                var a = surface.line[i];
                var b = surface.line[i + 1];
                if (Mathf.Abs(b.x - a.x) > Epsilon)
                {
                    continue;
                }
                var rise = b.y - a.y;
                var limit = ParadiseRules.For(a.x).step;
                if (rise > limit + Epsilon)
                {
                    violations.Add(
                        $"{where}: step up of {Tiles(rise)} tiles inside '{surface.name}' at x {Tiles(a.x)} "
                            + $"(limit {Tiles(limit)})."
                    );
                }
                else if (-rise > ParadiseRules.MaxFall + Epsilon && !soft.Contains(surface.name))
                {
                    violations.Add(
                        $"{where}: drop of {Tiles(-rise)} tiles inside '{surface.name}' at x {Tiles(a.x)} "
                            + $"(limit {Tiles(ParadiseRules.MaxFall)} onto ground that is not soft)."
                    );
                }
            }
        }

        /// <summary>
        /// The move from one surface to the next. Apart in x, the hero leaves the near end of
        /// one for the near end of the other, and the distance between is the gap. Where they
        /// overlap in x there is no gap, and he crosses where their heights are closest. A pod
        /// between them allows a rise up to its throw height, measured from the pod.
        /// </summary>
        private static void CheckTransfer(
            string where,
            Element from,
            ParadiseLayout.Pod pod,
            Element onto,
            HashSet<string> soft,
            List<string> violations
        )
        {
            Transfer(from.line, onto.line, out var takeoff, out var landing);

            var limits = ParadiseRules.For(takeoff.x);
            var gap = Mathf.Abs(landing.x - takeoff.x);
            if (gap > limits.gap + Epsilon)
            {
                violations.Add(
                    $"{where}: gap of {Tiles(gap)} tiles from '{from.name}' to '{onto.name}' at x {Tiles(takeoff.x)} "
                        + $"(limit {Tiles(limits.gap)})."
                );
            }

            var rise = landing.y - takeoff.y;
            if (rise > limits.step + Epsilon)
            {
                if (pod == null)
                {
                    violations.Add(
                        $"{where}: step up of {Tiles(rise)} tiles from '{from.name}' to '{onto.name}' "
                            + $"at x {Tiles(takeoff.x)} (limit {Tiles(limits.step)})."
                    );
                }
                else if (landing.y - pod.at.y > pod.tiles + Epsilon)
                {
                    violations.Add(
                        $"{where}: step up of {Tiles(landing.y - pod.at.y)} tiles from pod '{pod.name}' on "
                            + $"'{from.name}' to '{onto.name}' (the pod throws {Tiles(pod.tiles)})."
                    );
                }
            }
            else if (-rise > ParadiseRules.MaxFall + Epsilon && !soft.Contains(onto.name))
            {
                violations.Add(
                    $"{where}: drop of {Tiles(-rise)} tiles from '{from.name}' onto '{onto.name}' "
                        + $"at x {Tiles(takeoff.x)} (limit {Tiles(ParadiseRules.MaxFall)} onto ground that is not soft)."
                );
            }
        }

        /// <summary>
        /// Where the hero leaves one surface for the next and where he arrives, by the rule
        /// of CheckTransfer: the near ends when the surfaces are apart in x (true), the
        /// points where their heights are closest when they overlap (false). The scripted
        /// playtest (ParadiseRunTests) aims its jumps with the same points the rules measure.
        /// </summary>
        public static bool Transfer(List<Vector2> from, List<Vector2> onto, out Vector2 takeoff, out Vector2 landing)
        {
            var fromWest = from[0];
            var fromEast = from[from.Count - 1];
            var ontoWest = onto[0];
            var ontoEast = onto[onto.Count - 1];
            if (ontoWest.x >= fromEast.x)
            {
                takeoff = fromEast;
                landing = ontoWest;
                return true;
            }
            if (ontoEast.x <= fromWest.x)
            {
                takeoff = fromWest;
                landing = ontoEast;
                return true;
            }
            Closest(from, onto, out takeoff, out landing);
            return false;
        }

        /// <summary>Over the x two surfaces share, the points where their heights are closest.</summary>
        private static void Closest(List<Vector2> from, List<Vector2> onto, out Vector2 takeoff, out Vector2 landing)
        {
            var west = Mathf.Max(from[0].x, onto[0].x);
            var east = Mathf.Min(from[from.Count - 1].x, onto[onto.Count - 1].x);
            var candidates = new List<float> { west, east };
            foreach (var point in from)
            {
                if (point.x > west && point.x < east)
                {
                    candidates.Add(point.x);
                }
            }
            foreach (var point in onto)
            {
                if (point.x > west && point.x < east)
                {
                    candidates.Add(point.x);
                }
            }
            takeoff = new Vector2(west, HeightAt(from, west));
            landing = new Vector2(west, HeightAt(onto, west));
            foreach (var x in candidates)
            {
                var a = HeightAt(from, x);
                var b = HeightAt(onto, x);
                if (Mathf.Abs(b - a) < Mathf.Abs(landing.y - takeoff.y))
                {
                    takeoff = new Vector2(x, a);
                    landing = new Vector2(x, b);
                }
            }
        }

        /// <summary>The line's height at x; at a wall, the side met first walking east. Beyond an end, that end's height.</summary>
        public static float HeightAt(List<Vector2> line, float x)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                if (b.x - a.x > Epsilon && x >= a.x && x <= b.x)
                {
                    return Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                }
            }
            return x <= line[0].x ? line[0].y : line[line.Count - 1].y;
        }

        private static bool Near(List<Vector2> line, Vector2 at, float reach)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                if (b.x - a.x > Epsilon)
                {
                    if (at.x < a.x || at.x > b.x)
                    {
                        continue;
                    }
                    var y = Mathf.Lerp(a.y, b.y, (at.x - a.x) / (b.x - a.x));
                    if (Mathf.Abs(at.y - y) <= reach + Epsilon)
                    {
                        return true;
                    }
                }
                else if (
                    Mathf.Abs(at.x - a.x) <= Epsilon
                    && at.y >= Mathf.Min(a.y, b.y) - reach - Epsilon
                    && at.y <= Mathf.Max(a.y, b.y) + reach + Epsilon
                )
                {
                    return true;
                }
            }
            return false;
        }

        private static ParadiseLayout.Zone ZoneAt(IReadOnlyList<ParadiseLayout.Zone> zones, float x)
        {
            foreach (var zone in zones)
            {
                if (x <= zone.xMax)
                {
                    return zone;
                }
            }
            return zones[zones.Count - 1];
        }

        private static string Tiles(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>One element of a main path: a surface (its top edge, west to east) or a pod.</summary>
        private sealed class Element
        {
            public string name;
            public List<Vector2> line;
            public ParadiseLayout.Pod pod;
        }
    }
}
