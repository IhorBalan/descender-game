using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The painted look of a <see cref="LanternFruit"/>: the fruit hanging and ripe, and the
    /// frames of its burst, its pop and its growing back. Every sprite's pivot is the
    /// fruit's middle, but the burst's, which is the ground under it. Without one the fruit
    /// stands as a placeholder circle. Assets/Data/Enemies/LanternFruitLook.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Lantern fruit look", fileName = "LanternFruitLook")]
    public sealed class LanternFruitLook : ScriptableObject
    {
        [SerializeField]
        private Sprite hang;

        [SerializeField]
        [Tooltip("The warning: shown over the hanging fruit as it ripens, and while it falls.")]
        private Sprite ripe;

        [SerializeField]
        [Tooltip("Played where it bursts; the pivot is the ground.")]
        private Sprite[] burst = new Sprite[0];

        [SerializeField]
        [Tooltip("Played where a strike pops it.")]
        private Sprite[] pop = new Sprite[0];

        [SerializeField]
        [Tooltip("Played over the time it grows back; the last frame gives way to the hanging fruit.")]
        private Sprite[] grow = new Sprite[0];

        [SerializeField]
        [Tooltip("Frames a second of the burst and the pop.")]
        private float framesPerSecond = 14f;

        [SerializeField]
        [Tooltip(
            "Units from the fruit's middle up to the top of its painted stem, where the stalk to the branch starts."
        )]
        private float stemTop = 1f;

        public Sprite Hang => hang;
        public Sprite Ripe => ripe;
        public Sprite[] Burst => burst;
        public Sprite[] Pop => pop;
        public Sprite[] Grow => grow;
        public float FramesPerSecond => framesPerSecond;
        public float StemTop => stemTop;

        /// <summary>Fills a look from code, for tests.</summary>
        public void Set(Sprite hanging, Sprite ripened, Sprite[] bursting, Sprite[] popping, Sprite[] growing)
        {
            hang = hanging;
            ripe = ripened;
            burst = bursting ?? new Sprite[0];
            pop = popping ?? new Sprite[0];
            grow = growing ?? new Sprite[0];
        }
    }
}
