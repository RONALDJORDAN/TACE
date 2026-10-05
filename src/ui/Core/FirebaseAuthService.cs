using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BotRpaUI.Core
{
    /// <summary>
    /// Autenticação Firebase (Identity Toolkit REST) para o acesso ao Realtime Database.
    /// O RTDB do InnovStock nega tudo por padrão (default deny) e exige `auth` em todos
    /// os nós, então o Bot_RPA faz login com a conta de serviço configurada em
    /// Configurações e anexa `?auth=idToken` em cada chamada. O token vive SÓ em
    /// memória (nunca é gravado em disco); a senha fica no settings.json local.
    /// Sem credenciais configuradas, todo sync de nuvem é desativado com aviso claro.
    /// </summary>
    public static class FirebaseAuthService
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private static string _idToken = string.Empty;
        private static string _refreshToken = string.Empty;
        private static DateTime _expiresAtUtc = DateTime.MinValue;

        public static bool HasCredentials =>
            !string.IsNullOrWhiteSpace(SettingsManager.FirebaseEmail) &&
            !string.IsNullOrWhiteSpace(SettingsManager.FirebasePassword);

        public static void Invalidate()
        {
            _idToken = string.Empty;
            _expiresAtUtc = DateTime.MinValue;
        }

        public static async Task<string> GetIdTokenAsync()
        {
            if (!HasCredentials) return string.Empty;
            if (!string.IsNullOrEmpty(_idToken) && DateTime.UtcNow < _expiresAtUtc.AddMinutes(-5))
                return _idToken;

            await _gate.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_idToken) && DateTime.UtcNow < _expiresAtUtc.AddMinutes(-5))
                    return _idToken;
                if (!string.IsNullOrEmpty(_refreshToken) && await TryRefreshAsync())
                    return _idToken;
                if (await TryLoginAsync())
                    return _idToken;
                return string.Empty;
            }
            finally { _gate.Release(); }
        }

        private static async Task<bool> TryLoginAsync()
        {
            try
            {
                string key = SettingsManager.FirebaseApiKey;
                if (string.IsNullOrWhiteSpace(key)) return false;
                string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" + key;
                var payload = new Dictionary<string, object>
                {
                    ["email"] = SettingsManager.FirebaseEmail.Trim(),
                    ["password"] = SettingsManager.FirebasePassword,
                    ["returnSecureToken"] = true
                };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(url, content);
                if (!response.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("idToken", out var idProp)) return false;
                _idToken = idProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("refreshToken", out var refProp))
                    _refreshToken = refProp.GetString() ?? string.Empty;
                long expiresIn = 3600;
                if (doc.RootElement.TryGetProperty("expiresIn", out var expProp) &&
                    long.TryParse(expProp.GetString(), out var parsed))
                    expiresIn = parsed;
                _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);
                return !string.IsNullOrEmpty(_idToken);
            }
            catch { return false; }
        }

        private static async Task<bool> TryRefreshAsync()
        {
            try
            {
                string key = SettingsManager.FirebaseApiKey;
                if (string.IsNullOrWhiteSpace(key)) return false;
                string url = "https://securetoken.googleapis.com/v1/token?key=" + key;
                var body = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(_refreshToken);
                using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
                using var response = await _http.PostAsync(url, content);
                if (!response.IsSuccessStatusCode) { _refreshToken = string.Empty; return false; }
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("id_token", out var idProp)) return false;
                _idToken = idProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("refresh_token", out var refProp))
                    _refreshToken = refProp.GetString() ?? string.Empty;
                long expiresIn = 3600;
                if (doc.RootElement.TryGetProperty("expires_in", out var expProp) &&
                    long.TryParse(expProp.GetString(), out var parsed))
                    expiresIn = parsed;
                _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);
                return !string.IsNullOrEmpty(_idToken);
            }
            catch { return false; }
        }

        private static string WithAuth(string url, string token)
        {
            if (string.IsNullOrEmpty(token)) return url;
            return url.Contains("?") ? url + "&auth=" + token : url + "?auth=" + token;
        }

        private static async Task<HttpResponseMessage?> SendOnceAsync(HttpMethod method, string url, string? json, string token, HttpClient client)
        {
            var request = new HttpRequestMessage(method, WithAuth(url, token));
            if (json != null)
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return await client.SendAsync(request);
        }

        private static async Task<HttpResponseMessage?> SendWithRetryAsync(HttpMethod method, string url, string? json, HttpClient client)
        {
            string token = await GetIdTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var response = await SendOnceAsync(method, url, json, token, client);
                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    response.Dispose();
                    Invalidate();
                    token = await GetIdTokenAsync();
                    if (string.IsNullOrEmpty(token)) return null;
                    response = await SendOnceAsync(method, url, json, token, client);
                }
                return response;
            }
            catch { return null; }
        }

        public static async Task<string?> GetStringAsync(string url, HttpClient client)
        {
            try
            {
                using var response = await SendWithRetryAsync(HttpMethod.Get, url, null, client);
                if (response == null || !response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStringAsync();
            }
            catch { return null; }
        }

        public static async Task<bool> PutJsonAsync(string url, string json, HttpClient client)
        {
            try
            {
                using var response = await SendWithRetryAsync(HttpMethod.Put, url, json, client);
                return response != null && response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> PatchJsonAsync(string url, string json, HttpClient client)
        {
            try
            {
                using var response = await SendWithRetryAsync(new HttpMethod("PATCH"), url, json, client);
                return response != null && response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
