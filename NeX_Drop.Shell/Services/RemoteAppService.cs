// RemoteAppService — Team Leader Task (Thangamani)
// Listens to Firebase "remote_app_link" node.
// Downloads .exe via HttpClient to local folder.
// Notifies ViewModel → Kumaran's UI shows icon.

using NeX_Drop.Common.Models;
using NeX_Drop.Shell.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace NeX_Drop.Shell.Services
{
    public class RemoteAppService
    {
        private const string URL = "https://nex-launcher-69169-default-rtdb.firebaseio.com/";

        // Local folder for downloaded installers
        private static readonly string DownloadFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "NeX_Drop", "RemoteApps");

        private FirebaseSyncManager? _sync;
        private ShellViewModel?      _vm;

        // Single HttpClient instance — reuse for all downloads
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

        private readonly HashSet<string> _done = new(StringComparer.OrdinalIgnoreCase);

        public void Start(ShellViewModel vm)
        {
            _vm = vm;
            Directory.CreateDirectory(DownloadFolder);

            _sync = new FirebaseSyncManager(URL);
            _sync.OnRemoteAppLinkReceived += async link => await DownloadAsync(link);
            _sync.StartRemoteAppLinkListener();
        }

        // Team leader: "HttpClient use panni background-la antha .exe-ah download panni oru local folder-la vai"
        private async Task DownloadAsync(RemoteAppLink link)
        {
            var url = link.DownloadUrl;
            if (_done.Contains(url)) { NotifyIfExists(url, link.UpdatedBy); return; }

            var localPath = GetLocalPath(url);

            try
            {
                Debug.WriteLine($"[RemoteApp] Downloading: {url}");

                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var src  = await response.Content.ReadAsStreamAsync();
                await using var dest = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await src.CopyToAsync(dest);

                Debug.WriteLine($"[RemoteApp] Saved to: {localPath}");
                _done.Add(url);

                // Notify Kumaran's UI
                var name = MakeDisplayName(localPath);
                _vm?.AddRemoteApp(localPath, name, link.UpdatedBy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemoteApp] Download failed: {ex.Message}");
            }
        }

        private void NotifyIfExists(string url, string pushedBy)
        {
            var path = GetLocalPath(url);
            if (File.Exists(path)) _vm?.AddRemoteApp(path, MakeDisplayName(path), pushedBy);
        }

        private static string GetLocalPath(string url)
        {
            try
            {
                var name = Path.GetFileName(new Uri(url).LocalPath);
                if (string.IsNullOrWhiteSpace(name)) name = "RemoteApp.exe";
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                return Path.Combine(DownloadFolder, name);
            }
            catch { return Path.Combine(DownloadFolder, $"App_{DateTime.Now:yyyyMMddHHmmss}.exe"); }
        }

        private static string MakeDisplayName(string path)
        {
            var n = Path.GetFileNameWithoutExtension(path)
                        .Replace("_", " ").Replace("-", " ")
                        .Replace("Setup", "").Replace("Installer", "")
                        .Replace("install", "").Trim();
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(n.ToLower());
        }

        public void Stop() => _sync?.StopAll();
    }
}
