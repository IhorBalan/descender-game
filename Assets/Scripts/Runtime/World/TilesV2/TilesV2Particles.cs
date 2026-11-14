using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Makes a Tiles V2 world's particles (docs/art/tiles-v2.md), every one a single art
    /// pixel: dust that drifts round each torch and takes the tile light, so it shows only
    /// where there is light; embers rising from each flame; sand trickling from the
    /// underside of sand that hangs over a gap; sparks jumping from the surface of a liquid
    /// that glows. The systems are set up in code from the map, so there is no prefab to
    /// keep in step with it.
    /// </summary>
    public static class TilesV2Particles
    {
        /// <summary>One art pixel in units.</summary>
        public const float Pixel = 1f / 16f;

        /// <summary>The material whose overhangs trickle.</summary>
        public const string SandKey = "sand";

        /// <summary>The share of sand overhang cells that trickle, in hundredths.</summary>
        public const int TricklePercent = 45;

        private const int TrickleSalt = 311;
        private const int LongestFall = 14;

        // How fast things speed up, in units a second each second. The particles are pushed
        // by a force of their own, not by the physics' gravity: the project has no 3D physics
        // for a particle system to take it from.
        private const float SandFall = 8.8f;
        private const float SparkFall = 2.4f;
        private const float EmberLift = 0.5f;

        /// <summary>A liquid sparks when the light it gives is at least this bright.</summary>
        private const float Glowing = 0.5f;

        private static readonly Color DustColour = new Color(1f, 0.96f, 0.86f, 0.8f);
        private static readonly Color EmberHot = new Color(1f, 0.8f, 0.36f, 1f);
        private static readonly Color EmberCool = new Color(1f, 0.42f, 0.14f, 1f);
        private static readonly Color SandLight = new Color(0.95f, 0.89f, 0.66f, 1f);
        private static readonly Color SandDark = new Color(0.84f, 0.74f, 0.45f, 1f);

        /// <summary>Builds every particle system of the world under `parent`; returns how many.</summary>
        public static int Build(TilesV2World world, TileField field, Transform parent)
        {
            var set = world.Set;
            var count = 0;
            if (world.Torches != null)
            {
                foreach (Transform torch in world.Torches)
                {
                    TorchDust(torch, set.litMaterial);
                    count++;
                    if (!torch.TryGetComponent<TorchFlame>(out var flame) || flame.Embers)
                    {
                        TorchEmbers(torch, set.unlitMaterial);
                        count++;
                    }
                }
            }
            var root = new GameObject("Particles").transform;
            root.SetParent(parent, false);
            count += TilesV2Air.Build(world, root);
            for (var y = 0; y < field.Height; y++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    if (Trickles(field, x, y, out var fall))
                    {
                        var cell = field.CellAt(x, y);
                        SandTrickle(root, new Vector2(cell.x + 0.5f, cell.y), fall, set.litMaterial);
                        count++;
                    }
                }
                count += Sparks(field, y, root, set.unlitMaterial);
            }
            return count;
        }

        /// <summary>
        /// Whether sand trickles from under a grid cell: a full sand tile with two empty
        /// cells under it, picked by a hash of the cell. `fall` is how many cells the grains
        /// drop before they land.
        /// </summary>
        public static bool Trickles(TileField field, int x, int y, out int fall)
        {
            fall = 0;
            if (field.BlockKey(x, y) != SandKey || !field.Empty(x, y - 1) || !field.Empty(x, y - 2))
            {
                return false;
            }
            var cell = field.CellAt(x, y);
            if (TilesV2Painter.Hash(cell.x, cell.y, TrickleSalt) % 100 >= TricklePercent)
            {
                return false;
            }
            while (fall < LongestFall && field.Empty(x, y - 1 - fall))
            {
                fall++;
            }
            return true;
        }

        private static void TorchDust(Transform torch, Material material)
        {
            var system = Make(torch, "Dust", Vector3.zero, material, 14);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startColor = DustColour;
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 1.6f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4.5f;
            shape.radiusThickness = 1f;
            var drift = system.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            drift.y = new ParticleSystem.MinMaxCurve(0.02f, 0.2f);
            drift.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.2f;
            Fade(system, Color.white, Color.white, 0.25f);
            system.Play();
        }

        private static void TorchEmbers(Transform torch, Material material)
        {
            var system = Make(torch, "Embers", new Vector3(0f, 0.35f, 0f), material, 8);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(EmberHot, EmberCool);
            Push(system, EmberLift);
            var emission = system.emission;
            emission.rateOverTime = 2.2f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var sway = system.velocityOverLifetime;
            sway.enabled = true;
            sway.space = ParticleSystemSimulationSpace.World;
            sway.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            sway.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            sway.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Fade(system, Color.white, new Color(0.7f, 0.2f, 0.1f), 0f);
            system.Play();
        }

        private static void SandTrickle(Transform parent, Vector2 at, int fall, Material material)
        {
            var system = Make(parent, "Sand", at, material, 64);
            var main = system.main;
            // The time a grain takes to drop `fall` cells from rest.
            main.startLifetime = Mathf.Sqrt(2f * Mathf.Max(fall, 1) / SandFall);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(SandLight, SandDark);
            Push(system, -SandFall);
            var emission = system.emission;
            emission.rateOverTime = 18f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 0.3f;
            system.Play();
        }

        // Sparks over every run of glowing liquid surface in a row of the field; returns how many systems.
        private static int Sparks(TileField field, int y, Transform parent, Material material)
        {
            var made = 0;
            var x = 0;
            while (x < field.Width)
            {
                if (!GlowingSurface(field, x, y))
                {
                    x++;
                    continue;
                }
                var from = x;
                while (x < field.Width && GlowingSurface(field, x, y))
                {
                    x++;
                }
                var width = x - from;
                var cell = field.CellAt(from, y);
                var system = Make(
                    parent,
                    "Sparks",
                    new Vector2(cell.x + width / 2f, cell.y + 1f - 3f * Pixel),
                    material,
                    Mathf.Clamp(width * 4, 6, 64)
                );
                var main = system.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startSpeed = 0f;
                main.startColor = new ParticleSystem.MinMaxGradient(EmberHot, EmberCool);
                Push(system, -SparkFall);
                var emission = system.emission;
                emission.rateOverTime = 1.2f * width;
                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                shape.radius = width / 2f;
                var leap = system.velocityOverLifetime;
                leap.enabled = true;
                leap.space = ParticleSystemSimulationSpace.World;
                leap.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                leap.y = new ParticleSystem.MinMaxCurve(0.7f, 1.9f);
                leap.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                Fade(system, Color.white, new Color(0.7f, 0.2f, 0.1f), 0f);
                system.Play();
                made++;
            }
            return made;
        }

        private static bool GlowingSurface(TileField field, int x, int y)
        {
            var liquid = field.Liquid[field.Index(x, y)];
            if (liquid == null || liquid.Emits.maxColorComponent < Glowing)
            {
                return false;
            }
            return !field.Inside(x, y + 1) || field.Liquid[field.Index(x, y + 1)] != liquid;
        }

        // A system of single-pixel particles in world space, stopped so it can be set up.
        public static ParticleSystem Make(Transform parent, string name, Vector3 at, Material material, int most)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            var system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = Pixel;
            main.startRotation = 0f;
            main.maxParticles = most;
            var shown = holder.GetComponent<ParticleSystemRenderer>();
            shown.renderMode = ParticleSystemRenderMode.Billboard;
            shown.sortingOrder = TilesV2World.ParticleOrder;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            return system;
        }

        // A steady push up (or, below zero, down) on every particle, in units a second each second.
        private static void Push(ParticleSystem system, float up)
        {
            var force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = 0f;
            force.y = up;
            force.z = 0f;
        }

        // Colour over a particle's life: from one colour to another, fading out at the end (and in over `fadeIn` of it).
        public static void Fade(ParticleSystem system, Color from, Color to, float fadeIn)
        {
            var life = system.colorOverLifetime;
            life.enabled = true;
            var gradient = new Gradient();
            var alpha =
                fadeIn > 0f
                    ? new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, fadeIn),
                        new GradientAlphaKey(1f, 1f - fadeIn),
                        new GradientAlphaKey(0f, 1f),
                    }
                    : new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 0.6f),
                        new GradientAlphaKey(0f, 1f),
                    };
            gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) }, alpha);
            life.color = gradient;
        }
    }
}
