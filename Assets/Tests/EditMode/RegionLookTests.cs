using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// Two things the first New Game playtest caught: a region's air particles stay in
    /// its own band (Ruined Earth's smoke and ash were drifting through Paradise), and
    /// glow art is added on top unlit as in Godot (drawn normally, a glow tile's black
    /// surround showed as a black square).
    /// </summary>
    public class RegionLookTests
    {
        private const string GlowShader = "Descender/Sprite Glow";

        [Test]
        public void ClipKeepsTheOverlap()
        {
            var view = new Rect(-10f, -60f, 20f, 12f);
            var band = new Rect(0f, -85f, 480f, 34f); // Ruined Earth's band: rows 51 to 85
            var clip = RegionParticles.Clip(view, band);
            Assert.That(clip.xMin, Is.EqualTo(0f));
            Assert.That(clip.xMax, Is.EqualTo(10f));
            Assert.That(clip.yMin, Is.EqualTo(-60f));
            Assert.That(clip.yMax, Is.EqualTo(-51f));
        }

        [Test]
        public void ClipIsEmptyWhenTheViewIsAboveTheBand()
        {
            var view = new Rect(65f, -21f, 20f, 12f); // the view at the Paradise start
            var band = new Rect(0f, -85f, 480f, 34f);
            var clip = RegionParticles.Clip(view, band);
            Assert.That(clip.width * clip.height, Is.EqualTo(0f));
        }

        [Test]
        public void EveryTileSetDrawsItsGlowLayerAdditively()
        {
            var guids = AssetDatabase.FindAssets("t:RegionTileSet", new[] { "Assets/Data/Regions/Tiles" });
            Assert.That(guids.Length, Is.EqualTo(12));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var set = AssetDatabase.LoadAssetAtPath<RegionTileSet>(path);
                Assert.That(set.glowMaterial, Is.Not.Null, path);
                Assert.That(set.glowMaterial.shader.name, Is.EqualTo(GlowShader), path);
            }
        }

        [Test]
        public void PropGlowSpritesDrawAdditively()
        {
            var checkedAny = false;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Props" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var glow = AssetDatabase.LoadAssetAtPath<GameObject>(path).transform.Find("Glow");
                if (glow == null)
                {
                    continue;
                }
                checkedAny = true;
                var renderer = glow.GetComponent<SpriteRenderer>();
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(GlowShader), path);
            }
            Assert.That(checkedAny, Is.True, "no prop has a Glow sprite to check");
        }
    }
}
