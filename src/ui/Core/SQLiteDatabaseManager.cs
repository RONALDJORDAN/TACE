using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace InnovPlayUI.Core
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

            _dbPath = Path.Combine(recordsDir, "inno_database.db");
            _connectionString = $"Data Source={_dbPath}";
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
                string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
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
                    origem = "Inno_RPA",
                    machineId = Environment.MachineName
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
                var response = await _innovStockHttp.PutAsync(url, content);
                return response.IsSuccessStatusCode;
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
                        origem = "Inno_RPA",
                        machineId = Environment.MachineName
                    };
                }

                string json = System.Text.Json.JsonSerializer.Serialize(dict);
                var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
                var request = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod("PATCH"), url) { Content = content };
                var response = await _innovStockHttp.SendAsync(request);
                return response.IsSuccessStatusCode ? dict.Count : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
