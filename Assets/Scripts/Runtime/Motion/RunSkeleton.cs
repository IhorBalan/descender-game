using System;
using System.Collections.Generic;
using UnityEngine;

namespace Descender.Motion
{
    /// <summary>
    /// A procedural 2D humanoid that runs: a skeleton of bones (hips, spine, chest, neck,
    /// head; shoulders, upper and lower arms and hands; thighs, shins and feet, each side)
    /// built in code under this object, drawn with plain placeholder shapes, and posed
    /// every frame from <see cref="RunGait"/>. This object is the root: it travels along
    /// the ground at the running speed while the phase goes round the cycle, both from the
    /// same time, so a foot on the ground stays where it came down. The run's numbers and
    /// the body's lengths are edited in the inspector, also while it plays. It stands in the
    /// CharacterAnimationLab scene (<see cref="CharacterAnimationLab"/>) and touches nothing
    /// of the game. The figure is seen from its left: the left limbs nearer, the right
    /// ones behind and darker.
    /// </summary>
    public sealed class RunSkeleton : MonoBehaviour
    {
        /// <summary>How far the root may travel before it and everything with it are moved back, so numbers stay small.</summary>
        public const float RecentreDistance = 1000f;

        /// <summary>The share of the upper arm's swing the shoulder (and its pauldron) turns with.</summary>
        public const float ShoulderFollow = 0.3f;

        // Each side's bone names, built once so posing allocates nothing: [0] the left (near), [1] the right.
        private static readonly string[] Shoulder = { "Shoulder.L", "Shoulder.R" };
        private static readonly string[] UpperArm = { "UpperArm.L", "UpperArm.R" };
        private static readonly string[] LowerArm = { "LowerArm.L", "LowerArm.R" };
        private static readonly string[] Hand = { "Hand.L", "Hand.R" };
        private static readonly string[] Thigh = { "Thigh.L", "Thigh.R" };
        private static readonly string[] Shin = { "Shin.L", "Shin.R" };
        private static readonly string[] Foot = { "Foot.L", "Foot.R" };

        private static readonly Color Near = new Color(0.86f, 0.74f, 0.58f);
        private static readonly Color Far = new Color(0.52f, 0.44f, 0.36f);
        private static readonly Color Shirt = new Color(0.33f, 0.45f, 0.72f);
        private static readonly Color Trousers = new Color(0.25f, 0.27f, 0.36f);
        private static readonly Color TrousersFar = new Color(0.17f, 0.18f, 0.25f);

        [SerializeField]
        private RunGaitSettings settings = new RunGaitSettings();

        [SerializeField]
        private RunBody body = new RunBody();

        [SerializeField]
        [Tooltip("Where in the cycle it starts (0 is the right foot's contact).")]
        [Range(0f, 1f)]
        private float startPhase;

        private readonly RunPose pose = new RunPose();
        private readonly List<Transform> bones = new List<Transform>(24);
        private readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>(24);
        private readonly Dictionary<Transform, Piece> pieces = new Dictionary<Transform, Piece>(24);
        private readonly Dictionary<Transform, float> lengths = new Dictionary<Transform, float>(24);
        private bool shapesShown = true;
        private float phase;
        private float travelled;
        private Vector3 start;
        private bool built;

        /// <summary>Raised when the root is moved back by this many units (see <see cref="RecentreDistance"/>).</summary>
        public event Action<float> Recentred;

        public RunGaitSettings Settings => settings;
        public RunBody Body => body;

        /// <summary>The body this frame, relative to the root.</summary>
        public RunPose Pose => pose;

        /// <summary>0 .. 1 through the cycle.</summary>
        public float Phase => phase;

        /// <summary>The bones, parents before their children.</summary>
        public IReadOnlyList<Transform> Bones => bones;

        /// <summary>Whether time stands still (the lab's pause); <see cref="Advance"/> still steps it.</summary>
        public bool Paused { get; set; }

        /// <summary>How fast the run plays: 1 is real time.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>A bone by name ("Thigh.L", "Head" ...), or null.</summary>
        public Transform Bone(string name) => byName.TryGetValue(name, out var bone) ? bone : null;

        /// <summary>Gives it a body's lengths before it is built (the editor's scene maker).</summary>
        public void SetBody(RunBody value) => body = value;

