using NeX_Drop.Shell.ViewModels;
using System.Diagnostics;

namespace NeX_Drop.Shell.Services
{
    public class FirebaseListenerService
    {
        private const string URL = "https://nex-launcher-69169-default-rtdb.firebaseio.com/";
        private FirebaseSyncManager? _sync;

        public void Start(ShellViewModel vm)
        {
            _sync = new FirebaseSyncManager(URL);
            _sync.OnActiveModeChanged      += m => { Debug.WriteLine($"[Listener] Mode: {m}"); vm.ApplyModeChange(m); };
            _sync.OnConnectionStateChanged += o => { if (o) vm.MarkOnline(); else vm.MarkOffline(); };
            _sync.StartActiveModeListener();
            _ = Task.Run(async () => { var m = await _sync.GetActiveModeOnceAsync(); if (m != null) vm.ApplyModeChange(m); });
        }

        public void Stop() => _sync?.StopAll();
    }
}
