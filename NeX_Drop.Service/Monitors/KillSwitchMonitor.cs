using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeX_Drop.Common.Models;
using System.Diagnostics;

namespace NeX_Drop.Service.Monitors
{
    public sealed class KillSwitchMonitor : BackgroundService
    {
        private readonly ILogger<KillSwitchMonitor> _log;
        private volatile string _mode = ModeConstants.Mode1;

        private static readonly HashSet<string> OwnProcs = new(StringComparer.OrdinalIgnoreCase)
            { "nex_drop.shell", "nex_drop.service" };

        public KillSwitchMonitor(ILogger<KillSwitchMonitor> log) => _log = log;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _log.LogInformation("[KillSwitch] Started. Mode={Mode}", _mode);
            while (!ct.IsCancellationRequested)
            {
                Scan();
                await Task.Delay(250, ct).ContinueWith(_ => { });
            }
        }

        private void Scan()
        {
            var whitelist   = ModeWhitelists.GetWhitelistForMode(_mode);
            var blacklisted = ModeWhitelists.AlwaysBlacklisted;

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var name = p.ProcessName.ToLowerInvariant();
                    if (OwnProcs.Contains(name)) continue;

                    bool isBlacklisted = blacklisted.Any(b =>
                        string.Equals(b, name, StringComparison.OrdinalIgnoreCase));

                    bool inWhitelist = whitelist.Any(w =>
                        string.Equals(w, name, StringComparison.OrdinalIgnoreCase));

                    if (isBlacklisted)
                        Kill(p, "BLACKLISTED");
                    else if (!inWhitelist && p.MainWindowHandle != IntPtr.Zero)
                        Kill(p, "NOT_IN_WHITELIST");
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        private void Kill(Process p, string reason)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                p.Kill(true);
                sw.Stop();

                // NFR: log kill latency — must be <500ms
                var latency = sw.ElapsedMilliseconds;
                if (latency > 500)
                    _log.LogWarning("[KillSwitch] ⚠ Kill latency {Ms}ms > 500ms target | {Name} ({Reason})",
                        latency, p.ProcessName, reason);
                else
                    _log.LogInformation("[KillSwitch] Killed {Name} ({Reason}) in {Ms}ms",
                        p.ProcessName, reason, latency);
            }
            catch { }
        }

        public void UpdateMode(string newMode)
        {
            _log.LogInformation("[KillSwitch] Mode updated: {Mode}", newMode);
            _mode = newMode;
        }
    }
}
