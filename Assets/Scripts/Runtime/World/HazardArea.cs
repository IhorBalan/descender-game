using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// Spikes: a run of hazard tiles as one trigger on the Hazard layer, built by
    /// RegionTiles. The hero loses health while inside. Port of scripts/hazard.gd;
    /// it polls with OnTriggerStay2D so a hero still on the spikes when his
    /// invulnerability ends is hurt again.
    /// </summary>
    public sealed class HazardArea : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int damage = 1;

        private void OnTriggerEnter2D(Collider2D other) => Hurt(other);

        private void OnTriggerStay2D(Collider2D other) => Hurt(other);

        private void Hurt(Collider2D other)
        {
            if (other.TryGetComponent<Player.PlayerController>(out var hero))
            {
                hero.Hurt(damage, transform.position);
            }
        }
    }
}
