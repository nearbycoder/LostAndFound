namespace LostAndFound
{
    /// <summary>
    /// What a stamp will do where it's held over the claim slip, said in the hint bar before it comes down (a stamp can't be
    /// taken back). It names only what's on the desk, the claimants and what's on the counter tray, never whether the
    /// verdict is the right one. With two claimants, RETURN gives the item to the one whose half of the slip it's over.
    /// </summary>
    public static class StampPreview
    {
        /// <param name="onTray">what's on the counter tray, or null</param>
        /// <param name="claimants">the names on the slip, in its order</param>
        /// <param name="who">the claimant whose half of the slip the stamp is over</param>
        /// <returns>null if the stamp needs something on the tray and there's nothing there</returns>
        public static string What(Verdict v, ObjectDef onTray, string[] claimants, int who)
        {
            claimants ??= new string[0];
            switch (v)
            {
                case Verdict.Return:
                    if (onTray == null) return null;
                    string to = claimants.Length == 0 ? "the claimant" : claimants[System.Math.Clamp(who, 0, claimants.Length - 1)];
                    return $"RETURN: give {Nudges.The(onTray)} to {to}";
                case Verdict.Seal:
                    return onTray == null ? null : $"SEAL: lock {Nudges.The(onTray)} in the Iron Drawer";
                default:
                    string them = claimants.Length switch
                    {
                        0 => "them",
                        1 => claimants[0],
                        2 => $"{claimants[0]} and {claimants[1]}",
                        _ => string.Join(", ", claimants, 0, claimants.Length - 1) + " and " + claimants[^1],
                    };
                    return $"REFUSE: send {them} away with nothing";
            }
        }

        /// <summary>The hint while a stamp is held: off the slip, how to use it; over it, what it will do there.</summary>
        public static string Hint(string what, bool overSlip, bool pad) => Hint(what, overSlip, pad, false);

        /// <param name="touch">by touch, a tap brings the stamp to a spot and says what it would do there; a tap on that spot again
        /// brings it down (a stamp can't be taken back, so it's never one tap)</param>
        public static string Hint(string what, bool overSlip, bool pad, bool touch)
        {
            if (touch && !pad)
                return overSlip ? what + "   ·   Tap here again to stamp" : "Tap the slip to see what it would do there, then tap again to stamp   ·   Back puts it back";
            if (!overSlip)
                return pad ? "Bring it over the slip, then A to stamp   ·   B puts the stamp back" : "Click on the slip to stamp it   ·   Right click to put the stamp back";
            return what + (pad ? "   ·   A to stamp" : "   ·   Click to stamp");
        }
    }
}
