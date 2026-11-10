using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// A platform that travels from where it stands to a point and back at a steady
    /// speed, while it is running. Its body is kinematic and it says how fast it
    /// moves (<see cref="Velocity"/>), so the hero standing on it rides along; it runs
    /// before him in each physics step, so he rides this step's movement, not the
    /// last one's. A trigger's <see cref="SetMover"/> action starts and stops it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(-50)]
    public sealed class Mover : MonoBehaviour, Player.IMovingGround
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

        /// <summary>Units a second.</summary>
        public float Speed => speed;

        public Vector2 Velocity { get; private set; }

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
                Velocity = Vector2.zero;
                return;
            }
            travelled += speed * Time.fixedDeltaTime / length;
            var next = origin + travel * Progress;
            Velocity = (next - body.position) / Time.fixedDeltaTime;
            body.MovePosition(next);
        }
    }
}
