using System.Collections.Generic;
using System.Linq;
using Descender.EditorTools.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;

namespace Descender.Tests
{
    public sealed class ParadiseCliffTests
    {
        private const string Architecture = "Assets/Art/Paradise/Kit/Architecture";
        private const float Left = 0f;
        private const float Right = 30f;
        private const float Top = -20f;

        private static readonly string[] GardenScenes =
        {
            "Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity",
            "Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity",
        };

        /// <summary>A block of ground, clockwise from its top left, reaching under the clouds.</summary>
        private static List<ParadiseLayout.P> Block() =>
            new List<ParadiseLayout.P>
            {
                new ParadiseLayout.P(Left, Top, true),
                new ParadiseLayout.P(Right, Top, true),
                new ParadiseLayout.P(Right, -100f, true),
                new ParadiseLayout.P(Left, -100f, true),
            };

        [Test]
        public void RockSidesWanderOutwardAndNeverIntoTheWalk()
        {
            var rough = ParadiseGreybox.RoughenSides(Block(), 12f);
            var sides = rough.Where(p => p.y < Top - 0.01f && p.y > -95f).ToList();
            Assert.That(sides, Is.Not.Empty);
            foreach (var p in sides)
            {
                // Either side of the block, never inside it: the collider stays covered.
                Assert.That(p.x <= Left || p.x >= Right, Is.True, $"({p.x}, {p.y}) is inside the block");
                Assert.That(p.x, Is.InRange(Left - 1f, Right + 1f));
            }
            Assert.That(sides.Max(p => p.x), Is.GreaterThan(Right + 0.1f), "the right side did not move");
            Assert.That(sides.Min(p => p.x), Is.LessThan(Left - 0.1f), "the left side did not move");
        }

        [Test]
        public void RoughnessLeavesTheWalkAndWhatIsUnderTheCloudsAlone()
        {
            var block = Block();
            var rough = ParadiseGreybox.RoughenSides(block, 12f);
            foreach (var corner in block)
            {
                Assert.That(rough.Any(p => p.x == corner.x && p.y == corner.y), Is.True, $"lost {corner.V}");
            }
            Assert.That(rough.Count(p => Mathf.Approximately(p.y, Top)), Is.EqualTo(2), "the top gained points");
            Assert.That(rough.Count(p => p.y < -95f), Is.EqualTo(2), "points were added under the clouds");
        }

        [Test]
        public void RoughnessIsTheSameEveryBuild()
        {
            var first = ParadiseGreybox.RoughenSides(Block(), 12f);
            var second = ParadiseGreybox.RoughenSides(Block(), 12f);
            Assert.That(second.Select(p => p.V), Is.EqualTo(first.Select(p => p.V)));
        }

        [Test]
        public void ThePierHasTheWallsCourses()
        {
            var pier = (TextureImporter)AssetImporter.GetAtPath($"{Architecture}/wall_pier.png");
            var wall = (TextureImporter)AssetImporter.GetAtPath($"{Architecture}/wall_block.png");
            var pierTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Architecture}/wall_pier.png");
            var wallTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Architecture}/wall_block.png");
            // Same height at the same density: mapped by world height, joints meet joints.
            Assert.That(pierTexture.height, Is.EqualTo(wallTexture.height));
            Assert.That(pier.spritePixelsPerUnit, Is.EqualTo(wall.spritePixelsPerUnit));
            Assert.That(pier.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
        }

        [Test]
        public void EveryLitMeshOnTheGroundCarriesTheChannelsTheNormalsPassReads()
        {
            var checkedMeshes = 0;
            foreach (var path in GardenScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (filter.name != "Piers" && filter.name != "SoftSides")
                            {
                                continue;
                            }
                            var mesh = filter.sharedMesh;
                            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), filter.name);
                            Assert.That(mesh.tangents.Length, Is.EqualTo(mesh.vertexCount), filter.name);
                            Assert.That(mesh.normals.All(n => n == Vector3.back), Is.True, filter.name);
                            checkedMeshes++;
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            Assert.That(checkedMeshes, Is.GreaterThan(0), "no piers or rock sides in the garden");
        }

        [Test]
        public void WhatIsBuiltIntoAWallLiesOverTheFillAndUnderTheShade()
        {
            var scene = EditorSceneManager.OpenScene(GardenScenes[0], OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var fill = roots
                    .SelectMany(r => r.GetComponentsInChildren<SpriteShapeRenderer>(true))
                    .Where(r => r.transform.Find("Depth") != null)
                    .Select(r => r.sortingOrder)
                    .Distinct()
                    .ToList();
                var renderers = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToList();
                var built = renderers.Where(r => r.name == "Piers" || r.name == "SoftSides").ToList();
                var shade = renderers.Where(r => r.name == "Depth" || r.name == "UnderLip").ToList();
                Assert.That(fill, Has.Count.EqualTo(1));
                Assert.That(built, Is.Not.Empty);
                Assert.That(shade, Is.Not.Empty);
                Assert.That(built.Min(r => r.sortingOrder), Is.GreaterThan(fill[0]));
                Assert.That(built.Max(r => r.sortingOrder), Is.LessThan(shade.Min(r => r.sortingOrder)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
