using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotRpaUI.Core
{
    public class InnoAppSettings
    {
        [JsonPropertyName("font_ui_size")]
        public int FontUiSize { get; set; } = 14;

        [JsonPropertyName("font_title_size")]
        public int FontTitleSize { get; set; } = 20;

        [JsonPropertyName("font_mono_size")]
        public int FontMonoSize { get; set; } = 13;

        [JsonPropertyName("motor_c_enabled")]
        public bool MotorCEnabled { get; set; } = true;

        [JsonPropertyName("reverse_tethering_enabled")]
        public bool ReverseTetheringEnabled { get; set; } = true;

        [JsonPropertyName("ai_learn_active")]
        public bool AiLearnActive { get; set; } = false;

        [JsonPropertyName("enable_device_vision7")]
        public bool EnableDeviceVision7 { get; set; } = true;

        [JsonPropertyName("enable_device_tabmulti")]
        public bool EnableDeviceTabMulti { get; set; } = true;

        [JsonPropertyName("enable_device_nokia_t20")]
        public bool EnableDeviceNokiaT20 { get; set; } = true;

        [JsonPropertyName("show_disconnected_devices")]
        public bool ShowDisconnectedDevices { get; set; } = false;

        [JsonPropertyName("usb_monitor_interface")]
        public string UsbMonitorInterface { get; set; } = "Classic";

        [JsonPropertyName("meta_lote_total")]
        public int MetaLoteTotal { get; set; } = 100;

        [JsonPropertyName("google_account_email")]
        public string GoogleAccountEmail { get; set; } = "";

        [JsonPropertyName("google_account_password_dpapi")]
        public string GoogleAccountPasswordDpapi { get; set; } = "";
    }

    public enum DeviceHardwareType
    {
        Vision7,
        TabMultiM7,
        NokiaT20,
        Unknown
    }

    public static class DeviceFilterHelper
    {
        public static DeviceHardwareType DetectType(string? serial, string? model, string? manufacturer)
        {
            string s = (serial ?? "").Trim();
            string m = (model ?? "").Trim();
            string f = (manufacturer ?? "").Trim();

            // Se o model for genérico/pendente e tivermos um serial, busca histórico no SQLite
            if ((string.IsNullOrEmpty(m) || m.StartsWith("Pendente", StringComparison.OrdinalIgnoreCase) || m == "Desconhecido" || m == "---") && !string.IsNullOrEmpty(s))
            {
                try
                {
                    var dbDev = SQLiteDatabaseManager.BuscarPorSerial(s);
                    if (dbDev != null)
                    {
                        if (!string.IsNullOrEmpty(dbDev.Modelo) && dbDev.Modelo != "Desconhecido") m = dbDev.Modelo;
                        if (!string.IsNullOrEmpty(dbDev.Fabricante) && dbDev.Fabricante != "---") f = dbDev.Fabricante;
                    }
                }
                catch { }
            }

            // 1. Nokia T20 (Android 11)
            if (m.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA-1392", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA-1394", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA-1396", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA-1397", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA1392", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA1394", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA1396", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TA1397", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.IndexOf("HMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("TA139", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("TA-139", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DeviceHardwareType.NokiaT20;
            }

            // 2. Multilaser TabMulti M7
            if (m.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("TabMulti", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("m7_MULTI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("f7_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("ML01", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("ML02", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("NB367", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("NB368", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("NB380", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.IndexOf("Multilaser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("m7", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("multi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DeviceHardwareType.TabMultiM7;
            }

            // 3. Positivo Tab Vision 7
            if (m.IndexOf("Vision", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("T0307", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("T307", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("v7_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T307", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("T0307", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("POS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DeviceHardwareType.Vision7;
            }

            return DeviceHardwareType.Unknown;
        }

        public static bool IsDeviceModelAllowed(string? serial, string? model, string? manufacturer)
        {
            // Se todas as 3 chaves estiverem desligadas, NENHUM dispositivo é exibido
            if (!SettingsManager.EnableDeviceVision7 && !SettingsManager.EnableDeviceTabMulti && !SettingsManager.EnableDeviceNokiaT20)
            {
                return false;
            }

            var type = DetectType(serial, model, manufacturer);
            switch (type)
            {
                case DeviceHardwareType.Vision7:
                    return SettingsManager.EnableDeviceVision7;
                case DeviceHardwareType.TabMultiM7:
                    return SettingsManager.EnableDeviceTabMulti;
                case DeviceHardwareType.NokiaT20:
                    return SettingsManager.EnableDeviceNokiaT20;
                case DeviceHardwareType.Unknown:
                default:
                    // Se for desconhecido, só permite se pelo menos um modelo estiver ativo
                    return SettingsManager.EnableDeviceVision7 || SettingsManager.EnableDeviceTabMulti || SettingsManager.EnableDeviceNokiaT20;
            }
        }
    }

    public static class SettingsManager
    {
        private static InnoAppSettings _settings = new InnoAppSettings();
        private static readonly object _lock = new object();

        public static event Action? OnSettingsChanged;

        static SettingsManager()
        {
            Load();
        }

        public static bool MotorCEnabled
        {
            get => _settings.MotorCEnabled;
            set { _settings.MotorCEnabled = value; Save(); }
        }

        public static bool ReverseTetheringEnabled
        {
            get => _settings.ReverseTetheringEnabled;
            set { _settings.ReverseTetheringEnabled = value; Save(); }
        }

        public static bool AiLearnActive
        {
            get => _settings.AiLearnActive;
            set { _settings.AiLearnActive = value; Save(); }
        }

        public static bool EnableDeviceVision7
        {
            get => _settings.EnableDeviceVision7;
            set { _settings.EnableDeviceVision7 = value; Save(); }
        }

        public static bool EnableDeviceTabMulti
        {
            get => _settings.EnableDeviceTabMulti;
            set { _settings.EnableDeviceTabMulti = value; Save(); }
        }

        public static bool EnableDeviceNokiaT20
        {
            get => _settings.EnableDeviceNokiaT20;
            set { _settings.EnableDeviceNokiaT20 = value; Save(); }
        }

        public static bool ShowDisconnectedDevices
        {
            get => _settings.ShowDisconnectedDevices;
            set { _settings.ShowDisconnectedDevices = value; Save(); }
        }

        public static string UsbMonitorInterface
        {
            get => _settings.UsbMonitorInterface ?? "Classic";
            set { _settings.UsbMonitorInterface = value; Save(); }
        }

        public static int MetaLoteTotal
        {
            get => _settings.MetaLoteTotal;
            set { _settings.MetaLoteTotal = value < 1 ? 1 : (value > 10000 ? 10000 : value); Save(); }
        }

        public static string GoogleAccountEmail => _settings.GoogleAccountEmail ?? "";

        public static bool HasGoogleAccountConfigured =>
            !string.IsNullOrWhiteSpace(GoogleAccountEmail) &&
            !string.IsNullOrWhiteSpace(_settings.GoogleAccountPasswordDpapi);

        public static bool SetGoogleAccount(string email, string password)
        {
            email = (email ?? "").Trim();
            if (email.Length == 0 || email.Length > 200 || !email.Contains('@') ||
                string.IsNullOrEmpty(password) || password.Length > 200)
            {
                return false;
            }

            string protectedPassword = ProtectSecret(password);
            if (string.IsNullOrEmpty(protectedPassword))
            {
                return false;
            }

            lock (_lock)
            {
                _settings.GoogleAccountEmail = email;
                _settings.GoogleAccountPasswordDpapi = protectedPassword;
            }
            Save();
            return true;
        }

        public static string GetGoogleAccountPassword()
        {
            return UnprotectSecret(_settings.GoogleAccountPasswordDpapi);
        }

        public static InnoAppSettings Current => _settings;

        private const string DpapiPrefix = "DPAPIv1:";
        private static readonly byte[] SecretEntropy = Encoding.UTF8.GetBytes("BotRPA-Google-Account-v1");

        private static string ProtectSecret(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            try
            {
                byte[] protectedBytes = ProtectedData.Protect(
                    plainBytes, SecretEntropy, DataProtectionScope.CurrentUser);
                return DpapiPrefix + Convert.ToBase64String(protectedBytes);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                Array.Clear(plainBytes, 0, plainBytes.Length);
            }
        }

        private static string UnprotectSecret(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue) ||
                !protectedValue.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            byte[] protectedBytes = Array.Empty<byte>();
            byte[] plainBytes = Array.Empty<byte>();
            try
            {
                protectedBytes = Convert.FromBase64String(protectedValue.Substring(DpapiPrefix.Length));
                plainBytes = ProtectedData.Unprotect(
                    protectedBytes, SecretEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (protectedBytes.Length > 0)
                {
                    Array.Clear(protectedBytes, 0, protectedBytes.Length);
                }
                if (plainBytes.Length > 0)
                {
                    Array.Clear(plainBytes, 0, plainBytes.Length);
                }
            }
        }

        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    string configPath = Path.Combine(PathHelper.GetRootDirectory(), "config", "settings.json");
                    if (File.Exists(configPath))
                    {
                        string json = File.ReadAllText(configPath);
                        var loaded = JsonSerializer.Deserialize<InnoAppSettings>(json);
                        if (loaded != null)
                        {
                            _settings = loaded;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsManager.Load] {ex.Message}");
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    string configPath = Path.Combine(PathHelper.GetRootDirectory(), "config", "settings.json");
                    string? dir = Path.GetDirectoryName(configPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_settings, options);
                    File.WriteAllText(configPath, json);

                    OnSettingsChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsManager.Save] {ex.Message}");
                }
            }
        }
    }
}
