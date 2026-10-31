using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A parallax backdrop layer: moves at a fraction of the camera's movement and
    /// repeats horizontally. Factor 0 is glued to the camera (sky), 1 moves with
    /// the world. Port of the Parallax2D layers in the Godot region scenes.
    /// </summary>
    [ExecuteAlways]
    public sealed class BackdropLayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)]
        private float parallaxFactor = 0.5f;

        [SerializeField, Min(0f)]
        private float repeatWidth;

        [SerializeField]
        private Vector2 origin;

        [SerializeField]
        private bool followY = true;

        [SerializeField]
        private float yFactor = 0.8f;

        private void LateUpdate()
        {
            var camera = Application.isPlaying
                ? CameraRig.GameCamera.Main?.transform
                : UnityEngine.Camera.main?.transform;
            if (camera == null)
            {
                return;
            }
            var cameraPosition = (Vector2)camera.position;
            var position = origin + (cameraPosition - origin) * (1f - parallaxFactor);
            if (!followY)
            {
                position.y = origin.y;
            }
            else
            {
                position.y = origin.y + (cameraPosition.y - origin.y) * (1f - parallaxFactor * yFactor);
            }
            if (repeatWidth > 0f)
            {
                var offset = cameraPosition.x - position.x;
                position.x += Mathf.Floor(offset / repeatWidth) * repeatWidth;
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
