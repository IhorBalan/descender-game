using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.EditorTools.Setup
{
    /// <summary>
    /// One-time project setup, from the menu Descender > Setup > Render pipeline
    /// and settings: makes the URP asset with a 2D Renderer, assigns it to Graphics
    /// and Quality, and sets the player and physics settings the game depends on
    /// (320x180 at integer scale, 60 Hz physics, the 2D gravity in level pixels,
    /// the collision layers). Safe to run again: existing assets are reused.
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string SettingsFolder = "Assets/Settings";
        public const string RendererPath = SettingsFolder + "/Descender 2D Renderer.asset";
        public const string PipelinePath = SettingsFolder + "/Descender URP.asset";

        /// <summary>One unit is one tile of 16 level pixels; the hero's numbers are in pixels.</summary>
        public const float PixelsPerUnit = 16f;

        public static readonly string[] Layers =
        {
            "World",
            "Player",
            "Enemy",
            "LightBarrier",
            "Ledge",
            "Hazard",
            "Pickup",
            "Trigger",
        };

        [MenuItem("Descender/Setup/Render pipeline and settings")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsFolder);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            // 60 physics ticks a second, interpolated bodies, gravity in level pixels per unit.
            Time.fixedDeltaTime = 1f / 60f;
            Physics2D.gravity = new Vector2(0f, -711f / PixelsPerUnit); // 2 * 72 / 0.45^2, the hero's rise gravity
            Physics2D.queriesHitTriggers = false;

            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.productName = "Descender";
            PlayerSettings.companyName = "Descender";

            AddLayers();
            AssetDatabase.SaveAssets();
            Debug.Log("Descender: render pipeline, physics, player settings and layers are set.");
        }

        private static void AddLayers()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
            );
            var layers = tagManager.FindProperty("layers");
            foreach (var wanted in Layers)
            {
                var found = false;
                for (var i = 8; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).stringValue == wanted)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    continue;
                }
                for (var i = 8; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = wanted;
                        break;
                    }
                }
            }
            tagManager.ApplyModifiedProperties();
        }
    }
}
