using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// A door: solid while closed, passable and faint while open. It is whatever
    /// colliders and sprites the object and its children have, so a trigger zone that
    /// should outlive the door's state does not go under it; a trigger's
    /// <see cref="SetDoor"/> action opens and closes it.
    /// </summary>
    public sealed class Door : MonoBehaviour
    {
        /// <summary>How much of its colour an open door keeps.</summary>
        public const float OpenAlpha = 0.2f;

        [SerializeField]
        private bool startsOpen;

        private bool open;
        private bool applied;

        public bool IsOpen => open;

        private void Start()
        {
            if (!applied)
            {
                Set(startsOpen);
            }
        }

        public void Open() => Set(true);

        public void Close() => Set(false);

        public void Set(SwitchTo to) => Set(to == SwitchTo.Toggle ? !open : to == SwitchTo.On);

        public void Set(bool nowOpen)
        {
            open = nowOpen;
            applied = true;
            foreach (var box in GetComponentsInChildren<Collider2D>(true))
            {
                box.enabled = !open;
            }
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
            {
                var color = sprite.color;
                color.a = open ? OpenAlpha : 1f;
                sprite.color = color;
            }
        }
    }
}
