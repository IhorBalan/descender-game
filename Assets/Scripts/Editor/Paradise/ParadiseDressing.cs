using System.Collections.Generic;
using UnityEngine;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Where the art goes in each zone (docs/design/paradise.md, sections 4 and 5): props
    /// on the ground (their height found on the zone's own terrain curves, so they sit on
    /// it), big shapes behind the play layer, light shafts, foreground silhouettes and the
    /// air. Built into the zone scenes by ParadiseArt.Dress.
    /// </summary>
    public static class ParadiseDressing
    {
        public enum PropLayer
        {
            Mid,
            Ground,
        }

        public struct PropSpot
        {
            public string name;
            public Vector2 at;
            public float scale;
            public bool flip;
            public PropLayer layer;
        }

        public struct ForeSpot
        {
            public int index;
            public Vector2 at;
            public float scale;
            public bool flip;
        }

        public struct Air
        {
            public string name;
            public string kind;
            public int amount;
            public Color color;
            public float life;
            public Vector2 direction;
            public float spread;
            public Vector2 speed;
            public Vector2 size;
            public float spin;
            public float sway;
        }

        public sealed class Plan
        {
            public float backdropY = -57f;
            public readonly List<PropSpot> props = new List<PropSpot>();
            public readonly List<Vector4> shafts = new List<Vector4>();
            public readonly List<ForeSpot> foreground = new List<ForeSpot>();
            public readonly List<Air> air = new List<Air>();
        }

        public static Plan For(ParadiseLayout.Zone zone)
        {
            return zone.number == 1 ? Zone1(zone) : zone.number == 2 ? Zone2(zone) : new Plan();
        }

        /// <summary>The highest walkable surface of the zone's ground at x (ledges aside), or `fallback`.</summary>
        public static float SurfaceY(ParadiseLayout.Zone zone, float x, float fallback = -60f, float below = 0f)
        {
            var best = float.NegativeInfinity;
            foreach (var shape in zone.shapes)
            {
                if (!shape.collides)
                {
                    continue;
                }
                var (left, right) = ParadiseGreybox.Tangents(shape.outline, true);
                var points = ParadiseGreybox.Sample(shape.outline, left, right, true);
                for (var i = 0; i < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Count];
                    if (Mathf.Approximately(a.x, b.x) || x < Mathf.Min(a.x, b.x) || x > Mathf.Max(a.x, b.x))
                    {
                        continue;
                    }
                    var y = Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                    if (y > best && y < below)
                    {
                        best = y;
                    }
                }
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        private static void Add(Plan plan, ParadiseLayout.Zone zone, string name, float x, float scale = 1f, bool flip = false, PropLayer layer = PropLayer.Ground, float? y = null, float sink = 0.15f)
        {
            var at = new Vector2(x, (y ?? SurfaceY(zone, x)) - sink);
            plan.props.Add(new PropSpot { name = name, at = at, scale = scale, flip = flip, layer = layer });
        }

        private static void Fore(Plan plan, ParadiseLayout.Zone zone, int index, float x, float scale, bool flip, bool hanging)
        {
            var ground = SurfaceY(zone, x);
            // Standing pieces start well below the ground, so their base never shows on screen.
            var y = hanging ? ground + 10f : ground - 6.5f;
            plan.foreground.Add(new ForeSpot { index = index, at = new Vector2(x, y), scale = scale, flip = flip });
        }

        private static Air Particles(string name, string kind, int amount, Color color, float life, Vector2 dir, float spread, Vector2 speed, Vector2 size, float spin = 0f, float sway = 4f)
        {
            return new Air
            {
                name = name,
                kind = kind,
                amount = amount,
                color = color,
                life = life,
                direction = dir,
                spread = spread,
                speed = speed,
                size = size,
                spin = spin,
                sway = sway,
            };
        }

        /// <summary>The Waking Hollow at dawn: flowers along the path, giant curled leaves behind, warm motes, petals and pollen, soft rays.</summary>
        private static Plan Zone1(ParadiseLayout.Zone zone)
        {
            var plan = new Plan { backdropY = -56f };
            string[] flowers = { "z1_prop_flowers_a", "z1_prop_flowers_b", "z1_prop_flowers_c" };
            float[] flowerX = { 18, 27, 37, 62, 78, 84, 97, 112, 120, 136, 148, 162, 178, 192, 210, 220, 241, 252, 270, 279, 296, 308, 322, 340, 356, 372, 388 };
            for (var i = 0; i < flowerX.Length; i++)
            {
                Add(plan, zone, flowers[i % 3], flowerX[i], 0.7f + (i * 37 % 10) * 0.04f, i % 2 == 1);
            }
            // Giant curled leaves behind the play layer: the Hollow's cradle and the way out.
            Add(plan, zone, "z1_prop_giant_leaf", 6f, 1.1f, false, PropLayer.Mid, -64f, 0f);
            Add(plan, zone, "z1_prop_giant_leaf", 104f, 0.85f, true, PropLayer.Mid, -60f, 0f);
            Add(plan, zone, "z1_prop_giant_leaf", 232f, 1f, false, PropLayer.Mid, -66f, 0f);
            Add(plan, zone, "z1_prop_giant_leaf", 344f, 0.9f, true, PropLayer.Mid, -61f, 0f);

            foreach (var x in new[] { 24f, 118f, 196f, 288f, 366f })
            {
                plan.shafts.Add(new Vector4(x, -36f, 1.6f, 1.5f));
            }
            var fore = 0;
            for (var x = 30f; x < 400f; x += 38f)
            {
                Fore(plan, zone, fore, x, 0.55f + (fore % 3) * 0.08f, fore % 2 == 0, false);
                fore++;
            }
            plan.air.Add(Particles("Motes", "mote", 45, new Color(1f, 0.86f, 0.62f), 11f, new Vector2(0.15f, -1f), 30f, new Vector2(2f, 6f), new Vector2(0.4f, 1f), sway: 8f));
            plan.air.Add(Particles("Petals", "petal", 26, new Color(1f, 0.82f, 0.86f), 13f, new Vector2(-0.35f, 1f), 25f, new Vector2(8f, 16f), new Vector2(0.5f, 0.9f), spin: 0.4f, sway: 5f));
            plan.air.Add(Particles("Pollen", "pollen", 40, new Color(1f, 0.92f, 0.55f), 8f, new Vector2(0.6f, -0.2f), 40f, new Vector2(3f, 8f), new Vector2(0.5f, 1f), sway: 6f));
            return plan;
        }

        /// <summary>The Orchard under the canopy: trees in rows too even, fungi lighting the dells, fruit, shafts through the leaves, fireflies.</summary>
        private static Plan Zone2(ParadiseLayout.Zone zone)
        {
            var plan = new Plan { backdropY = -57f };
            // The orchard's trees, evenly spaced: a little too tidy.
            for (var x = 586f; x <= 742f; x += 26f)
            {
                Add(plan, zone, "z2_prop_orchard_tree", x, 0.8f, false, PropLayer.Mid, -62.2f, 0f);
            }
            Add(plan, zone, "z2_prop_orchard_tree", 420f, 0.75f, true, PropLayer.Mid, -62f, 0f);
            Add(plan, zone, "z2_prop_orchard_tree", 470f, 0.8f, false, PropLayer.Mid, -62.2f, 0f);
            Add(plan, zone, "z2_prop_orchard_tree", 515f, 0.95f, true, PropLayer.Mid, -62f, 0f);
            Add(plan, zone, "z2_prop_orchard_tree", 920f, 1.45f, false, PropLayer.Mid, -64.2f, 0f);
            Add(plan, zone, "z2_prop_orchard_tree", 1010f, 0.8f, true, PropLayer.Mid, -60.2f, 0f);
            Add(plan, zone, "z2_prop_orchard_tree", 1150f, 0.85f, false, PropLayer.Mid, -62f, 0f);
            Add(plan, zone, "z2_prop_orchard_tree", 1240f, 0.8f, true, PropLayer.Mid, -50.6f, 0f);

            float[] mushroomX = { 430, 452, 498, 776, 795, 808, 830, 846, 896, 938, 962, 1015, 1064, 1118, 1205, 1260 };
            for (var i = 0; i < mushroomX.Length; i++)
            {
                Add(plan, zone, "z2_prop_mushrooms", mushroomX[i], 0.55f + (i % 3) * 0.12f, i % 2 == 0);
            }
            Add(plan, zone, "z2_prop_mushrooms", 556f, 0.6f, false, PropLayer.Ground, -76.2f);
            Add(plan, zone, "z2_prop_mushrooms", 570f, 0.5f, true, PropLayer.Ground, -76f);
            Add(plan, zone, "z2_prop_mushrooms", 990f, 0.6f, false, PropLayer.Ground, -80.3f);
            Add(plan, zone, "z2_prop_mushrooms", 1052f, 0.55f, true, PropLayer.Ground, -80f);

            // Fruit hanging under the canopy walk (the pivot is the stem's top).
            foreach (var x in new[] { 604f, 614f, 622f, 636f, 648f, 664f, 676f })
            {
                Add(plan, zone, "z2_prop_fruit", x, 0.8f, x % 2f > 0.5f, PropLayer.Ground, -46.9f, 0f);
            }

            for (var x = 430f; x < 1300f; x += 46f)
            {
                plan.shafts.Add(new Vector4(x, -32f, 1.4f + (x % 3f) * 0.2f, 1.9f));
            }
            var fore = 0;
            for (var x = 420f; x < 1300f; x += 36f)
            {
                var hanging = fore % 3 == 2;
                Fore(plan, zone, fore, x, 0.55f + (fore % 2) * 0.1f, fore % 2 == 1, hanging);
                fore++;
            }
            plan.air.Add(Particles("Motes", "mote", 70, new Color(0.55f, 1f, 0.92f), 12f, new Vector2(0f, -1f), 50f, new Vector2(1f, 4f), new Vector2(0.4f, 1.1f), sway: 10f));
            plan.air.Add(Particles("Fireflies", "firefly", 30, new Color(0.75f, 1f, 0.6f), 9f, new Vector2(0.4f, -0.6f), 90f, new Vector2(2f, 6f), new Vector2(0.5f, 0.9f), sway: 14f));
            plan.air.Add(Particles("Pollen", "pollen", 40, new Color(0.6f, 0.95f, 1f), 9f, new Vector2(0.3f, 0.4f), 60f, new Vector2(2f, 5f), new Vector2(0.4f, 0.8f), sway: 6f));
            return plan;
        }
    }
}
