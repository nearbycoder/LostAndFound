using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// A window narrower than 16:9 (4:3, or one snapped to half a screen) sees as much of the desk side to side as a 16:9 one:
    /// the camera's height of view grows instead of the sides being cut off. 16:9 and wider are left as they were.
    /// </summary>
    public class CameraFramingTests
    {
        static float HalfWidthTan(float fov, float aspect) => Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * aspect;

        [TestCase(16f / 9f)]
        [TestCase(1600f / 900f)]
        [TestCase(1200f / 674f)]
        [TestCase(2560f / 1080f)]
        [TestCase(32f / 9f)]
        public void SixteenByNineAndWiderAreUnchanged(float aspect)
        {
            foreach (float fov in new[] { 58f, 50f, 38f })
                Assert.AreEqual(fov, CameraRig.FovFor(fov, aspect), 1e-4f);
        }

        [TestCase(1024, 768)]   // 4:3
        [TestCase(1280, 1024)]  // 5:4
        [TestCase(1440, 900)]   // 16:10
        [TestCase(960, 1080)]   // a 1080p screen's half
        [TestCase(1280, 1440)]  // a 1440p screen's half
        public void ANarrowerWindowKeepsTheSixteenByNineWidth(int w, int h)
        {
            float aspect = (float)w / h;
            foreach (float fov in new[] { 58f, 50f, 38f })
            {
                float wide = CameraRig.FovFor(fov, aspect);
                Assert.Greater(wide, fov, "taller than asked for");
                Assert.AreEqual(HalfWidthTan(fov, 16f / 9f), HalfWidthTan(wide, aspect), 1e-4f, "as wide as at 16:9");
            }
        }

        [Test]
        public void TheSlipsCloseUpIsFramedAsBefore()
        {
            // round 5's formula for the slip's close-up, now the rig's for every view
            foreach (float aspect in new[] { 4f / 3f, 0.9f, 1.6f })
            {
                float before = 2f * Mathf.Atan(Mathf.Tan(25f * Mathf.Deg2Rad) * (16f / 9f) / aspect) * Mathf.Rad2Deg;
                Assert.AreEqual(before, CameraRig.FovFor(50f, aspect), 1e-3f);
            }
        }

        [Test]
        public void OddShapesStayInBounds()
        {
            Assert.AreEqual(58f, CameraRig.FovFor(58f, 0f), "an unknown shape changes nothing");
            Assert.AreEqual(58f, CameraRig.FovFor(58f, float.NaN));
            Assert.AreEqual(CameraRig.FovFor(58f, 0.5f), CameraRig.FovFor(58f, 0.2f), 1e-4f, "no taller than at 1:2");
            Assert.Less(CameraRig.FovFor(58f, 0.5f), 180f);
        }
    }
}
