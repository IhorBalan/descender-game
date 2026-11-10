using Descender.UI;
using NUnit.Framework;

namespace Descender.Tests
{
    /// <summary>
    /// The gilded list rows set MenuText's space-padded columns apart: the first
    /// column on the left, the rest against the right end; skill rows take the
    /// gilded colours.
    /// </summary>
    public class MenuRowTests
    {
        [TestCase("  Hinge Blade              reach x1.4", "  Hinge Blade", "reach x1.4")]
        [TestCase("Old Sword   120 light", "Old Sword", "120 light")]
        [TestCase("Heart          ---   level 3", "Heart", "---   level 3")]
        [TestCase("  Basin Flask x3", "  Basin Flask x3", "")]
        [TestCase("Two  spaces stay", "Two  spaces stay", "")]
        [TestCase("Trailing   ", "Trailing", "")]
        public void ColumnsSplitAtTheFirstRunOfThreeSpaces(string text, string first, string rest)
        {
            UiScrollList.SplitColumns(text, out var left, out var right);
            Assert.That(left, Is.EqualTo(first));
            Assert.That(right, Is.EqualTo(rest));
        }

        [Test]
        public void SkillRowsTakeTheGildedColours()
        {
            Assert.That(SkillScreen.Gilded(UiTheme.Highlight), Is.EqualTo(TitleKit.Gold), "open to a point");
            Assert.That(SkillScreen.Gilded(UiTheme.Locked), Is.EqualTo(TitleKit.Faded), "locked");
            Assert.That(SkillScreen.Gilded(UiTheme.Item), Is.EqualTo(TitleKit.Parchment), "plain");
        }
    }
}
