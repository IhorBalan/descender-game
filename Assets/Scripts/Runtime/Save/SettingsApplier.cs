using UnityEngine;

namespace Descender.Save
{
    /// <summary>
    /// Pushes a <see cref="SettingsData"/> to the game: the window (size from the
    /// scale, fullscreen), v-sync, the master volume on the listener and the music's
    /// own volume. The settings screen calls <see cref="Apply"/> on every change and
    /// the game once on boot. Port of Settings.apply() in scripts/settings.gd; the
    /// binding overrides are applied by <c>InputRouter</c> when it wakes.
    /// </summary>
    public static class SettingsApplier
    {
        private static bool muted;

        /// <summary>
        /// Set while the tests run (<c>MuteDuringTests</c> in the PlayMode test assembly turns it on
        /// for the whole run): the listener stays at zero whatever the settings say, so a test run on
        /// a developer's machine makes no sound. Off again, the listener takes the settings' volume.
        /// </summary>
        public static bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                AudioListener.volume = MasterVolume(SettingsData.Current);
            }
        }

        /// <summary>
        /// A command-line (batch-mode) run of the game or the editor, which is how the tests run on
        /// a developer's machine, is silent from its first frame: the listener starts at zero before
        /// any scene loads, and <see cref="Apply"/> keeps it there.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SilenceBatchMode()
        {
            if (Application.isBatchMode)
            {
                AudioListener.volume = 0f;
            }
        }

        public static void Apply(SettingsData settings)
        {
            if (settings == null)
            {
                return;
            }
            ApplyWindow(settings);
            QualitySettings.vSyncCount = settings.vsync ? 1 : 0;
            AudioListener.volume = MasterVolume(settings);
            if (Audio.Music.Instance != null)
            {
                Audio.Music.Instance.ApplyVolume();
            }
        }

        /// <summary>
        /// The listener volume the settings ask for: zero while <see cref="Muted"/> and in any
        /// batch-mode run, so a test run never plays through the speakers.
        /// </summary>
        public static float MasterVolume(SettingsData settings)
        {
            return MasterVolume(settings, muted || Application.isBatchMode);
        }

        /// <summary>The listener volume for these settings, or zero when <paramref name="silent"/>.</summary>
        public static float MasterVolume(SettingsData settings, bool silent)
        {
            return silent ? 0f : settings.volume;
        }

        private static void ApplyWindow(SettingsData settings)
        {
            if (Application.isEditor || Application.isBatchMode)
            {
                return;
            }
            var mode = settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            var (width, height) = settings.WindowSizeFor(settings.scale);
            if (settings.fullscreen)
            {
                if (Screen.fullScreenMode != mode)
                {
                    Screen.fullScreenMode = mode;
                }
                return;
            }
            // A window bigger than the screen gets the largest scale that fits, as the Godot game did.
            var usable = Screen.mainWindowDisplayInfo.workArea;
            while ((width > usable.width || height > usable.height) && width > SettingsData.BaseSize.width)
            {
                width -= SettingsData.BaseSize.width;
                height -= SettingsData.BaseSize.height;
            }
            if (Screen.fullScreenMode != mode || Screen.width != width || Screen.height != height)
            {
                Screen.SetResolution(width, height, mode);
            }
        }
    }
}
