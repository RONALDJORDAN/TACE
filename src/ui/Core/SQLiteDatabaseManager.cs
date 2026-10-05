using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace BotRpaUI.Core
{
    public class DispositivoDB
    {
        public int ID { get; set; }
        public string Serial { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Versao_Android { get; set; } = string.Empty;
        public string API_Level { get; set; } = string.Empty;
        public string Bateria { get; set; } = string.Empty;
        public string IMEI { get; set; } = string.Empty;
        public string MAC { get; set; } = string.Empty;
        public string DataRegistro { get; set; } = string.Empty;
        public string MarcaModelo { get; set; } = string.Empty;
    }

    public static class SQLiteDatabaseManager
    {
        public static event Action<int, int>? OnCloudPullCompleted;

        private static HashSet<string>? _serialCache = null;
        private static readonly object _cacheLock = new object();
        private static string _dbPath = "";
        private static string _connectionString = "";

        public static string GetDatabasePath()
        {
            if (!string.IsNullOrEmpty(_dbPath)) return _dbPath;

            string basePath = PathHelper.GetRootDirectory();
            string recordsDir = Path.Combine(basePath, "data");

            if (!Directory.Exists(recordsDir))
            {
                Directory.CreateDirectory(recordsDir);
            }

            _dbPath = Path.Combine(recordsDir, "bot_database.db");
            _connectionString = $"Data Source={_dbPath}";

            // Migracao automatica do legado Inno_RPA (uma unica vez por maquina).
            try
            {
                string legacy = Path.Combine(recordsDir, "inno_database.db");
                if (!File.Exists(_dbPath) && File.Exists(legacy))
                    File.Move(legacy, _dbPath);
            }
            catch { }
            return _dbPath;
        }

        public static void InitializeDatabase()
        {
            GetDatabasePath();
            bool isNew = !File.Exists(_dbPath);

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                
                string createDispositivos = @"
                    CREATE TABLE IF NOT EXISTS Dispositivos (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Serial TEXT UNIQUE,
                        Fabricante TEXT,
                        Modelo TEXT,
                        Versao_Android TEXT,
                        API_Level TEXT,
                        Bateria TEXT,
                        IMEI TEXT,
                        MAC TEXT,
                        DataRegistro TEXT
                    );";

                using (var cmd = new SqliteCommand(createDispositivos, connection))
                {
                    cmd.ExecuteNonQuery();
                }

                string createExecucoes = @"
                    CREATE TABLE IF NOT EXISTS Execucoes (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Data TEXT,
                        Tipo TEXT,
                        Detalhes TEXT
                    );";

                using (var cmd = new SqliteCommand(createExecucoes, connection))
                {
                    cmd.ExecuteNonQuery();
                }

                if (isNew)
                {
                    ImportFromCsvIfExists(connection);
                }
            }
        }

        private static void ImportFromCsvIfExists(SqliteConnection connection)
        {
            string oldCsvPath = Path.Combine(PathHelper.GetRootDirectory(), "registros_de_dispositivos", "banco_dispositivos.csv");
            if (!File.Exists(oldCsvPath)) return;

            try
            {
                var lines = File.ReadAllLines(oldCsvPath, System.Text.Encoding.UTF8);

                using (var transaction = connection.BeginTransaction())
                {
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO Dispositivos (Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC, DataRegistro)
                        VALUES ($serial, $fabricante, $modelo, $versao, $api, $bateria, $imei, $mac, $data)";

                    var pSerial = cmd.CreateParameter(); pSerial.ParameterName = "$serial"; cmd.Parameters.Add(pSerial);
                    var pFab = cmd.CreateParameter(); pFab.ParameterName = "$fabricante"; cmd.Parameters.Add(pFab);
                    var pMod = cmd.CreateParameter(); pMod.ParameterName = "$modelo"; cmd.Parameters.Add(pMod);
                    var pVer = cmd.CreateParameter(); pVer.ParameterName = "$versao"; cmd.Parameters.Add(pVer);
                    var pApi = cmd.CreateParameter(); pApi.ParameterName = "$api"; cmd.Parameters.Add(pApi);
                    var pBat = cmd.CreateParameter(); pBat.ParameterName = "$bateria"; cmd.Parameters.Add(pBat);
                    var pImei = cmd.CreateParameter(); pImei.ParameterName = "$imei"; cmd.Parameters.Add(pImei);
                    var pMac = cmd.CreateParameter(); pMac.ParameterName = "$mac"; cmd.Parameters.Add(pMac);
                    var pData = cmd.CreateParameter(); pData.ParameterName = "$data"; cmd.Parameters.Add(pData);

                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        var parts = lines[i].Split(';');
                        if (parts.Length >= 9)
                        {
                            pSerial.Value = parts[0];
                            pFab.Value = parts[1];
                            pMod.Value = parts[2];
                            pVer.Value = parts[3];
                            pApi.Value = parts[4];
                            pBat.Value = parts[5];
                            pImei.Value = parts[6];
                            pMac.Value = parts[7];
                            pData.Value = parts[8];
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
            catch { }
        }

        private static HashSet<string> GetSerialCache()
        {
            lock (_cacheLock)
            {
                if (_serialCache != null) return _serialCache;
                
                _serialCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                InitializeDatabase();

                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using (var cmd = new SqliteCommand("SELECT Serial FROM Dispositivos", connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                                _serialCache.Add(reader.GetString(0));
                        }
                    }
                }
                return _serialCache;
            }
        }

        public static void RegistrarSeNovo(string serial, string fabricante, string modelo, string mac)
        {
            if (string.IsNullOrEmpty(serial)) return;
            if (serial == "unknown" || serial.StartsWith("?") || serial == "Pendente") return;

            // Mapeamento automático de hardware: Seriais T30764 / T030764 ou Modelos T0307G / T307G são Positivo Tab Vision 7
            bool isVision7Hardware = 
                (!string.IsNullOrEmpty(serial) && (serial.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || serial.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0 || serial.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || serial.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                (!string.IsNullOrEmpty(modelo) && (modelo.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || modelo.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || modelo.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || modelo.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                (!string.IsNullOrEmpty(fabricante) && (fabricante.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || fabricante.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || fabricante.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0));

            bool isNokiaT20Hardware = 
                (!string.IsNullOrEmpty(modelo) && (modelo.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 || modelo.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                (!string.IsNullOrEmpty(fabricante) && fabricante.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isVision7Hardware)
            {
                fabricante = "Positivo";
                if (string.IsNullOrEmpty(modelo) || modelo == "Desconhecido" || !modelo.Contains("Vision"))
                {
                    string extraTag = !string.IsNullOrEmpty(modelo) && modelo != "Desconhecido" ? " (" + modelo + ")" : " (T307G)";
                    modelo = "Tab Vision 7" + extraTag;
                }
            }
            else if (isNokiaT20Hardware)
            {
                fabricante = "Nokia";
                if (string.IsNullOrEmpty(modelo) || modelo == "Desconhecido" || !modelo.Contains("T20"))
                {
                    modelo = "Nokia T20 (Android 11)";
                }
            }

            lock (_cacheLock)
            {
                var cache = GetSerialCache();
                if (cache.Contains(serial))
                {
                    // Se o aparelho já está cadastrado no cache, mas agora temos um MAC válido (17 caracteres)
                    // e o banco pode estar com MAC 'N/A', '---' ou vazio:
                    if (!string.IsNullOrEmpty(mac) && mac.Length == 17 && mac != "02:00:00:00:00:00")
                    {
                        try
                        {
                            using (var connection = new SqliteConnection(_connectionString))
                            {
                                connection.Open();
                                var cmd = connection.CreateCommand();
                                cmd.CommandText = @"
                                    UPDATE Dispositivos 
                                    SET MAC = $mac 
                                    WHERE Serial = $serial 
                                      AND (MAC IS NULL OR MAC = '' OR MAC = 'N/A' OR MAC = '---' OR MAC = '02:00:00:00:00:00');";
                                cmd.Parameters.AddWithValue("$mac", mac);
                                cmd.Parameters.AddWithValue("$serial", serial);
                                cmd.ExecuteNonQuery();
                            }
                            SincronizarCsv();
                            _ = Task.Run(async () => {
                                var d = BuscarPorSerial(serial);
                                if (d != null) await SincronizarComInnovStockAsync(d);
                            });
                        }
                        catch { }
                    }
                    return;
                }

                // Cuidado contra duplicidade: se já existir outro registro com este mesmo MAC, atualiza o serial sem duplicar linha
                if (!string.IsNullOrEmpty(mac) && mac.Length == 17 && mac != "02:00:00:00:00:00")
                {
                    try
                    {
                        using (var connection = new SqliteConnection(_connectionString))
                        {
                            connection.Open();
                            var checkCmd = connection.CreateCommand();
                            checkCmd.CommandText = "SELECT Serial FROM Dispositivos WHERE MAC = $mac LIMIT 1;";
                            checkCmd.Parameters.AddWithValue("$mac", mac);
                            var existingSerial = checkCmd.ExecuteScalar() as string;
                            if (!string.IsNullOrEmpty(existingSerial))
                            {
                                var updateCmd = connection.CreateCommand();
                                updateCmd.CommandText = "UPDATE Dispositivos SET Serial = $serial, Fabricante = $fabricante, Modelo = $modelo WHERE MAC = $mac;";
                                updateCmd.Parameters.AddWithValue("$serial", serial);
                                updateCmd.Parameters.AddWithValue("$fabricante", string.IsNullOrEmpty(fabricante) ? "Desconhecido" : fabricante);
                                updateCmd.Parameters.AddWithValue("$modelo", modelo);
                                updateCmd.Parameters.AddWithValue("$mac", mac);
                                updateCmd.ExecuteNonQuery();
                                cache.Add(serial);
                                SincronizarCsv();
                                _ = Task.Run(async () => {
                                    var d = BuscarPorSerial(serial);
                                    if (d != null) await SincronizarComInnovStockAsync(d);
                                });
                                return;
                            }
                        }
                    }
                    catch { }
                }

                try
                {
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO Dispositivos (Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC, DataRegistro)
                            VALUES ($serial, $fabricante, $modelo, $versao, $api, $bateria, $imei, $mac, $data)";
                        cmd.Parameters.AddWithValue("$serial", serial);
                        cmd.Parameters.AddWithValue("$fabricante", string.IsNullOrEmpty(fabricante) ? "Desconhecido" : fabricante);
                        cmd.Parameters.AddWithValue("$modelo", modelo);
                        cmd.Parameters.AddWithValue("$versao", "N/A");
                        cmd.Parameters.AddWithValue("$api", "N/A");
                        cmd.Parameters.AddWithValue("$bateria", "N/A");
                        cmd.Parameters.AddWithValue("$imei", "N/A");
                        cmd.Parameters.AddWithValue("$mac", mac);
                        cmd.Parameters.AddWithValue("$data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.ExecuteNonQuery();
                    }
                    cache.Add(serial);
                    if (!string.IsNullOrEmpty(mac) && mac.Length == 17) SincronizarCsv();
                    _ = Task.Run(async () => {
                        var d = BuscarPorSerial(serial);
                        if (d != null) await SincronizarComInnovStockAsync(d);
                    });
                }
                catch { }
            }
        }

        public static DispositivoDB? BuscarPorSerial(string serial)
        {
            if (string.IsNullOrEmpty(serial)) return null;
            InitializeDatabase();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT ID, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC, DataRegistro FROM Dispositivos WHERE Serial = $serial LIMIT 1";
                    cmd.Parameters.AddWithValue("$serial", serial);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string ser = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            string fab = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            string mod = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

                            bool isVision7 = 
                                (ser.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || ser.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0 || ser.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || ser.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (mod.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || mod.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || mod.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || mod.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (fab.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || fab.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || fab.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0);

                            if (isVision7)
                            {
                                if (string.IsNullOrWhiteSpace(fab) || fab == "Desconhecido") fab = "Positivo";
                                if (!mod.Contains("Vision"))
                                {
                                    string tagStr = !string.IsNullOrWhiteSpace(mod) && mod != "Desconhecido" ? mod : "T307G";
                                    mod = "Tab Vision 7 (" + tagStr + ")";
                                }
                            }

                            return new DispositivoDB
                            {
                                ID = reader.GetInt32(0),
                                Serial = ser,
                                Fabricante = fab,
                                Modelo = mod,
                                MarcaModelo = (fab + " " + mod).Trim(),
                                Versao_Android = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                                API_Level = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                Bateria = reader.IsDBNull(6) ? "N/A" : reader.GetString(6),
                                IMEI = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                                MAC = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                                DataRegistro = reader.IsDBNull(9) ? "N/A" : reader.GetString(9)
                            };
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public static List<DispositivoDB> LerTodos()
        {
            var lista = new List<DispositivoDB>();
            InitializeDatabase();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT ID, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC, DataRegistro FROM Dispositivos";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string ser = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            string fab = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            string mod = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

                            // Normalização Positivo Tab Vision 7 para seriais T30764 / T030764 e modelos T0307G / T307G
                            bool isVision7 = 
                                (ser.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || ser.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0 || ser.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || ser.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (mod.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || mod.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || mod.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || mod.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (fab.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || fab.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || fab.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0);

                            if (isVision7)
                            {
                                if (string.IsNullOrWhiteSpace(fab) || fab == "Desconhecido") fab = "Positivo";
                                if (!mod.Contains("Vision"))
                                {
                                    string tagStr = !string.IsNullOrWhiteSpace(mod) && mod != "Desconhecido" ? mod : "T307G";
                                    mod = "Tab Vision 7 (" + tagStr + ")";
                                }
                            }

                            string marcaModeloFormada = (fab + " " + mod).Trim();

                            lista.Add(new DispositivoDB
                            {
                                ID = reader.GetInt32(0),
                                Serial = ser,
                                Fabricante = fab,
                                Modelo = mod,
                                MarcaModelo = marcaModeloFormada,
                                Versao_Android = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                API_Level = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                Bateria = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                IMEI = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                MAC = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                                DataRegistro = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                            });
                        }
                    }
                }
            }
            catch { }

            return lista;
        }

        public static void SalvarTodos(List<DispositivoDB> lista)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
                            UPDATE Dispositivos SET 
                            Fabricante = $fabricante, 
                            Modelo = $modelo, 
                            MAC = $mac, 
                            Serial = $serial
                            WHERE ID = $id";

                        var pFab = cmd.CreateParameter(); pFab.ParameterName = "$fabricante"; cmd.Parameters.Add(pFab);
                        var pMod = cmd.CreateParameter(); pMod.ParameterName = "$modelo"; cmd.Parameters.Add(pMod);
                        var pMac = cmd.CreateParameter(); pMac.ParameterName = "$mac"; cmd.Parameters.Add(pMac);
                        var pSerial = cmd.CreateParameter(); pSerial.ParameterName = "$serial"; cmd.Parameters.Add(pSerial);
                        var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);

                        foreach (var d in lista)
                        {
                            pFab.Value = d.Fabricante ?? string.Empty;
                            pMod.Value = d.Modelo ?? string.Empty;
                            pMac.Value = d.MAC ?? string.Empty;
                            pSerial.Value = d.Serial ?? string.Empty;
                            pId.Value = d.ID;
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
                SincronizarCsv();
                _ = Task.Run(() => SincronizarTodosComInnovStockAsync());
            }
            catch { }
        }

        public static void Excluir(int id)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM Dispositivos WHERE ID = $id";
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        public static void InvalidarCache()
        {
            lock (_cacheLock)
            {
                _serialCache = null;
            }
        }

        public static void SincronizarCsv()
        {
            try
            {
                string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                string dataDir = System.IO.Path.Combine(baseDir, "data");
                if (!System.IO.Directory.Exists(dataDir)) System.IO.Directory.CreateDirectory(dataDir);

                string csvPath = System.IO.Path.Combine(dataDir, "dispositivos_registrados.csv");
                var todos = LerTodos();
                var sb = new StringBuilder();
                sb.AppendLine("ID;Serial;Fabricante;Modelo;Versao_Android;API_Level;Bateria;MAC;DataRegistro");
                foreach (var d in todos)
                {
                    sb.AppendLine($"{d.ID};{d.Serial};{d.Fabricante};{d.Modelo};{d.Versao_Android};{d.API_Level};{d.Bateria};{d.MAC};{d.DataRegistro}");
                }
                System.IO.File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);

                string relatorioPath = System.IO.Path.Combine(dataDir, "relatorio_serial_mac.csv");
                var sbRel = new StringBuilder();
                sbRel.AppendLine("Serial;MAC;Fabricante;Modelo;DataRegistro");
                foreach (var d in todos)
                {
                    sbRel.AppendLine($"{d.Serial};{d.MAC};{d.Fabricante};{d.Modelo};{d.DataRegistro}");
                }
                System.IO.File.WriteAllText(relatorioPath, sbRel.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static readonly System.Net.Http.HttpClient _innovStockHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static string SanitizeFirebaseKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "unknown";
            return key.Replace(".", "_").Replace("$", "_").Replace("[", "_").Replace("]", "_").Replace("#", "_").Replace("/", "_").Trim();
        }

        public static async Task<bool> SincronizarComInnovStockAsync(DispositivoDB disp)
        {
            if (disp == null || string.IsNullOrWhiteSpace(disp.Serial)) return false;
            try
            {
                string baseUrl = SettingsManager.FirebaseDatabaseUrl.TrimEnd('/');
                string safeKey = SanitizeFirebaseKey(disp.Serial);
                string url = $"{baseUrl}/rpa_devices/{safeKey}.json";

                var payload = new
                {
                    id = disp.ID,
                    serial = disp.Serial,
                    mac = string.IsNullOrWhiteSpace(disp.MAC) ? "N/A" : disp.MAC,
                    fabricante = string.IsNullOrWhiteSpace(disp.Fabricante) ? "Desconhecido" : disp.Fabricante,
                    modelo = string.IsNullOrWhiteSpace(disp.Modelo) ? "Desconhecido" : disp.Modelo,
                    marcaModelo = disp.MarcaModelo,
                    versao_android = string.IsNullOrWhiteSpace(disp.Versao_Android) ? "N/A" : disp.Versao_Android,
                    api_level = string.IsNullOrWhiteSpace(disp.API_Level) ? "N/A" : disp.API_Level,
                    bateria = string.IsNullOrWhiteSpace(disp.Bateria) ? "N/A" : disp.Bateria,
                    imei = string.IsNullOrWhiteSpace(disp.IMEI) ? "N/A" : disp.IMEI,
                    dataRegistro = string.IsNullOrWhiteSpace(disp.DataRegistro) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : disp.DataRegistro,
                    syncedAt = DateTime.UtcNow.ToString("o"),
                    origem = "Bot_RPA",
                    machineId = Environment.MachineName
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                return await FirebaseAuthService.PutJsonAsync(url, json, _innovStockHttp);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<int> SincronizarTodosComInnovStockAsync()
        {
            try
            {
                var todos = LerTodos();
                if (todos == null || todos.Count == 0) return 0;

                string baseUrl = SettingsManager.FirebaseDatabaseUrl.TrimEnd('/');
                string url = $"{baseUrl}/rpa_devices.json";

                var dict = new Dictionary<string, object>();
                foreach (var disp in todos)
                {
                    if (string.IsNullOrWhiteSpace(disp.Serial)) continue;
                    string safeKey = SanitizeFirebaseKey(disp.Serial);
                    dict[safeKey] = new
                    {
                        id = disp.ID,
                        serial = disp.Serial,
                        mac = string.IsNullOrWhiteSpace(disp.MAC) ? "N/A" : disp.MAC,
                        fabricante = string.IsNullOrWhiteSpace(disp.Fabricante) ? "Desconhecido" : disp.Fabricante,
                        modelo = string.IsNullOrWhiteSpace(disp.Modelo) ? "Desconhecido" : disp.Modelo,
                        marcaModelo = disp.MarcaModelo,
                        versao_android = string.IsNullOrWhiteSpace(disp.Versao_Android) ? "N/A" : disp.Versao_Android,
                        api_level = string.IsNullOrWhiteSpace(disp.API_Level) ? "N/A" : disp.API_Level,
                        bateria = string.IsNullOrWhiteSpace(disp.Bateria) ? "N/A" : disp.Bateria,
                        imei = string.IsNullOrWhiteSpace(disp.IMEI) ? "N/A" : disp.IMEI,
                        dataRegistro = string.IsNullOrWhiteSpace(disp.DataRegistro) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : disp.DataRegistro,
                        syncedAt = DateTime.UtcNow.ToString("o"),
                        origem = "Bot_RPA",
                        machineId = Environment.MachineName
                    };
                }

                string json = System.Text.Json.JsonSerializer.Serialize(dict);
                return await FirebaseAuthService.PatchJsonAsync(url, json, _innovStockHttp) ? dict.Count : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string CloudTexto(JsonElement el, string nome)
        {
            try
            {
                if (el.TryGetProperty(nome, out var p))
                {
                    if (p.ValueKind == JsonValueKind.String) return p.GetString() ?? string.Empty;
                    if (p.ValueKind == JsonValueKind.Number) return p.GetRawText();
                }
            }
            catch { }
            return string.Empty;
        }

        private static bool CloudVazio(string? v)
        {
            return string.IsNullOrWhiteSpace(v) || v == "N/A" || v == "---" || v == "Desconhecido";
        }

        private static bool IsMacValido(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return false;
            if (mac == "N/A" || mac == "---" || mac == "02:00:00:00:00:00") return false;
            return mac.Length == 17;
        }

        /// <summary>
        /// Multi-operador: sobe para a nuvem os seriais locais que ainda nao existem la.
        /// Evita duplicadas porque a chave no Firebase e o proprio serial sanitizado.
        /// Retorna quantos foram enviados com sucesso.
        /// </summary>
        public static async Task<int> EnviarLocaisFaltantesParaNuvemAsync()
        {
            try
            {
                string baseUrl = SettingsManager.FirebaseDatabaseUrl.TrimEnd('/');
                string url = $"{baseUrl}/rpa_devices.json?shallow=true";
                string? shallowJson = await FirebaseAuthService.GetStringAsync(url, _innovStockHttp);
                HashSet<string> chavesNuvem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(shallowJson) && shallowJson.Trim() != "null")
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(shallowJson);
                        if (dict != null)
                        {
                            foreach (var k in dict.Keys) chavesNuvem.Add(k);
                        }
                    }
                    catch
                    {
                        try
                        {
                            var dict2 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(shallowJson);
                            if (dict2 != null)
                            {
                                foreach (var k in dict2.Keys) chavesNuvem.Add(k);
                            }
                        }
                        catch { }
                    }
                }

                var locais = LerTodos();
                if (locais == null || locais.Count == 0) return 0;
                int enviados = 0;
                foreach (var disp in locais)
                {
                    if (string.IsNullOrWhiteSpace(disp.Serial)) continue;
                    string safeKey = SanitizeFirebaseKey(disp.Serial);
                    if (chavesNuvem.Contains(safeKey)) continue;
                    try
                    {
                        if (await SincronizarComInnovStockAsync(disp)) enviados++;
                    }
                    catch { }
                }
                return enviados;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Baixa os dispositivos da nuvem InnovStock (GET /rpa_devices.json) para o banco local.
        /// Politica segura Fase 1: SOMENTE adiciona ou preenche campos vazios — nunca apaga
        /// registros locais e nunca sobrescreve um valor local valido. Nao re-envia nada
        /// (o upload normal continua pelas rotinas de registro), logo nao ha loop.
        /// Retorna (importados, atualizados).
        /// </summary>
        public static async Task<(int importados, int atualizados, List<string> afetados)> BaixarDoInnovStockAsync()
        {
            int importados = 0, atualizados = 0;
            var afetados = new List<string>();
            try
            {
                string baseUrl = SettingsManager.FirebaseDatabaseUrl.TrimEnd('/');
                string url = $"{baseUrl}/rpa_devices.json";

                string? json = await FirebaseAuthService.GetStringAsync(url, _innovStockHttp);
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") return (0, 0, new List<string>());

                var nuvem = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (nuvem == null || nuvem.Count == 0) return (0, 0, new List<string>());

                InitializeDatabase();

                lock (_cacheLock)
                {
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            var selCmd = connection.CreateCommand();
                            selCmd.CommandText = "SELECT Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC, DataRegistro FROM Dispositivos WHERE Serial = $serial LIMIT 1;";
                            var pSel = selCmd.CreateParameter(); pSel.ParameterName = "$serial"; selCmd.Parameters.Add(pSel);

                            var insCmd = connection.CreateCommand();
                            insCmd.CommandText = @"
                                INSERT OR IGNORE INTO Dispositivos (Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC, DataRegistro)
                                VALUES ($serial, $fabricante, $modelo, $versao, $api, $bateria, $imei, $mac, $data);";
                            var pIns = new Microsoft.Data.Sqlite.SqliteParameter[9];
                            string[] insNomes = { "$serial", "$fabricante", "$modelo", "$versao", "$api", "$bateria", "$imei", "$mac", "$data" };
                            for (int i = 0; i < insNomes.Length; i++)
                            {
                                pIns[i] = insCmd.CreateParameter();
                                pIns[i].ParameterName = insNomes[i];
                                insCmd.Parameters.Add(pIns[i]);
                            }

                            var updCmd = connection.CreateCommand();
                            updCmd.CommandText = @"
                                UPDATE Dispositivos SET Fabricante = $fabricante, Modelo = $modelo,
                                    Versao_Android = $versao, API_Level = $api, Bateria = $bateria,
                                    IMEI = $imei, MAC = $mac WHERE Serial = $serial;";
                            var pUpd = new Microsoft.Data.Sqlite.SqliteParameter[8];
                            string[] updNomes = { "$fabricante", "$modelo", "$versao", "$api", "$bateria", "$imei", "$mac", "$serial" };
                            for (int i = 0; i < updNomes.Length; i++)
                            {
                                pUpd[i] = updCmd.CreateParameter();
                                pUpd[i].ParameterName = updNomes[i];
                                updCmd.Parameters.Add(pUpd[i]);
                            }

                            var selByMacCmd = connection.CreateCommand();
                            selByMacCmd.CommandText = "SELECT Serial FROM Dispositivos WHERE MAC = $mac LIMIT 1;";
                            var pMacSel = selByMacCmd.CreateParameter(); pMacSel.ParameterName = "$mac"; selByMacCmd.Parameters.Add(pMacSel);

                            var mergeMacCmd = connection.CreateCommand();
                            mergeMacCmd.CommandText = "UPDATE Dispositivos SET Serial = $serial, Fabricante = $fabricante, Modelo = $modelo WHERE MAC = $mac;";
                            var pMergeSerial = mergeMacCmd.CreateParameter(); pMergeSerial.ParameterName = "$serial"; mergeMacCmd.Parameters.Add(pMergeSerial);
                            var pMergeFab = mergeMacCmd.CreateParameter(); pMergeFab.ParameterName = "$fabricante"; mergeMacCmd.Parameters.Add(pMergeFab);
                            var pMergeMod = mergeMacCmd.CreateParameter(); pMergeMod.ParameterName = "$modelo"; mergeMacCmd.Parameters.Add(pMergeMod);
                            var pMergeMac = mergeMacCmd.CreateParameter(); pMergeMac.ParameterName = "$mac"; mergeMacCmd.Parameters.Add(pMergeMac);

                            foreach (var kv in nuvem)
                            {
                                var el = kv.Value;
                                if (el.ValueKind != JsonValueKind.Object) continue;
                                if (el.TryGetProperty("deleted", out var delProp) && delProp.ValueKind == JsonValueKind.True) continue;

                                string serial = CloudTexto(el, "serial");
                                if (string.IsNullOrWhiteSpace(serial) || serial == "unknown" || serial.StartsWith("?") || serial == "Pendente") continue;

                                string cFab = CloudTexto(el, "fabricante");
                                string cMod = CloudTexto(el, "modelo");
                                string cVer = CloudTexto(el, "versao_android");
                                string cApi = CloudTexto(el, "api_level");
                                string cBat = CloudTexto(el, "bateria");
                                string cImei = CloudTexto(el, "imei");
                                string cMac = CloudTexto(el, "mac");
                                string cData = CloudTexto(el, "dataRegistro");

                                pSel.Value = serial;
                                string lFab = "", lMod = "", lVer = "", lApi = "", lBat = "", lImei = "", lMac = "";
                                bool existe = false;
                                using (var reader = selCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        existe = true;
                                        lFab = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                        lMod = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                        lVer = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                        lApi = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                        lBat = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                        lImei = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                        lMac = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                    }
                                }

                                if (!existe)
                                {
                                    bool mergedByMac = false;
                                    if (IsMacValido(cMac))
                                    {
                                        try
                                        {
                                            pMacSel.Value = cMac;
                                            var found = selByMacCmd.ExecuteScalar() as string;
                                            if (!string.IsNullOrEmpty(found) && !found.Equals(serial, StringComparison.OrdinalIgnoreCase))
                                            {
                                                pMergeSerial.Value = serial;
                                                pMergeFab.Value = CloudVazio(cFab) ? "" : cFab;
                                                pMergeMod.Value = CloudVazio(cMod) ? "" : cMod;
                                                pMergeMac.Value = cMac;
                                                mergeMacCmd.ExecuteNonQuery();
                                                atualizados++;
                                                afetados.Add(serial);
                                                mergedByMac = true;
                                            }
                                        }
                                        catch { }
                                    }
                                    if (!mergedByMac)
                                    {
                                        pIns[0].Value = serial;
                                        pIns[1].Value = CloudVazio(cFab) ? "" : cFab;
                                        pIns[2].Value = CloudVazio(cMod) ? "" : cMod;
                                        pIns[3].Value = CloudVazio(cVer) ? "" : cVer;
                                        pIns[4].Value = CloudVazio(cApi) ? "" : cApi;
                                        pIns[5].Value = CloudVazio(cBat) ? "" : cBat;
                                        pIns[6].Value = CloudVazio(cImei) ? "" : cImei;
                                        pIns[7].Value = CloudVazio(cMac) ? "" : cMac;
                                        pIns[8].Value = string.IsNullOrWhiteSpace(cData) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : cData;
                                        if (insCmd.ExecuteNonQuery() == 1) { importados++; afetados.Add(serial); }
                                    }
                                }
                                else
                                {
                                    string nFab = CloudVazio(lFab) && !CloudVazio(cFab) ? cFab : lFab;
                                    string nMod = CloudVazio(lMod) && !CloudVazio(cMod) ? cMod : lMod;
                                    string nVer = CloudVazio(lVer) && !CloudVazio(cVer) ? cVer : lVer;
                                    string nApi = CloudVazio(lApi) && !CloudVazio(cApi) ? cApi : lApi;
                                    string nBat = CloudVazio(lBat) && !CloudVazio(cBat) ? cBat : lBat;
                                    string nImei = CloudVazio(lImei) && !CloudVazio(cImei) ? cImei : lImei;
                                    string nMac = CloudVazio(lMac) && !CloudVazio(cMac) ? cMac : lMac;
                                    if (nFab != lFab || nMod != lMod || nVer != lVer || nApi != lApi || nBat != lBat || nImei != lImei || nMac != lMac)
                                    {
                                        pUpd[0].Value = nFab; pUpd[1].Value = nMod; pUpd[2].Value = nVer;
                                        pUpd[3].Value = nApi; pUpd[4].Value = nBat; pUpd[5].Value = nImei;
                                        pUpd[6].Value = nMac; pUpd[7].Value = serial;
                                        updCmd.ExecuteNonQuery();
                                        atualizados++;
                                        afetados.Add(serial);
                                    }
                                }
                            }
                            transaction.Commit();
                        }
                    }
                    _serialCache = null;
                    SincronizarCsv();
                }
            }
            catch { }
            if (importados > 0 || atualizados > 0)
            {
                try { OnCloudPullCompleted?.Invoke(importados, atualizados); }
                catch { }
            }
            return (importados, atualizados, afetados);
        }
    }
}
