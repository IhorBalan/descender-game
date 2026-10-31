using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace Descender.Save
{
    public enum PromptLayout
    {
        Auto = 0,
        Xbox = 1,
        PlayStation = 2,
    }

    /// <summary>
    /// Player settings: volumes, window, screen shake, game feel and accessibility,
    /// and the Input System binding overrides. The values the Godot game kept in
    /// user://settings.cfg, now a JSON file under Application.persistentDataPath.
    /// Applying them to the mixer, the window and the input actions is the job of
    /// the SettingsApplier behaviour; this class only holds and stores them.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const string FileName = "settings.json";
        public static readonly (int width, int height) BaseSize = (320, 180);
        public const int MinWindowScale = 2;
        public const int MaxWindowScale = 6;
        public const int DefaultWindowScale = 4;
        /// <summary>Screen shake amounts the settings screen cycles through: off, half, full.</summary>
        public static readonly float[] ShakeLevels = { 0f, 0.5f, 1f };
        /// <summary>Dialogue typing speeds in characters per second; the last is instant.</summary>
        public static readonly string[] TextSpeedNames = { "Slow", "Normal", "Fast", "Instant" };
        public static readonly float[] TextSpeeds = { 25f, 45f, 90f, 0f };
        public const int DefaultTextSpeed = 1;
        public static readonly string[] LayoutNames = { "Auto", "Xbox", "PlayStation" };

        /// <summary>The actions the settings screen lets the player rebind, in menu order, and their labels.</summary>
        public static readonly (string action, string label)[] Rebindable =
        {
            ("MoveLeft", "Move left"),
            ("MoveRight", "Move right"),
            ("MoveDown", "Move down"),
            ("Jump", "Jump"),
            ("Attack", "Attack"),
            ("Dash", "Dash"),
            ("Cast", "Holy Light"),
            ("Heal", "Heal"),
            ("Interact", "Talk"),
            ("Pause", "Pause"),
            ("Restart", "Restart"),
        };

        public float volume = 1f;
        public float music = 1f;
        public float sfx = 1f;
        public bool blips = true;
        public bool fullscreen = true;
        public int scale = DefaultWindowScale;
        public bool vsync = true;
        public float shake = 1f;
        public int textSpeed = DefaultTextSpeed;
        public bool hitFlash = true;
        public bool hitStop = true;
        public bool rumble = true;
        public PromptLayout prompts = PromptLayout.Auto;
        /// <summary>The Input System's binding overrides, as SaveBindingOverridesAsJson writes them; "" for the defaults.</summary>
        public string bindings = "";

        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));

        /// <summary>Puts every value back in range, as the Godot loader clamped them.</summary>
        public SettingsData Clamped()
        {
            volume = Clamp01(volume);
            music = Clamp01(music);
            sfx = Clamp01(sfx);
            scale = Math.Max(MinWindowScale, Math.Min(MaxWindowScale, scale));
            shake = Clamp01(shake);
            textSpeed = Math.Max(0, Math.Min(TextSpeeds.Length - 1, textSpeed));
            prompts = (PromptLayout)Math.Max(0, Math.Min(LayoutNames.Length - 1, (int)prompts));
            bindings = bindings ?? "";
            return this;
        }

        /// <summary>How many characters a second dialogue types, or 0 for a whole page at once.</summary>
        public float CharsPerSecond() => TextSpeeds[Math.Max(0, Math.Min(TextSpeeds.Length - 1, textSpeed))];

        public string TextSpeedName() => TextSpeedNames[Math.Max(0, Math.Min(TextSpeedNames.Length - 1, textSpeed))];

        public (int width, int height) WindowSizeFor(int windowScale) => (BaseSize.width * windowScale, BaseSize.height * windowScale);

        /// <summary>The name of a screen shake level: "Off", "Half" or "Full".</summary>
        public static string ShakeName(float amount) => amount <= 0f ? "Off" : amount >= 1f ? "Full" : "Half";

        public void ResetAudio()
        {
            volume = 1f;
            music = 1f;
            sfx = 1f;
            blips = true;
        }

        public void ResetDisplay()
        {
            fullscreen = true;
            scale = DefaultWindowScale;
            vsync = true;
            shake = 1f;
        }

        public void ResetGame()
        {
            textSpeed = DefaultTextSpeed;
            hitFlash = true;
            hitStop = true;
            rumble = true;
            prompts = PromptLayout.Auto;
        }

        public void ResetBindings() => bindings = "";

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        /// <summary>The settings a JSON text holds, clamped; defaults when it cannot be read.</summary>
        public static SettingsData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new SettingsData();
            }
            try
            {
                return (JsonConvert.DeserializeObject<SettingsData>(json) ?? new SettingsData()).Clamped();
            }
            catch (JsonException)
            {
                return new SettingsData();
            }
        }

        private static SettingsData current;

        /// <summary>The settings file under Application.persistentDataPath.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "settings.json");

        /// <summary>The live settings, loaded from <see cref="DefaultPath"/> on first use.</summary>
        public static SettingsData Current
        {
            get => current ??= Load(DefaultPath);
            set => current = value ?? new SettingsData();
        }

        /// <summary>Saves to <see cref="DefaultPath"/>.</summary>
        public bool Save() => Save(DefaultPath);

        public static SettingsData Load(string path)
        {
            try
            {
                return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new SettingsData();
            }
            catch (IOException)
            {
                return new SettingsData();
            }
        }

        public bool Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, ToJson());
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
