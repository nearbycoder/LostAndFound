// Flush the browser's in-memory file system to IndexedDB, so a save written a moment ago survives the tab closing.
// A flush asked for while one is running (a save and then the settings, say) runs once more when it's done, rather than
// two at once (which Emscripten warns about, and which can finish out of order).
mergeInto(LibraryManager.library, {
  LafSyncFS: function () {
    if (Module.lafSyncing) { Module.lafSyncAgain = true; return; }
    Module.lafSyncing = true;
    var run = function () {
      FS.syncfs(false, function (err) {
        if (err) console.log('[Save] IndexedDB sync failed: ' + err);
        if (Module.lafSyncAgain) { Module.lafSyncAgain = false; run(); }
        else Module.lafSyncing = false;
      });
    };
    run();
  },
});
