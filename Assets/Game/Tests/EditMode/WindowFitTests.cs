using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// The window fits the desktop: 1600×900 where there's room for it, otherwise the largest 16:9 window that leaves room
    /// for a title bar and a panel (Tools/unity.sh smallscreen checks the real thing in a headless KWin).
    /// </summary>
    public class WindowFitTests
    {
        static readonly Vector2Int D = WindowFit.Default;

        [Test]
        public void TheDefaultWindowIsKeptWhereItFits()
        {
            Assert.AreEqual(D, WindowFit.Fit(D, 1920, 1080));
            Assert.AreEqual(D, WindowFit.Fit(D, 1680, 1050));
            Assert.AreEqual(D, WindowFit.Fit(D, 2560, 1440));
            Assert.AreEqual(D, WindowFit.Fit(D, 0, 0), "an unknown desktop changes nothing");
        }

        [TestCase(1366, 768, 1200, 674)]   // the commonest small laptop
        [TestCase(1280, 800, 1176, 662)]   // a 2560×1600 screen at 200%, the Steam Deck's desktop
        [TestCase(1440, 900, 1324, 744)]   // a MacBook Air's default
        [TestCase(1536, 864, 1350, 758)]   // 1920×1080 at 125%
        [TestCase(1280, 720, 1124, 632)]
        [TestCase(1600, 900, 1408, 792)]   // the window's own size, with no room for its title bar
        public void ATooBigWindowBecomesTheLargest16By9ThatFits(int dw, int dh, int w, int h)
        {
            var fit = WindowFit.Fit(D, dw, dh);
            Assert.AreEqual(new Vector2Int(w, h), fit);
            Assert.IsTrue(WindowFit.Fits(fit, dw, dh), "the new size fits");
            Assert.LessOrEqual(Mathf.Abs(fit.x * 9f / 16f - fit.y), 1.5f, "16:9");
            Assert.AreEqual(0, fit.x % 2);
            Assert.AreEqual(0, fit.y % 2);
            Assert.AreEqual(fit, WindowFit.Fit(fit, dw, dh), "a fitted window stays as it is");
        }

        [Test]
        public void AWindowRememberedFromABiggerScreenIsFitted()
        {
            Assert.AreEqual(new Vector2Int(1200, 674), WindowFit.Fit(new Vector2Int(2560, 1440), 1366, 768));
            Assert.AreEqual(new Vector2Int(1000, 600), WindowFit.Fit(new Vector2Int(1000, 600), 1366, 768), "one that fits is the player's choice");
        }

        [Test]
        public void NeverSmallerThan640By360()
        {
            Assert.AreEqual(WindowFit.Smallest, WindowFit.Fit(D, 400, 300));
        }
    }
}
