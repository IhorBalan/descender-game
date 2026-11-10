using System.Reflection;
using Descender.Player;
using Descender.Save;
using Descender.UI;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The dev console's `tune` and `ability` on a tuning made for the test: fields by
    /// reflection, showing, setting, resetting, what cannot be saved, forced abilities
    /// and the panel's text.
    /// </summary>
    public class LiveTuningTests
    {
        private PlayerTuning asset;
        private PlayerTuning live;

        [SetUp]
        public void SetUp()
        {
            LiveTuning.Forget();
            asset = ScriptableObject.CreateInstance<PlayerTuning>();
            live = LiveTuning.Begin(asset);
        }

        [TearDown]
        public void TearDown()
        {
            LiveTuning.Forget();
            Object.DestroyImmediate(asset);
        }

        private static string Tune(params string[] words) => TuneCommands.Run(words);

        private static string Force(params string[] words) => TuneCommands.Ability(words);

        [Test]
        public void TheFieldsAreFoundByReflectionUnderTheirHeaders()
        {
            var expected = 0;
            foreach (var info in typeof(PlayerTuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var type = info.FieldType;
                if (type == typeof(float) || type == typeof(int) || type == typeof(bool) || type == typeof(Vector2))
                {
                    expected++;
                }
            }
            Assert.That(LiveTuning.Fields.Count, Is.EqualTo(expected));
            Assert.That(expected, Is.GreaterThan(50), "every number on the tuning is tunable");
            Assert.That(LiveTuning.Fields[0].Name, Is.EqualTo("runSpeed"), "in the order they are declared");
            Assert.That(LiveTuning.Find("jumpHeight").Group, Is.EqualTo("Jump (px, s)"));
            Assert.That(LiveTuning.Find("JUMPHEIGHT"), Is.SameAs(LiveTuning.Find("jumpHeight")));
            Assert.That(LiveTuning.Find("dashSpeed").Group, Is.EqualTo("Dash"));
            Assert.That(LiveTuning.Find("hitFlashFrames").Group, Is.EqualTo("Feel"));
            Assert.That(LiveTuning.Find("PixelsPerUnit"), Is.Null, "a constant is not a tuning field");
            Assert.That(LiveTuning.Groups.Count, Is.EqualTo(7));
        }

        [Test]
        public void TheWorkingCopyIsWhatAHeroOfThatTuningPlaysWith()
        {
            Assert.That(live, Is.Not.SameAs(asset));
            Assert.That(LiveTuning.Working, Is.SameAs(live));
            Assert.That(LiveTuning.Source, Is.SameAs(asset));
            Assert.That(LiveTuning.For(asset), Is.SameAs(live), "the next hero made from the asset gets the copy");
            Assert.That(LiveTuning.Begin(live), Is.SameAs(live));
            Assert.That(LiveTuning.Begin(asset), Is.SameAs(live));
            var other = ScriptableObject.CreateInstance<PlayerTuning>();
            Assert.That(LiveTuning.For(other), Is.SameAs(other));
            Object.DestroyImmediate(other);
        }

        [Test]
        public void TuneShowsAFieldAndSetsItOnTheCopyOnly()
        {
            Assert.That(Tune("jumpHeight"), Does.StartWith("jumpHeight 72   [Jump (px, s)]"));
            Assert.That(Tune("jumpHeight", "90"), Is.EqualTo("jumpHeight 90 (asset 72), was 72"));
            Assert.That(live.jumpHeight, Is.EqualTo(90f));
            Assert.That(asset.jumpHeight, Is.EqualTo(72f), "the asset is not touched");
            Assert.That(Tune("jumpheight"), Does.StartWith("jumpHeight 90 (asset 72)"));
            Assert.That(Tune("coyoteTime", "0.25"), Is.EqualTo("coyoteTime 0.25 (asset 0.1), was 0.1"));
            Assert.That(live.JumpVelocity, Is.EqualTo(2f * 90f / live.jumpTimeToApex).Within(0.001f));
        }

        [Test]
        public void AValueOfTheWrongKindIsRefusedWithTheUsage()
        {
            Assert.That(Tune("runSpeed", "fast"), Is.EqualTo("Usage: tune runSpeed a number"));
            Assert.That(Tune("runSpeed", "1", "2"), Is.EqualTo("Usage: tune runSpeed a number"));
            Assert.That(Tune("attackDamage", "1.5"), Is.EqualTo("Usage: tune attackDamage a whole number"));
            Assert.That(Tune("attackOffset", "24"), Is.EqualTo("Usage: tune attackOffset x y"));
            Assert.That(ConsoleCommands.IsError(Tune("runSpeed", "NaN")), Is.True);
            Assert.That(LiveTuning.Changed(), Is.Empty);
            Assert.That(Tune("jumpSpeed", "3"), Does.StartWith("No tuning field 'jumpSpeed'"));
        }

        [Test]
        public void AWholeNumberAndAPairAreSet()
        {
            Assert.That(Tune("attackDamage", "3"), Is.EqualTo("attackDamage 3 (asset 1), was 1"));
            Assert.That(live.attackDamage, Is.EqualTo(3));
            Assert.That(Tune("attackOffset", "24", "16"), Is.EqualTo("attackOffset 24 16 (asset 20 18), was 20 18"));
            Assert.That(live.attackOffset, Is.EqualTo(new Vector2(24f, 16f)));
            Tune("attackOffset", "22.5,-4");
            Assert.That(live.attackOffset, Is.EqualTo(new Vector2(22.5f, -4f)));
        }

        [Test]
        public void TuneWithATextShowsTheFieldsThatMatch()
        {
            var dash = Tune("dash");
            Assert.That(dash, Does.Contain("dashSpeed 260"));
            Assert.That(dash, Does.Contain("dashCooldown 0.4"));
            Assert.That(dash, Does.Contain("zealSpeedMultiplier 1.25"), "the group's fields too");
            Assert.That(dash, Does.Not.Contain("runSpeed"));
            Assert.That(Tune("nothinglikethis"), Does.StartWith("No tuning field or group matches"));
        }

        [Test]
        public void TuneListNamesEveryField()
        {
            var list = Tune("list");
            foreach (var field in LiveTuning.Fields)
            {
                Assert.That(list, Does.Contain(field.Name));
            }
            Assert.That(list.Split('\n').Length, Is.EqualTo(LiveTuning.Groups.Count));
        }

        [Test]
        public void TheSummaryNamesWhatWasTuned()
        {
            Assert.That(Tune(), Does.StartWith(LiveTuning.NothingTunedText));
            Tune("runSpeed", "120");
            Tune("dashTime", "0.2");
            var summary = Tune();
            Assert.That(summary, Does.StartWith("Tuned: runSpeed 120 (asset 100), dashTime 0.2 (asset 0.14)"));
            Assert.That(summary, Does.Contain("Dash [7]"));
        }

        [Test]
        public void ResetPutsTheAssetsValuesBack()
        {
            Tune("runSpeed", "120");
            Tune("dashTime", "0.2");
            Assert.That(Tune("reset", "runSpeed"), Is.EqualTo("runSpeed 100"));
            Assert.That(live.runSpeed, Is.EqualTo(100f));
            Assert.That(live.dashTime, Is.EqualTo(0.2f));
            Assert.That(Tune("reset"), Is.EqualTo("Put 1 back to the asset's."));
            Assert.That(live.dashTime, Is.EqualTo(0.14f));
            Assert.That(LiveTuning.Changed(), Is.Empty);
            Assert.That(Tune("reset"), Is.EqualTo(LiveTuning.NothingTunedText));
            Assert.That(Tune("reset", "nothing"), Does.StartWith("No tuning field 'nothing'"));
        }

        [Test]
        public void SaveWritesOnlyToATuningThatIsAnAsset()
        {
            Assert.That(Tune("save"), Is.EqualTo(LiveTuning.NothingTunedText));
            Tune("runSpeed", "120");
            Assert.That(Tune("save"), Is.EqualTo(LiveTuning.NotAnAssetText));
            Assert.That(asset.runSpeed, Is.EqualTo(100f));
            Assert.That(live.runSpeed, Is.EqualTo(120f), "what was tuned stays tuned");
        }

        [Test]
        public void WithoutAHeroTuneSaysSo()
        {
            LiveTuning.Forget();
            Assert.That(Tune("runSpeed"), Is.EqualTo(TuneCommands.NoHeroText));
            Assert.That(TuneCommands.Tune(new[] { "runSpeed" }), Is.EqualTo(TuneCommands.NoHeroText));
        }

        [Test]
        public void AForcedAbilityWinsOverWhatHeOwns()
        {
            Assert.That(LiveTuning.Has(Ability.Wings, false), Is.False);
            Assert.That(Force("wings", "on"), Is.EqualTo("wings on (forced, not his)"));
            Assert.That(LiveTuning.Has(Ability.Wings, false), Is.True);
            Assert.That(LiveTuning.Has(Ability.Glide, false), Is.False, "the others are as he owns them");
            Force("Wings", "off");
            Assert.That(LiveTuning.Has(Ability.Wings, true), Is.False);
            Assert.That(Force("wings", "reset"), Is.EqualTo("wings off"));
            Assert.That(LiveTuning.Has(Ability.Wings, true), Is.True);

            Force("all", "on");
            foreach (Ability ability in System.Enum.GetValues(typeof(Ability)))
            {
                Assert.That(LiveTuning.Has(ability, false), Is.True, ability.ToString());
            }
            Assert.That(Force("reset"), Does.StartWith("wings off, lightdash off"));
            Assert.That(LiveTuning.Has(Ability.Smite, false), Is.False);
        }

        [Test]
        public void AnAbilityThatDoesNotExistOrAWordThatIsNotOnOrOffIsRefused()
        {
            Assert.That(Force("fly", "on"), Does.StartWith("No ability 'fly'. Abilities: wings, lightdash"));
            Assert.That(Force("3", "on"), Does.StartWith("No ability '3'"));
            Assert.That(Force("wings", "maybe"), Is.EqualTo(TuneCommands.AbilityUsage));
            Assert.That(Force("wings", "on", "now"), Is.EqualTo(TuneCommands.AbilityUsage));
            Assert.That(LiveTuning.IsForced(Ability.Wings, out _), Is.False);
        }

        [Test]
        public void ThePanelShowsAGroupWithTheChangedValuesMarked()
        {
            Tune("jumpHeight", "90");
            var jump = TuningOverlay.GroupStarting("jump");
            Assert.That(jump, Is.EqualTo(1));
            var text = TuningOverlay.Text(live, jump);
            Assert.That(text, Does.StartWith("Jump (px, s)  2/7\n"));
            Assert.That(text, Does.Contain("<color=#FFD966>* jumpHeight 90 (asset 72)</color>"));
            Assert.That(text, Does.Contain("\n  coyoteTime 0.1\n"));
            Assert.That(text, Does.Not.Contain("runSpeed"), "one group at a time");
            Assert.That(TuningOverlay.Text(asset, jump), Does.Contain("\n  jumpHeight 72\n"), "the asset has no marks");
            Assert.That(TuningOverlay.Text(null, 0), Is.EqualTo(TuneCommands.NoHeroText));
            Assert.That(TuningOverlay.GroupStarting("swim"), Is.EqualTo(-1));
        }
    }
}
