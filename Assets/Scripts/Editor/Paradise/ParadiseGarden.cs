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
            // A bright sky needs only a hint of rays.
            art.shaft.SetFloat("_Intensity", 0.22f);
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
            color.postExposure.Override(z == 1 ? 0.05f : 0f);
            color.contrast.Override(z == 1 ? 14f : 12f);
            color.saturation.Override(z == 1 ? 4f : 8f);
            color.colorFilter.Override(z == 1 ? new Color(1f, 0.985f, 0.96f) : new Color(0.97f, 1f, 0.97f));
            var split = Get<SplitToning>();
            split.shadows.Override(new Color(0.42f, 0.5f, 0.62f));
            split.highlights.Override(new Color(0.66f, 0.58f, 0.42f));
            split.balance.Override(10f);
            var bloom = Get<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.5f);
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
            foreach (var p in plan.plants)
            {
                Place(props, p.name, p.at, p.scale, p.flip, art.plant, -85, art);
            }
            foreach (var p in plan.ruins)
            {
                Place(props, p.name, p.at, p.scale, p.flip, art.lit, -88, art);
            }
            foreach (var p in plan.trees)
            {
                // Behind the play layer, rooted below the terrace's lip.
                Place(props, p.name, p.at, p.scale, p.flip, art.mid ?? art.plant, -150, art);
            }
            foreach (var p in plan.walls)
            {
                var hangs = p.name == "ivy_curtain" || p.name == "wisteria_drape";
                Place(props, p.name, p.at, p.scale, p.flip, art.plant, hangs ? -98 : -86, art);
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

        private static void Place(
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
                springs[i] = DeckY(line, feet[i].x) - deckDepth;
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
                springs[i] = Mathf.Min(springs[i], spring);
                springs[i + 1] = Mathf.Min(springs[i + 1], spring);
            }
            for (var i = 0; i < feet.Count; i++)
            {
                Column(parent, $"Column{i}", feet[i].x, springs[i], feet[i].ground, art);
            }
        }

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
                    new Vector3(x, shaftFoot, 0f),
                    Vector3.one
                );
                shaft.drawMode = SpriteDrawMode.Tiled;
                shaft.tileMode = SpriteTileMode.Continuous;
                shaft.size = new Vector2(1f, capitalFoot - shaftFoot + 0.05f);
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
                var n = 0;
                for (var x = x0 + 0.8f; x < x1 - 0.5f; x += 2.2f + Hash(x, 5f) * 2.5f, n++)
                {
                    var y = DeckY(line, x) - ParadiseGreybox.DeckThickness + 0.15f;
                    var ivy = Sprites.ContainsKey("wisteria_drape")
                        ? new[] { "ivy_drape_a", "ivy_drape_b", "wisteria_drape", "ivy_drape_c", "vine_hanging" }
                        : new[] { "ivy_drape_a", "ivy_drape_b", "ivy_drape_c", "vine_hanging" };
                    plan.hanging.Add(
                        new Spot
                        {
                            name = ivy[(int)(Hash(x, 6f) * ivy.Length) % ivy.Length],
                            at = new Vector2(x, y),
                            scale = 0.9f + Hash(x, 7f) * 0.4f,
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
                if (Sprites.ContainsKey("loggia_frame"))
                {
                    plan.framing.Add(
                        new Spot
                        {
                            name = "loggia_frame",
                            at = new Vector2(8f, ParadiseDressing.SurfaceY(zone, 8f) - 4f),
                            scale = 1f,
                        }
                    );
                    plan.framing.Add(
                        new Spot
                        {
                            name = "loggia_frame",
                            at = new Vector2(335f, ParadiseDressing.SurfaceY(zone, 335f) - 4f),
                            scale = 1f,
                            flip = true,
                        }
                    );
                }
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
                if (Sprites.ContainsKey("loggia_frame"))
                {
                    plan.framing.Add(
                        new Spot
                        {
                            name = "loggia_frame",
                            at = new Vector2(880f, ParadiseDressing.SurfaceY(zone, 880f) - 4f),
                            scale = 1f,
                        }
                    );
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
                plan.water.Add(
                    new Waterfall
                    {
                        x = 628f,
                        top = -46.5f,
                        bottom = -62f,
                        width = 1.6f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 657f,
                        top = -45.8f,
                        bottom = -61.8f,
                        width = 1.6f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 686f,
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
                        x = 996f,
                        top = -62.4f,
                        bottom = -80.2f,
                        width = 1.5f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 1045f,
                        top = -61.6f,
                        bottom = -80.2f,
                        width = 1.5f,
                        foam = true,
                    }
                );
                plan.water.Add(
                    new Waterfall
                    {
                        x = 1098f,
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
            // Foreground: big ferns and bushes rising from below the screen, ivy hanging from above.
            var k = 0;
            for (var x = zone.xMin + 25f; x < zone.xMax; x += 34f + Hash(x, 10f) * 14f, k++)
            {
                var ground = ParadiseDressing.SurfaceY(zone, x);
                if (k % 3 == 2)
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
