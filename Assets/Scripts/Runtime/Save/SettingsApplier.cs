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

        /// <summary>The listener volume the settings ask for: zero while <see cref="Muted"/>.</summary>
        public static float MasterVolume(SettingsData settings)
        {
            return muted ? 0f : settings.volume;
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
