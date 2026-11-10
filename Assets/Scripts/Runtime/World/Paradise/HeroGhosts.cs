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
        /// <summary>Children of the rig that are not part of his body, left out of the copies.</summary>
        private static readonly string[] NotCopied = { "Fx", "RimLight", "SlashArc", "Shockwave", "Shadow" };

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
            public GameObject root;
            public Transform[] bones;
            public SpriteRenderer[] renderers;
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
                        showing += ghost != null && ghost.root.activeSelf ? 1 : 0;
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
                if (ghost == null || !ghost.root.activeSelf)
                {
                    continue;
                }
                var t = (now - ghost.bornAt) / life;
                if (t >= 1f)
                {
                    ghost.root.SetActive(false);
                    continue;
                }
                var c = tint;
                c.a = tint.a * (1f - t) * (1f - t);
                foreach (var r in ghost.renderers)
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
                if (ghost != null && ghost.root != null)
                {
                    Destroy(ghost.root);
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
            var ghost = ghosts[next] ??= Make();
            next = (next + 1) % ghosts.Length;
            ghost.root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            ghost.root.transform.localScale = transform.lossyScale;
            for (var i = 1; i < live.Length && i < ghost.bones.Length; i++)
            {
                live[i].GetLocalPositionAndRotation(out var position, out var rotation);
                ghost.bones[i].SetLocalPositionAndRotation(position, rotation);
                ghost.bones[i].localScale = live[i].localScale;
            }
            ghost.bornAt = now;
            ghost.root.SetActive(true);
        }

        /// <summary>
        /// A copy of the rig's body. Made under an inactive holder, so nothing on it wakes
        /// before the animation and effects are taken off.
        /// </summary>
        private Ghost Make()
        {
            var holder = new GameObject("GhostHolder");
            holder.SetActive(false);
            var copy = Instantiate(gameObject, holder.transform);
            copy.name = "AnselGhost";
            var bones = copy.GetComponentsInChildren<Transform>(true);
            foreach (var behaviour in copy.GetComponents<MonoBehaviour>())
            {
                DestroyImmediate(behaviour);
            }
            foreach (var bone in bones)
            {
                if (System.Array.IndexOf(NotCopied, bone.name) >= 0)
                {
                    bone.gameObject.SetActive(false);
                }
            }
            var renderers = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var r in copy.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!Copied(r.transform, copy.transform))
                {
                    continue;
                }
                renderers.Add(r);
                r.sharedMaterial = material;
                // Light adds up, so the order of the parts within a copy does not matter.
                r.sortingOrder = order;
            }
            copy.SetActive(false);
            copy.transform.SetParent(null, false);
            Destroy(holder);
            return new Ghost
            {
                root = copy,
                bones = bones,
                renderers = renderers.ToArray(),
            };
        }

        private static bool Copied(Transform part, Transform root)
        {
            for (var t = part; t != null && t != root; t = t.parent)
            {
                if (System.Array.IndexOf(NotCopied, t.name) >= 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
