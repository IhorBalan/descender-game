using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.Motion
{
    /// <summary>
    /// The CharacterAnimationLab scene's own behaviour (Assets/Scenes/Sandbox/
    /// CharacterAnimationLab.unity, made by Descender &gt; Lab &gt; Character animation lab):
    /// a ground with a mark every unit, a camera that keeps the <see cref="RunSkeleton"/>
    /// in view, the <see cref="RunDebugOverlay"/> and the keys to look closer. Tab shows or
    /// hides the overlay, K goes from the painted run (<see cref="RunFrames"/>) to the knight's
    /// parts on the bones (<see cref="RunKnightSkin"/>) to the bare bones and back, Space
    /// pauses, the right arrow steps one sixtieth of a second while paused, 1, 2 and 3 play at full, a quarter and a tenth speed. It runs at 60 frames a
    /// second. Nothing of the game is in the scene or touched by it.
    /// </summary>
    public sealed class CharacterAnimationLab : MonoBehaviour
    {
        /// <summary>What the runner looks like: K goes from one to the next.</summary>
        public enum Look
        {
            Painted,
            Parts,
            Bones,
        }

        public const float Step = 1f / 60f;
        private const int Ticks = 48;

        private static readonly Color Earth = new Color(0.2f, 0.22f, 0.26f);
        private static readonly Color Tick = new Color(0.55f, 0.6f, 0.7f);

        [SerializeField]
        private RunSkeleton skeleton;

        [SerializeField]
        private RunDebugOverlay overlay;

        [SerializeField]
        private RunKnightSkin skin;

        [SerializeField]
        private RunFrames painted;

        [SerializeField]
        private new Camera camera;

        [SerializeField]
        [Tooltip("Where the camera sits from the runner's root.")]
        private Vector2 cameraOffset = new Vector2(0.6f, 1.3f);

        private Transform ground;
        private Look look;
        private readonly Transform[] ticks = new Transform[Ticks];

        public RunSkeleton Skeleton => skeleton;
        public RunDebugOverlay Overlay => overlay;
        public RunKnightSkin Skin => skin;
        public RunFrames Painted => painted;

        /// <summary>What the runner looks like now.</summary>
        public Look Current => look;

        /// <summary>Fills in what the scene is made of (the editor's scene maker and tests).</summary>
        public void Configure(
            RunSkeleton runner,
            RunDebugOverlay debug,
            Camera view,
            RunKnightSkin knight = null,
            RunFrames run = null
        )
        {
            skeleton = runner;
            overlay = debug;
            camera = view;
            skin = knight;
            painted = run;
        }

        /// <summary>Shows the runner one way: the painted run, the knight's parts or the bones.</summary>
        public void Show(Look next)
        {
            look = next;
            if (look == Look.Painted && painted == null)
            {
                look = Look.Parts;
            }
            if (look == Look.Parts && skin == null)
            {
                look = Look.Bones;
            }
            if (painted != null)
            {
                painted.Shown = look == Look.Painted;
            }
            if (skin != null)
            {
                skin.Shown = look == Look.Parts;
            }
            if (skeleton != null)
            {
                skeleton.ShapesShown = look == Look.Bones;
            }
        }

        /// <summary>The next look after this one, round again after the bones.</summary>
        public void ShowNext() => Show(look == Look.Bones ? Look.Painted : look + 1);

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (overlay != null && skeleton != null)
            {
                overlay.Bind(skeleton);
                // With the knight on he is what is seen first; Tab brings the bones back.
                overlay.Visible = skin == null && painted == null;
            }
            ground = Fx.Shapes.Block(transform, "Ground", new Vector2(64f * 16f, 16f), Vector2.zero, Earth).transform;
            for (var i = 0; i < Ticks; i++)
            {
                var mark = Fx.Shapes.Block(transform, "Mark", new Vector2(1f, 3f), Vector2.zero, Tick);
                mark.sortingOrder = 1;
                ticks[i] = mark.transform;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || skeleton == null)
            {
                return;
            }
            if (keyboard.tabKey.wasPressedThisFrame && overlay != null)
            {
                overlay.Visible = !overlay.Visible;
            }
            if (keyboard.kKey.wasPressedThisFrame)
            {
                ShowNext();
            }
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                skeleton.Paused = !skeleton.Paused;
            }
            if (skeleton.Paused && keyboard.rightArrowKey.wasPressedThisFrame)
            {
                skeleton.Advance(Step);
            }
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                skeleton.TimeScale = 1f;
            }
            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                skeleton.TimeScale = 0.25f;
            }
            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                skeleton.TimeScale = 0.1f;
            }
        }

        private void Start() => Show(Look.Painted);

        private void LateUpdate()
        {
            if (skeleton == null)
            {
                return;
            }
            var root = skeleton.transform.position;
            if (camera != null)
            {
                camera.transform.position = new Vector3(
                    root.x + cameraOffset.x,
                    root.y + cameraOffset.y,
                    camera.transform.position.z
                );
            }
            // The ground stands still in the world: it is put back a whole unit at a time under the runner.
            var under = Mathf.Floor(root.x);
            ground.position = new Vector3(under, root.y - 0.5f, 0f);
            for (var i = 0; i < Ticks; i++)
            {
                ticks[i].position = new Vector3(under + i - Ticks / 2, root.y - 0.09f, 0f);
            }
        }
    }
}
