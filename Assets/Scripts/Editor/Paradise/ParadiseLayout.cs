using System.Collections.Generic;
using Descender.Save;
using UnityEngine;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// The greybox of Paradise as data: every zone's ground, ledges, pods, save
    /// shrines, design markers, lights and camera rooms, in world units (x right from
    /// the region's west edge, y up, the region's top edge at 0; the main path runs
    /// near y = -60 in a 120-row band). Built into zone scenes by ParadiseGreybox.
    /// The numbers follow the construction rules in docs/design/paradise.md: before
    /// Wings, main-path gaps are at most 4 tiles and steps at most 4 up (the jump
    /// reaches 4.5 high and 5 across). Each zone names its main path, the surfaces and
    /// pods the straight west to east run crosses; ParadiseChecker walks it against
    /// ParadiseRules before a zone is built.
    /// </summary>
    public static class ParadiseLayout
    {
        public enum Profile
        {
            Ground,
            Leaf,
            Decor,
        }

        /// <summary>A point of a shape; sharp points are corners, the rest are smoothed.</summary>
        public struct P
        {
            public float x;
            public float y;
            public bool sharp;

            public P(float x, float y, bool sharp = false)
            {
                this.x = x;
                this.y = y;
                this.sharp = sharp;
            }

            public Vector2 V => new Vector2(x, y);
        }

        public sealed class Shape
        {
            public string name;
            public Profile profile;
            public List<P> outline;
            public bool collides = true;

            /// <summary>Rock allowed to float: drawn with a rocky underside (docs/design/paradise.md, 5).</summary>
            public bool island;
        }

        /// <summary>
        /// A walkable deck that is architecture: a bridge, walkway, balcony or landing. It is
        /// one-way (jump up through, stand on top) and the builder stands it on columns, with
        /// arches between them, down to the first surface below or into the cloud sea.
        /// </summary>
        public sealed class Bridge
        {
            public string name;
            public List<P> deck;
            public float spacing = 6f;
        }

        public sealed class Ledge
        {
            public string name;
            public List<P> path;
        }

        public sealed class Pod
        {
            public string name;
            public Vector2 at;
            public float tiles;
        }

        public sealed class Marker
        {
            public Descender.World.Paradise.DesignMarker.Kind kind;
            public string label;
            public string note;
            public Vector2 at;
        }

        public sealed class Lamp
        {
            public Vector2 at;
            public Color color;
            public float radiusTiles;
            public float energy;
        }

        /// <summary>Where along the region (x, in tiles) an ability becomes the hero's.</summary>
        public readonly struct Grant
        {
            public readonly Ability ability;
            public readonly float x;

            public Grant(Ability ability, float x)
            {
                this.ability = ability;
                this.x = x;
            }
        }

        public sealed class Zone
        {
            public int number;
            public string scene;
            public float xMin;
            public float xMax;
            public readonly List<Shape> shapes = new List<Shape>();
            public readonly List<Ledge> ledges = new List<Ledge>();
            public readonly List<Bridge> bridges = new List<Bridge>();
            public readonly List<Pod> pods = new List<Pod>();
            public readonly List<Vector2> seeds = new List<Vector2>();
            public readonly List<Vector2> saves = new List<Vector2>();
            public readonly List<Marker> markers = new List<Marker>();
            public readonly List<Lamp> lamps = new List<Lamp>();
            public readonly List<Rect> rooms = new List<Rect>();

            /// <summary>Ground built by hand (marble terrace walls with a carved coping); the rest is natural cliff rock.</summary>
            public readonly HashSet<string> masonry = new HashSet<string>();

            /// <summary>
            /// The main path: the surfaces (ground shapes, decks, ledges) and bounce pods the
            /// straight west to east run crosses, by name and in order. ParadiseChecker walks it.
            /// </summary>
            public readonly List<string> mainPath = new List<string>();

            /// <summary>Main-path elements a long fall may land on (water, moss, cloud): no fall limit onto them.</summary>
            public readonly HashSet<string> soft = new HashSet<string>();

            public void Masonry(params string[] names)
            {
                masonry.UnionWith(names);
            }

            public void MainPath(params string[] names)
            {
                mainPath.AddRange(names);
            }

            public void Soft(params string[] names)
            {
                soft.UnionWith(names);
            }

            /// <summary>A bounce pod throwing the hero `tiles` high, named so the main path can cross it.</summary>
            public void Bounce(string name, float x, float y, float tiles)
            {
                pods.Add(
                    new Pod
                    {
                        name = name,
                        at = new Vector2(x, y),
                        tiles = tiles,
                    }
                );
            }

            /// <summary>Ground whose top runs left to right along `top`, filled down to `bottom`.</summary>
            public void Ground(string name, float bottom, params P[] top)
            {
                var outline = new List<P>(top);
                var first = top[0];
                var last = top[top.Length - 1];
                outline[0] = new P(first.x, first.y, true);
                outline[outline.Count - 1] = new P(last.x, last.y, true);
                outline.Add(new P(last.x, bottom, true));
                outline.Add(new P(first.x, bottom, true));
                shapes.Add(
                    new Shape
                    {
                        name = name,
                        profile = Profile.Ground,
                        outline = outline,
                    }
                );
            }

            /// <summary>A closed shape given by its whole outline, clockwise from its top-left.</summary>
            public void Closed(string name, Profile profile, bool collides, params P[] outline)
            {
                shapes.Add(
                    new Shape
                    {
                        name = name,
                        profile = profile,
                        outline = new List<P>(outline),
                        collides = collides,
                    }
                );
            }

            /// <summary>A deck on columns: see <see cref="Bridge"/>. Spacing is the distance between columns.</summary>
            public void Deck(string name, float spacing, params P[] deck)
            {
                bridges.Add(
                    new Bridge
                    {
                        name = name,
                        deck = new List<P>(deck),
                        spacing = spacing,
                    }
                );
            }

            /// <summary>Floating rock, declared: its outline clockwise from its top-left.</summary>
            public void Island(string name, params P[] outline)
            {
                shapes.Add(
                    new Shape
                    {
                        name = name,
                        profile = Profile.Ground,
                        outline = new List<P>(outline),
                        island = true,
                    }
                );
            }

            /// <summary>A one-way ledge with nothing under it. Not used in Paradise: a ledge there is a Deck.</summary>
            public void OneWay(string name, params P[] path)
            {
                ledges.Add(new Ledge { name = name, path = new List<P>(path) });
            }

            public void Mark(
                Descender.World.Paradise.DesignMarker.Kind kind,
                float x,
                float y,
                string label,
                string note = ""
            )
            {
                markers.Add(
                    new Marker
                    {
                        kind = kind,
                        label = label,
                        note = note,
                        at = new Vector2(x, y),
                    }
                );
            }

            public void Light(float x, float y, Color color, float radiusTiles, float energy)
            {
                lamps.Add(
                    new Lamp
                    {
                        at = new Vector2(x, y),
                        color = color,
                        radiusTiles = radiusTiles,
                        energy = energy,
                    }
                );
            }
        }

        public const string SceneFolder = "Assets/Scenes/Paradise";

        public static readonly Vector2 Start = new Vector2(12f, -62.4f);

        /// <summary>
        /// The abilities found along the region, west to east: Wings with the feather at the
        /// top of the Crown Vine (zone 3), the light dash on the Terraces (zone 5).
        /// ParadiseRules reads the jump limits at an x from these.
        /// </summary>
        public static readonly IReadOnlyList<Grant> Grants = new List<Grant>
        {
            new Grant(Ability.Wings, 2560f),
            new Grant(Ability.LightDash, 4700f),
        };

        public static List<Zone> All() => new List<Zone> { Zone1(), Zone2() };

        private static P S(float x, float y) => new P(x, y, true);

        private static P C(float x, float y) => new P(x, y);

        private static readonly Color Dawn = new Color(1f, 0.82f, 0.6f);
        private static readonly Color Rose = new Color(1f, 0.7f, 0.72f);
        private static readonly Color Fruit = new Color(0.55f, 1f, 0.92f);
        private static readonly Color Fungus = new Color(0.4f, 0.85f, 1f);

        /// <summary>
        /// Zone 1, the Waking Hollow (x 0 to 400): a sheltered bowl at dawn, a teaching
        /// run (seed pods to strike, a 3-tile gap, steps of 2 to 3.5, a 4-tile gap), the
        /// Rising Leaf (a 70-tile flowing ramp), a descent over canopy clumps to the first
        /// save shrine, and the way under the canopy. A root cave below the leaf hides a
        /// secret; a ledge on the first screen is out of reach until Wings.
        /// </summary>
        public static Zone Zone1()
        {
            var z = new Zone
            {
                number = 1,
                scene = "Paradise_Z1_WakingHollow",
                xMin = 0f,
                xMax = 400f,
            };
            z.rooms.Add(new Rect(0f, -105f, 400f, 90f));

            // The Hollow: the region's west wall curving down into a soft bowl.
            z.Ground(
                "Hollow",
                -100f,
                S(0f, -28f),
                S(3f, -29f),
                C(4f, -40f),
                C(5.5f, -52f),
                C(8f, -59f),
                C(12f, -62.8f),
                C(18f, -63.6f),
                C(24f, -63.4f),
                C(30f, -62.4f),
                C(36f, -61f),
                C(42f, -60.2f),
                C(50f, -60f),
                C(58f, -60f),
                C(66f, -60.2f),
                S(70f, -60.4f)
            );
            // A 3-tile gap, then the steps: 2, 3, 3.5 up, and a 4-tile gap off the top.
            z.Ground(
                "Steps",
                -100f,
                S(73f, -60.4f),
                C(80f, -60.6f),
                C(88f, -60.2f),
                C(96f, -60f),
                S(103f, -60f),
                S(103f, -58f),
                S(109f, -58f),
                S(109f, -55f),
                S(115f, -55f),
                S(115f, -51.5f),
                S(124f, -51.5f)
            );
            // The Rising Arcade: an inclined colonnade bridge, 12 tiles of climb over 74, its
            // columns standing on the lower garden below.
            z.Deck(
                "RisingArcade",
                6f,
                S(128f, -51.6f),
                C(140f, -50.5f),
                C(152f, -48.6f),
                C(164f, -46.4f),
                C(176f, -44f),
                C(188f, -41.6f),
                C(196f, -40f),
                S(202f, -38.8f)
            );
            // Under the leaf: the safe lower ground, a root cave with steps out, the
            // climb back up to the save shrine.
            z.Ground(
                "UnderLeaf",
                -100f,
                S(124f, -66f),
                C(140f, -66.4f),
                S(166f, -66f),
                S(166f, -78f),
                S(186f, -78f),
                S(186f, -74.5f),
                S(191f, -74.5f),
                S(191f, -71f),
                S(196f, -71f),
                S(196f, -67.5f),
                S(201f, -67.5f),
                C(206f, -66f),
                C(230f, -66.5f),
                C(260f, -66f),
                S(280f, -66f),
                S(280f, -62.75f),
                S(286f, -62.75f)
            );
            // A broken bridge stepping down from the arcade's end: three spans on their own
            // columns, with 2, 3 and 3-tile gaps where the old spans fell.
            z.Deck(
                "BrokenSpan1",
                6f,
                S(204f, -41.5f),
                C(210f, -42.5f),
                C(218f, -43.2f),
                C(226f, -43.8f),
                S(231f, -44.6f)
            );
            z.Deck(
                "BrokenSpan2",
                6f,
                S(234f, -45.6f),
                C(242f, -46.8f),
                C(250f, -47.4f),
                C(256f, -48.2f),
                S(261f, -49f)
            );
            z.Deck("BrokenSpan3", 6f, S(264f, -50f), C(272f, -51.2f), C(278f, -52.4f), S(283f, -53.6f));
            // The shrine terrace and the slope down under the canopy.
            z.Ground(
                "ShrineTerrace",
                -100f,
                S(286f, -59.5f),
                C(312f, -59.5f),
                C(330f, -60.2f),
                C(350f, -61.2f),
                C(370f, -62f),
                S(401f, -62.2f)
            );

            // A balcony on the first screen, 6.6 tiles up on two columns: needs Wings.
            z.Deck("WingsBalcony", 5f, S(26f, -56.8f), S(31f, -56.8f));

            // The canopy curl over the Hollow (behind, no collision).
            z.Closed(
                "HollowCurl",
                Profile.Decor,
                false,
                S(6f, -46f),
                C(14f, -44f),
                C(24f, -44.5f),
                C(34f, -47f),
                S(38f, -50f),
                C(33f, -49f),
                C(24f, -47.5f),
                C(14f, -47.6f),
                S(8f, -48.5f)
            );

            z.seeds.Add(new Vector2(46f, -59.4f));
            z.seeds.Add(new Vector2(54f, -59.4f));
            z.seeds.Add(new Vector2(90f, -59.6f));

            z.saves.Add(new Vector2(300f, -59.5f));
            z.MainPath("Hollow", "Steps", "RisingArcade", "BrokenSpan1", "BrokenSpan2", "BrokenSpan3", "ShrineTerrace");
            // The garden's built terraces; the Hollow and the lower garden are the cliff itself.
            z.Masonry("Steps", "ShrineTerrace");

            var k = Descender.World.Paradise.DesignMarker.Kind.Story;
            z.Mark(
                k,
                12f,
                -60f,
                "Beat 1: opening",
                "Ansel wakes half-made; the Voice tells him to go back. Trigger 'opening'."
            );
            z.Mark(
                Descender.World.Paradise.DesignMarker.Kind.Secret,
                28.5f,
                -55.6f,
                "Secret: Wings ledge",
                "Seen on the first screen, reached after Wings (zone 3)."
            );
            z.Mark(
                Descender.World.Paradise.DesignMarker.Kind.Secret,
                178f,
                -76.8f,
                "Secret: root cave",
                "A relic or upgrade in the cave under the Rising Arcade."
            );
            z.Mark(Descender.World.Paradise.DesignMarker.Kind.Landmark, 165f, -43f, "Landmark: the Rising Arcade");
            z.Mark(k, 392f, -60f, "Into the Orchard", "The canopy closes overhead; the light turns teal.");

            z.Light(14f, -54f, Dawn, 12f, 0.9f);
            z.Light(60f, -54f, Rose, 10f, 0.7f);
            z.Light(160f, -40f, Dawn, 14f, 0.8f);
            z.Light(240f, -42f, Rose, 12f, 0.7f);
            z.Light(300f, -55f, Dawn, 8f, 1f);
            return z;
        }

        /// <summary>
        /// Zone 2, the Orchard Under the Canopy (x 400 to 1300): Mereth's tree with the
        /// token in a root hollow below a 4-tile hole; orchard rows too evenly spaced; a
        /// bounce pod up to a canopy walk running above the floor; a fungi dell crossed
        /// by pods; the Lantern Tree with a save shrine in its roots and a spiral of
        /// branches up to a Wings-only secret; a flowing run over three 4-tile gaps with
        /// a roots route under them; the climb out to the Great Vines.
        /// </summary>
        public static Zone Zone2()
        {
            var z = new Zone
            {
                number = 2,
                scene = "Paradise_Z2_Orchard",
                xMin = 400f,
                xMax = 1300f,
            };
            z.rooms.Add(new Rect(400f, -105f, 900f, 90f));

            // Mereth's grove, the hole down to the root hollow at x 545 to 549.
            z.Closed(
                "Grove",
                Profile.Ground,
                true,
                S(399f, -62.2f),
                C(410f, -62f),
                C(425f, -61.6f),
                C(440f, -62f),
                C(460f, -62.4f),
                C(480f, -62f),
                C(500f, -62f),
                C(530f, -62f),
                S(545f, -62f),
                S(545f, -65.5f),
                S(535f, -65.5f),
                S(535f, -100f),
                S(399f, -100f)
            );
            z.Closed(
                "OrchardRows",
                Profile.Ground,
                true,
                S(549f, -62f),
                C(570f, -62.2f),
                C(600f, -62f),
                C(640f, -61.8f),
                C(680f, -62f),
                C(720f, -62f),
                C(740f, -62.4f),
                C(758f, -62f),
                C(764f, -64.5f),
                C(772f, -68f),
                C(785f, -70.2f),
                C(800f, -70.8f),
                C(820f, -70.6f),
                C(840f, -70.2f),
                S(858f, -70f),
                S(858f, -100f),
                S(575f, -100f),
                S(575f, -65.5f),
                S(549f, -65.5f)
            );
            z.Ground("RootHollowFloor", -100f, S(535f, -76f), C(555f, -76.4f), S(575f, -76f));
            // The dell's east wall, 12 tiles high: a big pod throws the hero over it.
            z.Ground(
                "LanternRise",
                -100f,
                S(858f, -58f),
                C(870f, -58.4f),
                C(885f, -60f),
                C(895f, -62.5f),
                C(905f, -64f),
                C(930f, -64.2f),
                S(945f, -63f)
            );
            // The flowing run: three arch bridges with 4-tile gaps in rhythm; the roots route
            // passes under their arches.
            z.Deck("Run1", 7f, S(945f, -63f), C(960f, -61.5f), C(972f, -60.5f), C(984f, -61.8f), S(994f, -62.4f));
            z.Deck("Run2", 7f, S(998f, -62f), C(1010f, -60.4f), C(1022f, -59.8f), C(1034f, -61f), S(1043f, -61.6f));
            z.Deck("Run3", 7f, S(1047f, -61.4f), C(1060f, -60f), C(1072f, -59.2f), C(1086f, -60.5f), S(1096f, -61.4f));
            z.Ground(
                "ExitClimb",
                -100f,
                S(1100f, -61f),
                C(1115f, -60f),
                C(1130f, -60.6f),
                C(1150f, -62f),
                S(1170f, -62f),
                S(1170f, -58.5f),
                S(1182f, -58.5f),
                S(1182f, -55f),
                S(1194f, -55f),
                S(1194f, -51.5f),
                C(1215f, -51.5f),
                C(1240f, -50.6f),
                C(1270f, -50f),
                S(1301f, -50f)
            );
            // The roots route under the run: a fall lands here, pods throw back up.
            z.Ground(
                "RootsRoute",
                -100f,
                S(945f, -80f),
                C(990f, -80.4f),
                C(1040f, -80f),
                C(1080f, -80.6f),
                S(1100f, -80f)
            );

            // The aqueduct walkway above the orchard, reached by the pod at x 608.
            z.Deck("Aqueduct1", 6f, S(600f, -47f), C(612f, -46.6f), S(626f, -46.5f));
            z.Deck("Aqueduct2", 6f, S(630f, -46.2f), C(642f, -45.8f), S(655f, -45.8f));
            z.Deck("Aqueduct3", 6f, S(659f, -45.6f), C(672f, -46f), S(684f, -46.4f));
            z.Deck("AqueductStair", 5f, S(688f, -47f), C(697f, -49.6f), S(706f, -53f));
            z.Deck("GardenWall", 5f, S(430f, -58.6f), S(441f, -58.4f));
            z.Deck("DellBridge", 6f, S(800f, -64f), S(812f, -64f));
            // The lantern tower: landings 4 up each on its columns, the crown 6.5 up (Wings).
            z.Deck("Landing1", 4f, S(898f, -60.5f), S(906f, -60.5f));
            z.Deck("Landing2", 4f, S(908f, -56.5f), S(916f, -56.5f));
            z.Deck("Landing3", 4f, S(918f, -52.5f), S(926f, -52.5f));
            z.Deck("Landing4", 4f, S(908f, -48.5f), S(916f, -48.5f));
            z.Deck("Landing5", 4f, S(898f, -44.5f), S(906f, -44.5f));
            z.Deck("LanternCrown", 3f, S(912f, -38f), S(918f, -38f));

            z.Bounce("HollowPod", 547f, -75.6f, 16f);
            z.Bounce("AqueductPod", 608f, -61.6f, 15f);
            z.Bounce("DellPod1", 790f, -70.4f, 6f);
            z.Bounce("DellPod2", 826f, -70.2f, 6f);
            z.Bounce("DellWallPod", 853f, -69.6f, 14f);
            z.Bounce("RootsPod1", 996f, -79.6f, 19f);
            z.Bounce("RootsPod2", 1045f, -79.6f, 19f);
            z.Bounce("RootsPod3", 1098f, -79.6f, 20f);

            // The straight run: over the hole by Mereth's tree, down into the dell, the big pod
            // over its east wall, the three arch bridges and the climb out.
            z.MainPath("Grove", "OrchardRows", "DellWallPod", "LanternRise", "Run1", "Run2", "Run3", "ExitClimb");
            z.saves.Add(new Vector2(590f, -62f));
            // The orchard stands on terraces; the root hollow and the roots route are the rock below.
            z.Masonry("Grove", "OrchardRows", "LanternRise", "ExitClimb");
            z.saves.Add(new Vector2(904f, -64f));
            z.saves.Add(new Vector2(1288f, -50f));

            // The orchard's trunks behind the play layer, evenly spaced: a little too tidy.
            for (var x = 586f; x <= 742f; x += 12f)
            {
                z.Closed(
                    $"Trunk{x:0}",
                    Profile.Decor,
                    false,
                    S(x, -30f),
                    S(x + 2.4f, -30f),
                    S(x + 2.6f, -62f),
                    S(x - 0.2f, -62f)
                );
                z.Light(x + 1.2f, -52f, Fruit, 5f, 0.7f);
            }
            z.Closed("LanternTrunk", Profile.Decor, false, S(912f, -24f), S(928f, -24f), S(931f, -64f), S(909f, -64f));
            // The canopy roof over the whole orchard.
            z.Closed(
                "CanopyRoof",
                Profile.Decor,
                false,
                S(380f, -22f),
                S(1180f, -22f),
                C(1160f, -34f),
                C(1100f, -31f),
                C(1000f, -35f),
                C(900f, -29f),
                C(800f, -36f),
                C(700f, -33f),
                C(600f, -37f),
                C(500f, -32f),
                S(400f, -36f)
            );

            var npc = Descender.World.Paradise.DesignMarker.Kind.Npc;
            var item = Descender.World.Paradise.DesignMarker.Kind.Item;
            var secret = Descender.World.Paradise.DesignMarker.Kind.Secret;
            var landmark = Descender.World.Paradise.DesignMarker.Kind.Landmark;
            z.Mark(
                npc,
                515f,
                -60.5f,
                "Beat 2: Mereth",
                "Tends a tree of tally-marked fruit. Tobiah went to the Low Gate 'talking the whole way'."
            );
            z.Mark(
                item,
                568f,
                -74.6f,
                "Beat 3: harvest token",
                "Tobiah's token, his tally scratched in. In the root hollow below Mereth."
            );
            z.Mark(landmark, 920f, -36f, "Landmark: the lantern tower");
            z.Mark(secret, 915f, -36.8f, "Secret: Lantern crown", "Wings only: the crown landing is 6.5 up.");
            z.Mark(secret, 1060f, -78.6f, "Secret: roots route", "Something to find under the run.");
            z.Mark(Descender.World.Paradise.DesignMarker.Kind.Story, 1296f, -48.5f, "Into the Great Vines");

            z.Light(515f, -55f, Fruit, 8f, 0.9f);
            z.Light(560f, -72f, Fungus, 9f, 0.8f);
            z.Light(805f, -66f, Fungus, 10f, 1f);
            z.Light(840f, -66f, Fungus, 8f, 0.9f);
            z.Light(920f, -38f, new Color(0.85f, 1f, 0.95f), 30f, 1.4f);
            z.Light(1020f, -78f, Fungus, 10f, 0.8f);
            for (var x = 960f; x <= 1280f; x += 40f)
            {
                z.Light(x, -53f, Fruit, 6f, 0.6f);
            }
            return z;
        }
    }
}
