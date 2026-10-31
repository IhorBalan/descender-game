using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World
{
    /// <summary>
    /// A point light of a region, as the Godot scene placed it (scripts/world/
    /// world_light.gd): its colour, energy and reach in level pixels. It makes its
    /// own Light2D when the scene loads, so the generated scenes carry only data,
    /// and switches it off when far from the camera, so the hundreds of lanterns in
    /// a region cost nothing until they are near. Checked a few times a second.
    /// </summary>
    public sealed class WorldLight : MonoBehaviour
    {
        [SerializeField]
        private Color color = Color.white;

        [Tooltip("Godot's energy: the light's intensity.")]
        [SerializeField, Min(0f)]
        private float energy = 1f;

        [Tooltip("How far the light reaches, in level pixels.")]
        [SerializeField, Min(1f)]
        private float radiusPixels = 64f;

        [Tooltip("Whether the light wavers (data for now, as in Godot).")]
        [SerializeField]
        private bool flicker;

        [SerializeField, Min(1f)]
        private float activeDistance = 40f;

        [SerializeField, Min(0.05f)]
        private float checkEvery = 0.25f;

        private Light2D light2D;
        private float nextCheck;

        public Color Color => color;
        public float Energy => energy;
        public float RadiusPixels => radiusPixels;
        public bool Flicker => flicker;
        public float Radius => radiusPixels / Player.PlayerTuning.PixelsPerUnit;

        private void Awake()
        {
            light2D = GetComponent<Light2D>();
            if (light2D == null)
            {
                light2D = gameObject.AddComponent<Light2D>();
            }
            light2D.lightType = Light2D.LightType.Point;
            light2D.color = color;
            light2D.intensity = energy;
            light2D.pointLightInnerRadius = 0f;
            light2D.pointLightOuterRadius = Radius;
            light2D.falloffIntensity = 0.5f;
            light2D.shadowsEnabled = false;
            nextCheck = Random.Range(0f, checkEvery); // spread the checks over frames
        }

        private void Update()
        {
            if (Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + checkEvery;
            var camera = CameraRig.GameCamera.Main;
            if (camera == null)
            {
                return;
            }
            var distance = Vector2.Distance(camera.transform.position, transform.position);
            light2D.enabled = distance <= activeDistance + Radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
