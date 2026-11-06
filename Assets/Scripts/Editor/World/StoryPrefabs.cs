using System.Collections.Generic;
using System.IO;
using Descender.UI;
using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.World
{
    /// <summary>
    /// Descender &gt; Setup &gt; Story prefabs: makes the three things the dialogue
    /// pipeline places, when they are missing: Assets/Prefabs/Story/StoryTrigger
    /// (an invisible area the hero's height), Assets/Prefabs/Story/Relic (the relic
    /// sprite, bobbing, with a prompt) and Assets/Prefabs/Npcs/Npc (a body and a
    /// prompt). An existing prefab is left as it is, except the NPC's list of bodies,
    /// which is generated: the first idle frame of every character under
    /// Assets/Art/Sprites. Safe to run again.
    /// </summary>
    public static class StoryPrefabs
    {
        public const string TriggerPath = "Assets/Prefabs/Story/StoryTrigger.prefab";
        public const string RelicPath = "Assets/Prefabs/Story/Relic.prefab";
        public const string NpcPath = "Assets/Prefabs/Npcs/Npc.prefab";
        public const string SpritesFolder = "Assets/Art/Sprites";
        public const string RelicSpritePath = SpritesFolder + "/relic.png";

        /// <summary>The relic's area and prompt height, as scripts/lore_item.gd has them, in level pixels.</summary>
        public static readonly Vector2 RelicArea = new Vector2(28f, 32f);
        public const float RelicPromptHeight = 30f;

        /// <summary>Behind the hero (10), level with the enemies.</summary>
        public const int BodySortingOrder = 5;

        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        [MenuItem("Descender/Setup/Story prefabs")]
        public static void Run()
        {
            var made = 0;
            made += Ensure(TriggerPath, BuildTrigger) ? 1 : 0;
            made += Ensure(RelicPath, BuildRelic) ? 1 : 0;
            made += Ensure(NpcPath, BuildNpc) ? 1 : 0;
            var bodies = RefreshBodies();
            AssetDatabase.SaveAssets();
            Debug.Log($"Descender: {made} story prefab(s) made; the NPC knows {bodies} character(s).");
        }

        /// <summary>Every character folder under Assets/Art/Sprites with an idle strip, and its first frame, in name order.</summary>
        public static List<Npc.Body> IdleFrames()
        {
            var found = new List<Npc.Body>();
            if (!AssetDatabase.IsValidFolder(SpritesFolder))
            {
                return found;
            }
            var folders = AssetDatabase.GetSubFolders(SpritesFolder);
            System.Array.Sort(folders, System.StringComparer.Ordinal);
            foreach (var folder in folders)
            {
                var character = Path.GetFileName(folder);
                Sprite first = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{folder}/{character}_idle.png"))
                {
                    // Frames are named <strip>_<n>; the shortest, then lowest, name is frame 0.
                    if (asset is Sprite frame && (first == null || Earlier(frame.name, first.name)))
                    {
                        first = frame;
                    }
                }
                if (first != null)
                {
                    found.Add(new Npc.Body { character = character, idle = first });
                }
            }
            return found;
        }

        private static bool Earlier(string a, string b)
        {
            return a.Length != b.Length ? a.Length < b.Length : string.CompareOrdinal(a, b) < 0;
        }

        private static bool Ensure(string path, System.Func<GameObject> build)
        {
            if (File.Exists(path))
            {
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets/Prefabs");
            AssetDatabase.Refresh();
            var root = build();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return true;
        }

        private static GameObject BuildTrigger()
        {
            var root = Root("StoryTrigger", Vector2.zero);
            var trigger = root.AddComponent<StoryTrigger>();
            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("automatic").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildRelic()
        {
            var root = Root("Relic", RelicArea);
            var body = new GameObject("Sprite");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, Relic.RestHeight / Ppu, 0f);
            var renderer = body.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RelicSpritePath);
            renderer.sortingOrder = BodySortingOrder;
            var relic = root.AddComponent<Relic>();
            var serialized = new SerializedObject(relic);
            serialized.FindProperty("areaSize").vector2Value = RelicArea;
            serialized.FindProperty("promptHeight").floatValue = RelicPromptHeight;
            serialized.FindProperty("body").objectReferenceValue = body.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildNpc()
        {
            var root = Root("Npc", Vector2.zero);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var renderer = body.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = BodySortingOrder;
            var npc = root.AddComponent<Npc>();
            var serialized = new SerializedObject(npc);
            serialized.FindProperty("sprite").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>An object on the Trigger layer with the area an Interactable makes in Awake, so the prefab shows it.</summary>
        private static GameObject Root(string name, Vector2 area)
        {
            var root = new GameObject(name) { layer = LayerMask.NameToLayer("Trigger") };
            var box = root.AddComponent<BoxCollider2D>();
            var size = area == Vector2.zero ? new Vector2(28f, 40f) : area;
            box.isTrigger = true;
            box.size = size / Ppu;
            box.offset = new Vector2(0f, size.y / 2f / Ppu);
            return root;
        }

        /// <summary>Writes the idle frames into the NPC prefab when they differ. Returns how many it lists.</summary>
        private static int RefreshBodies()
        {
            var frames = IdleFrames();
            var prefab = AssetDatabase.LoadAssetAtPath<Npc>(NpcPath);
            if (prefab == null)
            {
                return 0;
            }
            if (Same(prefab.Bodies, frames))
            {
                return frames.Count;
            }
            var root = PrefabUtility.LoadPrefabContents(NpcPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<Npc>());
                var bodies = serialized.FindProperty("bodies");
                bodies.arraySize = frames.Count;
                for (var i = 0; i < frames.Count; i++)
                {
                    var body = bodies.GetArrayElementAtIndex(i);
                    body.FindPropertyRelative("character").stringValue = frames[i].character;
                    body.FindPropertyRelative("idle").objectReferenceValue = frames[i].idle;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, NpcPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return frames.Count;
        }

        private static bool Same(IReadOnlyList<Npc.Body> have, List<Npc.Body> want)
        {
            if (have.Count != want.Count)
            {
                return false;
            }
            for (var i = 0; i < have.Count; i++)
            {
                if (have[i].character != want[i].character || have[i].idle != want[i].idle)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
