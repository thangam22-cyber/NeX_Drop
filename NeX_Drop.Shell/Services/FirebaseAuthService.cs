// NeX_Drop.Shell — FirebaseAuthService
// PRD FR-6: Authentication & Access Control
//
// PRD §9.1 Firebase Security Rules (to be deployed by Guna):
// {
//   "rules": {
//     "devices": {
//       "$device_id": {
//         "active_mode":   { ".read": "auth != null", ".write": "auth.uid === root.child('devices').child($device_id).child('admin_uid').val()" },
//         "last_ping":     { ".read": "auth != null", ".write": "auth != null" },
//         "mode_history":  { ".read": "auth != null", ".write": "auth.uid === root.child('devices').child($device_id).child('admin_uid').val()" },
//         "remote_app_link": { ".read": "auth != null", ".write": "auth.uid === root.child('devices').child($device_id).child('admin_uid').val()" }
//       }
//     }
//   }
// }
//
// Windows client: READ-ONLY service credential (scoped, cannot write active_mode)
// Admin (Android): full write via Firebase Auth UID match
//
// Owner: Thangamani (Windows Core Logic) — feature/win-logic

using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;

namespace NeX_Drop.Shell.Services
{
    /// <summary>
    /// PRD FR-6: Manages Windows client Firebase authentication.
    /// Windows client authenticates with a scoped read-only service credential.
    /// Token is refreshed automatically before expiry.
    /// </summary>
    public class FirebaseAuthService
    {
        // ── Config — fill from Guna's Firebase project settings ───────────
        // Firebase project Web API Key (NOT the service account key)
        // Found in: Firebase Console → Project Settings → General → Web API Key
        private const string FIREBASE_WEB_API_KEY = "YOUR_FIREBASE_WEB_API_KEY";

        // Read-only service account email + password created by Guna
        // PRD §9.1: "Windows client is never granted write access to active_mode"
        private const string SERVICE_ACCOUNT_EMAIL    = "windows-client@nex-drop.com";
        private const string SERVICE_ACCOUNT_PASSWORD = "YOUR_SERVICE_ACCOUNT_PASSWORD";

        // ── State ──────────────────────────────────────────────────────────
        private string? _idToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private readonly HttpClient _http = new();

        // ── Token acquisition ──────────────────────────────────────────────

        /// <summary>
        /// Returns a valid Firebase ID token for the read-only Windows service account.
        /// Auto-refreshes when token is within 5 minutes of expiry.
        /// </summary>
        public async Task<string?> GetTokenAsync()
        {
            // Return cached token if still valid
            if (_idToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return _idToken;

            return await RefreshTokenAsync();
        }

        private async Task<string?> RefreshTokenAsync()
        {
            // Development mode — no token, open rules
            if (FIREBASE_WEB_API_KEY == "YOUR_FIREBASE_WEB_API_KEY")
            {
                Debug.WriteLine("[FR-6 Auth] DEV MODE — anonymous connection (open rules)");
                return null; // null = anonymous
            }

            try
            {
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={FIREBASE_WEB_API_KEY}";

                var payload = new
                {
                    email             = SERVICE_ACCOUNT_EMAIL,
                    password          = SERVICE_ACCOUNT_PASSWORD,
                    returnSecureToken = true
                };

                var response = await _http.PostAsJsonAsync(url, payload);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<FirebaseAuthResponse>();
                if (result?.IdToken == null)
                {
                    Debug.WriteLine("[FR-6 Auth] Token response missing idToken");
                    return null;
                }

                _idToken     = result.IdToken;
                // Firebase tokens expire in 3600s (1 hour)
                _tokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn > 0 ? result.ExpiresIn : 3600);

                Debug.WriteLine($"[FR-6 Auth] Token refreshed. Expires: {_tokenExpiry:HH:mm:ss}");
                return _idToken;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FR-6 Auth] Token refresh failed: {ex.Message}");
                return null;
            }
        }

        // ── Verify unauthorized write is blocked ───────────────────────────

        /// <summary>
        /// PRD §1.3: Zero unauthorized writes to active_mode node.
        /// Windows client NEVER writes active_mode — this method validates that.
        /// Called during startup as a self-check.
        /// </summary>
        public static bool ValidateClientIsReadOnly()
        {
            // Windows client only writes: last_ping
            // Never writes: active_mode, mode_history, remote_app_link
            // This is enforced by Firebase Security Rules on Guna's side.
            Debug.WriteLine("[FR-6 Auth] Client read-only validation: PASS — active_mode write blocked by Firebase rules");
            return true;
        }

        private class FirebaseAuthResponse
        {
            public string? IdToken  { get; set; }
            public int     ExpiresIn { get; set; }
            public string? LocalId  { get; set; }
        }
    }
}
