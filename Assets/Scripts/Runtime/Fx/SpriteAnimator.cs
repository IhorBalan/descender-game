using System;
using System.Collections.Generic;
using UnityEngine;

namespace Descender.Fx
{
    /// <summary>
    /// Frame animation driven from code, the way Godot's AnimatedSprite2D was: a list
    /// of named strips (sprites, fps, loop) on a SpriteRenderer, with Play, PlayOnce,
    /// Pose and a speed scale. Prefabs get their strips from
    /// Assets/Art/Sprites/sprites.json through tools/unity/build_prefabs.py. Hit stop
    /// slows the animation with the game since it runs on Time.deltaTime.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [Serializable]
        public struct Strip
        {
            public string name;
            public float fps;
            public bool loop;
            public Sprite[] frames;
        }

        [SerializeField]
        private Strip[] strips = Array.Empty<Strip>();

        [SerializeField]
        private string startAnimation = "idle";

        private SpriteRenderer renderer2d;
        private readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);
        private int current = -1;
        private int frame;
        private float frameTime;
        private bool playing;
        private string oneShot = "";

        /// <summary>The strip showing, or "" before the first Play.</summary>
        public string Animation => current >= 0 ? strips[current].name : "";

        public int Frame => frame;
        public bool IsPlaying => playing;

        /// <summary>Plays the strips faster or slower; 1 is the spec's fps.</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>The renderer the strips play on; found on first use so EditMode code, where Awake does not run, can use it too.</summary>
        public SpriteRenderer Renderer => renderer2d != null ? renderer2d : renderer2d = GetComponent<SpriteRenderer>();

        public bool FlipX
        {
            get => Renderer.flipX;
            set => Renderer.flipX = value;
        }

        public Color Color
        {
            get => Renderer.color;
            set => Renderer.color = value;
        }

        /// <summary>A bare Visual child with a renderer and an empty animator, for a body whose prefab has none.</summary>
        public static SpriteAnimator CreateChild(Transform parent)
        {
            var child = new GameObject("Visual");
            child.transform.SetParent(parent, false);
            child.AddComponent<SpriteRenderer>();
            return child.AddComponent<SpriteAnimator>();
        }

        /// <summary>Replaces the strips (the prefab builder and tests use this).</summary>
        public void SetStrips(Strip[] value)
        {
            strips = value ?? Array.Empty<Strip>();
            Index();
            current = -1;
        }

        public bool Has(string animation) => index.ContainsKey(animation ?? "");

        /// <summary>How long one run of a strip takes at the current speed, in seconds.</summary>
        public float Length(string animation)
        {
            if (!index.TryGetValue(animation ?? "", out var i))
            {
                return 0f;
            }
            var strip = strips[i];
            return strip.frames.Length / Mathf.Max(strip.fps * Mathf.Abs(SpeedScale), 0.001f);
        }

        /// <summary>True once a non-looping strip has shown its last frame.</summary>
        public bool Finished => current >= 0 && !strips[current].loop && !playing;

        private void Awake()
        {
            renderer2d = GetComponent<SpriteRenderer>();
            Index();
            if (!string.IsNullOrEmpty(startAnimation) && Has(startAnimation))
            {
                Play(startAnimation);
            }
        }

        private void Index()
        {
            index.Clear();
            for (var i = 0; i < strips.Length; i++)
            {
                if (!string.IsNullOrEmpty(strips[i].name) && !index.ContainsKey(strips[i].name))
                {
                    index.Add(strips[i].name, i);
                }
            }
        }

        private void Update()
        {
            if (!playing || current < 0)
            {
                return;
            }
            var strip = strips[current];
            if (strip.frames.Length == 0)
            {
                return;
            }
            frameTime += Time.deltaTime * Mathf.Max(strip.fps, 0.001f) * Mathf.Abs(SpeedScale);
            while (frameTime >= 1f)
            {
                frameTime -= 1f;
                if (frame + 1 < strip.frames.Length)
                {
                    frame++;
                }
                else if (strip.loop)
                {
                    frame = 0;
                }
                else
                {
                    playing = false;
                    frameTime = 0f;
                    break;
                }
            }
            Show();
        }

        /// <summary>Switches to a strip, or restarts it when it was paused by Pose. A playing strip is left alone.</summary>
        public void Play(string animation)
        {
            if (!index.TryGetValue(animation ?? "", out var i))
            {
                return;
            }
            if (current == i && (playing || !strips[i].loop))
            {
                return;
            }
            Start(i);
        }

        /// <summary>Restarts a strip from its first frame even when it is already showing.</summary>
        public void Restart(string animation)
        {
            if (index.TryGetValue(animation ?? "", out var i))
            {
                Start(i);
            }
        }

        /// <summary>
        /// Starts a one-shot strip from its first frame once per token: a second call
        /// with the same name does nothing until <see cref="ClearOneShot"/>, so a finished
        /// strip stays on its last frame instead of replaying.
        /// </summary>
        public void PlayOnce(string animation)
        {
            if (oneShot == animation)
            {
                return;
            }
            oneShot = animation;
            Restart(animation);
        }

        public void ClearOneShot() => oneShot = "";

        /// <summary>Holds one frame of a strip.</summary>
        public void Pose(string animation, int poseFrame)
        {
            if (!index.TryGetValue(animation ?? "", out var i))
            {
                return;
            }
            current = i;
            playing = false;
            frameTime = 0f;
            frame = Mathf.Clamp(poseFrame, 0, Mathf.Max(0, strips[i].frames.Length - 1));
            Show();
        }

        public void Pause() => playing = false;

        private void Start(int i)
        {
            current = i;
            frame = 0;
            frameTime = 0f;
            playing = true;
            Show();
        }

        private void Show()
        {
            var strip = strips[current];
            if (strip.frames.Length > 0)
            {
                Renderer.sprite = strip.frames[Mathf.Min(frame, strip.frames.Length - 1)];
            }
        }
    }
}
