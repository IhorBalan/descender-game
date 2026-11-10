using Descender.Player;
using UnityEngine;

namespace Descender.Lab
{
    /// <summary>A pad in a lab station: the hero stepping on it goes to another station.</summary>
    public sealed class LabPad : MonoBehaviour
    {
        private LabRoot lab;
        private int target;

        public int Target => target;

        public void Init(LabRoot root, int station)
        {
            lab = root;
            target = station;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (lab != null && body != null && body.TryGetComponent<PlayerController>(out var hero) && hero.IsAlive)
            {
                lab.Enter(hero, target);
            }
        }
    }
}
