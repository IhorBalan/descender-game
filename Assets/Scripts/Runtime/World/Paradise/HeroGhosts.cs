using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Afterimages of Ansel on the dash and the dive of a slam: every few hundredths of a
    /// second a copy of him is left behind in the pose he had, drawn in light (the glow
    /// material, additive), and fades out. The copies are clones of the rig made the first
    /// time they are needed (without the rig's animation, effects and lights) and reused:
    /// each one copies every bone of the live rig once, when it is left behind, and then
    /// holds still. Runs after HeroRig has posed him for the frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HeroGhosts : MonoBehaviour
    {
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color tint = new Color(0.62f, 0.8f, 1f, 0.26f);

        [SerializeField, Range(1, 8)]
        private int count = 5;

        [Tooltip("Seconds between two copies.")]
        [SerializeField, Min(0.01f)]
        private float interval = 0.04f;

        [Tooltip("Seconds a copy takes to fade.")]
        [SerializeField, Min(0.05f)]
        private float life = 0.28f;

        [Tooltip("The sorting order of the copies: just behind him.")]
        [SerializeField]
        private int order = 9;

        private Player.PlayerController hero;
        private Transform[] live;
        private Ghost[] ghosts;
        private int next;
        private float nextAt;

        private sealed class Ghost
        {
            public RigCopy.Copy copy;
            public float bornAt;
        }

        public Material Material => material;

        public int Showing
        {
            get
            {
                var showing = 0;
                if (ghosts != null)
                {
                    foreach (var ghost in ghosts)
                    {
                        showing += ghost != null && ghost.copy.root.activeSelf ? 1 : 0;
                    }
                }
                return showing;
            }
        }

        public void Configure(Material glow) => material = glow;

        private void LateUpdate()
        {
            if (hero == null)
            {
                hero = GetComponentInParent<Player.PlayerController>();
                if (hero == null)
                {
                    return;
                }
            }
            var now = Time.time;
            var diving = hero.State == Player.PlayerState.Slam && !hero.SlamLanded;
            if ((hero.IsDashing || diving) && now >= nextAt && material != null)
            {
                Leave(now);
                nextAt = now + interval;
            }
            if (ghosts == null)
            {
                return;
            }
            foreach (var ghost in ghosts)
            {
                if (ghost == null || !ghost.copy.root.activeSelf)
                {
                    continue;
                }
                var t = (now - ghost.bornAt) / life;
                if (t >= 1f)
                {
                    ghost.copy.root.SetActive(false);
                    continue;
                }
                var c = tint;
                c.a = tint.a * (1f - t) * (1f - t);
                foreach (var r in ghost.copy.renderers)
                {
                    r.color = c;
                }
            }
        }

        private void OnDestroy()
        {
            if (ghosts == null)
            {
                return;
            }
            foreach (var ghost in ghosts)
            {
                if (ghost != null && ghost.copy.root != null)
                {
                    Destroy(ghost.copy.root);
                }
            }
        }

        /// <summary>Leaves the next copy where he is now, posed as he is now.</summary>
        private void Leave(float now)
        {
            if (ghosts == null)
            {
                live = GetComponentsInChildren<Transform>(true);
                ghosts = new Ghost[count];
            }
            var ghost = ghosts[next] ??= new Ghost { copy = RigCopy.Make(gameObject, "AnselGhost", material, order) };
            next = (next + 1) % ghosts.Length;
            var root = ghost.copy.root.transform;
            root.SetPositionAndRotation(transform.position, transform.rotation);
            root.localScale = transform.lossyScale;
            RigCopy.Pose(ghost.copy, live);
            ghost.bornAt = now;
            ghost.copy.root.SetActive(true);
        }
    }
}
