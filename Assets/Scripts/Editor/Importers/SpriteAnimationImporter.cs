using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Descender.EditorTools.Importers
{
    /// <summary>
    /// Descender > Import > Sprite animations: for every character in
    /// Assets/Art/Sprites/sprites.json, an AnimationClip per strip (the frames the
    /// strip's .meta slices, at the spec's fps, looping as it says) under
    /// Assets/Art/Sprites/&lt;name&gt;/Animations, and an AnimatorController with one
    /// state per clip, so code plays an animation by name as the Godot game did.
    /// The strips themselves are sliced by tools/unity/import_godot_assets.py, which
    /// writes their .meta from the same spec.
    /// </summary>
    public static class SpriteAnimationImporter
    {
        public const string SpritesFolder = "Assets/Art/Sprites";

        [MenuItem("Descender/Import/Sprite animations")]
        public static void ImportAll()
        {
            var spec = JObject.Parse(File.ReadAllText(Path.Combine(SpritesFolder, "sprites.json")));
            var count = 0;
            foreach (var character in spec.Properties())
            {
                count += ImportCharacter(character.Name, (JObject)character.Value);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Descender: {count} animation clips written.");
        }

        public static int ImportCharacter(string name, JObject info)
        {
            var folder = $"{SpritesFolder}/{name}";
            var animFolder = $"{folder}/Animations";
            if (!AssetDatabase.IsValidFolder(animFolder))
            {
                AssetDatabase.CreateFolder(folder, "Animations");
            }
            var controllerPath = $"{animFolder}/{name}.controller";
            var controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var count = 0;
            foreach (var animation in ((JObject)info["animations"]).Properties())
            {
                var anim = (JObject)animation.Value;
                var strip = $"{folder}/{anim.Value<string>("file")}";
                var frames = new List<Sprite>();
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(strip))
                {
                    if (asset is Sprite sprite)
                    {
                        frames.Add(sprite);
                    }
                }
                frames.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
                if (frames.Count == 0)
                {
                    Debug.LogWarning($"{name}/{animation.Name}: no sprites in {strip}; is the strip sliced?");
                    continue;
                }
                var clipPath = $"{animFolder}/{name}_{animation.Name}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                Fill(clip, frames, anim.Value<float?>("fps") ?? 8f, anim.Value<bool?>("loop") ?? false);
                EnsureState(machine, animation.Name, clip);
                count++;
            }
            EditorUtility.SetDirty(controller);
            return count;
        }

        private static int FrameIndex(string spriteName)
        {
            var underscore = spriteName.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out var index) ? index : 0;
        }

        /// <summary>Writes the sprite keys: one frame every 1/fps seconds, the last held for a frame.</summary>
        public static void Fill(AnimationClip clip, List<Sprite> frames, float fps, bool loop)
        {
            clip.frameRate = Mathf.Max(fps, 1f);
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite",
            };
            var keys = new ObjectReferenceKeyframe[frames.Count];
            for (var i = 0; i < frames.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = frames.Count / clip.frameRate;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static void EnsureState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            foreach (var child in machine.states)
            {
                if (child.state.name == name)
                {
                    child.state.motion = clip;
                    return;
                }
            }
            var state = machine.AddState(name);
            state.motion = clip;
            if (name == "idle")
            {
                machine.defaultState = state;
            }
        }
    }
}