        /// <summary>How long a bone is this frame: from its joint to the next.</summary>
        public float LengthOf(Transform bone) => lengths.TryGetValue(bone, out var length) ? length : 0f;

        /// <summary>Whether the placeholder shapes are drawn (off when something wears the skeleton).</summary>
        public bool ShapesShown
        {
            get => shapesShown;
            set
            {
                shapesShown = value;
                foreach (var piece in pieces.Values)
                {
                    piece.Renderer.enabled = value;
                }
            }
        }

        /// <summary>A point of the pose (relative to the root) in the world.</summary>
        public Vector3 World(Vector2 local) => transform.position + (Vector3)local;

        private void Awake()
        {
            settings.Resolve();
            start = transform.position;
            phase = startPhase;
            Build();
            Apply();
        }

        private void Update()
        {
            if (!Paused)
            {
                Advance(Time.deltaTime * TimeScale);
            }
        }

        private void OnValidate() => settings?.Resolve();

        /// <summary>Builds the skeleton if it is not yet and poses it at `at` of the cycle, where it stands (the editor's pictures).</summary>
        public void ShowAt(float at)
        {
            settings.Resolve();
            if (!built)
            {
                start = transform.position;
                Build();
            }
            phase = Mathf.Repeat(at, 1f);
            Apply();
        }

        /// <summary>Moves the run on by `seconds`: the phase round the cycle and the root along the ground, together.</summary>
        public void Advance(float seconds)
        {
            settings.Resolve();
            if (seconds > 0f)
            {
                phase = Mathf.Repeat(phase + seconds / settings.cycleDuration, 1f);
                travelled += settings.runningSpeed * seconds;
                if (travelled > RecentreDistance)
                {
                    travelled -= RecentreDistance;
                    Recentred?.Invoke(RecentreDistance);
                }
            }
            Apply();
        }

        private void Apply()
        {
            if (!built)
            {
                return;
            }
            transform.position = start + new Vector3(travelled, 0f, 0f);
            RunGait.Evaluate(phase, settings, body, pose);
            var spine =
                Vector2.Lerp(pose.hips, pose.chest, 0.5f)
                + RunGait.Rotate(pose.chest - pose.hips, -90f).normalized * 0.02f;
            Place("Hips", pose.hips, pose.chest);
            Place("Spine", pose.hips, spine);
            Place("Chest", spine, pose.chest);
            Place("Neck", pose.chest, pose.neck);
            Place("Head", pose.neck, pose.head + (pose.head - pose.neck));
            PlaceArm(0, pose.leftArm);
            PlaceArm(1, pose.rightArm);
            PlaceLeg(0, pose.leftLeg);
            PlaceLeg(1, pose.rightLeg);
        }

        /// <summary>
        /// The shoulder: on the arm's joint, square to the spine and pointing back, turned
        /// with the torso and a share (<see cref="ShoulderFollow"/>) of the upper arm's swing,
        /// as a pauldron strapped to the arm is. Not from the chest to the joint: that short
        /// line spins as the shoulders turn, and what is worn on it would spin with it.
        /// </summary>
        private void PlaceArm(int side, RunLimb arm)
        {
            var spine = (pose.chest - pose.hips).normalized;
            var swing = Vector2.SignedAngle(-spine, arm.middle - arm.root);
            var back = RunGait.Rotate(spine, 90f + swing * ShoulderFollow);
            Place(Shoulder[side], arm.root, arm.root + back * Vector2.Distance(pose.chest, arm.root));
            Place(UpperArm[side], arm.root, arm.middle);
            Place(LowerArm[side], arm.middle, arm.end);
            Place(Hand[side], arm.end, arm.tip);
        }

        private void PlaceLeg(int side, RunLimb leg)
        {
            Place(Thigh[side], leg.root, leg.middle);
            Place(Shin[side], leg.middle, leg.end);
            Place(Foot[side], leg.end, leg.tip);
        }

