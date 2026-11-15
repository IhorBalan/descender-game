using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// What drifts in the air of a Tiles V2 world's regions (docs/art/tiles-v2.md): motes of
    /// gold in Paradise, a wind of petals in Lust, rain in Gluttony, embers in Heresy, snow
    /// in Treachery. One particle system for each region that has any, every particle a
    /// single art pixel; they ride with the camera and a region's system gives only while
    /// the camera is in that region's rows, so walking from one region into the next the
    /// old air thins out as the new one fills.
    /// </summary>
    public sealed class TilesV2Air : MonoBehaviour
    {
        // How far past the view's edges the motes are born, in units.
        private const float Around = 4f;

        [SerializeField]
        private List<ParticleSystem> systems = new List<ParticleSystem>();

        [SerializeField, Tooltip("The world heights each system's region lies between: its foot and its top.")]
        private List<Vector2> spans = new List<Vector2>();

        public int Count => systems.Count;

        /// <summary>The system of a region, by its number among the regions that have one, and the heights it gives between.</summary>
        public ParticleSystem SystemAt(int index, out Vector2 span)
        {
            span = spans[index];
            return systems[index];
        }

        /// <summary>Builds the air of every region of the world's map under `parent`; returns how many systems.</summary>
        public static int Build(TilesV2World world, Transform parent)
        {
            var map = world.Map;
            var set = world.Set;
            if (map.Regions.Count == 0)
            {
                return 0;
            }
            var holder = new GameObject("Air");
            holder.transform.SetParent(parent, false);
            var air = holder.AddComponent<TilesV2Air>();
            var origin = world.transform.position.y;
            for (var i = 0; i < map.Regions.Count; i++)
            {
                var region = set.RegionOf(map.Regions[i].Key);
                if (region == null || region.airRate <= 0f)
                {
                    continue;
                }
                var material = region.airLit ? set.litMaterial : set.unlitMaterial;
                var most = Mathf.CeilToInt(region.airRate * region.airLife * 1.2f) + 8;
                var system = TilesV2Particles.Make(holder.transform, "Air " + region.key, Vector3.zero, material, most);
                var main = system.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(region.airLife * 0.7f, region.airLife);
                main.startSpeed = 0f;
                main.startColor = region.airColour;
                main.prewarm = true;
                var emission = system.emission;
                emission.rateOverTime = region.airRate;
                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(set.View.x + 2f * Around, set.View.y + 2f * Around, 0.1f);
                var drift = system.velocityOverLifetime;
                drift.enabled = true;
                drift.space = ParticleSystemSimulationSpace.World;
                drift.x = new ParticleSystem.MinMaxCurve(region.airAcross.x, region.airAcross.y);
                drift.y = new ParticleSystem.MinMaxCurve(region.airUp.x, region.airUp.y);
                drift.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                TilesV2Particles.Fade(system, Color.white, Color.white, 0.2f);
                system.Play();
                air.systems.Add(system);
                air.spans.Add(
                    new Vector2(origin + map.Height - map.RegionEnd(i), origin + map.Height - map.Regions[i].Top)
                );
            }
            air.Follow(world.Framed(world.Spawn));
            return air.systems.Count;
        }

        /// <summary>Brings the air to a view's middle and lets only that place's region give.</summary>
        public void Follow(Vector2 centre)
        {
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            for (var i = 0; i < systems.Count; i++)
            {
                if (systems[i] == null)
                {
                    continue;
                }
                var emission = systems[i].emission;
                emission.enabled = centre.y >= spans[i].x && centre.y < spans[i].y;
            }
        }

        private void LateUpdate()
        {
            var view = Camera.main;
            if (view != null)
            {
                Follow(view.transform.position);
            }
        }
    }
}
