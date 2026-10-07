using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// The size of the game's window on a given desktop. The player opens at 1600×900, which is bigger than a 1366×768
    /// laptop, a 1440×900 MacBook or a HiDPI screen scaled to 1280×800 (Unity gives the desktop in scaled points there),
    /// so the shelf arrow, the claim slip and the hints ended up off the screen. A window that doesn't fit becomes the
    /// largest 16:9 window that does, with room left for a title bar and a panel. Tools/unity.sh smallscreen checks it.
    /// </summary>
    public static class WindowFit
    {
        public static readonly Vector2Int Default = new(1600, 900);

        /// <summary>A window this much of the desktop's height (or less) fits with its title bar and a panel beside it.</summary>
        public const float FitsHeight = 0.92f;
        /// <summary>A window made to fit takes up to this much of the desktop each way.</summary>
        public const float TargetWidth = 0.92f, TargetHeight = 0.88f;
        /// <summary>Never smaller than this, however small the desktop says it is.</summary>
        public static readonly Vector2Int Smallest = new(640, 360);

        public static bool Fits(Vector2Int window, int desktopW, int desktopH) =>
            desktopW <= 0 || desktopH <= 0 || (window.x <= desktopW && window.y <= Mathf.FloorToInt(desktopH * FitsHeight));

        /// <summary>The window unchanged if it fits the desktop, otherwise the largest 16:9 window that does (even sizes).</summary>
        public static Vector2Int Fit(Vector2Int window, int desktopW, int desktopH)
        {
            if (Fits(window, desktopW, desktopH)) return window;
            int maxW = Mathf.FloorToInt(desktopW * TargetWidth), maxH = Mathf.FloorToInt(desktopH * TargetHeight);
            int w = Mathf.Min(maxW, Mathf.FloorToInt(maxH * 16f / 9f));
            int h = Mathf.FloorToInt(w * 9f / 16f);
            w -= w % 2;
            h -= h % 2;
            return new Vector2Int(Mathf.Max(w, Smallest.x), Mathf.Max(h, Smallest.y));
        }
    }
}
