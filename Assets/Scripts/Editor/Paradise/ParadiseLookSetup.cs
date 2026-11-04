using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// Descender &gt; Paradise &gt; Create zone looks: writes the post-processing looks of
    /// the first two Paradise zones (docs/design/paradise.md, sections 4 and 5) and the
    /// materials for the Paradise shaders in Assets/Shaders/Paradise. The script is the
    /// source of truth for these numbers: running it again finds the same assets and sets
    /// the same values (tweak here, not in the inspector). Zone profiles go on each zone's
    /// Volume, blended over about a screen at the borders. When the garden art is present,
    /// building a zone overwrites the profiles' grading with ParadiseGarden.TuneLook; the
    /// materials below are still this script's.
    ///
    /// Shared by both zones: Neutral tonemapping, because it only compresses what goes
    /// above 1 (the HDR glow of fruit, fungi and motes) and keeps the painted art's hues,
    /// where ACES would push gold toward orange and darken the midtones; and because the
    /// tonemapper is an enum that cannot blend, every zone must use the same one. Neutral
    /// still maps 1.0 to about 0.63 linear; the painted kit (tools/ai_art) is bright enough
    /// that exposure needs little or no lift. Bloom thresholds sit near 1 so only emissive
    /// (HDR) things glow, never a merely bright sprite.
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

            BuildZone1(LoadOrCreateProfile(Zone1ProfilePath));
            BuildZone2(LoadOrCreateProfile(Zone2ProfilePath));
            BuildMaterials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"ParadiseLookSetup: wrote {Zone1ProfilePath}, {Zone2ProfilePath} and the materials in {MaterialFolder}"
            );
        }

        /// <summary>
        /// Zone 1, the Waking Hollow, in the garden's key art: a clear, cool morning with a
        /// warm sun. Enough contrast that the lit play layer stands off the hazed distance;
        /// saturation barely lifted (the painted art carries its own colour); a faint warm
        /// filter, cool blue shadows and gold highlights; a soft warm bloom for the sun,
        /// lanterns and seed pods; a faint deep-blue vignette that frames without darkening.
        /// </summary>
        private static void BuildZone1(VolumeProfile profile)
        {
            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.05f);
            color.contrast.Override(14f);
            color.saturation.Override(4f);
            color.colorFilter.Override(new Color(1f, 0.985f, 0.96f, 1f));

            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.Override(new Color(0.42f, 0.50f, 0.62f, 1f));
            split.highlights.Override(new Color(0.62f, 0.56f, 0.44f, 1f));
            split.balance.Override(10f);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.94f, 0.85f, 1f));
            bloom.highQualityFiltering.Override(true);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(new Color(0.04f, 0.07f, 0.14f, 1f));
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// Zone 2, the Orchard: cooler and greener than zone 1, quiet, with the fruit's glow as
        /// a second light. No exposure lift; more contrast so the glowing fruit pops; a
        /// slightly cool filter, teal shadows and mint highlights weighted toward the shadows;
        /// a cyan-tinted bloom with a slightly lower threshold for the glow; a deeper teal
        /// vignette for the orchard's enclosed feel.
        /// </summary>
        private static void BuildZone2(VolumeProfile profile)
        {
            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(12f);
            color.saturation.Override(6f);
            color.colorFilter.Override(new Color(0.95f, 1f, 1f, 1f));

            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.Override(new Color(0.36f, 0.48f, 0.60f, 1f));
            split.highlights.Override(new Color(0.48f, 0.64f, 0.58f, 1f));
            split.balance.Override(-15f);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.78f);
            bloom.tint.Override(new Color(0.72f, 1f, 0.95f, 1f));
            bloom.highQualityFiltering.Override(true);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(new Color(0f, 0.05f, 0.07f, 1f));
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
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

        /// <summary>
        /// The profile's component of a type, added (and stored inside the profile asset,
        /// which VolumeProfile.Add alone does not do) the first time.
        /// </summary>
        private static T GetOrAdd<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>();
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            EditorUtility.SetDirty(component);
            return component;
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
