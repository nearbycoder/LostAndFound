// Flush the browser's in-memory file system to IndexedDB, so a save written a moment ago survives the tab closing.
mergeInto(LibraryManager.library, {
  LafSyncFS: function () {
    FS.syncfs(false, function (err) {
      if (err) console.log('[Save] IndexedDB sync failed: ' + err);
    });
  },
});
