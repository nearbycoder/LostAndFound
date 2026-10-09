// The page around the game, from the game (WebPage.cs): its fullscreen, and its touch controls.
// The page's fullscreen: The canvas goes fullscreen, as the page's own button does;
// the browser refuses (quietly) if no click or key press is recent enough.
mergeInto(LibraryManager.library, {
  LafIsFullscreen: function () {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  },
  LafSetFullscreen: function (on) {
    try {
      if (on) {
        var el = Module.canvas;
        var go = el.requestFullscreen || el.webkitRequestFullscreen;
        var p = go && go.call(el);
        if (p && p.catch) p.catch(function (e) { console.log('[Page] fullscreen refused: ' + e); });
      } else if (document.fullscreenElement || document.webkitFullscreenElement) {
        (document.exitFullscreen || document.webkitExitFullscreen).call(document);
      }
    } catch (e) { console.log('[Page] fullscreen failed: ' + e); }
  },
  // A phone or a tablet, as the page judged it before the game loaded (index.html's lafTouchFirst): lighter graphics by default.
  LafTouchDevice: function () {
    return window.lafTouchFirst ? 1 : 0;
  },
  // Which of the page's touch controls apply now (TouchInput.State's bits).
  LafTouchState: function (flags) {
    if (window.lafTouchState) window.lafTouchState(flags);
  },
});
