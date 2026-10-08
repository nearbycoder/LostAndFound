using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>Settings > Graphics fidelity: four steps from Low to Ultra, High still the game as designed, each step at least as
    /// costly as the one below it, and round 1's saved Picture quality (0..2) reading as the same step.</summary>
    public class FidelityTests
    {
        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "settingstests", System.Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "settings.json");
            Settings.UseFile(path);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.UseFile(null);
            Directory.Delete(dir, true);
        }

        [Test]
        public void FourStepsLowToUltra()
        {
            Assert.AreEqual(4, FidelityStep.Count);
            CollectionAssert.AreEqual(new[] { "Low", "Medium", "High", "Ultra" }, GraphicsQuality.Names);
            Assert.AreEqual(FidelityStep.High, Settings.PictureQuality, "the default is High, today's look");
        }

        [Test]
        public void HighIsTheGameAsDesigned()
        {
            var h = FidelityStep.At(FidelityStep.High);
            Assert.AreEqual(0f, h.renderScale, "the pipeline asset's own render scale");
            Assert.AreEqual(0, h.mainShadowRes, "the asset's own shadow maps");
            Assert.AreEqual(0, h.lampShadowRes);
            Assert.AreEqual(2, h.softShadows);
            Assert.AreEqual(2, h.cascades);
            Assert.AreEqual(1, h.msaa);
            Assert.AreEqual(2, h.postAA, "SMAA High, as round 1's High");
            Assert.AreEqual(2, h.ambientOcclusion, "ambient occlusion as the renderer asset has it");
            Assert.IsTrue(h.depthOfField);
            Assert.IsFalse(h.hqDepthOfField);
            Assert.AreEqual(0, h.anisotropic);
            Assert.IsFalse(h.hdr64);
        }

        [Test]
        public void EachStepCostsAtLeastTheOneBelow()
        {
            float Scale(FidelityStep s) => s.renderScale > 0f ? s.renderScale : 1f;
            int Res(int r) => r > 0 ? r : 2048;
            for (int i = 1; i < FidelityStep.Count; i++)
            {
                FidelityStep a = FidelityStep.At(i - 1), b = FidelityStep.At(i);
                Assert.GreaterOrEqual(Scale(b), Scale(a), $"{b.name} renders at least as many pixels as {a.name}");
                Assert.GreaterOrEqual(Res(b.mainShadowRes), Res(a.mainShadowRes), b.name);
                Assert.GreaterOrEqual(Res(b.lampShadowRes), Res(a.lampShadowRes), b.name);
                Assert.GreaterOrEqual(b.softShadows, a.softShadows, b.name);
                Assert.GreaterOrEqual(b.msaa, a.msaa, b.name);
                Assert.GreaterOrEqual(b.postAA, a.postAA, b.name);
                Assert.GreaterOrEqual(b.ambientOcclusion, a.ambientOcclusion, b.name);
                Assert.GreaterOrEqual(b.reflectionRes, a.reflectionRes, b.name);
                Assert.GreaterOrEqual(b.dust, a.dust, b.name);
            }
            var low = FidelityStep.At(FidelityStep.Low);
            Assert.Less(low.renderScale, 1f, "Low renders fewer pixels");
            Assert.AreEqual(0, low.ambientOcclusion, "and has no ambient occlusion");
            Assert.IsFalse(low.depthOfField, "or depth of field");
            var ultra = FidelityStep.At(FidelityStep.Ultra);
            Assert.Greater(ultra.renderScale, 1f, "Ultra supersamples");
            Assert.AreEqual(4096, ultra.mainShadowRes);
            Assert.AreEqual(4, ultra.msaa);
        }

        [Test]
        public void RoundOnesSavedPictureQualityReadsTheSame()
        {
            // round 1 to 11 saved Picture quality as 0, 1 or 2 under "quality"
            for (int q = 0; q <= 2; q++)
            {
                File.WriteAllText(path, $"{{\"values\": [ {{\"key\": \"quality\", \"value\": {q}.0}} ]}}");
                Settings.UseFile(path);
                Assert.AreEqual(q, Settings.PictureQuality);
                Assert.AreEqual(new[] { "Low", "Medium", "High" }[q], GraphicsQuality.Names[Settings.PictureQuality]);
            }
        }

        [Test]
        public void UltraIsSavedAndOutOfRangeIsClamped()
        {
            Settings.PictureQuality = FidelityStep.Ultra;
            Settings.UseFile(path);   // as the next launch reads it
            Assert.AreEqual(FidelityStep.Ultra, Settings.PictureQuality);
            Settings.PictureQuality = 9;
            Assert.AreEqual(FidelityStep.Ultra, Settings.PictureQuality);
            Settings.PictureQuality = -2;
            Assert.AreEqual(FidelityStep.Low, Settings.PictureQuality);
            Assert.AreEqual("Ultra", FidelityStep.At(7).name, "a step past the end is Ultra");
        }
    }
}
