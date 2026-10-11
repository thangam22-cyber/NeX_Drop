// NeX_Drop — FirebaseSyncManager
// Guna's contract: new FirebaseSyncManager(url) → StartActiveModeListener() → StartHeartbeat()
// Hema's node: "remote_app_link" → { downloadUrl, updatedBy, updatedAt }

using Firebase.Database;
using Firebase.Database.Query;
using NeX_Drop.Common.Models;
using System.Diagnostics;

namespace NeX_Drop.Shell.Services
{
    public class FirebaseSyncManager : IDisposable
    {
        private readonly FirebaseClient _client;
        private readonly string         _deviceId;

        private IDisposable? _modeSub;
        private IDisposable? _remoteAppSub;
        private CancellationTokenSource? _hbCts;

        // ── Events ────────────────────────────────────────────────────────
        public event Action<string>?        OnActiveModeChanged;
        public event Action<RemoteAppLink>? OnRemoteAppLinkReceived;
        public event Action<bool>?          OnConnectionStateChanged;
        public event Action<string>?        OnHeartbeatSent;

        // ── Constructor (Guna's signature) ───────────────────────────────
        public FirebaseSyncManager(string url, string deviceId = "laptop_01", string? token = null)
        {
            _deviceId = deviceId;
            _client   = token != null
                ? new FirebaseClient(url, new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(token) })
                : new FirebaseClient(url);
        }

        // ── FR-4: Mode listener ──────────────────────────────────────────
        public void StartActiveModeListener()
        {
            try
            {
                _modeSub = _client
                    .Child(FirebaseNodes.DevicesRoot)
                    .Child(_deviceId)
                    .Child(FirebaseNodes.ActiveMode)
                    .AsObservable<string>()
                    .Subscribe(
                        s => { if (!string.IsNullOrWhiteSpace(s.Object)) { OnActiveModeChanged?.Invoke(s.Object); OnConnectionStateChanged?.Invoke(true); } },
                        ex => { Debug.WriteLine($"[Sync] Mode error: {ex.Message}"); OnConnectionStateChanged?.Invoke(false); Task.Delay(5000).ContinueWith(_ => StartActiveModeListener()); });
            }
            catch (Exception ex) { Debug.WriteLine($"[Sync] StartModeListener failed: {ex.Message}"); }
        }

        // ── FR-7: remote_app_link listener ───────────────────────────────
        public void StartRemoteAppLinkListener()
        {
            try
            {
                _remoteAppSub = _client
                    .Child(FirebaseNodes.RemoteAppLink)
                    .AsObservable<RemoteAppLink>()
                    .Subscribe(
                        s => { if (s.Object?.DownloadUrl?.StartsWith("https") == true) OnRemoteAppLinkReceived?.Invoke(s.Object); },
                        ex => { Task.Delay(5000).ContinueWith(_ => StartRemoteAppLinkListener()); });
            }
            catch (Exception ex) { Debug.WriteLine($"[Sync] StartRemoteAppListener failed: {ex.Message}"); }
        }

        // ── FR-5: Heartbeat ──────────────────────────────────────────────
        public void StartHeartbeat()
        {
            _hbCts = new CancellationTokenSource();
            _ = Task.Run(() => HbLoopAsync(_hbCts.Token));
        }

        private async Task HbLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var t = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                    await _client.Child(FirebaseNodes.DevicesRoot).Child(_deviceId)
                                 .Child(FirebaseNodes.LastPing).PutAsync(t);
                    OnHeartbeatSent?.Invoke(t);
                    OnConnectionStateChanged?.Invoke(true);
                }
                catch { OnConnectionStateChanged?.Invoke(false); }

                try { await Task.Delay(TimeSpan.FromSeconds(HeartbeatConfig.IntervalSeconds), ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        public async Task<string?> GetActiveModeOnceAsync()
        {
            try { return await _client.Child(FirebaseNodes.DevicesRoot).Child(_deviceId).Child(FirebaseNodes.ActiveMode).OnceSingleAsync<string>(); }
            catch { return null; }
        }

        public void StopAll() { _modeSub?.Dispose(); _remoteAppSub?.Dispose(); _hbCts?.Cancel(); }
        public void Dispose() => StopAll();
    }
}
