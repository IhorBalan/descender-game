using System.Collections.Generic;
using System.Linq;
using Descender.EditorTools.Paradise;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>The garden's built stone where it shows its edges: the root hollow, the walls' corners, the lanterns on them.</summary>
    public sealed class ParadiseMasonryTests
    {
        private const string WakingHollow = "Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity";
        private const string Orchard = "Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity";
        private const string Terrace = "Assets/Art/Paradise/Garden_Terrace.asset";

        /// <summary>How far the hollow's floor sags below its ends toward its middle (it is a curve, -76 to -76.4).</summary>
        private const float FloorDip = 0.45f;

        private static ParadiseLayout.Zone Zone(int number) => ParadiseLayout.All().First(z => z.number == number);

        /// <summary>The root hollow under Mereth's grove: its floor's span and height, and the slabs' underside.</summary>
        private static (float left, float right, float floor, float ceiling) Hollow()
        {
            var zone = Zone(2);
            var floor = zone.shapes.First(s => s.name == "RootHollowFloor").outline;
            var grove = zone.shapes.First(s => s.name == "Grove").outline;
            var left = floor.Min(p => p.x);
            var right = floor.Max(p => p.x);
            // The slab's underside: the highest point of the Grove's outline at the hollow's wall
            // that is below its walk.
            var walk = grove.Max(p => p.y);
            var ceiling = grove.Where(p => Mathf.Abs(p.x - left) < 0.01f && p.y < walk - 1f).Max(p => p.y);
            return (left, right, floor.Max(p => p.y), ceiling);
        }

        private static void InScene(string path, System.Action<GameObject[]> check)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                check(scene.GetRootGameObjects());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>A quad mesh's quads as (x min, x max, y min, y max), four vertices each.</summary>
        private static List<(float x0, float x1, float y0, float y1)> Quads(Mesh mesh)
        {
            var v = mesh.vertices;
            var quads = new List<(float, float, float, float)>();
            for (var i = 0; i + 3 < v.Length; i += 4)
            {
                var four = v.Skip(i).Take(4).ToList();
                quads.Add((four.Min(p => p.x), four.Max(p => p.x), four.Min(p => p.y), four.Max(p => p.y)));
            }
            return quads;
        }

        [Test]
        public void EveryHangingLanternLightsTheStoneAroundIt()
        {
            var lanterns = 0;
            foreach (var path in new[] { WakingHollow, Orchard })
            {
                InScene(
                    path,
                    roots =>
                    {
                        foreach (
                            var lantern in roots
                                .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                                .Where(r => r.name == "lantern" && r.transform.parent.name == "Hanging")
                        )
                        {
                            lanterns++;
                            var at = $"lantern at {lantern.transform.position.x:0.0}";
                            var light = lantern.transform.Find("Light")?.GetComponent<WorldLight>();
                            Assert.That(light, Is.Not.Null, at);
                            Assert.That(light.Color, Is.EqualTo(ParadiseGarden.LanternColor), at);
                            Assert.That(light.Radius, Is.EqualTo(ParadiseGarden.LanternReach).Within(0.01f), at);
                            Assert.That(light.Energy, Is.GreaterThan(0f), at);
                            // From its glass, below the hook it hangs from.
                            Assert.That(
                                lantern.transform.position.y - light.transform.position.y,
                                Is.EqualTo(ParadiseGarden.LanternGlassDrop).Within(0.01f),
                                at
                            );
                            var glass = lantern.transform.Find("Glow")?.GetComponent<SpriteRenderer>();
                            Assert.That(glass, Is.Not.Null, at);
                            Assert.That(glass.color, Is.EqualTo(ParadiseGarden.LanternGlass), at);
                            var flame = light.GetComponent<World.Paradise.LanternFlame>();
                            Assert.That(flame, Is.Not.Null, at);
                            Assert.That(
                                new SerializedObject(flame).FindProperty("glass").objectReferenceValue,
                                Is.EqualTo(glass),
                                $"{at}: its glass breathes with it"
                            );
                        }
                    }
                );
            }
            Assert.That(lanterns, Is.GreaterThan(0));
        }

        [Test]
        public void ALanternsFlameBreathesGentlyAboutItsStrength()
        {
            var go = new GameObject("Flame");
            try
            {
                var flame = go.AddComponent<World.Paradise.LanternFlame>();
                flame.Configure(null, ParadiseGarden.LanternBreath, 0.45f);
                var levels = Enumerable.Range(0, 400).Select(i => flame.Level(i * 0.05f)).ToList();
                Assert.That(levels.Min(), Is.GreaterThanOrEqualTo(1f - ParadiseGarden.LanternBreath - 1e-4f));
                Assert.That(levels.Max(), Is.LessThanOrEqualTo(1f + ParadiseGarden.LanternBreath + 1e-4f));
                Assert.That(
                    levels.Max() - levels.Min(),
                    Is.GreaterThan(ParadiseGarden.LanternBreath * 0.5f),
                    "it moves"
                );
                // Never a flicker: a twentieth of a second changes it by a little only.
                for (var i = 1; i < levels.Count; i++)
                {
                    Assert.That(
                        Mathf.Abs(levels[i] - levels[i - 1]),
                        Is.LessThan(ParadiseGarden.LanternBreath * 0.25f)
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NoPierStandsInTheRootHollow()
        {
            var (left, right, _, _) = Hollow();
            InScene(
                Orchard,
                roots =>
                {
                    var piers = roots
                        .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                        .Where(m => m.name == "Piers");
                    Assert.That(piers, Is.Not.Empty);
                    foreach (var mesh in piers)
                    {
                        foreach (var q in Quads(mesh.sharedMesh))
                        {
                            Assert.That(
                                q.x1 <= left + 0.01f || q.x0 >= right - 0.01f,
                                $"a pier from {q.x0:0.0} to {q.x1:0.0} stands in the hollow ({left} to {right})"
                            );
                        }
                    }
                }
            );
        }

        [Test]
        public void TheRootHollowsWallsAreQuoinedFromTheSlabDownToItsFloor()
        {
            var (left, right, floor, ceiling) = Hollow();
            InScene(
                Orchard,
                roots =>
                {
                    var quads = roots
                        .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                        .Where(m => m.name == "Quoins")
                        .SelectMany(m => Quads(m.sharedMesh))
                        .ToList();
                    foreach (var (wall, inward) in new[] { (left, -1f), (right, 1f) })
                    {
                        // The long stones stand a little proud of the face; the rest is in the wall.
                        var quoin = quads.Where(q => q.x0 < wall && q.x1 > wall).ToList();
                        Assert.That(quoin, Has.Count.EqualTo(1), $"the hollow's wall at {wall}");
                        var (x0, x1, y0, y1) = quoin[0];
                        var inside = inward < 0f ? wall - x0 : x1 - wall;
                        Assert.That(inside, Is.GreaterThan(2f), $"the quoin at {wall} is built into its wall");
                        Assert.That(
                            y1,
                            Is.EqualTo(ceiling).Within(0.01f),
                            $"the quoin at {wall} starts under the slab"
                        );
                        Assert.That(y0, Is.EqualTo(floor).Within(FloorDip), $"the quoin at {wall} stops at the floor");
                    }
                }
            );
        }

        [Test]
        public void TheEndsOfTheSlabsOverTheHollowStandOnOneColumnEachOnItsFloor()
        {
            var zone = Zone(2);
            var (_, _, floor, _) = Hollow();
            // The hole down into the hollow, between the Grove's end and the Orchard Rows' start.
            var ends = new[]
            {
                zone.shapes.First(s => s.name == "Grove").outline.Where(p => p.y > -63f).Max(p => p.x),
                zone.shapes.First(s => s.name == "OrchardRows").outline[0].x,
            };
            InScene(
                Orchard,
                roots =>
                {
                    var pilasters = roots
                        .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .Where(t => t.name == "Pilaster")
                        .ToList();
                    foreach (var x in ends)
                    {
                        var here = pilasters.Where(p => Mathf.Abs(p.Find("Shaft").position.x - x) < 0.01f).ToList();
                        Assert.That(here, Has.Count.EqualTo(1), $"one column at the slab's end at {x}");
                        var foot = here[0].Find("Base");
                        Assert.That(foot, Is.Not.Null, $"the column at {x} stands on something");
                        Assert.That(
                            foot.position.y,
                            Is.EqualTo(floor).Within(FloorDip),
                            $"the column at {x} stands on the floor"
                        );
                    }
                }
            );
        }

        [Test]
        public void EveryWallNicheHoldsItsLanternUnderTheCopingClearOfIvyFallsAndPiers()
        {
            var niches = 0;
            foreach (var number in new[] { 1, 2 })
            {
                var zone = Zone(number);
                var plan = ParadiseGarden.GardenPlan(zone);
                foreach (var niche in plan.niches)
                {
                    niches++;
                    var x = niche.at.x;
                    var at = $"zone {number}, niche at {x:0.0}";
                    var lanterns = plan
                        .hanging.Where(h => h.name == "lantern" && Mathf.Abs(h.at.x - x) < 0.01f)
                        .ToList();
                    Assert.That(lanterns, Has.Count.EqualTo(1), at);
                    Assert.That(
                        lanterns[0].at.y,
                        Is.EqualTo(niche.at.y - ParadiseGarden.NicheCrown).Within(0.001f),
                        at
                    );
                    Assert.That(
                        niche.at.y,
                        Is.EqualTo(ParadiseDressing.SurfaceY(zone, x) - 0.95f - ParadiseGarden.NicheGap).Within(0.001f),
                        at
                    );
                    var clear = ParadiseGarden.NicheHalfWidth + 1f;
                    Assert.That(
                        plan.walls.Where(w => w.name.StartsWith("ivy_drape")).All(w => Mathf.Abs(w.at.x - x) >= clear),
                        at
                    );
                    Assert.That(plan.water.All(w => Mathf.Abs(w.x - x) >= clear), at);
                    // Under a level coping, not a slope.
                    var walk = ParadiseDressing.SurfaceY(zone, x);
                    foreach (var side in new[] { x - ParadiseGarden.NicheHalfWidth, x + ParadiseGarden.NicheHalfWidth })
                    {
                        Assert.That(
                            ParadiseDressing.SurfaceY(zone, side),
                            Is.EqualTo(walk).Within(ParadiseGarden.NicheLevel),
                            at
                        );
                    }
                }
            }
            Assert.That(niches, Is.GreaterThan(0));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ADecksBannerOrLanternHangsInsideAnArchUnderItsKeystoneNeverInFrontOfAColumn(int number)
        {
            var zone = Zone(number);
            var plan = ParadiseGarden.GardenPlan(zone);
            var checkedOne = false;
            foreach (var bridge in zone.bridges)
            {
                var x0 = bridge.deck[0].x;
                var x1 = bridge.deck[bridge.deck.Count - 1].x;
                if (x1 - x0 <= 9f)
                {
                    continue;
                }
                var (line, feet) = ParadiseGreybox.Supports(bridge, zone);
                var at = ParadiseGarden.DeckHanging(bridge, zone, line, (x0 + x1) / 2f);
                var where = $"zone {number}, {bridge.name}";
                Assert.That(
                    plan.hanging.Any(h =>
                        (h.name == "banner" || h.name == "lantern") && Vector2.Distance(h.at, at) < 0.001f
                    ),
                    where
                );
                Assert.That(
                    plan.hanging.Where(h =>
                            (h.name.StartsWith("ivy") || h.name.StartsWith("vine") || h.name == "wisteria_drape")
                            && h.at.x >= x0
                            && h.at.x <= x1
                        )
                        .All(h => Mathf.Abs(h.at.x - at.x) >= ParadiseGarden.DeckOrnamentClearance),
                    $"{where}: no ivy over it"
                );
                var arches = ParadiseGarden.Arches(line, feet, ParadiseGreybox.DeckThickness, out _);
                if (arches.Count > 0)
                {
                    Assert.That(
                        arches.Any(a => Mathf.Abs(a.Centre - at.x) < 0.001f),
                        $"{where}: under an arch's keystone"
                    );
                    var arch = arches.First(a => Mathf.Abs(a.Centre - at.x) < 0.001f);
                    Assert.That(at.y, Is.LessThan(arch.Crown).And.GreaterThan(arch.spring), $"{where}: in the opening");
                }
                else
                {
                    Assert.That(feet.All(f => Mathf.Abs(f.x - at.x) > 1f), $"{where}: clear of the columns");
                }
                checkedOne = true;
            }
            Assert.That(checkedOne, Is.True);
        }

        [Test]
        public void NoNicheIsCutIntoAPier()
        {
            InScene(
                Orchard,
                roots =>
                {
                    var niches = roots
                        .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                        .Where(r => r.name == ParadiseGarden.WallNiche)
                        .Select(r => r.bounds)
                        .ToList();
                    var piers = roots
                        .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                        .Where(m => m.name == "Piers")
                        .SelectMany(m => Quads(m.sharedMesh))
                        .ToList();
                    foreach (var n in niches)
                    {
                        Assert.That(
                            piers.All(q => q.x1 <= n.min.x || q.x0 >= n.max.x),
                            $"the niche at {n.center.x:0.0} overlaps a pier"
                        );
                    }
                }
            );
        }

        [Test]
        public void TheTerracesUndersideIsFinishedStoneNotTheIslandsRock()
        {
            var uses = AssetDatabase.GetDependencies(Terrace, false);
            Assert.That(uses.Any(p => p.EndsWith($"/{ParadiseGarden.SoffitSprite}.png")), Is.True);
            Assert.That(uses.Any(p => p.EndsWith("/island_bottom_flip.png")), Is.False);
        }

        [Test]
        public void TheLanternTreeStandsOnItsTrunksGroundWithTheLandingsInItsCrown()
        {
            var zone = Zone(2);
            var trunk = zone.shapes.First(s => s.name == ParadiseGarden.LanternTrunk).outline;
            var x = trunk.Average(p => p.x);
            var plan = ParadiseGarden.GardenPlan(zone);
            var tree = plan.trees.Where(t => Mathf.Approximately(t.scale, ParadiseGarden.LanternTreeScale)).ToList();
            Assert.That(tree, Has.Count.EqualTo(1));
            Assert.That(tree[0].at.x, Is.EqualTo(x).Within(0.01f));
            Assert.That(tree[0].at.y, Is.EqualTo(ParadiseDressing.SurfaceY(zone, x) + 0.3f).Within(0.01f));
            // The blossom tree is 10 units tall at scale 1: the crown holds the highest landing.
            var crown = zone.bridges.First(b => b.name == "LanternCrown").deck.Max(p => p.y);
            Assert.That(tree[0].at.y + 10f * tree[0].scale, Is.GreaterThan(crown + 2f));
        }
    }
}
