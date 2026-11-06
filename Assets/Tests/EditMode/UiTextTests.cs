using System.Collections.Generic;
using Descender.Economy;
using Descender.Save;
using Descender.Story;
using Descender.UI;
using NUnit.Framework;

namespace Descender.Tests
{
    /// <summary>The pure parts of the in-game UI: prompt rendering, menu lines, the typing clock and the console registry.</summary>
    public class UiTextTests
    {
        [SetUp]
        public void Reset()
        {
            StoryFlags.Reset();
            Inventory.Reset();
            Skills.Reset();
        }

        [Test]
        public void PromptsRenderWrapsNamesInTheKeyColourAndLeavesUnknownTokens()
        {
            var rendered = Prompts.Render(
                "{navigate} pick   {ui_accept} chooses   {nothing}",
                token => token == "nothing" ? null : token.ToUpperInvariant()
            );
            var key = UiTheme.KeyColorHex;
            Assert.That(
                rendered,
                Is.EqualTo($"<color={key}>NAVIGATE</color> pick   <color={key}>UI_ACCEPT</color> chooses   {{nothing}}")
            );
            Assert.That(Prompts.Plain(rendered), Is.EqualTo("NAVIGATE pick   UI_ACCEPT chooses   {nothing}"));
            Assert.That(Prompts.Render("", _ => "x"), Is.EqualTo(""));
        }

        [Test]
        public void PromptsNamePadButtonsByLayout()
        {
            Assert.That(Prompts.PadName("<Gamepad>/buttonSouth", PromptLayout.Xbox), Is.EqualTo("A"));
            Assert.That(Prompts.PadName("<Gamepad>/buttonSouth", PromptLayout.PlayStation), Is.EqualTo("Cross"));
            Assert.That(Prompts.PadName("<Gamepad>/leftShoulder", PromptLayout.PlayStation), Is.EqualTo("L1"));
            Assert.That(Prompts.PadName("<Gamepad>/dpad/left", PromptLayout.Xbox), Is.EqualTo("D-Left"));
            Assert.That(Prompts.IsPlayStation("Sony DualShock 4"), Is.True);
            Assert.That(Prompts.IsPlayStation("Xbox Wireless Controller"), Is.False);
            Assert.That(Prompts.IsPlayStation(null), Is.False);
        }

        [Test]
        public void PurseWornAndPackLinesReadAsTheGodotMenusWroteThem()
        {
            Assert.That(MenuText.PurseText(), Is.EqualTo("0 light   0 tally bones"));
            Inventory.AddCurrency("light", 12);
            Inventory.AddCurrency("bones", 1);
            Assert.That(MenuText.PurseText(), Is.EqualTo("12 light   1 tally bone"));
            Assert.That(MenuText.WornText(), Is.EqualTo("Sword: -   Armour: -   Charm: -"));
            Inventory.Add("hinge_blade");
            Inventory.Add("flask", 3);
            Assert.That(MenuText.PackLine("flask"), Is.EqualTo("  Basin Flask x3"));
            Assert.That(MenuText.PackLine("hinge_blade"), Does.StartWith("  Hinge Blade"));
            Inventory.Equip("hinge_blade");
            Assert.That(MenuText.PackLine("hinge_blade"), Does.StartWith(MenuText.WornMark + "Hinge Blade"));
            Assert.That(MenuText.PackLine("hinge_blade"), Does.Contain("reach x1.4"));
            Assert.That(MenuText.WornText(), Is.EqualTo("Sword: Hinge Blade   Armour: -   Charm: -"));
        }

        [Test]
        public void KeptThingsListFlagsAndUnforgedIron()
        {
            Assert.That(MenuText.KeptThings(), Is.Empty);
            StoryFlags.Set("item:scarf");
            StoryFlags.Set("upgrade:iron:world_1:3");
            StoryFlags.Set("upgrade:iron:world_1:7");
            Assert.That(MenuText.KeptThings(), Is.EqualTo(new List<string> { "Thorne's red scarf", "Church iron x2" }));
            StoryFlags.Set("sword:1");
            StoryFlags.Set("sword:2");
            Assert.That(MenuText.IronCarried(), Is.EqualTo(1));
        }

        [Test]
        public void ShopLinesShowPricesThenSold()
        {
            var shop = Shops.Get("pell");
            var sword = shop.Stock[0];
            Assert.That(
                MenuText.ShopLine("pell", sword),
                Is.EqualTo($"{MenuText.Pad("Church Iron Sword", 22)}   120 light")
            );
            StoryFlags.Set(Shops.BoughtFlag("pell", "iron_sword"));
            Assert.That(MenuText.ShopLine("pell", sword), Does.EndWith("   sold"));
            Assert.That(MenuText.WareNote("flask"), Is.EqualTo(Items.Description("flask")));
            Assert.That(MenuText.WareNote("hinge_blade"), Does.EndWith("(reach x1.4)"));
        }

