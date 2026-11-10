using System;
using System.Collections.Generic;
using System.IO;
using Descender.World.Paradise;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Build hero rig: Ansel for the new Paradise, from the painted
    /// parts and the layout in Assets/Art/Paradise/Ansel (written by tools/hero_ansel/fit_parts.py).
    /// Builds one skeleton from ansel_rig.json, gives each bending part a grid mesh weighted to
    /// its bones so it curves at the joints instead of hinging, and saves
    /// Assets/Prefabs/Paradise/HeroRig.prefab, which HeroRigInstaller puts on the hero in
    /// ParadiseMain and HeroRig animates. Every part carries its normal map (ansel_*_n.png), so
    /// the scene's lights shape him; a small warm light behind his head rims his outline. Also
    /// builds his effects: dust, glints of light, streaks, the arc of the cut, a shockwave,
    /// splashes and ripples for the pools, a contact shadow (HeroShadow) and afterimages
    /// (HeroGhosts), from the textures in Ansel/Fx (tools/hero_ansel/fx_textures.py).
    /// </summary>
    public static class ParadiseHero
    {
        public const string HeroFolder = ParadiseArt.ArtRoot + "/Ansel";
        public const string PrefabPath = "Assets/Prefabs/Paradise/HeroRig.prefab";
        public const int BaseOrder = 10;
        private const int CloakJointsPerChain = 5;

        /// <summary>How close to a bone, in pixels, a vertex counts as wholly on it.</summary>
        private const float WeightSoftness = 10f;

        private static string LayoutPath => $"{HeroFolder}/ansel_rig.json";

        public static bool Available => File.Exists(LayoutPath);

        [Serializable]
        private sealed class RigBone
        {
            public string name;
            public string parent;
            public float x;
            public float y;
            public float tipX;
            public float tipY;

            public Vector2 At => new Vector2(x, y);
            public Vector2 Tip => new Vector2(tipX, tipY);
        }

        [Serializable]
        private sealed class RigPart
        {
            public string name;
            public string file;
            public int order;
            public bool skinned;
            public string[] bones;
            public int cols;
            public int rows;
            public float originX;
            public float originY;
            public string bone;

            public Vector2 Origin => new Vector2(originX, originY);
        }

        [Serializable]
        private sealed class RigLayout
        {
            public float ppu;
            public RigBone[] skeleton;
            public RigPart[] parts;
        }

        [MenuItem("Descender/Paradise/Build hero rig")]
        public static HeroRig Build()
        {
            var layout = JsonUtility.FromJson<RigLayout>(File.ReadAllText(LayoutPath));
            var byName = new Dictionary<string, RigBone>();
            foreach (var bone in layout.skeleton)
            {
                byName[bone.name] = bone;
            }

            var imported = new HashSet<string>();
            foreach (var part in layout.parts)
            {
                if (imported.Add(part.file))
                {
                    Import(part, layout.ppu, byName);
                }
            }

            var root = new GameObject(HeroRigInstaller.RigName);
            var transforms = new Dictionary<string, Transform>();
            foreach (var bone in layout.skeleton)
            {
                var hasParent = !string.IsNullOrEmpty(bone.parent);
                var made = new GameObject(bone.name).transform;
                made.SetParent(hasParent ? transforms[bone.parent] : root.transform, false);
                made.localPosition = hasParent ? bone.At - byName[bone.parent].At : bone.At;
                transforms[bone.name] = made;
            }

            var lit = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.LitMaterialPath);
            foreach (var part in layout.parts)
            {
                Dress(part, root.transform, transforms, byName, lit);
            }

            var cloak = new List<Transform>();
            for (var chain = 0; transforms.ContainsKey($"Cloak{chain}_0"); chain++)
            {
                for (var joint = 0; joint < CloakJointsPerChain; joint++)
                {
                    cloak.Add(transforms[$"Cloak{chain}_{joint}"]);
                }
            }

            var rig = root.AddComponent<HeroRig>();
            rig.SetBones(
                name =>
                    transforms.TryGetValue(name, out var found)
                        ? found
                        : throw new InvalidOperationException($"The layout has no bone {name}."),
                cloak.ToArray(),
                CloakJointsPerChain,
                new[] { transforms["HairBack"], transforms["HairTop"] },
                new[] { transforms["TailBack"], transforms["TailFront"] }
            );
            Effects(rig, root.transform, lit);
            RimLight(root.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"ParadiseHero: built {PrefabPath}.");
            return prefab.GetComponent<HeroRig>();
        }

        private static void Dress(
            RigPart part,
            Transform root,
            Dictionary<string, Transform> transforms,
            Dictionary<string, RigBone> byName,
            Material material
        )
        {
            var path = $"{HeroFolder}/{part.file}.png";
            var sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new InvalidOperationException($"Missing hero part {path}.");
            var holder = new GameObject(part.name);
            if (part.skinned)
            {
                holder.transform.SetParent(root, false);
                holder.transform.localPosition = part.Origin;
            }
            else
            {
                holder.transform.SetParent(transforms[part.bone], false);
                holder.transform.localPosition = part.Origin - byName[part.bone].At;
            }

            var renderer = holder.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = BaseOrder + part.order;
            if (!part.skinned)
            {
                return;
            }

            var bones = new Transform[part.bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                bones[i] = transforms[part.bones[i]];
            }
            var skin = holder.AddComponent<SpriteSkin>();
            skin.SetRootBone(bones[0]);
            var state = skin.SetBoneTransforms(bones);
            if (state != SpriteSkinState.Ready)
            {
                throw new InvalidOperationException($"The {part.name} skin is not ready: {state}.");
            }
        }

        private static void Import(RigPart part, float ppu, Dictionary<string, RigBone> byName)
        {
            var path = $"{HeroFolder}/{part.file}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer =
                AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing hero part " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            if (part.skinned)
            {
                Skin(importer, part, ppu, byName);
            }
            NormalMap(importer, $"{HeroFolder}/{part.file}_n.png");
        }

        /// <summary>Gives a part's sprite its normal map as the _NormalMap secondary texture.</summary>
        private static void NormalMap(TextureImporter sprite, string mapPath)
        {
            AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceSynchronousImport);
            var mapImporter =
                AssetImporter.GetAtPath(mapPath) as TextureImporter
                ?? throw new InvalidOperationException("Missing normal map " + mapPath);
            mapImporter.textureType = TextureImporterType.NormalMap;
            mapImporter.convertToNormalmap = false;
            mapImporter.mipmapEnabled = true;
            mapImporter.wrapMode = TextureWrapMode.Clamp;
            mapImporter.filterMode = FilterMode.Bilinear;
            mapImporter.textureCompression = TextureImporterCompression.Uncompressed;
            mapImporter.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var data = factories.GetSpriteEditorDataProviderFromObject(sprite);
            data.InitSpriteEditorDataProvider();
            data.GetDataProvider<ISecondaryTextureDataProvider>().textures = new[]
            {
                new SecondarySpriteTexture
                {
                    name = "_NormalMap",
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath),
                },
            };
            data.Apply();
            sprite.SaveAndReimport();
        }

        private static Sprite FxSprite(string name, float ppu)
        {
            var path = $"{HeroFolder}/Fx/{name}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer =
                AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing effect texture " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>The dust, glints and streaks (world-space particles), the arc of the cut and the shockwave.</summary>
        private static void Effects(HeroRig rig, Transform root, Material lit)
        {
            var glow = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.GlowMaterialPath);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.UnlitMaterialPath);
            var soft = FxSprite("fx_soft", 64f);
            var fx = new GameObject("Fx").transform;
            fx.SetParent(root, false);

            var dust = Particles(fx, "Dust", FxSprite("fx_dust", 128f), lit, 18, 0.05f, 2.5f, 1.8f);
            var glints = Particles(fx, "Glints", soft, glow, 20, -0.02f, 1.5f, 0.2f);
            var streaks = Particles(fx, "Streaks", FxSprite("fx_streak", 128f), glow, 19, 0f, 4f, 1f);
            var streakMain = streaks.main;
            streakMain.startSize3D = true;

            var arc = new GameObject("SlashArc").AddComponent<SpriteRenderer>();
            arc.transform.SetParent(root, false);
            // Centred on the sword shoulder; the arc's radius is the arm and the blade together.
            arc.transform.localPosition = new Vector3(-0.04f, 1.3f, 0f);
            arc.sprite = FxSprite("fx_slash", 100f);
            // Mirrored, so the arc's fading tail lies behind the blade, where it came from.
            arc.flipX = true;
            arc.sharedMaterial = glow;
            arc.color = new Color(1f, 0.95f, 0.82f, 0f);
            arc.sortingOrder = BaseOrder + 13;

            var shockwave = new GameObject("Shockwave").AddComponent<SpriteRenderer>();
            shockwave.transform.SetParent(root, false);
            shockwave.sprite = FxSprite("fx_ring", 256f);
            shockwave.sharedMaterial = glow;
            shockwave.color = new Color(1f, 0.9f, 0.65f, 0f);
            shockwave.sortingOrder = BaseOrder + 19;

            rig.SetEffects(dust, glints, streaks, arc, shockwave, soft, unlit);

            // Water: droplets that fall back, and rings drawn on the pools' own layer, over them.
            var splash = Particles(fx, "Splash", soft, unlit, 18, 1.1f, 0.6f, 0.7f);
            var ripples = Particles(fx, "Ripples", FxSprite("fx_ring", 256f), unlit, 5, 0f, 0f, 3.2f);
            var rippleMain = ripples.main;
            rippleMain.startSize3D = true;
            ripples.GetComponent<ParticleSystemRenderer>().sortingLayerName = ParadiseGarden.WaterLayer;
            rig.SetWater(splash, ripples);

            // The contact shadow under his feet, drawn just behind him.
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root, false);
            shadow.sprite = FxSprite("fx_shadow", 128f);
            shadow.sharedMaterial = unlit;
            shadow.color = new Color(0f, 0f, 0f, 0f);
            shadow.sortingOrder = BaseOrder;
            rig.gameObject.AddComponent<HeroShadow>().Configure(shadow);

            rig.gameObject.AddComponent<HeroGhosts>().Configure(glow);
        }

        private static ParticleSystem Particles(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            int order,
            float gravity,
            float drag,
            float grow
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.gravityModifier = gravity;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sheet.spriteCount == 0)
            {
                sheet.AddSprite(sprite);
            }
            else
            {
                sheet.SetSprite(0, sprite);
            }

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));

            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = BaseOrder + order;
            return system;
        }

        /// <summary>A small warm light behind and above his head, so the edges of his normal maps catch a rim.</summary>
        private static void RimLight(Transform root)
        {
            var go = new GameObject("RimLight");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(-0.5f, 1.45f, 0f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 0;
            light.color = new Color(1f, 0.88f, 0.66f);
            light.intensity = 0.8f;
            light.pointLightOuterRadius = 1.4f;
            light.pointLightInnerRadius = 0.1f;
            light.falloffIntensity = 0.6f;
            var settings = new SerializedObject(light);
            settings.FindProperty("m_NormalMapQuality").intValue = (int)Light2D.NormalMapQuality.Accurate;
            settings.FindProperty("m_NormalMapDistance").floatValue = 0.35f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Writes the part's bones and a grid mesh weighted to them into its sprite.</summary>
        private static void Skin(TextureImporter importer, RigPart part, float ppu, Dictionary<string, RigBone> byName)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var data = factories.GetSpriteEditorDataProviderFromObject(importer);
            data.InitSpriteEditorDataProvider();
            var rect = data.GetSpriteRects()[0];

            var count = part.bones.Length;
            var spriteBones = new List<SpriteBone>(count);
            var from = new Vector2[count];
            var to = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var bone = byName[part.bones[i]];
                from[i] = (bone.At - part.Origin) * ppu;
                to[i] = (bone.Tip - part.Origin) * ppu;
                var parent = Array.IndexOf(part.bones, bone.parent);
                spriteBones.Add(
                    new SpriteBone
                    {
                        name = bone.name,
                        guid = Hash128.Compute(part.file + bone.name).ToString(),
                        position = parent < 0 ? from[i] : (bone.At - byName[bone.parent].At) * ppu,
                        rotation = Quaternion.identity,
                        length = Mathf.Max(1f, Vector2.Distance(from[i], to[i])),
                        parentId = parent,
                    }
                );
            }

            var size = rect.rect.size;
            var columns = part.cols + 1;
            var vertices = new Vertex2DMetaData[columns * (part.rows + 1)];
            for (var y = 0; y <= part.rows; y++)
            {
                for (var x = 0; x <= part.cols; x++)
                {
                    var at = new Vector2(size.x * x / part.cols, size.y * y / part.rows);
                    vertices[y * columns + x] = new Vertex2DMetaData
                    {
                        position = at,
                        boneWeight = Weigh(at, from, to),
                    };
                }
            }

            var indices = new List<int>(part.cols * part.rows * 6);
            for (var y = 0; y < part.rows; y++)
            {
                for (var x = 0; x < part.cols; x++)
                {
                    var a = y * columns + x;
                    indices.AddRange(new[] { a, a + columns, a + 1, a + 1, a + columns, a + columns + 1 });
                }
            }

            var outline = new List<int>();
            for (var x = 0; x < part.cols; x++)
            {
                outline.Add(x);
            }
            for (var y = 0; y < part.rows; y++)
            {
                outline.Add(y * columns + part.cols);
            }
            for (var x = part.cols; x > 0; x--)
            {
                outline.Add(part.rows * columns + x);
            }
            for (var y = part.rows; y > 0; y--)
            {
                outline.Add(y * columns);
            }
            var edges = new Vector2Int[outline.Count];
            for (var i = 0; i < edges.Length; i++)
            {
                edges[i] = new Vector2Int(outline[i], outline[(i + 1) % outline.Count]);
            }

            data.GetDataProvider<ISpriteBoneDataProvider>().SetBones(rect.spriteID, spriteBones);
            var mesh = data.GetDataProvider<ISpriteMeshDataProvider>();
            mesh.SetVertices(rect.spriteID, vertices);
            mesh.SetIndices(rect.spriteID, indices.ToArray());
            mesh.SetEdges(rect.spriteID, edges);
            data.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>Weights a vertex to its four nearest bones, falling off steeply with distance.</summary>
        private static BoneWeight Weigh(Vector2 at, Vector2[] from, Vector2[] to)
        {
            var scored = new (float weight, int bone)[from.Length];
            for (var i = 0; i < from.Length; i++)
            {
                var distance = DistanceToSegment(at, from[i], to[i]) + WeightSoftness;
                scored[i] = (1f / (distance * distance * distance * distance), i);
            }
            Array.Sort(scored, (a, b) => b.weight.CompareTo(a.weight));

            var total = 0f;
            for (var i = 0; i < Mathf.Min(4, scored.Length); i++)
            {
                total += scored[i].weight;
            }
            float Share(int i) => i < scored.Length ? scored[i].weight / total : 0f;
            int Index(int i) => i < scored.Length ? scored[i].bone : 0;
            return new BoneWeight
            {
                boneIndex0 = Index(0),
                boneIndex1 = Index(1),
                boneIndex2 = Index(2),
                boneIndex3 = Index(3),
                weight0 = Share(0),
                weight1 = Share(1),
                weight2 = Share(2),
                weight3 = Share(3),
            };
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var line = b - a;
            var along = line.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, line) / line.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + line * along);
        }
    }
}
