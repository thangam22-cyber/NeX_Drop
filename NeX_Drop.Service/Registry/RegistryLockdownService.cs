using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace NeX_Drop.Service.Registry
{
    public sealed class RegistryLockdownService : BackgroundService
    {
        private readonly ILogger<RegistryLockdownService> _log;
        private const string WinlogonPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        private const string NexShell     = @"C:\Program Files\NeX_Drop\NeX_Drop.Shell.exe";

        public RegistryLockdownService(ILogger<RegistryLockdownService> log) => _log = log;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            ApplyShellReg();
            DisableTaskMgr();

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(60_000, ct).ContinueWith(_ => { });
                VerifyShellReg();
            }
        }

        public void ApplyShellReg()
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                         .OpenSubKey(WinlogonPath, writable: true);
                if (k == null) { _log.LogWarning("[Registry] Cannot open Winlogon (need admin)"); return; }
                k.SetValue("Shell", NexShell, RegistryValueKind.String);
                _log.LogInformation("[Registry] Shell set to NeX_Drop");
            }
            catch (Exception ex) { _log.LogError("[Registry] Shell set failed: {E}", ex.Message); }
        }

        private void DisableTaskMgr()
        {
            try
            {
                // Use RegistryKey.OpenBaseKey instead of Registry.CurrentUser to avoid namespace conflict
                using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                using var k    = hkcu.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System",
                    writable: true);
                k?.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                _log.LogInformation("[Registry] Task Manager disabled");
            }
            catch (Exception ex) { _log.LogWarning("[Registry] DisableTaskMgr failed: {E}", ex.Message); }
        }

        private void VerifyShellReg()
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                         .OpenSubKey(WinlogonPath);
                var cur = k?.GetValue("Shell") as string;
                if (!string.Equals(cur, NexShell, StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogWarning("[Registry] Shell tampered ({Cur}) — reapplying...", cur);
                    ApplyShellReg();
                }
            }
            catch (Exception ex) { _log.LogDebug("[Registry] Verify error: {E}", ex.Message); }
        }

        public void RevertToExplorer()
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                         .OpenSubKey(WinlogonPath, writable: true);
                k?.SetValue("Shell", "explorer.exe", RegistryValueKind.String);
                _log.LogInformation("[Registry] Shell reverted to explorer.exe");
            }
            catch (Exception ex) { _log.LogError("[Registry] Revert failed: {E}", ex.Message); }
        }
    }
}
