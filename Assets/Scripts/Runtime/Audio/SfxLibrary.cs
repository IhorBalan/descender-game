using System;
using UnityEngine;

namespace Descender.Audio
{
    /// <summary>
    /// The sound effects by the names the Godot game played them under ("jump",
    /// "hit", "enemy_die"...), one entry per clip in Assets/Audio/sfx. Built as
    /// Assets/Data/SfxLibrary.asset by tools/unity/build_prefabs.py; Sfx in Main.unity
    /// holds it. Edit the asset to swap a sound.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Sfx Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public AudioClip clip;
        }

        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;

        public void SetEntries(Entry[] value) => entries = value ?? Array.Empty<Entry>();

        /// <summary>The clip of a name, or null when the library has none.</summary>
        public AudioClip Find(string name)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].name, name, StringComparison.Ordinal))
                {
                    return entries[i].clip;
                }
            }
            return null;
        }
    }
}
