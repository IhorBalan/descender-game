using Descender.EditorTools.Importers;
using NUnit.Framework;

namespace Descender.Tests.EditMode
{
    public class PropPrefabImporterTests
    {
        [TestCase("blossom_tree", "Blossom Tree")]
        [TestCase("lantern", "Lantern")]
        [TestCase("angel_statue_2", "Angel Statue 2")]
        public void ToTitleCapitalisesEachWord(string stem, string expected)
        {
            Assert.That(PropPrefabImporter.ToTitle(stem), Is.EqualTo(expected));
        }
    }
}
