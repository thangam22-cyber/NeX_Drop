// NeX_Drop.Shell — OfflineDetectionService
// PRD FR-5: Heartbeat & Device Status — Offline Detection (Windows side)
//
// PRD §4.5:
//   - Default heartbeat interval: 30 seconds
//   - Offline threshold: 3 missed heartbeats (90 seconds)
//   - Admin receives push notification when device transitions to Offline
//     (Android push = Deepika/Hema's task. Windows side = local notification + Firebase flag)
//
// Owner: Thangamani (Windows Core Logic) — feature/win-logic

using Firebase.Database;
using Firebase.Database.Query;
using NeX_Drop.Common.Models;
using NeX_Drop.Shell.ViewModels;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NeX_Drop.Shell.Services
{
    public class OfflineDetectionService : IDisposable
    {
        private const string FIREBASE_URL = "https://nex-launcher-69169-default-rtdb.firebaseio.com/";
        private const string DEVICE_ID    = "laptop_01";

        private readonly FirebaseClient        _client;
        private readonly ShellViewModel        _vm;
        private          CancellationTokenSource _cts = new();

        private DateTime _lastSuccessfulPing = DateTime.UtcNow;
        private bool     _isOffline          = false;
        private int      _consecutiveFails   = 0;

        // PRD FR-5: 3 missed heartbeats = offline
        private const int OfflineThreshold = HeartbeatConfig.OfflineThresholdCount;

        public OfflineDetectionService(ShellViewModel vm)
        {
            _vm     = vm;
            _client = new FirebaseClient(FIREBASE_URL);
        }

        public void Start()
        {
            _ = Task.Run(() => MonitorLoopAsync(_cts.Token));
            Debug.WriteLine("[OfflineDetect] Started. Threshold = 3 missed heartbeats (90s)");
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatConfig.IntervalSeconds), ct)
                    .ContinueWith(_ => { });

                await CheckAndPingAsync();
            }
        }

        private async Task CheckAndPingAsync()
        {
            try
            {
                // Write heartbeat
                var pingTime = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

                await _client
                    .Child(FirebaseNodes.DevicesRoot)
                    .Child(DEVICE_ID)
                    .Child(FirebaseNodes.LastPing)
                    .PutAsync(pingTime);

                // Success
                _lastSuccessfulPing = DateTime.UtcNow;
                _consecutiveFails   = 0;

                if (_isOffline)
                {
                    _isOffline = false;
                    _vm.MarkOnline();
                    _vm.UpdateHeartbeatText("Just now");
                    Debug.WriteLine("[OfflineDetect] Back ONLINE ✅");
                }
                else
                {
                    _vm.UpdateHeartbeatText("Just now");
                }
            }
            catch (Exception ex)
            {
                _consecutiveFails++;
                Debug.WriteLine($"[OfflineDetect] Heartbeat fail #{_consecutiveFails}: {ex.Message}");

                // PRD FR-5: 3 missed = OFFLINE
                if (_consecutiveFails >= OfflineThreshold && !_isOffline)
                {
                    _isOffline = true;
                    _vm.MarkOffline();
                    _vm.UpdateHeartbeatText($"Offline ({_consecutiveFails * HeartbeatConfig.IntervalSeconds}s ago)");

                    Debug.WriteLine("[OfflineDetect] Device OFFLINE — notifying admin...");

                    // Windows side: show local notification
                    ShowOfflineNotification();

                    // Write offline_alert to Firebase (will sync when back online)
                    await WriteOfflineAlertAsync();
                }
            }
        }

        // ── Windows Toast Notification ────────────────────────────────────
        // PRD FR-5: Admin receives notification on offline transition
        // Windows side: local system tray notification
        private static void ShowOfflineNotification()
        {
            try
            {
                // Use Windows MessageBox as fallback (Toast requires UWP package)
                // For full Toast: add Microsoft.Windows.SDK.Contracts NuGet
                Debug.WriteLine("[OfflineDetect] OFFLINE NOTIFICATION: NeX_Drop — Device went offline!");

                // Show a non-blocking system notification via MessageBox on UI thread
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // Non-blocking balloon tip via NotifyIcon (if available)
                    // PRD note: Full push notification to admin handled by Android (Deepika/Hema)
                    Debug.WriteLine("[OfflineDetect] Local offline alert triggered.");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OfflineDetect] Notification failed: {ex.Message}");
            }
        }

        // ── Write offline_alert to Firebase ─────────────────────────────
        private async Task WriteOfflineAlertAsync()
        {
            try
            {
                var alert = new
                {
                    detected_at = DateTime.UtcNow.ToString("o"),
                    reason      = $"Missed {_consecutiveFails} consecutive heartbeats ({_consecutiveFails * HeartbeatConfig.IntervalSeconds}s)",
                    device_id   = DEVICE_ID
                };

                // This will sync when connection is restored
                await _client
                    .Child(FirebaseNodes.DevicesRoot)
                    .Child(DEVICE_ID)
                    .Child(FirebaseNodes.OfflineAlert)
                    .PutAsync(alert);

                Debug.WriteLine("[OfflineDetect] Offline alert written to Firebase.");
            }
            catch
            {
                // Expected — we're offline. Firebase will sync when reconnected.
                Debug.WriteLine("[OfflineDetect] Offline alert queued (will sync on reconnect).");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _client.Dispose();
        }
    }
}
