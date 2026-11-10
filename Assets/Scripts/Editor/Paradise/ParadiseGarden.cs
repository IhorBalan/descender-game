using System.Collections.Generic;
using System.IO;
using Descender.World;
using Descender.World.Paradise;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Paradise as a sunlit celestial garden (docs/design/paradise.md, section 5): the
    /// marble-and-ivy kit from tools/paradise_art (Assets/Art/Paradise/Kit and
    /// Backdrop_Garden) turned into the zones' profiles, materials and look, and the
    /// dressing that keeps everything connected. Decks stand on marble colonnades with arches
    /// where the deck is level; plants sit on surfaces found on the terrain; ivy, banners
    /// and lanterns hang from decks and arches; water falls only where it spills from an
    /// aqueduct's break or a terrace's edge, and lands in foam or goes down into the clouds.
    /// </summary>
    public static class ParadiseGarden
    {
        public const string KitRoot = ParadiseArt.ArtRoot + "/Kit";
        public const string BackdropRoot = ParadiseArt.ArtRoot + "/Backdrop_Garden";

        public static bool Available => Directory.Exists(KitRoot) && Directory.Exists(BackdropRoot);

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Get(string name) =>
            Sprites.TryGetValue(name, out var s)
                ? s
                : throw new System.InvalidOperationException("Missing garden sprite " + name);

        /// <summary>After ParadiseArt's import: the derived underside, the sprites, the profiles' parts.</summary>
        public static void Prepare()
        {
            Sprites.Clear();
            WriteDepthTexture();
            WriteContactShadow();
            WriteCaps();
            WriteTierArches();
            ParadiseArt.DeriveFlipped(
                $"{KitRoot}/Nature/island_bottom.png",
                $"{ParadiseArt.DerivedFolder}/island_bottom_flip.png",
                1f - 0.95f
            );
            var soffit = $"{KitRoot}/Architecture/wall_soffit.png";
            if (File.Exists(soffit))
            {
                // The slab's edge is drawn 10 rows down the 92-row band (wall_soffit.py), the rest hangs below.
                ParadiseArt.DeriveFlipped(soffit, $"{ParadiseArt.DerivedFolder}/{SoffitSprite}.png", 10f / 92f);
            }
            ParadiseNormals.Apply(new[] { KitRoot, ParadiseArt.DerivedFolder });
            foreach (var folder in new[] { KitRoot, BackdropRoot, ParadiseArt.DerivedFolder })
            {
                foreach (var path in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
                    if (sprite != null)
                    {
                        Sprites[Path.GetFileNameWithoutExtension(path)] = sprite;
                    }
                }
            }
            // The shaft repeats down a column of any height (Tiled draw mode).
            var shaft = AssetImporter.GetAtPath($"{KitRoot}/Architecture/column_shaft.png") as TextureImporter;
            if (shaft != null && shaft.wrapMode != TextureWrapMode.Repeat)
            {
                shaft.wrapMode = TextureWrapMode.Repeat;
                shaft.SaveAndReimport();
            }
        }

        /// <summary>A zone's garden art: shared kit, the zone's own grading and ambient.</summary>
        public static ParadiseArt.ZoneArt Zone(int z, ParadiseArt.ZoneArt basis)
        {
            var art = basis;
            art.garden = true;
            // The sky's own light is cool: shadows go blue, and only the sun warms what it touches.
            art.ambient = z == 1 ? new Color(0.88f, 0.93f, 1f) : new Color(0.86f, 0.95f, 1f);
            art.shaftColor = new Color(1f, 0.95f, 0.8f);
            var fill = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitRoot}/Nature/rock_fill.png");
            art.ground = ParadiseArt.Profile(
                "Garden_Ground",
                fill,
                new[]
                {
                    new AngleRange
                    {
                        start = 180f - UndersideReach,
                        end = 180f,
                        order = 0,
                        sprites = new List<Sprite> { Get("island_bottom_flip") },
                    },
                    new AngleRange
                    {
                        start = -180f,
                        end = -180f + UndersideReach,
                        order = 0,
                        sprites = new List<Sprite> { Get("island_bottom_flip") },
                    },
                }
            );
            art.lip = ParadiseArt.Profile(
                "Garden_Lip",
                null,
                new[]
                {
                    new AngleRange
                    {
                        start = -90f,
                        end = 90f,
                        order = 1,
                        sprites = new List<Sprite> { Get("grass_lip") },
                    },
                }
            );
            // Built terraces: marble ashlar walls under a carved coping (the grass lip until it
            // exists). Where one overhangs, its underside is finished stone, not the islands' rock.
            var wall = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitRoot}/Architecture/wall_block.png");
            var underside = Sprites.ContainsKey(SoffitSprite) ? Get(SoffitSprite) : Get("island_bottom_flip");
            art.terrace = ParadiseArt.Profile(
                "Garden_Terrace",
                wall,
                new[]
                {
                    new AngleRange
                    {
                        start = 180f - UndersideReach,
                        end = 180f,
                        order = 0,
                        sprites = new List<Sprite> { underside },
                    },
                    new AngleRange
                    {
                        start = -180f,
                        end = -180f + UndersideReach,
                        order = 0,
                        sprites = new List<Sprite> { underside },
                    },
                }
            );
            art.terraceLip = ParadiseArt.Profile(
                "Garden_TerraceLip",
                null,
                new[]
                {
                    new AngleRange
                    {
                        start = -90f,
                        end = 90f,
                        order = 1,
                        sprites = new List<Sprite>
                        {
                            Sprites.ContainsKey("wall_cap") ? Get("wall_cap") : Get("grass_lip"),
                        },
                    },
                }
            );
            art.ledge = ParadiseArt.Profile(
                "Garden_Deck",
                null,
                new[]
                {
                    new AngleRange
                    {
                        start = -90f,
                        end = 90f,
                        order = 1,
                        sprites = new List<Sprite> { Get("deck_top") },
                    },
                }
            );
            // Foreground silhouettes: deep cool green, as the key art's framing leaves, keeping
            // a little of their own colour so they read as leaves, not holes.
            if (art.foreground != null)
            {
                art.foreground.SetColor("_HazeColor", new Color(0.1f, 0.17f, 0.15f));
                art.foreground.SetFloat("_HazeAmount", 0.34f);
                EditorUtility.SetDirty(art.foreground);
            }
            // Beams of sun through the air: on a bright sky an added light has to be fairly strong to read.
            art.shaft.SetFloat("_Intensity", BeamIntensity);
            EditorUtility.SetDirty(art.shaft);
            art.water = ParadiseArt.SaveMaterial("Paradise Waterfall", "Descender/Waterfall");
            art.water.SetColor("_Color", new Color(0.92f, 0.97f, 1f, 0.92f));
            EditorUtility.SetDirty(art.water);
            // Plants answer the garden's wind (GardenWind) each by its kind: see SwayMaterial.
            SwayVariants.Clear();
            ParadiseGrade.Apply(art.look, z);
            return art;
        }

        // ------------------------------------------------------------------ dress

        /// <summary>The garden's backdrop, look, dressing, water and air for one zone.</summary>
        public static void Dress(ParadiseLayout.Zone zone, Transform root, ParadiseArt.ZoneArt art)
        {
            var look = ParadiseArt.Group(root, "Look");
            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.transform.SetParent(look, false);
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 0f;
            volume.sharedProfile = art.look;

            var backdrop = ParadiseArt.Group(root, "Backdrop");
            var centre = new Vector2((zone.xMin + zone.xMax) / 2f, -56f);
            var sky = ParadiseArt.Layer(
                backdrop,
                "Sky",
                Get("garden_bg0_sky_clear"),
                art.unlit,
                -1000 + zone.number,
                centre,
                Vector2.zero,
                true
            );
            // The sun stays where it is; the clouds cut from the same painting move past it.
            var highClouds = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "HighClouds",
                    Get("garden_bg0_clouds"),
                    art.unlit,
                    -995 + zone.number,
                    centre,
                    new Vector2(HighCloudScroll, 0f),
                    false
                ),
                0f
            );
            Drift(highClouds, HighCloudDrift);
            // The backdrops are painted as whole screens (32 x 18 units); our view is 20 x 11.25,
            // so each is scaled to one screen and pinned to the view vertically (scroll.y 0), its
            // horizon where it was painted; sideways each scrolls at its own depth.
            var far = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "Far",
                    Get("garden_bg1_far"),
                    art.backdropFar,
                    -990 + zone.number,
                    centre,
                    new Vector2(0.08f, 0f),
                    false
                ),
                0f
            );
            var midFar = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "MidFar",
                    Get("garden_bg2_midfar"),
                    art.backdropFar,
                    -980 + zone.number,
                    centre,
                    new Vector2(0.2f, 0f),
                    false
                ),
                0f
            );
            var cloudBack = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "CloudsBack",
                    Get("cloud_sea_0"),
                    art.backdropMid,
                    -976 + zone.number,
                    centre,
                    new Vector2(0.24f, 0f),
                    false
                ),
                -5.6f
            );
            var mid = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "Mid",
                    Get("garden_bg3_mid"),
                    art.backdropMid,
                    -970 + zone.number,
                    centre,
                    new Vector2(0.4f, 0f),
                    false
                ),
                -1f
            );
            var cloudFront = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "CloudsFront",
                    Get("cloud_sea_1"),
                    art.backdropMid,
                    -966 + zone.number,
                    centre,
                    new Vector2(0.46f, 0f),
                    false
                ),
                -6.4f
            );
            // Between the walk and the distance: the garden's own architecture, hazed and in its own cloud.
            var near = Pinned(
                ParadiseArt.Layer(
                    backdrop,
                    "Near",
                    Get("garden_bg4_near"),
                    art.backdropMid,
                    -960 + zone.number,
                    centre,
                    new Vector2(0.68f, 0f),
                    false
                ),
                0f
            );
            Drift(cloudBack, 1.5f);
            Drift(cloudFront, 3f);
            var zoneLook = root.gameObject.AddComponent<ZoneLook>();
            zoneLook.Configure(
                zone.number,
                zone.xMin,
                zone.xMax,
                art.ambient,
                volume,
                sky,
                new[] { highClouds, far, midFar, cloudBack, mid, cloudFront, near }
            );
            if (CanopyOf(zone.number, out var canopyFrom, out var canopyTo) && Sprites.ContainsKey(CanopyBack))
            {
                // The orchard's roof of leaves (canopy.py), pinned to the top of the view so its
                // edge never shows at any height: the blossom crowns seen from below, just behind
                // the play layer, rocking a little as the leaves stir; and the same growth near
                // and out of focus along the very top, passing faster than the world.
                var roof = Pinned(
                    ParadiseArt.Layer(
                        backdrop,
                        "CanopyBack",
                        Get(CanopyBack),
                        art.unlit,
                        CanopyBackOrder,
                        centre,
                        new Vector2(CanopyBackScroll, 0f),
                        false
                    ),
                    CanopyBackDrop
                );
                Sway(roof, 3f, 9f);
                var leaves = Pinned(
                    ParadiseArt.Layer(
                        backdrop,
                        "CanopyFront",
                        Get(CanopyFront),
                        art.unlit,
                        CanopyFrontOrder,
                        centre,
                        new Vector2(CanopyFrontScroll, 0f),
                        false
                    ),
                    CanopyFrontDrop
                );
                leaves.GetComponent<SpriteRenderer>().sortingLayerName = ForegroundLayer;
                Sway(leaves, 5f, 7f);
                zoneLook.SetCanopy(CanopyCover, canopyFrom, canopyTo, CanopyOpens, new[] { roof, leaves });
            }

            var plan = GardenPlan(zone);
            var props = ParadiseArt.Group(root, "Props");
            var shadows = ParadiseArt.Group(root, "ContactShadows");
            foreach (var p in plan.plants)
            {
                Vary(Place(props, p.name, p.at, p.scale, p.flip, art.plant, -85, art), p.at.x);
                ContactShadow(shadows, p.name, p.at, p.scale, art);
            }
            // Butterflies keep to the flower beds: every other bush or lily clump.
            var beds = new List<Vector2>();
            foreach (var p in plan.plants)
            {
                if ((p.name == "flower_bush" || p.name.StartsWith("lily")) && Hash(p.at.x, 51f) > 0.5f)
                {
                    beds.Add(p.at);
                }
            }
            if (beds.Count > 0)
            {
                var butterflies = new GameObject("Butterflies").AddComponent<Butterflies>();
                butterflies.transform.SetParent(ParadiseArt.Group(root, "Life"), false);
                butterflies.Configure(beds);
            }
            foreach (var p in plan.ruins)
            {
                Vary(Place(props, p.name, p.at, p.scale, p.flip, art.lit, -88, art), p.at.x);
                ContactShadow(shadows, p.name, p.at, p.scale, art);
            }
            foreach (var p in plan.trees)
            {
                // Behind the play layer, rooted below the terrace's lip.
                var tree = Place(props, p.name, p.at, p.scale, p.flip, art.mid ?? art.plant, -150, art);
                if (Mathf.Approximately(p.scale, LanternTreeScale))
                {
                    // In the shade of its own crown, so the marble landings climbing through it
                    // stand out white against it; its fruit keeps its full glow.
                    tree.color = LanternTreeShade;
                }
            }
            foreach (var p in plan.walls)
            {
                var hangs = p.name == "ivy_curtain" || p.name == "wisteria_drape" || p.name.StartsWith("ivy_drape");
                // Ivy over a wall comes out from behind the coping: in front of it, its dense top
                // would sit on the stone like a box.
                var order =
                    !hangs ? -86
                    : p.name.StartsWith("ivy_drape") ? WallIvyOrder
                    : -98;
                Vary(Place(props, p.name, p.at, p.scale, p.flip, art.plant, order, art), p.at.x);
                if (!hangs)
                {
                    ContactShadow(shadows, p.name, p.at, p.scale, art);
                }
            }
            foreach (var p in plan.framing)
            {
                var r = ParadiseArt.Sprite(
                    ParadiseArt.Group(root, "Foreground"),
                    p.name,
                    Get(p.name),
                    art.foreground,
                    405,
                    p.at,
                    Vector3.one * p.scale
                );
                r.flipX = p.flip;
                r.gameObject.AddComponent<ForegroundParallax>().Configure(0.15f);
            }
            Railings(zone, ParadiseArt.Group(root, "Railings"), art);
            Pilasters(zone, ParadiseArt.Group(root, "Pilasters"), art);
            if (plan.numbers.Count > 0 && Sprites.ContainsKey(NumeralAtlas))
            {
                Numbers(root, plan.numbers, art);
            }
            if (Sprites.ContainsKey(WallNiche))
            {
                // Set into the wall: over its blocks and piers, under the depth shade and the ivy's spill.
                var niches = ParadiseArt.Group(root, "Niches");
                foreach (var p in plan.niches)
                {
                    Place(niches, p.name, p.at, p.scale, false, art.lit, NicheOrder, art);
                }
            }
            var hanging = ParadiseArt.Group(root, "Hanging");
            foreach (var p in plan.hanging)
            {
                var r = Place(
                    hanging,
                    p.name,
                    p.at,
                    p.scale,
                    p.flip,
                    p.name.StartsWith("ivy") || p.name.StartsWith("vine") ? art.plant : art.lit,
                    -87,
                    art
                );
                if (p.name == "lantern")
                {
                    LanternLight(r.transform, zone.number);
                }
            }
            var water = ParadiseArt.Group(root, "Water");
            foreach (var w in plan.water)
            {
                Fall(zone, water, w, art);
            }
            var pergolas = ParadiseArt.Group(root, "Pergolas");
            foreach (var x in plan.pergolas)
            {
                Pergola(zone, pergolas, x, art);
            }
            var shafts = ParadiseArt.Group(root, "Shafts");
            var frontBeam = FrontBeamMaterial(art);
            for (var i = 0; i < plan.shafts.Count; i++)
            {
                var x = plan.shafts[i];
                var inFront = i % 3 == 1;
                var r = ParadiseArt.Sprite(
                    shafts,
                    inFront ? "Beam Front" : "Beam",
                    art.lightShaft,
                    inFront ? frontBeam : art.shaft,
                    inFront ? 390 : -300,
                    new Vector3(x, ParadiseDressing.SurfaceY(zone, x) + BeamTopOverWalk, 0f),
                    Vector3.one * (0.85f + Hash(x, 62f) * 0.5f)
                );
                // Its top is its pivot: it hangs from there and leans the way the sunlight runs.
                r.transform.rotation = Quaternion.Euler(0f, 0f, BeamLean);
                r.color = art.shaftColor;
                if (inFront)
                {
                    r.sortingLayerName = ForegroundLayer;
                    continue;
                }
                // Dust caught in the light where the beam comes down past the walk.
                var walk = ParadiseDressing.SurfaceY(zone, x);
                var along = (BeamTopOverWalk - BeamDustAbove) * Mathf.Tan(BeamLean * Mathf.Deg2Rad);
                Air(
                    shafts,
                    "Beam Dust",
                    art.mote,
                    art.glow,
                    BeamDust,
                    new Color(1f, 0.96f, 0.84f, 0.6f),
                    10f,
                    -(Vector2)SunAndClouds.SunDirection,
                    30f,
                    new Vector2(0.6f, 1.6f),
                    new Vector2(0.06f, 0.16f),
                    0f,
                    6f,
                    new Rect(x + along - 2f, walk + BeamDustAbove - 2.5f, 4f, 5f)
                );
            }
            var front = ParadiseArt.Group(root, "Foreground");
            for (var i = 0; i < plan.foreground.Count; i++)
            {
                var f = plan.foreground[i];
                var r = ParadiseArt.Sprite(
                    front,
                    f.name,
                    Get(f.name),
                    FrontMaterial(f.name.Contains("hang"), art),
                    400 + i % 3,
                    f.at,
                    Vector3.one * f.scale
                );
                r.flipX = f.flip;
                r.sortingLayerName = ForegroundLayer;
                var parallax = r.gameObject.AddComponent<ForegroundParallax>();
                parallax.Configure(FrontDepth, FrontDepth);
                parallax.FadeAwayFrom(
                    ParadiseDressing.SurfaceY(zone, f.at.x, f.at.y) + CameraOverWalk,
                    FrontFadeNear,
                    FrontFadeFar
                );
            }
            var air = ParadiseArt.Group(root, "Air");
            var area = new Rect(zone.xMin, -105f, zone.xMax - zone.xMin, 90f);
            var pollen = Air(
                air,
                "Pollen",
                art.pollen,
                art.glow,
                45,
                new Color(1f, 0.9f, 0.62f, 0.5f),
                9f,
                new Vector2(0.6f, -0.2f),
                50f,
                new Vector2(3f, 8f),
                new Vector2(0.12f, 0.32f),
                0f,
                6f,
                area
            );
            pollen.SetWindCarry(1f);
            var petals = Air(
                air,
                "Petals",
                art.petal,
                art.unlit,
                22,
                new Color(1f, 1f, 0.96f),
                13f,
                new Vector2(-0.4f, 1f),
                25f,
                new Vector2(8f, 16f),
                new Vector2(0.45f, 0.8f),
                0.4f,
                5f,
                area
            );
            // Real petals flip and flutter as they fall, never slide flat.
            petals.SetTumble(0.35f);
            petals.SetWindCarry(1.6f);
            Air(
                air,
                "Motes",
                art.mote,
                art.glow,
                30,
                new Color(1f, 0.95f, 0.8f, 0.4f),
                11f,
                new Vector2(0.1f, -1f),
                40f,
                new Vector2(2f, 5f),
                new Vector2(0.1f, 0.26f),
                0f,
                8f,
                area
            );
            if (zone.number == 2 && art.firefly != null)
            {
                // The orchard is lit from within (docs/design/paradise.md): fireflies wander low
                // over the walk and the dells, each glowing up and dimming as it goes.
                Air(
                        air,
                        "Fireflies",
                        art.firefly,
                        art.glow,
                        Fireflies,
                        FireflyColor,
                        7f,
                        new Vector2(0f, -1f),
                        180f,
                        new Vector2(1f, 3f),
                        new Vector2(0.2f, 0.34f),
                        0f,
                        14f,
                        new Rect(zone.xMin, FireflyFloor, zone.xMax - zone.xMin, FireflyBand)
                    )
                    .SetBlinks(2);
            }
        }

        /// <summary>How many fireflies are in a view of the orchard, their colour, and the band of air they keep to.</summary>
        public const int Fireflies = 16;

        public static readonly Color FireflyColor = new Color(0.86f, 1f, 0.6f, 0.9f);

        public const float FireflyFloor = -82f;

        public const float FireflyBand = 32f;

        // --------------------------------------------------------------- railings

        /// <summary>The steepest a balustrade follows a walk, in degrees: beyond, its balusters would lean.</summary>
        private const float RailingMaxSlope = 12f;

        private const float RailingPiece = 4f;

        /// <summary>
        /// Balustrades along the far side of every deck and built terrace, as in the key art:
        /// behind the hero and the plants, their foot on the walk, in pieces that follow the
        /// walk's slope where it is gentle and stop where it is steep. Natural rock gets none.
        /// </summary>
        private static void Railings(ParadiseLayout.Zone zone, Transform parent, ParadiseArt.ZoneArt art)
        {
            if (!Sprites.ContainsKey("balustrade"))
            {
                return;
            }
            foreach (var bridge in zone.bridges)
            {
                var (left, right) = ParadiseGreybox.Tangents(bridge.deck, false);
                var line = ParadiseGreybox.Sample(bridge.deck, left, right, false);
                Railing(
                    parent,
                    x => DeckY(line, x),
                    bridge.deck[0].x + 0.3f,
                    bridge.deck[bridge.deck.Count - 1].x - 0.3f,
                    art
                );
            }
            foreach (var shape in zone.shapes)
            {
                if (!zone.masonry.Contains(shape.name))
                {
                    continue;
                }
                var end = 0;
                while (end + 1 < shape.outline.Count && shape.outline[end + 1].x >= shape.outline[end].x - 0.001f)
                {
                    end++;
                }
                Railing(
                    parent,
                    x => ParadiseDressing.SurfaceY(zone, x, float.NaN),
                    shape.outline[0].x + 0.5f,
                    shape.outline[end].x - 0.5f,
                    art
                );
            }
        }

        private static void Railing(
            Transform parent,
            System.Func<float, float> surface,
            float x0,
            float x1,
            ParadiseArt.ZoneArt art
        )
        {
            var sprite = Get("balustrade");
            // A run of balustrade ends in a square post at each end, as a mason builds one.
            var runStart = float.NaN;
            var runEnd = 0f;
            void EndRun()
            {
                if (float.IsNaN(runStart))
                {
                    return;
                }
                RailPost(parent, runStart + RailPostInset, surface(runStart + RailPostInset), art);
                RailPost(parent, runEnd - RailPostInset, surface(runEnd - RailPostInset), art);
                runStart = float.NaN;
            }
            for (var a = x0; a < x1 - 1f; a += RailingPiece)
            {
                var b = Mathf.Min(a + RailingPiece, x1);
                var ya = surface(a);
                var yb = surface(b);
                var ym = surface((a + b) / 2f);
                var angle = Mathf.Atan2(yb - ya, b - a) * Mathf.Rad2Deg;
                var fits =
                    !float.IsNaN(ya)
                    && !float.IsNaN(yb)
                    && !float.IsNaN(ym)
                    // A step or a gap inside the piece: no railing over it.
                    && Mathf.Abs(ym - (ya + yb) / 2f) <= 0.3f
                    && Mathf.Abs(angle) <= RailingMaxSlope;
                if (!fits)
                {
                    EndRun();
                    continue;
                }
                if (float.IsNaN(runStart))
                {
                    runStart = a;
                }
                runEnd = b;
                var r = ParadiseArt.Sprite(
                    parent,
                    "Balustrade",
                    sprite,
                    art.lit,
                    -90,
                    new Vector3((a + b) / 2f, (ya + yb) / 2f - 0.08f, 0f),
                    Vector3.one * 0.8f
                );
                r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                r.drawMode = SpriteDrawMode.Tiled;
                r.tileMode = SpriteTileMode.Continuous;
                // A hair longer than the step, so neighbouring pieces meet without a seam.
                r.size = new Vector2((b - a) / 0.8f / Mathf.Cos(angle * Mathf.Deg2Rad) + 0.1f, sprite.bounds.size.y);
            }
            EndRun();
        }

        /// <summary>How far in from a run's end its post stands, so the post covers the cut handrail.</summary>
        private const float RailPostInset = 0.22f;

        private static void RailPost(Transform parent, float x, float y, ParadiseArt.ZoneArt art)
        {
            if (float.IsNaN(y) || !Sprites.ContainsKey("balustrade_post"))
            {
                return;
            }
            // Upright even where the railing slopes, and over it, so it closes the run.
            ParadiseArt.Sprite(
                parent,
                "Rail Post",
                Get("balustrade_post"),
                art.lit,
                -89,
                new Vector3(x, y - 0.1f, 0f),
                Vector3.one * 0.8f
            );
        }

        /// <summary>
        /// A pilaster on every corner of a built terrace: where it steps up (a riser, from the
        /// lower walk to the upper coping) and where it ends at a drop (from its coping down
        /// into the shade). Masons finish a wall's corner with a pier, so the blocks and the
        /// coping never stop in a bare cut.
        /// </summary>
        private static void Pilasters(ParadiseLayout.Zone zone, Transform parent, ParadiseArt.ZoneArt art)
        {
            if (!Sprites.ContainsKey("column_shaft"))
            {
                return;
            }
            foreach (var shape in zone.shapes)
            {
                if (!zone.masonry.Contains(shape.name))
                {
                    continue;
                }
                var points = shape.outline;
                var end = 0;
                while (end + 1 < points.Count && points[end + 1].x >= points[end].x - 0.001f)
                {
                    end++;
                }
                // A walk that ends at a drop (a slab's end over a hollow) ends at the top of the
                // drop: its pilaster goes down from the coping, and the drop is not a riser.
                while (
                    end > 0 && Mathf.Abs(points[end].x - points[end - 1].x) < 0.01f && points[end].y < points[end - 1].y
                )
                {
                    end--;
                }
                for (var i = 0; i < end; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    if (Mathf.Abs(b.x - a.x) < 0.01f && Mathf.Abs(b.y - a.y) >= 3f)
                    {
                        Pilaster(parent, a.x, Mathf.Max(a.y, b.y), Mathf.Min(a.y, b.y), true, art);
                    }
                }
                // The terrace's two ends, where its wall drops away.
                EndPilaster(zone, parent, points[0].x, points[0].y, -1f, art);
                EndPilaster(zone, parent, points[end].x, points[end].y, 1f, art);
            }
        }

        /// <summary>How far a pilaster at a terrace's end goes down when nothing below it is near.</summary>
        private const float EndPilasterDepth = 14f;

        /// <summary>
        /// The pilaster at a terrace's end, on the side `outward` (-1 west, +1 east): down to the
        /// ground beside it when there is some within reach, standing on it (the root hollow's
        /// floor, the roots route), else into the shade. None where another terrace stands right
        /// against the end: that terrace's own corner is the one that shows.
        /// </summary>
        private static void EndPilaster(
            ParadiseLayout.Zone zone,
            Transform parent,
            float x,
            float top,
            float outward,
            ParadiseArt.ZoneArt art
        )
        {
            var beside = x + outward * 0.6f;
            for (var y = top - 0.1f; y > top - EndPilasterDepth - 6f; y -= 0.1f)
            {
                if (InsideSolid(zone, new Vector2(beside, y)))
                {
                    if (top - y > 1.5f)
                    {
                        Pilaster(parent, x, top, y + 0.05f, true, art);
                    }
                    return;
                }
            }
            Pilaster(parent, x, top, top - EndPilasterDepth, false, art);
        }

        private static void Pilaster(
            Transform parent,
            float x,
            float top,
            float foot,
            bool standing,
            ParadiseArt.ZoneArt art
        )
        {
            const float width = 0.85f;
            var pilaster = new GameObject("Pilaster").transform;
            pilaster.SetParent(parent, false);
            var tint = new Color(0.93f, 0.93f, 0.95f);
            var shaftFoot = foot;
            if (standing)
            {
                var b = ParadiseArt.Sprite(
                    pilaster,
                    "Base",
                    Get("column_base"),
                    art.lit,
                    -97,
                    new Vector3(x, foot - 0.05f, 0f),
                    Vector3.one * width
                );
                b.color = tint;
                shaftFoot = foot + 0.8f * width;
            }
            // The capital's abacus meets the coping's underside (the coping strip is centred on
            // the walk line and hangs about 0.7 below it).
            var capitalFoot = top - 0.68f - width;
            var shaft = ParadiseArt.Sprite(
                pilaster,
                "Shaft",
                Get("column_shaft"),
                art.lit,
                -98,
                new Vector3(x, shaftFoot, 0f),
                new Vector3(ShaftWidth * width, 1f, 1f)
            );
            shaft.drawMode = SpriteDrawMode.Tiled;
            shaft.tileMode = SpriteTileMode.Continuous;
            shaft.size = new Vector2(1f, Mathf.Max(0.2f, capitalFoot - shaftFoot + 0.3f));
            shaft.color = tint;
            var cap = ParadiseArt.Sprite(
                pilaster,
                "Capital",
                Get("column_capital"),
                art.lit,
                -97,
                new Vector3(x, capitalFoot, 0f),
                Vector3.one * width
            );
            cap.color = tint;
        }

        /// <summary>
        /// The golden tree's place among the backdrops: over the blurred layers that hold the temple
        /// islands (MidFar and Mid), so a soft ghost never slides across the sharpest thing in the
        /// distance, and behind the near clouds and the Near layer.
        /// </summary>
        public const int GoldenTreeOrder = -967;

        /// <summary>The region's far landmark: the golden tree on its island, drifting across Heaven 1.</summary>
        public static void DressRegion(GameObject root)
        {
            if (!Sprites.ContainsKey("golden_tree"))
            {
                return;
            }
            // Sun and cloud shadows over the whole region.
            ParadiseNormals.BuildSkyLights(root);
            // Flocks crossing the far sky now and then.
            if (root.GetComponent<DistantBirds>() == null)
            {
                root.AddComponent<DistantBirds>();
            }
            var old = root.transform.Find("GoldenTree");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            var far = AssetDatabase.LoadAssetAtPath<Material>(
                $"{ParadiseArt.MaterialFolder}/Paradise Backdrop Far Z1.mat"
            );
            var glow = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.GlowMaterialPath);
            // Far away: small, near the horizon, pinned to the view vertically, drifting across Heaven 1.
            var tree = ParadiseArt.Sprite(
                root.transform,
                "GoldenTree",
                Get("golden_tree"),
                far,
                GoldenTreeOrder,
                new Vector3(1850f, -58f, 0f),
                Vector3.one * 0.28f
            );
            tree.gameObject.AddComponent<ForegroundParallax>().Configure(-0.996f, -1f, new Vector2(0f, -2.5f));
            var g = ParadiseArt.Sprite(
                tree.transform,
                "Glow",
                Get("golden_tree_glow"),
                glow,
                GoldenTreeOrder + 1,
                tree.transform.position,
                Vector3.one
            );
            g.transform.localScale = Vector3.one;
            g.color = new Color(1f, 1f, 1f, 0.14f);
        }

        // ------------------------------------------------------------------ depth

        private const string DepthTexturePath = ParadiseArt.DerivedFolder + "/depth_shade.png";
        private const string DepthMaterialName = "Paradise Depth Shade";

        /// <summary>How far down the shade texture reaches, in units; deeper stays at its last row.</summary>
        private const float DepthReach = 32f;

        /// <summary>Width of the strips the shade mesh is cut into, in units.</summary>
        private const float DepthStrip = 1f;

        /// <summary>
        /// Shade over a ground or terrace shape that deepens with distance below the walkable
        /// surface above it, as in the key art: the lit coping and the first course stay
        /// bright, the face below falls into cool shadow, so the walk reads as the brightest
        /// band and a wall as a cliff with depth, not a flat sheet of blocks. One mesh per
        /// shape, clipped to its outline in narrow strips; each vertex is shaded from the
        /// surface height over it, smoothed across steps so a riser casts a soft shadow
        /// instead of a seam.
        /// </summary>
        public static void Depth(Transform parent, List<Vector2> outline, ParadiseArt.ZoneArt art)
        {
            var xMin = float.MaxValue;
            var xMax = float.MinValue;
            foreach (var p in outline)
            {
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
            }
            float Smoothed(float x)
            {
                var sum = 0f;
                for (var k = -3; k <= 3; k++)
                {
                    sum += SurfaceOf(outline, Mathf.Clamp(x + k * 0.5f, xMin, xMax));
                }
                return sum / 7f;
            }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var xa = xMin; xa < xMax - 0.001f; xa += DepthStrip)
            {
                var strip = ClipToStrip(outline, xa, Mathf.Min(xa + DepthStrip, xMax));
                if (strip.Count < 3)
                {
                    continue;
                }
                strip = Simplify(strip);
                if (strip.Count < 3)
                {
                    continue;
                }
                var first = vertices.Count;
                foreach (var p in strip)
                {
                    vertices.Add(p);
                    // v 1 at the surface, falling with depth (the texture clamps past its reach).
                    uvs.Add(new Vector2(p.x / WeatherPeriod, 1f - Mathf.Max(0f, Smoothed(p.x) - p.y) / DepthReach));
                }
                if (Crossings(outline, xa + DepthStrip * 0.5f) <= 2)
                {
                    // One column of rock: star-shaped from its centre, a fan covers it exactly.
                    var centre = Vector2.zero;
                    foreach (var p in strip)
                    {
                        centre += p;
                    }
                    centre /= strip.Count;
                    var c = vertices.Count;
                    vertices.Add(centre);
                    uvs.Add(
                        new Vector2(
                            centre.x / WeatherPeriod,
                            1f - Mathf.Max(0f, Smoothed(centre.x) - centre.y) / DepthReach
                        )
                    );
                    for (var i = 0; i < strip.Count; i++)
                    {
                        triangles.Add(c);
                        triangles.Add(first + (i + 1) % strip.Count);
                        triangles.Add(first + i);
                    }
                }
                else
                {
                    // Rock above open air (an overhang): clip ears, so the gap stays open.
                    foreach (var t in Triangulate(strip))
                    {
                        triangles.Add(first + t);
                    }
                }
            }
            if (triangles.Count == 0)
            {
                return;
            }
            var mesh = new Mesh { name = parent.name + " depth" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            var colors = new Color[vertices.Count];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Depth");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DepthMaterial();
            renderer.sortingOrder = -100;
        }

        private const string CastShadowMaterialName = "Paradise Cast Shadow";

        /// <summary>Width of the strips the shadow under a lip is cut into, in units.</summary>
        private const float ShadowStrip = 0.5f;

        /// <summary>
        /// The shadow the coping or the grass throws on the face just below it: the one hard
        /// line that says the lip stands proud of the wall. It follows the surface exactly
        /// (the depth shade is smoothed across steps, this must not be).
        /// </summary>
        public static void UnderLip(Transform parent, List<Vector2> outline, bool masonry)
        {
            // It starts behind the lip, whose lower edge is about a unit under the walk
            // (manifest: walkableRow), so its top is never seen; grass hangs unevenly and
            // throws a softer shadow than a cut coping.
            var start = masonry ? 0.75f : 0.45f;
            var length = masonry ? 1.2f : 1.9f;
            var strength = masonry ? 0.5f : 0.34f;
            const int rows = 7;
            var depths = new float[rows];
            var alphas = new float[rows];
            for (var row = 0; row < rows; row++)
            {
                var t = row / (rows - 1f);
                depths[row] = start + length * t;
                alphas[row] = strength * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1f, t)));
            }
            var xMin = float.MaxValue;
            var xMax = float.MinValue;
            foreach (var p in outline)
            {
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
            }
            var shade = new Color(0.14f, 0.17f, 0.27f);
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            void Column(float x, float surface)
            {
                for (var row = 0; row < depths.Length; row++)
                {
                    vertices.Add(new Vector3(x, surface - depths[row], 0f));
                    colors.Add(new Color(shade.r, shade.g, shade.b, alphas[row]));
                }
            }
            var joined = false;
            for (var x = xMin; x < xMax - 0.001f; x += ShadowStrip)
            {
                var next = Mathf.Min(x + ShadowStrip, xMax);
                var here = SurfaceOf(outline, x);
                var there = SurfaceOf(outline, next);
                // A riser inside the strip: the lower lip's shadow runs on up to it.
                var riser = Mathf.Abs(here - there) > (next - x) * 2.5f;
                if (riser)
                {
                    here = there = Mathf.Min(here, there);
                    joined = false;
                }
                if (!joined)
                {
                    Column(x, here);
                }
                var a = vertices.Count - depths.Length;
                Column(next, there);
                var b = vertices.Count - depths.Length;
                for (var row = 0; row < depths.Length - 1; row++)
                {
                    triangles.AddRange(new[] { a + row, b + row, a + row + 1 });
                    triangles.AddRange(new[] { a + row + 1, b + row, b + row + 1 });
                }
                joined = !riser;
            }
            if (triangles.Count == 0)
            {
                return;
            }
            var mesh = new Mesh { name = parent.name + " under lip" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, new Vector2[vertices.Count]);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("UnderLip");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ParadiseArt.SaveMaterial(
                CastShadowMaterialName,
                "Universal Render Pipeline/2D/Sprite-Unlit-Default"
            );
            renderer.sortingOrder = -100;
        }

        /// <summary>How far a rock side's edge fades out, in units: about the softness of the painted kit's edges.</summary>
        private const float SideFade = 0.07f;

        /// <summary>
        /// A fading ribbon of the same rock outside each steep edge. The fill's polygon has
        /// no anti-aliasing, which is invisible on a ruled edge and a pixel staircase on a
        /// ragged one. The depth shade stops at the outline, so the ribbon carries it itself.
        /// </summary>
        public static void SoftenSides(Transform parent, List<Vector2> outline, ParadiseArt.ZoneArt art)
        {
            var texture = art.ground.fillTexture;
            var unit = ParadiseArt.PixelsPerUnit(texture);
            var scale = new Vector2(unit / texture.width, unit / texture.height);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Color Shaded(Vector2 p, float inward, float alpha)
            {
                var depth = Mathf.Max(0f, SurfaceOf(outline, p.x + inward) - p.y);
                var a = DepthShade(depth);
                // The fill's mid grey, so multiplying by this lands where the shade laid over it does.
                const float rock = 0.6f;
                return new Color(
                    Mathf.Lerp(1f, DepthShadeColor.r / rock, a),
                    Mathf.Lerp(1f, DepthShadeColor.g / rock, a),
                    Mathf.Lerp(1f, DepthShadeColor.b / rock, a),
                    alpha
                );
            }
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                var edge = b - a;
                if (
                    edge.sqrMagnitude < 1e-6f
                    || Mathf.Abs(edge.y) < Mathf.Abs(edge.x) * 0.7f
                    || Mathf.Max(a.y, b.y) < -95f
                )
                {
                    continue;
                }
                // The outline runs clockwise, so outward is to its left.
                var outward = new Vector2(-edge.y, edge.x).normalized;
                var inward = outward.x > 0f ? -1.5f : 1.5f;
                var first = vertices.Count;
                foreach (var p in new[] { a, b, a + outward * SideFade, b + outward * SideFade })
                {
                    vertices.Add(p);
                    uvs.Add(Vector2.Scale(p, scale));
                }
                colors.Add(Shaded(a, inward, 1f));
                colors.Add(Shaded(b, inward, 1f));
                colors.Add(Shaded(a, inward, 0f));
                colors.Add(Shaded(b, inward, 0f));
                triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 1, first + 3 });
            }
            if (triangles.Count == 0)
            {
                return;
            }
            var mesh = new Mesh { name = parent.name + " soft sides" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            FaceTheCamera(mesh);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("SoftSides");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ParadiseNormals.LitMesh(texture, art.lit);
            renderer.sortingOrder = -102;
        }

        /// <summary>
        /// The channels the lit sprite shader's normals pass reads. Sprites and SpriteShape
        /// come with them; a plain mesh without them writes a zero normal and misses the sun.
        /// </summary>
        private static void FaceTheCamera(Mesh mesh)
        {
            var normals = new Vector3[mesh.vertexCount];
            var tangents = new Vector4[mesh.vertexCount];
            for (var i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.back;
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
            }
            mesh.normals = normals;
            mesh.tangents = tangents;
        }

        /// <summary>About how far apart a wall's piers stand, in units.</summary>
        private const float PierSpacing = 16f;

        /// <summary>Width of wall_pier.png, in units (128 px a unit).</summary>
        private const float PierWidth = 3f;

        /// <summary>Height of wall_pier.png and of wall_block.png, in units: the same courses.</summary>
        private const float PierTile = 8f;

        /// <summary>How far under the walk a pier's top starts, hidden behind the coping.</summary>
        private const float PierTuck = 0.45f;

        /// <summary>
        /// Quoined piers built into a terrace wall at even spacing, so a long wall is a row of
        /// bays and not one plane. One mesh per wall; v is world height over the tile, the
        /// mapping the wall's own fill has, so the piers' courses are the wall's courses.
        /// </summary>
        public static void Piers(Transform parent, List<Vector2> outline, ParadiseArt.ZoneArt art)
        {
            var xMin = float.MaxValue;
            var xMax = float.MinValue;
            var bottom = float.MaxValue;
            foreach (var p in outline)
            {
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
                bottom = Mathf.Min(bottom, p.y);
            }
            var count = Mathf.RoundToInt((xMax - xMin) / PierSpacing);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var x = xMin + (i + 0.5f) * (xMax - xMin) / count;
                var left = x - PierWidth * 0.5f;
                var right = x + PierWidth * 0.5f;
                if (left < xMin + 0.5f || right > xMax - 0.5f || Crossings(outline, x) > 2)
                {
                    continue;
                }
                // Only where the wall goes all the way down: under a slab over a hollow there is no
                // wall for a pier to be built into, and it would stand in the air.
                if (LowestOf(outline, left) > bottom + 0.5f || LowestOf(outline, right) > bottom + 0.5f)
                {
                    continue;
                }
                var topLeft = SurfaceOf(outline, left) - PierTuck;
                var topRight = SurfaceOf(outline, right) - PierTuck;
                // Across a step of the wall there is no single coping for it to stand under.
                if (Mathf.Abs(topLeft - topRight) > PierWidth * 0.6f)
                {
                    continue;
                }
                var first = vertices.Count;
                vertices.Add(new Vector3(left, topLeft, 0f));
                vertices.Add(new Vector3(right, topRight, 0f));
                vertices.Add(new Vector3(left, bottom, 0f));
                vertices.Add(new Vector3(right, bottom, 0f));
                uvs.Add(new Vector2(0f, topLeft / PierTile));
                uvs.Add(new Vector2(1f, topRight / PierTile));
                uvs.Add(new Vector2(0f, bottom / PierTile));
                uvs.Add(new Vector2(1f, bottom / PierTile));
                triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 1, first + 3 });
            }
            if (triangles.Count == 0)
            {
                return;
            }
            var mesh = new Mesh { name = parent.name + " piers" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            var colors = new Color[vertices.Count];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
            mesh.colors = colors;
            FaceTheCamera(mesh);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Piers");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ParadiseNormals.LitMesh(Get("wall_pier").texture, art.lit);
            renderer.sortingOrder = -102;
        }

        /// <summary>The lowest point of the outline over x: where the shape's body ends there.</summary>
        private static float LowestOf(List<Vector2> outline, float x)
        {
            var low = float.MaxValue;
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                if ((a.x <= x && b.x >= x) || (b.x <= x && a.x >= x))
                {
                    var y = Mathf.Approximately(a.x, b.x)
                        ? Mathf.Min(a.y, b.y)
                        : Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                    low = Mathf.Min(low, y);
                }
            }
            return low;
        }

        /// <summary>How tall a terrace's side must be to be built with quoins (a slab's end is not).</summary>
        public const float QuoinMinFace = 4f;

        /// <summary>How far a corner's long stones stand proud of the face (its short ones are flush).</summary>
        private const float QuoinProud = 0.28f;

        /// <summary>
        /// Quoins up every side of a built terrace that shows (the walls of the root hollow, the
        /// dell's east wall, the ends of the roots route): a wall's corner is built of long and short
        /// stones bonded into its courses, not cut through them. Each runs from the top of the side
        /// down to where ground or another wall stands against it. Not at the zone's ends, where the
        /// next zone's terrace carries on.
        /// </summary>
        public static void Quoins(
            Transform parent,
            ParadiseLayout.Zone zone,
            ParadiseLayout.Shape shape,
            ParadiseArt.ZoneArt art
        )
        {
            var outline = new List<Vector2>();
            foreach (var p in shape.outline)
            {
                outline.Add(new Vector2(p.x, p.y));
            }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                var x = a.x;
                var top = Mathf.Max(a.y, b.y);
                var bottom = Mathf.Min(a.y, b.y);
                if (
                    Mathf.Abs(a.x - b.x) > 0.01f
                    || top - bottom < QuoinMinFace
                    || x < zone.xMin + 1.5f
                    || x > zone.xMax - 1.5f
                )
                {
                    continue;
                }
                // It faces away from the side the shape is on.
                var outward = Inside(outline, new Vector2(x - 0.3f, (top + bottom) / 2f)) ? 1f : -1f;
                var end = top;
                for (var y = top - 0.25f; y > bottom; y -= 0.25f)
                {
                    if (InsideSolid(zone, new Vector2(x + outward * 0.3f, y)))
                    {
                        break;
                    }
                    end = y;
                }
                if (top - end < QuoinMinFace)
                {
                    continue;
                }
                var outer = x + outward * QuoinProud;
                var inner = outer - outward * PierWidth;
                var first = vertices.Count;
                vertices.Add(new Vector3(Mathf.Min(outer, inner), top, 0f));
                vertices.Add(new Vector3(Mathf.Max(outer, inner), top, 0f));
                vertices.Add(new Vector3(Mathf.Min(outer, inner), end, 0f));
                vertices.Add(new Vector3(Mathf.Max(outer, inner), end, 0f));
                uvs.Add(new Vector2(0f, top / PierTile));
                uvs.Add(new Vector2(1f, top / PierTile));
                uvs.Add(new Vector2(0f, end / PierTile));
                uvs.Add(new Vector2(1f, end / PierTile));
                triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 1, first + 3 });
            }
            if (triangles.Count == 0)
            {
                return;
            }
            var mesh = new Mesh { name = parent.name + " quoins" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            var colors = new Color[vertices.Count];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
            mesh.colors = colors;
            FaceTheCamera(mesh);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Quoins");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ParadiseNormals.LitMesh(Get("wall_pier").texture, art.lit);
            renderer.sortingOrder = -102;
        }

        private static bool Inside(List<Vector2> points, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>The highest point of the outline over x: the walkable surface there.</summary>
        private static float SurfaceOf(List<Vector2> outline, float x)
        {
            var top = float.MinValue;
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                if ((a.x <= x && b.x >= x) || (b.x <= x && a.x >= x))
                {
                    var y = Mathf.Approximately(a.x, b.x)
                        ? Mathf.Max(a.y, b.y)
                        : Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                    top = Mathf.Max(top, y);
                }
            }
            return top;
        }

        /// <summary>How many times the outline crosses the vertical line at x.</summary>
        private static int Crossings(List<Vector2> outline, float x)
        {
            var n = 0;
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                if ((a.x <= x) != (b.x <= x))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>The polygon without repeated or collinear points (clipping leaves both).</summary>
        private static List<Vector2> Simplify(List<Vector2> polygon)
        {
            var result = new List<Vector2>();
            foreach (var p in polygon)
            {
                if (result.Count == 0 || (result[result.Count - 1] - p).sqrMagnitude > 1e-6f)
                {
                    result.Add(p);
                }
            }
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude <= 1e-6f)
            {
                result.RemoveAt(result.Count - 1);
            }
            for (var i = result.Count - 1; i >= 0 && result.Count > 3; i--)
            {
                var a = result[(i + result.Count - 1) % result.Count];
                var b = result[i];
                var c = result[(i + 1) % result.Count];
                if (Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) < 1e-5f)
                {
                    result.RemoveAt(i);
                }
            }
            return result;
        }

        /// <summary>Ear clipping: triangles over a simple polygon, as indices into it.</summary>
        private static List<int> Triangulate(List<Vector2> polygon)
        {
            var result = new List<int>();
            var index = new List<int>();
            var area = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
                index.Add(i);
            }
            if (area < 0f)
            {
                index.Reverse();
            }
            var guard = 0;
            while (index.Count > 3 && guard++ < polygon.Count * polygon.Count)
            {
                var clipped = false;
                for (var i = 0; i < index.Count; i++)
                {
                    var ia = index[(i + index.Count - 1) % index.Count];
                    var ib = index[i];
                    var ic = index[(i + 1) % index.Count];
                    var a = polygon[ia];
                    var b = polygon[ib];
                    var c = polygon[ic];
                    var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross <= 1e-6f)
                    {
                        continue;
                    }
                    var inside = false;
                    foreach (var j in index)
                    {
                        if (j != ia && j != ib && j != ic && InTriangle(polygon[j], a, b, c))
                        {
                            inside = true;
                            break;
                        }
                    }
                    if (inside)
                    {
                        continue;
                    }
                    result.Add(ia);
                    result.Add(ib);
                    result.Add(ic);
                    index.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // Degenerate (collinear) leftovers: drop a vertex and go on.
                    index.RemoveAt(0);
                }
            }
            if (index.Count == 3)
            {
                result.Add(index[0]);
                result.Add(index[1]);
                result.Add(index[2]);
            }
            return result;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            var d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            var d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            var negative = d1 < 0f || d2 < 0f || d3 < 0f;
            var positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        /// <summary>A polygon clipped to the vertical strip x0..x1 (Sutherland-Hodgman, two edges).</summary>
        private static List<Vector2> ClipToStrip(List<Vector2> polygon, float x0, float x1)
        {
            var left = ClipEdge(polygon, x0, true);
            return ClipEdge(left, x1, false);
        }

        private static List<Vector2> ClipEdge(List<Vector2> polygon, float x, bool keepRight)
        {
            var result = new List<Vector2>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var aIn = keepRight ? a.x >= x : a.x <= x;
                var bIn = keepRight ? b.x >= x : b.x <= x;
                if (aIn)
                {
                    result.Add(a);
                }
                if (aIn != bIn)
                {
                    var t = (x - a.x) / (b.x - a.x);
                    result.Add(new Vector2(x, Mathf.Lerp(a.y, b.y, t)));
                }
            }
            return result;
        }

        private static Material DepthMaterial()
        {
            var material = ParadiseArt.SaveMaterial(
                DepthMaterialName,
                "Universal Render Pipeline/2D/Sprite-Unlit-Default"
            );
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DepthTexturePath);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static readonly Color DepthShadeColor = new Color(0.15f, 0.19f, 0.3f);

        /// <summary>How much of the cool shade lies on the stone this far under the walk.</summary>
        private static float DepthShade(float depth) =>
            // The view shows about four units under the walk: the shade has to start within it.
            Mathf.SmoothStep(0f, 0.56f, Mathf.InverseLerp(0.8f, 4.6f, depth))
            + Mathf.SmoothStep(0f, 0.14f, Mathf.InverseLerp(5f, 22f, depth));

        /// <summary>How far along the ground the weathering repeats, in units.</summary>
        private const float WeatherPeriod = 24f;

        /// <summary>
        /// The depth shade's texture, written once per build: across, one WeatherPeriod of
        /// ground; down, DepthReach units below the walk (the top row at the surface). It is
        /// the stone's weathering as well as its shade, so a wall or a rock face looks aged
        /// and lit by the sky rather than printed:
        /// - the cool shadow deepening below the walk;
        /// - broad patches, some darker and warmer (old grime), some paler (sun-bleached),
        ///   so neighbouring tiles of the same texture never read the same;
        /// - streaks of grime and moss running down from under the coping, where rain drips.
        /// Every pattern repeats exactly across the width, so the ground has no seam.
        /// </summary>
        private static void WriteDepthTexture()
        {
            const int w = 512;
            const int h = 256;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var shade = DepthShadeColor;
            var grime = new Color(0.27f, 0.22f, 0.13f);
            var moss = new Color(0.24f, 0.31f, 0.14f);
            var bleach = new Color(1f, 0.97f, 0.9f);
            var streaks = Streaks();
            for (var y = 0; y < h; y++)
            {
                var depth = (h - 1 - y) / (float)(h - 1) * DepthReach;
                var v = depth / DepthReach;
                var a = DepthShade(depth);
                for (var x = 0; x < w; x++)
                {
                    var u = x / (float)w;
                    var c = new Color(shade.r, shade.g, shade.b, a);
                    // Broad patches (periodic in u, so the texture repeats without a seam).
                    var n =
                        0.5f * Mathf.Sin(2f * Mathf.PI * (3f * u) + 1.3f + 2.2f * v)
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * (7f * u) + 2f * Mathf.Sin(2f * Mathf.PI * 1.5f * v))
                        + 0.2f * Mathf.Sin(2f * Mathf.PI * (13f * u) + 5f * v + 0.7f);
                    var below = Mathf.InverseLerp(0.4f, 1.6f, depth);
                    if (n > 0f)
                    {
                        c = Over(c, grime, 0.16f * n * below);
                    }
                    else
                    {
                        c = Over(c, bleach, 0.1f * -n * below);
                    }
                    // Drip streaks from under the walk's edge.
                    foreach (var (sx, width, length, strength, mossy) in streaks)
                    {
                        var dx = Mathf.Abs(Mathf.Repeat(u - sx + 0.5f, 1f) - 0.5f) * WeatherPeriod;
                        if (dx > width || depth < 0.5f || depth > length + 0.5f)
                        {
                            continue;
                        }
                        var across = 1f - dx / width;
                        var along = 1f - Mathf.InverseLerp(0.5f, length + 0.5f, depth);
                        c = Over(c, mossy ? moss : grime, strength * across * across * along);
                    }
                    texture.SetPixel(x, y, c);
                }
            }
            Directory.CreateDirectory(ParadiseArt.DerivedFolder);
            File.WriteAllBytes(DepthTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(DepthTexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(DepthTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>The drip streaks in one period: where (0..1), half-width and length (units), strength, moss or grime.</summary>
        private static List<(float x, float width, float length, float strength, bool mossy)> Streaks()
        {
            var result = new List<(float, float, float, float, bool)>();
            for (var i = 0; i < 26; i++)
            {
                var x = Hash(i, 41f);
                var width = 0.06f + Hash(i, 42f) * 0.22f;
                var length = 0.8f + Hash(i, 43f) * Hash(i, 44f) * 4.5f;
                var strength = 0.12f + Hash(i, 45f) * 0.2f;
                result.Add((x, width, length, strength, Hash(i, 46f) > 0.6f));
            }
            return result;
        }

        /// <summary>Straight-alpha "over": `top` at `amount` laid on `under`.</summary>
        private static Color Over(Color under, Color top, float amount)
        {
            amount = Mathf.Clamp01(amount);
            var a = amount + under.a * (1f - amount);
            if (a <= 1e-5f)
            {
                return new Color(top.r, top.g, top.b, 0f);
            }
            var r = (top.r * amount + under.r * under.a * (1f - amount)) / a;
            var g = (top.g * amount + under.g * under.a * (1f - amount)) / a;
            var b = (top.b * amount + under.b * under.a * (1f - amount)) / a;
            return new Color(r, g, b, a);
        }

        // ------------------------------------------------------------------- caps

        /// <summary>How much of a strip's length its rounded end cap takes, in units.</summary>
        public const float CapLength = 0.45f;

        /// <summary>How far a strip stops short of its end: it runs a little under the cap, so no seam shows.</summary>
        public const float CapInset = CapLength - 0.04f;

        /// <summary>The strips that get rounded end caps: decks, terrace copings, grass lips.</summary>
        private static readonly string[] CappedStrips = { "deck_top", "wall_cap", "grass_lip" };

        private static string CapPath(string strip, bool left) =>
            $"{ParadiseArt.DerivedFolder}/{strip}_cap_{(left ? "left" : "right")}.png";

        /// <summary>Whether a strip's two caps exist (made by Prepare from the kit).</summary>
        public static bool CapsReady(string strip) =>
            !string.IsNullOrEmpty(strip)
            && AssetDatabase.LoadAssetAtPath<Sprite>(CapPath(strip, true)) != null
            && AssetDatabase.LoadAssetAtPath<Sprite>(CapPath(strip, false)) != null;

        /// <summary>
        /// A rounded cap finishing a strip at one end: its painted strip stops CapLength short
        /// of `end` (the caller insets it), and the cap fills that length, its outer corners
        /// rounded like worn stone. `inward` points from the end along the strip.
        /// </summary>
        public static void Cap(
            Transform parent,
            string strip,
            Vector2 end,
            Vector2 inward,
            bool left,
            Material material,
            int order
        )
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CapPath(strip, left));
            var along = left ? inward : -inward;
            var r = ParadiseArt.Sprite(
                parent,
                left ? "CapLeft" : "CapRight",
                sprite,
                material,
                order,
                end + inward * CapLength,
                Vector3.one
            );
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
        }

        /// <summary>A deck's two ends capped (its strip is inset by ParadiseGreybox.BuildLedge).</summary>
        public static void DeckCaps(Transform parent, List<ParadiseLayout.P> points, ParadiseArt.ZoneArt art)
        {
            var last = points.Count - 1;
            Cap(parent, "deck_top", points[0].V, (points[1].V - points[0].V).normalized, true, art.lit, -90);
            Cap(
                parent,
                "deck_top",
                points[last].V,
                (points[last - 1].V - points[last].V).normalized,
                false,
                art.lit,
                -90
            );
        }

        /// <summary>
        /// Every capped strip's two caps: a slice from the middle of the strip (its ends are
        /// ragged), with the outer corners rounded, a small radius at the top and a large one
        /// under the stone, anti-aliased, pivoted like the strip so it lines up with it.
        /// </summary>
        private static void WriteCaps()
        {
            foreach (var strip in CappedStrips)
            {
                var folder = strip == "grass_lip" ? "Nature" : "Architecture";
                var sourcePath = $"{KitRoot}/{folder}/{strip}.png";
                if (!File.Exists(sourcePath))
                {
                    continue;
                }
                var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                var ppu = importer != null ? importer.spritePixelsPerUnit : 64f;
                var pivotY = 0.5f;
                if (importer != null)
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    pivotY = settings.spritePivot.y;
                }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(sourcePath));
                var w = Mathf.Max(4, Mathf.RoundToInt(CapLength * ppu));
                var x0 = Mathf.RoundToInt(source.width * 0.4f);
                WriteCap(source, x0, w, true, CapPath(strip, true), ppu, pivotY);
                WriteCap(source, x0, w, false, CapPath(strip, false), ppu, pivotY);
                Object.DestroyImmediate(source);
            }
        }

        private static void WriteCap(
            Texture2D source,
            int x0,
            int w,
            bool outerLeft,
            string path,
            float ppu,
            float pivotY
        )
        {
            var h = source.height;
            var cap = new Texture2D(w, h, TextureFormat.RGBA32, false);
            // The painted body's extent (rows with real alpha), so the radii fit the stone, not the padding.
            var lowest = h;
            var highest = -1;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (source.GetPixel(x0 + x, y).a > 0.5f)
                    {
                        lowest = Mathf.Min(lowest, y);
                        highest = Mathf.Max(highest, y);
                    }
                }
            }
            if (highest < 0)
            {
                lowest = 0;
                highest = h - 1;
            }
            var body = highest - lowest + 1;
            var top = body * 0.16f;
            var bottom = Mathf.Min(w * 0.95f, body * 0.45f);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var c = source.GetPixel(x0 + x, y);
                    var fromOuter = outerLeft ? x + 0.5f : w - x - 0.5f;
                    var fromBottom = y - lowest + 0.5f;
                    var fromTop = highest - y + 0.5f;
                    var coverage = 1f;
                    if (fromOuter < top && fromTop < top)
                    {
                        coverage = Corner(fromOuter, fromTop, top);
                    }
                    else if (fromOuter < bottom && fromBottom < bottom)
                    {
                        coverage = Corner(fromOuter, fromBottom, bottom);
                    }
                    c.a *= coverage;
                    cap.SetPixel(x, y, c);
                }
            }
            File.WriteAllBytes(path, cap.EncodeToPNG());
            Object.DestroyImmediate(cap);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            // On the joint with the strip, at the strip's own pivot height.
            settings.spritePivot = new Vector2(outerLeft ? 1f : 0f, pivotY);
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// The arches for an aqueduct's lower tier: arch_span and arch_span_wide without the
        /// walkway band on top (its grass and lip), so a tier reads as an arcade carrying the
        /// arcade above, not a floating stretch of path. Same size, pivot and density.
        /// </summary>
        private static void WriteTierArches()
        {
            foreach (var name in new[] { "arch_span", "arch_span_wide" })
            {
                var sourcePath = $"{KitRoot}/Architecture/{name}.png";
                if (!File.Exists(sourcePath))
                {
                    continue;
                }
                var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                var ppu = importer != null ? importer.spritePixelsPerUnit : 64f;
                var pivot = new Vector2(0.5f, 0f);
                if (importer != null)
                {
                    var read = new TextureImporterSettings();
                    importer.ReadTextureSettings(read);
                    pivot = read.spritePivot;
                }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(sourcePath));
                var h = source.height;
                // The walkway band is the top 9 percent (the walkable row is at 6); cut it with a soft edge.
                var cut = Mathf.RoundToInt(h * 0.09f);
                var soft = Mathf.Max(2, Mathf.RoundToInt(h * 0.02f));
                for (var y = h - cut - soft; y < h; y++)
                {
                    var fromTop = h - 1 - y;
                    var keep = Mathf.Clamp01((fromTop - cut) / (float)soft);
                    for (var x = 0; x < source.width; x++)
                    {
                        var c = source.GetPixel(x, y);
                        c.a *= keep;
                        source.SetPixel(x, y, c);
                    }
                }
                var path = $"{ParadiseArt.DerivedFolder}/{name}_tier.png";
                File.WriteAllBytes(path, source.EncodeToPNG());
                Object.DestroyImmediate(source);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var tierImporter = (TextureImporter)AssetImporter.GetAtPath(path);
                tierImporter.textureType = TextureImporterType.Sprite;
                tierImporter.spriteImportMode = SpriteImportMode.Single;
                tierImporter.spritePixelsPerUnit = ppu;
                var settings = new TextureImporterSettings();
                tierImporter.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                tierImporter.SetTextureSettings(settings);
                tierImporter.alphaIsTransparency = true;
                tierImporter.wrapMode = TextureWrapMode.Clamp;
                tierImporter.filterMode = FilterMode.Bilinear;
                tierImporter.mipmapEnabled = false;
                tierImporter.maxTextureSize = ParadiseArt.MaxTextureSize;
                tierImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                tierImporter.SaveAndReimport();
            }
        }

        /// <summary>Coverage of a pixel in a rounded corner of the given radius, with a one-pixel soft edge.</summary>
        private static float Corner(float u, float v, float radius)
        {
            var dx = radius - u;
            var dy = radius - v;
            var d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(0.5f - d);
        }

        public const string CheckpointPath = "Assets/Prefabs/Paradise/Checkpoint Garden.prefab";
        public const string CatalogPath = "Assets/Data/Placements Paradise.asset";
        private const string PrototypeCheckpointPath = "Assets/Prefabs/Pickups/Checkpoint.prefab";

        /// <summary>
        /// The placements catalog for the garden: the shared one, with the save spawning a
        /// marble shrine (a short column carrying a lantern that lights while it is the active
        /// save) instead of the prototype's pixel-art campfire. Rebuilt with the zones.
        /// </summary>
        public static PlacementCatalog Catalog(ParadiseArt.ZoneArt art)
        {
            var source = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(ParadiseGreybox.CatalogPath);
            if (source == null || !Sprites.ContainsKey("lantern") || !Sprites.ContainsKey("column_shaft"))
            {
                return source;
            }
            var shrine = CheckpointPrefab(art);
            var catalog = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = Object.Instantiate(source);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, catalog);
            }
            catalog.name = Path.GetFileNameWithoutExtension(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("key").stringValue == "save")
                {
                    entry.FindPropertyRelative("prefab").objectReferenceValue = shrine;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static GameObject CheckpointPrefab(ParadiseArt.ZoneArt art)
        {
            var root = PrefabUtility.LoadPrefabContents(PrototypeCheckpointPath);
            try
            {
                var old = root.transform.Find("Sprite");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
                // Origin at the foot, like the prototype's, so the respawn point stands the hero on the floor.
                var shrine = new GameObject("Shrine").transform;
                shrine.SetParent(root.transform, false);
                ParadiseArt.Sprite(
                    shrine,
                    "Base",
                    Get("column_base"),
                    art.lit,
                    -2,
                    new Vector3(0f, -0.05f),
                    Vector3.one * 0.9f
                );
                var shaft = ParadiseArt.Sprite(
                    shrine,
                    "Shaft",
                    Get("column_shaft"),
                    art.lit,
                    -3,
                    new Vector3(0f, 0.7f),
                    new Vector3(ShaftWidth * 0.9f, 1f, 1f)
                );
                shaft.drawMode = SpriteDrawMode.Tiled;
                shaft.tileMode = SpriteTileMode.Continuous;
                shaft.size = new Vector2(1f, 1.4f);
                ParadiseArt.Sprite(
                    shrine,
                    "Capital",
                    Get("column_capital"),
                    art.lit,
                    -2,
                    new Vector3(0f, 1.8f),
                    Vector3.one * 0.9f
                );
                // The lantern stands on the capital (its pivot is its top).
                var lantern = ParadiseArt.Sprite(
                    shrine,
                    "Lantern",
                    Get("lantern"),
                    art.lit,
                    -1,
                    new Vector3(0f, 2.7f + 1.2f * 1.1f - 0.24f),
                    Vector3.one * 1.1f
                );
                var lit = new GameObject("Lit");
                lit.transform.SetParent(lantern.transform, false);
                if (Sprites.TryGetValue("lantern_glow", out var glowSprite))
                {
                    var glow = ParadiseArt.Sprite(
                        lit.transform,
                        "Glow",
                        glowSprite,
                        art.glow,
                        0,
                        lantern.transform.position,
                        Vector3.one
                    );
                    glow.transform.localScale = Vector3.one;
                }
                var light = new GameObject("Light");
                light.transform.SetParent(lit.transform, false);
                light.transform.localPosition = new Vector3(0f, -0.6f, 0f);
                var worldLight = light.AddComponent<WorldLight>();
                var lightSettings = new SerializedObject(worldLight);
                lightSettings.FindProperty("color").colorValue = new Color(1f, 0.85f, 0.55f);
                lightSettings.FindProperty("energy").floatValue = 0.45f;
                lightSettings.FindProperty("radiusPixels").floatValue = 6f * Player.PlayerTuning.PixelsPerUnit;
                lightSettings.ApplyModifiedPropertiesWithoutUndo();
                lit.SetActive(false);

                var checkpoint = root.GetComponent<Descender.Pickups.Checkpoint>();
                var settings = new SerializedObject(checkpoint);
                settings.FindProperty("sprite").objectReferenceValue = lantern;
                var frames = settings.FindProperty("frames");
                frames.arraySize = 2;
                frames.GetArrayElementAtIndex(0).objectReferenceValue = lantern.sprite;
                frames.GetArrayElementAtIndex(1).objectReferenceValue = lantern.sprite;
                settings.FindProperty("lit").objectReferenceValue = lit;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Directory.CreateDirectory(Path.GetDirectoryName(CheckpointPath));
                return PrefabUtility.SaveAsPrefabAsset(root, CheckpointPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>One screen wide (the paintings are 32 units, the view 20), offset on screen by `y`.</summary>
        private static Transform Pinned(Transform layer, float y)
        {
            layer.localScale = Vector3.one * (20f / 32f);
            var serialized = new SerializedObject(layer.GetComponent<BackdropLayer>());
            serialized.FindProperty("viewOffset").vector2Value = new Vector2(0f, y);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return layer;
        }

        private static void Drift(Transform layer, float pixelsPerSecond)
        {
            var serialized = new SerializedObject(layer.GetComponent<BackdropLayer>());
            serialized.FindProperty("drift").vector2Value = new Vector2(pixelsPerSecond, 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Rocks a layer sideways by `pixels` (level pixels) over `seconds`.</summary>
        private static void Sway(Transform layer, float pixels, float seconds)
        {
            var serialized = new SerializedObject(layer.GetComponent<BackdropLayer>());
            serialized.FindProperty("sway").vector2Value = new Vector2(pixels, seconds);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The canopy's two bands (canopy.py).</summary>
        public const string CanopyBack = "canopy_back";

        public const string CanopyFront = "canopy_front";

        /// <summary>
        /// Where the canopy's roof draws: over the backdrops (to -958) and behind the sunbeams
        /// (-300), so the light falls from it, and behind the orchard's own trees (-150).
        /// </summary>
        public const int CanopyBackOrder = -310;

        /// <summary>The near leaves draw on the Foreground layer, over the framing (400 to 402).</summary>
        public const int CanopyFrontOrder = 403;

        /// <summary>How much of the camera's travel the roof follows (nearly the world's), and the near leaves (more).</summary>
        public const float CanopyBackScroll = 0.9f;

        public const float CanopyFrontScroll = 1.3f;

        /// <summary>
        /// Where each band hangs from the view's top: its middle this far above the view's middle
        /// (the roof is 5 units deep on screen, the near leaves 3.1; the view is 11.25 high).
        /// </summary>
        public const float CanopyBackDrop = 3.125f;

        public const float CanopyFrontDrop = 4.06f;

        private static SpriteRenderer Place(
            Transform parent,
            string name,
            Vector2 at,
            float scale,
            bool flip,
            Material material,
            int order,
            ParadiseArt.ZoneArt art
        )
        {
            material = SwayMaterial(name, material, art);
            var r = ParadiseArt.Sprite(parent, name, Get(name), material, order, at, Vector3.one * scale);
            r.flipX = flip;
            if (Sprites.TryGetValue(name + "_glow", out var glow))
            {
                var g = ParadiseArt.Sprite(
                    r.transform,
                    "Glow",
                    glow,
                    art.glow,
                    order + 1,
                    r.transform.position,
                    Vector3.one
                );
                g.transform.localScale = Vector3.one;
                g.flipX = flip;
            }
            return r;
        }

        private static readonly Dictionary<string, Material> SwayVariants = new Dictionary<string, Material>();

        /// <summary>
        /// The material a garden piece sways with, by what it is, all on the Sprite Lit Sway
        /// shader so they batch (ParadiseSway.hlsl, driven by GardenWind): lilies, ferns and
        /// grass light and quick; bushes and hedges a little stiffer; blossom trees slow and
        /// heavy; ivy, vines and wisteria hanging from their top, rippling down the strands;
        /// the banner fluttering like cloth; lanterns swinging a touch. Each kind is a copy of
        /// the material it would have had (its haze kept), saved beside it; anything else,
        /// or a material on another shader, is returned as it is.
        /// </summary>
        public static Material SwayMaterial(string name, Material material, ParadiseArt.ZoneArt art)
        {
            if (material == null || art.plant == null)
            {
                return material;
            }
            // Banners and lanterns were drawn plain lit; they move now, so they take the plant shader.
            var basis = material == art.lit && (name == "banner" || name == "lantern") ? art.plant : material;
            if (basis.shader != art.plant.shader)
            {
                return material;
            }
            var kind = SwayKindOf(name);
            if (kind == null)
            {
                return material;
            }
            var key = basis.name + " " + kind;
            if (SwayVariants.TryGetValue(key, out var variant) && variant != null)
            {
                return variant;
            }
            variant = ParadiseArt.SaveMaterial(key, basis.shader.name);
            variant.CopyPropertiesFromMaterial(basis);
            // amount (units at the tip per unit wind, radians when hanging), natural frequency
            // (rad/s), variation, flutter (or ripple), flutter speed (times the frequency), lag down
            // the length (rad/unit), push at the tip; hangs from its pivot.
            switch (kind)
            {
                case "Grass":
                    SetSway(variant, 0.09f, 6.5f, 0.8f, 0.012f, 1.6f, 0.4f, 0.35f, false);
                    break;
                case "Shrub":
                    SetSway(variant, 0.06f, 4f, 0.7f, 0.012f, 2.2f, 0.4f, 0.25f, false);
                    break;
                case "Tree":
                    SetSway(variant, 0.12f, 1.5f, 0.6f, 0.01f, 4f, 0.4f, 0f, false);
                    break;
                case "Ivy":
                    SetSway(variant, 0.04f, 2.2f, 0.7f, 0.05f, 2.6f, 0.45f, 0.3f, true);
                    break;
                case "Cloth":
                    SetSway(variant, 0.06f, 2.4f, 0.6f, 0.09f, 3.5f, 0.6f, 0.25f, true);
                    break;
                default: // Lantern: a rigid pendulum.
                    SetSway(variant, 0.03f, 2.9f, 0.5f, 0f, 1f, 0.02f, 0.15f, true);
                    break;
            }
            EditorUtility.SetDirty(variant);
            SwayVariants[key] = variant;
            return variant;
        }

        /// <summary>The framing's material: its shade is in the art, so no haze; slow, heavy sway.</summary>
        private static Material FrontMaterial(bool hangs, ParadiseArt.ZoneArt art)
        {
            var material = ParadiseArt.SaveMaterial(
                $"{art.foreground.name} {(hangs ? "Hanging" : "Rising")}",
                art.foreground.shader.name
            );
            material.CopyPropertiesFromMaterial(art.foreground);
            material.SetFloat("_HazeAmount", 0f);
            if (hangs)
            {
                SetSway(material, 0.012f, 1.1f, 0.6f, 0.02f, 2.2f, 0.25f, 0f, true);
            }
            else
            {
                SetSway(material, 0.14f, 1.1f, 0.6f, 0.01f, 2.5f, 0.4f, 0f, false);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string SwayKindOf(string name)
        {
            if (name.StartsWith("lily") || name.StartsWith("grass") || name == "fern")
            {
                return "Grass";
            }
            if (name == "blossom_tree")
            {
                return "Tree";
            }
            if (name.StartsWith("ivy") || name.StartsWith("vine") || name == "wisteria_drape")
            {
                return "Ivy";
            }
            if (name == "banner")
            {
                return "Cloth";
            }
            if (name == "lantern")
            {
                return "Lantern";
            }
            return name.Contains("bush") || name.Contains("hedge") ? "Shrub" : null;
        }

        private static void SetSway(
            Material material,
            float amount,
            float frequency,
            float variation,
            float flutter,
            float flutterSpeed,
            float lag,
            float push,
            bool hangs
        )
        {
            material.SetFloat("_SwayAmount", amount);
            material.SetFloat("_SwaySpeed", frequency);
            material.SetFloat("_SwayScale", variation);
            material.SetFloat("_SwayFlutter", flutter);
            material.SetFloat("_SwayFlutterSpeed", flutterSpeed);
            material.SetFloat("_SwayLag", lag);
            material.SetFloat("_SwayHang", hangs ? 1f : 0f);
            material.SetFloat("_PushStrength", push);
            material.SetFloat("_PushRadius", hangs ? 1.2f : 1.3f);
            material.SetFloat("_SwayUseObjectY", 0f);
        }

        private const string ContactShadowPath = ParadiseArt.DerivedFolder + "/contact_shadow.png";

        /// <summary>
        /// A soft shadow where a prop meets the ground (AO, as Playdead grounds its props): a
        /// flat dark ellipse under its foot, about as wide as the prop, so nothing looks pasted on.
        /// </summary>
        private static void ContactShadow(
            Transform parent,
            string name,
            Vector2 at,
            float scale,
            ParadiseArt.ZoneArt art
        )
        {
            var shadow = AssetDatabase.LoadAssetAtPath<Sprite>(ContactShadowPath);
            if (shadow == null || !Sprites.TryGetValue(name, out var sprite))
            {
                return;
            }
            var width = sprite.bounds.size.x * scale * 0.95f;
            var r = ParadiseArt.Sprite(
                parent,
                "Shadow",
                shadow,
                art.unlit,
                -94,
                new Vector3(at.x, at.y + 0.04f, 0f),
                new Vector3(width / shadow.bounds.size.x, 0.42f / shadow.bounds.size.y, 1f)
            );
            r.color = new Color(0.1f, 0.12f, 0.2f, 0.5f);
        }

        /// <summary>A repeated piece made a little lighter or darker, so a run of them is not one stamp.</summary>
        private static void Vary(SpriteRenderer r, float x)
        {
            // Brightness and a little warmth both vary: no two plants of a kind are the same green.
            var v = 0.88f + Hash(x, 31f) * 0.12f;
            var warm = Hash(x, 32f) - 0.5f;
            r.color = new Color(v * (1f + 0.05f * warm), v, v * (1f - 0.07f * warm), 1f);
        }

        /// <summary>The contact shadow's texture: a soft-edged ellipse, darkest under the middle.</summary>
        private static void WriteContactShadow()
        {
            const int w = 128;
            const int h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var dx = (x + 0.5f) / w * 2f - 1f;
                    var dy = (y + 0.5f) / h * 2f - 1f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            Directory.CreateDirectory(ParadiseArt.DerivedFolder);
            File.WriteAllBytes(ContactShadowPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(ContactShadowPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ContactShadowPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// How far water landing at x can spread along its ground, left (x) and right (y), up to
        /// `limit`: it stops at a wall, at an edge, and where the ground steps or slopes away.
        /// </summary>
        public static Vector2 PoolReach(ParadiseLayout.Zone zone, float x, float foot, float limit)
        {
            const float step = 0.25f;
            var ground = ParadiseDressing.SurfaceY(zone, x, float.NaN, foot + PoolSearch);
            float Side(float sign)
            {
                for (var d = step; d <= limit; d += step)
                {
                    var at = x + sign * d;
                    var y = ParadiseDressing.SurfaceY(zone, at, float.NaN, foot + PoolSearch);
                    if (
                        float.IsNaN(y)
                        || Mathf.Abs(y - ground) > PoolMaxStep
                        || InsideSolid(zone, new Vector2(at + sign * PoolWallGap, ground + PoolWallTest))
                    )
                    {
                        return d - step;
                    }
                }
                return limit;
            }
            return float.IsNaN(ground) ? Vector2.zero : new Vector2(Side(-1f), Side(1f));
        }

        /// <summary>Each shape's outline as built (its curves sampled), kept while the shape lives.</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            ParadiseLayout.Shape,
            List<Vector2>
        > SampledOutlines = new System.Runtime.CompilerServices.ConditionalWeakTable<
            ParadiseLayout.Shape,
            List<Vector2>
        >();

        private static bool InsideSolid(ParadiseLayout.Zone zone, Vector2 p)
        {
            foreach (var shape in zone.shapes)
            {
                if (!shape.collides)
                {
                    continue;
                }
                var points = SampledOutlines.GetValue(
                    shape,
                    s =>
                    {
                        var (left, right) = ParadiseGreybox.Tangents(s.outline, true);
                        return ParadiseGreybox.Sample(s.outline, left, right, true);
                    }
                );
                var inside = false;
                for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                {
                    var a = points[i];
                    var b = points[j];
                    if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    {
                        inside = !inside;
                    }
                }
                if (inside)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How far a spout is set back into the wall or deck it comes out of, in units.</summary>
        public const float SpoutTuck = 0.35f;

        /// <summary>Where a fall's spout is pinned: its wall edge, set back into the wall or deck it leaves.</summary>
        public static float SpoutPivot(Waterfall w) =>
            w.x + (w.fromRight ? 1f : -1f) * (1.44f * SpoutScale(w) + SpoutTuck);

        public static float SpoutScale(Waterfall w) => Mathf.Clamp(w.width / 1.5f, 0.8f, 1.3f);

        private static void Fall(ParadiseLayout.Zone zone, Transform parent, Waterfall w, ParadiseArt.ZoneArt art)
        {
            var height = w.top - w.bottom;
            var tex = Get("waterfall_tex");
            var r = ParadiseArt.Sprite(
                parent,
                "Waterfall",
                tex,
                art.water,
                -105,
                new Vector3(w.x, w.bottom, 0f),
                Vector3.one
            );
            // The texture's pivot is its top: the fall hangs from its lip, stretched to its foot.
            r.transform.position = new Vector3(w.x, w.top, 0f);
            r.transform.localScale = new Vector3(w.width / tex.bounds.size.x, height / tex.bounds.size.y, 1f);
            // A fall with no pool goes down into the clouds: it thins into mist over its last stretch.
            var intoClouds = !w.foam;
            var thinOver = Mathf.Min(12f, height * 0.4f);
            r.gameObject.AddComponent<global::Descender.World.Paradise.Waterfall>().Configure(1f, intoClouds, thinOver);
            if (Sprites.ContainsKey("waterfall_spout"))
            {
                // The carved channel mouth the water leaves from.
                // Its pivot is the wall edge; the water leaves its lip 92 px out and 6 px up (64 px a unit).
                // Set back into the wall or the deck's end by a little: at the end of a broken span
                // the deck's rounded corner left sky between them, and the spout hung in the air.
                var scale = SpoutScale(w);
                // Turned (its sprite mirrored about the wall edge), it comes out of a wall on the
                // right, over its face, so it draws over the wall's blocks and piers.
                Place(
                    parent,
                    "waterfall_spout",
                    new Vector2(SpoutPivot(w), w.top - 0.09f * scale),
                    scale,
                    w.fromRight,
                    art.lit,
                    w.fromRight ? NicheOrder : -103,
                    art
                );
            }
            // The water's look lives in its shaders' defaults: each build puts the materials
            // back on them (the sheet keeps the zone's tint), so no stale value outlives a retune.
            void Defaults(Material m)
            {
                var fresh = new Material(m.shader);
                m.CopyPropertiesFromMaterial(fresh);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(m);
            }
            Material Water(string name, string shader)
            {
                var m = ParadiseArt.SaveMaterial(name, shader);
                Defaults(m);
                return m;
            }
            var tint = art.water.GetColor("_Color");
            Defaults(art.water);
            art.water.SetColor("_Color", tint);
            var droplets = Water("Paradise Water Droplets", "Descender/Water Spray");
            droplets.SetFloat("_Round", 1f);
            var puffs = Water("Paradise Water Puff", "Descender/Water Spray");
            puffs.SetTexture("_MainTex", Get("mist_soft").texture);
            puffs.SetFloat("_Round", 0f);
            var mist = Water("Paradise Water Mist", "Descender/Water Mist");
            var spray = new GameObject("Spray");
            spray.transform.SetParent(parent, false);
            if (w.foam)
            {
                // Where it lands: foam heaped under the fall, low so the deck stays readable
                // underfoot and behind the deck's railing and plants, as the fall is. In front of
                // the deck, the pool it lands in, mirroring what stands over it.
                const float heapHeight = 1.8f;
                const float ground = 0.5f;
                var reach = PoolReach(zone, w.x, w.bottom, w.width * 2.6f + 1f);
                var heap = ParadiseArt.Sprite(
                    parent,
                    "Foam",
                    tex,
                    Water("Paradise Water Foam", "Descender/Water Foam"),
                    -95,
                    new Vector3(w.x, w.bottom - ground + heapHeight, 0f),
                    new Vector3(w.width * 4.4f / tex.bounds.size.x, heapHeight / tex.bounds.size.y, 1f)
                );
                heap.gameObject.AddComponent<global::Descender.World.Paradise.WaterQuad>()
                    .Configure(new Vector4(w.width * 0.5f, ground, reach.x, reach.y));
                if (reach.x + reach.y >= PoolLeast)
                {
                    var surface = ParadiseArt.Sprite(
                        parent,
                        "Pool",
                        tex,
                        Water("Paradise Water Reflection", "Descender/Water Reflection"),
                        0,
                        new Vector3(w.x, ParadiseDressing.SurfaceY(zone, w.x, w.bottom, w.bottom + PoolSearch), 0f),
                        new Vector3(
                            (Mathf.Max(reach.x, reach.y) * 2f + 0.2f) / tex.bounds.size.x,
                            PoolDepth / tex.bounds.size.y,
                            1f
                        )
                    );
                    surface.sortingLayerName = WaterLayer;
                    surface
                        .gameObject.AddComponent<global::Descender.World.Paradise.WaterQuad>()
                        .Configure(new Vector4(reach.x, reach.y, 0f, 0f));
                }
                Mist(parent, new Vector2(w.x, w.bottom + 0.1f), w.width * 0.6f, 3.2f, 0.34f, mist, -84);
                spray.transform.position = new Vector3(w.x, w.bottom + 0.05f, 0f);
            }
            else
            {
                // Into the clouds: a tall plume over the stretch where the water thins away,
                // behind the play layer, veiling the fall's end into the sea of cloud.
                Mist(
                    parent,
                    new Vector2(w.x, w.bottom + thinOver * 0.2f),
                    w.width * 0.8f,
                    thinOver * 1.1f,
                    0.5f,
                    mist,
                    -104
                );
                spray.transform.position = new Vector3(w.x, w.bottom + thinOver * 0.45f, 0f);
            }
            spray
                .AddComponent<global::Descender.World.Paradise.WaterfallSpray>()
                .Configure(droplets, puffs, w.width, intoClouds, thinOver, w.foam ? -94 : -104);
        }

        /// <summary>
        /// A plume of spray mist rising from `source` (its middle, `halfWidth` either side),
        /// billowing up `rise` units and leaning downwind, at most `alpha` dense.
        /// </summary>
        private static void Mist(
            Transform parent,
            Vector2 source,
            float halfWidth,
            float rise,
            float alpha,
            Material material,
            int order
        )
        {
            var tex = Get("waterfall_tex");
            const float below = 0.8f;
            var size = new Vector2(halfWidth * 2f + rise * 1.7f + 1f, rise + below + 0.6f);
            var r = ParadiseArt.Sprite(
                parent,
                "Mist",
                tex,
                material,
                order,
                new Vector3(source.x + rise * 0.15f, source.y - below + size.y, 0f),
                new Vector3(size.x / tex.bounds.size.x, size.y / tex.bounds.size.y, 1f)
            );
            r.color = new Color(1f, 1f, 1f, alpha);
            r.gameObject.AddComponent<global::Descender.World.Paradise.WaterQuad>()
                .Configure(new Vector4(halfWidth, below, rise, -rise * 0.15f));
        }

        private static RegionParticles Air(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            int amount,
            Color color,
            float life,
            Vector2 dir,
            float spread,
            Vector2 speed,
            Vector2 size,
            float spin,
            float sway,
            Rect area
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<RegionParticles>();
            particles.Configure(sprite, amount, color, life, dir, spread, speed, size, spin, sway, material, area);
            return particles;
        }

        // ---------------------------------------------------------------- columns

        /// <summary>
        /// A deck's colonnade: marble columns (base on the ground, a shaft of any height, a
        /// capital) and, where the deck is level between two columns, a round arch carrying it.
        /// Columns that go down into the clouds have no base. Called with the column feet the
        /// builder found (the first surface below, or the cloud sea).
        /// </summary>
        public static void Colonnade(
            Transform parent,
            List<Vector2> line,
            List<(float x, float ground)> feet,
            float deckDepth,
            ParadiseArt.ZoneArt art
        )
        {
            var arches = Arches(line, feet, deckDepth, out var springs);
            foreach (var arch in arches)
            {
                var i = arch.index;
                var a = feet[i];
                var b = feet[i + 1];
                var sprite = Get(arch.wide ? "arch_span_wide" : "arch_span");
                var nominal = arch.wide ? 10f : 6f;
                var r = ParadiseArt.Sprite(
                    parent,
                    $"Arch{i}",
                    sprite,
                    art.lit,
                    -118,
                    new Vector3(arch.x, arch.spring, 0f),
                    new Vector3(arch.span / nominal, 1f, 1f)
                );
                r.sortingOrder = -118 + (i % 2);
                // A tall aqueduct gets a lower tier, as Roman ones do: from the walk below, its
                // deck is off the screen, and the columns alone would hang from the sky.
                var groundLevel = Mathf.Abs(a.ground - b.ground) < 0.3f;
                var intoClouds = Mathf.Min(a.ground, b.ground) <= ParadiseGreybox.CloudSea + 0.01f;
                if (groundLevel && !intoClouds && arch.spring - a.ground > 8f)
                {
                    var tierName = (arch.wide ? "arch_span_wide" : "arch_span") + "_tier";
                    var tier = ParadiseArt.Sprite(
                        parent,
                        $"Tier{i}",
                        Sprites.TryGetValue(tierName, out var tierSprite) ? tierSprite : sprite,
                        art.lit,
                        -118 + (i % 2),
                        new Vector3(a.x, a.ground + 3.2f, 0f),
                        new Vector3(arch.span / nominal, 1f, 1f)
                    );
                    tier.color = new Color(0.94f, 0.94f, 0.96f);
                }
            }
            for (var i = 0; i < feet.Count; i++)
            {
                Column(parent, $"Column{i}", feet[i].x, springs[i], feet[i].ground, art);
            }
        }

        /// <summary>One arch of a colonnade: between columns `index` and `index + 1`, from x, its springing.</summary>
        public struct ArchSpan
        {
            public int index;
            public float x;
            public float span;
            public float spring;
            public bool wide;

            /// <summary>The middle of its opening, under the keystone.</summary>
            public float Centre => x + span / 2f;

            /// <summary>The underside of its crown: what a lantern under the arch hangs from.</summary>
            public float Crown => spring + (wide ? WideArchCrown : ArchCrown);
        }

        /// <summary>How far above its springing the underside of an arch's crown is (arch_span, arch_span_wide).</summary>
        public const float ArchCrown = 2.55f;

        public const float WideArchCrown = 538f / 128f;

        /// <summary>
        /// The arches a colonnade carries, and where each column stops (its springing, or the
        /// deck's underside): wherever two neighbouring columns stand level, close enough and
        /// high enough, an arch carries the deck between them. A column that carries one arch
        /// stops lower, so the next span is not level and gets none: arches and open spans take
        /// turns along a long deck.
        /// </summary>
        public static List<ArchSpan> Arches(
            List<Vector2> line,
            List<(float x, float ground)> feet,
            float deckDepth,
            out float[] springs
        )
        {
            springs = new float[feet.Count];
            for (var i = 0; i < feet.Count; i++)
            {
                // Up into the deck by a little, measured at the capital's higher corner, so the
                // capital carries the deck with no sky between them on a slope either.
                var x = feet[i].x;
                var under = Mathf.Max(DeckY(line, x - CapitalHalfWidth), DeckY(line, x + CapitalHalfWidth));
                springs[i] = under - deckDepth + DeckSeat;
            }
            var arches = new List<ArchSpan>();
            for (var i = 0; i + 1 < feet.Count; i++)
            {
                var a = feet[i];
                var b = feet[i + 1];
                var span = b.x - a.x;
                var level = Mathf.Abs(springs[i] - springs[i + 1]) < 0.35f;
                var spring = Mathf.Min(springs[i], springs[i + 1]) - ArchRise;
                if (!level || span < 3.5f || span > 11f || spring - Mathf.Max(a.ground, b.ground) < 2.2f)
                {
                    continue;
                }
                arches.Add(
                    new ArchSpan
                    {
                        index = i,
                        x = a.x,
                        span = span,
                        spring = spring,
                        wide = span > 8f,
                    }
                );
                springs[i] = Mathf.Min(springs[i], spring);
                springs[i + 1] = Mathf.Min(springs[i + 1], spring);
            }
            return arches;
        }

        /// <summary>Where the pergolas stand, each by the x of its left column.</summary>
        public static float[] PergolasOf(int zone) =>
            zone == 1 ? new[] { 348f }
            : zone == 2 ? new[] { 533f, 1258f }
            : new float[0];

        /// <summary>From one column of a pergola to the other: an arch_span at its painted width.</summary>
        public const float PergolaSpan = 6f;

        /// <summary>How far above the walk a pergola's arch springs, well clear of the hero's head.</summary>
        public const float PergolaRise = 3.2f;

        /// <summary>How far above its springing the underside of the arch's crown is.</summary>
        private const float PergolaCrown = ArchCrown;

        /// <summary>A free-standing arch on two columns: a colonnade carrying nothing but its own coping.</summary>
        private static void Pergola(ParadiseLayout.Zone zone, Transform parent, float x, ParadiseArt.ZoneArt art)
        {
            var left = ParadiseDressing.SurfaceY(zone, x, float.NaN);
            var right = ParadiseDressing.SurfaceY(zone, x + PergolaSpan, float.NaN);
            var top = Mathf.Max(left, right) + PergolaRise + ArchRise - DeckSeat;
            Colonnade(
                parent,
                new List<Vector2> { new Vector2(x - 3f, top), new Vector2(x + PergolaSpan + 3f, top) },
                new List<(float x, float ground)> { (x, left), (x + PergolaSpan, right) },
                0f,
                art
            );
        }

        /// <summary>Half the capital's width, where it meets the deck above.</summary>
        private const float CapitalHalfWidth = 0.8f;

        /// <summary>How far a capital reaches up behind the deck it carries, so they meet without a gap.</summary>
        private const float DeckSeat = 0.3f;

        /// <summary>The shaft's width to match the capital's neck (the painted shaft is 0.86 units across, the neck 1).</summary>
        private const float ShaftWidth = 1.1f;

        /// <summary>How far an arch's crown sits above its springing line (arch_span: 256 px, walkable row 16).</summary>
        public const float ArchRise = (256f - 16f) / 64f;

        private static void Column(
            Transform parent,
            string name,
            float x,
            float top,
            float ground,
            ParadiseArt.ZoneArt art
        )
        {
            var column = new GameObject(name).transform;
            column.SetParent(parent, false);
            var intoClouds = ground <= ParadiseGreybox.CloudSea + 0.01f;
            var shaftFoot = intoClouds ? ground : ground + 0.95f;
            if (!intoClouds)
            {
                ParadiseArt.Sprite(
                    column,
                    "Base",
                    Get("column_base"),
                    art.lit,
                    -119,
                    new Vector3(x, ground - 0.05f, 0f),
                    Vector3.one
                );
                ContactShadow(column, "column_base", new Vector2(x, ground - 0.05f), 1.15f, art);
            }
            var capitalFoot = top - 1f;
            if (capitalFoot - shaftFoot > 0.2f)
            {
                var shaft = ParadiseArt.Sprite(
                    column,
                    "Shaft",
                    Get("column_shaft"),
                    art.lit,
                    -121,
                    new Vector3(x, shaftFoot - 0.15f, 0f),
                    new Vector3(ShaftWidth, 1f, 1f)
                );
                shaft.drawMode = SpriteDrawMode.Tiled;
                shaft.tileMode = SpriteTileMode.Continuous;
                shaft.size = new Vector2(1f, capitalFoot - shaftFoot + 0.45f);
            }
            ParadiseArt.Sprite(
                column,
                "Capital",
                Get("column_capital"),
                art.lit,
                -119,
                new Vector3(x, capitalFoot, 0f),
                Vector3.one
            );
        }

        private static float DeckY(List<Vector2> line, float x)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                if (x >= line[i].x && x <= line[i + 1].x)
                {
                    var t = Mathf.Approximately(line[i + 1].x, line[i].x)
                        ? 0f
                        : (x - line[i].x) / (line[i + 1].x - line[i].x);
                    return Mathf.Lerp(line[i].y, line[i + 1].y, t);
                }
            }
            return x < line[0].x ? line[0].y : line[line.Count - 1].y;
        }

        // ------------------------------------------------------------------- plan

        public struct Spot
        {
            public string name;
            public Vector2 at;
            public float scale;
            public bool flip;
        }

        public struct Waterfall
        {
            public float x;
            public float top;
            public float bottom;
            public float width;
            public bool foam;

            /// <summary>Whether it leaves a wall on its right (its spout turned to face west); else on its left.</summary>
            public bool fromRight;
        }

        public sealed class Plan
        {
            public readonly List<Spot> plants = new List<Spot>();
            public readonly List<Spot> ruins = new List<Spot>();
            public readonly List<Spot> hanging = new List<Spot>();
            public readonly List<Spot> foreground = new List<Spot>();
            public readonly List<Waterfall> water = new List<Waterfall>();
            public readonly List<float> shafts = new List<float>();
            public readonly List<float> pergolas = new List<float>();
            public readonly List<Spot> walls = new List<Spot>();
            public readonly List<Spot> trees = new List<Spot>();
            public readonly List<Spot> framing = new List<Spot>();

            /// <summary>Blind arches set into the walls, each with a lantern hung in it (WallHangings).</summary>
            public readonly List<Spot> niches = new List<Spot>();

            /// <summary>The orchard's tree numbers, cut in the coping: each its numerals (as its name), centred at its spot.</summary>
            public readonly List<Spot> numbers = new List<Spot>();
        }

        /// <summary>A deterministic 0..1 from a position, so the dressing is the same every build.</summary>
        private static float Hash(float x, float salt) =>
            Mathf.Repeat(Mathf.Sin(x * 12.9898f + salt * 78.233f) * 43758.547f, 1f);

        public static Plan GardenPlan(ParadiseLayout.Zone zone)
        {
            var plan = new Plan();
            string[] plants =
            {
                "lily_cluster_a",
                "lily_cluster_b",
                "lily_cluster_c",
                "flower_bush",
                "fern",
                "lily_cluster_a",
                "fern",
            };
            // Plants on every walkable top, spaced irregularly: found on the terrain, never in the air.
            for (var x = zone.xMin + 3f; x < zone.xMax - 2f; x += 2.8f + Hash(x, 1f) * 4f)
            {
                var y = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                if (float.IsNaN(y) || NearGap(zone, x))
                {
                    continue;
                }
                var name = plants[(int)(Hash(x, 2f) * plants.Length) % plants.Length];
                // The lily clusters are painted 1.5 times bigger than the first kit's.
                var scale =
                    name == "flower_bush" ? 1.1f + Hash(x, 3f) * 0.4f
                    : name == "fern" ? 1.0f + Hash(x, 3f) * 0.4f
                    : 0.8f + Hash(x, 3f) * 0.3f;
                plan.plants.Add(
                    new Spot
                    {
                        name = name,
                        at = new Vector2(x, y - 0.1f),
                        scale = scale,
                        flip = Hash(x, 4f) > 0.5f,
                    }
                );
            }
            // Ivy and the odd banner or lantern hanging from every deck's underside.
            foreach (var bridge in zone.bridges)
            {
                var (left, right) = ParadiseGreybox.Tangents(bridge.deck, false);
                var line = ParadiseGreybox.Sample(bridge.deck, left, right, false);
                var x0 = bridge.deck[0].x;
                var x1 = bridge.deck[bridge.deck.Count - 1].x;
                var ornament =
                    x1 - x0 > 9f ? DeckHanging(bridge, zone, line, (x0 + x1) / 2f) : new Vector2(float.NaN, 0f);
                // Growth, not a curtain: clumps with bare stone between them, long strands
                // and short tufts mixed, the pale wisteria only now and then; none over the
                // deck's banner or lantern.
                var ivy = new[] { "ivy_drape_a", "ivy_drape_b", "ivy_drape_c", "vine_hanging", "ivy_drape_a" };
                for (var x = x0 + 1.2f; x < x1 - 0.8f; x += 2.6f + Hash(x, 5f) * 4.5f)
                {
                    if (Hash(x, 18f) < 0.3f || Mathf.Abs(x - ornament.x) < DeckOrnamentClearance)
                    {
                        continue;
                    }
                    var y = DeckY(line, x) - ParadiseGreybox.DeckThickness + 0.15f;
                    var name =
                        Sprites.ContainsKey("wisteria_drape") && Hash(x, 19f) > 0.84f
                            ? "wisteria_drape"
                            : ivy[(int)(Hash(x, 6f) * ivy.Length) % ivy.Length];
                    var length = Hash(x, 7f);
                    var scale = 0.5f + length * length * 0.85f;
                    plan.hanging.Add(
                        new Spot
                        {
                            name = name,
                            at = new Vector2(x, y + 0.1f * Hash(x, 20f)),
                            scale = name == "wisteria_drape" ? Mathf.Min(scale, 0.8f) : scale,
                            flip = Hash(x, 8f) > 0.5f,
                        }
                    );
                }
                if (x1 - x0 > 9f)
                {
                    plan.hanging.Add(
                        new Spot
                        {
                            name = Hash((x0 + x1) / 2f, 9f) > 0.5f ? "banner" : "lantern",
                            at = ornament,
                            scale = 1f,
                        }
                    );
                }
            }
            foreach (var shape in zone.shapes)
            {
                if (!zone.masonry.Contains(shape.name))
                {
                    continue;
                }
                // The top run: the outline from its first point while x grows (as the lips are built).
                var end = 0;
                while (end + 1 < shape.outline.Count && shape.outline[end + 1].x >= shape.outline[end].x - 0.001f)
                {
                    end++;
                }
                var x0 = shape.outline[0].x;
                var x1 = shape.outline[end].x;
                if (Sprites.ContainsKey("hedge_flowers"))
                {
                    for (var x = x0 + 4f; x < x1 - 4f; x += 9f + Hash(x, 12f) * 6f)
                    {
                        var y = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                        if (!float.IsNaN(y) && !NearGap(zone, x))
                        {
                            // Knee to waist high on the hero (1.1 to 1.5 tiles), so a hedge edges the walk instead of walling it.
                            plan.walls.Add(
                                new Spot
                                {
                                    name = "hedge_flowers",
                                    at = new Vector2(x, y - 0.05f),
                                    scale = 0.55f + Hash(x, 13f) * 0.2f,
                                    flip = Hash(x, 14f) > 0.5f,
                                }
                            );
                        }
                    }
                }
                // Ivy spilling over the coping down the face here and there, so the wall is an old
                // garden wall that things grow on, not a fresh sheet of blocks.
                for (var x = x0 + 5f; x < x1 - 5f; x += 6f + Hash(x, 23f) * 9f)
                {
                    var top = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                    if (float.IsNaN(top) || NearGap(zone, x) || Hash(x, 24f) < 0.35f)
                    {
                        continue;
                    }
                    var drape = Hash(x, 25f);
                    plan.walls.Add(
                        new Spot
                        {
                            name =
                                drape < 0.4f ? "ivy_drape_a"
                                : drape < 0.75f ? "ivy_drape_b"
                                : "ivy_drape_c",
                            at = new Vector2(x, top - 0.35f),
                            scale = 0.6f + Hash(x, 26f) * 0.5f,
                            flip = Hash(x, 27f) > 0.5f,
                        }
                    );
                }
                // Over the wall's edges (where the coping ends at a drop): a curtain of ivy or wisteria.
                foreach (var edge in new[] { x0 + 1.2f, x1 - 1.2f })
                {
                    var top = ParadiseDressing.SurfaceY(zone, edge, float.NaN);
                    if (float.IsNaN(top))
                    {
                        continue;
                    }
                    var curtain =
                        Sprites.ContainsKey("wisteria_drape") && Hash(edge, 15f) > 0.5f
                            ? "wisteria_drape"
                            : "ivy_curtain";
                    plan.walls.Add(
                        new Spot
                        {
                            name = curtain,
                            at = new Vector2(edge, top - 0.5f),
                            scale = 0.8f,
                            flip = edge > x0 + 2f,
                        }
                    );
                }
            }
            // Sunbeams all along the walk, irregularly spaced; closer under the canopy, where the
            // sun reaches the orchard only in shafts through its gaps.
            var hasCanopy = CanopyOf(zone.number, out var canopyFrom, out var canopyTo);
            for (var x = zone.xMin + 14f; x < zone.xMax - 6f; )
            {
                plan.shafts.Add(x);
                var under = hasCanopy && x > canopyFrom - CanopyOpens && x < canopyTo + CanopyOpens;
                x += under
                    ? CanopyBeamSpacing + Hash(x, 61f) * CanopyBeamSpacingVaries
                    : BeamSpacing + Hash(x, 61f) * BeamSpacingVaries;
            }
            // Arched pergolas behind the walk, on the longest runs of wall and railing, hung with growth.
            foreach (var x in PergolasOf(zone.number))
            {
                plan.pergolas.Add(x);
                var crown =
                    Mathf.Max(
                        ParadiseDressing.SurfaceY(zone, x, float.NaN),
                        ParadiseDressing.SurfaceY(zone, x + PergolaSpan, float.NaN)
                    )
                    + PergolaRise
                    + PergolaCrown;
                var growth = new[] { "ivy_drape_b", "wisteria_drape", "ivy_drape_c" };
                for (var i = 0; i < growth.Length; i++)
                {
                    var along = x + PergolaSpan * (i + 1) / (growth.Length + 1f);
                    plan.hanging.Add(
                        new Spot
                        {
                            name = growth[i],
                            at = new Vector2(along + (Hash(along, 41f) - 0.5f) * 0.5f, crown),
                            scale = 0.55f + Hash(along, 42f) * 0.2f,
                            flip = Hash(along, 43f) > 0.5f,
                        }
                    );
                }
            }
            if (zone.number == 1)
            {
                // A ruined garden: a fallen arch and broken columns where the old loggia stood.
                plan.ruins.Add(
                    new Spot
                    {
                        name = "broken_column",
                        at = new Vector2(33f, ParadiseDressing.SurfaceY(zone, 33f) - 0.1f),
                        scale = 1f,
                    }
                );
                plan.ruins.Add(
                    new Spot
                    {
                        name = "fallen_arch_fragment",
                        at = new Vector2(58f, ParadiseDressing.SurfaceY(zone, 58f) - 0.15f),
                        scale = 1f,
                        flip = true,
                    }
                );
                plan.ruins.Add(
                    new Spot
                    {
                        name = "broken_column",
                        at = new Vector2(320f, ParadiseDressing.SurfaceY(zone, 320f) - 0.1f),
                        scale = 1.1f,
                    }
                );
                plan.ruins.Add(
                    new Spot
                    {
                        name = "broken_column",
                        at = new Vector2(390f, ParadiseDressing.SurfaceY(zone, 390f) - 0.1f),
                        scale = 0.9f,
                        flip = true,
                    }
                );
                // Water: a spring in the Hollow's wall spills over its edge into the gap and the clouds;
                // the broken aqueduct spans spill where they broke, onto the lower garden.
                plan.water.Add(
                    new Waterfall
                    {
                        x = 71.5f,
                        top = -60.4f,
                        bottom = ParadiseGreybox.CloudSea + 12f,
                        width = 2f,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 126f,
                        top = -51.6f,
                        bottom = -66f,
                        width = 1.8f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 232.5f,
                        top = -44.6f,
                        bottom = -66.5f,
                        width = 1.4f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 262.5f,
                        top = -49f,
                        bottom = -66f,
                        width = 1.4f,
                        foam = true,
                    }
                );
            }
            else if (zone.number == 2)
            {
                // Every tree of the orchard rows is numbered, its number cut in the coping under it.
                var number = FirstTreeNumber;
                for (var x = OrchardRowsFrom; x <= OrchardRowsTo; x += OrchardRowSpacing)
                {
                    var walk = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                    if (!float.IsNaN(walk))
                    {
                        plan.numbers.Add(new Spot { name = Roman(number), at = new Vector2(x, walk - NumberDrop) });
                    }
                    number++;
                }
                if (Sprites.ContainsKey("blossom_tree"))
                {
                    // The orchard: blossom trees rooted on the terraces, in rows a little too even.
                    var rows = new List<float> { 420f, 470f, 515f, 1010f, 1150f, 1240f };
                    for (var x = OrchardRowsFrom; x <= OrchardRowsTo; x += OrchardRowSpacing)
                    {
                        rows.Add(x);
                    }
                    foreach (var x in rows)
                    {
                        var y = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                        if (!float.IsNaN(y))
                        {
                            plan.trees.Add(
                                new Spot
                                {
                                    name = "blossom_tree",
                                    at = new Vector2(x, y + 0.3f),
                                    scale = x == 515f ? 1.2f : 0.95f + Hash(x, 16f) * 0.15f,
                                    flip = Hash(x, 17f) > 0.5f,
                                }
                            );
                        }
                    }
                }
                LanternTree(zone, plan);
                plan.ruins.Add(
                    new Spot
                    {
                        name = "broken_column",
                        at = new Vector2(452f, ParadiseDressing.SurfaceY(zone, 452f) - 0.1f),
                        scale = 1f,
                    }
                );
                plan.ruins.Add(
                    new Spot
                    {
                        name = "fallen_arch_fragment",
                        at = new Vector2(745f, ParadiseDressing.SurfaceY(zone, 745f) - 0.15f),
                        scale = 1f,
                    }
                );
                plan.ruins.Add(
                    new Spot
                    {
                        name = "broken_column",
                        at = new Vector2(1160f, ParadiseDressing.SurfaceY(zone, 1160f) - 0.1f),
                        scale = 1.1f,
                        flip = true,
                    }
                );
                // The aqueduct over the orchard spills through its gaps; the run's arch bridges are
                // aqueducts too and spill into the roots route; a stream drops into the dell.
                // Each fall is 1.2 past its deck's end, so the spout's back sits on the deck.
                plan.water.Add(
                    new Waterfall
                    {
                        x = 627.2f,
                        top = -46.5f,
                        bottom = -62f,
                        width = 1.6f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 656.2f,
                        top = -45.8f,
                        bottom = -61.8f,
                        width = 1.6f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 685.2f,
                        top = -46.4f,
                        bottom = -62f,
                        width = 1.6f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 857.4f,
                        top = -58.2f,
                        bottom = -70f,
                        width = 1.8f,
                        foam = true,
                        // Out of the dell's east wall (the Lantern Rise), not the open air west of it.
                        fromRight = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 995.2f,
                        top = -62.4f,
                        bottom = -80.2f,
                        width = 1.5f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 1044.2f,
                        top = -61.6f,
                        bottom = -80.2f,
                        width = 1.5f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 1097.2f,
                        top = -61.4f,
                        bottom = -80.3f,
                        width = 1.5f,
                        foam = true,
                    }
                );
            }
            // Foreground: the garden between the eye and the walk, out of focus (fg_framing.py),
            // rising into the bottom of the frame and hanging into its top in turn, with open
            // view between them. Nothing over a gap: there is no ground there to frame.
            var side = 0;
            for (var x = zone.xMin + 20f; x < zone.xMax; x += 30f + Hash(x, 10f) * 16f)
            {
                var ground = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                if (float.IsNaN(ground))
                {
                    continue;
                }
                var rises = side++ % 2 == 0;
                // Under the canopy its own leaves hang over the walk instead.
                if (!rises && hasCanopy && x > canopyFrom - CanopyOpens && x < canopyTo + CanopyOpens)
                {
                    continue;
                }
                plan.foreground.Add(
                    new Spot
                    {
                        name = $"fg_soft_{(rises ? "rise" : "hang")}_{Mathf.FloorToInt(Hash(x, 21f) * 2.999f)}",
                        at = new Vector3(
                            x,
                            FrontAnchor(rises ? ground - FrontRiseFoot : ground + FrontHangTop, ground)
                        ),
                        scale = 1f,
                        flip = Hash(x, 22f) > 0.5f,
                    }
                );
            }
            WallHangings(zone, plan);
            return plan;
        }

        /// <summary>How far apart the sunbeams stand under the canopy, at least, and how much more at most.</summary>
        public const float CanopyBeamSpacing = 15f;

        public const float CanopyBeamSpacingVaries = 10f;

        /// <summary>How little the high clouds follow the walk: they are almost as far off as the sun.</summary>
        public const float HighCloudScroll = 0.03f;

        /// <summary>Their own slow drift on the wind, in level pixels a second.</summary>
        public const float HighCloudDrift = 0.6f;

        /// <summary>
        /// How far from flat an underside may tilt and still carry the hanging rock fringe, in
        /// degrees. A rounded corner turns through every angle from an underside to a wall; with a
        /// wider reach its arc took the fringe too, drawn diagonally as long stretched stripes.
        /// </summary>
        public const float UndersideReach = 30f;

        /// <summary>The edge a built terrace's underside takes: wall_soffit.png, flipped for the SpriteShape.</summary>
        public const string SoffitSprite = "wall_soffit_flip";

        /// <summary>How far the camera may be from the walk a framing piece was placed for before it fades.</summary>
        public const float FrontFadeNear = 2.5f;

        /// <summary>How far, at which it is gone: on a route far below, its cut edge would show.</summary>
        public const float FrontFadeFar = 6f;

        /// <summary>How far apart the sunbeams stand, at least, and how much more at most.</summary>
        public const float BeamSpacing = 26f;

        public const float BeamSpacingVaries = 18f;

        /// <summary>
        /// The beams' lean from upright, in degrees: down the way the sunlight runs, from the
        /// upper right (SunAndClouds.SunDirection), the same way as the rays painted in the sky.
        /// </summary>
        public static readonly float BeamLean =
            -Mathf.Atan2(SunAndClouds.SunDirection.x, SunAndClouds.SunDirection.y) * Mathf.Rad2Deg;

        /// <summary>Where a beam hangs from: above the top of the view when the hero stands on the walk.</summary>
        private const float BeamTopOverWalk = 9f;

        /// <summary>
        /// A beam in front of the walk is lighter than one behind it: it crosses the hero and
        /// must never hide him, only say the air between him and the eye is lit.
        /// </summary>
        public const float FrontBeamIntensity = 0.16f;

        /// <summary>How high over the walk the dust in a beam hangs thickest, and how much of it there is.</summary>
        private const float BeamDustAbove = 2.5f;

        private const int BeamDust = 60;

        /// <summary>Wall ivy sits under the coping (-99) and over the wall's depth shade (-100) and body.</summary>
        public const int WallIvyOrder = -101;

        /// <summary>A beam behind the walk.</summary>
        public const float BeamIntensity = 0.42f;

        private static Material FrontBeamMaterial(ParadiseArt.ZoneArt art)
        {
            var material = ParadiseArt.SaveMaterial($"{art.shaft.name} Front", art.shaft.shader.name);
            material.CopyPropertiesFromMaterial(art.shaft);
            material.SetFloat("_Intensity", FrontBeamIntensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The sorting layer that can sample the finished Default layer, so pools can mirror it.</summary>
        public const string WaterLayer = "Water";

        /// <summary>The out-of-focus framing, which has to pass in front of the pools.</summary>
        public const string ForegroundLayer = "Foreground";

        /// <summary>How far a pool's surface reaches toward the viewer, seen at a grazing angle.</summary>
        public const float PoolDepth = 0.5f;

        /// <summary>A pool narrower than this, hemmed in on both sides, is left as foam.</summary>
        public const float PoolLeast = 2f;

        private const float PoolSearch = 1f;
        private const float PoolMaxStep = 0.35f;
        private const float PoolWallTest = 0.5f;

        /// <summary>Kept clear of a wall: the column at a wall's end stands proud of its collider.</summary>
        private const float PoolWallGap = 0.5f;

        /// <summary>How much faster than the walk the framing passes, both ways: it is that much nearer the eye.</summary>
        public const float FrontDepth = 0.4f;

        /// <summary>The foot of a rising piece under the walk, in units (RISE_FOOT in fg_framing.py).</summary>
        public const float FrontRiseFoot = 8f;

        /// <summary>The top of a hanging piece over the walk, in units (HANG_TOP in fg_framing.py).</summary>
        public const float FrontHangTop = 11f;

        /// <summary>How far over the walk the camera sits when the hero stands on it.</summary>
        public const float CameraOverWalk = 2.1f;

        /// <summary>
        /// Where to put a piece with vertical parallax so that it is at `target` when the hero
        /// stands on `ground`: ForegroundParallax shifts it by FrontDepth of the camera's
        /// distance from its anchor, so the anchor is not the place it should be seen.
        /// </summary>
        public static float FrontAnchor(float target, float ground) =>
            (target + (ground + CameraOverWalk) * FrontDepth) / (1f + FrontDepth);

        /// <summary>Whether x is within a tile of a gap's edge (plants there would overhang the drop).</summary>
        /// <summary>
        /// Banners, and now and then a lantern, hung under the coping of the garden walls at wide
        /// intervals, so a long wall has a rhythm of its own. Kept clear of the ivy over the coping,
        /// the falls and the pergolas, and of gaps.
        /// </summary>
        private static void WallHangings(ParadiseLayout.Zone zone, Plan plan)
        {
            var taken = new List<float>();
            foreach (var p in plan.walls)
            {
                if (p.name.StartsWith("ivy_drape"))
                {
                    taken.Add(p.at.x);
                }
            }
            foreach (var w in plan.water)
            {
                taken.Add(w.x);
            }
            bool Clear(float x)
            {
                foreach (var other in taken)
                {
                    if (Mathf.Abs(other - x) < WallHangingClearance)
                    {
                        return false;
                    }
                }
                foreach (var pergola in plan.pergolas)
                {
                    if (x > pergola - 1f && x < pergola + PergolaSpan + 1f)
                    {
                        return false;
                    }
                }
                return true;
            }
            foreach (var shape in zone.shapes)
            {
                if (!zone.masonry.Contains(shape.name))
                {
                    continue;
                }
                var end = 0;
                while (end + 1 < shape.outline.Count && shape.outline[end + 1].x >= shape.outline[end].x - 0.001f)
                {
                    end++;
                }
                var x1 = shape.outline[end].x;
                for (
                    var x = shape.outline[0].x + 9f;
                    x < x1 - 9f;
                    x += WallHangingSpacing + Hash(x, 71f) * WallHangingSpacingVaries
                )
                {
                    var top = ParadiseDressing.SurfaceY(zone, x, float.NaN);
                    if (float.IsNaN(top) || NearGap(zone, x) || !Clear(x))
                    {
                        continue;
                    }
                    var name = Hash(x, 72f) < 0.7f ? "banner" : "lantern";
                    var at = new Vector2(x, top - CopingDepth);
                    // A lantern hangs in a niche of its own where the wall has room for one.
                    if (name == "lantern" && NicheFits(zone, shape, plan, x, at.y - NicheGap))
                    {
                        plan.niches.Add(
                            new Spot
                            {
                                name = WallNiche,
                                at = new Vector2(x, at.y - NicheGap),
                                scale = 1f,
                            }
                        );
                        at.y -= NicheGap + NicheCrown;
                    }
                    plan.hanging.Add(
                        new Spot
                        {
                            name = name,
                            at = at,
                            scale = 1f,
                        }
                    );
                    taken.Add(x);
                }
            }
        }

        /// <summary>The blind arch a wall lantern hangs in (wall_niche.py), placed by its top.</summary>
        public const string WallNiche = "wall_niche";

        /// <summary>Half the niche's width and its height, in units (559 x 576 px at 128 a unit).</summary>
        public const float NicheHalfWidth = 2.18f;

        public const float NicheHeight = 4.5f;

        /// <summary>How far under the coping the niche's keystone starts, and its intrados under that (the lantern's hook).</summary>
        public const float NicheGap = 0.12f;

        public const float NicheCrown = 0.4f;

        /// <summary>How far the walk may rise or fall across a niche's width and still be level enough for one.</summary>
        public const float NicheLevel = 0.35f;

        /// <summary>Where a niche draws: over the wall (-103) and its piers (-102), under the depth shade (-100).</summary>
        public const int NicheOrder = -101;

        /// <summary>
        /// Whether a niche fits in the wall under the coping at x: the wall goes deep enough below
        /// it on both sides, and it keeps clear of the ivy spilling over the coping, the falls and
        /// the wall's piers, so it is never half covered or cut into a pier.
        /// </summary>
        private static bool NicheFits(
            ParadiseLayout.Zone zone,
            ParadiseLayout.Shape shape,
            Plan plan,
            float x,
            float top
        )
        {
            var foot = top - NicheHeight - 0.4f;
            if (
                !InsideSolid(zone, new Vector2(x - NicheHalfWidth, foot))
                || !InsideSolid(zone, new Vector2(x + NicheHalfWidth, foot))
            )
            {
                return false;
            }
            // Only under a level coping: under a slope one shoulder of the arch would run into it.
            var walk = ParadiseDressing.SurfaceY(zone, x, float.NaN);
            foreach (var side in new[] { x - NicheHalfWidth, x + NicheHalfWidth })
            {
                var there = ParadiseDressing.SurfaceY(zone, side, float.NaN);
                if (float.IsNaN(there) || Mathf.Abs(there - walk) > NicheLevel)
                {
                    return false;
                }
            }
            foreach (var p in plan.walls)
            {
                if (p.name.StartsWith("ivy_drape") && Mathf.Abs(p.at.x - x) < NicheHalfWidth + 1.2f)
                {
                    return false;
                }
            }
            foreach (var w in plan.water)
            {
                if (Mathf.Abs(w.x - x) < NicheHalfWidth + w.width)
                {
                    return false;
                }
            }
            foreach (var pier in PierCentres(shape))
            {
                if (Mathf.Abs(pier - x) < NicheHalfWidth + PierWidth * 0.5f + 0.2f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Where Piers spaces a wall's piers (before it skips the ones that do not fit).</summary>
        private static IEnumerable<float> PierCentres(ParadiseLayout.Shape shape)
        {
            var xMin = float.MaxValue;
            var xMax = float.MinValue;
            foreach (var p in shape.outline)
            {
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
            }
            var count = Mathf.RoundToInt((xMax - xMin) / PierSpacing);
            for (var i = 0; i < count; i++)
            {
                yield return xMin + (i + 0.5f) * (xMax - xMin) / count;
            }
        }

        /// <summary>
        /// Where a deck's banner or lantern hangs: under the crown of the arch nearest the deck's
        /// middle, inside its opening and below its keystone (hung from under the deck it covered
        /// the keystone's gold medallion); with no arch, from the deck between the two columns
        /// nearest the middle, never in front of one.
        /// </summary>
        public static Vector2 DeckHanging(
            ParadiseLayout.Bridge bridge,
            ParadiseLayout.Zone zone,
            List<Vector2> line,
            float mid
        )
        {
            var (_, feet) = ParadiseGreybox.Supports(bridge, zone);
            var arches = Arches(line, feet, ParadiseGreybox.DeckThickness, out _);
            if (arches.Count > 0)
            {
                var nearest = arches[0];
                foreach (var a in arches)
                {
                    if (Mathf.Abs(a.Centre - mid) < Mathf.Abs(nearest.Centre - mid))
                    {
                        nearest = a;
                    }
                }
                return new Vector2(nearest.Centre, nearest.Crown - DeckHangingTuck);
            }
            var x = mid;
            if (feet.Count > 1)
            {
                var best = float.MaxValue;
                for (var i = 0; i + 1 < feet.Count; i++)
                {
                    var centre = (feet[i].x + feet[i + 1].x) / 2f;
                    if (Mathf.Abs(centre - mid) < best)
                    {
                        best = Mathf.Abs(centre - mid);
                        x = centre;
                    }
                }
            }
            return new Vector2(x, DeckY(line, x) - ParadiseGreybox.DeckThickness - 0.2f);
        }

        /// <summary>How far under an arch's crown a hanging's hook is.</summary>
        private const float DeckHangingTuck = 0.05f;

        /// <summary>How close the ivy under a deck may hang to the deck's banner or lantern.</summary>
        public const float DeckOrnamentClearance = 1.8f;

        /// <summary>
        /// The orchard rows' trees (zone 2): from, to and between, and the number the first bears.
        /// The orchard is a little too tidy (docs/design/paradise.md): every tree is numbered.
        /// </summary>
        public const float OrchardRowsFrom = 586f;

        public const float OrchardRowsTo = 742f;

        public const float OrchardRowSpacing = 26f;

        public const int FirstTreeNumber = 39;

        /// <summary>How far under the walk the middle of the coping's plain marble band is, where a number is cut.</summary>
        public const float NumberDrop = 0.77f;

        /// <summary>Over the coping (-99), under the railings (-90) and the plants.</summary>
        public const int NumberOrder = -98;

        /// <summary>
        /// The gilded numerals (tree_numerals.py): one texture, the letters of NumeralLetters each
        /// centred in a cell NumeralCell px wide, each drawn NumeralWidths px wide with an empty
        /// margin of NumeralMargin either side, at 128 px a unit.
        /// </summary>
        public const string NumeralAtlas = "numerals";

        private const string NumeralLetters = "IVXL";

        private const int NumeralCell = 32;

        private static readonly int[] NumeralWidths = { 12, 26, 26, 22 };

        private const int NumeralMargin = 2;

        /// <summary>A number from 1 to 89 in Roman numerals (the orchard has far fewer trees).</summary>
        public static string Roman(int n)
        {
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            return tens[Mathf.Clamp(n, 1, 89) / 10] + ones[Mathf.Clamp(n, 1, 89) % 10];
        }

        /// <summary>
        /// The zone's tree numbers, cut in the coping and gilded: one mesh over the numerals'
        /// texture, a quad for each letter, the letters of a number set edge to edge (less their
        /// margins) and centred on its spot. One draw call for every number in the zone.
        /// </summary>
        private static void Numbers(Transform root, List<Spot> numbers, ParadiseArt.ZoneArt art)
        {
            var texture = Get(NumeralAtlas).texture;
            var height = texture.height / 128f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var spot in numbers)
            {
                var width = 0f;
                foreach (var letter in spot.name)
                {
                    width += NumeralWidths[NumeralLetters.IndexOf(letter)] / 128f;
                }
                var x = spot.at.x - width / 2f;
                foreach (var letter in spot.name)
                {
                    var i = NumeralLetters.IndexOf(letter);
                    var drawn = NumeralWidths[i] + 2 * NumeralMargin;
                    var left = i * NumeralCell + (NumeralCell - drawn) / 2;
                    var x0 = x - NumeralMargin / 128f;
                    var x1 = x0 + drawn / 128f;
                    var first = vertices.Count;
                    vertices.Add(new Vector3(x0, spot.at.y + height / 2f, 0f));
                    vertices.Add(new Vector3(x1, spot.at.y + height / 2f, 0f));
                    vertices.Add(new Vector3(x0, spot.at.y - height / 2f, 0f));
                    vertices.Add(new Vector3(x1, spot.at.y - height / 2f, 0f));
                    uvs.Add(new Vector2((float)left / texture.width, 1f));
                    uvs.Add(new Vector2((float)(left + drawn) / texture.width, 1f));
                    uvs.Add(new Vector2((float)left / texture.width, 0f));
                    uvs.Add(new Vector2((float)(left + drawn) / texture.width, 0f));
                    triangles.AddRange(new[] { first, first + 1, first + 2, first + 2, first + 1, first + 3 });
                    x += NumeralWidths[i] / 128f;
                }
            }
            var mesh = new Mesh { name = root.name + " tree numbers" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            var colors = new Color[vertices.Count];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
            mesh.colors = colors;
            FaceTheCamera(mesh);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("TreeNumbers");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ParadiseNormals.LitMesh(texture, art.lit);
            renderer.sortingOrder = NumberOrder;
        }

        /// <summary>The stand-in the layout keeps for the orchard's roof of leaves (a Decor shape the art replaces).</summary>
        public const string CanopyRoof = "CanopyRoof";

        /// <summary>How much of the sky the orchard's canopy closes: the sun comes through it only in flecks.</summary>
        public const float CanopyCover = 1f;

        /// <summary>Over how many units either end of the canopy the sky opens out again.</summary>
        public const float CanopyOpens = 14f;

        /// <summary>
        /// Where a zone's canopy of leaves runs (zone 2, the Orchard Under the Canopy,
        /// docs/design/paradise.md): the span of the layout's CanopyRoof, within the zone.
        /// </summary>
        public static bool CanopyOf(int number, out float from, out float to)
        {
            from = to = 0f;
            var zone = ParadiseLayout.All().Find(z => z.number == number);
            var roof = zone?.shapes.Find(s => s.name == CanopyRoof);
            if (roof == null)
            {
                return false;
            }
            var left = float.MaxValue;
            var right = float.MinValue;
            foreach (var p in roof.outline)
            {
                left = Mathf.Min(left, p.x);
                right = Mathf.Max(right, p.x);
            }
            from = Mathf.Max(zone.xMin, left) + CanopyOpens;
            to = Mathf.Min(zone.xMax, right) - CanopyOpens;
            return to > from;
        }

        /// <summary>The stand-in the layout keeps for the Lantern Tree's trunk (a Decor shape the art replaces).</summary>
        public const string LanternTrunk = "LanternTrunk";

        /// <summary>How much bigger than an orchard tree the Lantern Tree is: its crown holds the tower's landings.</summary>
        public const float LanternTreeScale = 3f;

        /// <summary>The Lantern Tree's tint: the cool shade under the orchard's canopy.</summary>
        public static readonly Color LanternTreeShade = new Color(0.72f, 0.82f, 0.82f);

        /// <summary>
        /// The orchard's landmark (docs/design/paradise.md, zone 2): the Lantern Tree, a huge tree
        /// whose fruit lights the screen, with the save shrine in its roots and the landings climbing
        /// through its crown. It stands where the layout put its trunk, behind the landings, so they
        /// read as built up through its branches; its golden fruit glows as every orchard tree's does.
        /// </summary>
        private static void LanternTree(ParadiseLayout.Zone zone, Plan plan)
        {
            var trunk = zone.shapes.Find(s => s.name == LanternTrunk);
            if (trunk == null)
            {
                return;
            }
            var x = 0f;
            foreach (var p in trunk.outline)
            {
                x += p.x;
            }
            x /= trunk.outline.Count;
            var ground = ParadiseDressing.SurfaceY(zone, x, float.NaN);
            if (float.IsNaN(ground))
            {
                return;
            }
            plan.trees.Add(
                new Spot
                {
                    name = "blossom_tree",
                    at = new Vector2(x, ground + 0.3f),
                    scale = LanternTreeScale,
                    flip = true,
                }
            );
        }

        /// <summary>
        /// A hanging lantern's warm light, thrown on the stone round it from its glass. A real
        /// light, not a glow drawn over the wall, so the pool takes the masonry's own colour and
        /// relief. Stronger under the orchard's canopy, where the garden is lit from within.
        /// </summary>
        private static void LanternLight(Transform lantern, int zone)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(lantern, false);
            go.transform.localPosition = new Vector3(0f, -LanternGlassDrop, 0f);
            var light = go.AddComponent<WorldLight>();
            var settings = new SerializedObject(light);
            settings.FindProperty("color").colorValue = LanternColor;
            settings.FindProperty("energy").floatValue = zone == 1 ? LanternEnergy * 0.7f : LanternEnergy;
            settings.FindProperty("radiusPixels").floatValue = LanternReach * Player.PlayerTuning.PixelsPerUnit;
            // On only while its pool can reach the view: a 2D light is drawn once for each batch of
            // sorting layers, so every lantern left on off screen cost a few draw calls.
            settings.FindProperty("activeDistance").floatValue = LanternActiveDistance;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // The glass burns amber: at full strength the additive glow went to white.
            var glass = lantern.Find("Glow");
            SpriteRenderer glow = null;
            if (glass != null && glass.TryGetComponent(out glow))
            {
                glow.color = LanternGlass;
            }
            go.AddComponent<global::Descender.World.Paradise.LanternFlame>().Configure(glow, LanternBreath, 0.45f);
        }

        /// <summary>How far a lantern's flame swells and ebbs about its strength.</summary>
        public const float LanternBreath = 0.08f;

        /// <summary>The colour the lantern's glass glows (its glow sprite's tint).</summary>
        public static readonly Color LanternGlass = new Color(1f, 0.66f, 0.34f, 0.9f);

        /// <summary>How far below a lantern's hook its glass is, in units (the sprite hangs from its top).</summary>
        public const float LanternGlassDrop = 0.62f;

        /// <summary>How far a lantern's light reaches, in units, and how strong it is.</summary>
        public const float LanternReach = 4.5f;

        public const float LanternEnergy = 1.1f;

        /// <summary>
        /// How far from the camera a lantern's light stays on, plus its reach (WorldLight): the
        /// view's half diagonal is about 11.5 units, so its pool switches on just before it shows.
        /// </summary>
        public const float LanternActiveDistance = 13f;

        public static readonly Color LanternColor = new Color(1f, 0.74f, 0.42f);

        /// <summary>How far apart the hangings on a wall are, at least, and how much more at most.</summary>
        public const float WallHangingSpacing = 22f;

        public const float WallHangingSpacingVaries = 12f;

        /// <summary>How close a hanging may come to ivy over the coping or a fall.</summary>
        public const float WallHangingClearance = 2.5f;

        /// <summary>How far below the walk the coping's lower edge is, where a hanging's rod goes.</summary>
        private const float CopingDepth = 0.95f;

        private static bool NearGap(ParadiseLayout.Zone zone, float x)
        {
            var a = ParadiseDressing.SurfaceY(zone, x - 1f, float.NaN);
            var b = ParadiseDressing.SurfaceY(zone, x + 1f, float.NaN);
            var c = ParadiseDressing.SurfaceY(zone, x, float.NaN);
            return float.IsNaN(a) || float.IsNaN(b) || Mathf.Abs(a - c) > 1.5f || Mathf.Abs(b - c) > 1.5f;
        }
    }
}
