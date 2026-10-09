namespace LostAndFound
{
    /// <summary>
    /// The browser page around the WebGL build (Plugins/WebGL/LafPage.jslib). A browser only goes fullscreen while a click or
    /// key press still counts as recent (a few seconds), and Unity's own Screen.fullScreen waits for the next one, so the
    /// settings card asks the page directly, in the frame its click lands. Elsewhere these do nothing.
    /// </summary>
    public static class WebPage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int LafIsFullscreen();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void LafSetFullscreen(int on);
        public static bool Fullscreen => LafIsFullscreen() != 0;
        public static void SetFullscreen(bool on) => LafSetFullscreen(on ? 1 : 0);
#else
        public static bool Fullscreen => false;
        public static void SetFullscreen(bool on) { }
#endif
    }
}
