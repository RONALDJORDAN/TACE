using System;
using System.Collections.Generic;
using System.IO;
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

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
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
            string oldCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "registros_de_dispositivos", "banco_dispositivos.csv");
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
                if (cache.Contains(serial)) return;

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
                }
                catch { }
            }
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
    }
}
