using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        public string MarcaModelo { get; set; } = string.Empty;

        /// <summary>
        /// Numero de Patrimonio (etiqueta patrimonial do tablet).
        /// Coluna manual: o operador preenche o codigo de inventario de cada aparelho.
        /// </summary>
        public string Patrimonio { get; set; } = string.Empty;
    }

    public static class SQLiteDatabaseManager
    {
        private static HashSet<string>? _serialCache = null;
        private static readonly object _cacheLock = new object();
        private static string _dbPath = "";
        private static string _connectionString = "";
        private static bool _esquemaValidado = false;
        private static bool _patrimoniosNormalizados = false;

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
                        Patrimonio TEXT,
                        Serial TEXT UNIQUE,
                        Fabricante TEXT,
                        Modelo TEXT,
                        Versao_Android TEXT,
                        API_Level TEXT,
                        Bateria TEXT,
                        IMEI TEXT,
                        MAC TEXT
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

                // Garante o schema canonico (Patrimonio apos ID, sem DataRegistro).
                // Os IDs existentes sao preservados e alinhados ao patrimonio.
                GarantirEsquemaDispositivos(connection);

                if (isNew)
                {
                    ImportFromCsvIfExists(connection);
                }

                if (!_patrimoniosNormalizados)
                {
                    NormalizarPatrimoniosExistentes(connection);
                    _patrimoniosNormalizados = true;
                }
            }
        }

        /// <summary>
        /// Colunas canonicas da tabela Dispositivos (a ordem importa: Patrimonio vem logo apos o ID).
        /// </summary>
        private static readonly string[] COLUNAS_DISPOSITIVOS =
        {
            "Patrimonio", "Serial", "Fabricante", "Modelo",
            "Versao_Android", "API_Level", "Bateria", "IMEI", "MAC"
        };

        /// <summary>
        /// Garante o formato do banco (schema canonico com Patrimonio).
        /// Os IDs sao preservados: eles representam o numero de patrimonio alinhado.
        /// </summary>
        private static void GarantirEsquemaDispositivos(SqliteConnection connection)
        {
            try
            {
                // Validacao feita uma vez por processo (o schema so muda por migracao nossa).
                if (_esquemaValidado) return;

                var colunasAtuais = LerColunas(connection, "Dispositivos");
                if (colunasAtuais.Count == 0) return; // tabela recem-criada no schema correto

                bool precisaMigrarSchema =
                    colunasAtuais.Contains("DataRegistro", StringComparer.OrdinalIgnoreCase) ||
                    !colunasAtuais.Contains("Patrimonio", StringComparer.OrdinalIgnoreCase);

                if (precisaMigrarSchema)
                {
                    RecriarTabelaDispositivos(connection);
                }

                _esquemaValidado = true;
            }
            catch { }
        }

        /// <summary>
        /// Padroniza os patrimonios legados para o formato INVxxxx (apenas digitos apos o prefixo).
        /// Valores sem digitos (ex.: etiquetas antigas com letras) sao preservados para revisao manual.
        /// </summary>
        private static void NormalizarPatrimoniosExistentes(SqliteConnection connection)
        {
            try
            {
                var atualizacoes = new List<KeyValuePair<int, string>>();

                using (var select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT ID, Patrimonio FROM Dispositivos WHERE Patrimonio IS NOT NULL AND TRIM(Patrimonio) <> '';";
                    using (var reader = select.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string atual = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                            if (string.IsNullOrWhiteSpace(atual)) continue;
                            if (!PatrimonioHelper.TryNormalizar(atual, out string normalizado, out _)) continue;
                            if (string.IsNullOrWhiteSpace(normalizado) || normalizado == atual) continue;

                            atualizacoes.Add(new KeyValuePair<int, string>(id, normalizado));
                        }
                    }
                }

                if (atualizacoes.Count == 0) return;

                using (var transaction = connection.BeginTransaction())
                {
                    using (var update = connection.CreateCommand())
                    {
                        update.CommandText = "UPDATE Dispositivos SET Patrimonio = $patrimonio WHERE ID = $id";
                        var pPat = update.CreateParameter(); pPat.ParameterName = "$patrimonio"; update.Parameters.Add(pPat);
                        var pId = update.CreateParameter(); pId.ParameterName = "$id"; update.Parameters.Add(pId);

                        foreach (var par in atualizacoes)
                        {
                            pPat.Value = par.Value;
                            pId.Value = par.Key;
                            update.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
            catch { }
        }

        private static List<string> LerColunas(SqliteConnection connection, string tabela)
        {
            var lista = new List<string>();
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info({tabela});";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(1)) lista.Add(reader.GetString(1));
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        /// <summary>
        /// Recria a tabela Dispositivos copiando os dados existentes e preservando os IDs.
        /// Colunas legadas sao descartadas e colunas ausentes recebem valor vazio.
        /// </summary>
        private static void RecriarTabelaDispositivos(SqliteConnection connection)
        {
            var colunasAtuais = LerColunas(connection, "Dispositivos");
            if (colunasAtuais.Count == 0) return;

            // Todas as colunas canonicas entram no destino; as ausentes na origem recebem ''.
            // O Serial mantem NULL (em vez de '') para nao violar a constraint UNIQUE
            // caso existam linhas antigas com serial vazio.
            var expressoes = new List<string>();
            foreach (var c in COLUNAS_DISPOSITIVOS)
            {
                if (!colunasAtuais.Contains(c, StringComparer.OrdinalIgnoreCase))
                    expressoes.Add("''");
                else if (string.Equals(c, "Serial", StringComparison.OrdinalIgnoreCase))
                    expressoes.Add("\"Serial\"");
                else
                    expressoes.Add("COALESCE(\"" + c + "\", '')");
            }
            if (expressoes.Count == 0) return;

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            CREATE TABLE Dispositivos__novo (
                                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                Patrimonio TEXT,
                                Serial TEXT UNIQUE,
                                Fabricante TEXT,
                                Modelo TEXT,
                                Versao_Android TEXT,
                                API_Level TEXT,
                                Bateria TEXT,
                                IMEI TEXT,
                                MAC TEXT
                            );";
                        cmd.ExecuteNonQuery();
                    }

                    string listaColunas = "ID, " + string.Join(", ", COLUNAS_DISPOSITIVOS);
                    string listaValores = "ID, " + string.Join(", ", expressoes);

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = $"INSERT INTO Dispositivos__novo ({listaColunas}) SELECT {listaValores} FROM Dispositivos ORDER BY ID ASC;";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DROP TABLE Dispositivos;";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "ALTER TABLE Dispositivos__novo RENAME TO Dispositivos;";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        try
                        {
                            // Mantem o autoincrement acima do maior ID preservado.
                            cmd.CommandText = @"
                                DELETE FROM sqlite_sequence WHERE name IN ('Dispositivos', 'Dispositivos__novo');
                                INSERT INTO sqlite_sequence (name, seq)
                                SELECT 'Dispositivos', IFNULL(MAX(ID), 0) FROM Dispositivos;";
                            cmd.ExecuteNonQuery();
                        }
                        catch { }
                    }

                    transaction.Commit();
                }
                catch
                {
                    try { transaction.Rollback(); } catch { }
                    throw;
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
                        INSERT OR IGNORE INTO Dispositivos (Patrimonio, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC)
                        VALUES ($patrimonio, $serial, $fabricante, $modelo, $versao, $api, $bateria, $imei, $mac)";

                    var pPat = cmd.CreateParameter(); pPat.ParameterName = "$patrimonio"; cmd.Parameters.Add(pPat);
                    var pSerial = cmd.CreateParameter(); pSerial.ParameterName = "$serial"; cmd.Parameters.Add(pSerial);
                    var pFab = cmd.CreateParameter(); pFab.ParameterName = "$fabricante"; cmd.Parameters.Add(pFab);
                    var pMod = cmd.CreateParameter(); pMod.ParameterName = "$modelo"; cmd.Parameters.Add(pMod);
                    var pVer = cmd.CreateParameter(); pVer.ParameterName = "$versao"; cmd.Parameters.Add(pVer);
                    var pApi = cmd.CreateParameter(); pApi.ParameterName = "$api"; cmd.Parameters.Add(pApi);
                    var pBat = cmd.CreateParameter(); pBat.ParameterName = "$bateria"; cmd.Parameters.Add(pBat);
                    var pImei = cmd.CreateParameter(); pImei.ParameterName = "$imei"; cmd.Parameters.Add(pImei);
                    var pMac = cmd.CreateParameter(); pMac.ParameterName = "$mac"; cmd.Parameters.Add(pMac);

                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        var parts = lines[i].Split(';');
                        if (parts.Length >= 9)
                        {
                            pPat.Value = "";
                            pSerial.Value = parts[0];
                            pFab.Value = parts[1];
                            pMod.Value = parts[2];
                            pVer.Value = parts[3];
                            pApi.Value = parts[4];
                            pBat.Value = parts[5];
                            pImei.Value = parts[6];
                            pMac.Value = parts[7];
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
                            AuditLogManager.GravarDispositivo("ATUALIZAR_MAC", serial, "", mac, "MAC preenchido automaticamente");
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
                            INSERT OR IGNORE INTO Dispositivos (Patrimonio, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC)
                            VALUES ($patrimonio, $serial, $fabricante, $modelo, $versao, $api, $bateria, $imei, $mac)";
                        cmd.Parameters.AddWithValue("$patrimonio", "");
                        cmd.Parameters.AddWithValue("$serial", serial);
                        cmd.Parameters.AddWithValue("$fabricante", string.IsNullOrEmpty(fabricante) ? "Desconhecido" : fabricante);
                        cmd.Parameters.AddWithValue("$modelo", modelo);
                        cmd.Parameters.AddWithValue("$versao", "N/A");
                        cmd.Parameters.AddWithValue("$api", "N/A");
                        cmd.Parameters.AddWithValue("$bateria", "N/A");
                        cmd.Parameters.AddWithValue("$imei", "N/A");
                        cmd.Parameters.AddWithValue("$mac", mac);
                        cmd.ExecuteNonQuery();
                    }
                    cache.Add(serial);
                    if (!string.IsNullOrEmpty(mac) && mac.Length == 17) SincronizarCsv();
                    AuditLogManager.GravarDispositivo("CADASTRAR", serial, "", mac, "Novo dispositivo cadastrado");
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
                    cmd.CommandText = "SELECT ID, Patrimonio, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC FROM Dispositivos WHERE Serial = $serial LIMIT 1";
                    cmd.Parameters.AddWithValue("$serial", serial);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string ser = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            string fab = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                            string mod = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

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
                                Patrimonio = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Serial = ser,
                                Fabricante = fab,
                                Modelo = mod,
                                MarcaModelo = (fab + " " + mod).Trim(),
                                Versao_Android = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                API_Level = reader.IsDBNull(6) ? "N/A" : reader.GetString(6),
                                Bateria = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                                IMEI = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                                MAC = reader.IsDBNull(9) ? "N/A" : reader.GetString(9)
                            };
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public static DispositivoDB? BuscarPorMAC(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return null;

            InitializeDatabase();
            string alvo = mac.Trim().Replace('-', ':').ToUpperInvariant();
            string? serial = null;

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT Serial FROM Dispositivos WHERE REPLACE(UPPER(TRIM(MAC)), '-', ':') = $mac ORDER BY CASE WHEN Patrimonio IS NULL OR TRIM(Patrimonio) = '' THEN 1 ELSE 0 END, ID LIMIT 1;";
                    cmd.Parameters.AddWithValue("$mac", alvo);
                    serial = cmd.ExecuteScalar() as string;
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(serial))
                return BuscarPorSerial(serial);

            return null;
        }

        public static bool RegistrarComPatrimonio(string serial, string fabricante, string modelo, string mac, string patrimonio)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            if (string.IsNullOrWhiteSpace(patrimonio)) return false;
            if (!PatrimonioHelper.TryNormalizar(patrimonio, out string normalizado, out _)) return false;

            string macNormalizado = (mac ?? string.Empty).Trim();
            RegistrarSeNovo(serial, fabricante, modelo, macNormalizado);

            var alvo = BuscarPorSerial(serial);
            if (alvo != null && MacCaptureService.MacValido(macNormalizado))
            {
                string macAtual = (alvo.MAC ?? string.Empty).Trim().Replace('-', ':');
                string macNovo = macNormalizado.Replace('-', ':');
                if (!string.Equals(macAtual, macNovo, StringComparison.OrdinalIgnoreCase))
                    RegistrarOuAtualizarMAC(serial, alvo.Modelo, macNormalizado);
            }

            alvo = BuscarPorMAC(macNormalizado) ?? BuscarPorSerial(serial);
            if (alvo == null) return false;

            return AtualizarPatrimonio(alvo.ID, normalizado);
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
                    cmd.CommandText = @"
                        SELECT ID, Patrimonio, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC
                        FROM Dispositivos
                        ORDER BY
                            CASE WHEN Patrimonio IS NULL OR TRIM(Patrimonio) = '' THEN 1 ELSE 0 END,
                            CASE WHEN UPPER(TRIM(Patrimonio)) LIKE 'INV%'
                                 THEN CAST(SUBSTR(UPPER(TRIM(Patrimonio)), 4) AS INTEGER)
                                 ELSE 999999999 END,
                            Patrimonio COLLATE NOCASE,
                            ID ASC";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string ser = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            string fab = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                            string mod = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

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
                                Patrimonio = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Serial = ser,
                                Fabricante = fab,
                                Modelo = mod,
                                MarcaModelo = marcaModeloFormada,
                                Versao_Android = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                API_Level = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                Bateria = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                IMEI = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                                MAC = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
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
                            Patrimonio = $patrimonio,
                            Fabricante = $fabricante, 
                            Modelo = $modelo, 
                            MAC = $mac, 
                            Serial = $serial
                            WHERE ID = $id";

                        var pPat = cmd.CreateParameter(); pPat.ParameterName = "$patrimonio"; cmd.Parameters.Add(pPat);
                        var pFab = cmd.CreateParameter(); pFab.ParameterName = "$fabricante"; cmd.Parameters.Add(pFab);
                        var pMod = cmd.CreateParameter(); pMod.ParameterName = "$modelo"; cmd.Parameters.Add(pMod);
                        var pMac = cmd.CreateParameter(); pMac.ParameterName = "$mac"; cmd.Parameters.Add(pMac);
                        var pSerial = cmd.CreateParameter(); pSerial.ParameterName = "$serial"; cmd.Parameters.Add(pSerial);
                        var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);

                        foreach (var d in lista)
                        {
                            pPat.Value = PatrimonioHelper.NormalizarOuOriginal(d.Patrimonio);
                            pFab.Value = d.Fabricante ?? string.Empty;
                            pMod.Value = d.Modelo ?? string.Empty;
                            pMac.Value = d.MAC ?? string.Empty;
                            pSerial.Value = d.Serial ?? string.Empty;
                            pId.Value = d.ID;
                            cmd.ExecuteNonQuery();
                            AuditLogManager.GravarDispositivo("ATUALIZAR_DISPOSITIVO", d.Serial ?? string.Empty, d.Patrimonio ?? string.Empty, d.MAC ?? string.Empty,
                                $"Salvo em lote (ID={d.ID}, Fabricante={d.Fabricante}, Modelo={d.Modelo})");
                        }
                        transaction.Commit();
                    }
                }
                SincronizarCsv();
            }
            catch { }
        }

        /// <summary>
        /// Atualiza o numero de patrimonio e alinha o ID ao numero informado.
        /// Se o ID de destino estiver ocupado, o ocupante e movido para um ID livre.
        /// </summary>
        public static bool AtualizarPatrimonio(int id, string patrimonio)
        {
            if (!PatrimonioHelper.TryNormalizar(patrimonio, out string normalizado, out _)) return false;

            string serialAntes = "";
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Serial FROM Dispositivos WHERE ID = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        serialAntes = cmd.ExecuteScalar() as string ?? string.Empty;
                    }
                }
            }
            catch { }

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int idDestino = PatrimonioHelper.ExtrairIdNumerico(normalizado);
                            int idFinal = ResolverIdDestino(connection, transaction, id, idDestino);

                            var cmd = connection.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "UPDATE Dispositivos SET Patrimonio = $patrimonio, ID = $novoId WHERE ID = $id";
                            cmd.Parameters.AddWithValue("$patrimonio", normalizado);
                            cmd.Parameters.AddWithValue("$novoId", idFinal);
                            cmd.Parameters.AddWithValue("$id", id);

                            if (cmd.ExecuteNonQuery() == 0)
                            {
                                transaction.Rollback();
                                return false;
                            }

                            SincronizarSequenciaId(connection, transaction);
                            transaction.Commit();
                        }
                        catch
                        {
                            try { transaction.Rollback(); } catch { }
                            return false;
                        }
                    }
                }
                SincronizarCsv();
                AuditLogManager.GravarDispositivo("ATUALIZAR_PATRIMONIO", serialAntes, normalizado, "", $"ID alterado de {id} para idFinal");
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Atualiza os dados completos do dispositivo e alinha o ID ao patrimonio informado.
        /// </summary>
        public static bool AtualizarDispositivo(int id, string serial, string fabricante, string modelo, string mac, string patrimonio)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            if (!PatrimonioHelper.TryNormalizar(patrimonio, out string normalizado, out _)) return false;

            string serialAntes = "";
            string patrimonioAntes = "";
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Serial, Patrimonio FROM Dispositivos WHERE ID = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                serialAntes = r.IsDBNull(0) ? string.Empty : r.GetString(0);
                                patrimonioAntes = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int idDestino = PatrimonioHelper.ExtrairIdNumerico(normalizado);
                            int idFinal = ResolverIdDestino(connection, transaction, id, idDestino);

                            var cmd = connection.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                UPDATE Dispositivos SET
                                    Patrimonio = $patrimonio,
                                    Serial = $serial,
                                    Fabricante = $fabricante,
                                    Modelo = $modelo,
                                    MAC = $mac,
                                    ID = $novoId
                                WHERE ID = $id";
                            cmd.Parameters.AddWithValue("$patrimonio", normalizado);
                            cmd.Parameters.AddWithValue("$serial", serial);
                            cmd.Parameters.AddWithValue("$fabricante", fabricante ?? string.Empty);
                            cmd.Parameters.AddWithValue("$modelo", modelo ?? string.Empty);
                            cmd.Parameters.AddWithValue("$mac", mac ?? string.Empty);
                            cmd.Parameters.AddWithValue("$novoId", idFinal);
                            cmd.Parameters.AddWithValue("$id", id);

                            if (cmd.ExecuteNonQuery() == 0)
                            {
                                transaction.Rollback();
                                return false;
                            }

                            SincronizarSequenciaId(connection, transaction);
                            transaction.Commit();
                        }
                        catch
                        {
                            try { transaction.Rollback(); } catch { }
                            return false;
                        }
                    }
                }
                SincronizarCsv();
                AuditLogManager.GravarDispositivo("ATUALIZAR_DISPOSITIVO", serial, normalizado, mac ?? string.Empty,
                    $"Antes: Serial={serialAntes}, Patrimonio={patrimonioAntes}. Depois: Serial={serial}, Patrimonio={normalizado}, Modelo={modelo}, MAC={mac}");
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Resolve conflitos de ID movendo o registro ocupante para o proximo ID livre.
        /// Retorna o ID final que o registro editado deve assumir.
        /// </summary>
        private static int ResolverIdDestino(SqliteConnection connection, SqliteTransaction transaction, int idAtual, int idDestino)
        {
            if (idDestino <= 0 || idDestino == idAtual) return idAtual;

            bool ocupado;
            using (var check = connection.CreateCommand())
            {
                check.Transaction = transaction;
                check.CommandText = "SELECT COUNT(*) FROM Dispositivos WHERE ID = $destino AND ID <> $atual;";
                check.Parameters.AddWithValue("$destino", idDestino);
                check.Parameters.AddWithValue("$atual", idAtual);
                ocupado = Convert.ToInt64(check.ExecuteScalar() ?? 0L) > 0;
            }

            if (!ocupado) return idDestino;

            long maxId;
            using (var maxCmd = connection.CreateCommand())
            {
                maxCmd.Transaction = transaction;
                maxCmd.CommandText = "SELECT IFNULL(MAX(ID), 0) FROM Dispositivos;";
                maxId = Convert.ToInt64(maxCmd.ExecuteScalar() ?? 0L);
            }

            if (maxId >= int.MaxValue) throw new InvalidOperationException("Limite de IDs atingido.");

            int idLivre = (int)maxId + 1;
            using (var mover = connection.CreateCommand())
            {
                mover.Transaction = transaction;
                mover.CommandText = "UPDATE Dispositivos SET ID = $livre WHERE ID = $destino;";
                mover.Parameters.AddWithValue("$livre", idLivre);
                mover.Parameters.AddWithValue("$destino", idDestino);
                mover.ExecuteNonQuery();
            }
            return idDestino;
        }

        private static void SincronizarSequenciaId(SqliteConnection connection, SqliteTransaction transaction)
        {
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        UPDATE sqlite_sequence
                        SET seq = (SELECT IFNULL(MAX(ID), 0) FROM Dispositivos)
                        WHERE name = 'Dispositivos'
                          AND seq < (SELECT IFNULL(MAX(ID), 0) FROM Dispositivos);";
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        public static void Excluir(int id)
        {
            string serialExcluido = "";
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Serial FROM Dispositivos WHERE ID = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        serialExcluido = cmd.ExecuteScalar() as string ?? string.Empty;
                    }
                }
            }
            catch { }

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
                AuditLogManager.GravarDispositivo("EXCLUIR", serialExcluido, "", "", $"Dispositivo excluido (ID={id})");
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

        /// <summary>
        /// Registra ou atualiza o MAC Wi-Fi de um dispositivo na tabela Dispositivos.
        /// Se o serial já existe: atualiza MAC e Modelo (sem duplicar).
        /// Se não existe: insere novo registro (UPSERT).
        /// </summary>
        public static void RegistrarOuAtualizarMAC(string serial, string modelo, string mac)
        {
            if (string.IsNullOrEmpty(serial)) return;
            if (serial == "unknown" || serial.StartsWith("?") || serial == "Pendente") return;
            if (string.IsNullOrWhiteSpace(mac)) return;

            lock (_cacheLock)
            {
                var cache = GetSerialCache();
                bool jaExiste = cache.Contains(serial);

                try
                {
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
                            UPDATE Dispositivos
                            SET MAC = $mac,
                                Modelo = COALESCE(NULLIF($modelo, ''), Modelo)
                            WHERE Serial = $serial";
                        cmd.Parameters.AddWithValue("$mac", mac);
                        cmd.Parameters.AddWithValue("$modelo", string.IsNullOrEmpty(modelo) ? "Desconhecido" : modelo);
                        cmd.Parameters.AddWithValue("$serial", serial);
                        int linhasAfetadas = cmd.ExecuteNonQuery();

                        if (linhasAfetadas == 0)
                        {
                            // Não existe ainda: insere novo
                            cmd.CommandText = @"
                                INSERT OR IGNORE INTO Dispositivos (Patrimonio, Serial, Fabricante, Modelo, Versao_Android, API_Level, Bateria, IMEI, MAC)
                                VALUES ($patrimonio, $serial, $fabricante, $modelo, $versao, $api, $bateria, $imei, $mac)";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("$patrimonio", "");
                            cmd.Parameters.AddWithValue("$serial", serial);
                            cmd.Parameters.AddWithValue("$fabricante", "Desconhecido");
                            cmd.Parameters.AddWithValue("$modelo", string.IsNullOrEmpty(modelo) ? "Desconhecido" : modelo);
                            cmd.Parameters.AddWithValue("$versao", "N/A");
                            cmd.Parameters.AddWithValue("$api", "N/A");
                            cmd.Parameters.AddWithValue("$bateria", "N/A");
                            cmd.Parameters.AddWithValue("$imei", "N/A");
                            cmd.Parameters.AddWithValue("$mac", mac);
                            cmd.ExecuteNonQuery();
                            AuditLogManager.GravarDispositivo("CADASTRAR", serial, "", mac, $"Novo dispositivo via RegistrarOuAtualizarMAC (modelo={modelo})");
                        }
                        else
                        {
                            AuditLogManager.GravarDispositivo("ATUALIZAR_MAC", serial, "", mac, $"MAC atualizado (modelo={modelo})");
                        }
                    }
                    cache.Add(serial);
                    SincronizarCsv();
                }
                catch { }
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
                sb.AppendLine("ID;Patrimonio;Serial;Fabricante;Modelo;Versao_Android;API_Level;Bateria;IMEI;MAC");
                foreach (var d in todos)
                {
                    sb.AppendLine($"{d.ID};{d.Patrimonio};{d.Serial};{d.Fabricante};{d.Modelo};{d.Versao_Android};{d.API_Level};{d.Bateria};{d.IMEI};{d.MAC}");
                }
                System.IO.File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);

                string relatorioPath = System.IO.Path.Combine(dataDir, "relatorio_serial_mac.csv");
                var sbRel = new StringBuilder();
                sbRel.AppendLine("Serial;Patrimonio;MAC;Fabricante;Modelo");
                foreach (var d in todos)
                {
                    sbRel.AppendLine($"{d.Serial};{d.Patrimonio};{d.MAC};{d.Fabricante};{d.Modelo}");
                }
                System.IO.File.WriteAllText(relatorioPath, sbRel.ToString(), Encoding.UTF8);
            }
            catch { }
        }

    }
}
