using System.Collections.Generic;
using Descender.World;
using NUnit.Framework;

namespace Descender.Tests
{
    public class RegionTileDataTests
    {
        [Test]
        public void WritesAndReadsLayersAndKinds()
        {
            var layers = new Dictionary<string, RegionTileData.Cell[]>
            {
                ["Terrain"] = new[]
                {
                    new RegionTileData.Cell { X = 0, Y = 0, Tile = 5 },
                    new RegionTileData.Cell { X = 479, Y = 50, Tile = 255 },
                },
                ["Glow"] = new RegionTileData.Cell[0],
            };
            var kinds = new int[256];
            kinds[5] = (int)TileKind.Solid;
            kinds[7] = (int)TileKind.Hazard;

            var data = RegionTileData.Parse(RegionTileData.Write(layers, kinds));

            Assert.That(data.Layers["Terrain"].Length, Is.EqualTo(2));
            Assert.That(data.Layers["Terrain"][1].X, Is.EqualTo(479));
            Assert.That(data.Layers["Terrain"][1].Y, Is.EqualTo(50));
            Assert.That(data.Layers["Terrain"][1].Tile, Is.EqualTo(255));
            Assert.That(data.Layers["Glow"].Length, Is.EqualTo(0));
            Assert.That(data.KindOf(5), Is.EqualTo(TileKind.Solid));
            Assert.That(data.KindOf(7), Is.EqualTo(TileKind.Hazard));
            Assert.That(data.KindOf(6), Is.EqualTo(TileKind.None));
            Assert.That(data.KindOf(999), Is.EqualTo(TileKind.None));
        }

        [Test]
        public void EmptyTextIsAnEmptyRegion()
        {
            var data = RegionTileData.Parse("");
            Assert.That(data.Layers.Count, Is.EqualTo(0));
            Assert.That(data.Kinds.Length, Is.EqualTo(0));
        }

        [Test]
        public void RejectsAnotherKindOfFile()
        {
            Assert.Throws<System.FormatException>(() => RegionTileData.Parse("hello\nlayer x 0 \n"));
        }

        [Test]
        public void FringeHashMatchesGodot()
        {
            // The values EdgeDecor._hash gives in Godot for the same cells, so the fringes land where they did.
            Assert.That(RegionFringes.Hash(0, 0, 't'), Is.EqualTo(1240142170L));
            Assert.That(RegionFringes.Hash(12, 34, 'b'), Is.EqualTo(563120564L));
            Assert.That(RegionFringes.Hash(479, 500, 'r'), Is.EqualTo(1501801510L));
        }
    }
}
