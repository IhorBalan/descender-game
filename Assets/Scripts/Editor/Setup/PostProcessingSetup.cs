using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.EditorTools.Setup
{
    /// <summary>
    /// Descender &gt; Setup &gt; Post-processing: turns URP post-processing on for the new
    /// Paradise (docs/design/paradise.md). Gives the 2D renderer URP's PostProcessData
    /// (unused by a camera that does not render post-processing), sets the camera of
    /// ParadiseMain.unity to render it and puts a global Volume with a neutral profile
    /// there; the zones' own looks (bloom, grading, vignette) come from Volumes in their
    /// scenes. Main.unity, the prototype, is left alone. Safe to run again.
    /// </summary>
    public static class PostProcessingSetup
    {
        public const string RendererPath = "Assets/Settings/Descender 2D Renderer.asset";
        public const string PostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        public const string BaseProfilePath = "Assets/Settings/Post/Descender Base.asset";
        public const string MainScenePath = "Assets/Scenes/Paradise/ParadiseMain.unity";
        public const string VolumeName = "Global Volume";

        [MenuItem("Descender/Setup/Post-processing")]
        public static void Run()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PostProcessDataPath);
            if (renderer == null || data == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing {(renderer == null ? RendererPath : PostProcessDataPath)}."
                );
            }
            var serialized = new SerializedObject(renderer);
            var slot = serialized.FindProperty("m_PostProcessData");
            if (slot.objectReferenceValue != data)
            {
                slot.objectReferenceValue = data;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BaseProfilePath);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BaseProfilePath));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, BaseProfilePath);
            }

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                throw new System.InvalidOperationException("Main.unity has no camera.");
            }
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cameraData);

            Volume volume = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            {
                if (v.isGlobal && v.name == VolumeName)
                {
                    volume = v;
                }
            }
            if (volume == null)
            {
                volume = new GameObject(VolumeName).AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "PostProcessingSetup: renderer has PostProcessData, ParadiseMain camera renders post-processing, Global Volume uses "
                    + BaseProfilePath
            );
        }
    }
}
