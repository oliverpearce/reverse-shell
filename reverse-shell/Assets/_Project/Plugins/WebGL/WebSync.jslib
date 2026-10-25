// Web builds keep saves in the browser (IndexedDB). Unity 2019 only copies files there now and then, so a page
// reload could lose the last save: SyncFiles copies them right away (called by Game.World.WebSync after a save).
mergeInto(LibraryManager.library, {
    SyncFiles: function () {
        FS.syncfs(false, function (err) {
            if (err) console.log("Reverse Shell: could not store the save: " + err);
        });
    }
});
