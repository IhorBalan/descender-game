using System.Collections.Generic;
using Descender.Player;
using Descender.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The top left of the HUD, drawn with plain coloured boxes as the Godot
    /// StatusBar drew them: hearts for health, bars for healing charges, a 9 x 7
    /// icon per unlocked ability, the dash cooldown bar and the running power-ups
    /// with a bar under each that drains. Rebuilt when a value changes, never
    /// polled; the dash bar and the power bars resize in place.
    /// Port of scripts/status_bar.gd.
    /// </summary>
    public sealed class StatusBar : MonoBehaviour
    {
        public static readonly Color HeartOn = new Color(0.96f, 0.22f, 0.32f);
        public static readonly Color HeartOff = new Color(0.32f, 0.1f, 0.16f);
        public static readonly Color ChargeOn = new Color(1f, 0.86f, 0.32f);
        public static readonly Color ChargeOff = new Color(0.36f, 0.3f, 0.16f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.5f);
        public static readonly Color Wings = new Color(0.95f, 0.97f, 1f);
        public static readonly Color Light = new Color(1f, 0.9f, 0.5f);
        public static readonly Color DashReady = new Color(0.6f, 0.9f, 1f);
        public static readonly Color DashCharging = new Color(0.3f, 0.42f, 0.5f);
        public static readonly Color DashBack = new Color(0f, 0f, 0f, 0.5f);
        public static readonly Color Halo = new Color(1f, 0.85f, 0.4f);
        public static readonly Color Blade = new Color(0.95f, 0.97f, 1f);
        public static readonly Color BladeFire = new Color(1f, 0.65f, 0.25f);
        public static readonly Color Zeal = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Grip = new Color(0.85f, 0.75f, 0.6f);
        public static readonly Color Glide = new Color(0.8f, 0.9f, 1f);
        public static readonly Color Holy = new Color(1f, 0.97f, 0.75f);
        public static readonly Color Smite = new Color(1f, 0.75f, 0.55f);
        public static readonly Color PowerBarColor = new Color(1f, 1f, 1f, 0.8f);

        /// <summary>Where the ability icons start at most five hearts; more hearts push them right.</summary>
        public const float AbilityX = 58f;
        public const float HeartStep = 9f;
        public const float DashWidth = 20f;

        private RectTransform root;
        private RectTransform hearts;
        private RectTransform charges;
        private RectTransform abilitiesRow;
        private RectTransform dashRow;
        private RectTransform powersRow;
        private Image dashFill;
        private readonly Dictionary<Power, Image> powerBars = new Dictionary<Power, Image>();
        private readonly List<Ability> abilities = new List<Ability>();
        private readonly Dictionary<Power, float> powers = new Dictionary<Power, float>();

        public int Health { get; private set; } = 5;
        public int MaxHealth { get; private set; } = 5;
        public int Charges { get; private set; } = 3;
        public int MaxCharges { get; private set; } = 3;
        public float DashReadiness { get; private set; } = 1f;
        public IReadOnlyList<Ability> Abilities => abilities;
        public IReadOnlyDictionary<Power, float> Powers => powers;

        /// <summary>Makes the bar under a parent; the HUD calls this once.</summary>
        public static StatusBar Create(Transform parent)
        {
            var rect = UiKit.Rect(parent, "Status");
            UiKit.Place(rect, 0f, 0f, 120f, 28f);
            var bar = rect.gameObject.AddComponent<StatusBar>();
            bar.root = rect;
            bar.hearts = UiKit.Place(UiKit.Rect(rect, "Hearts"), 0f, 0f, 120f, 28f);
            bar.charges = UiKit.Place(UiKit.Rect(rect, "Charges"), 0f, 0f, 120f, 28f);
            bar.abilitiesRow = UiKit.Place(UiKit.Rect(rect, "Abilities"), 0f, 0f, 120f, 28f);
            bar.dashRow = UiKit.Place(UiKit.Rect(rect, "Dash"), 0f, 0f, 120f, 28f);
            bar.powersRow = UiKit.Place(UiKit.Rect(rect, "Powers"), 0f, 0f, 120f, 28f);
            bar.RebuildAll();
            return bar;
        }

        public void SetHealth(int current, int max)
        {
            Health = current;
            MaxHealth = Mathf.Max(max, 0);
            RebuildAll();
        }

        public void SetCharges(int current, int max)
        {
            Charges = current;
            MaxCharges = Mathf.Max(max, 0);
            RebuildCharges();
        }

        public void SetAbilities(IEnumerable<Ability> unlocked)
        {
            abilities.Clear();
            foreach (var ability in unlocked)
            {
                if (!abilities.Contains(ability))
                {
                    abilities.Add(ability);
                }
            }
            abilities.Sort();
            RebuildAbilities();
        }

        /// <summary>The dash cooldown, 0 to 1; only the fill moves.</summary>
        public void SetDashReadiness(float readiness)
        {
            DashReadiness = Mathf.Clamp01(readiness);
            if (dashFill == null)
            {
                return;
            }
            var fill = Mathf.Round(DashWidth * DashReadiness);
            dashFill.enabled = fill > 0f;
            dashFill.rectTransform.sizeDelta = new Vector2(fill, 3f);
            dashFill.color = DashReadiness >= 1f ? DashReady : DashCharging;
        }

        /// <summary>The running power-ups and their seconds left (infinity for the Halo).</summary>
        public void SetPowers(IReadOnlyDictionary<Power, float> running)
        {
            var sameSet = running.Count == powers.Count;
            if (sameSet)
            {
                foreach (var pair in running)
                {
                    if (!powers.ContainsKey(pair.Key))
                    {
                        sameSet = false;
                        break;
                    }
                }
            }
            powers.Clear();
            foreach (var pair in running)
            {
                powers[pair.Key] = pair.Value;
            }
            if (sameSet)
            {
                UpdatePowerBars();
            }
            else
            {
                RebuildPowers();
            }
        }

        /// <summary>The hearts take the space before the ability icons.</summary>
        public float AbilityStart() => Mathf.Max(AbilityX, 4f + MaxHealth * HeartStep + 4f);

        private void RebuildAll()
        {
            UiKit.Clear(hearts);
            for (var i = 0; i < MaxHealth; i++)
            {
                Pix(hearts, 4f + i * HeartStep, 4f, 7f, 7f, i < Health ? HeartOn : HeartOff, true);
            }
            RebuildCharges();
            RebuildAbilities();
            RebuildDash();
            RebuildPowers();
        }

        private void RebuildCharges()
        {
            UiKit.Clear(charges);
            for (var i = 0; i < MaxCharges; i++)
            {
                Pix(charges, 4f + i * 7f, 14f, 5f, 8f, i < Charges ? ChargeOn : ChargeOff, true);
            }
        }

        private void RebuildAbilities()
        {
            UiKit.Clear(abilitiesRow);
            var x = AbilityStart();
            foreach (var ability in abilities)
            {
                DrawAbility(ability, new Vector2(x, 4f));
                x += 12f;
            }
        }

        private void RebuildDash()
        {
            UiKit.Clear(dashRow);
            var at = new Vector2(AbilityStart(), 15f);
            Pix(dashRow, at.x + 1f, at.y + 1f, DashWidth, 3f, DashBack, false);
            dashFill = Pix(dashRow, at.x, at.y, DashWidth, 3f, DashReady, false);
            SetDashReadiness(DashReadiness);
        }

        private void RebuildPowers()
        {
            UiKit.Clear(powersRow);
            powerBars.Clear();
            var x = AbilityStart() + 26f;
            foreach (var power in HeroPowers.All)
            {
                if (!powers.ContainsKey(power))
                {
                    continue;
                }
                DrawPower(power, new Vector2(x, 15f));
                x += 12f;
            }
            UpdatePowerBars();
        }

        private void UpdatePowerBars()
        {
            foreach (var pair in powerBars)
            {
                var left = powers.TryGetValue(pair.Key, out var seconds) ? seconds : 0f;
                var fill = Mathf.Round(9f * Mathf.Clamp01(left / HeroPowers.Duration));
                pair.Value.enabled = fill > 0f;
                pair.Value.rectTransform.sizeDelta = new Vector2(fill, 1f);
            }
        }

        /// <summary>A 9x7 icon per ability, in boxes: the wings are two feathers, the light dash a streak with a spark, the wall grip a hand on a wall edge, the glide a wide open wing.</summary>
        private void DrawAbility(Ability ability, Vector2 at)
        {
            var row = abilitiesRow;
            switch (ability)
            {
                case Ability.Wings:
                    // Two feathers, each a triangle from the outer edge to the middle.
                    for (var i = 0; i < 4; i++)
                    {
                        Pix(row, at.x + i, at.y + 1 + i, 1f, 6f - i, Wings, true);
                        Pix(row, at.x + 8 - i, at.y + 1 + i, 1f, 6f - i, Wings, true);
                    }
                    break;
                case Ability.LightDash:
                    Pix(row, at.x, at.y + 3, 6f, 2f, Light, true);
                    Pix(row, at.x + 6, at.y + 1, 2f, 4f, ChargeOn, true);
                    break;
                case Ability.WallGrip:
                    Pix(row, at.x, at.y, 2f, 7f, DashCharging, true);
                    for (var i = 0; i < 3; i++)
                    {
                        Pix(row, at.x + 2 + i * 2, at.y + 1 + i, 2f, 2f, Grip, true);
                    }
                    break;
                case Ability.Glide:
                    // A wide open wing: a shallow chevron, two pixels thick at the tip.
                    for (var i = 0; i < 4; i++)
                    {
                        Pix(row, at.x + i, at.y + 4 - i, 1f, 2f, Glide, true);
                        Pix(row, at.x + 8 - i, at.y + 4 - i, 1f, 2f, Glide, true);
                    }
                    Pix(row, at.x + 4, at.y + 1, 1f, 2f, Glide, true);
                    break;
                case Ability.HolyLight:
                    Pix(row, at.x + 3, at.y, 3f, 7f, Holy, true);
                    Pix(row, at.x, at.y + 3, 7f, 3f, Holy, true);
                    Pix(row, at.x + 4, at.y + 3, 1f, 1f, Color.white, false);
                    break;
                case Ability.Smite:
                    Pix(row, at.x + 3, at.y, 3f, 5f, Smite, true);
                    // The hammer head, a triangle pointing down.
                    Pix(row, at.x + 1, at.y + 4, 7f, 1f, Smite, true);
                    Pix(row, at.x + 2, at.y + 5, 5f, 1f, Smite, true);
                    Pix(row, at.x + 3, at.y + 6, 3f, 1f, Smite, true);
                    Pix(row, at.x, at.y + 7, 9f, 1f, DashBack, false);
                    break;
            }
        }

        /// <summary>A 9x7 icon per power-up with a bar under it: the Halo a ring, the Lit Blade a short sword, Zeal two chevrons.</summary>
        private void DrawPower(Power power, Vector2 at)
        {
            var row = powersRow;
            switch (power)
            {
                case Power.Halo:
                    Pix(row, at.x + 1, at.y + 1, 7f, 4f, Halo, true);
                    Pix(row, at.x + 3, at.y + 2, 3f, 2f, DashBack, false);
                    break;
                case Power.Blade:
                    for (var i = 0; i < 5; i++)
                    {
                        Pix(row, at.x + 2 + i, at.y + 5 - i, 1f, 1f, Blade, true);
                    }
                    Pix(row, at.x + 1, at.y + 5, 3f, 1f, BladeFire, false);
                    Pix(row, at.x + 6, at.y, 1f, 1f, BladeFire, false);
                    break;
                case Power.Zeal:
                    foreach (var ox in new[] { 1f, 5f })
                    {
                        for (var i = 0; i < 3; i++)
                        {
                            Pix(row, at.x + ox + i, at.y + i, 1f, 1f, Zeal, true);
                            Pix(row, at.x + ox + i, at.y + 5 - i, 1f, 1f, Zeal, true);
                        }
                    }
                    break;
            }
            if (float.IsPositiveInfinity(powers[power]))
            {
                return;
            }
            Pix(row, at.x + 1, at.y + 9, 9f, 1f, DashBack, false);
            powerBars[power] = Pix(row, at.x, at.y + 8, 9f, 1f, PowerBarColor, false);
        }

        /// <summary>One box at (x, y) from the bar's top left, with a one-pixel shadow under it when asked.</summary>
        private static Image Pix(Transform parent, float x, float y, float w, float h, Color color, bool shadow)
        {
            if (shadow)
            {
                UiKit.Place(UiKit.Box(parent, "Shadow", Shadow).rectTransform, x + 1f, y + 1f, w, h);
            }
            var image = UiKit.Box(parent, "Pix", color);
            UiKit.Place(image.rectTransform, x, y, w, h);
            return image;
        }
    }
}
