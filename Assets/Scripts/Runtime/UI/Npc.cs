using System.Collections.Generic;
using Descender.Fx;
using Descender.World;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// Someone to talk to. Stands still, turns to face the hero when he comes close
    /// and plays its talk loop, if its Animator has one (a bool "Talking"), while its
    /// dialogue box is open. An NPC that trades names a shop: once its unique lines
    /// are spent, a talk plays a repeat line and opens the wares. A placement gives
    /// the name, the shop, the lines and the character; the body is that character's
    /// idle frame when the prefab lists one (Descender &gt; Setup &gt; Story prefabs fills
    /// the list from Assets/Art/Sprites), else a plain block. Port of scripts/npc.gd.
    /// </summary>
    public sealed class Npc : Interactable, IPlacementReceiver
    {
        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        /// <summary>The stand-in for a character with no art, in level pixels.</summary>
        public static readonly Vector2 PlaceholderSize = new Vector2(12f, 28f);
        public static readonly Color PlaceholderColor = new Color(0.75f, 0.78f, 0.9f);

        /// <summary>A character's standing frame, by the id placements use.</summary>
        [System.Serializable]
        public struct Body
        {
            public string character;
            public Sprite idle;
        }

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

        [SerializeField]
        [Tooltip("The idle frame of each character a placement may name.")]
        private List<Body> bodies = new List<Body>();

        public string NpcName => npcName;
        public string Shop => shop;
        public bool Trades => !string.IsNullOrEmpty(shop);
        public bool FacesLeft => facesLeft;
        public IReadOnlyList<Body> Bodies => bodies;

        /// <summary>The character the body shows, or "" for the placeholder.</summary>
        public string Character { get; private set; } = "";

        public void Configure(string displayName, string shopId, bool artFacesLeft)
        {
            npcName = displayName ?? "";
            shop = shopId ?? "";
            facesLeft = artFacesLeft;
        }

        /// <summary>The placement's name, shop, facing, lines and body.</summary>
        public void Configure(WorldPlacement placement)
        {
            Configure(placement.PlacementName, placement.Shop, placement.FacesLeft);
            Configure(placement.Dialogue, placement.Rotate);
            // The placement mirrors a thing that faces left. An NPC's flag is about its
            // art: he turns to the hero himself, and a mirrored prompt would read backwards.
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            transform.localScale = scale;
            Dress(placement.Character, placement.Tint);
        }

        /// <summary>The idle frame the prefab lists for a character, or null.</summary>
        public Sprite IdleOf(string character)
        {
            if (string.IsNullOrEmpty(character))
            {
                return null;
            }
            foreach (var body in bodies)
            {
                if (body.character == character)
                {
                    return body.idle;
                }
            }
            return null;
        }

        /// <summary>Shows a character's idle frame in a tint, or the placeholder block when there is no art for it.</summary>
        public void Dress(string character, Color tint)
        {
            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteRenderer>();
            }
            var idle = IdleOf(character);
            Character = idle != null ? character : "";
            if (sprite == null)
            {
                return;
            }
            var body = sprite.transform;
            if (idle != null)
            {
                sprite.sprite = idle;
                sprite.color = tint;
                body.localPosition = Vector3.zero;
                body.localScale = Vector3.one;
                return;
            }
            // Shapes.Square is 4 px wide with its pivot in the middle.
            sprite.sprite = Shapes.Square;
            sprite.color = PlaceholderColor;
            body.localPosition = new Vector3(0f, PlaceholderSize.y / 2f / Ppu, 0f);
            body.localScale = new Vector3(PlaceholderSize.x / 4f, PlaceholderSize.y / 4f, 1f);
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
            if (sprite != null && sprite.sprite == null)
            {
                Dress("", Color.white);
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
