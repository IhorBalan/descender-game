using System.IO;
using Descender.World.Paradise;
using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Build hero rig: Ansel at full resolution for the new
    /// Paradise, from the cutout parts painted by tools/paradise_art/pa_hero.py
    /// (Assets/Art/Paradise/Hero, 128 px a unit, each part's pivot on its joint). Imports
    /// the parts, stands them on bones in the rest skeleton of hero_rig.json (converted to
    /// units, feet at the rig's origin) and saves Assets/Prefabs/Paradise/HeroRig.prefab,
    /// which HeroRigInstaller puts on the hero in ParadiseMain and HeroRig animates.
    /// Legs hang from the hips, apart from the torso, so the body can lean without
    /// dragging them. The far limbs are the near ones' sprites, darkened.
    /// </summary>
    public static class ParadiseHero
    {
        public const string HeroFolder = ParadiseArt.ArtRoot + "/Hero";
        public const string PrefabPath = "Assets/Prefabs/Paradise/HeroRig.prefab";
        public const float Ppu = 128f;
        public const int BaseOrder = 10;

        private static readonly Color Far = new Color(0.58f, 0.58f, 0.70f);

        public static bool Available => File.Exists($"{HeroFolder}/hero_torso.png");

        // Pivots (normalised, Unity's bottom-left origin) from hero_rig.json.
        private static readonly (string part, float x, float y)[] Pivots =
        {
            ("hero_torso", 0.476f, 0.227f),
            ("hero_head", 0.558f, 0.237f),
            ("hero_hair_tuft", 0.708f, 0.325f),
            ("hero_upper_arm", 0.5f, 0.821f),
            ("hero_fore_arm", 0.455f, 0.839f),
            ("hero_thigh", 0.48f, 0.833f),
            ("hero_shin", 0.476f, 0.85f),
            ("hero_boot", 0.333f, 0.615f),
            ("hero_sword", 0.154f, 0.5f),
            ("hero_wing", 0.9f, 0.127f),
            ("hero_wing_glow", 0.81f, 0.211f),
        };

        /// <summary>The rest skeleton in hero_rig.json's shared frame: pixels, y down, soles at 205.</summary>
        private static Vector2 At(float x, float y) => new Vector2(x / Ppu, (205f - y) / Ppu);

        [MenuItem("Descender/Paradise/Build hero rig")]
        public static HeroRig Build()
        {
            foreach (var (part, x, y) in Pivots)
            {
                Import($"{HeroFolder}/{part}.png", new Vector2(x, y));
            }
            var hip = At(0f, 100f);
            var neck = At(3f, 38f);
            var shoulderF = At(1f, 47f);
            var shoulderB = At(-2f, 46f);
            var wingRoot = At(-9f, 56f);
            var upperLength = 0.281f;
            var gripFromElbow = new Vector2((11.2f - 10f) / Ppu, -(46.5f - 9f) / Ppu);
            var thighLength = 0.383f;
            var shinLength = 0.336f;
            // The hair tuft's root, from the head's pivot (24, 45) to its joint (12, 14.5), y down.
            var tuft = new Vector2((12f - 24f) / Ppu, (45f - 14.5f) / Ppu);

            var root = new GameObject(HeroRigInstaller.RigName);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.GlowMaterialPath);
            var lit = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.LitMaterialPath);

            var hips = Bone(root.transform, "Hips", hip);
            var torso = Part(hips, "Torso", "hero_torso", Vector2.zero, 13, lit, Color.white);
            var head = Part(torso, "Head", "hero_head", neck - hip, 12, lit, Color.white);
            var hair = Part(head, "HairTuft", "hero_hair_tuft", tuft, 11, lit, Color.white);

            var upperF = Part(torso, "UpperArmFront", "hero_upper_arm", shoulderF - hip, 16, lit, Color.white);
            var foreF = Part(
                upperF,
                "ForeArmFront",
                "hero_fore_arm",
                new Vector2(0f, -upperLength),
                15,
                lit,
                Color.white
            );
            var sword = Part(foreF, "Sword", "hero_sword", gripFromElbow, 14, lit, Color.white);
            var upperB = Part(torso, "UpperArmBack", "hero_upper_arm", shoulderB - hip, 4, lit, Far);
            var foreB = Part(upperB, "ForeArmBack", "hero_fore_arm", new Vector2(0f, -upperLength), 3, lit, Far);

            // The wing is light, not flesh: unlit, warm, so it reads even in shadow and against the sky.
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(ParadiseArt.UnlitMaterialPath);
            var wing = Part(torso, "Wing", "hero_wing", wingRoot - hip, 2, unlit, new Color(1f, 0.95f, 0.82f));
            var wingGlow = Part(wing, "WingGlow", "hero_wing_glow", Vector2.zero, 1, glow, new Color(1f, 1f, 1f, 0.6f));

            var thighF = Part(hips, "ThighFront", "hero_thigh", Vector2.zero, 10, lit, Color.white);
            var shinF = Part(thighF, "ShinFront", "hero_shin", new Vector2(0f, -thighLength), 9, lit, Color.white);
            var bootF = Part(shinF, "BootFront", "hero_boot", new Vector2(0f, -shinLength), 8, lit, Color.white);
            var thighB = Part(hips, "ThighBack", "hero_thigh", Vector2.zero, 7, lit, Far);
            var shinB = Part(thighB, "ShinBack", "hero_shin", new Vector2(0f, -thighLength), 6, lit, Far);
            var bootB = Part(shinB, "BootBack", "hero_boot", new Vector2(0f, -shinLength), 5, lit, Far);

            var rig = root.AddComponent<HeroRig>();
            rig.SetBones(
                hips,
                torso,
                head,
                upperF,
                foreF,
                sword,
                upperB,
                foreB,
                thighF,
                shinF,
                bootF,
                thighB,
                shinB,
                bootB,
                wing,
                wingGlow.GetComponent<SpriteRenderer>(),
                hair
            );

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"ParadiseHero: built {PrefabPath}.");
            return prefab.GetComponent<HeroRig>();
        }

        private static Transform Bone(Transform parent, string name, Vector2 at)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = at;
            return bone;
        }

        private static Transform Part(
            Transform parent,
            string name,
            string sprite,
            Vector2 at,
            int order,
            Material material,
            Color tint
        )
        {
            var bone = Bone(parent, name, at);
            var renderer = bone.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{HeroFolder}/{sprite}.png")
                ?? throw new System.InvalidOperationException($"Missing hero part {sprite}.");
            renderer.sharedMaterial = material;
            renderer.sortingOrder = BaseOrder + order;
            renderer.color = tint;
            return bone;
        }

        private static void Import(string path, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException("Missing hero part " + path);
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
