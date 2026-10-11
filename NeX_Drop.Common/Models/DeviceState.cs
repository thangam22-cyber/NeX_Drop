// NeX_Drop.Common — All Models
// PRD §8 binding data contract

using Newtonsoft.Json;

namespace NeX_Drop.Common.Models
{
    public class DeviceState
    {
        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = "laptop_01";

        [JsonProperty("active_mode")]
        public string ActiveMode { get; set; } = ModeConstants.Mode1;

        [JsonProperty("last_ping")]
        public string LastPing { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonProperty("admin_uid")]
        public string AdminUid { get; set; } = string.Empty;

        [JsonProperty("mode_history")]
        public List<ModeHistoryEntry> ModeHistory { get; set; } = new();

        [JsonProperty("remote_app_link")]
        public RemoteAppLink? RemoteAppLink { get; set; }

        // FR-5: offline_alert node — written by Firebase Cloud Function when heartbeat stales
        [JsonProperty("offline_alert")]
        public OfflineAlert? OfflineAlert { get; set; }
    }

    public class ModeHistoryEntry
    {
        [JsonProperty("mode")]
        public string Mode { get; set; } = string.Empty;

        [JsonProperty("changed_at")]
        public string ChangedAt { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonProperty("changed_by")]
        public string ChangedBy { get; set; } = string.Empty;
    }

    public class RemoteAppLink
    {
        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;
        [JsonProperty("updatedBy")]
        public string UpdatedBy { get; set; } = string.Empty;
        [JsonProperty("updatedAt")]
        public long UpdatedAt { get; set; }
    }

    // FR-5: Offline alert — written when device misses 3 heartbeats (90s)
    public class OfflineAlert
    {
        [JsonProperty("detected_at")]
        public string DetectedAt { get; set; } = string.Empty;
        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public static class ModeConstants
    {
        public const string Mode1     = "mode1";
        public const string Study     = "study";
        public const string Kids      = "kids";
        public const string Developer = "developer";
        public const string Lockdown  = "lockdown";
    }

    public static class FirebaseNodes
    {
        public const string DevicesRoot   = "devices";
        public const string DeviceId      = "laptop_01";
        public const string ActiveMode    = "active_mode";
        public const string LastPing      = "last_ping";
        public const string ModeHistory   = "mode_history";
        public const string RemoteAppLink = "remote_app_link";
        public const string OfflineAlert  = "offline_alert";
    }

    public static class HeartbeatConfig
    {
        public const int IntervalSeconds       = 30;   // PRD FR-5: default 30s
        public const int OfflineThresholdCount = 3;    // PRD FR-5: 3 missed = offline
        public static int OfflineThresholdSeconds => IntervalSeconds * OfflineThresholdCount; // 90s
    }

    public static class ModeWhitelists
    {
        public static readonly IReadOnlyList<string> Mode1Whitelist = new List<string>
        {
            "microsoftedge", "msedge",
            "winword", "excel",
            "acrord32", "acrobat",
            "notepad",
            "calculator", "calc",
            "mspaint",
            "explorer",
            "windowscamera", "camera",
            "systemsettings",
            "nex_drop.shell", "nex_drop.service",
        };

        public static readonly IReadOnlyList<string> StudyWhitelist = new List<string>
        {
            "microsoftedge", "msedge",
            "notepad", "winword",
            "calculator", "calc",
            "nex_drop.shell", "nex_drop.service",
        };

        public static readonly IReadOnlyList<string> KidsWhitelist = new List<string>
        {
            "windowscamera", "camera",
            "calculator", "calc",
            "mspaint",
            "nex_drop.shell", "nex_drop.service",
        };

        public static readonly IReadOnlyList<string> DeveloperWhitelist = new List<string>
        {
            "microsoftedge", "msedge",
            "code", "devenv",
            "windowsterminal", "wt",
            "notepad", "winword", "excel",
            "calculator", "calc",
            "explorer",
            "nex_drop.shell", "nex_drop.service",
        };

        public static readonly IReadOnlyList<string> LockdownWhitelist = new List<string>
        {
            "nex_drop.shell", "nex_drop.service",
        };

        public static readonly IReadOnlyList<string> AlwaysBlacklisted = new List<string>
        {
            "taskmgr", "cmd", "powershell", "powershell_ise", "pwsh",
            "regedit", "regedt32", "mmc", "msconfig", "gpedit",
        };

        public static IReadOnlyList<string> GetWhitelistForMode(string mode) => mode switch
        {
            ModeConstants.Mode1     => Mode1Whitelist,
            ModeConstants.Study     => StudyWhitelist,
            ModeConstants.Kids      => KidsWhitelist,
            ModeConstants.Developer => DeveloperWhitelist,
            ModeConstants.Lockdown  => LockdownWhitelist,
            _                       => Mode1Whitelist
        };
    }
}
