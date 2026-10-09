// The page's fullscreen, asked for from the game (WebPage.cs). The canvas goes fullscreen, as the page's own button does;
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
});
