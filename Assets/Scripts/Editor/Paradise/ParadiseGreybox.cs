using System.Collections.Generic;
using System.IO;
using System.Linq;
using Descender.World;
using Descender.World.Paradise;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Build greybox: builds the new Paradise from
    /// ParadiseLayout (docs/design/paradise.md), apart from the prototype game: Main.unity,
    /// World.asset and the prototype's Region_paradise.unity are never touched. It writes
    ///
    /// - the greybox art (plain textures with a one-tile grid, so distances read at a glance;
    ///   red is kept for the thorn hazards alone);
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
        public const string HazardPrefabPath = "Assets/Prefabs/Pickups/Hazard.prefab";
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

        [MenuItem("Descender/Paradise/Rebuild zone 3 from layout (overwrites)")]
        public static void RebuildZone3() => AskAndRebuild(3);

        [MenuItem("Descender/Paradise/Rebuild zone 4 from layout (overwrites)")]
        public static void RebuildZone4() => AskAndRebuild(4);

        [MenuItem("Descender/Paradise/Rebuild zone 5 from layout (overwrites)")]
        public static void RebuildZone5() => AskAndRebuild(5);

        [MenuItem("Descender/Paradise/Rebuild zone 6 from layout (overwrites)")]
        public static void RebuildZone6() => AskAndRebuild(6);

        [MenuItem("Descender/Paradise/Rebuild zone 7 from layout (overwrites)")]
        public static void RebuildZone7() => AskAndRebuild(7);

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
        /// numbers the shrines in the world's save order (by zone, west to east, then by x,
        /// so building a new zone leaves the older zones' indices alone: D-0010) and writes
        /// ParadiseWorld.asset. Run after moving, adding or removing a shrine or the start
        /// by hand.
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
            var zoneOrder = new Dictionary<string, int>();
            for (var i = 0; i < zones.Count; i++)
            {
                zoneOrder[ScenePath(zones[i])] = i;
            }
            saves.Sort(
                (a, b) =>
                    zoneOrder[a.scene] != zoneOrder[b.scene]
                        ? zoneOrder[a.scene].CompareTo(zoneOrder[b.scene])
                        : a.at.x.CompareTo(b.at.x)
            );
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
            JoinRooms(zones);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Joins every built zone's rooms (see JoinRooms), without rebuilding a zone; for batch mode.</summary>
        [MenuItem("Descender/Paradise/Join the zones' rooms")]
        public static void JoinAllRooms()
        {
            JoinRooms(ParadiseLayout.All());
        }

        /// <summary>
        /// Paradise's zones are one continuous land: every zone's rooms are joined, so the
        /// camera keeps to them all as one and never stops at a zone's border. A zone built
        /// before rooms could be joined is set here, without rebuilding it.
        /// </summary>
        private static void JoinRooms(List<ParadiseLayout.Zone> zones)
        {
            foreach (var zone in zones)
            {
                var path = ScenePath(zone);
                if (!File.Exists(path))
                {
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var changed = false;
                foreach (
                    var room in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldRoom>(true))
                )
                {
                    var serialized = new SerializedObject(room);
                    var joined = serialized.FindProperty("joined");
                    if (!joined.boolValue)
                    {
                        joined.boolValue = true;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorSceneManager.SaveScene(scene);
                }
                EditorSceneManager.CloseScene(scene, true);
            }
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
            public Sprite hazardSprite;
            public Sprite barrierSprite;
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
            // The one red thing in the greybox: thorns, so a hazard reads at a glance.
            art.hazardSprite = SpriteTex("hazard", 16, 16, (x, y) => new Color(0.85f, 0.08f, 0.1f));
            // A cell of angel light: pale gold, brighter at its edges.
            art.barrierSprite = SpriteTex(
                "barrier",
                16,
                16,
                (x, y) =>
                    x < 2 || x > 13 || y < 2 || y > 13 ? new Color(1f, 0.98f, 0.8f) : new Color(1f, 0.92f, 0.6f, 0.75f)
            );
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
            CheckConstructionRules(zone);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject($"Zone{zone.number}").transform;
            var groundGroup = Group(root, "Ground");
            var ledgeGroup = Group(root, "Ledges");
            var decorGroup = Group(root, "Decor");
            var podGroup = Group(root, "Pods");
            var placementGroup = Group(root, "Placements");
            var hazardGroup = Group(root, "Hazards");
            var barrierGroup = Group(root, "Barriers");
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
                BuildShape(shape, parent, art, zoneArt, zone.masonry.Contains(shape.name), zone);
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
            foreach (var hazard in zone.hazards)
            {
                BuildHazard(hazard, hazardGroup, art);
            }
            foreach (var barrier in zone.barriers)
            {
                BuildBarrier(barrier, barrierGroup, art);
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
            // The story beats that are things, from the catalog the zone's saves use.
            foreach (var placement in zone.placements)
            {
                BuildPlacement(placement, placementGroup, catalog);
            }
            if (ParadiseLayout.Start.x >= zone.xMin && ParadiseLayout.Start.x < zone.xMax)
            {
                var start = new GameObject(PlayerStartName);
                start.transform.SetParent(placementGroup, false);
                start.transform.position = ParadiseLayout.Start;
            }
            // A beat is a flag or a thing, never both: a marker a placement replaces is not built.
            var replaced = zone.ReplacedMarkers();
            foreach (var marker in zone.markers)
            {
                if (replaced.Contains(marker.label))
                {
                    continue;
                }
                BuildMarker(marker, markerGroup, art, zoneArt != null);
            }
            var lampIndex = 0;
            foreach (var lamp in zone.lamps)
            {
                BuildLamp(lamp, lightGroup, lampIndex++, zoneArt != null && zoneArt.garden ? GardenLampScale : 1f);
            }
            var roomIndex = 0;
            foreach (var room in zone.rooms)
            {
                BuildRoom(room, roomGroup, zone.number * 100 + roomIndex++);
            }
            if (zone.moments.Count > 0)
            {
                var momentGroup = Group(root, MomentsGroup);
                foreach (var moment in zone.moments)
                {
                    BuildMoment(moment, momentGroup);
                }
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
            bool masonry = false,
            ParadiseLayout.Zone zone = null
        )
        {
            var go = new GameObject(shape.name);
            go.transform.SetParent(parent, false);
            // The garden's ground has no knife edges: worn marble and weathered rock round
            // every corner the player can see (the bottom of the band stays square, unseen).
            var points =
                zoneArt != null && zoneArt.garden && shape.profile != ParadiseLayout.Profile.Decor
                    ? RoundCorners(shape.outline, masonry ? MarbleCornerRadius : RockCornerRadius)
                    : shape.outline;
            // Natural rock does not fall in a ruled line; cut stone does. Only the art moves.
            if (zoneArt != null && zoneArt.garden && !masonry && shape.profile == ParadiseLayout.Profile.Ground)
            {
                points = RoughenSides(points, shape.outline[0].x);
            }
            var (left, right) = Tangents(points, true);
            var controller = go.AddComponent<SpriteShapeController>();
            var renderer = go.GetComponent<SpriteShapeRenderer>();
            renderer.sharedMaterials = new[] { art.lit, art.lit };
            controller.spriteShape =
                zoneArt != null ? (masonry && zoneArt.terrace != null ? zoneArt.terrace : zoneArt.ground)
                : shape.profile == ParadiseLayout.Profile.Ground ? art.ground
                : shape.profile == ParadiseLayout.Profile.Leaf ? art.leaf
                : art.decor;
            if (zoneArt != null && zoneArt.garden)
            {
                ParadiseNormals.Dress(controller, renderer, art.lit);
            }
            // The fill at its own density (the painted kit is 128 px a unit), so it keeps its size.
            controller.fillPixelsPerUnit =
                controller.spriteShape != null && controller.spriteShape.fillTexture != null
                    ? ParadiseArt.PixelsPerUnit(controller.spriteShape.fillTexture)
                    : 64f;
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
            // Ground at -103, what is built into a wall at -102: the garden's depth shade (-100)
            // goes over both, under the lips (-99).
            renderer.sortingOrder =
                shape.profile == ParadiseLayout.Profile.Decor ? -200
                : shape.profile == ParadiseLayout.Profile.Leaf ? -95
                : -103;
            // The walk itself stays square: the collider and the lips follow the layout, so the
            // hero stands where the coping is; only the stone body under them is rounded.
            var (squareLeft, squareRight) = Tangents(shape.outline, true);
            if (shape.collides)
            {
                go.layer = LayerMask.NameToLayer("World");
                var collider = go.AddComponent<PolygonCollider2D>();
                collider.points = Sample(shape.outline, squareLeft, squareRight, true).ToArray();
            }
            controller.RefreshSpriteShape();
            BakeAndReport(controller, shape.name);
            if (zoneArt != null && shape.profile != ParadiseLayout.Profile.Decor)
            {
                BuildLips(
                    go.transform,
                    shape.outline,
                    squareLeft,
                    squareRight,
                    zoneArt,
                    masonry && zoneArt.terraceLip != null ? zoneArt.terraceLip : zoneArt.lip ?? zoneArt.ledge
                );
                if (zoneArt.garden && shape.collides)
                {
                    var sampled = Sample(points, left, right, true);
                    ParadiseGarden.Depth(go.transform, sampled, zoneArt);
                    ParadiseGarden.UnderLip(go.transform, sampled, masonry);
                    if (masonry)
                    {
                        ParadiseGarden.Piers(go.transform, sampled, zoneArt);
                        if (zone != null)
                        {
                            ParadiseGarden.Quoins(go.transform, zone, shape, zoneArt);
                        }
                    }
                    else if (shape.profile == ParadiseLayout.Profile.Ground)
                    {
                        ParadiseGarden.SoftenSides(go.transform, sampled, zoneArt);
                    }
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
                    // An outer corner: the ground falls away past the run's end. Against a wall
                    // that rises, the strip runs into the wall and needs no cap.
                    var before = points[(runStart - 1 + points.Count) % points.Count];
                    var after = points[(i + 1) % points.Count];
                    var capStart = before.y < points[runStart].y - 0.01f;
                    var capEnd = after.y < points[i].y - 0.01f;
                    BuildLip(
                        parent,
                        $"Lip{lip++}",
                        points,
                        left,
                        right,
                        runStart,
                        i,
                        zoneArt,
                        lipProfile,
                        capStart,
                        capEnd
                    );
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
            SpriteShape lipProfile,
            bool capStart = false,
            bool capEnd = false
        )
        {
            var strip =
                lipProfile != null && lipProfile.angleRanges.Count > 0 && lipProfile.angleRanges[0].sprites.Count > 0
                    ? lipProfile.angleRanges[0].sprites[0].name
                    : null;
            var caps = zoneArt.garden && to > from && ParadiseGarden.CapsReady(strip);
            capStart &= caps;
            capEnd &= caps;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var controller = go.AddComponent<SpriteShapeController>();
            var renderer = go.GetComponent<SpriteShapeRenderer>();
            renderer.sharedMaterials = new[] { zoneArt.lit, zoneArt.lit };
            renderer.sortingOrder = -99;
            controller.spriteShape = lipProfile;
            if (zoneArt.garden)
            {
                ParadiseNormals.Dress(controller, renderer, zoneArt.lit);
            }
            controller.autoUpdateCollider = false;
            controller.splineDetail = 16;
            var spline = controller.spline;
            spline.Clear();
            spline.isOpenEnded = true;
            for (var i = from; i <= to; i++)
            {
                var at = points[i].V;
                if ((i == from && capStart) || (i == to && capEnd))
                {
                    var inward = (points[i == from ? from + 1 : to - 1].V - at).normalized;
                    at += inward * ParadiseGarden.CapInset;
                }
                spline.InsertPointAt(i - from, new Vector3(at.x, at.y, 0f));
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
            if (capStart)
            {
                ParadiseGarden.Cap(
                    go.transform,
                    strip,
                    points[from].V,
                    (points[from + 1].V - points[from].V).normalized,
                    true,
                    zoneArt.lit,
                    -99
                );
            }
            if (capEnd)
            {
                ParadiseGarden.Cap(
                    go.transform,
                    strip,
                    points[to].V,
                    (points[to - 1].V - points[to].V).normalized,
                    false,
                    zoneArt.lit,
                    -99
                );
            }
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
            if (zoneArt != null && zoneArt.garden)
            {
                ParadiseNormals.Dress(controller, renderer, art.lit);
            }
            controller.autoUpdateCollider = false;
            controller.splineDetail = 16;
            var spline = controller.spline;
            spline.Clear();
            spline.isOpenEnded = true;
            // The greybox branch hangs just under the walkable line; the painted lip's
            // pivot is its walkable line, so it sits right on it.
            var drop = zoneArt != null ? 0f : 0.2f;
            var caps = zoneArt != null && zoneArt.garden && ParadiseGarden.CapsReady("deck_top");
            for (var i = 0; i < points.Count; i++)
            {
                var at = new Vector2(points[i].x, points[i].y - drop);
                if (caps && (i == 0 || i == points.Count - 1))
                {
                    // In along the deck by the cap's length; the collider keeps the full deck.
                    var inward = (points[i == 0 ? 1 : points.Count - 2].V - points[i].V).normalized;
                    at += inward * ParadiseGarden.CapLength;
                }
                spline.InsertPointAt(i, new Vector3(at.x, at.y, 0f));
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
            if (caps)
            {
                ParadiseGarden.DeckCaps(go.transform, points, zoneArt);
            }
        }

        public const float MarbleCornerRadius = 0.55f;
        public const float RockCornerRadius = 1f;

        /// <summary>Below this a corner is under the clouds and stays square.</summary>
        private const float UnseenBelow = -95f;

        /// <summary>
        /// The outline with every sharp, visible corner replaced by a fillet: a quadratic arc
        /// from `radius` before the corner to `radius` after it, tangent to both edges. Sharp
        /// points stay sharp in the arc (sampled), so the lips and the collider follow it.
        /// </summary>
        public static List<ParadiseLayout.P> RoundCorners(List<ParadiseLayout.P> points, float radius)
        {
            var n = points.Count;
            var result = new List<ParadiseLayout.P>();
            for (var i = 0; i < n; i++)
            {
                var p = points[i];
                if (!p.sharp || p.y < UnseenBelow)
                {
                    result.Add(p);
                    continue;
                }
                var c = p.V;
                var a = points[(i - 1 + n) % n].V - c;
                var b = points[(i + 1) % n].V - c;
                var la = a.magnitude;
                var lb = b.magnitude;
                if (la < 0.01f || lb < 0.01f || Vector2.Dot(a / la, b / lb) < -0.98f)
                {
                    result.Add(p);
                    continue;
                }
                var r = Mathf.Min(radius, la * 0.45f, lb * 0.45f);
                var start = c + a / la * r;
                var end = c + b / lb * r;
                const int steps = 5;
                for (var k = 0; k <= steps; k++)
                {
                    var t = k / (float)steps;
                    var q = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * c + t * t * end;
                    result.Add(new ParadiseLayout.P(q.x, q.y, true));
                }
            }
            return result;
        }

        private const float RoughStep = 0.4f;
        private const float RoughFade = 1f;
        private const float RoughAmount = 0.9f;

        /// <summary>
        /// The outline with its steep edges broken up: each wanders outward, never inward, so
        /// the rock can only ever overlap the hero and never leaves a gap beside the collider.
        /// Flat at both ends, where it meets the lips and the corners, and straight under
        /// the clouds, where nobody sees it.
        /// </summary>
        public static List<ParadiseLayout.P> RoughenSides(List<ParadiseLayout.P> points, float seed)
        {
            var n = points.Count;
            var result = new List<ParadiseLayout.P>();
            for (var i = 0; i < n; i++)
            {
                var a = points[i];
                result.Add(a);
                var edge = points[(i + 1) % n].V - a.V;
                var length = edge.magnitude;
                if (length < RoughFade * 2f || Mathf.Abs(edge.y) < Mathf.Abs(edge.x) * 2f)
                {
                    continue;
                }
                var along = edge / length;
                // The outline runs clockwise, so outward is to its left.
                var outward = new Vector2(-along.y, along.x);
                var steps = Mathf.FloorToInt(length / RoughStep);
                for (var k = 1; k < steps; k++)
                {
                    var distance = k * length / steps;
                    var p = a.V + along * distance;
                    if (p.y < UnseenBelow)
                    {
                        continue;
                    }
                    var fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(distance, length - distance) / RoughFade);
                    var bulge =
                        0.6f * Mathf.PerlinNoise(p.y * 0.25f, seed)
                        + 0.3f * Mathf.PerlinNoise(p.y * 0.75f, seed + 7f)
                        + 0.1f * Mathf.PerlinNoise(p.y * 2.1f, seed + 13f);
                    p += outward * (RoughAmount * fade * bulge);
                    result.Add(new ParadiseLayout.P(p.x, p.y, true));
                }
            }
            return result;
        }

        /// <summary>The cloud sea's level: a column with nothing under it goes down into it.</summary>
        public const float CloudSea = -112f;

        /// <summary>
        /// A zone is built only if ParadiseChecker finds nothing on its main path and the
        /// region's save shrines keep their spacing, so a batch build fails loudly on a jump
        /// the hero cannot make (docs/design/paradise.md, section 2).
        /// </summary>
        public static void CheckConstructionRules(ParadiseLayout.Zone zone)
        {
            var violations = ParadiseChecker.CheckZone(zone);
            violations.AddRange(ParadiseChecker.CheckShrineSpacing(ParadiseLayout.All()));
            if (violations.Count > 0)
            {
                throw new System.InvalidOperationException(
                    $"Zone {zone.number} breaks the construction rules and was not built:\n"
                        + string.Join("\n", violations)
                );
            }
        }

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
        /// A deck's walk line, sampled, and where its columns stand: one near each end and every
        /// `spacing` units between, each on the first surface below the deck, or the cloud sea.
        /// The dressing plans what hangs under the deck from the same columns the builder puts up.
        /// </summary>
        public static (List<Vector2> line, List<(float x, float ground)> feet) Supports(
            ParadiseLayout.Bridge bridge,
            ParadiseLayout.Zone zone
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
            var feet = new List<(float x, float ground)>();
            for (var i = 0; i < count; i++)
            {
                var x = count == 1 ? x0 : Mathf.Lerp(x0, x1, i / (float)(count - 1));
                var top = DeckY(line, x) - DeckThickness;
                feet.Add((x, ParadiseDressing.SurfaceY(zone, x, CloudSea, top - 0.5f)));
            }
            return (line, feet);
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
            var (line, feet) = Supports(bridge, zone);
            var group = new GameObject(bridge.name + " Columns").transform;
            group.SetParent(parent, false);
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

        /// <summary>
        /// Thorns: the Hazard prefab (its trigger on the Hazard layer hurts the hero) sized to
        /// the layout's rect, with the greybox's red stand-in over it. The art pass replaces
        /// the stand-in with briars; the prefab instance stays.
        /// </summary>
        private static void BuildHazard(ParadiseLayout.Hazard hazard, Transform parent, Art art)
        {
            var prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HazardPrefabPath)
                ?? throw new System.InvalidOperationException(HazardPrefabPath + " is missing.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = hazard.name;
            go.transform.SetParent(parent, false);
            go.transform.position = hazard.rect.min;
            var box = go.GetComponent<BoxCollider2D>();
            box.size = hazard.rect.size;
            box.offset = hazard.rect.size / 2f;
            var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = hazard.rect.size / 2f;
            // The stand-in is 16 px at 64 px per unit: a quarter tile, scaled to the rect.
            visual.transform.localScale = new Vector3(hazard.rect.width * 4f, hazard.rect.height * 4f, 1f);
            visual.sprite = art.hazardSprite;
            visual.sharedMaterial = art.lit;
            visual.sortingOrder = -70;
        }

        /// <summary>
        /// A light barrier: the LightBarrier component (its cells, colliders and shimmer are
        /// made in Awake) with the greybox's cell sprite, plus a faint stand-in over the whole
        /// column so the wall shows in the editor before play.
        /// </summary>
        private static void BuildBarrier(ParadiseLayout.Barrier barrier, Transform parent, Art art)
        {
            var go = new GameObject(barrier.name);
            go.transform.SetParent(parent, false);
            go.transform.position = barrier.at;
            var wall = go.AddComponent<LightBarrier>();
            var serialized = new SerializedObject(wall);
            serialized.FindProperty("height").intValue = barrier.height;
            serialized.FindProperty("sprite").objectReferenceValue = art.barrierSprite;
            serialized.FindProperty("material").objectReferenceValue = art.lit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0.5f, -barrier.height / 2f, 0f);
            visual.transform.localScale = new Vector3(4f, barrier.height * 4f, 1f);
            visual.sprite = art.barrierSprite;
            visual.sharedMaterial = art.lit;
            visual.color = new Color(1f, 1f, 1f, 0.35f);
            visual.sortingOrder = 19;
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

        /// <summary>A story beat as a WorldPlacement, named after the last part of its id.</summary>
        private static WorldPlacement BuildPlacement(
            ParadiseLayout.Placement placement,
            Transform parent,
            PlacementCatalog catalog
        )
        {
            var go = new GameObject(placement.Name);
            go.transform.SetParent(parent, false);
            var built = go.AddComponent<WorldPlacement>();
            var serialized = new SerializedObject(built);
            serialized.FindProperty("id").stringValue = placement.id;
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ApplyPlacement(built, placement);
            return built;
        }

        /// <summary>
        /// Brings a WorldPlacement's position and fields to the layout's, leaving its id and
        /// catalog alone. Returns whether anything differed, so a sync that changes nothing
        /// saves nothing.
        /// </summary>
        private static bool ApplyPlacement(WorldPlacement target, ParadiseLayout.Placement placement)
        {
            var serialized = new SerializedObject(target);
            var changed = SetString(serialized, "type", placement.type);
            changed |= SetStrings(serialized, "dialogue", placement.dialogue);
            changed |= SetStrings(serialized, "rotate", placement.rotate);
            changed |= SetString(serialized, "placementName", placement.placementName);
            changed |= SetString(serialized, "character", placement.character);
            changed |= SetString(serialized, "item", placement.item);
            changed |= SetString(serialized, "unless", placement.unless);
            var facesLeft = serialized.FindProperty("facesLeft");
            if (facesLeft.boolValue != placement.facesLeft)
            {
                facesLeft.boolValue = placement.facesLeft;
                changed = true;
            }
            var abilityIndex = serialized.FindProperty("abilityIndex");
            if (abilityIndex.intValue != placement.abilityIndex)
            {
                abilityIndex.intValue = placement.abilityIndex;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var at = target.transform.position;
            if ((Vector2)at != placement.at)
            {
                target.transform.position = new Vector3(placement.at.x, placement.at.y, at.z);
                changed = true;
            }
            return changed;
        }

        private static bool SetString(SerializedObject serialized, string field, string value)
        {
            var property = serialized.FindProperty(field);
            value ??= "";
            if (property.stringValue == value)
            {
                return false;
            }
            property.stringValue = value;
            return true;
        }

        private static bool SetStrings(SerializedObject serialized, string field, string[] values)
        {
            var property = serialized.FindProperty(field);
            values ??= new string[0];
            var same = property.arraySize == values.Length;
            for (var i = 0; same && i < values.Length; i++)
            {
                same = property.GetArrayElementAtIndex(i).stringValue == values[i];
            }
            if (same)
            {
                return false;
            }
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
            return true;
        }

        /// <summary>
        /// Puts the layout's placements into every zone scene that exists, without rebuilding
        /// it: see <see cref="SyncZonePlacements"/>.
        /// A scene the sync does not change is not saved, so its diff stays empty. In batch
        /// mode: -executeMethod Descender.EditorTools.Paradise.ParadiseGreybox.SyncPlacements.
        /// </summary>
        [MenuItem("Descender/Paradise/Sync placements from layout")]
        public static void SyncPlacements()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            var fallback = AssetDatabase.LoadAssetAtPath<PlacementCatalog>(CatalogPath);
            var changed = new List<string>();
            foreach (var zone in ParadiseLayout.All())
            {
                if (!File.Exists(ScenePath(zone)))
                {
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(ScenePath(zone), OpenSceneMode.Single);
                if (SyncZonePlacements(zone, scene, fallback))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changed.Add(zone.scene);
                }
            }
            Debug.Log(
                changed.Count > 0
                    ? $"ParadiseGreybox: placements synced from the layout into {string.Join(", ", changed)}."
                    : "ParadiseGreybox: placements already match the layout; no scene was changed."
            );
        }

        /// <summary>
        /// Brings one open zone scene's placements to the layout's, by id: adds each that is
        /// missing (under the group the scene's saves stand in, with the catalog those saves
        /// use, or `fallback` in a scene without a save), moves and refills each that is
        /// there, and removes the design markers that placements replace. Nothing else in the
        /// scene is touched, and a placement the layout no longer names is left alone.
        /// Returns whether the scene changed; a second run returns false.
        /// </summary>
        public static bool SyncZonePlacements(ParadiseLayout.Zone zone, Scene scene, PlacementCatalog fallback)
        {
            var roots = scene.GetRootGameObjects();
            var existing = roots.SelectMany(root => root.GetComponentsInChildren<WorldPlacement>(true)).ToList();
            var save = existing.Find(p => p.Type == "save");
            var catalog = save != null ? CatalogOf(save) ?? fallback : fallback;
            var changed = false;
            Transform group = null;
            foreach (var placement in zone.placements)
            {
                var present = existing.Find(p => p.Id == placement.id);
                if (present != null)
                {
                    changed |= ApplyPlacement(present, placement);
                    continue;
                }
                group ??= PlacementGroup(zone, scene, roots, save);
                existing.Add(BuildPlacement(placement, group, catalog));
                changed = true;
            }
            var replaced = zone.ReplacedMarkers();
            foreach (var marker in roots.SelectMany(root => root.GetComponentsInChildren<DesignMarker>(true)).ToList())
            {
                if (replaced.Contains(marker.Label))
                {
                    Object.DestroyImmediate(marker.gameObject);
                    changed = true;
                }
            }
            return changed;
        }

        private static PlacementCatalog CatalogOf(WorldPlacement placement)
        {
            return new SerializedObject(placement).FindProperty("catalog").objectReferenceValue as PlacementCatalog;
        }

        /// <summary>
        /// Where a scene keeps its placements: beside its saves, else the Placements group
        /// BuildZone makes, else a new one under the zone's root (or at the scene's root).
        /// </summary>
        private static Transform PlacementGroup(
            ParadiseLayout.Zone zone,
            Scene scene,
            GameObject[] roots,
            WorldPlacement save
        )
        {
            if (save != null && save.transform.parent != null)
            {
                return save.transform.parent;
            }
            foreach (var root in roots)
            {
                var found = root.transform.Find("Placements");
                if (found != null)
                {
                    return found;
                }
            }
            var group = new GameObject("Placements");
            SceneManager.MoveGameObjectToScene(group, scene);
            var zoneRoot = System.Array.Find(roots, root => root.name == $"Zone{zone.number}");
            if (zoneRoot != null)
            {
                group.transform.SetParent(zoneRoot.transform, false);
            }
            return group.transform;
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

        /// <summary>
        /// The sunlit garden's accent lights at under half strength: the painted kit is already
        /// lit, and full lamps on top of the sun blow the marble and the leaves out to white.
        /// </summary>
        private const float GardenLampScale = 0.4f;

        private static void BuildLamp(ParadiseLayout.Lamp lamp, Transform parent, int index, float scale)
        {
            var go = new GameObject($"Light{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = lamp.at;
            var light = go.AddComponent<WorldLight>();
            var serialized = new SerializedObject(light);
            serialized.FindProperty("color").colorValue = lamp.color;
            serialized.FindProperty("energy").floatValue = lamp.energy * scale;
            serialized.FindProperty("radiusPixels").floatValue = lamp.radiusTiles * Player.PlayerTuning.PixelsPerUnit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The group a zone's camera moments are built under.</summary>
        public const string MomentsGroup = "CameraMoments";

        private static void BuildMoment(ParadiseLayout.Moment moment, Transform parent)
        {
            var go = new GameObject(moment.name);
            go.transform.SetParent(parent, false);
            go.transform.position = moment.area.center;
            go.AddComponent<CameraRig.CameraZone>()
                .Configure(
                    moment.area.size,
                    moment.framing,
                    moment.zoom,
                    false,
                    false,
                    moment.lookAheadPixels,
                    moment.blend
                );
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
            // One continuous land: the camera never stops at a zone's border (JoinRooms).
            serialized.FindProperty("joined").boolValue = true;
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
            // Painted art scrolls smoothly (no 16 px grid snapping, which steps 8 screen pixels
            // at a time at 1440p), and the walk sits below the middle of the view, as in the
            // key art: more of the garden above the hero, less wall under his feet.
            if (camera != null && camera.TryGetComponent<CameraRig.GameCamera>(out var follow))
            {
                var settings = new SerializedObject(follow);
                settings.FindProperty("snapToPixelGrid").boolValue = false;
                settings.FindProperty("framing").vector2Value = new Vector2(0f, 1.25f);
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            // Painted art has no pixel grid to keep: the prototype's pixel-perfect camera only
            // boxed the view in by whole multiples of 320x180, black bars on any screen between
            // them, and turned a camera zone's pull-back into a jump from one multiple to the
            // next. The camera's own size (5.625 units, the same 20 x 11.25 tile view) frames
            // it instead, filling the screen and zooming smoothly.
            if (
                camera != null
                && camera.TryGetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>(out var pixelPerfect)
            )
            {
                Object.DestroyImmediate(pixelPerfect);
            }
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = ParadiseLayout.ViewHalfHeight;
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

        /// <summary>The save shrines in the world's save order: zone by zone, west to east within a zone (D-0010).</summary>
        public static List<Vector2> CheckpointOrder(List<ParadiseLayout.Zone> zones)
        {
            return zones.SelectMany(z => z.saves.OrderBy(s => s.x)).ToList();
        }
    }
}
