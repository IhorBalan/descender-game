using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World
{
    /// <summary>
    /// A region light that switches itself off when far from the camera, so the
    /// hundreds of lanterns in a region cost nothing until they are near.
    /// Checked a few times a second, not every frame.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class WorldLight : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        private float activeDistance = 30f;

        [SerializeField, Min(0.05f)]
        private float checkEvery = 0.25f;

        private Light2D light2D;
        private float nextCheck;

        private void Awake()
        {
            light2D = GetComponent<Light2D>();
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
            light2D.enabled = distance <= activeDistance;
        }
    }
}