        /// <summary>Puts a bone at `from`, pointing (its +x) at `to`, and stretches its shape to the length between.</summary>
        private void Place(string name, Vector2 from, Vector2 to)
        {
            var bone = byName[name];
            var along = to - from;
            bone.SetPositionAndRotation(
                World(from),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg)
            );
            lengths[bone] = along.magnitude;
            if (pieces.TryGetValue(bone, out var piece) && piece.Stretch)
            {
                piece.Renderer.transform.localScale = new Vector3(
                    along.magnitude,
                    piece.Thickness / Capsule.Thickness,
                    1f
                );
            }
        }

        private void Build()
        {
            if (built)
            {
                return;
            }
            built = true;
            var hips = Make("Hips", transform, Trousers, 0.34f, 3, round: true);
            var spine = Make("Spine", hips, Shirt, 0.36f, 3);
            var chest = Make("Chest", spine, Shirt, 0.38f, 3);
            var neck = Make("Neck", chest, Near, 0.1f, 3);
            Make("Head", neck, Near, body.head * 2f, 5, round: true);
            for (var side = 0; side < 2; side++)
            {
                var near = side == 0;
                var skin = near ? Near : Far;
                var cloth = near ? Shirt : Shirt * 0.7f;
                var legs = near ? Trousers : TrousersFar;
                var armOrder = near ? 6 : 0;
                var legOrder = near ? 4 : 1;
                var shoulder = Make(Shoulder[side], chest, cloth, 0.14f, armOrder);
                var upperArm = Make(UpperArm[side], shoulder, cloth, 0.12f, armOrder);
                var lowerArm = Make(LowerArm[side], upperArm, skin, 0.1f, armOrder);
                Make(Hand[side], lowerArm, skin, 0.1f, armOrder);
                var thigh = Make(Thigh[side], hips, legs, 0.17f, legOrder);
                var shin = Make(Shin[side], thigh, legs, 0.13f, legOrder);
                Make(
                    Foot[side],
                    shin,
                    near ? new Color(0.3f, 0.2f, 0.15f) : new Color(0.2f, 0.13f, 0.1f),
                    0.09f,
                    legOrder
                );
            }
        }

        private Transform Make(
            string name,
            Transform parent,
            Color color,
            float thickness,
            int order,
            bool round = false
        )
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            var shape = new GameObject("Shape");
            shape.transform.SetParent(bone, false);
            var renderer = shape.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = Fx.Shapes.Unlit;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.enabled = shapesShown;
            if (round)
            {
                // A disc (the head, the pelvis) sits a radius along the bone, not stretched.
                renderer.sprite = Fx.Shapes.Disc;
                shape.transform.localPosition = new Vector3(thickness * 0.5f, 0f, 0f);
                shape.transform.localScale = new Vector3(thickness, thickness, 1f);
            }
            else
            {
                renderer.sprite = Capsule.Sprite;
            }
            pieces[bone] = new Piece(renderer, thickness, !round);
            bones.Add(bone);
            byName[name] = bone;
            return bone;
        }

        private readonly struct Piece
        {
            public readonly SpriteRenderer Renderer;
            public readonly float Thickness;
            public readonly bool Stretch;

            public Piece(SpriteRenderer renderer, float thickness, bool stretch)
            {
                Renderer = renderer;
                Thickness = thickness;
                Stretch = stretch;
            }
        }
    }

    /// <summary>A white capsule sprite one unit long from its pivot (its left end's middle), made once in code.</summary>
    public static class Capsule
    {
        /// <summary>The capsule's thickness in units at scale 1.</summary>
        public const float Thickness = 0.25f;

        private const int Long = 64;
        private const int Wide = 16;
        private static Sprite sprite;

        public static Sprite Sprite
        {
            get
            {
                if (sprite == null)
                {
                    var texture = new Texture2D(Long, Wide, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                    };
                    var pixels = new Color32[Long * Wide];
                    var radius = Wide / 2f;
                    for (var y = 0; y < Wide; y++)
                    {
                        for (var x = 0; x < Long; x++)
                        {
                            var cx = Mathf.Clamp(x + 0.5f, radius, Long - radius);
                            var dx = x + 0.5f - cx;
                            var dy = y + 0.5f - radius;
                            var inside = dx * dx + dy * dy <= radius * radius;
                            pixels[y * Long + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                        }
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    sprite = Sprite.Create(texture, new Rect(0, 0, Long, Wide), new Vector2(0f, 0.5f), Long);
                }
                return sprite;
            }
        }
    }
}
