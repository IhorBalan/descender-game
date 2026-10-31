using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// Someone to talk to. Stands still, turns to face the hero when he comes close
    /// and plays its talk loop, if its Animator has one (a bool "Talking"), while its
    /// dialogue box is open. An NPC that trades names a shop: once its unique lines
    /// are spent, a talk plays a repeat line and opens the wares. The sprite and its
    /// animations come from the character prefab this sits on. Port of scripts/npc.gd.
    /// </summary>
    public sealed class Npc : Interactable
    {
        [SerializeField]
        private string npcName = "";

        [SerializeField]
        [Tooltip("A Shops id, or empty for someone who does not trade.")]
        private string shop = "";

        [SerializeField]
        [Tooltip("Whether the sprite art faces left (the hero's does not, the guards' does).")]
        private bool facesLeft;

        [SerializeField]
        private SpriteRenderer sprite;

        [SerializeField]
        private Animator animator;

        public string NpcName => npcName;
        public string Shop => shop;
        public bool Trades => !string.IsNullOrEmpty(shop);

        public void Configure(string displayName, string shopId, bool artFacesLeft)
        {
            npcName = displayName ?? "";
            shop = shopId ?? "";
            facesLeft = artFacesLeft;
        }

        protected override void Awake()
        {
            base.Awake();
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteRenderer>();
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        public override void SetTalking(bool talking)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool("Talking", talking);
            }
        }

        private void LateUpdate()
        {
            if (!IsPlayerNear || sprite == null || NearHero == null)
            {
                return;
            }
            var towardLeft = NearHero.transform.position.x < transform.position.x;
            sprite.flipX = towardLeft != facesLeft;
        }
    }
}
