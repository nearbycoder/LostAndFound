using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// The player isn't told when the pointer leaves its window, and keeps the last position it had inside: a pointer leaving
    /// by a side read as resting at that side and turned the desk (Tools/unity.sh pointertest). CameraRig.PointerLeft judges
    /// from the last step whether it has gone. Positions are whole pixels, 0 to size - 1, as the player reads them.
    /// </summary>
    public class PointerLeftTests
    {
        static readonly Vector2 Size = new(1600f, 900f);

        [TestCase(6f, -6f)]       // leaving slowly by the left: the last reading inside, a step from the side (6.3 px a step,
                                  // read rounded: the next would have been -0.3)
        [TestCase(2f, -4f)]
        [TestCase(0f, -4f)]       // a slow step that happened to end on the first pixel
        [TestCase(1599f, 4f)]     // and by the right
        [TestCase(1593f, 6f)]
        [TestCase(1f, -1f)]       // creeping out a pixel at a time
        [TestCase(30f, -61f)]     // briskly
        public void APointerOneStepFromLeavingHasLeft(float x, float dx)
        {
            Assert.IsTrue(CameraRig.PointerLeft(new Vector2(x, 450f), new Vector2(dx, 0f), Size));
        }

        [TestCase(12f, -1f)]      // slowing to a stop just inside a side, as a person does
        [TestCase(13f, -2f)]
        [TestCase(3f, -2f)]
        [TestCase(1587f, 2f)]
        [TestCase(1597f, 1f)]
        [TestCase(2f, -1f)]
        [TestCase(20f, 5f)]       // moving away from the side
        [TestCase(800f, -40f)]    // anywhere in the middle
        public void APointerThatStoppedInsideHasNot(float x, float dx)
        {
            Assert.IsFalse(CameraRig.PointerLeft(new Vector2(x, 450f), new Vector2(dx, 0f), Size));
        }

        [TestCase(0f, -33f)]      // flung against the screen's side, in a window that reaches it (maximised, or snapped):
        [TestCase(0f, -8f)]       // the screen stops it on the window's first pixel, in one big step
        [TestCase(1599f, 20f)]
        public void APointerStoppedByTheScreensSideIsStillIn(float x, float dx)
        {
            Assert.IsFalse(CameraRig.PointerLeft(new Vector2(x, 450f), new Vector2(dx, 0f), Size));
        }

        [Test]
        public void TheTopAndBottomCountToo()
        {
            Assert.IsTrue(CameraRig.PointerLeft(new Vector2(10f, 897f), new Vector2(0f, 5f), Size), "out by the top, near a side");
            Assert.IsTrue(CameraRig.PointerLeft(new Vector2(10f, 2f), new Vector2(-1f, -4f), Size), "out by the bottom");
            Assert.IsFalse(CameraRig.PointerLeft(new Vector2(10f, 880f), new Vector2(0f, 5f), Size));
        }

        [Test]
        public void NoStepYetIsIn()
        {
            Assert.IsFalse(CameraRig.PointerLeft(new Vector2(0f, 450f), Vector2.zero, Size));
            Assert.IsFalse(CameraRig.PointerLeft(new Vector2(800f, 450f), Vector2.zero, Size));
        }
    }
}
