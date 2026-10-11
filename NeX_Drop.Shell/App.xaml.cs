using Microsoft.Extensions.DependencyInjection;
using NeX_Drop.Shell.Services;
using NeX_Drop.Shell.ViewModels;
using NeX_Drop.Shell.Views;
using System.Windows;

namespace NeX_Drop.Shell
{
    public partial class App : Application
    {
        private ServiceProvider?         _sp;
        private OfflineDetectionService? _offlineDetect;
        private ModeHistoryService?      _modeHistory;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!SingleInstanceGuard.Acquire()) { Shutdown(); return; }

            // Log startup (NFR)
            NexEventLogger.Log(NexEventId.ShellStarted, "NeX_Drop Shell v1.0 started.");

            // FR-6: validate read-only client
            FirebaseAuthService.ValidateClientIsReadOnly();

            var services = new ServiceCollection();
            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<FirebaseListenerService>();
            services.AddSingleton<HeartbeatService>();
            services.AddSingleton<RemoteAppService>();
            services.AddSingleton<FirebaseAuthService>();
            _sp = services.BuildServiceProvider();

            var vm = _sp.GetRequiredService<ShellViewModel>();

            // Start all services
            _sp.GetRequiredService<FirebaseListenerService>().Start(vm);  // FR-4
            _sp.GetRequiredService<HeartbeatService>().Start();            // FR-5 heartbeat

            // FR-5: Offline detection
            _offlineDetect = new OfflineDetectionService(vm);
            _offlineDetect.Start();

            // Mode history listener
            _modeHistory = new ModeHistoryService(vm);
            _modeHistory.Start();

            // FR-7: Remote app download
            _sp.GetRequiredService<RemoteAppService>().Start(vm);

            // NFR: Uptime report every hour
            var uptimeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromHours(1)
            };
            uptimeTimer.Tick += (_, __) => NexEventLogger.LogUptimeReport();
            uptimeTimer.Start();

            new ShellWindow(vm).Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            NexEventLogger.Log(NexEventId.ShellStopped, "NeX_Drop Shell stopped.");
            NexEventLogger.LogUptimeReport();
            _offlineDetect?.Dispose();
            _modeHistory?.Dispose();
            _sp?.Dispose();
            SingleInstanceGuard.Release();
            base.OnExit(e);
        }
    }

    internal static class SingleInstanceGuard
    {
        private static Mutex? _mutex;
        public static bool Acquire()
        {
            _mutex = new Mutex(true, "Global\\NeX_Drop_Shell_v1", out bool created);
            return created;
        }
        public static void Release() => _mutex?.ReleaseMutex();
    }
}
