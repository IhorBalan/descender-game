using System.Collections.Generic;
using Descender.EditorTools.Paradise;
using Descender.World.Paradise;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.Tests
{
    public sealed class ParadiseGradeTests
    {
        private const float Border = 400f;
        private const float FarEnd = 1300f;
        private const float Blend = 16f;
        private const float HollowVignette = 0.4f;
        private const float OrchardVignette = 0.2f;

        /// <summary>A layer nothing else uses, so no other Volume can reach the blend.</summary>
        private const int IsolatedLayer = 30;

        private static VolumeProfile Graded(int zone) =>
            AssetDatabase.LoadAssetAtPath<VolumeProfile>(ParadiseGrade.ProfilePath(zone));

        private static T Effect<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            Assert.That(profile.TryGet<T>(out var component), Is.True, typeof(T).Name);
            Assert.That(component.active, Is.True, typeof(T).Name);
            return component;
        }

        private static Volume ZoneVolume(List<Object> made, int number, float vignette, float weight)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Vignette>().intensity.Override(vignette);
            var go = new GameObject($"Zone {number}") { layer = IsolatedLayer };
            made.Add(profile);
            made.Add(go);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = ZoneLook.VolumePriority(number);
            volume.weight = weight;
            volume.sharedProfile = profile;
            return volume;
        }

        /// <summary>Through URP's own Volume stack: a quarter of the ungraded picture used to show at the border.</summary>
        [TestCase(Border - Blend - 1f)]
        [TestCase(Border - Blend * 0.5f)]
        [TestCase(Border)]
        [TestCase(Border + Blend * 0.5f)]
        [TestCase(Border + Blend + 1f)]
        public void CrossingABorderBlendsOneGradeIntoTheNextWithNothingUngraded(float x)
        {
            var wasReady = VolumeManager.instance.isInitialized;
            if (!wasReady)
            {
                VolumeManager.instance.Initialize();
            }
            var made = new List<Object>();
            var stack = VolumeManager.instance.CreateStack();
            try
            {
                var hollow = ZoneLook.WeightUnderNext(x, Border, Blend, ZoneLook.Weight(x, 0f, Border, Blend));
                var orchard = ZoneLook.Weight(x, Border, FarEnd, Blend);
                // The later zone is made first, so only the priority can put it on top.
                ZoneVolume(made, 2, OrchardVignette, orchard);
                ZoneVolume(made, 1, HollowVignette, hollow);

                VolumeManager.instance.Update(stack, null, 1 << IsolatedLayer);

                var expected = Mathf.Lerp(HollowVignette, OrchardVignette, orchard);
                Assert.That(stack.GetComponent<Vignette>().intensity.value, Is.EqualTo(expected).Within(0.001f));
            }
            finally
            {
                VolumeManager.instance.DestroyStack(stack);
                foreach (var thing in made)
                {
                    Object.DestroyImmediate(thing);
                }
                if (!wasReady)
                {
                    VolumeManager.instance.Deinitialize();
                }
            }
        }

        [Test]
        public void AZoneIsGoneOnceTheNextHasCoveredIt()
        {
            var past = Border + Blend + 1f;
            Assert.That(
                ZoneLook.WeightUnderNext(past, Border, Blend, ZoneLook.Weight(past, 0f, Border, Blend)),
                Is.Zero
            );
        }

        [Test]
        public void TheHollowIsWarmAndTheOrchardCoolDeeperAndMoreEnclosed()
        {
            var hollow = Graded(1);
            var orchard = Graded(2);
            Assert.That(Effect<WhiteBalance>(hollow).temperature.value, Is.GreaterThan(0f));
            Assert.That(Effect<WhiteBalance>(orchard).temperature.value, Is.LessThan(0f));
            Assert.That(
                Effect<ColorAdjustments>(orchard).postExposure.value,
                Is.LessThan(Effect<ColorAdjustments>(hollow).postExposure.value)
            );
            Assert.That(
                Effect<Vignette>(orchard).intensity.value,
                Is.GreaterThan(Effect<Vignette>(hollow).intensity.value)
            );
        }

        /// <summary>A hazard can sit in a corner, so the frame may shade it and never hide it.</summary>
        [TestCase(1)]
        [TestCase(2)]
        public void TheVignetteFramesTheViewWithoutHidingItsCorners(int zone)
        {
            var vignette = Effect<Vignette>(Graded(zone));
            Assert.That(vignette.intensity.overrideState, Is.True);
            Assert.That(vignette.intensity.value, Is.InRange(0.1f, 0.3f));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void EveryZoneKeepsTheOneTonemapperThatCannotBlend(int zone)
        {
            var tonemapping = Effect<Tonemapping>(Graded(zone));
            Assert.That(tonemapping.mode.overrideState, Is.True);
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.Neutral));
        }
    }
}
