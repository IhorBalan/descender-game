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
    /// The art pass of Paradise's zones (docs/design/paradise.md, section 5), used by
    /// ParadiseGreybox when the generated art is present (Assets/Art/Paradise/manifest.json,
    /// from tools/paradise_art). Prepare() sets the textures' import settings from the
    /// manifest, makes the derived images (the underside strips flipped so their roots
    /// hang down on a clockwise outline, one petal cut from the petal strip), the zones'
    /// SpriteShape profiles and the materials. Dress() puts a zone's parallax backdrop,
    /// props with their glow, foreground silhouettes, air particles, light shafts and
    /// post-processing look into its scene.
    /// </summary>
    public static class ParadiseArt
    {
        public const string ArtRoot = "Assets/Art/Paradise";
        public const string ManifestPath = ArtRoot + "/manifest.json";
        public const string DerivedFolder = ArtRoot + "/Derived";
        public const string MaterialFolder = "Assets/Settings/Materials/Paradise";
        public const string GlowMaterialPath = MaterialFolder + "/Paradise Glow.mat";
        public const string RegionProfilePath = "Assets/Settings/Post/Paradise_Region.asset";
        public const string LitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        public const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        public static bool Available => File.Exists(ManifestPath);

        [System.Serializable]
        private sealed class Manifest
        {
            public ManifestFile[] files;
        }

        [System.Serializable]
        private sealed class ManifestFile
        {
            public string path;
            public float[] pivot;
            public int ppu = 64;
            public string blend;
            public string[] tileable;
        }

        /// <summary>The prepared art of one zone.</summary>
        public sealed class ZoneArt
        {
            public int zone;
            public SpriteShape ground;
            public SpriteShape ledge;
            public Sprite sky;
            public Sprite[] backdrop = new Sprite[3];
            public Material backdropFar;
            public Material backdropMid;
            public Material mid;
            public Material plant;
            public Material foreground;
            public Material shaft;
            public Material glow;
            public Material lit;
            public Material unlit;
            public VolumeProfile look;
            public Color ambient;
            public Color shaftColor;
            public readonly Dictionary<string, Sprite> props = new Dictionary<string, Sprite>();
            public readonly List<Sprite> foregrounds = new List<Sprite>();
            public Sprite mote;
            public Sprite firefly;
            public Sprite pollen;
            public Sprite petal;
            public Sprite lightShaft;

            public bool garden;
            public SpriteShape lip;
            public SpriteShape terrace;
            public SpriteShape terraceLip;
            public Material water;

            public Sprite Prop(string name) => props.TryGetValue(name, out var s) ? s : null;

            public Sprite Glow(string name) => Prop(name + "_glow");
        }

        private static readonly Dictionary<int, ZoneArt> Prepared = new Dictionary<int, ZoneArt>();

        // ----------------------------------------------------------------- prepare

        public static void Prepare()
        {
            Prepared.Clear();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            foreach (var file in manifest.files)
            {
                ApplyImport(file);
            }
            Directory.CreateDirectory(DerivedFolder);
            foreach (var z in new[] { 1, 2 })
            {
                Derive(
                    $"{Folder(z)}/Ground/z{z}_ground_bottom.png",
                    $"{DerivedFolder}/z{z}_ground_bottom_flip.png",
                    flipY: true,
                    crop: null,
                    pivotY: 1f - 0.9062f
                );
            }
            Derive(
                $"{ArtRoot}/Common/Particles/petal_strip.png",
                $"{DerivedFolder}/petal.png",
                flipY: false,
                crop: new RectInt(0, 0, 32, 32),
                pivotY: 0.5f
            );

            CheckShaders();
            ParadiseLookSetup.Run();
            EnsureGlowMaterial();
            EnsureRegionProfile();
            Prepared[1] = Zone(1, new Color(1f, 0.9f, 0.84f), new Color(1f, 0.86f, 0.68f));
            Prepared[2] = Zone(2, new Color(0.42f, 0.6f, 0.66f), new Color(0.85f, 1f, 0.92f));
            if (ParadiseGarden.Available)
            {
                // The sunlit garden (docs/design/paradise.md, 5) supersedes the dawn and teal sets.
                ParadiseGarden.Prepare();
                Prepared[1] = ParadiseGarden.Zone(1, Prepared[1]);
                Prepared[2] = ParadiseGarden.Zone(2, Prepared[2]);
            }
        }

        /// <summary>Stops the build on a shader that does not compile, rather than drawing pink.</summary>
        private static void CheckShaders()
        {
            var problems = new List<string>();
            foreach (
                var path in new[]
                {
                    "Assets/Shaders/Paradise/SpriteLitSway.shader",
                    "Assets/Shaders/Paradise/SpriteUnlitHaze.shader",
                    "Assets/Shaders/Paradise/LightShaft.shader",
                    "Assets/Shaders/SpriteGlow.shader",
                }
            )
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport
                );
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    problems.Add($"{path}: not imported");
                    continue;
                }
                foreach (var m in ShaderUtil.GetShaderMessages(shader))
                {
                    if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    {
                        problems.Add($"{path} ({shader.name}) line {m.line}: {m.message}");
                    }
                }
                if (ShaderUtil.ShaderHasError(shader) && problems.Count == 0)
                {
                    problems.Add($"{path} ({shader.name}): has errors");
                }
            }
            if (problems.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "Paradise shaders do not compile:\n" + string.Join("\n", problems)
                );
            }
        }

        public static ZoneArt Get(int zone) => Prepared.TryGetValue(zone, out var art) ? art : null;

        public static string Folder(int zone) => zone == 1 ? ArtRoot + "/Z1_WakingHollow" : ArtRoot + "/Z2_Orchard";

        private static void ApplyImport(ManifestFile file)
        {
            var importer = AssetImporter.GetAtPath(file.path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            if (file.path.Contains("_ground_fill"))
            {
                importer.textureType = TextureImporterType.Default;
            }
            else
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = file.ppu > 0 ? file.ppu : 64;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot =
                    file.pivot != null && file.pivot.Length == 2
                        ? new Vector2(file.pivot[0], file.pivot[1])
                        : new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
            }
            // Repeat only along the axes that tile: a strip that tiles in x must clamp in y, or
            // bilinear filtering pulls its bottom row into its top edge (a dark line on the sky).
            var repeatX = file.tileable != null && System.Array.IndexOf(file.tileable, "x") >= 0;
            var repeatY = file.tileable != null && System.Array.IndexOf(file.tileable, "y") >= 0;
            importer.wrapModeU = repeatX ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.wrapModeV = repeatY ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>A vertically flipped copy of a strip, for undersides drawn along a clockwise outline.</summary>
        public static void DeriveFlipped(string source, string target, float pivotY)
        {
            Directory.CreateDirectory(DerivedFolder);
            Derive(source, target, flipY: true, crop: null, pivotY: pivotY);
        }

        private static void Derive(string source, string target, bool flipY, RectInt? crop, float pivotY)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(source));
            var rect = crop ?? new RectInt(0, 0, src.width, src.height);
            var dst = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            for (var y = 0; y < rect.height; y++)
            {
                for (var x = 0; x < rect.width; x++)
                {
                    // LoadImage rows run bottom-up; a crop of the strip's first frame is its left square.
                    var sy = flipY ? rect.height - 1 - y : y;
                    dst.SetPixel(x, y, src.GetPixel(rect.x + x, src.height - rect.height + sy - rect.y));
                }
            }
            File.WriteAllBytes(target, dst.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, pivotY);
            importer.SetTextureSettings(settings);
            importer.wrapMode = crop.HasValue ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Missing sprite " + path);
            }
            return sprite;
        }

        private static ZoneArt Zone(int z, Color ambient, Color shaftColor)
        {
            var folder = Folder(z);
            var art = new ZoneArt
            {
                zone = z,
                ambient = ambient,
                shaftColor = shaftColor,
                lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath),
                unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath),
                glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath),
                mid = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Paradise Mid Z{z}.mat"),
                plant = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Paradise Plant Sway.mat"),
                foreground = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Paradise Foreground Z{z}.mat"),
                look = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"Assets/Settings/Post/Paradise_Z{z}.asset"),
            };
            art.backdropFar = BackdropMaterial(z, "Far", 0.1f);
            art.backdropMid = BackdropMaterial(z, "Mid", 0f);
            art.shaft = ShaftMaterial(z, shaftColor);
            art.sky = LoadSprite($"{folder}/Backdrop/z{z}_bg0_sky.png");
            art.backdrop[0] = LoadSprite($"{folder}/Backdrop/z{z}_bg1_far.png");
            art.backdrop[1] = LoadSprite($"{folder}/Backdrop/z{z}_bg2_midfar.png");
            art.backdrop[2] = LoadSprite($"{folder}/Backdrop/z{z}_bg3_mid.png");
            foreach (var path in Directory.GetFiles($"{folder}/Props", "*.png"))
            {
                art.props[Path.GetFileNameWithoutExtension(path)] = LoadSprite(path.Replace('\\', '/'));
            }
            foreach (var path in Directory.GetFiles($"{folder}/Foreground", "*.png"))
            {
                art.foregrounds.Add(LoadSprite(path.Replace('\\', '/')));
            }
            art.mote = LoadSprite($"{ArtRoot}/Common/Particles/mote.png");
            art.firefly = LoadSprite($"{ArtRoot}/Common/Particles/firefly.png");
            art.pollen = LoadSprite($"{ArtRoot}/Common/Particles/pollen.png");
            art.petal = LoadSprite($"{DerivedFolder}/petal.png");
            art.lightShaft = LoadSprite($"{ArtRoot}/Common/Particles/light_shaft.png");

            var fill = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Ground/z{z}_ground_fill.png");
            var top = LoadSprite($"{folder}/Ground/z{z}_ground_top.png");
            var under = LoadSprite($"{DerivedFolder}/z{z}_ground_bottom_flip.png");
            // Clockwise outlines: a top runs +x (0), an underside -x (180), walls +/-90.
            // The top's lip is its own open spline (ParadiseGreybox.BuildLips): a closed
            // shape does not reliably draw its top edge. The ground keeps the roots below.
            art.ground = Profile(
                $"Ground_Z{z}",
                fill,
                new[]
                {
                    new AngleRange
                    {
                        start = 120f,
                        end = 180f,
                        order = 0,
                        sprites = new List<Sprite> { under },
                    },
                    new AngleRange
                    {
                        start = -180f,
                        end = -120f,
                        order = 0,
                        sprites = new List<Sprite> { under },
                    },
                }
            );
            art.ledge = Profile(
                $"Ledge_Z{z}",
                null,
                new[]
                {
                    new AngleRange
                    {
                        start = -90f,
                        end = 90f,
                        order = 1,
                        sprites = new List<Sprite> { top },
                    },
                }
            );
            return art;
        }

        public static SpriteShape Profile(string name, Texture2D fill, AngleRange[] ranges)
        {
            var path = $"{ArtRoot}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<SpriteShape>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SpriteShape>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.fillTexture = fill;
            profile.fillOffset = 0f;
            profile.angleRanges.Clear();
            profile.angleRanges.AddRange(ranges);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        public static Material SaveMaterial(string name, string shaderName)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var shader =
                Shader.Find(shaderName) ?? throw new System.InvalidOperationException("Missing shader " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialFolder);
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static Material BackdropMaterial(int z, string depth, float haze)
        {
            // The backdrops have their haze painted in; the shader adds only a touch.
            var material = SaveMaterial($"Paradise Backdrop {depth} Z{z}", "Descender/Sprite Unlit Haze");
            material.SetColor("_HazeColor", z == 1 ? new Color(0.98f, 0.78f, 0.68f) : new Color(0.12f, 0.38f, 0.42f));
            material.SetFloat("_HazeAmount", haze);
            material.SetFloat("_SwayAmount", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ShaftMaterial(int z, Color color)
        {
            var material = SaveMaterial($"Paradise Shaft Z{z}", "Descender/Light Shaft");
            material.SetFloat("_UseRayTexture", 1f);
            material.SetColor("_Color", color);
            material.SetFloat("_Intensity", z == 1 ? 0.45f : 0.6f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureGlowMaterial()
        {
            var material = SaveMaterial("Paradise Glow", "Descender/Sprite Glow");
            // Above 1 so bloom (threshold about 1) picks the glow up.
            material.SetFloat("_Intensity", 2.2f);
            EditorUtility.SetDirty(material);
        }

        /// <summary>One Neutral tonemapper for the whole region: tonemapping cannot blend between zones.</summary>
        private static void EnsureRegionProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RegionProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, RegionProfilePath);
            }
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }
            tonemapping.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(profile);
        }

        /// <summary>The region root's Volume: the shared Neutral tonemapper.</summary>
        public static void DressRegion(GameObject root)
        {
            var go = new GameObject("Region Look");
            go.transform.SetParent(root.transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0.5f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RegionProfilePath);
            if (ParadiseGarden.Available)
            {
                ParadiseGarden.DressRegion(root);
            }
        }

        // ------------------------------------------------------------------- dress

        /// <summary>A zone's look and dressing, under its scene root.</summary>
        public static void Dress(ParadiseLayout.Zone zone, Transform root, ParadiseDressing.Plan plan)
        {
            var art = Get(zone.number);
            if (art == null)
            {
                return;
            }
            if (art.garden)
            {
                ParadiseGarden.Dress(zone, root, art);
                return;
            }
            var look = Group(root, "Look");
            var volumeGo = new GameObject("Volume");
            volumeGo.transform.SetParent(look, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 0f;
            volume.sharedProfile = art.look;

            var backdrop = Group(root, "Backdrop");
            var centre = new Vector2((zone.xMin + zone.xMax) / 2f, plan.backdropY);
            var sky = Layer(
                backdrop,
                "Sky",
                art.sky,
                art.unlit,
                -1000 + zone.number,
                centre,
                Vector2.zero,
                cover: true
            );
            var far = Layer(
                backdrop,
                "Far",
                art.backdrop[0],
                art.backdropFar,
                -990 + zone.number,
                centre,
                new Vector2(0.08f, 0.03f),
                cover: false
            );
            var midFar = Layer(
                backdrop,
                "MidFar",
                art.backdrop[1],
                art.backdropFar,
                -980 + zone.number,
                centre,
                new Vector2(0.18f, 0.06f),
                cover: false
            );
            var mid = Layer(
                backdrop,
                "Mid",
                art.backdrop[2],
                art.backdropMid,
                -970 + zone.number,
                centre,
                new Vector2(0.34f, 0.12f),
                cover: false
            );
            root.gameObject.AddComponent<ZoneLook>()
                .Configure(zone.number, zone.xMin, zone.xMax, art.ambient, volume, sky, new[] { far, midFar, mid });

            var props = Group(root, "Props");
            foreach (var p in plan.props)
            {
                Prop(props, art, p);
            }
            var shafts = Group(root, "Shafts");
            foreach (var s in plan.shafts)
            {
                var r = Sprite(
                    shafts,
                    "Shaft",
                    art.lightShaft,
                    art.shaft,
                    -300,
                    new Vector3(s.x, s.y, 0f),
                    new Vector3(s.z, s.w, 1f)
                );
                r.color = art.shaftColor;
            }
            var front = Group(root, "Foreground");
            for (var i = 0; i < plan.foreground.Count; i++)
            {
                var f = plan.foreground[i];
                var sprite = art.foregrounds[f.index % art.foregrounds.Count];
                // Tall enough to reach from below the screen's bottom edge up past the ground.
                var scale = Mathf.Max(f.scale, 8.5f / Mathf.Max(0.1f, sprite.bounds.size.y));
                var r = Sprite(
                    front,
                    sprite.name,
                    sprite,
                    art.foreground,
                    400 + i % 3,
                    new Vector3(f.at.x, f.at.y, 0f),
                    Vector3.one * scale
                );
                r.flipX = f.flip;
                r.gameObject.AddComponent<ForegroundParallax>().Configure(0.3f);
            }
            var air = Group(root, "Air");
            var area = new Rect(zone.xMin, -105f, zone.xMax - zone.xMin, 90f);
            foreach (var a in plan.air)
            {
                var go = new GameObject(a.name);
                go.transform.SetParent(air, false);
                var sprite =
                    a.kind == "mote" ? art.mote
                    : a.kind == "firefly" ? art.firefly
                    : a.kind == "pollen" ? art.pollen
                    : art.petal;
                var material = a.kind == "petal" ? art.unlit : art.glow;
                go.AddComponent<RegionParticles>()
                    .Configure(
                        sprite,
                        a.amount,
                        a.color,
                        a.life,
                        a.direction,
                        a.spread,
                        a.speed,
                        a.size,
                        a.spin,
                        a.sway,
                        material,
                        area
                    );
            }
        }

        public static Transform Group(Transform root, string name)
        {
            var existing = root.Find(name);
            if (existing != null)
            {
                return existing;
            }
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        public static Transform Layer(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            int order,
            Vector2 at,
            Vector2 scroll,
            bool cover
        )
        {
            var renderer = Sprite(parent, name, sprite, material, order, at, Vector3.one);
            var layer = renderer.gameObject.AddComponent<BackdropLayer>();
            var serialized = new SerializedObject(layer);
            serialized.FindProperty("scroll").vector2Value = scroll;
            serialized.FindProperty("cover").boolValue = cover;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return renderer.transform;
        }

        public static SpriteRenderer Sprite(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            int order,
            Vector3 at,
            Vector3 scale
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void Prop(Transform parent, ZoneArt art, ParadiseDressing.PropSpot spot)
        {
            var sprite = art.Prop(spot.name);
            if (sprite == null)
            {
                Debug.LogWarning($"ParadiseArt: no prop '{spot.name}' in zone {art.zone}.");
                return;
            }
            var material = spot.layer == ParadiseDressing.PropLayer.Mid ? art.mid : art.plant;
            var order = spot.layer == ParadiseDressing.PropLayer.Mid ? -150 : -85;
            var r = Sprite(
                parent,
                spot.name,
                sprite,
                material,
                order,
                new Vector3(spot.at.x, spot.at.y, 0f),
                Vector3.one * spot.scale
            );
            r.flipX = spot.flip;
            var glow = art.Glow(spot.name);
            if (glow != null)
            {
                var g = Sprite(r.transform, "Glow", glow, art.glow, order + 1, r.transform.position, Vector3.one);
                g.transform.localScale = Vector3.one;
                g.flipX = spot.flip;
            }
        }

        /// <summary>Paints a pod or seed with its art in place of the greybox shape.</summary>
        public static void Skin(SpriteRenderer visual, ZoneArt art, string prop, float scale, Vector2 offset)
        {
            var sprite = art?.Prop(prop);
            if (sprite == null)
            {
                return;
            }
            visual.sprite = sprite;
            visual.sharedMaterial = art.plant;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = offset;
            var glow = art.Glow(prop);
            if (glow != null)
            {
                var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
                g.transform.SetParent(visual.transform, false);
                g.sprite = glow;
                g.sharedMaterial = art.glow;
                g.sortingOrder = visual.sortingOrder + 1;
            }
        }
    }
}
