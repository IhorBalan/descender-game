using System.Collections.Generic;
using Descender.Economy;
using Descender.Player;
using Descender.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Health, healing charges, unlocked abilities, the dash cooldown, the purse,
    /// the level with its experience bar, the boss bar and short messages, on one
    /// canvas built in Awake. Values arrive through the setters (the game session
    /// wires the hero's events to them); only the dash readiness and the power-up
    /// timers are read every frame from <see cref="Hero"/>, as they drain. Messages
    /// take `{action}` tokens for the device in hand (see <see cref="Prompts"/>).
    /// Port of scripts/hud.gd; lives in Main.unity.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public const int SortOrder = 5;
        public const float BossBarWidth = 160f;
        public const float XpBarWidth = 40f;
        public const float DefaultMessageTime = 2.5f;

        /// <summary>The boss bar turns red at and below this share of its health (Boss.PHASE_TWO_AT).</summary>
        public const float BossHurtAt = 0.5f;

        public static readonly Color BossBarBack = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color BossBarFill = new Color(1f, 0.85f, 0.45f);
        public static readonly Color BossBarHurt = new Color(0.9f, 0.3f, 0.3f);
        public static readonly Color XpBarBack = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color XpBarFill = new Color(0.75f, 0.85f, 1f);
        public static readonly Color LevelColor = new Color(0.85f, 0.9f, 1f);

        /// <summary>The level line brightens while a skill point waits to be spent.</summary>
        public static readonly Color PointColor = new Color(1f, 0.95f, 0.7f);

        /// <summary>The line queued after a level's name on a new game.</summary>
        public const string ControlsHint =
            "{move} move   {jump} jump   {attack} attack   {dash} dash   {cast} cast   {heal} heal   {interact} talk";

        private Canvas canvas;
        private StatusBar status;
        private TextMeshProUGUI purseLabel;
        private TextMeshProUGUI levelLabel;
        private Image xpFill;
        private RectTransform bossBar;
        private TextMeshProUGUI bossName;
        private Image bossFill;
        private PromptLabel message;
        private TextMeshProUGUI messageText;
        private float messageLeft;
        private readonly Queue<(string text, float duration)> queue = new Queue<(string, float)>();
        private readonly Dictionary<Power, float> powers = new Dictionary<Power, float>();

        /// <summary>The hero whose dash and power-ups the bar follows; null shows the bar at rest.</summary>
        public IHeroMenuActions Hero { get; set; }

        public StatusBar Status => status;
        public bool BossShown => bossBar != null && bossBar.gameObject.activeSelf;
        public bool MessageShown => messageText != null && messageText.gameObject.activeSelf;
        public string MessageTemplate => message != null ? message.Template : "";

        /// <summary>Hides or shows the whole HUD (behind a full-screen page, during the title).</summary>
        public bool Visible
        {
            get => canvas != null && canvas.enabled;
            set
            {
                if (canvas != null)
                {
                    canvas.enabled = value;
                }
            }
        }

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            canvas = UiKit.MakeCanvas("HudCanvas", SortOrder, transform);
            var root = canvas.GetComponent<RectTransform>();
            status = StatusBar.Create(root);

            purseLabel = UiKit.Label(root, "Purse", "", UiTheme.SmallFontSize, Inventory.Currencies[0].Color, TextAlignmentOptions.Right, true);
            UiKit.Place(purseLabel.rectTransform, 196f, 3f, 120f, 10f);
            levelLabel = UiKit.Label(root, "Level", "", UiTheme.SmallFontSize, LevelColor, TextAlignmentOptions.Right, true);
            UiKit.Place(levelLabel.rectTransform, 196f, 12f, 120f, 10f);
            var xpBack = UiKit.Box(root, "XpBar", XpBarBack);
            UiKit.Place(xpBack.rectTransform, 316f - XpBarWidth, 23f, XpBarWidth, 3f);
            xpFill = UiKit.Box(xpBack.transform, "Fill", XpBarFill);
            UiKit.Place(xpFill.rectTransform, 1f, 1f, 0f, 1f);

            bossBar = UiKit.Rect(root, "BossBar");
            UiKit.Place(bossBar, (UiTheme.Width - BossBarWidth) / 2f, 140f, BossBarWidth, 16f);
            bossName = UiKit.Label(bossBar, "Name", "", UiTheme.SmallFontSize, UiTheme.Text, TextAlignmentOptions.Center, true);
            UiKit.Place(bossName.rectTransform, 0f, 0f, BossBarWidth, 10f);
            var bossBack = UiKit.Box(bossBar, "Back", BossBarBack);
            UiKit.Place(bossBack.rectTransform, 0f, 11f, BossBarWidth, 4f);
            bossFill = UiKit.Box(bossBar, "Fill", BossBarFill);
            UiKit.Place(bossFill.rectTransform, 1f, 12f, BossBarWidth - 2f, 2f);
            bossBar.gameObject.SetActive(false);

            messageText = UiKit.Label(root, "Message", "", UiTheme.SmallFontSize, UiTheme.Text, TextAlignmentOptions.Bottom, true);
            UiKit.Place(messageText.rectTransform, 4f, 158f, 312f, 18f);
            messageText.textWrappingMode = TextWrappingModes.Normal;
            message = messageText.gameObject.AddComponent<PromptLabel>();
            messageText.gameObject.SetActive(false);

            SetPurse();
            SetProgress();
        }

        private void Update()
        {
            if (Hero != null)
            {
                status.SetDashReadiness(Hero.DashReadiness);
                // The power-up bars drain every frame while one is running.
                powers.Clear();
                foreach (var power in HeroPowers.All)
                {
                    if (Hero.HasPower(power))
                    {
                        powers[power] = Hero.PowerTimeLeft(power);
                    }
                }
                if (powers.Count > 0 || status.Powers.Count > 0)
                {
                    status.SetPowers(powers);
                }
            }
            if (messageLeft > 0f)
            {
                messageLeft -= Time.deltaTime;
                if (messageLeft <= 0f)
                {
                    messageText.gameObject.SetActive(false);
                    if (queue.Count > 0)
                    {
                        var next = queue.Dequeue();
                        ShowMessage(next.text, next.duration);
                    }
                }
            }
        }

        public void SetHealth(int current, int max) => status.SetHealth(current, max);

        public void SetCharges(int current, int max) => status.SetCharges(current, max);

        public void SetAbilities(IEnumerable<Ability> abilities) => status.SetAbilities(abilities);

        public void SetPowers(IReadOnlyDictionary<Power, float> running) => status.SetPowers(running);

        /// <summary>Writes the purse from the pack (see Inventory).</summary>
        public void SetPurse() => purseLabel.text = MenuText.PurseText();

        /// <summary>Writes the level and fills the experience bar from the skills; the line names a point waiting.</summary>
        public void SetProgress()
        {
            levelLabel.text = MenuText.HudLevelText();
            levelLabel.color = Skills.PointsLeft() > 0 ? PointColor : LevelColor;
            var (into, needed) = Skills.LevelProgress();
            var share = needed > 0 ? Mathf.Clamp01((float)into / needed) : 1f;
            xpFill.rectTransform.sizeDelta = new Vector2(Mathf.Round((XpBarWidth - 2f) * share), 1f);
        }

        /// <summary>Shows the boss bar with the boss's name and health.</summary>
        public void ShowBoss(string bossName, int current, int max)
        {
            this.bossName.text = bossName;
            SetBossHealth(current, max);
            bossBar.gameObject.SetActive(true);
        }

        public void SetBossHealth(int current, int max)
        {
            var share = Mathf.Clamp01((float)current / Mathf.Max(max, 1));
            bossFill.rectTransform.sizeDelta = new Vector2(Mathf.Round((BossBarWidth - 2f) * share), 2f);
            bossFill.color = share <= BossHurtAt ? BossBarHurt : BossBarFill;
        }

        public void HideBoss() => bossBar.gameObject.SetActive(false);

        /// <summary>Clears the message line and whatever was queued behind it.</summary>
        public void HideMessage()
        {
            queue.Clear();
            messageText.gameObject.SetActive(false);
            messageLeft = 0f;
        }

        /// <summary>
        /// A negative duration keeps the message until the next one. Showing a message
        /// drops whatever was queued behind the previous one.
        /// </summary>
        public void ShowMessage(string text, float duration = DefaultMessageTime)
        {
            queue.Clear();
            messageText.gameObject.SetActive(true);
            message.Template = text;
            messageLeft = duration > 0f ? duration : 0f;
        }

        /// <summary>Shows the message once the current timed one has run out, or at once when nothing timed is showing.</summary>
        public void QueueMessage(string text, float duration = DefaultMessageTime)
        {
            if (messageLeft > 0f)
            {
                queue.Enqueue((text, duration));
            }
            else
            {
                ShowMessage(text, duration);
            }
        }
    }
}
