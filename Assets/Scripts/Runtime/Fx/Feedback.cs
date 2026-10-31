using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.Fx
{
    /// <summary>
    /// Game feel in one place, as the Godot Feedback autoload was: hit-stop, screen
    /// shake with gamepad rumble, sparks, death puffs and landing dust. The particles
    /// are three ParticleSystems made in code. Lives on its own object, made the first
    /// time something asks for it, so neither Main.unity nor a test has to set it up;
    /// reading the shake, rumble and hit-stop settings. Port of scripts/feedback.gd.
    /// </summary>
    public sealed class Feedback : MonoBehaviour
    {
        public const float HitStopScale = 0.05f;
        public const float RumbleTime = 0.15f;
        public static readonly Color DustColor = new Color(0.85f, 0.8f, 0.7f, 0.75f);

        /// <summary>How much shake, in pixels, a shake amount of 1 gives the camera.</summary>
        public const float ShakePixelsPerAmount = 6f;
        public const float ShakeTime = 0.25f;

        private static Feedback instance;

        private ParticleSystem sparks;
        private ParticleSystem puffs;
        private float hitStopUntil;
        private Coroutine rumble;

        /// <summary>How many sparks, puffs and dusts were asked for, for the tests.</summary>
        public static int SparkCount { get; private set; }
        public static int PuffCount { get; private set; }
        public static int DustCount { get; private set; }

        public static bool IsHitStopped => instance != null && instance.hitStopUntil > 0f;

        private static Feedback Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<Feedback>();
                    if (instance == null)
                    {
                        instance = new GameObject("Feedback").AddComponent<Feedback>();
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            sparks = MakeSystem("Sparks", 0.3f, 40);
            puffs = MakeSystem("Puffs", 1.2f, 30);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                if (hitStopUntil > 0f && Mathf.Approximately(Time.timeScale, HitStopScale))
                {
                    Time.timeScale = 1f;
                }
            }
        }

        private void Update()
        {
            if (hitStopUntil > 0f && Time.unscaledTime >= hitStopUntil)
            {
                hitStopUntil = 0f;
                // Pausing sets the scale to 0 meanwhile; only undo our own slow-down.
                if (Mathf.Approximately(Time.timeScale, HitStopScale))
                {
                    Time.timeScale = 1f;
                }
            }
        }

        /// <summary>Freezes the game for a moment of real time; a longer stop replaces a shorter one. Off with the setting.</summary>
        public static void HitStop(float seconds)
        {
            if (!Save.SettingsData.Current.hitStop || seconds <= 0f)
            {
                return;
            }
            var self = Instance;
            var until = Time.unscaledTime + seconds;
            if (until > self.hitStopUntil)
            {
                self.hitStopUntil = until;
            }
            if (Time.timeScale > 0f)
            {
                Time.timeScale = HitStopScale;
            }
        }

        /// <summary>Shakes the camera and rumbles the gamepad; amount is 0 to 1 and adds up across hits.</summary>
        public static void Shake(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }
            var camera = CameraRig.GameCamera.Main;
            if (camera != null)
            {
                camera.Shake(amount * ShakePixelsPerAmount, ShakeTime);
            }
            var settings = Save.SettingsData.Current;
            if (settings.rumble && Gamepad.current != null)
            {
                var self = Instance;
                Gamepad.current.SetMotorSpeeds(Mathf.Clamp01(amount * 0.5f), Mathf.Clamp01(amount));
                if (self.rumble != null)
                {
                    self.StopCoroutine(self.rumble);
                }
                self.rumble = self.StartCoroutine(self.StopRumble());
            }
        }

        private IEnumerator StopRumble()
        {
            yield return new WaitForSecondsRealtime(RumbleTime);
            rumble = null;
            Gamepad.current?.ResetHaptics();
        }

        /// <summary>Flashes whatever HitFlash the target carries.</summary>
        public static void Flash(Component target, float seconds = 0.1f)
        {
            if (target == null)
            {
                return;
            }
            var flash = target.GetComponentInChildren<HitFlash>();
            if (flash != null)
            {
                flash.Flash(seconds);
            }
        }

        /// <summary>A burst of sparks at a world position.</summary>
        public static void Spark(Vector2 at, Color color)
        {
            SparkCount++;
            Instance.Emit(Instance.sparks, at, color, 8, 50f, 130f, 0.15f, 0.3f, 1f, 2f, 360f, Vector2.zero);
        }

        public static void Spark(Vector2 at) => Spark(at, Color.white);

        /// <summary>A slow puff of dust or light where something died.</summary>
        public static void Puff(Vector2 at, Color color)
        {
            PuffCount++;
            Instance.Emit(Instance.puffs, at, color, 12, 8f, 30f, 0.6f, 1.1f, 2f, 4f, 360f, new Vector2(0f, 12f));
        }

        /// <summary>A small, quick puff at the hero's feet for a hard landing.</summary>
        public static void Dust(Vector2 at)
        {
            DustCount++;
            Instance.Emit(Instance.puffs, at, DustColor, 5, 10f, 30f, 0.2f, 0.35f, 1f, 2f, 80f, new Vector2(0f, 8f));
        }

        private void Emit(
            ParticleSystem system,
            Vector2 at,
            Color color,
            int count,
            float minSpeed,
            float maxSpeed,
            float minLife,
            float maxLife,
            float minSize,
            float maxSize,
            float spread,
            Vector2 drift
        )
        {
            if (system == null)
            {
                return;
            }
            var parameters = new ParticleSystem.EmitParams { startColor = color };
            for (var i = 0; i < count; i++)
            {
                var angle = 90f + Random.Range(-spread / 2f, spread / 2f);
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var speed = Random.Range(minSpeed, maxSpeed);
                parameters.position = new Vector3(at.x, at.y, 0f) + (Vector3)(Random.insideUnitCircle * 2f / 16f);
                parameters.velocity = (direction * speed + drift) / 16f;
                parameters.startLifetime = Random.Range(minLife, maxLife);
                parameters.startSize = Random.Range(minSize, maxSize) / 16f;
                system.Emit(parameters, 1);
            }
        }

        private ParticleSystem MakeSystem(string name, float lifetime, float gravity)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 512;
            main.gravityModifier = gravity / 16f / 9.81f;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = Shapes.Unlit;
            renderer.sortingOrder = 50;
            system.Play();
            return system;
        }
    }
}
