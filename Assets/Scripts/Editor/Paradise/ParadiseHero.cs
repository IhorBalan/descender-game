using System;
using System.Collections.Generic;
using System.IO;
using Descender.World.Paradise;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
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
    /// ParadiseMain and HeroRig animates. The far limbs are the near ones' sprites, darkened.
    /// </summary>
    public static class ParadiseHero
    {
        public const string HeroFolder = ParadiseArt.ArtRoot + "/Ansel";
        public const string PrefabPath = "Assets/Prefabs/Paradise/HeroRig.prefab";
        public const int BaseOrder = 10;
        private const int CloakJointsPerChain = 5;

        /// <summary>How close to a bone, in pixels, a vertex counts as wholly on it.</summary>
        private const float WeightSoftness = 10f;

        private static readonly Color Far = new Color(0.58f, 0.58f, 0.70f);

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
            public bool far;
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
                transforms["Hips"],
                transforms["Spine"],
                transforms["Chest"],
                transforms["Head"],
                transforms["UpperArmFront"],
                transforms["ForeArmFront"],
                transforms["Sword"],
                transforms["UpperArmBack"],
                transforms["ForeArmBack"],
                transforms["ThighFront"],
                transforms["ShinFront"],
                transforms["BootFront"],
                transforms["ThighBack"],
                transforms["ShinBack"],
                transforms["BootBack"],
                cloak.ToArray(),
                CloakJointsPerChain
            );

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
            renderer.color = part.far ? Far : Color.white;
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
