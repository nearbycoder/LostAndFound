using NUnit.Framework;

namespace LostAndFound.Tests
{
    /// <summary>
    /// Playing by touch in a browser (TouchInput): the hints say what a finger does (tap, touch and hold, pinch, and the page's
    /// buttons by name) instead of the mouse's clicks and the keys, a stamp is never one tap, and the controls card has a
    /// touch list as long as the others.
    /// </summary>
    public class TouchInputTests
    {
        [Test]
        public void HintsSayTapAndTouchAndHold()
        {
            Assert.AreEqual("Touch and hold the wallet to read its tag, then tap to pick it up.",
                TouchInput.Words("Hover the wallet to read its tag, then click to pick it up.", true));
            Assert.AreEqual("Touch and hold it until that part lights up, then tap it.",
                TouchInput.Words("Hover over it until that part lights up, then click it.", true));
            Assert.AreEqual("Turn it right round, and lean in close (pinch).", TouchInput.Words("Turn it right round, and lean in close (scroll).", true));
        }

        [Test]
        public void KeysBecomeThePagesButtons()
        {
            Assert.AreEqual("Good. Tap Tray to put it on the counter tray.", TouchInput.Words("Good. Press T to put it on the counter tray.", true));
            Assert.AreEqual("Stuck? Tap Nudge for a nudge from Agnes.", TouchInput.Words("Stuck? Press H for a nudge from Agnes.", true));
            Assert.AreEqual("Read the claim slip", TouchInput.Words("Read the claim slip  [Tab]", true));
            Assert.AreEqual("Agnes's rules   ·   tap or Rules to read them", TouchInput.Words("Agnes's rules   ·   click or R to read them", true));
            Assert.AreEqual("Tap on the slip to stamp it   ·   Back to put the stamp back",
                TouchInput.Words("Click on the slip to stamp it   ·   Right click to put the stamp back", true));
            Assert.AreEqual("Walter's tag says drawer A. Turn left to the drawers  (the left arrow)",
                TouchInput.Words("Walter's tag says drawer A. Turn left to the drawers  (A or ←)", true));
        }

        [Test]
        public void WithAMouseTheWordsStay()
        {
            const string s = "Hover the wallet to read its tag, then click to pick it up.";
            Assert.AreEqual(s, TouchInput.Words(s, false));
            Assert.IsNull(TouchInput.Words(null, true));
        }

        [Test]
        public void AStampIsNeverOneTap()
        {
            StringAssert.Contains("tap again to stamp", StampPreview.Hint(null, false, false, true));
            Assert.AreEqual("RETURN: give it back   ·   Tap here again to stamp", StampPreview.Hint("RETURN: give it back", true, false, true));
            Assert.AreEqual(StampPreview.Hint("x", true, true), StampPreview.Hint("x", true, true, true), "a pad in hand: the pad's words");
            Assert.AreEqual(StampPreview.Hint("x", true, false), StampPreview.Hint("x", true, false, false));
        }

        [Test]
        public void TheControlsCardHasATouchList()
        {
            string touch = ControlsCard.Text(false, true), keys = ControlsCard.Text(false);
            Assert.AreEqual(keys.Split('\n').Length, touch.Split('\n').Length, "as many lines as the keyboard's");
            StringAssert.Contains("Pinch", touch);
            StringAssert.Contains("Touch and hold", touch);
            StringAssert.DoesNotContain("click", touch.ToLowerInvariant());
        }

        [Test]
        public void NoTouchWithoutThePage()
        {
            Assert.IsFalse(TouchInput.Active, "in the editor and on the desktop, nothing turns touch on");
        }
    }
}
