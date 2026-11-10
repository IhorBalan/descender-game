using System.Collections.Generic;
using Descender.Player;
using UnityEngine;

namespace Descender.Lab
{
    /// <summary>
    /// The built lab: its stations, and the way into one. Entering a station puts the
    /// hero at its spawn with exactly its abilities, makes that the place he comes back
    /// to, and holds the camera to the room.
    /// </summary>
    public sealed class LabRoot : MonoBehaviour
    {
        /// <summary>The lab that is built now, or null.</summary>
        public static LabRoot Current { get; private set; }

        private List<Station> stations = new List<Station>();
        private readonly Dictionary<Station, Transform> parents = new Dictionary<Station, Transform>();
        private readonly HashSet<Station> visited = new HashSet<Station>();

        public IReadOnlyList<Station> Stations => stations;

        /// <summary>The station the hero is in, or -1 before he has entered one.</summary>
        public int Index { get; private set; } = -1;

        public Station Station => Index >= 0 ? stations[Index] : null;

        /// <summary>Fired with the station after the hero is put into it.</summary>
        public event System.Action<Station> Entered;

        public void Init(List<Station> made)
        {
            stations = made;
        }

        /// <summary>Notes the objects a station was built as.</summary>
        public void Hold(Station station, Transform built)
        {
            parents[station] = built;
        }

        /// <summary>The parent of a station's objects.</summary>
        public Transform ParentOf(Station station)
        {
            return parents.TryGetValue(station, out var parent) ? parent : null;
        }

        /// <summary>
        /// Puts a station back as the layout has it: its objects are thrown away and
        /// built again, and the lab's key is taken back.
        /// </summary>
        public void Rebuild(Station station)
        {
            var old = ParentOf(station);
            if (old != null)
            {
                // Out of the way at once; the object itself goes at the end of the frame.
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            Triggers.Keys.Remove(LabLayout.Key);
            Hold(station, LabBuilder.BuildStation(this, station));
        }

        /// <summary>The number of the station with an id, or 0 (the first) when there is none.</summary>
        public int IndexOf(string id)
        {
            var index = stations.FindIndex(s => s.id == id);
            return index < 0 ? 0 : index;
        }

        public void Enter(PlayerController hero, int index)
        {
            if (hero == null || stations.Count == 0)
            {
                return;
            }
            Index = Mathf.Clamp(index, 0, stations.Count - 1);
            var station = stations[Index];
            if (station.rebuilt && !visited.Add(station))
            {
                Rebuild(station);
            }
            hero.SetAbilities(station.abilities);
            hero.DeathY = station.room.yMin - 4f;
            hero.Respawn(station.spawn);
            var camera = CameraRig.GameCamera.Main;
            if (camera != null)
            {
                camera.Bounds = station.room;
                camera.Snap();
            }
            Entered?.Invoke(station);
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
