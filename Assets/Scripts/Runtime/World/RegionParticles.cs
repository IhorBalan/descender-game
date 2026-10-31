using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// One kind of particle drifting through the air of a region: petals, motes,
    /// embers, ash (port of scripts/world/region_particles.gd and region_fx.gd). The
    /// emitter follows the camera and fills the view plus a margin, so the air is
    /// never empty when the view moves. Speeds are level pixels a second, `size` is
    /// relative to the sprite drawn to the grid, `spin` turns a second at most and
    /// `sway` the sideways wobble in level pixels. Built on a ParticleSystem when the
    /// region loads.
    /// </summary>
    public sealed class RegionParticles : MonoBehaviour
    {
        public const int Order = 60;
        private const float MarginPixels = 48f;

        [SerializeField]
        private Sprite sprite;

        [SerializeField, Range(1, 500)]
        private int amount = 30;

        [SerializeField]
        private Color color = Color.white;

        [SerializeField, Min(0.1f)]
        private float lifetime = 10f;

        [SerializeField]
        private Vector2 direction = Vector2.down;

        [SerializeField, Range(0f, 180f)]
        private float spread = 25f;

        [SerializeField]
        private Vector2 speed = new Vector2(5f, 15f);

        [SerializeField]
        private Vector2 gravity = Vector2.zero;

        [SerializeField]
        private Vector2 size = Vector2.one;

        [SerializeField, Min(0f)]
        private float spin;

        [SerializeField, Min(0f)]
        private float sway;

        [SerializeField]
        private Material material;

        private ParticleSystem system;

        private void Awake()
        {
            if (sprite == null)
            {
                enabled = false;
                return;
            }
            var ppu = Player.PlayerTuning.PixelsPerUnit;
            system = gameObject.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.maxParticles = Mathf.Max(amount * 2, 64);
            main.startColor = color;
            var spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            main.startSize = new ParticleSystem.MinMaxCurve(spriteSize * size.x, spriteSize * size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.rateOverTime = amount / lifetime;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = new Vector3(20f, 11.25f, 1f);

            // Godot's direction, Y down, with the spread either side, at the speed range.
            var dir = new Vector2(direction.x, -direction.y).normalized;
            var a = Rotate(dir, -spread) * (speed.x / ppu);
            var b = Rotate(dir, spread) * (speed.y / ppu);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x));
            velocity.y = new ParticleSystem.MinMaxCurve(Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y));
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            if (gravity != Vector2.zero)
            {
                var force = system.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = new ParticleSystem.MinMaxCurve(gravity.x / ppu);
                force.y = new ParticleSystem.MinMaxCurve(-gravity.y / ppu);
                force.z = new ParticleSystem.MinMaxCurve(0f);
            }
            if (spin > 0f)
            {
                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-spin * Mathf.PI * 2f, spin * Mathf.PI * 2f);
            }
            if (sway > 0f)
            {
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = sway / ppu;
                noise.frequency = 0.4f;
                noise.scrollSpeed = 0.5f;
            }

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.AddSprite(sprite);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = Order;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            system.Play();
        }

        private void LateUpdate()
        {
            var camera = CameraRig.GameCamera.Main;
            if (camera == null || system == null)
            {
                return;
            }
            var margin = MarginPixels / Player.PlayerTuning.PixelsPerUnit;
            var at = camera.transform.position;
            transform.position = new Vector3(at.x, at.y, transform.position.z);
            var shape = system.shape;
            shape.scale = new Vector3((camera.HalfExtents.x + margin) * 2f, (camera.HalfExtents.y + margin) * 2f, 1f);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(r);
            var s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
