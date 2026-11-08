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

        /// <summary>
        /// Thorns: a patch that hurts (Assets/Prefabs/Pickups/Hazard.prefab), its rect in
        /// world units. Beside the main path, never across it: ParadiseChecker reports a
        /// hazard whose rect meets the top edge of a main-path surface.
        /// </summary>
        public sealed class Hazard
        {
            public string name;
            public Rect rect;
        }

        /// <summary>
        /// A wall of angel light (LightBarrier): a one-tile column of `height` cells running
        /// down from `at`, solid unless the hero is light dashing. It closes a shortcut back,
        /// never the main path before the dash: ParadiseChecker reports one that does.
        /// </summary>
        public sealed class Barrier
        {
            public string name;
            public Vector2 at;
            public int height;

            /// <summary>The cells' rect: one wide, from `at` down.</summary>
            public Rect Rect => new Rect(at.x, at.y - height, 1f, height);
        }

        public sealed class Lamp
        {
            public Vector2 at;
            public Color color;
            public float radiusTiles;
            public float energy;
        }

        /// <summary>
        /// A story beat that is a thing in the world: a trigger, an NPC, a story item or a
        /// feather, built as a WorldPlacement beside the save shrines. The fields are the
        /// WorldPlacement's; `type` says which matter. `at` is where its feet stand.
        /// </summary>
        public sealed class Placement
        {
            /// <summary>"paradise/z&lt;zone&gt;/&lt;name&gt;", as the shrines are numbered; the sync finds a placement by it.</summary>
            public string id;

            /// <summary>trigger, npc, item or ability.</summary>
            public string type;
            public Vector2 at;
            public string[] dialogue = new string[0];
            public string[] rotate = new string[0];
            public string placementName = "";
            public string character = "";
            public bool facesLeft;
            public string item = "";
            public int abilityIndex = -1;
            public string unless = "";

            /// <summary>
            /// The label of the DesignMarker this placement stands in for, or "". A beat is a
            /// flag or a thing, never both: the marker is not built, and the sync removes it.
            /// Matched by label (BuildMarker names the object after it), so a moved marker is still found.
            /// </summary>
            public string replaces = "";

            /// <summary>The last part of the id: the object's name in the scene.</summary>
            public string Name => id.Substring(id.LastIndexOf('/') + 1);
        }

        /// <summary>The placement types a zone may declare.</summary>
        public static readonly IReadOnlyList<string> PlacementTypes = new[] { "trigger", "npc", "item", "ability" };

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

            /// <summary>The story beats that are things: built under Placements, and synced into a built scene by id.</summary>
            public readonly List<Placement> placements = new List<Placement>();
            public readonly List<Lamp> lamps = new List<Lamp>();
            public readonly List<Hazard> hazards = new List<Hazard>();
            public readonly List<Barrier> barriers = new List<Barrier>();
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

            /// <summary>
            /// A placement of `type` named `name` in this zone (its id is "paradise/z&lt;zone&gt;/&lt;name&gt;"),
            /// feet at x, y, in place of the marker labelled `replaces`. Returns it, to set the
            /// fields its type needs.
            /// </summary>
            public Placement Place(string name, string type, float x, float y, string replaces = "")
            {
                var placement = new Placement
                {
                    id = $"paradise/z{number}/{name}",
                    type = type,
                    at = new Vector2(x, y),
                    replaces = replaces,
                };
                placements.Add(placement);
                return placement;
            }

            /// <summary>The labels of the markers this zone's placements stand in for.</summary>
            public HashSet<string> ReplacedMarkers()
            {
                var labels = new HashSet<string>();
                foreach (var placement in placements)
                {
                    if (!string.IsNullOrEmpty(placement.replaces))
                    {
                        labels.Add(placement.replaces);
                    }
                }
                return labels;
            }

            /// <summary>Thorns `width` x `height` tiles with their bottom-left corner at (x, y).</summary>
            public void Thorns(string name, float x, float y, float width, float height)
            {
                hazards.Add(new Hazard { name = name, rect = new Rect(x, y, width, height) });
            }

            /// <summary>A light barrier whose top-left cell corner is (x, top), `height` cells tall.</summary>
            public void Barrier(string name, float x, float top, int height)
            {
                barriers.Add(
                    new Barrier
                    {
                        name = name,
                        at = new Vector2(x, top),
                        height = height,
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

        public static List<Zone> All() =>
            new List<Zone> { Zone1(), Zone2(), Zone3(), Zone4(), Zone5(), Zone6(), Zone7() };

        private static P S(float x, float y) => new P(x, y, true);

        private static P C(float x, float y) => new P(x, y);

        private static readonly Color Dawn = new Color(1f, 0.82f, 0.6f);
        private static readonly Color Rose = new Color(1f, 0.7f, 0.72f);
        private static readonly Color Fruit = new Color(0.55f, 1f, 0.92f);
        private static readonly Color Fungus = new Color(0.4f, 0.85f, 1f);
        private static readonly Color Acid = new Color(0.7f, 1f, 0.45f);
        private static readonly Color Gold = new Color(1f, 0.9f, 0.55f);
        private static readonly Color StreamGlow = new Color(0.75f, 0.95f, 1f);
        private static readonly Color Marble = new Color(1f, 0.97f, 0.88f);
        private static readonly Color Pearl = new Color(0.9f, 0.92f, 0.95f);
        private static readonly Color Steel = new Color(0.6f, 0.7f, 0.85f);

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
            // The beat itself: the trigger stands on the start, so the Voice speaks as he wakes.
            z.Place("opening", "trigger", Start.x, -62.8f, "Beat 1: opening").dialogue = new[] { "opening" };
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
            // The beats themselves, feet on the Grove's walk and on the root hollow's floor.
            var mereth = z.Place("mereth", "npc", 515f, -62f, "Beat 2: Mereth");
            mereth.placementName = "Mereth";
            mereth.character = "mereth";
            mereth.facesLeft = true; // her art faces left, as in the prototype
            mereth.dialogue = new[] { "mereth_intro" };
            mereth.rotate = new[] { "mereth_again", "mereth_count", "mereth_orchard" };
            var token = z.Place("token", "item", 568f, -76.2f, "Beat 3: harvest token");
            token.item = "token";
            token.dialogue = new[] { "item_token" };
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

        /// <summary>
        /// Zone 3, the Great Vines (x 1300 to 2600): two flowing runs of curved branches
        /// over a moss floor with the first thorns; two hollow-trunk climbs by pod and
        /// landing; air lanes between the vines for the flyers to come; and the Crown Vine,
        /// a 50-tile climb to the feather, with the last shrine of the zone at its top. The
        /// Wings descent off the top is the start of zone 4.
        /// </summary>
        public static Zone Zone3()
        {
            var z = new Zone
            {
                number = 3,
                scene = "Paradise_Z3_GreatVines",
                xMin = 1300f,
                xMax = 2600f,
            };
            // The Crown Vine tops out near y -12, so the room reaches almost to the band's top.
            z.rooms.Add(new Rect(1300f, -105f, 1300f, 100f));

            // Out of the orchard: the last terrace sloping down to the first branch.
            z.Ground(
                "VineGate",
                -100f,
                S(1299f, -50f),
                C(1316f, -50.6f),
                C(1334f, -52.4f),
                C(1352f, -54.8f),
                C(1368f, -56.6f),
                S(1380f, -57f)
            );
            // The first flowing run: three branches rising and falling like waves, 4-tile gaps
            // in rhythm, over the moss floor.
            z.Deck(
                "Wave1",
                8f,
                S(1384f, -57.2f),
                C(1396f, -54.6f),
                C(1408f, -52.8f),
                C(1420f, -53.6f),
                C(1432f, -56.2f),
                C(1444f, -58.6f),
                S(1456f, -59.4f)
            );
            z.Deck(
                "Wave2",
                8f,
                S(1460f, -59.6f),
                C(1472f, -57f),
                C(1484f, -55f),
                C(1496f, -56f),
                C(1508f, -58.6f),
                S(1520f, -60.2f)
            );
            z.Deck("Wave3", 8f, S(1524f, -60.4f), C(1536f, -57.6f), C(1548f, -55.8f), C(1560f, -57.4f), S(1572f, -60f));
            // The moss floor under the run: deep and soft, with the first thorns. Pods throw
            // a fallen hero back up to the branches.
            z.Closed(
                "VineFloor",
                Profile.Leaf,
                true,
                S(1380f, -80f),
                C(1420f, -80.4f),
                C(1460f, -80f),
                C(1500f, -80.6f),
                C(1540f, -80f),
                S(1576f, -80f),
                S(1576f, -100f),
                S(1380f, -100f)
            );
            z.Thorns("Thorns1", 1400f, -80.2f, 12f, 1.5f);
            z.Thorns("Thorns2", 1504f, -80.4f, 10f, 1.5f);
            z.Bounce("FloorPod1", 1470f, -79.6f, 22f);
            z.Bounce("FloorPod2", 1540f, -79.6f, 22f);
            // The first hollow trunk: a pod throws the hero to the landings inside it.
            z.Ground(
                "TrunkFoot",
                -100f,
                S(1576f, -60.6f),
                C(1590f, -61f),
                C(1610f, -61.2f),
                C(1630f, -61f),
                S(1644f, -61f)
            );
            z.Bounce("TrunkPod", 1636f, -60.6f, 16f);
            z.Deck("TrunkLanding1", 4f, S(1626f, -46f), S(1636f, -46f));
            z.Deck("TrunkLanding2", 4f, S(1640f, -42f), S(1650f, -42f));
            z.Deck("TrunkLanding3", 4f, S(1654f, -38.2f), S(1664f, -38.2f));
            z.Closed("Trunk1", Profile.Decor, false, S(1618f, -30f), S(1672f, -30f), S(1676f, -62f), S(1614f, -62f));
            // The high branch down from the trunk to the first shrine.
            z.Deck("HighWave1", 8f, S(1668f, -38.6f), C(1680f, -41f), C(1692f, -44f), S(1704f, -46.6f));
            z.Deck("HighWave2", 8f, S(1708f, -47.2f), C(1720f, -50f), C(1732f, -53.4f), S(1744f, -56.4f));
            // A nest above the high branch, 7.6 up: Wings only.
            z.Deck("CanopyNest", 4f, S(1690f, -31f), S(1700f, -31f));
            z.Ground(
                "ShrineRoot",
                -100f,
                S(1748f, -57.4f),
                C(1770f, -58f),
                C(1800f, -58.6f),
                C(1830f, -59f),
                S(1850f, -59.2f)
            );
            // The second flowing run, over a second moss floor.
            z.Deck(
                "Wave4",
                8f,
                S(1854f, -59.4f),
                C(1866f, -56.6f),
                C(1878f, -54.6f),
                C(1890f, -55.6f),
                C(1902f, -58.4f),
                S(1914f, -60f)
            );
            z.Deck(
                "Wave5",
                8f,
                S(1918f, -60.2f),
                C(1930f, -57.4f),
                C(1942f, -55.4f),
                C(1954f, -56.6f),
                C(1966f, -59f),
                S(1978f, -60.6f)
            );
            z.Deck(
                "Wave6",
                8f,
                S(1982f, -60.8f),
                C(1994f, -58f),
                C(2006f, -56f),
                C(2018f, -57f),
                C(2030f, -59.4f),
                S(2042f, -61f)
            );
            z.Closed(
                "VineFloor2",
                Profile.Leaf,
                true,
                S(1850f, -80f),
                C(1900f, -80.4f),
                C(1950f, -80f),
                C(2000f, -80.4f),
                S(2046f, -80f),
                S(2046f, -100f),
                S(1850f, -100f)
            );
            z.Thorns("Thorns3", 1930f, -80.2f, 12f, 1.5f);
            z.Thorns("Thorns4", 2010f, -80.3f, 8f, 1.5f);
            z.Bounce("FloorPod3", 1900f, -79.6f, 22f);
            z.Bounce("FloorPod4", 2000f, -79.6f, 22f);
            z.Ground(
                "MidRoot",
                -100f,
                S(2046f, -61.2f),
                C(2070f, -61.6f),
                C(2100f, -61.4f),
                C(2140f, -61.8f),
                C(2180f, -61.6f),
                S(2200f, -61.5f)
            );
            // The second hollow trunk, up to a canopy walk with the second shrine.
            z.Bounce("Trunk2Pod", 2192f, -61.1f, 14f);
            z.Deck("Trunk2Landing1", 4f, S(2186f, -49f), S(2196f, -49f));
            z.Deck("Trunk2Landing2", 4f, S(2200f, -45f), S(2210f, -45f));
            z.Closed("Trunk2", Profile.Decor, false, S(2180f, -36f), S(2218f, -36f), S(2222f, -62f), S(2176f, -62f));
            z.Deck("CanopyWalk", 8f, S(2214f, -45.4f), C(2230f, -46f), C(2250f, -46.4f), S(2270f, -47f));
            z.Deck("CanopyDrop", 8f, S(2274f, -50.6f), C(2286f, -53f), S(2298f, -55.6f));
            // The root bed of the Crown Vine: deep moss, so a fall from the top is soft.
            z.Closed(
                "CrownRoot",
                Profile.Leaf,
                true,
                S(2302f, -57f),
                C(2330f, -58.4f),
                C(2360f, -60f),
                C(2400f, -61.6f),
                C(2440f, -62.4f),
                C(2480f, -63f),
                C(2520f, -62.6f),
                C(2560f, -62.5f),
                S(2601f, -62.6f),
                S(2601f, -100f),
                S(2302f, -100f)
            );
            // The Crown Vine: two pod throws and five landings inside the hollow trunk, 50
            // tiles from the root bed to the top, where the feather waits.
            z.Bounce("CrownPod1", 2486f, -62.6f, 18f);
            z.Deck("CrownLanding1", 4f, S(2476f, -46f), S(2486f, -46f));
            z.Deck("CrownLanding2", 4f, S(2490f, -42f), S(2500f, -42f));
            z.Deck("CrownLanding3", 4f, S(2504f, -38f), S(2514f, -38f));
            z.Bounce("CrownPod2", 2509f, -37.6f, 18f);
            z.Deck("CrownLanding4", 4f, S(2498f, -21.2f), S(2508f, -21.2f));
            z.Deck("CrownLanding5", 4f, S(2512f, -17.2f), S(2522f, -17.2f));
            z.Deck("CrownTop", 8f, S(2526f, -13.2f), C(2550f, -12.6f), C(2575f, -12.4f), S(2600f, -12.4f));
            z.Closed("CrownTrunk", Profile.Decor, false, S(2470f, -8f), S(2530f, -8f), S(2536f, -63f), S(2464f, -63f));

            z.MainPath(
                "VineGate",
                "Wave1",
                "Wave2",
                "Wave3",
                "TrunkFoot",
                "TrunkPod",
                "TrunkLanding1",
                "TrunkLanding2",
                "TrunkLanding3",
                "HighWave1",
                "HighWave2",
                "ShrineRoot",
                "Wave4",
                "Wave5",
                "Wave6",
                "MidRoot",
                "Trunk2Pod",
                "Trunk2Landing1",
                "Trunk2Landing2",
                "CanopyWalk",
                "CanopyDrop",
                "CrownRoot",
                "CrownPod1",
                "CrownLanding1",
                "CrownLanding2",
                "CrownLanding3",
                "CrownPod2",
                "CrownLanding4",
                "CrownLanding5",
                "CrownTop"
            );
            z.Soft("VineFloor", "VineFloor2", "CrownRoot");
            z.Masonry("VineGate");
            z.saves.Add(new Vector2(1756f, -57.6f));
            z.saves.Add(new Vector2(2250f, -46.4f));
            z.saves.Add(new Vector2(2580f, -12.4f));

            var story = Descender.World.Paradise.DesignMarker.Kind.Story;
            var secret = Descender.World.Paradise.DesignMarker.Kind.Secret;
            var encounter = Descender.World.Paradise.DesignMarker.Kind.Encounter;
            var landmark = Descender.World.Paradise.DesignMarker.Kind.Landmark;
            z.Mark(
                encounter,
                1500f,
                -44f,
                "Encounter: air lane",
                "A flying lane between the vines, above the first run."
            );
            z.Mark(encounter, 1950f, -44f, "Encounter: air lane 2", "A second lane over the second run.");
            z.Mark(secret, 1480f, -79f, "Secret: thorn hollow", "Between the thorns on the moss floor.");
            z.Mark(secret, 1695f, -30f, "Secret: canopy nest", "Wings only: 7.6 above the high branch.");
            z.Mark(secret, 2240f, -79f, "Secret: trunk hollow", "Under the canopy walk, by the second trunk.");
            z.Mark(landmark, 2500f, -30f, "Landmark: the Crown Vine", "Five screens tall; the feather is at its top.");
            z.Mark(
                Descender.World.Paradise.DesignMarker.Kind.Item,
                2560f,
                -11f,
                "Beat 4: feather",
                "The feather at the top of the Crown Vine: Wings. Jump off the top into the descent."
            );
            var feather = z.Place("feather", "ability", 2560f, -12.5f, "Beat 4: feather");
            feather.abilityIndex = (int)Ability.Wings;
            z.Mark(
                story,
                2594f,
                -11f,
                "Into the Fields",
                "Out from under the trees into the open gold of the meadows."
            );

            z.Light(1420f, -48f, Acid, 14f, 0.8f);
            z.Light(1480f, -76f, Fungus, 10f, 0.8f);
            z.Light(1645f, -40f, Acid, 10f, 0.8f);
            z.Light(1756f, -53f, Dawn, 8f, 1f);
            z.Light(1950f, -50f, Acid, 14f, 0.8f);
            z.Light(1960f, -76f, Fungus, 10f, 0.8f);
            z.Light(2200f, -42f, Acid, 10f, 0.8f);
            z.Light(2250f, -42f, Dawn, 8f, 1f);
            z.Light(2500f, -30f, Acid, 18f, 0.9f);
            z.Light(2570f, -8f, Dawn, 14f, 1.2f);
            return z;
        }

        /// <summary>
        /// Zone 4, the Tallied Fields (x 2600 to 3700): the Wings descent off the Crown Vine
        /// over five islands onto a moss bed; open golden meadows cut by glowing streams
        /// with 7-tile gaps; sky islands only Wings reach; two wide patrol grounds; the
        /// Sundial; the field-keeper's hut with the ledger page; Ithuriel at the Field
        /// Shrine; and the gust-return marker at the west edge.
        /// </summary>
        public static Zone Zone4()
        {
            var z = new Zone
            {
                number = 4,
                scene = "Paradise_Z4_TalliedFields",
                xMin = 2600f,
                xMax = 3700f,
            };
            z.rooms.Add(new Rect(2600f, -105f, 1100f, 100f));

            // The descent: five islands stepping down 8 at a time with 6-tile gaps, built for
            // the new double jump, over a soft moss bed.
            Descent(z, "Descent1", 2606f, -16f);
            Descent(z, "Descent2", 2626f, -24f);
            Descent(z, "Descent3", 2646f, -32f);
            Descent(z, "Descent4", 2666f, -40f);
            Descent(z, "Descent5", 2686f, -48f);
            z.Closed(
                "MossBed",
                Profile.Leaf,
                true,
                S(2599f, -62.6f),
                C(2640f, -63.2f),
                C(2680f, -62.8f),
                S(2700f, -62.4f),
                S(2700f, -100f),
                S(2599f, -100f)
            );
            // The west meadow: the first patrol ground, and the gust that carries the hero back.
            z.Ground(
                "WestMeadow",
                -100f,
                S(2700f, -56.4f),
                C(2730f, -56.8f),
                C(2760f, -57.6f),
                C(2800f, -58f),
                S(2820f, -58f)
            );
            // The glowing streams: a 7-tile gap each, the water below safe to fall into.
            Stream(z, "Stream1", 2820f, -58f);
            z.Ground("Bank1", -100f, S(2827f, -58.2f), C(2850f, -58.6f), C(2880f, -58.4f), S(2900f, -58.4f));
            Stream(z, "Stream2", 2900f, -58.4f);
            z.Ground("Bank2", -100f, S(2907f, -58.6f), C(2940f, -59f), C(2980f, -58.6f), S(3000f, -58.4f));
            Stream(z, "Stream3", 3000f, -58.4f);
            // Sky islands 7.6 above the banks: Wings only.
            z.Island("SkyIsland1", S(2850f, -51f), S(2862f, -51f), S(2862f, -54f), S(2850f, -54f));
            z.Island("SkyIsland2", S(2944f, -51.4f), S(2958f, -51.4f), S(2958f, -54.4f), S(2944f, -54.4f));
            // The Sundial's field: the dial, the keeper's hut with the ledger, the Field Shrine.
            z.Ground(
                "SundialField",
                -100f,
                S(3007f, -58.4f),
                C(3040f, -58.6f),
                C(3080f, -58.4f),
                C(3120f, -58.2f),
                C(3160f, -58f),
                S(3200f, -58.2f)
            );
            z.Closed(
                "SundialPlate",
                Profile.Decor,
                false,
                S(3010f, -56f),
                S(3060f, -56f),
                S(3060f, -58.6f),
                S(3010f, -58.6f)
            );
            z.Closed(
                "SundialGnomon",
                Profile.Decor,
                false,
                S(3030f, -40f),
                S(3034f, -40f),
                S(3046f, -56f),
                S(3030f, -56f)
            );
            z.Closed(
                "KeepersHut",
                Profile.Decor,
                false,
                S(3086f, -50f),
                S(3100f, -50f),
                S(3100f, -58.4f),
                S(3086f, -58.4f)
            );
            z.Closed("KeepersRoof", Profile.Decor, false, S(3084f, -49.6f), S(3093f, -46f), S(3102f, -49.6f));
            z.Deck("HutLoft", 4f, S(3088f, -49.6f), S(3098f, -49.6f));
            Stream(z, "Stream4", 3200f, -58.2f);
            // The east fields: the second patrol ground, two more streams, the last shrine.
            z.Ground(
                "EastField1",
                -100f,
                S(3207f, -58.4f),
                C(3250f, -58.8f),
                C(3300f, -58.6f),
                C(3350f, -58.8f),
                S(3400f, -58.6f)
            );
            Stream(z, "Stream5", 3400f, -58.6f);
            z.Ground("EastField2", -100f, S(3407f, -59f), C(3450f, -59.4f), C(3500f, -59.6f), S(3540f, -59.6f));
            Stream(z, "Stream6", 3540f, -59.6f);
            z.Ground("EastMeadow", -100f, S(3547f, -60f), C(3600f, -60.2f), C(3650f, -60.2f), S(3701f, -60f));

            z.MainPath(
                "Descent1",
                "Descent2",
                "Descent3",
                "Descent4",
                "Descent5",
                "WestMeadow",
                "Bank1",
                "Bank2",
                "SundialField",
                "EastField1",
                "EastField2",
                "EastMeadow"
            );
            z.Soft("MossBed");
            z.Masonry("SundialField");
            z.saves.Add(new Vector2(3150f, -58.05f));
            z.saves.Add(new Vector2(3650f, -60.2f));

            var story = Descender.World.Paradise.DesignMarker.Kind.Story;
            var npc = Descender.World.Paradise.DesignMarker.Kind.Npc;
            var item = Descender.World.Paradise.DesignMarker.Kind.Item;
            var secret = Descender.World.Paradise.DesignMarker.Kind.Secret;
            var encounter = Descender.World.Paradise.DesignMarker.Kind.Encounter;
            var landmark = Descender.World.Paradise.DesignMarker.Kind.Landmark;
            z.Mark(
                story,
                2712f,
                -55f,
                "Gust return",
                "A gust carries the hero back up the Crown Vine for the backtrack (the mechanic comes later; marker only)."
            );
            z.Mark(
                encounter,
                2760f,
                -56.8f,
                "Encounter: patrol ground (west)",
                "A wide flat stretch for the angel guards to come."
            );
            z.Mark(secret, 2856f, -50f, "Secret: sky island", "Wings only: 7.6 above the bank.");
            z.Mark(secret, 2903f, -63f, "Secret: stream cave", "Under the second stream, between the banks.");
            z.Mark(secret, 2951f, -50.4f, "Secret: sky island 2", "Wings only: 7.6 above the second bank.");
            z.Mark(landmark, 3036f, -38f, "Landmark: the Sundial", "A huge stone sundial whose shadow never moves.");
            z.Mark(item, 3093f, -56.5f, "Beat 5: ledger page", "The tally ledger's page, in the field-keeper's hut.");
            var ledger = z.Place("ledger", "item", 3093f, -58.4f, "Beat 5: ledger page");
            ledger.item = "ledger";
            ledger.dialogue = new[] { "item_ledger" };
            z.Mark(secret, 3093f, -48.6f, "Secret: hut loft", "Wings: the loft over the keeper's hut.");
            z.Mark(npc, 3156f, -56.5f, "Beat 6: Ithuriel", "Sits by the Field Shrine, humming Tobiah's song.");
            var ithuriel = z.Place("ithuriel", "npc", 3156f, -58.05f, "Beat 6: Ithuriel");
            ithuriel.placementName = "Ithuriel";
            ithuriel.character = "ithuriel";
            ithuriel.facesLeft = true; // faces the shrine
            ithuriel.dialogue = new[] { "ithuriel_intro" };
            ithuriel.rotate = new[] { "ithuriel_again", "ithuriel_song", "ithuriel_orders" };
            z.Mark(encounter, 3300f, -58.6f, "Encounter: patrol ground (east)", "The second patrol ground.");
            z.Mark(story, 3692f, -59f, "Into the Terraces", "The meadows end at the first terrace wall.");

            z.Light(2650f, -40f, Gold, 20f, 0.7f);
            z.Light(2760f, -50f, Gold, 16f, 0.7f);
            z.Light(3036f, -44f, Gold, 14f, 0.8f);
            z.Light(3093f, -54f, Dawn, 8f, 0.9f);
            z.Light(3150f, -54f, Dawn, 8f, 1f);
            z.Light(3300f, -50f, Gold, 16f, 0.7f);
            z.Light(3650f, -56f, Dawn, 8f, 1f);
            return z;
        }

        /// <summary>
        /// Zone 5, the Terraces (x 3700 to 5100): two flights of marble terraces climbing 42
        /// tiles in steps of at most 7 to the upper gardens, with two shortcut ledges back
        /// down closed by light barriers; the Wheel mural on the cliff behind the climb;
        /// gardens cut by Wings gaps with the three items and two encounter landings; the
        /// light dash at x 4700, then the first 8 and 9 tile gaps over a pool of light.
        /// </summary>
        public static Zone Zone5()
        {
            var z = new Zone
            {
                number = 5,
                scene = "Paradise_Z5_Terraces",
                xMin = 3700f,
                xMax = 5100f,
            };
            z.rooms.Add(new Rect(3700f, -105f, 1400f, 100f));

            z.Ground("TerraceFoot", -100f, S(3699f, -60f), C(3730f, -60.2f), S(3760f, -60.4f));
            // The first flight: four steps of 6.4 to 6.5, then the first landing.
            z.Ground(
                "Terraces1",
                -100f,
                S(3764f, -60.4f),
                S(3780f, -60.4f),
                S(3780f, -54f),
                S(3800f, -54f),
                S(3800f, -47.5f),
                S(3820f, -47.5f),
                S(3820f, -41f),
                S(3845f, -41f),
                S(3845f, -34.5f),
                S(3930f, -34.5f)
            );
            // The second flight to the upper gardens, 42 tiles above the foot.
            z.Ground(
                "Terraces2",
                -100f,
                S(3936f, -34.5f),
                S(3960f, -34.5f),
                S(3960f, -28.5f),
                S(3985f, -28.5f),
                S(3985f, -22f),
                S(4010f, -22f),
                S(4010f, -18f),
                S(4040f, -18f),
                C(4080f, -18.2f),
                S(4120f, -18f)
            );
            // Shortcut ledges back down the flights, each closed by a light barrier: the hero
            // passes them only once he can dash, on the way back.
            z.Deck("ShortcutLedge1", 6f, S(3826f, -30f), S(3860f, -30f));
            z.Barrier("Barrier1", 3840f, -26f, 4);
            z.Deck("ShortcutLedge2", 6f, S(3975f, -14f), S(4006f, -14f));
            z.Barrier("Barrier2", 3990f, -10f, 4);
            // The Wheel mural: the whole cliff behind the climb.
            z.Closed(
                "WheelMural",
                Profile.Decor,
                false,
                S(3970f, -6f),
                C(4010f, -4f),
                C(4050f, -6f),
                S(4066f, -26f),
                C(4050f, -46f),
                C(4010f, -48f),
                C(3970f, -46f),
                S(3954f, -26f)
            );
            // The upper gardens: Wings gaps of 7 over falls of light.
            z.Ground(
                "Garden1",
                -100f,
                S(4127f, -18.4f),
                C(4160f, -18.4f),
                C(4200f, -18.5f),
                C(4230f, -18.6f),
                S(4260f, -18.6f)
            );
            z.Ground(
                "Garden2",
                -100f,
                S(4267f, -18.8f),
                C(4300f, -18.8f),
                C(4330f, -18.9f),
                C(4370f, -19f),
                S(4400f, -19f)
            );
            z.Ground(
                "Garden3",
                -100f,
                S(4407f, -19.2f),
                C(4440f, -19.3f),
                C(4480f, -19.4f),
                C(4520f, -19.5f),
                S(4560f, -19.6f)
            );
            z.Ground(
                "DashTerrace",
                -100f,
                S(4566f, -19.8f),
                C(4600f, -19.9f),
                C(4650f, -20f),
                C(4700f, -20f),
                S(4720f, -20f)
            );
            // The first dash gaps: 9, 9 and 8 tiles over a pool of light, soft to fall into,
            // with a pod back up.
            z.Island("DashIsle1", S(4729f, -20f), S(4745f, -20f), S(4745f, -24f), S(4729f, -24f));
            z.Island("DashIsle2", S(4754f, -20f), S(4770f, -20f), S(4770f, -24f), S(4754f, -24f));
            z.Closed("LightPool", Profile.Leaf, true, S(4720f, -40f), S(4778f, -40f), S(4778f, -100f), S(4720f, -100f));
            z.Bounce("PoolPod", 4749f, -39.6f, 22f);
            z.Ground(
                "EastTerrace",
                -100f,
                S(4778f, -20.4f),
                C(4850f, -20.6f),
                C(4950f, -20.4f),
                C(5050f, -20.2f),
                S(5101f, -20f)
            );

            z.MainPath(
                "TerraceFoot",
                "Terraces1",
                "Terraces2",
                "Garden1",
                "Garden2",
                "Garden3",
                "DashTerrace",
                "DashIsle1",
                "DashIsle2",
                "EastTerrace"
            );
            z.Soft("LightPool");
            z.Masonry(
                "TerraceFoot",
                "Terraces1",
                "Terraces2",
                "Garden1",
                "Garden2",
                "Garden3",
                "DashTerrace",
                "EastTerrace"
            );
            z.saves.Add(new Vector2(4200f, -18.5f));
            z.saves.Add(new Vector2(4694f, -20f));

            var story = Descender.World.Paradise.DesignMarker.Kind.Story;
            var item = Descender.World.Paradise.DesignMarker.Kind.Item;
            var secret = Descender.World.Paradise.DesignMarker.Kind.Secret;
            var encounter = Descender.World.Paradise.DesignMarker.Kind.Encounter;
            var landmark = Descender.World.Paradise.DesignMarker.Kind.Landmark;
            z.Mark(encounter, 3900f, -33.5f, "Encounter: first landing", "A terrace landing sized for a guard fight.");
            z.Mark(
                landmark,
                4010f,
                -26f,
                "Landmark: the Wheel mural",
                "The biggest single image in the region, seen from below."
            );
            z.Mark(
                secret,
                3843f,
                -29f,
                "Secret: the first shortcut",
                "Behind the first barrier: dash through from the east."
            );
            z.Mark(
                secret,
                3990f,
                -13f,
                "Secret: the second shortcut",
                "Behind the second barrier, above the upper flight."
            );
            z.Mark(item, 4060f, -16.5f, "Item: tally stone", "The child's tally stone, dropped on the upper terrace.");
            var tally = z.Place("tally_stone", "item", 4060f, -18.1f, "Item: tally stone");
            tally.item = "tally_stone";
            tally.dialogue = new[] { "item_tally_stone" };
            z.Mark(
                story,
                4300f,
                -17f,
                "Beat 9: the gathered",
                "The gathered walk past the hero without looking at him."
            );
            z.Mark(
                item,
                4330f,
                -17.5f,
                "Item: prayer card",
                "The Warden's prayer card: 'What is pressed is not lost.'"
            );
            var card = z.Place("prayer_card", "item", 4330f, -18.9f, "Item: prayer card");
            card.item = "prayer_card";
            card.dialogue = new[] { "item_prayer_card" };
            z.Mark(encounter, 4400f, -18f, "Encounter: second landing", "The wide garden before the glove.");
            z.Mark(item, 4480f, -18f, "Item: the Maker's glove", "Left on the garden wall.");
            var glove = z.Place("glove", "item", 4480f, -19.4f, "Item: the Maker's glove");
            glove.item = "glove";
            glove.dialogue = new[] { "item_glove" };
            z.Mark(item, 4700f, -18.5f, "Beat: light dash", "The light dash, found halfway up the Terraces.");
            var dash = z.Place("light_dash", "ability", 4700f, -20f, "Beat: light dash");
            dash.abilityIndex = (int)Ability.LightDash;
            z.Mark(secret, 4749f, -38.5f, "Secret: the pool of light", "Under the dash gaps.");
            z.Mark(
                story,
                5092f,
                -19f,
                "Into the Making-house Approach",
                "The colour drains to pearl; pipes among the roots."
            );

            z.Light(3800f, -44f, Marble, 14f, 0.8f);
            z.Light(3900f, -28f, Dawn, 10f, 0.8f);
            z.Light(4010f, -26f, Marble, 24f, 1f);
            z.Light(4200f, -14f, Dawn, 8f, 1f);
            z.Light(4330f, -14f, Marble, 12f, 0.7f);
            z.Light(4694f, -16f, Dawn, 8f, 1f);
            z.Light(4749f, -36f, StreamGlow, 12f, 1f);
            z.Light(4950f, -16f, Marble, 14f, 0.7f);
            return z;
        }

        /// <summary>
        /// Zone 6, the Making-house Approach (x 5100 to 6300): pipe decks over a soft root
        /// bed with the dash gaps of 8 and 9, the machinery's surfaces (moving platforms, a
        /// steam vent, a shutter) built static with "Machinery (M3)" markers, the Making-house
        /// and its white door behind the whole zone, the trigger where the door comes into
        /// view, and the drain below with the basin shard.
        /// </summary>
        public static Zone Zone6()
        {
            var z = new Zone
            {
                number = 6,
                scene = "Paradise_Z6_MakingHouse",
                xMin = 5100f,
                xMax = 6300f,
            };
            z.rooms.Add(new Rect(5100f, -105f, 1200f, 100f));

            z.Ground("ApproachGarden", -100f, S(5099f, -20f), C(5140f, -20.4f), S(5180f, -20.6f));
            z.Deck("Pipe1", 8f, S(5188f, -21f), C(5220f, -20.6f), S(5250f, -21f));
            Machinery(z, "Platform1", 5259f, -20f, 12f, "moving platform");
            z.Deck("Pipe2", 8f, S(5280f, -21f), C(5330f, -20f), S(5380f, -21.4f));
            // A steam vent lifts the hero 7: a static step here.
            Machinery(z, "Vent1", 5386f, -14.4f, 10f, "steam vent");
            z.Deck("Pipe3", 8f, S(5404f, -15f), C(5460f, -14.6f), S(5520f, -15f));
            Machinery(z, "Shutter1", 5528f, -15.4f, 12f, "timed shutter");
            z.Deck("Pipe4", 8f, S(5548f, -16f), C(5600f, -15.6f), S(5660f, -16.4f));
            Machinery(z, "Platform2", 5669f, -17f, 12f, "moving platform");
            z.Ground(
                "DoorApproach",
                -100f,
                S(5690f, -18f),
                C(5750f, -18.4f),
                C(5800f, -18.6f),
                S(5860f, -18.8f)
            );
            z.Deck("Pipe5", 8f, S(5868f, -19f), C(5920f, -18.6f), S(5980f, -19.4f));
            z.Ground("ExitPipe", -100f, S(5989f, -20f), C(6100f, -20.4f), C(6200f, -20.6f), S(6301f, -21f));
            // The root bed under the pipes: soft, 22 below, with pods back up.
            z.Closed(
                "RootBed",
                Profile.Leaf,
                true,
                S(5180f, -42f),
                C(5400f, -42.4f),
                C(5600f, -42f),
                S(5690f, -42f),
                S(5690f, -100f),
                S(5180f, -100f)
            );
            z.Bounce("BedPod1", 5230f, -41.6f, 22f);
            z.Bounce("BedPod2", 5330f, -41.6f, 22f);
            z.Bounce("BedPod3", 5600f, -41.6f, 27f);
            z.Bounce("BedPod4", 5750f, -41.6f, 25f);
            // The drain below the pipes: a side path to the basin shard, water at the bottom.
            z.Closed(
                "Drain",
                Profile.Leaf,
                true,
                S(5860f, -50f),
                C(5900f, -50.4f),
                C(5940f, -50.2f),
                S(5989f, -50f),
                S(5989f, -100f),
                S(5860f, -100f)
            );
            z.Deck("DrainLedge", 4f, S(5900f, -34f), S(5912f, -34f));
            z.Bounce("DrainPod1", 5890f, -49.6f, 17f);
            z.Bounce("DrainPod2", 5906f, -33.6f, 16f);
            // The pipes behind, all running one way, and the white door of the Making-house.
            z.Closed("PipeRun1", Profile.Decor, false, S(5100f, -30f), S(6200f, -30f), S(6200f, -32f), S(5100f, -32f));
            z.Closed(
                "PipeRun2",
                Profile.Decor,
                false,
                S(5100f, -36f),
                S(6200f, -36f),
                S(6200f, -37.5f),
                S(5100f, -37.5f)
            );
            z.Closed("WhiteDoor", Profile.Decor, false, S(6180f, -2f), S(6232f, -2f), S(6232f, -21f), S(6180f, -21f));

            z.MainPath(
                "ApproachGarden",
                "Pipe1",
                "Platform1",
                "Pipe2",
                "Vent1",
                "Pipe3",
                "Shutter1",
                "Pipe4",
                "Platform2",
                "DoorApproach",
                "Pipe5",
                "ExitPipe"
            );
            z.Soft("RootBed", "Drain");
            z.Masonry("ApproachGarden", "DoorApproach", "ExitPipe");
            z.saves.Add(new Vector2(5250f, -21f));
            z.saves.Add(new Vector2(5800f, -18.6f));

            var story = Descender.World.Paradise.DesignMarker.Kind.Story;
            var item = Descender.World.Paradise.DesignMarker.Kind.Item;
            var secret = Descender.World.Paradise.DesignMarker.Kind.Secret;
            var landmark = Descender.World.Paradise.DesignMarker.Kind.Landmark;
            z.Mark(
                landmark,
                5700f,
                -8f,
                "Landmark: the Making-house",
                "In the background for the whole zone, growing closer; the white door at its foot."
            );
            z.Mark(story, 5720f, -16.5f, "Beat 10: the Making-house", "The hero sees the door. Trigger 'makinghouse'.");
            var door = z.Place("makinghouse", "trigger", 5720f, -18.2f, "Beat 10: the Making-house");
            door.dialogue = new[] { "makinghouse" };
            z.Mark(item, 5920f, -48.5f, "Beat 7: basin shard", "In the drain below the pipes, a side path.");
            var shard = z.Place("basin_shard", "item", 5920f, -50.2f, "Beat 7: basin shard");
            shard.item = "basin_shard";
            shard.dialogue = new[] { "item_basin_shard" };
            z.Mark(secret, 5500f, -40.5f, "Secret: among the roots", "On the root bed under the third pipe.");
            z.Mark(secret, 5906f, -32.5f, "Secret: the drain ledge", "Halfway down the drain.");
            z.Mark(secret, 6150f, -19f, "Secret: beside the door", "Where the gathered queue.");
            z.Mark(story, 6292f, -19.5f, "Into the Low Gate", "Past the edge of the garden: cold silver cliffs.");

            z.Light(5150f, -16f, Pearl, 12f, 0.7f);
            z.Light(5250f, -17f, Dawn, 8f, 1f);
            z.Light(5391f, -12f, Pearl, 8f, 0.9f);
            z.Light(5500f, -38f, Fungus, 10f, 0.7f);
            z.Light(5800f, -14f, Dawn, 8f, 1f);
            z.Light(5920f, -46f, StreamGlow, 10f, 0.9f);
            z.Light(6206f, -10f, Marble, 26f, 1.3f);
            return z;
        }

        /// <summary>
        /// Zone 7, the Low Gate (x 6300 to 7500): the cliff top with the first shrine, a
        /// descent of four rock ledges over a cloud bank to the cliff floor, the last climb
        /// in marble steps to the road above the cliff, the Warden's arena at the cliff's
        /// edge, and the Low Gate: the Voice at its outer door, a closed floor where the
        /// drop to Ruined Earth will be, the silver ring behind.
        /// </summary>
        public static Zone Zone7()
        {
            var z = new Zone
            {
                number = 7,
                scene = "Paradise_Z7_LowGate",
                xMin = 6300f,
                xMax = 7500f,
            };
            z.rooms.Add(new Rect(6300f, -105f, 1200f, 100f));

            z.Ground("CliffTop", -100f, S(6299f, -21f), C(6330f, -21.4f), C(6350f, -21.6f), S(6380f, -22f));
            // The descent: ledges 8 apart, 8 to 14 down, over a soft cloud bank.
            z.Island("Ledge1", S(6388f, -30f), S(6404f, -30f), S(6404f, -34f), S(6388f, -34f));
            z.Island("Ledge2", S(6412f, -42f), S(6428f, -42f), S(6428f, -46f), S(6412f, -46f));
            z.Island("Ledge3", S(6436f, -56f), S(6452f, -56f), S(6452f, -60f), S(6436f, -60f));
            z.Island("Ledge4", S(6460f, -70f), S(6476f, -70f), S(6476f, -74f), S(6460f, -74f));
            z.Closed(
                "CloudBank",
                Profile.Leaf,
                true,
                S(6380f, -80f),
                C(6430f, -80.6f),
                S(6484f, -80f),
                S(6484f, -100f),
                S(6380f, -100f)
            );
            z.Ground("CliffFloor", -100f, S(6484f, -78f), C(6540f, -78.4f), C(6620f, -78.2f), S(6700f, -78f));
            // The last climb: marble steps of 6.5, a shrine on the road, a second flight.
            z.Ground(
                "ClimbTerraces",
                -100f,
                S(6704f, -78f),
                S(6720f, -78f),
                S(6720f, -71.5f),
                S(6740f, -71.5f),
                S(6740f, -65f),
                S(6760f, -65f),
                S(6760f, -58.5f),
                S(6780f, -58.5f),
                S(6780f, -52f),
                S(6810f, -52f)
            );
            z.Ground("ArenaApproach", -100f, S(6814f, -52f), C(6860f, -52.2f), C(6900f, -52.4f), S(6960f, -52.6f));
            z.Ground(
                "UpperClimb",
                -100f,
                S(6968f, -52.6f),
                S(6990f, -52.6f),
                S(6990f, -46f),
                S(7010f, -46f),
                S(7010f, -39.5f),
                S(7030f, -39.5f),
                S(7030f, -33f),
                S(7060f, -33f)
            );
            z.Ground(
                "GateRoad",
                -100f,
                S(7068f, -33.4f),
                C(7120f, -33.6f),
                C(7200f, -33.8f),
                C(7300f, -34f),
                S(7356f, -34f)
            );
            // The arena: 80 flat tiles at the cliff's edge, open sky above.
            z.Ground("Arena", -100f, S(7360f, -34f), C(7400f, -34f), S(7440f, -34f));
            // The gate's outer door and the closed floor where the drop will be.
            z.Ground("GateStep", -100f, S(7444f, -34f), C(7470f, -34f), S(7500f, -34f));
            z.Closed(
                "LowGateRing",
                Profile.Decor,
                false,
                S(7452f, -4f),
                S(7488f, -4f),
                S(7500f, -16f),
                S(7500f, -22f),
                S(7488f, -34f),
                S(7452f, -34f),
                S(7440f, -22f),
                S(7440f, -16f)
            );

            z.MainPath(
                "CliffTop",
                "Ledge1",
                "Ledge2",
                "Ledge3",
                "Ledge4",
                "CliffFloor",
                "ClimbTerraces",
                "ArenaApproach",
                "UpperClimb",
                "GateRoad",
                "Arena",
                "GateStep"
            );
            z.Soft("CloudBank");
            z.Masonry("ClimbTerraces", "ArenaApproach", "UpperClimb", "GateRoad", "Arena", "GateStep");
            z.saves.Add(new Vector2(6350f, -21.6f));
            z.saves.Add(new Vector2(6900f, -52.4f));
            z.saves.Add(new Vector2(7350f, -34f));

            var story = Descender.World.Paradise.DesignMarker.Kind.Story;
            var secret = Descender.World.Paradise.DesignMarker.Kind.Secret;
            var encounter = Descender.World.Paradise.DesignMarker.Kind.Encounter;
            var landmark = Descender.World.Paradise.DesignMarker.Kind.Landmark;
            z.Mark(story, 6500f, -70f, "Wind (M3)", "The wind that pushes on the descent is built with the art pass.");
            z.Mark(secret, 6430f, -78.5f, "Secret: the cloud bank", "Under the ledges.");
            z.Mark(secret, 6650f, -76.5f, "Secret: the cliff foot", "At the far end of the floor, before the climb.");
            z.Mark(secret, 7100f, -32f, "Secret: the road's edge", "Over the cliff's edge, a ledge only Wings reach.");
            z.Mark(
                encounter,
                7400f,
                -33f,
                "Boss: Warden of the Low Gate (M4)",
                "The arena: 80 wide, open above. The fight comes in M4."
            );
            z.Mark(
                landmark,
                7470f,
                -2f,
                "Landmark: the Low Gate",
                "A vast ring of silver in the cliff, seen from zone 5 on."
            );
            z.Mark(
                story,
                7470f,
                -32.5f,
                "Beat 8: the Voice at the gate",
                "The Voice at the outer door. Trigger 'gate'."
            );
            z.Place("gate", "trigger", 7470f, -34f, "Beat 8: the Voice at the gate").dialogue = new[] { "gate" };
            z.Mark(
                story,
                7486f,
                -32.5f,
                "Exit: the drop to Ruined Earth (M4)",
                "A closed floor until the Warden falls."
            );

            z.Light(6350f, -17f, Dawn, 8f, 1f);
            z.Light(6430f, -60f, Steel, 20f, 0.6f);
            z.Light(6600f, -72f, Steel, 14f, 0.6f);
            z.Light(6900f, -48f, Dawn, 8f, 1f);
            z.Light(7200f, -28f, Steel, 16f, 0.6f);
            z.Light(7350f, -30f, Dawn, 8f, 1f);
            z.Light(7470f, -18f, Marble, 24f, 1.2f);
            return z;
        }

        /// <summary>
        /// A piece of the Making-house's machinery, static until M3: an island `width` wide
        /// and 3 thick with its top-left at (x, top), on the main path, with a marker saying
        /// what moves here later.
        /// </summary>
        private static void Machinery(Zone z, string name, float x, float top, float width, string what)
        {
            z.Island(name, S(x, top), S(x + width, top), S(x + width, top - 3f), S(x, top - 3f));
            z.Mark(
                Descender.World.Paradise.DesignMarker.Kind.Story,
                x + width / 2f,
                top + 1.5f,
                $"Machinery (M3): {what}",
                $"'{name}' stands still until the art pass builds the {what}."
            );
        }

        /// <summary>One island of the Wings descent: 14 wide, 4 thick, its top-left at (x, top).</summary>
        private static void Descent(Zone z, string name, float x, float top)
        {
            z.Island(name, S(x, top), S(x + 14f, top), S(x + 14f, top - 4f), S(x, top - 4f));
        }

        /// <summary>
        /// A glowing stream in a 7-tile gap east of x: its bed (soft, off the main path) 6
        /// below the bank, the water a decor sheet on it, and a glow.
        /// </summary>
        private static void Stream(Zone z, string name, float x, float bankY)
        {
            var bed = bankY - 6f;
            z.Closed(name + "Bed", Profile.Leaf, true, S(x, bed), S(x + 7f, bed), S(x + 7f, -100f), S(x, -100f));
            z.Soft(name + "Bed");
            z.Closed(name, Profile.Decor, false, S(x, bed + 1.5f), S(x + 7f, bed + 1.5f), S(x + 7f, bed), S(x, bed));
            z.Light(x + 3.5f, bed + 2f, StreamGlow, 8f, 1f);
        }
    }
}
