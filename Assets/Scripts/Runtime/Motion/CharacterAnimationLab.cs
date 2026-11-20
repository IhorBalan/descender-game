using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.Motion
{
    /// <summary>
    /// The CharacterAnimationLab scene's own behaviour (Assets/Scenes/Sandbox/
    /// CharacterAnimationLab.unity, made by Descender &gt; Lab &gt; Character animation lab):
    /// a ground with a mark every unit, a camera that keeps the <see cref="RunSkeleton"/>
    /// in view, the <see cref="RunDebugOverlay"/> and the keys to look closer. Tab shows or
    /// hides the overlay, Space pauses, the right arrow steps one sixtieth of a second while
    /// paused, 1, 2 and 3 play at full, a quarter and a tenth speed. It runs at 60 frames a
    /// second. Nothing of the game is in the scene or touched by it.
    /// </summary>
    public sealed class CharacterAnimationLab : MonoBehaviour
    {
        public const float Step = 1f / 60f;
        private const int Ticks = 48;

        private static readonly Color Earth = new Color(0.2f, 0.22f, 0.26f);
        private static readonly Color Tick = new Color(0.55f, 0.6f, 0.7f);

        [SerializeField]
        private RunSkeleton skeleton;

        [SerializeField]
        private RunDebugOverlay overlay;

        [SerializeField]
        private new Camera camera;

        [SerializeField]
        [Tooltip("Where the camera sits from the runner's root.")]
        private Vector2 cameraOffset = new Vector2(0.6f, 1.3f);

        private Transform ground;
        private readonly Transform[] ticks = new Transform[Ticks];

        public RunSkeleton Skeleton => skeleton;
        public RunDebugOverlay Overlay => overlay;

        /// <summary>Fills in what the scene is made of (the editor's scene maker and tests).</summary>
        public void Configure(RunSkeleton runner, RunDebugOverlay debug, Camera view)
        {
            skeleton = runner;
            overlay = debug;
            camera = view;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (overlay != null && skeleton != null)
            {
                overlay.Bind(skeleton);
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
