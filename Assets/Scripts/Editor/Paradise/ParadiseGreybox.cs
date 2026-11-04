using System.Collections.Generic;
using System.IO;
using System.Linq;
using Descender.World;
using Descender.World.Paradise;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Build greybox: builds the new Paradise from
    /// ParadiseLayout (docs/design/paradise.md), apart from the prototype game: Main.unity,
    /// World.asset and the prototype's Region_paradise.unity are never touched. It writes
    ///
    /// - the greybox art (plain textures with a one-tile grid, so distances read at a glance);
    /// - one scene per zone, and the region's root scene Region_paradise_new.unity
    ///   (RegionRoot + RegionZones), under Assets/Scenes/Paradise;
    /// - its own world, Assets/Data/Paradise/ParadiseWorld.asset: the one 7,500 x 120
    ///   band, the start and the save shrines in save order;
    /// - the entry scene ParadiseMain.unity: a copy of Main.unity made once, whose
    ///   WorldStreamer plays ParadiseWorld (post-processing on, see PostProcessingSetup).
    ///   New Game there starts in the new Paradise; Main still plays the prototype.
    ///
    /// Ground and branches are SpriteShapes whose colliders are sampled from the same
    /// curves, so what you see is what you stand on. Running it again rebuilds the zone
    /// scenes, the root and the world from the layout.
    /// </summary>
    public static class ParadiseGreybox
    {
        public const string ArtFolder = "Assets/Art/Paradise/Greybox";
        public const string RegionKey = "paradise_new";
        public const string RegionScenePath = ParadiseLayout.SceneFolder + "/Region_paradise_new.unity";
        public const string EntryScenePath = ParadiseLayout.SceneFolder + "/ParadiseMain.unity";
        public const string AtlasPath = "Assets/Data/Paradise/ParadiseWorld.asset";
        public const string PrototypeAtlasPath = "Assets/Data/World.asset";
        public const string PrototypeMainPath = "Assets/Scenes/Main.unity";
        public const string LookPath = "Assets/Data/Regions/paradise.asset";
        public const string CatalogPath = "Assets/Data/Placements.asset";
        public const string LitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        public const int RegionRows = 120;
        public const int RegionWidth = 7500;
        public const int SamplesPerSegment = 10;

        /// <summary>Opens the new Paradise's entry scene (also used to launch the editor into it).</summary>
        [MenuItem("Descender/Paradise/Open ParadiseMain")]
        public static void OpenEntry()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(EntryScenePath, OpenSceneMode.Single);
            }
        }

        /// <summary>
        /// Builds the zones that have no scene yet (greybox, and the art pass when the art
        /// exists), then the root, the world and the entry scene. An existing zone scene is
        /// never touched: once built, the scene is the source and is edited in place, by hand
        /// or by targeted editor scripts. Use Rebuild zone to start one over from the layout.
        /// </summary>
        [MenuItem("Descender/Paradise/Build new zones")]
        public static void Run()
        {
            var built = 0;
            Directory.CreateDirectory(ParadiseLayout.SceneFolder);
            var zones = ParadiseLayout.All();
            Art art = null;
            foreach (var zone in zones)
            {
                if (File.Exists(ScenePath(zone)))
                {
                    continue;
                }
                art ??= PrepareArt();
                BuildZone(zone, art, new List<Vector2>());
                built++;
            }
            Finish(zones);
            Debug.Log(
                $"ParadiseGreybox: built {built} new zone(s); existing zones kept as they are. Play {EntryScenePath}."
            );
        }

        /// <summary>
        /// Starts one zone over from ParadiseLayout, overwriting its scene and every edit made
        /// to it. From the menu it asks first; in batch mode set PARADISE_REBUILD_ZONE to the
        /// zone's number and run Descender.EditorTools.Paradise.ParadiseGreybox.RebuildFromEnvironment.
        /// </summary>
        public static void Rebuild(int number)
        {
            var zones = ParadiseLayout.All();
            var zone =
                zones.Find(z => z.number == number)
                ?? throw new System.ArgumentException($"No zone {number} in ParadiseLayout.");
            BuildZone(zone, PrepareArt(), new List<Vector2>());
            Finish(zones);
            Debug.Log($"ParadiseGreybox: rebuilt zone {number} from the layout.");
        }

        [MenuItem("Descender/Paradise/Rebuild zone 1 from layout (overwrites)")]
        public static void RebuildZone1() => AskAndRebuild(1);

        [MenuItem("Descender/Paradise/Rebuild zone 2 from layout (overwrites)")]
        public static void RebuildZone2() => AskAndRebuild(2);

        public static void RebuildFromEnvironment()
        {
            var text = System.Environment.GetEnvironmentVariable("PARADISE_REBUILD_ZONE");
            if (!int.TryParse(text, out var number))
            {
                throw new System.ArgumentException("Set PARADISE_REBUILD_ZONE to a zone number.");
            }
            Rebuild(number);
        }

        private static void AskAndRebuild(int number)
        {
            if (
                Application.isBatchMode
                || EditorUtility.DisplayDialog(
                    "Rebuild zone from layout",
                    $"Zone {number}'s scene will be rebuilt from ParadiseLayout. Every change made to it since (by hand or by script) is lost.",
                    "Rebuild",
                    "Cancel"
                )
            )
            {
                Rebuild(number);
            }
        }

        /// <summary>
        /// Reads the save shrines and the player start from the zone scenes as they are now,
        /// numbers the shrines in the world's save order and writes ParadiseWorld.asset. Run
        /// after moving, adding or removing a shrine or the start by hand.
        /// </summary>
        [MenuItem("Descender/Paradise/Sync world from scenes")]
        public static void SyncWorld()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            var zones = ParadiseLayout.All();
            var saves = new List<(string scene, string id, Vector2 at)>();
            var start = ParadiseLayout.Start;
            foreach (var zone in zones)
            {
                if (!File.Exists(ScenePath(zone)))
                {
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(ScenePath(zone), OpenSceneMode.Single);
                foreach (var placement in Object.FindObjectsByType<WorldPlacement>(FindObjectsInactive.Include))
                {
                    var so = new SerializedObject(placement);
                    if (so.FindProperty("type").stringValue == "save")
                    {
                        saves.Add((ScenePath(zone), so.FindProperty("id").stringValue, placement.transform.position));
                    }
                }
                var marker = GameObject.Find(PlayerStartName);
                if (marker != null && marker.scene == scene)
                {
                    start = marker.transform.position;
                }
            }
            if (start == ParadiseLayout.Start)
            {
                EnsurePlayerStart(zones);
            }
            saves.Sort((a, b) => a.at.y != b.at.y ? b.at.y.CompareTo(a.at.y) : a.at.x.CompareTo(b.at.x));
            // Number them in the zone scenes, then write the world in the same order.
            foreach (var zone in zones)
            {
                if (!File.Exists(ScenePath(zone)))
                {
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(ScenePath(zone), OpenSceneMode.Single);
                var changed = false;
                foreach (var placement in Object.FindObjectsByType<WorldPlacement>(FindObjectsInactive.Include))
                {
                    var so = new SerializedObject(placement);
                    if (so.FindProperty("type").stringValue != "save")
                    {
                        continue;
                    }
                    var index = saves.FindIndex(s =>
                        s.scene == ScenePath(zone)
                        && s.id == so.FindProperty("id").stringValue
                        && (Vector2)placement.transform.position == s.at
                    );
                    if (so.FindProperty("checkpointIndex").intValue != index)
                    {
                        so.FindProperty("checkpointIndex").intValue = index;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        placement.gameObject.name = $"Save{index}";
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            WriteAtlas(saves.ConvertAll(s => s.at), start);
            AssetDatabase.SaveAssets();
            Debug.Log($"ParadiseGreybox: world synced from the scenes: {saves.Count} save shrines, start at {start}.");
        }

        /// <summary>Opens the region's root and every built zone together, to see the whole map.</summary>
        [MenuItem("Descender/Paradise/Open whole region")]
        public static void OpenWholeRegion()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(RegionScenePath, OpenSceneMode.Single);
            foreach (var zone in ParadiseLayout.All())
            {
                if (File.Exists(ScenePath(zone)))
                {
                    EditorSceneManager.OpenScene(ScenePath(zone), OpenSceneMode.Additive);
                }
            }
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                var built = ParadiseLayout.All().FindAll(z => File.Exists(ScenePath(z)));
                var right = built.Count > 0 ? built[built.Count - 1].xMax : RegionWidth;
                view.Frame(
                    new Bounds(new Vector3(right / 2f, -RegionRows / 2f, 0f), new Vector3(right, RegionRows, 1f)),
                    false
                );
            }
        }

        public const string PlayerStartName = "PlayerStart";

        /// <summary>Puts a PlayerStart at the layout's start in its zone, when no zone scene has one yet.</summary>
        private static void EnsurePlayerStart(List<ParadiseLayout.Zone> zones)
        {
            var zone = zones.Find(z => ParadiseLayout.Start.x >= z.xMin && ParadiseLayout.Start.x < z.xMax);
            if (zone == null || !File.Exists(ScenePath(zone)))
            {
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath(zone), OpenSceneMode.Single);
            if (GameObject.Find(PlayerStartName) != null)
            {
                return;
            }
            var start = new GameObject(PlayerStartName);
            var placements = GameObject.Find($"Zone{zone.number}/Placements");
            if (placements != null)
            {
                start.transform.SetParent(placements.transform, false);
            }
            start.transform.position = ParadiseLayout.Start;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static string ScenePath(ParadiseLayout.Zone zone) => $"{ParadiseLayout.SceneFolder}/{zone.scene}.unity";

        private static Art PrepareArt()
        {
            var art = EnsureArt();
            if (ParadiseArt.Available)
            {
                ParadiseArt.Prepare();
            }
            return art;
        }

        /// <summary>The region root, the world (from the scenes), the entry scene and the build list.</summary>
        private static void Finish(List<ParadiseLayout.Zone> zones)
        {
            EnsureRoot(zones);
            SyncWorld();
            // Saved before the entry scene opens: a scene switch can leave a fresh asset's
            // in-memory object stale, so the scene must reference it as loaded from disk.
            AssetDatabase.SaveAssets();
            EnsureEntryScene(AssetDatabase.LoadAssetAtPath<WorldAtlas>(AtlasPath));
            AddToBuild(zones);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ art

        private sealed class Art
        {
            public SpriteShape ground;
            public SpriteShape leaf;
            public SpriteShape decor;
            public Sprite podSprite;
            public Sprite seedSprite;
            public Sprite flagSprite;
            public Material lit;
        }

        private static Art EnsureArt()
        {
            Directory.CreateDirectory(ArtFolder);
            var art = new Art { lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath) };
            var groundFill = Texture("ground_fill", 64, 64, (x, y) => Grid(x, y, new Color(0.56f, 0.54f, 0.5f)), true);
            var leafFill = Texture("leaf_fill", 64, 64, (x, y) => Grid(x, y, new Color(0.46f, 0.6f, 0.43f)), true);
            var decorFill = Texture("decor_fill", 64, 64, (x, y) => new Color(0.24f, 0.29f, 0.31f), true);
            var groundEdge = SpriteTex(
                "ground_edge",
                64,
                16,
                (x, y) => y > 11 ? new Color(0.9f, 0.86f, 0.72f) : new Color(0.66f, 0.63f, 0.56f)
            );
            var leafEdge = SpriteTex(
                "leaf_edge",
                64,
                16,
                (x, y) => y > 11 ? new Color(0.78f, 0.95f, 0.6f) : new Color(0.5f, 0.66f, 0.45f)
            );
            var decorEdge = SpriteTex("decor_edge", 64, 8, (x, y) => new Color(0.2f, 0.24f, 0.26f));
            art.podSprite = SpriteTex("pod", 64, 32, (x, y) => Ellipse(x, y, 64, 32, new Color(0.95f, 0.55f, 0.75f)));
            art.seedSprite = SpriteTex("seed", 32, 32, (x, y) => Ellipse(x, y, 32, 32, new Color(1f, 0.85f, 0.45f)));
            art.flagSprite = SpriteTex("flag", 16, 16, (x, y) => Color.white);
            art.ground = Profile("Ground", groundFill, groundEdge);
            art.leaf = Profile("Leaf", leafFill, leafEdge);
            art.decor = Profile("Decor", decorFill, decorEdge);
            return art;
        }

        private static Color Grid(int x, int y, Color baseColor)
        {
            // A one-tile grid: 64 px a tile, so distances read at a glance.
            var line = x == 0 || y == 0;
            var c = line ? baseColor * 0.86f : baseColor;
            c.a = 1f;
            return c;
        }

        private static Color Ellipse(int x, int y, int w, int h, Color color)
        {
            var dx = (x + 0.5f) / w * 2f - 1f;
            var dy = (y + 0.5f) / h * 2f - 1f;
            var d = dx * dx + dy * dy;
            if (d > 1f)
            {
                return Color.clear;
            }
            var c = Color.Lerp(color, Color.white, Mathf.Clamp01((dy + 0.2f) * 0.5f) * 0.5f);
            c.a = 1f;
            return c;
        }

        private static string WritePng(string name, int w, int h, System.Func<int, int, Color> pixel)
        {
            var path = $"{ArtFolder}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, pixel(x, y));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static Texture2D Texture(string name, int w, int h, System.Func<int, int, Color> pixel, bool repeat)
        {
            var path = WritePng(name, w, h, pixel);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite SpriteTex(string name, int w, int h, System.Func<int, int, Color> pixel)
        {
            var path = WritePng(name, w, h, pixel);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static SpriteShape Profile(string name, Texture2D fill, Sprite edge)
        {
            var path = $"{ArtFolder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<SpriteShape>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SpriteShape>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.fillTexture = fill;
            profile.fillOffset = 0f;
            profile.angleRanges.Clear();
            profile.angleRanges.Add(
                new AngleRange
                {
                    start = -180f,
                    end = 180f,
                    order = 0,
                    sprites = new List<Sprite> { edge },
                }
            );
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---------------------------------------------------------------- zones

        private static void BuildZone(ParadiseLayout.Zone zone, Art art, List<Vector2> checkpoints)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject($"Zone{zone.number}").transform;
            var groundGroup = Group(root, "Ground");
            var ledgeGroup = Group(root, "Ledges");
            var decorGroup = Group(root, "Decor");
            var podGroup = Group(root, "Pods");
            var placementGroup = Group(root, "Placements");
            var markerGroup = Group(root, "Markers");
            var lightGroup = Group(root, "Lights");
            var roomGroup = Group(root, "Rooms");

            var zoneArt = ParadiseArt.Get(zone.number);
            foreach (var shape in zone.shapes)
            {
                if (zoneArt != null && shape.profile == ParadiseLayout.Profile.Decor)
                {
                    continue; // the painted backdrop and props replace the greybox stand-ins
                }
                var parent = shape.profile == ParadiseLayout.Profile.Decor ? decorGroup : groundGroup;
                BuildShape(shape, parent, art, zoneArt, zone.masonry.Contains(shape.name));
            }
            foreach (var ledge in zone.ledges)
            {
                BuildLedge(ledge, ledgeGroup, art, zoneArt);
            }
            CheckNothingFloats(zone);
            var supportGroup = Group(root, "Supports");
            foreach (var bridge in zone.bridges)
            {
                BuildLedge(
                    new ParadiseLayout.Ledge { name = bridge.name, path = bridge.deck },
                    ledgeGroup,
                    art,
                    zoneArt
                );
                BuildSupports(bridge, zone, supportGroup, art, zoneArt);
            }
            var podIndex = 0;
            foreach (var pod in zone.pods)
            {
                BuildPod(pod, podGroup, art, podIndex++, zoneArt);
            }
            var seedIndex = 0;
            foreach (var seed in zone.seeds)
            {
                BuildSeed(seed, podGroup, art, seedIndex++, zoneArt);
            }
            // The garden's saves are marble shrines; the greybox keeps the prototype's.
            var catalog =
                zoneArt != null && zoneArt.garden
                    ? ParadiseGarden.Catalog(zoneArt)
                    : AssetDatabase.LoadAssetAtPath<PlacementCatalog>(CatalogPath);
            var saveIndex = 0;
            foreach (var save in zone.saves)
            {
                BuildSave(save, placementGroup, catalog, saveIndex++, zone.number);
            }
            if (ParadiseLayout.Start.x >= zone.xMin && ParadiseLayout.Start.x < zone.xMax)
            {
                var start = new GameObject(PlayerStartName);
                start.transform.SetParent(placementGroup, false);
                start.transform.position = ParadiseLayout.Start;
            }
            foreach (var marker in zone.markers)
            {
                BuildMarker(marker, markerGroup, art, zoneArt != null);
            }
            var lampIndex = 0;
            foreach (var lamp in zone.lamps)
            {
                BuildLamp(lamp, lightGroup, lampIndex++);
            }
            var roomIndex = 0;
            foreach (var room in zone.rooms)
            {
                BuildRoom(room, roomGroup, zone.number * 100 + roomIndex++);
            }

            if (zoneArt != null)
            {
                ParadiseArt.Dress(zone, root, ParadiseDressing.For(zone));
            }

            var path = $"{ParadiseLayout.SceneFolder}/{zone.scene}.unity";
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Transform Group(Transform root, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        private static void BuildShape(
            ParadiseLayout.Shape shape,
            Transform parent,
            Art art,
            ParadiseArt.ZoneArt zoneArt,
            bool masonry = false
        )
        {
            var go = new GameObject(shape.name);
            go.transform.SetParent(parent, false);
            var points = shape.outline;
            var (left, right) = Tangents(points, true);
            var controller = go.AddComponent<SpriteShapeController>();
            var renderer = go.GetComponent<SpriteShapeRenderer>();
            renderer.sharedMaterials = new[] { art.lit, art.lit };
            controller.spriteShape =
                zoneArt != null ? (masonry && zoneArt.terrace != null ? zoneArt.terrace : zoneArt.ground)
                : shape.profile == ParadiseLayout.Profile.Ground ? art.ground
                : shape.profile == ParadiseLayout.Profile.Leaf ? art.leaf
                : art.decor;
            controller.fillPixelsPerUnit = 64f;
            controller.worldSpaceUVs = true;
            controller.splineDetail = 16;
            controller.autoUpdateCollider = false;
            var spline = controller.spline;
            spline.Clear();
            spline.isOpenEnded = false;
            for (var i = 0; i < points.Count; i++)
            {
                spline.InsertPointAt(i, new Vector3(points[i].x, points[i].y, 0f));
            }
            for (var i = 0; i < points.Count; i++)
            {
                SetTangents(spline, i, points[i].sharp, left[i], right[i]);
            }
            // Ground at -101: the garden's depth shade (-100) goes over its fill, under its lips (-99).
            renderer.sortingOrder =
                shape.profile == ParadiseLayout.Profile.Decor ? -200
                : shape.profile == ParadiseLayout.Profile.Leaf ? -95
                : -101;
            if (shape.collides)
            {
                go.layer = LayerMask.NameToLayer("World");
                var collider = go.AddComponent<PolygonCollider2D>();
                collider.points = Sample(points, left, right, true).ToArray();
            }
            controller.RefreshSpriteShape();
            BakeAndReport(controller, shape.name);
            if (zoneArt != null && shape.profile != ParadiseLayout.Profile.Decor)
            {
                BuildLips(
                    go.transform,
                    points,
                    left,
                    right,
                    zoneArt,
                    masonry && zoneArt.terraceLip != null ? zoneArt.terraceLip : zoneArt.lip ?? zoneArt.ledge
                );
                if (zoneArt.garden && shape.collides)
                {
                    ParadiseGarden.Depth(go.transform, Sample(points, left, right, true), zoneArt);
                }
            }
        }

        /// <summary>
        /// The grass lip on a ground shape's top, as open splines along the same curve as
        /// its collider. A closed SpriteShape does not reliably draw its top edge art, an
        /// open one always does; the top is split at vertical risers so no step face gets
        /// grass on its side. The top is the outline from its first point while x grows.
        /// </summary>
        private static void BuildLips(
            Transform parent,
            List<ParadiseLayout.P> points,
            Vector2[] left,
            Vector2[] right,
            ParadiseArt.ZoneArt zoneArt,
            SpriteShape lipProfile
        )
        {
            var end = 0;
            while (end + 1 < points.Count && points[end + 1].x >= points[end].x - 0.001f)
            {
                end++;
            }
            var runStart = 0;
            var lip = 0;
            for (var i = 0; i <= end; i++)
            {
                var riser = i < end && Mathf.Abs(points[i + 1].x - points[i].x) < 0.01f;
                if (!riser && i < end)
                {
                    continue;
                }
                if (i > runStart)
                {
                    BuildLip(parent, $"Lip{lip++}", points, left, right, runStart, i, zoneArt, lipProfile);
                }
                runStart = i + 1;
            }
        }

        private static void BuildLip(
            Transform parent,
            string name,
            List<ParadiseLayout.P> points,
            Vector2[] left,
            Vector2[] right,
            int from,
            int to,
            ParadiseArt.ZoneArt zoneArt,
            SpriteShape lipProfile
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var controller = go.AddComponent<SpriteShapeController>();
            var renderer = go.GetComponent<SpriteShapeRenderer>();
            renderer.sharedMaterials = new[] { zoneArt.lit, zoneArt.lit };
            renderer.sortingOrder = -99;
            controller.spriteShape = lipProfile;
            controller.autoUpdateCollider = false;
            controller.splineDetail = 16;
            var spline = controller.spline;
            spline.Clear();
            spline.isOpenEnded = true;
            for (var i = from; i <= to; i++)
            {
                spline.InsertPointAt(i - from, new Vector3(points[i].x, points[i].y, 0f));
            }
            for (var i = from; i <= to; i++)
            {
                // The run's ends keep the ground's own handles inside the run, so the lip
                // follows the collider's curve exactly; across a cut there is none.
                var l = i == from ? Vector2.zero : left[i];
                var r = i == to ? Vector2.zero : right[i];
                SetTangents(spline, i - from, points[i].sharp, l, r);
                spline.SetHeight(i - from, 1f);
            }
            controller.RefreshSpriteShape();
        }

        /// <summary>
        /// Bakes a shape now and names it when SpriteShape's fill tessellation fails: the
        /// fallback generator then draws the fill without its edge sprites (no grass lip).
        /// </summary>
        private static void BakeAndReport(SpriteShapeController controller, string name)
        {
            var failed = false;
            void Watch(string message, string stack, LogType type)
            {
                if (message.Contains("Fill tessellation"))
                {
                    failed = true;
                }
            }
            Application.logMessageReceived += Watch;
            try
            {
                controller.BakeMesh().Complete();
            }
            finally
            {
                Application.logMessageReceived -= Watch;
            }
            if (failed)
            {
                Debug.LogWarning($"ParadiseGreybox: fill tessellation failed for shape '{name}'.");
            }
        }

        private static void BuildLedge(
            ParadiseLayout.Ledge ledge,
            Transform parent,
            Art art,
            ParadiseArt.ZoneArt zoneArt
        )
        {
            var go = new GameObject(ledge.name) { layer = LayerMask.NameToLayer("Ledge") };
            go.transform.SetParent(parent, false);
            var points = ledge.path;
            var (left, right) = Tangents(points, false);
            var controller = go.AddComponent<SpriteShapeController>();
            var renderer = go.GetComponent<SpriteShapeRenderer>();
            renderer.sharedMaterials = new[] { art.lit, art.lit };
            renderer.sortingOrder = -90;
            controller.spriteShape = zoneArt != null ? zoneArt.ledge : art.leaf;
            controller.autoUpdateCollider = false;
            controller.splineDetail = 16;
            var spline = controller.spline;
            spline.Clear();
            spline.isOpenEnded = true;
            // The greybox branch hangs just under the walkable line; the painted lip's
            // pivot is its walkable line, so it sits right on it.
            var drop = zoneArt != null ? 0f : 0.2f;
            for (var i = 0; i < points.Count; i++)
            {
                spline.InsertPointAt(i, new Vector3(points[i].x, points[i].y - drop, 0f));
            }
            for (var i = 0; i < points.Count; i++)
            {
                SetTangents(spline, i, points[i].sharp && i != 0 && i != points.Count - 1, left[i], right[i]);
                spline.SetHeight(i, zoneArt != null ? 1f : 1.6f);
            }
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var edge = go.AddComponent<EdgeCollider2D>();
            edge.usedByEffector = true;
            edge.points = Sample(points, left, right, false).ToArray();
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            effector.useSideFriction = false;
            controller.RefreshSpriteShape();
        }

        /// <summary>The cloud sea's level: a column with nothing under it goes down into it.</summary>
        public const float CloudSea = -112f;

        /// <summary>Ground must reach the clouds; rock that floats must be declared an island.</summary>
        private static void CheckNothingFloats(ParadiseLayout.Zone zone)
        {
            foreach (var shape in zone.shapes)
            {
                if (!shape.collides || shape.island || shape.profile == ParadiseLayout.Profile.Decor)
                {
                    continue;
                }
                var lowest = shape.outline.Min(p => p.y);
                if (lowest > -95f)
                {
                    throw new System.InvalidOperationException(
                        $"Zone {zone.number}: '{shape.name}' floats (its lowest point is {lowest}). Ground reaches the clouds (y -95 or lower); "
                            + "make it an Island (rock with an underside) or a Deck (architecture on columns). docs/design/paradise.md, section 5."
                    );
                }
            }
        }

        /// <summary>
        /// A deck's columns: one near each end and every `spacing` units between, each from the
        /// deck's underside down to the first surface below it, or into the cloud sea.
        /// </summary>
        private static void BuildSupports(
            ParadiseLayout.Bridge bridge,
            ParadiseLayout.Zone zone,
            Transform parent,
            Art art,
            ParadiseArt.ZoneArt zoneArt
        )
        {
            var (left, right) = Tangents(bridge.deck, false);
            var line = Sample(bridge.deck, left, right, false);
            var x0 = bridge.deck[0].x + 0.6f;
            var x1 = bridge.deck[bridge.deck.Count - 1].x - 0.6f;
            var count = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / bridge.spacing) + 1);
            if (x1 - x0 < 1.5f)
            {
                count = 1;
                x0 = x1 = (x0 + x1) / 2f;
            }
            var group = new GameObject(bridge.name + " Columns").transform;
            group.SetParent(parent, false);
            var feet = new List<(float x, float ground)>();
            for (var i = 0; i < count; i++)
            {
                var x = count == 1 ? x0 : Mathf.Lerp(x0, x1, i / (float)(count - 1));
                var top = DeckY(line, x) - DeckThickness;
                feet.Add((x, ParadiseDressing.SurfaceY(zone, x, CloudSea, top - 0.5f)));
            }
            if (zoneArt != null && zoneArt.garden)
            {
                ParadiseGarden.Colonnade(group, line, feet, DeckThickness, zoneArt);
                return;
            }
            for (var i = 0; i < feet.Count; i++)
            {
                Column(group, $"Column{i}", feet[i].x, DeckY(line, feet[i].x) - DeckThickness, feet[i].ground, art);
            }
        }

        /// <summary>The deck's art and walkway are this deep; columns start under it.</summary>
        public const float DeckThickness = 0.9f;

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

        /// <summary>A column from `top` down to `ground` (behind the play layer, no collision).</summary>
        private static void Column(Transform parent, string name, float x, float top, float ground, Art art)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, (top + ground) / 2f, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = art.flagSprite;
            renderer.sharedMaterial = art.lit;
            renderer.color = new Color(0.86f, 0.83f, 0.76f);
            renderer.sortingOrder = -120;
            // The flag sprite is 16 px at 64 px a unit: a quarter unit square.
            go.transform.localScale = new Vector3(0.8f / 0.25f, Mathf.Max(0.1f, top - ground) / 0.25f, 1f);
        }

        private static void BuildPod(
            ParadiseLayout.Pod pod,
            Transform parent,
            Art art,
            int index,
            ParadiseArt.ZoneArt zoneArt
        )
        {
            var go = new GameObject($"Pod{index} ({pod.tiles:0} tiles)");
            go.transform.SetParent(parent, false);
            go.transform.position = pod.at;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            var width = pod.tiles >= 10f ? 3f : 2.2f;
            box.size = new Vector2(width, 0.6f);
            box.offset = new Vector2(0f, 0.5f);
            var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            visual.transform.localScale = new Vector3(width, pod.tiles >= 10f ? 2.4f : 1.8f, 1f);
            visual.sprite = art.podSprite;
            visual.sharedMaterial = art.lit;
            visual.sortingOrder = -80;
            if (zoneArt != null && zoneArt.Prop("z2_prop_bounce_pod") != null)
            {
                visual.sortingOrder = -84;
                ParadiseArt.Skin(
                    visual,
                    zoneArt,
                    "z2_prop_bounce_pod",
                    width / 3.2f,
                    new Vector2(-width * 0.12f, -0.1f)
                );
            }
            go.AddComponent<BouncePod>().Configure(pod.tiles);
            var serialized = new SerializedObject(go.GetComponent<BouncePod>());
            serialized.FindProperty("visual").objectReferenceValue = visual.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSeed(Vector2 at, Transform parent, Art art, int index, ParadiseArt.ZoneArt zoneArt)
        {
            var go = new GameObject($"SeedPod{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.5f;
            circle.isTrigger = true;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = art.seedSprite;
            renderer.sharedMaterial = art.lit;
            renderer.sortingOrder = -80;
            if (zoneArt != null && zoneArt.Prop("z1_prop_seed_pod") != null)
            {
                // The art on a child, so the pod's own transform keeps the collider centred.
                Object.DestroyImmediate(renderer);
                var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
                visual.transform.SetParent(go.transform, false);
                visual.sortingOrder = -80;
                ParadiseArt.Skin(visual, zoneArt, "z1_prop_seed_pod", 0.55f, new Vector2(0f, -0.6f));
            }
            go.AddComponent<SeedPod>();
        }

        private static void BuildSave(
            Vector2 at,
            Transform parent,
            PlacementCatalog catalog,
            int checkpointIndex,
            int zone
        )
        {
            // Numbered within the zone here; Sync world gives the world-wide save index.
            var go = new GameObject($"Save{checkpointIndex}");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var placement = go.AddComponent<WorldPlacement>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("id").stringValue = $"paradise/z{zone}/save{checkpointIndex}";
            serialized.FindProperty("type").stringValue = "save";
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("checkpointIndex").intValue = checkpointIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildMarker(ParadiseLayout.Marker marker, Transform parent, Art art, bool editorOnly)
        {
            var go = new GameObject(marker.label);
            go.transform.SetParent(parent, false);
            go.transform.position = marker.at;
            go.AddComponent<DesignMarker>().Configure(marker.kind, marker.label, marker.note);
            // A small flag and label in the game view, so a playtest shows the plan.
            var flag = new GameObject("Flag").AddComponent<SpriteRenderer>();
            flag.transform.SetParent(go.transform, false);
            flag.transform.localScale = new Vector3(2f, 2f, 1f);
            flag.sprite = art.flagSprite;
            flag.color = DesignMarker.ColorOf(marker.kind);
            flag.sortingOrder = 300;
            var text = new GameObject("Label").AddComponent<TextMesh>();
            text.transform.SetParent(go.transform, false);
            text.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            text.text = marker.label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.LowerCenter;
            text.color = DesignMarker.ColorOf(marker.kind);
            var textRenderer = text.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = text.font.material;
            textRenderer.sortingOrder = 301;
            if (editorOnly)
            {
                // Dressed zones keep the plan as gizmos only, out of the game view.
                flag.gameObject.SetActive(false);
                text.gameObject.SetActive(false);
            }
        }

        private static void BuildLamp(ParadiseLayout.Lamp lamp, Transform parent, int index)
        {
            var go = new GameObject($"Light{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = lamp.at;
            var light = go.AddComponent<WorldLight>();
            var serialized = new SerializedObject(light);
            serialized.FindProperty("color").colorValue = lamp.color;
            serialized.FindProperty("energy").floatValue = lamp.energy;
            serialized.FindProperty("radiusPixels").floatValue = lamp.radiusTiles * Player.PlayerTuning.PixelsPerUnit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRoom(Rect rect, Transform parent, int id)
        {
            var go = new GameObject($"Room{id}");
            go.transform.SetParent(parent, false);
            // A room's transform is its top-left corner.
            go.transform.position = new Vector3(rect.xMin, rect.yMax, 0f);
            var room = go.AddComponent<WorldRoom>();
            var serialized = new SerializedObject(room);
            serialized.FindProperty("id").intValue = id;
            serialized.FindProperty("roomName").stringValue = go.name;
            serialized.FindProperty("size").vector2Value = rect.size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // --------------------------------------------------------------- curves

        /// <summary>Catmull-Rom tangents as Bezier handles; sharp points get none.</summary>
        public static (Vector2[] left, Vector2[] right) Tangents(List<ParadiseLayout.P> points, bool closed)
        {
            var n = points.Count;
            var left = new Vector2[n];
            var right = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                if (points[i].sharp)
                {
                    continue;
                }
                var prev = closed ? points[(i - 1 + n) % n].V : points[Mathf.Max(0, i - 1)].V;
                var next = closed ? points[(i + 1) % n].V : points[Mathf.Min(n - 1, i + 1)].V;
                var t = (next - prev) / 6f;
                left[i] = -t;
                right[i] = t;
            }
            return (left, right);
        }

        private static void SetTangents(Spline spline, int i, bool sharp, Vector2 left, Vector2 right)
        {
            if (sharp)
            {
                spline.SetTangentMode(i, ShapeTangentMode.Linear);
                return;
            }
            spline.SetTangentMode(i, ShapeTangentMode.Continuous);
            spline.SetLeftTangent(i, left);
            spline.SetRightTangent(i, right);
        }

        /// <summary>The same cubic Beziers the spline draws, sampled for the collider.</summary>
        public static List<Vector2> Sample(List<ParadiseLayout.P> points, Vector2[] left, Vector2[] right, bool closed)
        {
            var result = new List<Vector2>();
            var n = points.Count;
            var segments = closed ? n : n - 1;
            for (var i = 0; i < segments; i++)
            {
                var a = points[i].V;
                var b = points[(i + 1) % n].V;
                var p1 = a + right[i];
                var p2 = b + left[(i + 1) % n];
                var steps = points[i].sharp && points[(i + 1) % n].sharp ? 1 : SamplesPerSegment;
                for (var s = 0; s < steps; s++)
                {
                    var t = s / (float)steps;
                    var u = 1f - t;
                    result.Add(u * u * u * a + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * b);
                }
            }
            if (!closed)
            {
                result.Add(points[n - 1].V);
            }
            return result;
        }

        // ----------------------------------------------------------- the region

        /// <summary>Region_paradise_new.unity: made once, then only its zone list is brought up to date.</summary>
        private static void EnsureRoot(List<ParadiseLayout.Zone> zones)
        {
            if (!File.Exists(RegionScenePath))
            {
                BuildRoot(zones);
                return;
            }
            var scene = EditorSceneManager.OpenScene(RegionScenePath, OpenSceneMode.Single);
            var regionZones =
                Object.FindAnyObjectByType<RegionZones>()
                ?? throw new System.InvalidOperationException(RegionScenePath + " has no RegionZones.");
            SetZones(regionZones, zones);
            if (ParadiseGarden.Available)
            {
                ParadiseGarden.DressRegion(regionZones.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The zones with a scene, in RegionZones' list.</summary>
        private static void SetZones(RegionZones regionZones, List<ParadiseLayout.Zone> zones)
        {
            var built = zones.FindAll(z => File.Exists(ScenePath(z)));
            var zonesSerialized = new SerializedObject(regionZones);
            var list = zonesSerialized.FindProperty("zones");
            list.arraySize = built.Count;
            for (var i = 0; i < built.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("scene").stringValue = built[i].scene;
                element.FindPropertyRelative("xMin").floatValue = built[i].xMin;
                element.FindPropertyRelative("xMax").floatValue = built[i].xMax;
            }
            zonesSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRoot(List<ParadiseLayout.Zone> zones)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Region_" + RegionKey);
            var root = go.AddComponent<RegionRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty("key").stringValue = RegionKey;
            serialized.FindProperty("displayName").stringValue = "Paradise";
            serialized.FindProperty("subtitle").stringValue = "Celestial Gardens";
            serialized.FindProperty("look").objectReferenceValue = EnsureLook();
            serialized.FindProperty("topRow").intValue = 0;
            serialized.FindProperty("rows").intValue = RegionRows;
            serialized.FindProperty("width").intValue = RegionWidth;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (ParadiseArt.Available)
            {
                ParadiseArt.DressRegion(go);
            }
            SetZones(go.AddComponent<RegionZones>(), zones);
            EditorSceneManager.SaveScene(scene, RegionScenePath);
        }

        /// <summary>
        /// The new Paradise's RegionLook: the prototype's, copied once, with its music named
        /// (the region's key, paradise_new, has no track; the zones' ZoneLook sets the light).
        /// </summary>
        private static RegionLook EnsureLook()
        {
            const string path = "Assets/Data/Paradise/ParadiseLook.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path))
            {
                AssetDatabase.CopyAsset(LookPath, path);
            }
            var look = AssetDatabase.LoadAssetAtPath<RegionLook>(path);
            look.music = "paradise";
            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssets();
            return look;
        }

        /// <summary>The new Paradise's own world: one band, its start and save shrines.</summary>
        private static WorldAtlas WriteAtlas(List<Vector2> checkpoints, Vector2 start)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            var atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = ScriptableObject.CreateInstance<WorldAtlas>();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }
            var prototype = AssetDatabase.LoadAssetAtPath<WorldAtlas>(PrototypeAtlasPath);
            atlas.regions = new[]
            {
                new WorldAtlas.Band
                {
                    key = RegionKey,
                    displayName = "Paradise",
                    subtitle = "Celestial Gardens",
                    topRow = 0,
                    rows = RegionRows,
                    width = RegionWidth,
                },
            };
            atlas.width = RegionWidth;
            atlas.startRegion = RegionKey;
            atlas.start = start;
            atlas.checkpoints = checkpoints
                .Select(c => new WorldAtlas.Checkpoint { region = RegionKey, position = c })
                .ToArray();
            // The abilities keep the prototype's order: it is the save format's.
            atlas.abilities = prototype != null ? (string[])prototype.abilities.Clone() : new string[0];
            EditorUtility.SetDirty(atlas);
            return atlas;
        }

        /// <summary>ParadiseMain.unity: Main.unity copied once, its WorldStreamer on ParadiseWorld.</summary>
        private static void EnsureEntryScene(WorldAtlas atlas)
        {
            if (!File.Exists(EntryScenePath) && !AssetDatabase.CopyAsset(PrototypeMainPath, EntryScenePath))
            {
                throw new System.InvalidOperationException($"Could not copy {PrototypeMainPath} to {EntryScenePath}.");
            }
            var scene = EditorSceneManager.OpenScene(EntryScenePath, OpenSceneMode.Single);
            atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>(AtlasPath);
            if (atlas == null)
            {
                throw new System.InvalidOperationException(AtlasPath + " did not save.");
            }
            var streamer = Object.FindAnyObjectByType<WorldStreamer>();
            if (streamer == null)
            {
                throw new System.InvalidOperationException(EntryScenePath + " has no WorldStreamer.");
            }
            var serialized = new SerializedObject(streamer);
            serialized.FindProperty("atlas").objectReferenceValue = atlas;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null && camera.GetComponent<Fx.HeroWindPush>() == null)
            {
                camera.gameObject.AddComponent<Fx.HeroWindPush>();
            }
            if (camera != null && camera.GetComponent<HeroAura>() == null)
            {
                camera.gameObject.AddComponent<HeroAura>();
            }
            if (camera != null && ParadiseHero.Available)
            {
                // Ansel at full resolution (a cutout rig) in place of the pixel-art sprite.
                var installer =
                    camera.GetComponent<HeroRigInstaller>() ?? camera.gameObject.AddComponent<HeroRigInstaller>();
                installer.Configure(ParadiseHero.Build());
                EditorUtility.SetDirty(installer);
            }
            // Painted art needs the full screen resolution: the prototype's pixel-perfect
            // camera renders the world at 320x180 and scales it up, which turns every curve
            // into stairs. Keep its framing (the crop to 16:9) but render at full size.
            if (
                camera != null
                && camera.TryGetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>(out var pixelPerfect)
            )
            {
                var ppc = new SerializedObject(pixelPerfect);
                ppc.FindProperty("m_GridSnapping").intValue = 0;
                ppc.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Setup.PostProcessingSetup.Run();
            if (!File.ReadAllText(EntryScenePath).Contains(AssetDatabase.AssetPathToGUID(AtlasPath)))
            {
                throw new System.InvalidOperationException(
                    EntryScenePath + " was saved without its ParadiseWorld atlas."
                );
            }
        }

        private static void AddToBuild(List<ParadiseLayout.Zone> zones)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var wanted = new List<string> { EntryScenePath, RegionScenePath };
            wanted.AddRange(zones.Select(z => $"{ParadiseLayout.SceneFolder}/{z.scene}.unity"));
            foreach (var path in wanted)
            {
                if (scenes.All(s => s.path != path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>The save shrines in the world's save order: top to bottom, then left to right.</summary>
        public static List<Vector2> CheckpointOrder(List<ParadiseLayout.Zone> zones)
        {
            var all = zones.SelectMany(z => z.saves).ToList();
            all.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
            return all;
        }
    }
}
