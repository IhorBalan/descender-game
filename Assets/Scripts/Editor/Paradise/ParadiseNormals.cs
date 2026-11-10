using System.Collections.Generic;
using System.IO;
using Descender.World.Paradise;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Light with a direction for the painted garden. Assigns the normal maps that
    /// tools/ai_art/normals.py writes to the kit's sprites (as the _NormalMap secondary
    /// texture) and to the SpriteShape terrain (which has no secondary textures, so each
    /// fill and strip gets a lit material holding its map), and builds the region's sun and
    /// far sky lights. Everything here runs from the rebuild and changes nothing that is
    /// already right, so a rebuild does not reimport the kit.
    /// </summary>
    public static class ParadiseNormals
    {
        public const string Folder = ParadiseArt.ArtRoot + "/Normals";
        private const string SecondaryName = "_NormalMap";

        /// <summary>The far lights sit this far out; their reach is well past it, so they are even across the view.</summary>
        private const float FarDistance = SunAndClouds.SunDistance;

        private static readonly string[] Tiling =
        {
            "rock_fill",
            "wall_block",
            "wall_pier",
            "deck_top",
            "wall_cap",
            "grass_lip",
            "island_bottom",
            "column_shaft",
            "grass_tuft",
        };

        /// <summary>Sets the maps' import settings and gives every sprite under `folders` its map.</summary>
        public static void Apply(IEnumerable<string> folders)
        {
            if (!Directory.Exists(Folder))
            {
                return;
            }
            foreach (var path in Directory.GetFiles(Folder, "*_n.png"))
            {
                ImportMap(path.Replace('\\', '/'));
            }
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            foreach (var folder in folders)
            {
                foreach (var path in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                {
                    Assign(path.Replace('\\', '/'), factory);
                }
            }
        }

        private static string MapPath(string name) => $"{Folder}/{name}_n.png";

        private static void ImportMap(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                return;
            }
            var name = Path.GetFileNameWithoutExtension(path);
            var tiles = System.Array.Exists(Tiling, name.Contains);
            var wrap = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (
                importer.textureType == TextureImporterType.NormalMap
                && !importer.mipmapEnabled
                && importer.wrapMode == wrap
                && importer.textureCompression == TextureImporterCompression.CompressedHQ
            )
            {
                return;
            }
            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void Assign(string spritePath, SpriteDataProviderFactories factory)
        {
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath(Path.GetFileNameWithoutExtension(spritePath)));
            if (map == null || !(AssetImporter.GetAtPath(spritePath) is TextureImporter importer))
            {
                return;
            }
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var secondary = provider.GetDataProvider<ISecondaryTextureDataProvider>();
            var current = secondary.textures;
            if (current != null && current.Length == 1 && current[0].name == SecondaryName && current[0].texture == map)
            {
                return;
            }
            secondary.textures = new[]
            {
                new SecondarySpriteTexture { name = SecondaryName, texture = map },
            };
            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>Gives a SpriteShape's fill and strip their normal maps, and the tangents those need.</summary>
        public static void Dress(SpriteShapeController controller, SpriteShapeRenderer renderer, Material lit)
        {
            var shape = controller.spriteShape;
            if (shape == null)
            {
                return;
            }
            Texture strip = null;
            foreach (var range in shape.angleRanges)
            {
                if (range.sprites.Count > 0 && range.sprites[0] != null)
                {
                    strip = range.sprites[0].texture;
                    break;
                }
            }
            controller.enableTangents = true;
            renderer.sharedMaterials = new[] { Lit(shape.fillTexture, lit), Lit(strip, lit) };
        }

        private static Material Lit(Texture texture, Material lit)
        {
            var map = texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath(texture.name)) : null;
            if (map == null)
            {
                return lit;
            }
            var material = ParadiseArt.SaveMaterial($"Paradise Lit {texture.name}", lit.shader.name);
            material.SetTexture(SecondaryName, map);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A lit material for a mesh that shows this texture, with the texture's normal map.</summary>
        public static Material LitMesh(Texture texture, Material lit)
        {
            var material = ParadiseArt.SaveMaterial($"Paradise Lit {texture.name}", lit.shader.name);
            material.mainTexture = texture;
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath(texture.name));
            if (map != null)
            {
                material.SetTexture(SecondaryName, map);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The region's sun with its cloud shadows, and the far lights that shape the kit.</summary>
        public static void BuildSkyLights(GameObject root)
        {
            var rig = root.GetComponent<SunAndClouds>();
            if (rig == null)
            {
                rig = root.AddComponent<SunAndClouds>();
            }
            foreach (var old in new[] { "Sun", "SkyLights", "Flecks" })
            {
                var found = root.transform.Find(old);
                if (found != null)
                {
                    Object.DestroyImmediate(found.gameObject);
                }
            }
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root.transform, false);
            var sun = sunObject.AddComponent<Light2D>();
            sun.lightType = Light2D.LightType.Sprite;
            sun.blendStyleIndex = 0;
            FaceNormals(sun, SunAndClouds.SunDirection.z * SunAndClouds.SunDistance);
            // The same sun seen through a canopy of leaves, in flecks (off in the open).
            var flecksObject = new GameObject("Flecks");
            flecksObject.transform.SetParent(root.transform, false);
            var flecks = flecksObject.AddComponent<Light2D>();
            flecks.lightType = Light2D.LightType.Sprite;
            flecks.blendStyleIndex = 0;
            FaceNormals(flecks, SunAndClouds.SunDirection.z * SunAndClouds.SunDistance);

            var sky = new GameObject("SkyLights").transform;
            sky.SetParent(root.transform, false);
            // Light thrown back up by the sunlit cloud sea, onto whatever faces down.
            var bounce = FarLight(sky, "Bounce", new Vector3(-0.25f, -0.9f, 0.34f), new Color(1f, 0.9f, 0.76f), 0.2f);
            // The sun low along the edges: almost nothing on flat stone, a rim on every bevel.
            var rim = FarLight(sky, "Rim", new Vector3(0.85f, 0.47f, 0.14f), new Color(1f, 0.82f, 0.56f), 0.36f);

            var settings = new SerializedObject(rig);
            settings.FindProperty("sun").objectReferenceValue = sun;
            settings.FindProperty("flecks").objectReferenceValue = flecks;
            settings.FindProperty("skyLights").objectReferenceValue = sky;
            settings.FindProperty("sunShare").floatValue = 0.4f;
            settings.FindProperty("skyLightShare").floatValue = bounce + rim;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A light far out along a direction, even across the view; returns what a flat surface gets from it.</summary>
        private static float FarLight(Transform parent, string name, Vector3 direction, Color color, float intensity)
        {
            direction.Normalize();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(direction.x, direction.y, 0f) * FarDistance;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 0;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = FarDistance * 2f;
            light.pointLightInnerRadius = FarDistance * 1.6f;
            FaceNormals(light, direction.z * FarDistance);
            return intensity * direction.z;
        }

        /// <summary>Turns on a light's normal mapping, which has no public setter.</summary>
        private static void FaceNormals(Light2D light, float height)
        {
            var settings = new SerializedObject(light);
            settings.FindProperty("m_NormalMapQuality").intValue = (int)Light2D.NormalMapQuality.Fast;
            settings.FindProperty("m_NormalMapDistance").floatValue = height;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
