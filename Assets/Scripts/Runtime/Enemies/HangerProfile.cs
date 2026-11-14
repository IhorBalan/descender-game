using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The hanged thing as a placement finds it in the catalog: its health. The rest of
    /// its numbers are the old constants in <see cref="FoeNumbers.Hanger"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Hanger Profile", fileName = "hanger")]
    public sealed class HangerProfile : ScriptableObject
    {
        [Tooltip("Hits to kill.")]
        public int health = 3;
    }
}
