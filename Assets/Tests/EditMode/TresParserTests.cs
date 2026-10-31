using Descender.EditorTools.Importers;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    public class TresParserTests
    {
        private const string Sample =
            @"[gd_resource type=""Resource"" script_class=""WalkerProfile"" format=3]

[ext_resource type=""Script"" path=""res://scripts/enemies/walker_profile.gd"" id=""1_edfid""]

[resource]
script = ExtResource(""1_edfid"")
body = Vector2(18, 24)
speed = 32.5
health = 3
walk_anim = ""run""
faces_left = false
armour = true
loot = {
""bone_chance"": 0.08,
""light"": [3, 5],
""unique"": ""wardens_halo"",
""xp"": 3
}
puff = Color(0.55, 0.15, 0.15, 1)
";

        [Test]
        public void ReadsEveryKindOfValue()
        {
            var tres = TresParser.Parse(Sample);
            Assert.AreEqual("WalkerProfile", tres.ScriptClass);
            Assert.AreEqual(new Vector2(18, 24), tres.Vector("body", Vector2.zero));
            Assert.AreEqual(32.5f, tres.Float("speed", 0f));
            Assert.AreEqual(3, tres.Int("health", 0));
            Assert.AreEqual("run", tres.String("walk_anim", ""));
            Assert.IsFalse(tres.Bool("faces_left", true));
            Assert.IsTrue(tres.Bool("armour", false));
            Assert.AreEqual(new Color(0.55f, 0.15f, 0.15f, 1f), tres.ColorOf("puff", Color.black));
            var loot = tres.Dict("loot");
            Assert.AreEqual(0.08, TresParser.DictNumber(loot, "bone_chance", 0), 0.0001);
            Assert.AreEqual("wardens_halo", TresParser.DictString(loot, "unique", ""));
            var light = TresParser.DictList(loot, "light");
            Assert.AreEqual(2, light.Count);
            Assert.AreEqual(5.0, light[1]);
            Assert.AreEqual(7, tres.Int("missing", 7), "a missing key is its fallback");
            StringAssert.StartsWith("ExtResource(", tres.String("script", ""));
        }

        [Test]
        public void AnEmptyOrHeaderlessFileReadsAsNothing()
        {
            Assert.AreEqual(0, TresParser.Parse("").Values.Count);
            Assert.AreEqual(0, TresParser.Parse("[gd_resource]\n").Values.Count);
        }
    }
}
