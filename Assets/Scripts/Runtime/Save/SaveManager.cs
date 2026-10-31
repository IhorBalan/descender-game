using System.IO;
using UnityEngine;

namespace Descender.Save
{
    /// <summary>
    /// The save the level applies when it boots, and the slot the running game
    /// saves to. The title screen picks a slot and either clears it (new game) or
    /// reads it into <see cref="Pending"/>; the game session applies Pending once the
    /// level is built and writes back to <see cref="CurrentSlot"/> whenever the game saves.
    /// </summary>
    public static class SaveManager
    {
        public static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };

        /// <summary>One line on each difficulty, for the picker.</summary>
        public static readonly string[] DifficultyNotes =
        {
            "An extra heart and an extra healing charge",
            "The descent as it was meant",
            "Every blow lands twice as hard",
        };

        private static readonly int[] DifficultyHearts = { 1, 0, 0 };
        private static readonly int[] DifficultyCharges = { 1, 0, 0 };
        private static readonly int[] DifficultyDamage = { 1, 1, 2 };

        private static SaveSlots slots;

        /// <summary>The slots on disk; tests point this at a temporary folder with <see cref="UseDirectory"/>.</summary>
        public static SaveSlots Slots => slots ?? (slots = new SaveSlots(Path.Combine(Application.persistentDataPath, "saves")));

        public static void UseDirectory(string directory)
        {
            slots = new SaveSlots(directory);
        }

        /// <summary>The slot the running game saves to, or -1 when it should not save at all.</summary>
        public static int CurrentSlot { get; private set; } = -1;

        /// <summary>The save to apply when the level boots; null for a fresh game.</summary>
        public static SaveData Pending { get; private set; }

        public static Difficulty Difficulty { get; private set; } = Difficulty.Normal;

        /// <summary>Clears the slot and makes it the one the new game saves to.</summary>
        public static void StartNewGame(int slot, Difficulty chosen = Difficulty.Normal)
        {
            Slots.Delete(slot);
            CurrentSlot = slot;
            Difficulty = chosen;
            Pending = null;
        }

        /// <summary>Reads the slot for the level to apply. Returns false when it is empty.</summary>
        public static bool LoadSlot(int slot)
        {
            var data = Slots.Read(slot);
            if (data == null)
            {
                return false;
            }
            CurrentSlot = slot;
            Difficulty = data.Difficulty;
            Pending = data;
            return true;
        }

        /// <summary>Forgets the slot, as the smoke test and a level run directly do.</summary>
        public static void Detach()
        {
            CurrentSlot = -1;
            Pending = null;
            Difficulty = Difficulty.Normal;
        }

        /// <summary>Writes the progress to the current slot. Returns false when there is no slot.</summary>
        public static bool Write(SaveData data)
        {
            if (CurrentSlot < 0)
            {
                return false;
            }
            data.Difficulty = Difficulty;
            return Slots.Write(CurrentSlot, data);
        }

        public static string DifficultyName() => DifficultyNames[(int)Difficulty];

        /// <summary>Extra hearts the difficulty gives the hero.</summary>
        public static int BonusHearts() => DifficultyHearts[(int)Difficulty];

        /// <summary>Extra healing charges the difficulty gives the hero.</summary>
        public static int BonusCharges() => DifficultyCharges[(int)Difficulty];

        /// <summary>A hit on the hero, as the difficulty makes it.</summary>
        public static int ScaledDamage(int amount) => amount * DifficultyDamage[(int)Difficulty];

        /// <summary>One line for the slot picker: which level, where in it, how long so far.</summary>
        public static string Summary(int slot, System.Func<string, string> levelName)
        {
            var data = Slots.Read(slot);
            if (data == null)
            {
                return "Empty";
            }
            var place = (levelName?.Invoke(data.Level) ?? data.Level).Split(':')[0];
            if (data.Checkpoint >= 0)
            {
                place += $" checkpoint {data.Checkpoint + 1}";
            }
            var seconds = (int)data.PlayTime;
            var played = $"{seconds / 60}:{seconds % 60:00}";
            if (data.Difficulty != Difficulty.Normal)
            {
                played += "   " + DifficultyNames[(int)data.Difficulty];
            }
            if (data.Abilities.Count == 0)
            {
                return $"{place}   {played}";
            }
            var names = new System.Collections.Generic.List<string>();
            foreach (var ability in data.Abilities)
            {
                names.Add(AbilityName(ability));
            }
            return $"{place}   {string.Join(", ", names)}   {played}";
        }

        /// <summary>"Wings", "Light Dash", as Godot's String.capitalize() made of the enum names.</summary>
        public static string AbilityName(Ability ability)
        {
            switch (ability)
            {
                case Ability.Wings: return "Wings";
                case Ability.LightDash: return "Light Dash";
                case Ability.WallGrip: return "Wall Grip";
                case Ability.HolyLight: return "Holy Light";
                case Ability.Glide: return "Glide";
                case Ability.Smite: return "Smite";
                default: return ability.ToString();
            }
        }
    }
}
