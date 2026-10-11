// NeX_Drop.Shell — NexEventLogger
// NFR: 99% uptime logging — Windows Event Log full structured logging
//
// PRD NFR:
//   - Windows client uptime during active session > 99%
//   - Kill-switch detection-to-termination latency < 500ms (logged here)
//   - Mode-switch propagation latency < 2 seconds (logged here)
//   - Heartbeat delivery success rate > 99.5%
//   - Unauthorized write attempts = 0 (logged here)
//
// Logs to: Windows Event Log → Application → Source "NeX_Drop"
// Also logs to: %LOCALAPPDATA%\NeX_Drop\Logs\nex_drop_YYYY-MM-DD.log
//
// Owner: Thangamani (Windows Core Logic) — feature/win-logic

using System.Diagnostics;
using System.IO;

namespace NeX_Drop.Shell.Services
{
    public enum NexEventId
    {
        // Startup / Shutdown
        ShellStarted         = 1000,
        ShellStopped         = 1001,
        ServiceStarted       = 1002,

        // Mode events (FR-4)
        ModeChanged          = 2000,
        ModeChangePropagated = 2001,  // latency logged here

        // Heartbeat (FR-5)
        HeartbeatSent        = 3000,
        HeartbeatFailed      = 3001,
        DeviceWentOffline    = 3002,
        DeviceCameOnline     = 3003,

        // Kill-switch (FR-3)
        ProcessKilled        = 4000,
        KillLatencyWarning   = 4001,  // >500ms
        BypassAttemptBlocked = 4002,

        // Registry (FR-1)
        ShellRegistered      = 5000,
        RegistryTamperDetected = 5001,
        ShellReverted        = 5002,

        // Auth (FR-6)
        AuthTokenRefreshed   = 6000,
        AuthTokenFailed      = 6001,
        UnauthorizedWriteAttempt = 6002, // PRD §1.3: must be 0

        // Remote App (FR-7)
        RemoteAppReceived    = 7000,
        RemoteAppDownloaded  = 7001,
        RemoteAppFailed      = 7002,

        // NFR uptime
        UptimeReport         = 9000,
    }

    public static class NexEventLogger
    {
        private const string EventSource = "NeX_Drop";
        private const string EventLog    = "Application";

        private static readonly string LogFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "NeX_Drop", "Logs");

        private static readonly object _lock = new();

        // Uptime tracking
        private static readonly DateTime _startTime    = DateTime.UtcNow;
        private static int  _heartbeatTotal   = 0;
        private static int  _heartbeatSuccess = 0;
        private static int  _killCount        = 0;
        private static int  _modeChanges      = 0;

        static NexEventLogger()
        {
            Directory.CreateDirectory(LogFolder);
            EnsureEventSource();
        }

        // ── Core logging method ───────────────────────────────────────────
        public static void Log(NexEventId eventId, string message,
                               EventLogEntryType type = EventLogEntryType.Information)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var entry     = $"[{timestamp}] [{eventId}] {message}";

            // 1. Debug output
            Debug.WriteLine($"[NeX_Drop] {entry}");

            // 2. File log
            WriteToFile(entry);

            // 3. Windows Event Log
            WriteToEventLog(eventId, message, type);
        }

        // ── Specific NFR log methods ──────────────────────────────────────

        /// <summary>PRD NFR: Mode-switch propagation latency &lt; 2 seconds.</summary>
        public static void LogModeChange(string oldMode, string newMode, long latencyMs)
        {
            _modeChanges++;
            var msg = $"Mode changed: {oldMode} → {newMode} | Latency: {latencyMs}ms";

            if (latencyMs > 2000)
            {
                Log(NexEventId.ModeChangePropagated, $"⚠ {msg} EXCEEDED 2s target!", EventLogEntryType.Warning);
            }
            else
            {
                Log(NexEventId.ModeChangePropagated, $"✓ {msg} (within 2s target)");
            }
        }

        /// <summary>PRD NFR: Heartbeat delivery success rate &gt; 99.5%.</summary>
        public static void LogHeartbeat(bool success, string? error = null)
        {
            _heartbeatTotal++;
            if (success)
            {
                _heartbeatSuccess++;
                Log(NexEventId.HeartbeatSent, $"Heartbeat sent. Success rate: {HeartbeatSuccessRate:P1}");
            }
            else
            {
                Log(NexEventId.HeartbeatFailed, $"Heartbeat FAILED: {error} | Success rate: {HeartbeatSuccessRate:P1}",
                    EventLogEntryType.Warning);

                // Warn if success rate drops below 99.5%
                if (_heartbeatTotal > 10 && HeartbeatSuccessRate < 0.995)
                    Log(NexEventId.HeartbeatFailed,
                        $"⚠ Heartbeat success rate {HeartbeatSuccessRate:P1} below 99.5% NFR target!",
                        EventLogEntryType.Warning);
            }
        }

        /// <summary>PRD NFR: Kill-switch detection-to-termination latency &lt; 500ms.</summary>
        public static void LogProcessKilled(string procName, string reason, long latencyMs)
        {
            _killCount++;
            var msg = $"Killed: {procName} | Reason: {reason} | Latency: {latencyMs}ms";

            if (latencyMs > 500)
                Log(NexEventId.KillLatencyWarning, $"⚠ {msg} EXCEEDED 500ms target!", EventLogEntryType.Warning);
            else
                Log(NexEventId.ProcessKilled, $"✓ {msg}");
        }

        /// <summary>PRD §1.3: Unauthorized write attempts must be 0.</summary>
        public static void LogUnauthorizedWrite(string attemptedNode, string attemptedBy)
        {
            Log(NexEventId.UnauthorizedWriteAttempt,
                $"🚨 UNAUTHORIZED WRITE ATTEMPT: node={attemptedNode} by={attemptedBy}",
                EventLogEntryType.Error);
        }

        /// <summary>PRD NFR: Uptime report — logged every hour.</summary>
        public static void LogUptimeReport()
        {
            var uptime = DateTime.UtcNow - _startTime;
            var report = $"Uptime: {uptime:d'd 'h'h 'm'm'} | " +
                         $"Heartbeat success: {HeartbeatSuccessRate:P1} ({_heartbeatSuccess}/{_heartbeatTotal}) | " +
                         $"Kills: {_killCount} | Mode changes: {_modeChanges}";
            Log(NexEventId.UptimeReport, report);
        }

        private static double HeartbeatSuccessRate =>
            _heartbeatTotal == 0 ? 1.0 : (double)_heartbeatSuccess / _heartbeatTotal;

        // ── File logging ─────────────────────────────────────────────────
        private static void WriteToFile(string entry)
        {
            try
            {
                var filePath = Path.Combine(LogFolder, $"nex_drop_{DateTime.Now:yyyy-MM-dd}.log");
                lock (_lock)
                {
                    File.AppendAllText(filePath, entry + Environment.NewLine);
                }
            }
            catch { /* Never let logging crash the app */ }
        }

        // ── Windows Event Log ─────────────────────────────────────────────
        private static void WriteToEventLog(NexEventId id, string message, EventLogEntryType type)
        {
            try
            {
                if (EventLog.SourceExists(EventSource))
                    EventLog.WriteEntry(EventSource, message, type, (int)id);
            }
            catch { /* Event log may not be available in dev */ }
        }

        private static void EnsureEventSource()
        {
            try
            {
                if (!EventLog.SourceExists(EventSource))
                    EventLog.CreateEventSource(EventSource, EventLog);
            }
            catch { /* Requires admin — silently skip in dev */ }
        }
    }
}
