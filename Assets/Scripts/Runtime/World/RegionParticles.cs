using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// One kind of particle drifting through the air of a region: petals, motes,
    /// embers, ash (port of scripts/world/region_particles.gd and region_fx.gd). The
    /// emitter follows the camera and fills the view plus a margin, so the air is
    /// never empty when the view moves, but only the part of the view inside its own
    /// region's band, as Godot's emitter covered the band: a neighbour loaded ahead
    /// of the hero must not snow its ash into the region he is in. Speeds are level pixels a second, `size` is
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

        [Tooltip("Emit only inside this rect (world units) instead of the parent region's band: a Paradise zone's own stretch.")]
        [SerializeField]
        private bool useArea;

        [SerializeField]
        private Rect area;

        private ParticleSystem system;
        private RegionRoot region;
        private float fullRate;

        /// <summary>Sets everything a scene would serialize, for editor tools that build zones.</summary>
        public void Configure(
            Sprite particleSprite,
            int particleAmount,
            Color tint,
            float life,
            Vector2 dir,
            float spreadDegrees,
            Vector2 speedRange,
            Vector2 sizeRange,
            float spinTurns,
            float swayPixels,
            Material particleMaterial,
            Rect emitArea)
        {
            sprite = particleSprite;
            amount = particleAmount;
            color = tint;
            lifetime = life;
            direction = dir;
            spread = spreadDegrees;
            speed = speedRange;
            size = sizeRange;
            spin = spinTurns;
            sway = swayPixels;
            material = particleMaterial;
            useArea = true;
            area = emitArea;
        }

        private void Awake()
        {
            region = GetComponentInParent<RegionRoot>();
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
            fullRate = amount / lifetime;
            emission.rateOverTime = fullRate;

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
            // All three axes must share a mode: x and y are two-constant ranges, so z is too.
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

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
            var half = camera.HalfExtents + Vector2.one * margin;
            var view = new Rect((Vector2)at - half, half * 2f);
            var area = useArea ? Clip(view, this.area) : region != null ? Clip(view, region.Bounds) : view;
            var emission = system.emission;
            if (area.width <= 0f || area.height <= 0f)
            {
                // The view is outside the band: nothing new, what is in the air drifts on.
                emission.enabled = false;
                return;
            }
            emission.enabled = true;
            // The same density whatever share of the view the band fills.
            emission.rateOverTime = fullRate * area.width * area.height / (view.width * view.height);
            transform.position = new Vector3(area.center.x, area.center.y, transform.position.z);
            var shape = system.shape;
            shape.scale = new Vector3(area.width, area.height, 1f);
        }

        /// <summary>The overlap of two rects; zero-sized when they do not meet.</summary>
        public static Rect Clip(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax <= xMin || yMax <= yMin ? new Rect(xMin, yMin, 0f, 0f) : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
