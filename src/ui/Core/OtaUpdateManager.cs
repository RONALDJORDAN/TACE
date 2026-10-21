using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BotRpaUI.Core
{
    public class OtaManifest
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; } = "";

        [JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; } = false;

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = "";

        [JsonPropertyName("published_at")]
        public long PublishedAt { get; set; } = 0;
    }

    public class OtaDeviceTelemetry
    {
        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = Environment.MachineName;

        [JsonPropertyName("os")]
        public string Os { get; set; } = Environment.OSVersion.ToString();

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("last_seen")]
        public long LastSeen { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "ONLINE";
    }

    public class OtaUpdateManager
    {
        public static string FirebaseDatabaseUrl
        {
            get
            {
                try
                {
                    string configPath = Path.Combine(PathHelper.GetRootDirectory(), "config", "settings.json");
                    if (File.Exists(configPath))
                    {
                        string json = File.ReadAllText(configPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("firebase_database_url", out var prop) && !string.IsNullOrWhiteSpace(prop.GetString()))
                        {
                            return prop.GetString()!.TrimEnd('/');
                        }
                    }
                }
                catch { }
                return "https://matematica-em-foco-default-rtdb.firebaseio.com";
            }
        }

        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    // version.txt e arquivo de PROGRAMA (instalado junto dos binarios)
                    string versionFile = Path.Combine(PathHelper.GetInstallDirectory(), "config", "version.txt");
                    if (File.Exists(versionFile))
                    {
                        string ver = File.ReadAllText(versionFile).Trim();
                        if (!string.IsNullOrWhiteSpace(ver)) return ver;
                    }

                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    if (version != null && version.Major > 0)
                    {
                        return $"{version.Major}.{version.Minor}.{version.Build}";
                    }
                }
                catch { }
                // Fallback seguro: nunca usar a data atual (ela suprimiria updates
                // reais ao parecer "mais nova" que o manifesto). "0.0.0" força
                // a detecção de qualquer versão publicada.
                return "0.0.0";
            }
        }

        public static string MachineId
        {
            get
            {
                string rawId = $"{Environment.MachineName}_{Environment.UserName}";
                using var sha = SHA256.Create();
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawId));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// Envia o sinal de telemetria da máquina para o Firebase Realtime Database.
        /// Best-effort e sem login obrigatório: tenta PUT anônimo (caminho público)
        /// e, só se houver credenciais configuradas, tenta fallback autenticado.
        /// Falha silenciosa nunca bloqueia a checagem de atualização.
        /// </summary>
        public async Task<bool> SendTelemetryPingAsync(string? currentVersion = null, string status = "ONLINE")
        {
            try
            {
                string version = currentVersion ?? CurrentVersion;
                var telemetry = new OtaDeviceTelemetry
                {
                    Hostname = Environment.MachineName,
                    Os = GetFriendlyOsName(),
                    Version = version,
                    LastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Status = status
                };

                string json = JsonSerializer.Serialize(telemetry);
                string url = $"{FirebaseDatabaseUrl}/ota/devices/{MachineId}.json";

                // 1. Tentativa anônima (sem login). Funciona se a regra do nó permitir escrita pública.
                try
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var response = await HttpClient.PutAsync(url, content);
                    if (response.IsSuccessStatusCode) return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OTA Telemetry Public Error] {ex.Message}");
                }

                // 2. Fallback autenticado apenas se houver credenciais (compat com DB privado).
                if (FirebaseAuthService.HasCredentials)
                {
                    return await FirebaseAuthService.PutJsonAsync(url, json, HttpClient);
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTA Telemetry Error] {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Consulta a versão mais recente publicada na nuvem.
        /// Caminho único público de APENAS LEITURA: GET anônimo, sem login, sem token.
        /// Requer regra Firebase: "ota": { "manifest": { ".read": true } }.
        /// Mantém fallback autenticado só para compat com DB ainda privado.
        /// </summary>
        public async Task<OtaManifest?> CheckForUpdatesAsync()
        {
            try
            {
                string url = $"{FirebaseDatabaseUrl}/ota/manifest.json";

                // 1. Leitura pública anônima (sem login).
                try
                {
                    using var response = await HttpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string publicJson = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(publicJson) && publicJson != "null")
                        {
                            return JsonSerializer.Deserialize<OtaManifest>(publicJson);
                        }
                        return null;
                    }
                    Debug.WriteLine($"[OTA Check Public] HTTP {(int)response.StatusCode} — tentando fallback.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OTA Check Public Error] {ex.Message}");
                }

                // 2. Fallback autenticado apenas se houver credenciais (compat com DB privado).
                if (FirebaseAuthService.HasCredentials)
                {
                    string? json = await FirebaseAuthService.GetStringAsync(url, HttpClient);
                    if (string.IsNullOrWhiteSpace(json) || json == "null") return null;

                    var manifest = JsonSerializer.Deserialize<OtaManifest>(json);
                    return manifest;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTA Check Error] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compara a versão remota com a versão local instalada.
        /// Retorna TRUE estritamente se a versão remota for MAIS NOVA que a versão local.
        /// Se a versão local for igual ou mais recente, retorna FALSE (não atualiza).
        /// </summary>
        public bool IsUpdateAvailable(OtaManifest? manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version)) return false;

            string localStr = CurrentVersion.Trim();
            string remoteStr = manifest.Version.Trim();

            // Se as strings forem idênticas, não há atualização
            if (string.Equals(localStr, remoteStr, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsRemoteVersionStrictlyNewer(localStr, remoteStr);
        }

        /// <summary>
        /// Valida se a versão remota é cronologicamente ou numericamente estritamente superior à local.
        /// </summary>
        public static bool IsRemoteVersionStrictlyNewer(string localVersionStr, string remoteVersionStr)
        {
            if (string.IsNullOrWhiteSpace(remoteVersionStr) || string.IsNullOrWhiteSpace(localVersionStr))
            {
                return false;
            }

            // 1. Tenta analisar como Timestamp / Data (DD.MM.YYYY.HHmm ou YYYY.MM.DD...)
            DateTime? localDate = TryParseDateVersion(localVersionStr);
            DateTime? remoteDate = TryParseDateVersion(remoteVersionStr);

            if (localDate.HasValue && remoteDate.HasValue)
            {
                // Só atualiza se a data remota for estritamente maior que a data local
                return remoteDate.Value > localDate.Value;
            }

            // 2. Se a versão local é timestamp (versão de build nova) e a remota é semântica antiga (ex: "1.0.0"), local é mais recente
            if (localDate.HasValue && !remoteDate.HasValue)
            {
                return false;
            }

            // 3. Tenta comparar como Version Semântica tradicional (Major.Minor.Build.Revision)
            string cleanLocal = CleanVersionString(localVersionStr);
            string cleanRemote = CleanVersionString(remoteVersionStr);

            if (Version.TryParse(cleanLocal, out var lVer) && Version.TryParse(cleanRemote, out var rVer))
            {
                return rVer > lVer;
            }

            // 4. Comparação Numérica Segmentada (Token a Token)
            string[] localParts = cleanLocal.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            string[] remoteParts = cleanRemote.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            int maxLen = Math.Max(localParts.Length, remoteParts.Length);
            for (int i = 0; i < maxLen; i++)
            {
                string lp = i < localParts.Length ? localParts[i] : "0";
                string rp = i < remoteParts.Length ? remoteParts[i] : "0";

                bool lIsNum = long.TryParse(lp, out long lNum);
                bool rIsNum = long.TryParse(rp, out long rNum);

                if (lIsNum && rIsNum)
                {
                    if (rNum > lNum) return true;
                    if (rNum < lNum) return false;
                }
                else
                {
                    int cmp = string.Compare(rp, lp, StringComparison.OrdinalIgnoreCase);
                    if (cmp > 0) return true;
                    if (cmp < 0) return false;
                }
            }

            // Se for equivalente ou menor, NUNCA atualiza
            return false;
        }

        private static string CleanVersionString(string ver)
        {
            if (string.IsNullOrWhiteSpace(ver)) return "0.0";
            ver = ver.Trim().TrimStart('v', 'V').Trim();
            return ver;
        }

        public static DateTime? TryParseDateVersion(string versionStr)
        {
            if (string.IsNullOrWhiteSpace(versionStr)) return null;
            string clean = CleanVersionString(versionStr);

            string[] parts = clean.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            // 4 partes: DD.MM.YYYY.HHmm (Ex: 02.09.2026.1630)
            if (parts.Length == 4)
            {
                if (int.TryParse(parts[0], out int p0) &&
                    int.TryParse(parts[1], out int p1) &&
                    int.TryParse(parts[2], out int p2) &&
                    int.TryParse(parts[3], out int p3))
                {
                    // Formato DD.MM.YYYY.HHmm (ano na posição 2)
                    if (p2 >= 2000 && p2 <= 2100 && p1 >= 1 && p1 <= 12 && p0 >= 1 && p0 <= 31)
                    {
                        int hour = p3 / 100;
                        int min = p3 % 100;
                        if (hour >= 0 && hour <= 23 && min >= 0 && min <= 59)
                        {
                            return new DateTime(p2, p1, p0, hour, min, 0);
                        }
                        return new DateTime(p2, p1, p0);
                    }
                    // Formato YYYY.MM.DD.HHmm (ano na posição 0)
                    if (p0 >= 2000 && p0 <= 2100 && p1 >= 1 && p1 <= 12 && p2 >= 1 && p2 <= 31)
                    {
                        int hour = p3 / 100;
                        int min = p3 % 100;
                        if (hour >= 0 && hour <= 23 && min >= 0 && min <= 59)
                        {
                            return new DateTime(p0, p1, p2, hour, min, 0);
                        }
                        return new DateTime(p0, p1, p2);
                    }
                }
            }

            // 3 partes: DD.MM.YYYY ou YYYY.MM.DD
            if (parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int p0) &&
                    int.TryParse(parts[1], out int p1) &&
                    int.TryParse(parts[2], out int p2))
                {
                    if (p2 >= 2000 && p2 <= 2100 && p1 >= 1 && p1 <= 12 && p0 >= 1 && p0 <= 31)
                    {
                        return new DateTime(p2, p1, p0);
                    }
                    if (p0 >= 2000 && p0 <= 2100 && p1 >= 1 && p1 <= 12 && p2 >= 1 && p2 <= 31)
                    {
                        return new DateTime(p0, p1, p2);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Realiza o download do arquivo .ZIP de atualização com progresso e validação SHA-256
        /// </summary>
        public async Task<(bool Success, string? FilePath, string? ErrorMessage)> DownloadAndVerifyUpdateAsync(
            OtaManifest? manifest, 
            IProgress<double>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                return (false, null, "URL de download inválida ou manifesto nulo.");
            }

            try
            {
                string tempDir = Path.Combine(PathHelper.GetRootDirectory(), "data", "temp_ota");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "BotRPA_Update.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);

                using var response = await HttpClient.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue && progress != null)
                    {
                        double percent = (double)totalRead / totalBytes.Value * 100.0;
                        progress.Report(percent);
                    }
                }

                fileStream.Close();

                // Valida o Hash SHA-256 se um hash tiver sido especificado no manifesto
                if (!string.IsNullOrWhiteSpace(manifest.Sha256))
                {
                    string computedHash = CalculateSha256(zipPath);
                    if (!string.Equals(computedHash, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(zipPath);
                        return (false, null, $"Falha de integridade SHA-256. Esperado: {manifest.Sha256}, Obtido: {computedHash}");
                    }
                }

                return (true, zipPath, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Erro durante o download: {ex.Message}");
            }
        }

        /// <summary>
        /// Aplica a atualização executando o script auxiliar de extração e reinicialização.
        /// O ZIP OTA contém a estrutura com pastas (config/version.txt na raiz do
        /// pacote), então a extração é feita sobre a raiz do app (tools+config).
        /// </summary>
        public bool ApplyUpdate(string zipFilePath)
        {
            try
            {
                // A atualizacao substitui os PROGRAMAS: usa a pasta de instalacao.
                string root = PathHelper.GetInstallDirectory();
                string scriptPath = Path.Combine(root, "scripts", "apply_ota_update.bat");
                if (!File.Exists(scriptPath))
                {
                    CreateApplyUpdateScript(scriptPath);
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    Arguments = "\"" + zipFilePath + "\" \"" + root + "\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(psi);
                Application.Current.Shutdown();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTA Apply Error] {ex.Message}");
                return false;
            }
        }

        public static void CreateApplyUpdateScript(string scriptPath)
        {
            string? dir = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string content = "@echo off\r\n"
                + "title Bot_RPA OTA Updater\r\n"
                + "echo ========================================================\r\n"
                + "echo   Plataforma Bot_RPA - Aplicando Atualizacao OTA...\r\n"
                + "echo ========================================================\r\n"
                + "echo.\r\n"
                + "echo Aguardando o encerramento do BotRPAui.exe...\r\n"
                + "timeout /t 3 /nobreak >nul\r\n"
                + "taskkill /F /IM BotRPAui.exe /T 2>nul\r\n"
                + "timeout /t 2 /nobreak >nul\r\n"
                + "\r\n"
                + "set \"ZIP_PATH=%~1\"\r\n"
                + "set \"TARGET_DIR=%~2\"\r\n"
                + "if \"%TARGET_DIR%\"==\"\" set \"TARGET_DIR=%~dp0..\"\r\n"
                + "set \"LOG_FILE=%TARGET_DIR%\\logs\\ota_update_%date:~6,4%-%date:~3,2%-%date:~0,2%.log\"\r\n"
                + "if not exist \"%TARGET_DIR%\\logs\" mkdir \"%TARGET_DIR%\\logs\" 2>nul\r\n"
                + "\r\n"
                + "if not exist \"%ZIP_PATH%\" (\r\n"
                + "    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%>> \"%LOG_FILE%\" 2>&1\r\n"
                + "    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%\r\n"
                + "    pause\r\n"
                + "    exit /b 1\r\n"
                + ")\r\n"
                + "\r\n"
                + "echo Extraindo pacote de atualizacao...>> \"%LOG_FILE%\" 2>&1\r\n"
                + "echo Extraindo pacote de atualizacao...\r\n"
                + "powershell -NoProfile -Command \"Expand-Archive -LiteralPath $env:ZIP_PATH -DestinationPath $env:TARGET_DIR -Force\">> \"%LOG_FILE%\" 2>&1\r\n"
                + "if errorlevel 1 (\r\n"
                + "    echo [ERRO] Falha ao extrair o pacote. Veja o log: %LOG_FILE%\r\n"
                + "    pause\r\n"
                + "    exit /b 1\r\n"
                + ")\r\n"
                + "\r\n"
                + "echo Atualizacao concluida com sucesso!>> \"%LOG_FILE%\" 2>&1\r\n"
                + "echo Atualizacao concluida com sucesso!\r\n"
                + "echo Reiniciando o Bot_RPA...\r\n"
                + "timeout /t 2 /nobreak >nul\r\n"
                + "\r\n"
                + "if exist \"%TARGET_DIR%\\BotRPAui.exe\" (\r\n"
                + "    start \"\" \"%TARGET_DIR%\\BotRPAui.exe\"\r\n"
                + ") else if exist \"%TARGET_DIR%\\data\\output\\BotRPAui.exe\" (\r\n"
                + "    start \"\" \"%TARGET_DIR%\\data\\output\\BotRPAui.exe\"\r\n"
                + ")\r\n"
                + "exit /b 0\r\n";
            File.WriteAllText(scriptPath, content, Encoding.UTF8);
        }

        private static string CalculateSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string GetFriendlyOsName()
        {
            return Environment.OSVersion.VersionString;
        }
    }
}
