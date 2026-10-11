// NeX_Drop.Shell — ModeHistoryService
// PRD §8: mode_history — audit trail of mode changes
//
// Windows client = READ-ONLY for mode_history (PRD §9.1)
// Android admin (Hema/Deepika) = WRITES mode_history entries
//
// This service:
//   1. Reads mode_history from Firebase on startup
//   2. Listens for new entries in real-time
//   3. Exposes history list to ShellViewModel for UI display
//
// Owner: Thangamani (Windows Core Logic) — feature/win-logic

using Firebase.Database;
using Firebase.Database.Query;
using NeX_Drop.Common.Models;
using NeX_Drop.Shell.ViewModels;
using System.Diagnostics;

namespace NeX_Drop.Shell.Services
{
    public class ModeHistoryService : IDisposable
    {
        private const string FIREBASE_URL = "https://nex-launcher-69169-default-rtdb.firebaseio.com/";
        private const string DEVICE_ID    = "laptop_01";

        private readonly FirebaseClient _client;
        private readonly ShellViewModel _vm;
        private IDisposable? _subscription;

        public ModeHistoryService(ShellViewModel vm)
        {
            _vm    = vm;
            _client = new FirebaseClient(FIREBASE_URL);
        }

        public void Start()
        {
            _ = Task.Run(async () =>
            {
                await LoadHistoryOnceAsync();
                StartHistoryListener();
            });
        }

        // Load existing history on startup
        private async Task LoadHistoryOnceAsync()
        {
            try
            {
                var history = await _client
                    .Child(FirebaseNodes.DevicesRoot)
                    .Child(DEVICE_ID)
                    .Child(FirebaseNodes.ModeHistory)
                    .OnceAsync<ModeHistoryEntry>();

                var entries = history
                    .Select(e => e.Object)
                    .Where(e => e != null)
                    .OrderByDescending(e => e!.ChangedAt)
                    .Take(10) // show last 10
                    .ToList();

                _vm.UpdateModeHistory(entries!);
                Debug.WriteLine($"[ModeHistory] Loaded {entries.Count} entries from Firebase.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModeHistory] Load failed: {ex.Message}");
            }
        }

        // Listen for new history entries in real-time
        private void StartHistoryListener()
        {
            try
            {
                _subscription = _client
                    .Child(FirebaseNodes.DevicesRoot)
                    .Child(DEVICE_ID)
                    .Child(FirebaseNodes.ModeHistory)
                    .AsObservable<ModeHistoryEntry>()
                    .Subscribe(
                        onNext: async _ =>
                        {
                            // Reload full history when any change detected
                            await LoadHistoryOnceAsync();
                        },
                        onError: ex =>
                        {
                            Debug.WriteLine($"[ModeHistory] Listener error: {ex.Message}");
                        });

                Debug.WriteLine("[ModeHistory] Real-time listener started.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModeHistory] Listener failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _client.Dispose();
        }
    }
}