        [Test]
        public void SkillAndCampLinesFollowTheLevel()
        {
            Assert.That(MenuText.SkillStatusText(), Is.EqualTo("Level 1   0 / 25 experience"));
            Assert.That(MenuText.SkillPointsText(), Is.EqualTo("No skill point to spend: the next level gives one"));
            Assert.That(
                MenuText.SkillLine("vigour"),
                Is.EqualTo($"{MenuText.Pad("Vigour", 14)} {MenuText.Pad("---", 4)}   +1 heart per rank")
            );
            Assert.That(MenuText.SkillLine("deep_draught"), Does.EndWith("   level 3"));
            Assert.That(MenuText.CampStatusText(2, 5), Is.EqualTo("Checkpoint 3 of 5      Level 1"));
            Assert.That(MenuText.HudLevelText(), Is.EqualTo("Lv 1"));
            Skills.AddXp(25);
            Assert.That(MenuText.SkillPointsText(), Is.EqualTo("1 skill point to spend"));
            Assert.That(MenuText.CampStatusText(-1, 0), Is.EqualTo("Level 2      1 skill point to spend"));
            Assert.That(MenuText.HudLevelText(), Is.EqualTo("Lv 2   1 skill point"));
            Assert.That(Skills.Raise("vigour"), Is.EqualTo(""));
            Assert.That(MenuText.SkillLine("vigour"), Does.StartWith($"{MenuText.Pad("Vigour", 14)} I-- "));
        }

        [Test]
        public void TyperTypesRestsOnPunctuationAndBlips()
        {
            var typer = new DialogueTyper();
            typer.Start("Hi. There, you");
            Assert.That(typer.Visible, Is.EqualTo(0));
            Assert.That(typer.IsDone, Is.False);
            var blipped = typer.Tick(0.1f, 45f);
            Assert.That(typer.Visible, Is.EqualTo(4));
            Assert.That(blipped, Is.True, "three or more new characters blip");
            Assert.That(typer.IsPausing, Is.True, "a full stop rests the typing");
            typer.Tick(DialogueTyper.PauseSentence, 45f);
            Assert.That(typer.Visible, Is.EqualTo(4), "the rest eats the tick");
            typer.Tick(1f, 45f);
            Assert.That(typer.Visible, Is.EqualTo(14));
            Assert.That(typer.IsDone, Is.True);
            Assert.That(typer.IsPausing, Is.False);
        }

        [Test]
        public void TyperInstantSpeedShowsTheWholePageAndSkipFinishes()
        {
            var typer = new DialogueTyper();
            typer.Start("Return to the Basin.");
            Assert.That(typer.Tick(0.016f, 0f), Is.True);
            Assert.That(typer.IsDone, Is.True);
            typer.Start("No.");
            typer.Skip();
            Assert.That(typer.Visible, Is.EqualTo(3));
            Assert.That(typer.Tick(0.1f, 45f), Is.False);
        }

        [Test]
        public void ConsoleRunsHelpUnknownAndRegisteredCommands()
        {
            var commands = new ConsoleCommands();
            var cleared = 0;
            commands.Cleared += () => cleared++;
            commands.Register("echo", args => string.Join(" ", args), "echo words", "Says the words back.");
            commands.Register("quiet", _ => null);
            Assert.That(commands.Run("echo a  b"), Is.EqualTo("a b"));
            Assert.That(commands.Run("quiet"), Is.EqualTo(ConsoleCommands.DoneText));
            Assert.That(commands.Run("   "), Is.EqualTo(""));
            Assert.That(commands.Run("nope"), Does.StartWith("Unknown command 'nope'"));
            Assert.That(ConsoleCommands.IsError(commands.Run("nope")), Is.True);
            Assert.That(ConsoleCommands.IsError("Usage: show_boxes on|off"), Is.True);
            Assert.That(commands.Run("help echo"), Is.EqualTo("echo words\n  Says the words back."));
            Assert.That(commands.Run("help"), Does.Contain("clear"));
            Assert.That(commands.Names(), Is.EqualTo(new List<string> { "clear", "echo", "help", "quiet" }));
            commands.Run("clear");
            Assert.That(cleared, Is.EqualTo(1));
            commands.Unregister("echo");
            Assert.That(commands.Has("echo"), Is.False);
        }
    }
}
