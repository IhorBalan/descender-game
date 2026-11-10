using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Copies of Ansel's body, drawn in another material: the afterimages of HeroGhosts and the
    /// outline of HeroLight. A copy is a clone of the rig made under an inactive holder (so
    /// nothing on it wakes) with the rig's own behaviours taken off and everything that is
    /// not his body (effects, lights, his shadow, other copies) switched off. Its bones line
    /// up, one for one, with the rig's transforms at the time it was made, so Pose can copy
    /// the live pose across.
    /// </summary>
    public static class RigCopy
    {
        /// <summary>Children of the rig that are not part of his body, left out of the copies.</summary>
        public static readonly string[] NotBody = { "Fx", "RimLight", "SlashArc", "Shockwave", "Shadow", Outline };

        /// <summary>The name of HeroLight's outline copy, which lives under the rig.</summary>
        public const string Outline = "Silhouette";

        public sealed class Copy
        {
            public GameObject root;
            public Transform[] bones;
            public SpriteRenderer[] renderers;
        }

        /// <summary>
        /// An inactive copy of the rig's body, unparented, every part drawn with the material at
        /// the order.
        /// </summary>
        public static Copy Make(GameObject rig, string name, Material material, int order)
        {
            var holder = new GameObject(name + "Holder");
            holder.SetActive(false);
            var copy = Object.Instantiate(rig, holder.transform);
            copy.name = name;
            var bones = copy.GetComponentsInChildren<Transform>(true);
            foreach (var behaviour in copy.GetComponents<MonoBehaviour>())
            {
                Object.DestroyImmediate(behaviour);
            }
            // From 1: the copy's own root may carry a left-out name (the outline does).
            for (var i = 1; i < bones.Length; i++)
            {
                if (System.Array.IndexOf(NotBody, bones[i].name) >= 0)
                {
                    bones[i].gameObject.SetActive(false);
                }
            }
            var renderers = new List<SpriteRenderer>();
            foreach (var r in copy.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!IsBody(r.transform, copy.transform))
                {
                    continue;
                }
                renderers.Add(r);
                r.sharedMaterial = material;
                r.sortingOrder = order;
            }
            copy.SetActive(false);
            copy.transform.SetParent(null, false);
            Object.Destroy(holder);
            return new Copy
            {
                root = copy,
                bones = bones,
                renderers = renderers.ToArray(),
            };
        }

        /// <summary>Gives the copy the pose of the live rig (every bone below the root).</summary>
        public static void Pose(Copy copy, Transform[] live)
        {
            for (var i = 1; i < live.Length && i < copy.bones.Length; i++)
            {
                live[i].GetLocalPositionAndRotation(out var position, out var rotation);
                copy.bones[i].SetLocalPositionAndRotation(position, rotation);
                copy.bones[i].localScale = live[i].localScale;
            }
        }

        /// <summary>Whether the part belongs to his body: no ancestor below the root is left out.</summary>
        public static bool IsBody(Transform part, Transform root)
        {
            for (var t = part; t != null && t != root; t = t.parent)
            {
                if (System.Array.IndexOf(NotBody, t.name) >= 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
