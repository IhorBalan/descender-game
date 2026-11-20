using System;
using System.Collections.Generic;
using UnityEngine;

namespace Descender.Motion
{
    /// <summary>One part of a skin: a sprite worn on a bone of a <see cref="RunSkeleton"/>.</summary>
    [Serializable]
    public sealed class RunPart
    {
        public string name;
        public Sprite sprite;

        [Tooltip(
            "The bone it is worn on; a limb named without its side (Thigh) is worn on both, the far (.R) darker and behind."
        )]
        public string bone;

        [Tooltip("The joint it hangs from, in the sprite's pixels from its bottom left (its pivot).")]
        public Vector2 joint;

        [Tooltip("For a limb, the next joint; for a rigid part, a point along its bone's direction as drawn.")]
        public Vector2 end;

        [Tooltip("Keeps its size and only turns with its bone; a limb is stretched to its bone's length.")]
        public bool rigid;

        public int order;

        [Tooltip("Degrees a hanging part swings each step.")]
        public float sway;

        [Tooltip(
            "Held at a steady angle in the world (degrees, 0 forward, 90 up) rather than laid along its bone: a sword carried low."
        )]
        public bool steady;

        [Tooltip("The size it is worn at against the rest (a rigid part only).")]
        [Min(0.01f)]
        public float scale = 1f;

        public float worldAngle;

        [Tooltip("For a steady part, how much of its bone's turning it still follows (0 none, 1 all).")]
        [Range(0f, 1f)]
        public float follow;
    }

    /// <summary>
    /// Dresses a <see cref="RunSkeleton"/> in the crowned knight's parts (cut from his
    /// standing frame by tools/hero_knight/parts.py into Assets/Art/Sprites/hero/parts):
    /// each part hangs from its bone's joint and lies along the bone, a limb stretched to
    /// the bone's length, a rigid part (torso, head, cloak, tabard, sword) turning with it;
    /// the cloak and tabard swing a little with each step. The limbs of the near side are
    /// worn on the far side too, darker and behind. The skeleton's placeholder shapes are
    /// hidden while it is worn. In the CharacterAnimationLab, K takes it off and puts it on.
    /// </summary>
    public sealed class RunKnightSkin : MonoBehaviour
    {
        /// <summary>How dark the far side's parts are.</summary>
        public const float FarShade = 0.55f;

        /// <summary>How far behind the near parts the far side's are drawn.</summary>
        public const int FarBehind = 14;

        [SerializeField]
        private RunSkeleton skeleton;

        [SerializeField]
        private List<RunPart> parts = new List<RunPart>();

        private readonly List<Worn> worn = new List<Worn>(24);
        private bool shown = true;

        /// <summary>How many sprites are worn (a part on both sides counts twice).</summary>
        public int Count => worn.Count;

        public IReadOnlyList<RunPart> Parts => parts;

        /// <summary>Whether the knight is worn (else the placeholder shapes show).</summary>
        public bool Shown
        {
            get => shown;
            set
            {
                shown = value;
                foreach (var part in worn)
                {
                    part.Renderer.enabled = value;
                }
                if (skeleton != null)
                {
                    skeleton.ShapesShown = !value;
                }
            }
        }

        /// <summary>Fills in the skeleton and the parts (the editor's scene maker).</summary>
        public void Configure(RunSkeleton target, List<RunPart> list)
        {
            skeleton = target;
            parts = list;
        }

        /// <summary>The sprite worn on a bone for a part, or null.</summary>
        public SpriteRenderer WornOn(string part, string bone)
        {
            foreach (var item in worn)
            {
                if (item.Part.name == part && item.Bone.name == bone)
                {
                    return item.Renderer;
                }
            }
            return null;
        }

        private void Start()
        {
            Dress();
            Fit();
        }

        private void LateUpdate() => Fit();

        /// <summary>Hangs every part on its bones, once; the skeleton must be built.</summary>
        public void Dress()
        {
            if (worn.Count > 0 || skeleton == null)
            {
                return;
            }
            foreach (var part in parts)
            {
                if (part == null || part.sprite == null)
                {
                    continue;
                }
                if (skeleton.Bone(part.bone) != null)
                {
                    Hang(part, part.bone, part.order, false);
                }
                else
                {
                    Hang(part, part.bone + ".L", part.order, false);
                    Hang(part, part.bone + ".R", part.order - FarBehind, true);
                }
            }
            // Shown, but a hidden knight leaves the shapes as they are (the painted run may be up).
            foreach (var part in worn)
            {
                part.Renderer.enabled = shown;
            }
            if (shown && skeleton != null)
            {
                skeleton.ShapesShown = false;
            }
        }

        /// <summary>Stretches each limb to its bone and swings what hangs; called every frame.</summary>
        public void Fit()
        {
            var step = skeleton != null ? Mathf.Sin(skeleton.Phase * Mathf.PI * 4f) : 0f;
            foreach (var part in worn)
            {
                if (!part.Part.rigid && part.Length > 1e-5f)
                {
                    part.Fit.localScale = new Vector3(skeleton.LengthOf(part.Bone) / part.Length, 1f, 1f);
                }
                if (part.Part.sway != 0f)
                {
                    part.Fit.localRotation = Quaternion.Euler(0f, 0f, part.Part.sway * step);
                }
                if (part.Part.steady)
                {
                    // The part's own line (joint to end as drawn) put at the world angle, turned a little with its bone.
                    var swing = Mathf.DeltaAngle(-90f, part.Bone.eulerAngles.z); // from hanging straight down
                    var angle = part.Part.worldAngle + part.Part.follow * swing - part.DrawnAngle;
                    part.Fit.rotation = Quaternion.Euler(0f, 0f, angle);
                    part.Art.localRotation = Quaternion.identity;
                }
            }
        }

        private void Hang(RunPart part, string boneName, int order, bool far)
        {
            var bone = skeleton.Bone(boneName);
            if (bone == null)
            {
                return;
            }
            var fit = new GameObject("Wears " + part.name).transform;
            fit.SetParent(bone, false);
            var art = new GameObject(part.name);
            art.transform.SetParent(fit, false);
            var along = part.end - part.joint;
            art.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            if (part.rigid)
            {
                art.transform.localScale = new Vector3(part.scale, part.scale, 1f);
            }
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = part.sprite;
            renderer.sharedMaterial = Fx.Shapes.Unlit;
            renderer.sortingOrder = order;
            renderer.color = far ? new Color(FarShade, FarShade, FarShade * 1.08f) : Color.white;
            worn.Add(
                new Worn(
                    part,
                    bone,
                    fit,
                    art.transform,
                    renderer,
                    along.magnitude / part.sprite.pixelsPerUnit,
                    Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg
                )
            );
        }

        private readonly struct Worn
        {
            public readonly RunPart Part;
            public readonly Transform Bone;
            public readonly Transform Fit;
            public readonly Transform Art;
            public readonly SpriteRenderer Renderer;

            /// <summary>The part's joint to its end, in units.</summary>
            public readonly float Length;

            /// <summary>The angle of the part's joint to its end as drawn, in degrees.</summary>
            public readonly float DrawnAngle;

            public Worn(
                RunPart part,
                Transform bone,
                Transform fit,
                Transform art,
                SpriteRenderer renderer,
                float length,
                float drawnAngle
            )
            {
                Part = part;
                Bone = bone;
                Fit = fit;
                Art = art;
                Renderer = renderer;
                Length = length;
                DrawnAngle = drawnAngle;
            }
        }
    }
}
