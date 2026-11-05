using System.Collections.Generic;
using System.IO;
using Descender.World;
using Descender.World.Paradise;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            ParadiseArt.DeriveFlipped(
                $"{KitRoot}/Nature/island_bottom.png",
                $"{ParadiseArt.DerivedFolder}/island_bottom_flip.png",
                1f - 0.95f
            );
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
            art.ambient = z == 1 ? new Color(1f, 0.97f, 0.92f) : new Color(0.96f, 1f, 0.95f);
            art.shaftColor = new Color(1f, 0.95f, 0.8f);
            var fill = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitRoot}/Nature/rock_fill.png");
            art.ground = ParadiseArt.Profile(
                "Garden_Ground",
                fill,
                new[]
                {
                    new AngleRange
                    {
                        start = 120f,
                        end = 180f,
                        order = 0,
                        sprites = new List<Sprite> { Get("island_bottom_flip") },
                    },
                    new AngleRange
                    {
                        start = -180f,
                        end = -120f,
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
            // Built terraces: marble ashlar walls under a carved coping (the grass lip until it exists).
            var wall = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitRoot}/Architecture/wall_block.png");
            art.terrace = ParadiseArt.Profile(
                "Garden_Terrace",
                wall,
                new[]
                {
                    new AngleRange
                    {
                        start = 120f,
                        end = 180f,
                        order = 0,
                        sprites = new List<Sprite> { Get("island_bottom_flip") },
                    },
                    new AngleRange
                    {
                        start = -180f,
                        end = -120f,
                        order = 0,
                        sprites = new List<Sprite> { Get("island_bottom_flip") },
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
                art.foreground.SetColor("_HazeColor", new Color(0.06f, 0.11f, 0.11f));
                art.foreground.SetFloat("_HazeAmount", 0.5f);
                EditorUtility.SetDirty(art.foreground);
            }
            // A bright sky needs only a hint of rays.
            art.shaft.SetFloat("_Intensity", 0.15f);
            EditorUtility.SetDirty(art.shaft);
            art.water = ParadiseArt.SaveMaterial("Paradise Waterfall", "Descender/Waterfall");
            art.water.SetColor("_Color", new Color(0.92f, 0.97f, 1f, 0.92f));
            EditorUtility.SetDirty(art.water);
            TuneLook(art.look, z);
            return art;
        }

        /// <summary>
        /// Sunlit grading for the painted kit (tools/ai_art), which is already bright: no exposure
        /// lift in zone 1, enough contrast that the play layer stands off the hazed distance,
        /// saturation barely lifted, warm highlights, cool sky-blue shadows, a soft sheen on light.
        /// This is what the zones' profiles end up with; ParadiseLookSetup's numbers are overwritten.
        /// </summary>
        private static void TuneLook(VolumeProfile profile, int z)
        {
            if (profile == null)
            {
                return;
            }
            T Get<T>()
                where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c))
                {
                    c = profile.Add<T>(true);
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                c.active = true;
                return c;
            }
            var color = Get<ColorAdjustments>();
            color.postExposure.Override(z == 1 ? -0.05f : -0.08f);
            color.contrast.Override(z == 1 ? 14f : 12f);
            color.saturation.Override(z == 1 ? 4f : 8f);
            color.colorFilter.Override(z == 1 ? new Color(1f, 0.99f, 0.975f) : new Color(0.97f, 1f, 0.975f));
            var split = Get<SplitToning>();
            split.shadows.Override(new Color(0.42f, 0.5f, 0.62f));
            split.highlights.Override(new Color(0.66f, 0.58f, 0.42f));
            split.balance.Override(10f);
            var bloom = Get<Bloom>();
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));
            var vignette = Get<Vignette>();
            vignette.color.Override(new Color(0.1f, 0.12f, 0.2f));
            vignette.intensity.Override(z == 1 ? 0.12f : 0.15f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);
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
                Get("garden_bg0_sky"),
                art.unlit,
                -1000 + zone.number,
                centre,
                Vector2.zero,
                true
            );
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
            Drift(cloudBack, 1.5f);
            Drift(cloudFront, 3f);
            root.gameObject.AddComponent<ZoneLook>()
                .Configure(
                    zone.number,
                    zone.xMin,
                    zone.xMax,
                    art.ambient,
                    volume,
                    sky,
                    new[] { far, midFar, cloudBack, mid, cloudFront }
                );

            var plan = GardenPlan(zone);
            var props = ParadiseArt.Group(root, "Props");
            var shadows = ParadiseArt.Group(root, "ContactShadows");
            foreach (var p in plan.plants)
            {
                Vary(Place(props, p.name, p.at, p.scale, p.flip, art.plant, -85, art), p.at.x);
                ContactShadow(shadows, p.name, p.at, p.scale, art);
            }
            foreach (var p in plan.ruins)
            {
                Vary(Place(props, p.name, p.at, p.scale, p.flip, art.lit, -88, art), p.at.x);
                ContactShadow(shadows, p.name, p.at, p.scale, art);
            }
            foreach (var p in plan.trees)
            {
                // Behind the play layer, rooted below the terrace's lip.
                Place(props, p.name, p.at, p.scale, p.flip, art.mid ?? art.plant, -150, art);
            }
            foreach (var p in plan.walls)
            {
                var hangs = p.name == "ivy_curtain" || p.name == "wisteria_drape" || p.name.StartsWith("ivy_drape");
                Vary(Place(props, p.name, p.at, p.scale, p.flip, art.plant, hangs ? -98 : -86, art), p.at.x);
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
            var hanging = ParadiseArt.Group(root, "Hanging");
            foreach (var p in plan.hanging)
            {
                Place(
                    hanging,
                    p.name,
                    p.at,
                    p.scale,
                    p.flip,
                    p.name.StartsWith("ivy") || p.name.StartsWith("vine") ? art.plant : art.lit,
                    -87,
                    art
                );
            }
            var water = ParadiseArt.Group(root, "Water");
            foreach (var w in plan.water)
            {
                Fall(water, w, art);
            }
            var shafts = ParadiseArt.Group(root, "Shafts");
            foreach (var x in plan.shafts)
            {
                var r = ParadiseArt.Sprite(
                    shafts,
                    "Shaft",
                    art.lightShaft,
                    art.shaft,
                    -300,
                    new Vector3(x, -34f, 0f),
                    new Vector3(1.8f, 1.9f, 1f)
                );
                r.color = art.shaftColor;
            }
            var front = ParadiseArt.Group(root, "Foreground");
            for (var i = 0; i < plan.foreground.Count; i++)
            {
                var f = plan.foreground[i];
                var r = ParadiseArt.Sprite(
                    front,
                    f.name,
                    Get(f.name),
                    art.foreground,
                    400 + i % 3,
                    f.at,
                    Vector3.one * f.scale
                );
                r.flipX = f.flip;
                r.gameObject.AddComponent<ForegroundParallax>().Configure(0.3f);
            }
            var air = ParadiseArt.Group(root, "Air");
            var area = new Rect(zone.xMin, -105f, zone.xMax - zone.xMin, 90f);
            Air(
                air,
                "Pollen",
                art.pollen,
                art.glow,
                45,
                new Color(1f, 0.93f, 0.6f),
                9f,
                new Vector2(0.6f, -0.2f),
                50f,
                new Vector2(3f, 8f),
                new Vector2(0.5f, 1f),
                0f,
                6f,
                area
            );
            Air(
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
            Air(
                air,
                "Motes",
                art.mote,
                art.glow,
                30,
                new Color(1f, 0.95f, 0.8f),
                11f,
                new Vector2(0.1f, -1f),
                40f,
                new Vector2(2f, 5f),
                new Vector2(0.3f, 0.7f),
                0f,
                8f,
                area
            );
        }

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
                    continue;
                }
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
                Pilaster(parent, points[0].x, points[0].y, points[0].y - 14f, false, art);
                Pilaster(parent, points[end].x, points[end].y, points[end].y - 14f, false, art);
            }
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

        /// <summary>The region's far landmark: the golden tree on its island, drifting across Heaven 1.</summary>
        public static void DressRegion(GameObject root)
        {
            if (!Sprites.ContainsKey("golden_tree"))
            {
                return;
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
                -985,
                new Vector3(1850f, -58f, 0f),
                Vector3.one * 0.28f
            );
            tree.gameObject.AddComponent<ForegroundParallax>().Configure(-0.996f, -1f, new Vector2(0f, -2.5f));
            var g = ParadiseArt.Sprite(
                tree.transform,
                "Glow",
                Get("golden_tree_glow"),
                glow,
                -984,
                tree.transform.position,
                Vector3.one
            );
            g.transform.localScale = Vector3.one;
            g.color = new Color(1f, 1f, 1f, 0.3f);
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
            var shade = new Color(0.15f, 0.19f, 0.3f);
            var grime = new Color(0.27f, 0.22f, 0.13f);
            var moss = new Color(0.24f, 0.31f, 0.14f);
            var bleach = new Color(1f, 0.97f, 0.9f);
            var streaks = Streaks();
            for (var y = 0; y < h; y++)
            {
                var depth = (h - 1 - y) / (float)(h - 1) * DepthReach;
                var v = depth / DepthReach;
                // The view shows about four units under the walk: the shade has to start within it.
                var a =
                    Mathf.SmoothStep(0f, 0.56f, Mathf.InverseLerp(0.8f, 4.6f, depth))
                    + Mathf.SmoothStep(0f, 0.14f, Mathf.InverseLerp(5f, 22f, depth));
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
                lightSettings.FindProperty("energy").floatValue = 0.9f;
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

        private static void Fall(Transform parent, Waterfall w, ParadiseArt.ZoneArt art)
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
            r.gameObject.AddComponent<global::Descender.World.Paradise.Waterfall>().Configure(4f, 1.2f);
            if (Sprites.ContainsKey("waterfall_spout"))
            {
                // The carved channel mouth the water leaves from.
                // Its pivot is the wall edge; the water leaves its lip 92 px out and 6 px up (64 px a unit).
                var scale = Mathf.Clamp(w.width / 1.5f, 0.8f, 1.3f);
                Place(
                    parent,
                    "waterfall_spout",
                    new Vector2(w.x - 1.44f * scale, w.top - 0.09f * scale),
                    scale,
                    false,
                    art.lit,
                    -103,
                    art
                );
            }
            if (w.foam)
            {
                Place(
                    parent,
                    "waterfall_foam",
                    new Vector2(w.x, w.bottom - 0.2f),
                    Mathf.Clamp(w.width / 2.5f, 0.6f, 1.3f),
                    false,
                    art.unlit,
                    -86,
                    art
                );
                Mist(parent, new Vector2(w.x, w.bottom + 0.4f), 0.7f, art, -84);
            }
            Mist(parent, new Vector2(w.x, w.top - 0.3f), 0.4f, art, -104);
        }

        private static void Mist(Transform parent, Vector2 at, float scale, ParadiseArt.ZoneArt art, int order)
        {
            var r = ParadiseArt.Sprite(parent, "Mist", Get("mist_soft"), art.unlit, order, at, Vector3.one * scale);
            r.color = new Color(1f, 1f, 1f, 0.45f);
        }

        private static void Air(
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
            go.AddComponent<RegionParticles>()
                .Configure(sprite, amount, color, life, dir, spread, speed, size, spin, sway, material, area);
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
            var springs = new float[feet.Count];
            for (var i = 0; i < feet.Count; i++)
            {
                // Up into the deck by a little, measured at the capital's higher corner, so the
                // capital carries the deck with no sky between them on a slope either.
                var x = feet[i].x;
                var under = Mathf.Max(DeckY(line, x - CapitalHalfWidth), DeckY(line, x + CapitalHalfWidth));
                springs[i] = under - deckDepth + DeckSeat;
            }
            // Level between neighbours: an arch carries the deck, so the column stops at its springing.
            var arched = new bool[feet.Count];
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
                var wide = span > 8f;
                var sprite = Get(wide ? "arch_span_wide" : "arch_span");
                var nominal = wide ? 10f : 6f;
                var arch = ParadiseArt.Sprite(
                    parent,
                    $"Arch{i}",
                    sprite,
                    art.lit,
                    -118,
                    new Vector3(a.x, spring, 0f),
                    new Vector3(span / nominal, 1f, 1f)
                );
                arch.sortingOrder = -118 + (i % 2);
                arched[i] = arched[i + 1] = true;
                // A tall aqueduct gets a lower tier, as Roman ones do: from the walk below, its
                // deck is off the screen, and the columns alone would hang from the sky.
                var groundLevel = Mathf.Abs(a.ground - b.ground) < 0.3f;
                var intoClouds = Mathf.Min(a.ground, b.ground) <= ParadiseGreybox.CloudSea + 0.01f;
                if (groundLevel && !intoClouds && spring - a.ground > 8f)
                {
                    var tier = ParadiseArt.Sprite(
                        parent,
                        $"Tier{i}",
                        sprite,
                        art.lit,
                        -118 + (i % 2),
                        new Vector3(a.x, a.ground + 3.2f, 0f),
                        new Vector3(span / nominal, 1f, 1f)
                    );
                    tier.color = new Color(0.94f, 0.94f, 0.96f);
                }
                springs[i] = Mathf.Min(springs[i], spring);
                springs[i + 1] = Mathf.Min(springs[i + 1], spring);
            }
            for (var i = 0; i < feet.Count; i++)
            {
                Column(parent, $"Column{i}", feet[i].x, springs[i], feet[i].ground, art);
            }
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
        }

        public sealed class Plan
        {
            public readonly List<Spot> plants = new List<Spot>();
            public readonly List<Spot> ruins = new List<Spot>();
            public readonly List<Spot> hanging = new List<Spot>();
            public readonly List<Spot> foreground = new List<Spot>();
            public readonly List<Waterfall> water = new List<Waterfall>();
            public readonly List<float> shafts = new List<float>();
            public readonly List<Spot> walls = new List<Spot>();
            public readonly List<Spot> trees = new List<Spot>();
            public readonly List<Spot> framing = new List<Spot>();
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
                // Growth, not a curtain: clumps with bare stone between them, long strands
                // and short tufts mixed, the pale wisteria only now and then.
                var ivy = new[] { "ivy_drape_a", "ivy_drape_b", "ivy_drape_c", "vine_hanging", "ivy_drape_a" };
                for (var x = x0 + 1.2f; x < x1 - 0.8f; x += 2.6f + Hash(x, 5f) * 4.5f)
                {
                    if (Hash(x, 18f) < 0.3f)
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
                    var mid = (x0 + x1) / 2f;
                    var under = DeckY(line, mid) - ParadiseGreybox.DeckThickness - 0.2f;
                    plan.hanging.Add(
                        new Spot
                        {
                            name = Hash(mid, 9f) > 0.5f ? "banner" : "lantern",
                            at = new Vector2(mid, under),
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
                plan.shafts.Add(80f);
                plan.shafts.Add(300f);
            }
            else if (zone.number == 2)
            {
                if (Sprites.ContainsKey("blossom_tree"))
                {
                    // The orchard: blossom trees rooted on the terraces, in rows a little too even.
                    var rows = new List<float> { 420f, 470f, 515f, 1010f, 1150f, 1240f };
                    for (var x = 586f; x <= 742f; x += 26f)
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
                plan.shafts.Add(520f);
                plan.shafts.Add(900f);
                plan.shafts.Add(1200f);
            }
            // Foreground: dark leaves rising into the bottom of the frame, as in the key art,
            // ivy hanging from above now and then; the kit's ferns and bushes if the leaves are missing.
            var leaves = new[] { "fg_leaves_a", "fg_leaves_b", "fg_leaves_d" };
            var k = 0;
            for (var x = zone.xMin + 18f; x < zone.xMax; x += 20f + Hash(x, 10f) * 16f, k++)
            {
                var ground = ParadiseDressing.SurfaceY(zone, x);
                if (Sprites.ContainsKey(leaves[0]) && k % 4 != 3)
                {
                    var name = leaves[(int)(Hash(x, 21f) * leaves.Length) % leaves.Length];
                    plan.foreground.Add(
                        new Spot
                        {
                            name = name,
                            at = new Vector3(x, ground - 5.1f),
                            scale = 0.5f + Hash(x, 11f) * 0.12f,
                            flip = Hash(x, 22f) > 0.5f,
                        }
                    );
                }
                else if (k % 3 == 2)
                {
                    plan.foreground.Add(
                        new Spot
                        {
                            name = "ivy_curtain",
                            at = new Vector3(x, ground + 9.5f),
                            scale = 1.6f,
                            flip = k % 2 == 0,
                        }
                    );
                }
                else
                {
                    plan.foreground.Add(
                        new Spot
                        {
                            name = k % 2 == 0 ? "fern" : "flower_bush",
                            at = new Vector3(x, ground - 5.2f),
                            scale = 3.4f + Hash(x, 11f),
                            flip = k % 2 == 1,
                        }
                    );
                }
            }
            return plan;
        }

        /// <summary>Whether x is within a tile of a gap's edge (plants there would overhang the drop).</summary>
        private static bool NearGap(ParadiseLayout.Zone zone, float x)
        {
            var a = ParadiseDressing.SurfaceY(zone, x - 1f, float.NaN);
            var b = ParadiseDressing.SurfaceY(zone, x + 1f, float.NaN);
            var c = ParadiseDressing.SurfaceY(zone, x, float.NaN);
            return float.IsNaN(a) || float.IsNaN(b) || Mathf.Abs(a - c) > 1.5f || Mathf.Abs(b - c) > 1.5f;
        }
    }
}
