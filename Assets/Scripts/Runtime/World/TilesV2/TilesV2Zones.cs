using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Makes the areas the hero feels a Tiles V2 world's liquids through, with the game's
    /// own parts: a Triggers.Water over every stretch of a liquid he swims in, and a
    /// HazardArea trigger on the Hazard layer over every stretch of one that hurts (lava).
    /// A stretch is a run of columns whose liquid spans the same rows, so the top of each
    /// area is the true surface above it.
    /// </summary>
    public static class TilesV2Zones
    {
        // The liquid stands this far under the top of its top cell (the generator's SURFACE_DROP).
        private const float SurfaceDrop = 3f / 16f;

        // A hazard starts a little under the surface and inside its sides, so the brim is safe.
        private const float HazardInset = 2f / 16f;

        /// <summary>Builds the areas under `parent`; returns how many.</summary>
        public static int Build(TilesV2World world, TileField field, Transform parent)
        {
            var root = new GameObject("Zones").transform;
            root.SetParent(parent, false);
            var count = 0;
            foreach (var liquid in world.Set.liquids)
            {
                if (liquid.tile == null || (!liquid.swim && liquid.hazard <= 0))
                {
                    continue;
                }
                foreach (var area in Stretches(field, liquid.tile))
                {
                    var surface = area.yMax - SurfaceDrop;
                    if (liquid.swim)
                    {
                        var holder = new GameObject("Water " + liquid.key);
                        holder.transform.SetParent(root, false);
                        var size = new Vector2(area.width, surface - area.yMin);
                        holder.transform.localPosition = new Vector3(area.center.x, area.yMin + size.y / 2f, 0f);
                        holder.AddComponent<Triggers.Water>().Configure(size);
                        count++;
                    }
                    if (liquid.hazard > 0)
                    {
                        var holder = new GameObject("Hazard " + liquid.key) { layer = LayerMask.NameToLayer("Hazard") };
                        holder.transform.SetParent(root, false);
                        var size = new Vector2(area.width - 2f * HazardInset, surface - HazardInset - area.yMin);
                        holder.transform.localPosition = new Vector3(area.center.x, area.yMin + size.y / 2f, 0f);
                        var box = holder.AddComponent<BoxCollider2D>();
                        box.isTrigger = true;
                        box.size = size;
                        holder.AddComponent<HazardArea>();
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// The stretches of a liquid in the grid's units: for every column its runs of the
        /// liquid from top to bottom, neighbouring columns joined where a run spans the same
        /// rows. Each rectangle's top is the top of its top cell.
        /// </summary>
        public static List<Rect> Stretches(TileField field, LiquidTile liquid)
        {
            var areas = new List<Rect>();
            // The runs still open at the column before: (bottom row, top row) to the column they began at.
            var open = new Dictionary<(int, int), int>();
            var here = new List<(int, int)>();
            for (var x = 0; x <= field.Width; x++)
            {
                here.Clear();
                if (x < field.Width)
                {
                    var y = 0;
                    while (y < field.Height)
                    {
                        if (field.Liquid[field.Index(x, y)] != liquid)
                        {
                            y++;
                            continue;
                        }
                        var bottom = y;
                        while (y < field.Height && field.Liquid[field.Index(x, y)] == liquid)
                        {
                            y++;
                        }
                        here.Add((bottom, y - 1));
                    }
                }
                var closing = new List<(int, int)>();
                foreach (var run in open.Keys)
                {
                    if (!here.Contains(run))
                    {
                        closing.Add(run);
                    }
                }
                foreach (var run in closing)
                {
                    var from = field.CellAt(open[run], run.Item1);
                    areas.Add(new Rect(from.x, from.y, x - open[run], run.Item2 - run.Item1 + 1));
                    open.Remove(run);
                }
                foreach (var run in here)
                {
                    if (!open.ContainsKey(run))
                    {
                        open[run] = x;
                    }
                }
            }
            return areas;
        }
    }
}
