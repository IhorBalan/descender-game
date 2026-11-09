using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Create zone looks: creates the post-processing profiles of
    /// the first two Paradise zones (docs/design/paradise.md, sections 4 and 5), graded by
    /// <see cref="ParadiseGrade"/>, and writes the materials for the Paradise shaders in
    /// Assets/Shaders/Paradise. The script is the source of truth for the materials' numbers:
    /// running it again finds the same assets and sets the same values (tweak here, not in the
    /// inspector). Zone profiles go on each zone's Volume, blended over about a screen at the
    /// borders.
    /// </summary>
    public static class ParadiseLookSetup
    {
        public const string PostFolder = "Assets/Settings/Post";
        public const string Zone1ProfilePath = PostFolder + "/Paradise_Z1.asset";
        public const string Zone2ProfilePath = PostFolder + "/Paradise_Z2.asset";
        public const string MaterialFolder = "Assets/Settings/Materials/Paradise";

        public const string LitSwayShader = "Descender/Sprite Lit Sway";
        public const string UnlitHazeShader = "Descender/Sprite Unlit Haze";
        public const string LightShaftShader = "Descender/Light Shaft";

        /// <summary>Zone 1 morning sky near the horizon: what far layers fade into.</summary>
        public static readonly Color Zone1Haze = new Color(0.80f, 0.88f, 0.97f, 1f);

        /// <summary>Zone 2 air in the orchard: a soft blue-green, so far trees sink into it.</summary>
        public static readonly Color Zone2Haze = new Color(0.62f, 0.80f, 0.84f, 1f);

        [MenuItem("Descender/Paradise/Create zone looks")]
        public static void Run()
        {
            System.IO.Directory.CreateDirectory(PostFolder);
            System.IO.Directory.CreateDirectory(MaterialFolder);

            ParadiseGrade.Apply(LoadOrCreateProfile(Zone1ProfilePath), 1);
            ParadiseGrade.Apply(LoadOrCreateProfile(Zone2ProfilePath), 2);
            BuildMaterials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"ParadiseLookSetup: wrote {Zone1ProfilePath}, {Zone2ProfilePath} and the materials in {MaterialFolder}"
            );
        }

        /// <summary>
        /// One material per use: the play layer's plants (sway and push), mid background
        /// per zone (lit, gentle sway, a little haze), far layers per zone (unlit, mostly
        /// haze), dark foreground silhouettes per zone (unlit, pushed toward near-black,
        /// sway and push so they react as the hero passes), and the zone 2 canopy shafts.
        /// </summary>
        private static void BuildMaterials()
        {
            var plant = LoadOrCreateMaterial("Paradise Plant Sway", LitSwayShader);
            SetSway(plant, 0.12f, 1.4f, 0.7f, 1.5f, 0.35f);
            SetHaze(plant, Color.white, 0f);

            var midZ1 = LoadOrCreateMaterial("Paradise Mid Z1", LitSwayShader);
            SetSway(midZ1, 0.04f, 0.9f, 0.5f, 1.5f, 0f);
            SetHaze(midZ1, Zone1Haze, 0.12f);

            var midZ2 = LoadOrCreateMaterial("Paradise Mid Z2", LitSwayShader);
            SetSway(midZ2, 0.04f, 0.9f, 0.5f, 1.5f, 0f);
            SetHaze(midZ2, Zone2Haze, 0.18f);

            var farZ1 = LoadOrCreateMaterial("Paradise Far Z1", UnlitHazeShader);
            SetSway(farZ1, 0f, 0.6f, 0.3f, 1.5f, 0f);
            SetHaze(farZ1, Zone1Haze, 0.3f);

            var farZ2 = LoadOrCreateMaterial("Paradise Far Z2", UnlitHazeShader);
            SetSway(farZ2, 0f, 0.6f, 0.3f, 1.5f, 0f);
            SetHaze(farZ2, Zone2Haze, 0.38f);

            var foreZ1 = LoadOrCreateMaterial("Paradise Foreground Z1", UnlitHazeShader);
            SetSway(foreZ1, 0.1f, 1.1f, 0.6f, 2f, 0.3f);
            SetHaze(foreZ1, new Color(0.16f, 0.07f, 0.10f, 1f), 0.75f);

            var foreZ2 = LoadOrCreateMaterial("Paradise Foreground Z2", UnlitHazeShader);
            SetSway(foreZ2, 0.1f, 1.1f, 0.6f, 2f, 0.3f);
            SetHaze(foreZ2, new Color(0.02f, 0.07f, 0.09f, 1f), 0.8f);

            // Sunlight breaking through: pale warm against the teal, so it reads as the
            // outside sun. Intensity keeps it mostly below the bloom threshold; where it
            // crosses something bright it starts to glow.
            var shaft = LoadOrCreateMaterial("Paradise Light Shaft Z2", LightShaftShader);
            shaft.SetFloat("_UseRayTexture", 0f);
            shaft.SetColor("_Color", new Color(1f, 0.95f, 0.8f, 1f));
            shaft.SetFloat("_Intensity", 0.55f);
            shaft.SetFloat("_TopWidth", 0.6f);
            shaft.SetFloat("_EdgeSoftness", 0.8f);
            shaft.SetFloat("_FadeTop", 0.25f);
            shaft.SetFloat("_FadeBottom", 0.45f);
            shaft.SetFloat("_ScrollSpeed", 0.25f);
            shaft.SetVector("_NoiseScale", new Vector4(5f, 1.5f, 0f, 0f));
            shaft.SetFloat("_NoiseStrength", 0.55f);
            shaft.SetFloat("_PulseAmount", 0.15f);
            shaft.SetFloat("_PulseSpeed", 0.7f);
            EditorUtility.SetDirty(shaft);
        }

        private static void SetSway(
            Material material,
            float amount,
            float speed,
            float scale,
            float pushRadius,
            float pushStrength
        )
        {
            material.SetFloat("_SwayAmount", amount);
            material.SetFloat("_SwaySpeed", speed);
            material.SetFloat("_SwayScale", scale);
            material.SetFloat("_SwayUseObjectY", 0f);
            material.SetFloat("_SwayHeight", 1f);
            material.SetFloat("_PushRadius", pushRadius);
            material.SetFloat("_PushStrength", pushStrength);
            EditorUtility.SetDirty(material);
        }

        private static void SetHaze(Material material, Color color, float amount)
        {
            material.SetColor("_HazeColor", color);
            material.SetFloat("_HazeAmount", amount);
            EditorUtility.SetDirty(material);
        }

        private static VolumeProfile LoadOrCreateProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            // A component whose sub-asset was lost would make TryGet/Add misbehave.
            profile.components.RemoveAll(c => c == null);
            return profile;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    $"Shader {shaderName} not found; is Assets/Shaders/Paradise imported?"
                );
            }
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }
    }
}
