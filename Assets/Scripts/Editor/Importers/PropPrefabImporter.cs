using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Descender.EditorTools.Importers
{
    /// <summary>
    /// Descender > Import > Prop prefabs: a prefab for every prop PNG under
    /// Assets/Art/Props/&lt;region&gt;/ (a `&lt;name&gt;.png`, its `_n.png` normal map, which
    /// the .meta already attaches as the sprite's _NormalMap, and for one that
    /// shines a `_glow.png`), under Assets/Prefabs/Props/&lt;region&gt;/&lt;name&gt;.prefab:
    /// a WorldProp root with an Art sprite on the lit sprite material and, when
    /// there is a glow, a Glow child on the additive one. A prop is placed by its
    /// top-left corner, as the Godot prop scenes were. Run again after a PNG is
    /// added or repainted; existing prefabs keep their changes except the sprites.
    /// </summary>
    public static class PropPrefabImporter
    {
        public const string PropsFolder = "Assets/Art/Props";
        public const string PrefabsFolder = "Assets/Prefabs/Props";
        public const string LitMaterialPath = "Assets/Settings/Materials/Sprite Lit.mat";
        public const string GlowMaterialPath = "Assets/Settings/Materials/Sprite Glow.mat";

        [MenuItem("Descender/Import/Prop prefabs")]
        public static void ImportAll()
        {
            var count = 0;
            foreach (var regionFolder in AssetDatabase.GetSubFolders(PropsFolder))
            {
                var region = Path.GetFileName(regionFolder);
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { regionFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var stem = Path.GetFileNameWithoutExtension(path);
                    if (stem.EndsWith("_n") || stem.EndsWith("_glow"))
                    {
                        continue;
                    }
                    if (Import(region, stem))
                    {
                        count++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Descender: {count} prop prefabs written.");
        }

        public static bool Import(string region, string stem)
        {
            var art = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsFolder}/{region}/{stem}.png");
            if (art == null)
            {
                return false;
            }
            var glow = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsFolder}/{region}/{stem}_glow.png");
            var folder = $"{PrefabsFolder}/{region}";
            Directory.CreateDirectory(folder);
            var prefabPath = $"{folder}/{stem}.prefab";
            var root = new GameObject(ToTitle(stem));
            root.AddComponent<Descender.World.WorldProp>();
            var artObject = new GameObject("Art");
            artObject.transform.SetParent(root.transform, false);
            var renderer = artObject.AddComponent<SpriteRenderer>();
            renderer.sprite = art;
            renderer.sharedMaterial = Material(LitMaterialPath, "Universal Render Pipeline/2D/Sprite-Lit-Default");
            renderer.sortingLayerName = "Props";
            if (glow != null)
            {
                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(root.transform, false);
                var glowRenderer = glowObject.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = glow;
                glowRenderer.sharedMaterial = Material(
                    GlowMaterialPath,
                    "Universal Render Pipeline/2D/Sprite-Unlit-Default",
                    additive: true
                );
                glowRenderer.sortingLayerName = "Props";
                glowRenderer.sortingOrder = 1;
            }
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                // Keep the prefab's own edits: only the sprites are refreshed.
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var artRenderer = contents.transform.Find("Art")?.GetComponent<SpriteRenderer>();
                if (artRenderer != null)
                {
                    artRenderer.sprite = art;
                }
                var glowRenderer = contents.transform.Find("Glow")?.GetComponent<SpriteRenderer>();
                if (glowRenderer != null && glow != null)
                {
                    glowRenderer.sprite = glow;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                Object.DestroyImmediate(root);
                return true;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return true;
        }

        private static Material Material(string path, string shaderName, bool additive = false)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets/Settings/Materials");
            var shader = Shader.Find(shaderName);
            material = new Material(shader);
            if (additive)
            {
                material.SetFloat("_BlendMode", 1f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.One);
                material.EnableKeyword("_ALPHABLEND_ON");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>"blossom_tree" to "Blossom Tree".</summary>
        public static string ToTitle(string stem)
        {
            var words = stem.Split('_');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }
    }
}
