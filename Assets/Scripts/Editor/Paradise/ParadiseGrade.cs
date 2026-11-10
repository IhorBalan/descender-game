using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.EditorTools.Paradise
{
    /// <summary>
    /// The colour grade and vignette of each dressed Paradise zone: the one place their numbers
    /// live (docs/design/paradise.md, the table of zones). Zone 1 is dawn gold and rose, zone 2
    /// deep teal and cyan, and the difference between them is what makes crossing the border feel
    /// like arriving somewhere. White balance moves the whole picture, which keeps neutrals looking
    /// lit rather than tinted; split toning gives shadows and highlights their own hue; the vignette
    /// is the zone's darkest colour rather than black, so it frames the view without dirtying it.
    /// Every zone keeps the Neutral tonemapper, because a tonemapper cannot blend across a border,
    /// and Neutral only compresses what goes above 1 (the HDR glow of fruit, fungi and motes) and
    /// keeps the painted art's hues, where ACES would push gold toward orange. Bloom thresholds sit
    /// above 1 so only emissive things glow, never a merely bright sprite.
    /// </summary>
    public static class ParadiseGrade
    {
        private static readonly Dictionary<int, Look> Looks = new Dictionary<int, Look>
        {
            [1] = new Look
            {
                exposure = -0.05f,
                contrast = 16f,
                saturation = 6f,
                temperature = 8f,
                tint = 4f,
                shadows = new Color(0.55f, 0.44f, 0.6f),
                highlights = new Color(0.7f, 0.57f, 0.44f),
                balance = 10f,
                lift = new Vector4(1.01f, 0.985f, 1.01f, 0.007f),
                bloomThreshold = 1.1f,
                bloomIntensity = 0.36f,
                bloomTint = new Color(1f, 0.9f, 0.78f),
                vignette = new Color(0.14f, 0.07f, 0.16f),
                vignetteIntensity = 0.18f,
                vignetteSmoothness = 0.45f,
            },
            [2] = new Look
            {
                exposure = -0.2f,
                contrast = 18f,
                saturation = -2f,
                temperature = -7f,
                tint = -2f,
                greenFromBlue = 14f,
                shadows = new Color(0.33f, 0.52f, 0.6f),
                highlights = new Color(0.48f, 0.58f, 0.56f),
                balance = -15f,
                lift = new Vector4(0.98f, 1f, 1.02f, 0.012f),
                bloomThreshold = 1.05f,
                bloomIntensity = 0.45f,
                bloomTint = new Color(0.72f, 1f, 0.96f),
                vignette = new Color(0.02f, 0.11f, 0.15f),
                vignetteIntensity = 0.24f,
                vignetteSmoothness = 0.5f,
            },
        };

        /// <summary>Writes every zone's grade into its profile, without rebuilding any scene.</summary>
        [MenuItem("Descender/Paradise/Apply zone grades")]
        public static void ApplyAll()
        {
            foreach (var zone in Looks.Keys)
            {
                Apply(AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath(zone)), zone);
            }
            AssetDatabase.SaveAssets();
        }

        public static string ProfilePath(int zone) => $"{ParadiseLookSetup.PostFolder}/Paradise_Z{zone}.asset";

        /// <summary>Sets a zone's grade on its profile; a zone with no grade yet is left as it is.</summary>
        public static void Apply(VolumeProfile profile, int zone)
        {
            if (profile == null || !Looks.TryGetValue(zone, out var look))
            {
                return;
            }
            T Get<T>()
                where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var component))
                {
                    component = profile.Add<T>();
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                component.active = true;
                EditorUtility.SetDirty(component);
                return component;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);

            var balance = Get<WhiteBalance>();
            balance.temperature.Override(look.temperature);
            balance.tint.Override(look.tint);

            // Blue lends to green what green gives up, so blues turn teal, greys stay grey and
            // gold, with little blue in it, keeps its colour.
            var mixer = Get<ChannelMixer>();
            mixer.greenOutGreenIn.Override(100f - look.greenFromBlue);
            mixer.greenOutBlueIn.Override(look.greenFromBlue);

            var color = Get<ColorAdjustments>();
            color.postExposure.Override(look.exposure);
            color.contrast.Override(look.contrast);
            color.saturation.Override(look.saturation);
            color.colorFilter.Override(Color.white);

            var split = Get<SplitToning>();
            split.shadows.Override(look.shadows);
            split.highlights.Override(look.highlights);
            split.balance.Override(look.balance);

            // In light as bright as this no shadow is black: air fills them with the sky's colour.
            // In linear light a small lift barely touches the midtones and raises only the darkest.
            Get<LiftGammaGain>().lift.Override(look.lift);

            var bloom = Get<Bloom>();
            bloom.threshold.Override(look.bloomThreshold);
            bloom.intensity.Override(look.bloomIntensity);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(look.bloomTint);
            bloom.highQualityFiltering.Override(true);

            var vignette = Get<Vignette>();
            vignette.color.Override(look.vignette);
            vignette.intensity.Override(look.vignetteIntensity);
            vignette.smoothness.Override(look.vignetteSmoothness);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
        }

        private sealed class Look
        {
            public float exposure;
            public float contrast;
            public float saturation;
            public float temperature;
            public float tint;
            public float greenFromBlue;
            public Color shadows;
            public Color highlights;
            public float balance;
            public Vector4 lift;
            public float bloomThreshold;
            public float bloomIntensity;
            public Color bloomTint;
            public Color vignette;
            public float vignetteIntensity;
            public float vignetteSmoothness;
        }
    }
}
