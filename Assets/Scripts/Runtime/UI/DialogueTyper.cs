using System;

namespace Descender.UI
{
    /// <summary>
    /// The typing clock of the dialogue box, kept apart from the box so it can be
    /// stepped in a test: characters appear at a speed, the typing rests a moment
    /// after a sentence and a shorter one after a comma, and every few characters
    /// ask for a blip. Instant speed (0) shows the whole page at once.
    /// </summary>
    public sealed class DialogueTyper
    {
        /// <summary>The speed at the Normal setting; the rests are scaled against it.</summary>
        public const float BaseCharsPerSecond = 45f;
        public const float PauseSentence = 0.2f;
        public const float PauseComma = 0.08f;
        public const string SentenceEnds = ".!?";
        public const string CommaEnds = ",;:";
        public const int BlipEvery = 3;
        public const float BlinkPeriod = 0.8f;

        private string text = "";
        private float shown;
        private float pause;
        private int sinceBlip;
        private float blink;

        public string Text => text;

        /// <summary>How many characters are showing.</summary>
        public int Visible => (int)Math.Min(shown, text.Length);

        public bool IsDone => shown >= text.Length;

        /// <summary>Whether the typing is resting on a punctuation mark.</summary>
        public bool IsPausing => !IsDone && pause > 0f;

        /// <summary>Whether the "more" mark is in its lit half of the blink.</summary>
        public bool MoreLit => IsDone && blink % BlinkPeriod < BlinkPeriod * 0.6f;

        /// <summary>Starts typing a page from its first character.</summary>
        public void Start(string page)
        {
            text = page ?? "";
            shown = 0f;
            pause = 0f;
            sinceBlip = 0;
            blink = 0f;
        }

        /// <summary>Shows the whole page at once.</summary>
        public void Skip()
        {
            shown = text.Length;
            pause = 0f;
        }

        /// <summary>
        /// Advances `delta` seconds at `charsPerSecond` (0 for instant). Returns true
        /// when a blip should sound: every few new characters, and once for an instant page.
        /// </summary>
        public bool Tick(float delta, float charsPerSecond)
        {
            blink += delta;
            if (IsDone)
            {
                return false;
            }
            if (pause > 0f)
            {
                pause -= delta;
                return false;
            }
            var before = Visible;
            if (charsPerSecond <= 0f)
            {
                shown = text.Length;
            }
            else
            {
                shown = Math.Min(shown + charsPerSecond * delta, text.Length);
            }
            var after = Visible;
            if (after == before)
            {
                return false;
            }
            var last = text[after - 1];
            if (after < text.Length && charsPerSecond > 0f)
            {
                // The rests keep their feel at every speed: shorter when typing faster.
                var restScale = BaseCharsPerSecond / charsPerSecond;
                if (SentenceEnds.IndexOf(last) >= 0 && text[after] != last)
                {
                    pause = PauseSentence * restScale;
                }
                else if (CommaEnds.IndexOf(last) >= 0)
                {
                    pause = PauseComma * restScale;
                }
            }
            sinceBlip += after - before;
            if (sinceBlip >= BlipEvery)
            {
                sinceBlip = 0;
                return true;
            }
            return false;
        }
    }
}
