using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Descender.Save
{
    /// <summary>
    /// Save slots on disk: each is a JSON file `slot_<n>.json` under a folder, three
    /// of them, as the Godot game kept under user://saves. The folder is injected so
    /// tests write to a temporary one; the game uses <see cref="SaveManager"/>, which
    /// points this at Application.persistentDataPath/saves.
    /// </summary>
    public sealed class SaveSlots
    {
        public const int SlotCount = 3;

        public string Directory { get; }

        public event Action<string> Warning;

        public SaveSlots(string directory)
        {
            Directory = directory;
        }

        public string SlotPath(int slot) => Path.Combine(Directory, $"slot_{slot + 1}.json");

        public bool HasSave(int slot) => File.Exists(SlotPath(slot));

        /// <summary>The slot's save, or null when it is missing or unreadable.</summary>
        public SaveData Read(int slot)
        {
            if (!HasSave(slot))
            {
                return null;
            }
            try
            {
                var data = SaveData.FromJson(JObject.Parse(File.ReadAllText(SlotPath(slot))));
                if (data != null)
                {
                    return data;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException) { }
            Warning?.Invoke($"Save slot {slot + 1} is unreadable, treating it as empty");
            return null;
        }

        public bool Write(int slot, SaveData data)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(SlotPath(slot), data.ToJson().ToString(Formatting.Indented));
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Warning?.Invoke($"Could not write save slot {slot + 1}: {exception.Message}");
                return false;
            }
        }

        public void Delete(int slot)
        {
            if (HasSave(slot))
            {
                File.Delete(SlotPath(slot));
            }
        }

        /// <summary>The slot saved most recently, or -1 when every slot is empty.</summary>
        public int LatestSlot()
        {
            var latest = -1;
            var latestTime = -1.0;
            for (var slot = 0; slot < SlotCount; slot++)
            {
                var data = Read(slot);
                if (data != null && data.SavedAt > latestTime)
                {
                    latest = slot;
                    latestTime = data.SavedAt;
                }
            }
            return latest;
        }
    }
}
