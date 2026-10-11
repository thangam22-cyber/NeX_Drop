using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace NeX_Drop.Service.Monitors
{
    public sealed class ShellWatchdogService : BackgroundService
    {
        private readonly ILogger<ShellWatchdogService> _log;
        private const string ShellExe = @"C:\Program Files\NeX_Drop\NeX_Drop.Shell.exe";

        public ShellWatchdogService(ILogger<ShellWatchdogService> log) => _log = log;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (!Process.GetProcessesByName("NeX_Drop.Shell").Any())
                {
                    _log.LogWarning("[Watchdog] Shell not running — restarting...");
                    try { Process.Start(new ProcessStartInfo(ShellExe) { UseShellExecute = true }); }
                    catch (Exception ex) { _log.LogError("[Watchdog] Restart failed: {Msg}", ex.Message); }
                }
                await Task.Delay(3000, ct).ContinueWith(_ => { });
            }
        }
    }
}
