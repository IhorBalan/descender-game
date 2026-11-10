using System.Collections.Generic;
using System.IO;
using Descender.EditorTools.Paradise;
using Descender.World.Paradise;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The new Ansel (Assets/Art/Paradise/Ansel, built by ParadiseHero into HeroRig.prefab):
    /// the layout is whole, every bending part is skinned to its bones with weights that blend
    /// across the joints, and the rig carries the cloak and no wing.
    /// </summary>
    public class AnselRigTests
    {
        private static JObject Layout() => JObject.Parse(File.ReadAllText($"{ParadiseHero.HeroFolder}/ansel_rig.json"));

        private static GameObject Prefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(ParadiseHero.PrefabPath)
            ?? throw new AssertionException($"No prefab at {ParadiseHero.PrefabPath}");

        [Test]
        public void TheLayoutNamesOnlyPartsAndBonesThatExist()
        {
            var layout = Layout();
            var bones = new HashSet<string>();
            foreach (var bone in layout["skeleton"])
            {
                var parent = (string)bone["parent"];
                Assert.That(
                    string.IsNullOrEmpty(parent) || bones.Contains(parent),
                    Is.True,
                    $"{bone["name"]} comes before its parent {parent}"
                );
                bones.Add((string)bone["name"]);
            }
            foreach (var part in layout["parts"])
            {
                var path = $"{ParadiseHero.HeroFolder}/{part["file"]}.png";
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, $"no sprite at {path}");
                foreach (var name in part["bones"])
                {
                    Assert.That(bones, Does.Contain((string)name), $"{part["name"]} names a missing bone");
                }
                if (!(bool)part["skinned"])
                {
                    Assert.That(bones, Does.Contain((string)part["bone"]), $"{part["name"]} hangs from a missing bone");
                }
            }
        }

        [Test]
        public void EveryBendingPartIsSkinnedToItsBones()
        {
            var skins = Prefab().GetComponentsInChildren<SpriteSkin>(true);
            Assert.That(
                skins.Length,
                Is.EqualTo(7),
                "cloak, two arms, two legs, the torso and the head (for the hair) bend"
            );
            foreach (var skin in skins)
            {
                var sprite = skin.GetComponent<SpriteRenderer>().sprite;
                Assert.That(skin.rootBone, Is.Not.Null, $"{skin.name} has no root bone");
                Assert.That(skin.boneTransforms, Has.Length.EqualTo(sprite.GetBones().Length), skin.name);
                Assert.That(skin.boneTransforms, Has.None.Null, $"{skin.name} lost a bone");
                Assert.That(
                    sprite.HasVertexAttribute(VertexAttribute.BlendWeight),
                    Is.True,
                    $"{sprite.name} has no weights"
                );
                foreach (var weight in sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight))
                {
                    var sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                    Assert.That(
                        sum,
                        Is.EqualTo(1f).Within(0.01f),
                        $"{sprite.name} has a vertex whose weights do not add up"
                    );
                }
            }
        }

        [Test]
        public void TheKneeBlendsTheThighIntoTheShin()
        {
            // A smooth bend needs vertices around the joint that follow both bones, not one.
            var leg = AssetDatabase.LoadAssetAtPath<Sprite>($"{ParadiseHero.HeroFolder}/ansel_leg.png");
            var shared = 0;
            foreach (var weight in leg.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight))
            {
                var thighAndShin =
                    (weight.boneIndex0 == 0 && weight.boneIndex1 == 1)
                    || (weight.boneIndex0 == 1 && weight.boneIndex1 == 0);
                if (thighAndShin && weight.weight1 > 0.2f)
                {
                    shared++;
                }
            }
            Assert.That(
                shared,
                Is.GreaterThanOrEqualTo(4),
                "no vertex around the knee follows both the thigh and the shin"
            );
        }

        [Test]
        public void EveryPartHasItsNormalMap()
        {
            foreach (var renderer in Prefab().GetComponentsInChildren<SpriteRenderer>(true))
            {
                var sprite = renderer.sprite;
                if (sprite == null || !sprite.name.StartsWith("ansel_"))
                {
                    continue;
                }
                var path = AssetDatabase.GetAssetPath(sprite);
                var data = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
                data.Init();
                var provider = data.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));
                provider.InitSpriteEditorDataProvider();
                var textures = provider
                    .GetDataProvider<UnityEditor.U2D.Sprites.ISecondaryTextureDataProvider>()
                    .textures;
                Assert.That(textures, Is.Not.Null.And.Not.Empty, $"{sprite.name} has no normal map");
                Assert.That(textures[0].name, Is.EqualTo("_NormalMap"), sprite.name);
                Assert.That(textures[0].texture, Is.Not.Null, $"{sprite.name}'s normal map is missing");
            }
        }

        [Test]
        public void TheRigCarriesItsHairTunicHoodAndEffects()
        {
            var rig = new SerializedObject(Prefab().GetComponent<HeroRig>());
            Assert.That(rig.FindProperty("hairBones").arraySize, Is.EqualTo(2), "two locks of hair");
            Assert.That(rig.FindProperty("tailBones").arraySize, Is.EqualTo(2), "the tunic's two tails");
            foreach (
                var field in new[] { "hood", "handFront", "handBack", "dust", "glints", "streaks", "slashArc", "ring" }
            )
            {
                Assert.That(rig.FindProperty(field).objectReferenceValue, Is.Not.Null, $"the rig has no {field}");
            }
        }

        [Test]
        public void TheRigCarriesTheCloakAndNoWing()
        {
            var prefab = Prefab();
            var rig = new SerializedObject(prefab.GetComponent<HeroRig>());
            var cloak = rig.FindProperty("cloakBones");
            Assert.That(cloak.arraySize, Is.EqualTo(15), "three chains of five cloak joints");
            for (var i = 0; i < cloak.arraySize; i++)
            {
                Assert.That(
                    cloak.GetArrayElementAtIndex(i).objectReferenceValue,
                    Is.Not.Null,
                    $"cloak joint {i} is empty"
                );
            }
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(t.name, Does.Not.Contain("Wing"), "the new Ansel has no wing");
            }
        }
    }
}
