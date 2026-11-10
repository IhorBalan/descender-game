using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// A platform that travels from where it stands to a point and back at a steady
    /// speed, while it is running. Its body is kinematic, so what stands on it is
    /// carried by the physics. A trigger's <see cref="SetMover"/> action starts and
    /// stops it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Mover : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where it goes, from where it starts, in units.")]
        private Vector2 travel = new Vector2(0f, 4f);

        [SerializeField, Min(0.1f)]
        [Tooltip("Units a second.")]
        private float speed = 2f;

        [SerializeField]
        private bool running;

        private Rigidbody2D body;
        private Vector2 origin;
        private float travelled;

        public bool Running => running;
        public Vector2 Travel => travel;

        /// <summary>How far along it is: 0 at the start, 1 at the far end.</summary>
        public float Progress => Mathf.PingPong(travelled, 1f);

        public void Configure(Vector2 to, float unitsPerSecond, bool startsRunning = false)
        {
            travel = to;
            speed = Mathf.Max(unitsPerSecond, 0.1f);
            running = startsRunning;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            origin = transform.position;
        }

        public void Set(SwitchTo to) => running = to == SwitchTo.Toggle ? !running : to == SwitchTo.On;

        private void FixedUpdate()
        {
            var length = travel.magnitude;
            if (!running || length <= 0f)
            {
                return;
            }
            travelled += speed * Time.fixedDeltaTime / length;
            body.MovePosition(origin + travel * Progress);
        }
    }
}
