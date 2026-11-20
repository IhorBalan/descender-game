using UnityEngine;

namespace Descender.Motion
{
    /// <summary>
    /// The crowned knight's painted run on a <see cref="RunSkeleton"/>: eight drawn frames
    /// (tools/hero_knight/run_frames.py cuts them), one for each of the run's key poses,
    /// each shown for the eighth of the cycle about its key. The frames follow the
    /// skeleton's phase and ride on its root, so they run at its speed; at the lab's stride
    /// the planted foot keeps to the ground.
    /// </summary>
    public sealed class RunFrames : MonoBehaviour
    {
        /// <summary>Drawn over the skeleton's shapes and the knight's parts.</summary>
        public const int Order = 40;

        [SerializeField]
        private RunSkeleton skeleton;

        [SerializeField]
        [Tooltip("The run's frames, one for each key pose, from the right leg's contact.")]
        private Sprite[] frames = new Sprite[0];

        private SpriteRenderer view;
        private bool shown = true;

        /// <summary>How many frames the run has.</summary>
        public int Count => frames.Length;

        /// <summary>The frame shown now.</summary>
        public int Frame { get; private set; }

        /// <summary>The renderer the frames are drawn with (null until it is made).</summary>
        public SpriteRenderer View => view;

        /// <summary>Whether the painted run is shown.</summary>
        public bool Shown
        {
            get => shown;
            set
            {
                shown = value;
                if (view != null)
                {
                    view.enabled = value;
                }
            }
        }

        /// <summary>Fills in the skeleton and the frames (the editor's scene maker).</summary>
        public void Configure(RunSkeleton target, Sprite[] list)
        {
            skeleton = target;
            frames = list;
        }

        /// <summary>The frame for a phase of the cycle: the key pose nearest it.</summary>
        public static int FrameAt(float phase, int count)
        {
            var at = Mathf.FloorToInt((phase - Mathf.Floor(phase)) * count + 0.5f);
            return at % count;
        }

        private void Awake() => Make();

        private void LateUpdate() => Show();

        /// <summary>Puts up the frame for the skeleton's phase now.</summary>
        public void Show()
        {
            Make();
            if (skeleton == null || frames.Length == 0)
            {
                return;
            }
            Frame = FrameAt(skeleton.Phase, frames.Length);
            view.sprite = frames[Frame];
        }

        private void Make()
        {
            if (view != null)
            {
                return;
            }
            var holder = new GameObject("Painted run");
            holder.transform.SetParent(skeleton != null ? skeleton.transform : transform, false);
            view = holder.AddComponent<SpriteRenderer>();
            view.sharedMaterial = Fx.Shapes.Unlit;
            view.sortingOrder = Order;
            view.enabled = shown;
        }
    }
}
