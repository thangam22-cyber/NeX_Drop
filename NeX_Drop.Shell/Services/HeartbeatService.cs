using System.Diagnostics;

namespace NeX_Drop.Shell.Services
{
    public class HeartbeatService
    {
        private const string URL = "https://nex-launcher-69169-default-rtdb.firebaseio.com/";
        private FirebaseSyncManager? _sync;

        public void Start()
        {
            _sync = new FirebaseSyncManager(URL);
            _sync.OnHeartbeatSent += t => Debug.WriteLine($"[Heartbeat] {t}");
            _sync.StartHeartbeat();
        }

        public void Stop() => _sync?.StopAll();
    }
}
