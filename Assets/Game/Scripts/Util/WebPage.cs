namespace LostAndFound
{
    /// <summary>
    /// The browser page around the WebGL build (Plugins/WebGL/LafPage.jslib). A browser only goes fullscreen while a click or
    /// key press still counts as recent (a few seconds), and Unity's own Screen.fullScreen waits for the next one, so the
    /// settings card asks the page directly, in the frame its click lands. The page's touch controls (TouchInput) are told which
    /// of them apply. Elsewhere these do nothing.
    /// </summary>
    public static class WebPage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int LafIsFullscreen();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void LafSetFullscreen(int on);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int LafTouchDevice();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void LafTouchState(int flags);
        public static bool Fullscreen => LafIsFullscreen() != 0;
        public static void SetFullscreen(bool on) => LafSetFullscreen(on ? 1 : 0);
        /// <summary>A phone or a tablet (a touch screen and no mouse), as the page judged it before the game loaded.</summary>
        public static bool TouchDevice => LafTouchDevice() != 0;
        public static void TouchState(int flags) => LafTouchState(flags);
#else
        public static bool Fullscreen => false;
        public static void SetFullscreen(bool on) { }
        public static bool TouchDevice => false;
        public static void TouchState(int flags) { }
#endif
    }
}
