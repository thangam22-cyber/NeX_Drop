using NeX_Drop.Common.Models;
using NeX_Drop.Shell.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NeX_Drop.Shell.ViewModels
{
    public class AppTileItem
    {
        public string Name        { get; set; } = string.Empty;
        public string Icon        { get; set; } = string.Empty;  // emoji or image path
        public string LaunchTarget { get; set; } = string.Empty;
        public bool   IsRemote    { get; set; } = false;
        public string PushedBy    { get; set; } = string.Empty;
    }

    public class ShellViewModel : INotifyPropertyChanged
    {
        // ── Clock ──────────────────────────────────────────────────────────────
        private readonly DispatcherTimer _clock;
        private string _time = "";
        public string CurrentTime { get => _time; private set { _time = value; OnPC(); } }

        // ── Mode ───────────────────────────────────────────────────────────────
        private string _mode = ModeConstants.Mode1;
        public string ActiveMode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                OnPC(); OnPC(nameof(ModeDisplay)); OnPC(nameof(ModeDescription));
                OnPC(nameof(ModeColor)); OnPC(nameof(StatusBarText));
                RebuildAppTiles();
            }
        }

        public string ModeDisplay => ActiveMode switch
        {
            ModeConstants.Mode1     => "Mode 1",
            ModeConstants.Study     => "Study",
            ModeConstants.Kids      => "Kids",
            ModeConstants.Developer => "Developer",
            ModeConstants.Lockdown  => "Lockdown",
            _                       => ActiveMode
        };

        public string ModeDescription => ActiveMode switch
        {
            ModeConstants.Mode1     => "Application access, UI behavior and network restrictions are enforced.",
            ModeConstants.Study     => "Study-focused access. Browser and notes enabled.",
            ModeConstants.Kids      => "Safe environment. Camera and Calculator only.",
            ModeConstants.Developer => "Extended developer access. Audit logging active.",
            ModeConstants.Lockdown  => "Emergency lockdown. All access suspended.",
            _                       => ""
        };

        public Brush ModeColor => ActiveMode switch
        {
            ModeConstants.Lockdown => new SolidColorBrush(Color.FromRgb(248, 81, 73)),
            ModeConstants.Kids     => new SolidColorBrush(Color.FromRgb(63, 185, 80)),
            _                      => new SolidColorBrush(Color.FromRgb(88, 166, 255))
        };

        public string StatusBarText => $"System locked down  •  {ModeDisplay} (Active)  •  Tamper protection active";

        // ── Connection ─────────────────────────────────────────────────────────
        private bool _online = false;
        public bool IsOnline
        {
            get => _online;
            set { _online = value; OnPC(); OnPC(nameof(StatusColor)); OnPC(nameof(StatusText)); OnPC(nameof(HeartbeatText)); }
        }
        public string StatusText   => IsOnline ? "Online" : "Offline";
        public Brush  StatusColor  => IsOnline
            ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
            : new SolidColorBrush(Color.FromRgb(248, 81, 73));

        private string _lastPing = "Never";
        public string HeartbeatText
        {
            get => _lastPing;
            set { _lastPing = value; OnPC(); }
        }

        // ── Device Info ─────────────────────────────────────────────────────────
        public string DeviceId { get; } = "NEX-WIN-001";
        public string Version  { get; } = "v1.0.0";

        // ── App Tiles ───────────────────────────────────────────────────────────
        public ObservableCollection<AppTileItem> AppTiles { get; } = new();

        // Remote apps from FR-7
        private readonly List<AppTileItem> _remoteApps = new();

        public bool HasRemoteApps => _remoteApps.Count > 0;

        // ── Admin Panel ─────────────────────────────────────────────────────────
        private bool _showAdminPanel = false;
        public bool ShowAdminPanel
        {
            get => _showAdminPanel;
            set { _showAdminPanel = value; OnPC(); AdminPassword = ""; AdminError = ""; OnPC(nameof(AdminPassword)); OnPC(nameof(AdminError)); }
        }

        private string _adminPassword = "";
        public string AdminPassword
        {
            get => _adminPassword;
            set { _adminPassword = value; OnPC(); }
        }

        private string _adminError = "";
        public string AdminError
        {
            get => _adminError;
            set { _adminError = value; OnPC(); OnPC(nameof(HasAdminError)); }
        }
        public bool HasAdminError => !string.IsNullOrEmpty(_adminError);

        // ── Commands ────────────────────────────────────────────────────────────
        public ICommand LaunchAppCommand        { get; }
        public ICommand OpenSettingsCommand     { get; }
        public ICommand AdminLoginCommand       { get; }
        public ICommand CancelAdminCommand      { get; }

        // ── Constructor ─────────────────────────────────────────────────────────
        public ShellViewModel()
        {
            LaunchAppCommand    = new RelayCommand<AppTileItem>(LaunchApp);
            OpenSettingsCommand = new RelayCommand(_ => ShowAdminPanel = true);
            AdminLoginCommand   = new RelayCommand(_ => TryAdminLogin());
            CancelAdminCommand  = new RelayCommand(_ => ShowAdminPanel = false);

            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (_, __) => CurrentTime = DateTime.Now.ToString("hh:mm tt\ndd/MM/yyyy");
            _clock.Start();
            CurrentTime = DateTime.Now.ToString("hh:mm tt\ndd/MM/yyyy");

            RebuildAppTiles();
        }

        // ── Build whitelisted app tiles for current mode ──────────────────────
        private void RebuildAppTiles()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppTiles.Clear();

                var staticApps = GetStaticAppsForMode(ActiveMode);
                foreach (var a in staticApps) AppTiles.Add(a);

                // Add remote apps at the end
                foreach (var r in _remoteApps) AppTiles.Add(r);
            });
        }

        private static List<AppTileItem> GetStaticAppsForMode(string mode) => mode switch
        {
            ModeConstants.Mode1 => new List<AppTileItem>
            {
                new() { Name="Microsoft Edge",  Icon="🌐", LaunchTarget="msedge" },
                new() { Name="Microsoft Word",  Icon="📝", LaunchTarget="winword" },
                new() { Name="Microsoft Excel", Icon="📊", LaunchTarget="excel" },
                new() { Name="PDF Viewer",      Icon="📄", LaunchTarget="AcroRd32" },
                new() { Name="Notepad",         Icon="🗒️",  LaunchTarget="notepad" },
                new() { Name="Calculator",      Icon="🧮", LaunchTarget="calculator:" },
                new() { Name="Paint",           Icon="🎨", LaunchTarget="mspaint" },
                new() { Name="File Explorer",   Icon="📁", LaunchTarget="explorer" },
                new() { Name="Camera",          Icon="📷", LaunchTarget="windowscamera:" },
                new() { Name="Settings",        Icon="⚙️",  LaunchTarget="__settings__" },
            },
            ModeConstants.Study => new List<AppTileItem>
            {
                new() { Name="Microsoft Edge",  Icon="🌐", LaunchTarget="msedge" },
                new() { Name="Notepad",         Icon="🗒️",  LaunchTarget="notepad" },
                new() { Name="Calculator",      Icon="🧮", LaunchTarget="calculator:" },
                new() { Name="Settings",        Icon="⚙️",  LaunchTarget="__settings__" },
            },
            ModeConstants.Kids => new List<AppTileItem>
            {
                new() { Name="Camera",      Icon="📷", LaunchTarget="windowscamera:" },
                new() { Name="Calculator",  Icon="🧮", LaunchTarget="calculator:" },
                new() { Name="Paint",       Icon="🎨", LaunchTarget="mspaint" },
                new() { Name="Settings",    Icon="⚙️",  LaunchTarget="__settings__" },
            },
            ModeConstants.Developer => new List<AppTileItem>
            {
                new() { Name="Microsoft Edge",  Icon="🌐", LaunchTarget="msedge" },
                new() { Name="VS Code",         Icon="💻", LaunchTarget="code" },
                new() { Name="Windows Terminal",Icon="🖥️", LaunchTarget="wt" },
                new() { Name="Notepad",         Icon="🗒️",  LaunchTarget="notepad" },
                new() { Name="File Explorer",   Icon="📁", LaunchTarget="explorer" },
                new() { Name="Calculator",      Icon="🧮", LaunchTarget="calculator:" },
                new() { Name="Settings",        Icon="⚙️",  LaunchTarget="__settings__" },
            },
            _ => new List<AppTileItem>
            {
                new() { Name="Settings", Icon="⚙️", LaunchTarget="__settings__" },
            }
        };

        // ── Launch App — FOREGROUND FIX ────────────────────────────────────────
        private void LaunchApp(AppTileItem? tile)
        {
            if (tile == null) return;

            // Settings button → open admin panel
            if (tile.LaunchTarget == "__settings__")
            {
                ShowAdminPanel = true;
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    UseShellExecute = true,
                };

                // URI protocols (calculator:, windowscamera:)
                if (tile.LaunchTarget.EndsWith(":"))
                {
                    psi.FileName = tile.LaunchTarget;
                }
                else if (File.Exists(tile.LaunchTarget))
                {
                    // Remote app — direct exe path
                    psi.FileName = tile.LaunchTarget;
                }
                else
                {
                    psi.FileName = tile.LaunchTarget;
                }

                var proc = Process.Start(psi);

                // FOREGROUND FIX: bring launched window to front
                if (proc != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(800); // wait for window to appear
                        BringProcessToForeground(proc);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NeX_Drop] Launch failed '{tile.LaunchTarget}': {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private static void BringProcessToForeground(Process proc)
        {
            try
            {
                proc.Refresh();
                var hwnd = proc.MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                {
                    // Find by process name
                    var found = Process.GetProcessesByName(proc.ProcessName).FirstOrDefault();
                    hwnd = found?.MainWindowHandle ?? IntPtr.Zero;
                }
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, 9); // SW_RESTORE
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                }
            }
            catch { /* process may have exited */ }
        }

        // ── Admin Login — Password: 2026 ───────────────────────────────────────
        private void TryAdminLogin()
        {
            if (AdminPassword == "2026")
            {
                ShowAdminPanel = false;
                RestoreExplorer();
            }
            else
            {
                AdminError = "Incorrect password. Access denied.";
            }
        }

        // ── Restore explorer.exe shell ─────────────────────────────────────────
        private void RestoreExplorer()
        {
            try
            {
                // 1. Revert registry shell key to explorer.exe
                const string regPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, writable: true))
                {
                    key?.SetValue("Shell", "explorer.exe",
                        Microsoft.Win32.RegistryValueKind.String);
                }

                // 2. Re-enable Task Manager
                const string policyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(policyPath, writable: true))
                {
                    key?.DeleteValue("DisableTaskMgr", throwOnMissingValue: false);
                }

                // 3. Launch explorer.exe immediately so desktop appears
                Process.Start(new ProcessStartInfo("explorer.exe")
                    { UseShellExecute = true });

                // 4. Exit NeX_Drop shell
                Application.Current.Dispatcher.Invoke(() =>
                    Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                AdminError = $"Restore failed: {ex.Message}";
            }
        }

        // ── Mode History (PRD §8) ───────────────────────────────────────────────
        public ObservableCollection<ModeHistoryDisplayItem> ModeHistory { get; } = new();
        public bool HasModeHistory => ModeHistory.Count > 0;

        public void UpdateModeHistory(List<NeX_Drop.Common.Models.ModeHistoryEntry> entries)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ModeHistory.Clear();
                foreach (var e in entries)
                {
                    ModeHistory.Add(new ModeHistoryDisplayItem
                    {
                        ModeDisplay = e.Mode.ToUpperInvariant().Replace("MODE", "MODE "),
                        ChangedAt   = FormatTimestamp(e.ChangedAt),
                        ChangedBy   = string.IsNullOrEmpty(e.ChangedBy) ? "Admin" : e.ChangedBy
                    });
                }
                OnPC(nameof(HasModeHistory));
            });
        }

        private static string FormatTimestamp(string iso)
        {
            if (DateTime.TryParse(iso, out var dt))
                return dt.ToLocalTime().ToString("dd MMM HH:mm:ss");
            return iso;
        }

        // ── Called by services ─────────────────────────────────────────────────
        public void ApplyModeChange(string newMode)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Log latency (NFR: <2s)
                NexEventLogger.LogModeChange(ActiveMode, newMode, 0);
                ActiveMode    = newMode;
                IsOnline      = true;
                HeartbeatText = "Just now";
            });
        }

        public void MarkOnline()  => Application.Current.Dispatcher.Invoke(() => IsOnline = true);
        public void MarkOffline() => Application.Current.Dispatcher.Invoke(() => IsOnline = false);

        public void UpdateHeartbeatText(string text) =>
            Application.Current.Dispatcher.Invoke(() => HeartbeatText = text);

        public void AddRemoteApp(string localPath, string displayName, string pushedBy)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_remoteApps.Any(r => r.LaunchTarget == localPath)) return;
                _remoteApps.Add(new AppTileItem
                {
                    Name         = displayName,
                    Icon         = "📦",
                    LaunchTarget = localPath,
                    IsRemote     = true,
                    PushedBy     = pushedBy
                });
                RebuildAppTiles();
                OnPC(nameof(HasRemoteApps));
            });
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPC([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ModeHistoryDisplayItem
    {
        public string ModeDisplay { get; set; } = string.Empty;
        public string ChangedAt   { get; set; } = string.Empty;
        public string ChangedBy   { get; set; } = string.Empty;
    }

    // ── Commands ────────────────────────────────────────────────────────────────
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _exec;
        public RelayCommand(Action<T?> exec) => _exec = exec;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _exec(p is T t ? t : default);
        public event EventHandler? CanExecuteChanged
        { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec;
        public RelayCommand(Action<object?> exec) => _exec = exec;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _exec(p);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

// ── Extension methods appended for FR-5, NFR, Mode History ──────────────
// These extend ShellViewModel — added as partial class methods
