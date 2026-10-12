mergeInto(LibraryManager.library, {
  $CommanderObservationSyncState: { busy: false, pending: false },
  CommanderObservation_Sync__deps: ['$CommanderObservationSyncState'],
  CommanderObservation_Sync: function () {
    if (typeof FS !== 'undefined' && typeof FS.syncfs === 'function') {
      var state = CommanderObservationSyncState;
      if (state.busy) { state.pending = true; return; }
      var sync = function () {
        state.busy = true;
        try {
          FS.syncfs(false, function () {
            state.busy = false;
            if (state.pending) { state.pending = false; sync(); }
          });
        } catch (error) { state.busy = false; }
      };
      sync();
    }
  },
  CommanderObservation_Download: function (jsonPointer) {
    var json = UTF8ToString(jsonPointer);
    if (json.length > 131072 || typeof document === 'undefined') return;
    var blob = new Blob([json], { type: 'application/json' });
    var link = document.createElement('a');
    var url = URL.createObjectURL(blob);
    link.href = url;
    link.download = 'commander-local-metrics.json';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 30000);
  }
});
