using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace BotRpaUI.Core
{
    public class AuditoriaRegistro
    {
        public int ID { get; set; }
        public string DataHora { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Acao { get; set; } = string.Empty;
        public string Tabela { get; set; } = string.Empty;
        public string RegistroId { get; set; } = string.Empty;
        public string Detalhes { get; set; } = string.Empty;
        public string IpMaquina { get; set; } = string.Empty;
        public string Modalidade { get; set; } = string.Empty;

        public string DataHoraFormatada
        {
            get
            {
                if (DateTime.TryParseExact(DataHora, "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime data))
                {
                    return data.ToString("dd/MM/yyyy HH:mm:ss");
                }

                return DataHora;
            }
        }
    }

    public class AuditoriaResumo
    {
        public int Total { get; set; }
        public int Ultimos7Dias { get; set; }
        public int Ultimos30Dias { get; set; }
        public string UltimaDataHora { get; set; } = string.Empty;
        public string UltimaAcao { get; set; } = string.Empty;
        public string AcaoPrincipal { get; set; } = string.Empty;
        public int AcaoPrincipalQuantidade { get; set; }
    }

    public static class AuditLogManager
    {
        private static string _connectionString = "";
        private static bool _inicializado = false;
        private static readonly object _initLock = new object();

        public static string UsuarioAtual { get; set; } = "Operador";

        private static string GetConnectionString()
        {
            if (!string.IsNullOrEmpty(_connectionString)) return _connectionString;
            string dbPath = Path.Combine(PathHelper.GetRootDirectory(), "data", "bot_database.db");
            _connectionString = $"Data Source={dbPath}";
            return _connectionString;
        }

        public static void Inicializar()
        {
            if (_inicializado) return;
            lock (_initLock)
            {
                if (_inicializado) return;

                string dbPath = Path.Combine(PathHelper.GetRootDirectory(), "data", "bot_database.db");
                string? dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _connectionString = $"Data Source={dbPath}";

                try
                {
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        connection.Open();
                        string createTable = @"
                            CREATE TABLE IF NOT EXISTS Auditoria (
                                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                DataHora TEXT NOT NULL,
                                Usuario TEXT NOT NULL,
                                Acao TEXT NOT NULL,
                                Tabela TEXT NOT NULL,
                                RegistroId TEXT,
                                Detalhes TEXT,
                                IpMaquina TEXT
                            );";
                        using (var cmd = new SqliteCommand(createTable, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        string createIndex = @"
                            CREATE INDEX IF NOT EXISTS IX_Auditoria_DataHora ON Auditoria(DataHora);
                            CREATE INDEX IF NOT EXISTS IX_Auditoria_Usuario ON Auditoria(Usuario);
                            CREATE INDEX IF NOT EXISTS IX_Auditoria_Acao ON Auditoria(Acao);";
                        using (var cmd = new SqliteCommand(createIndex, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        GarantirColunaModalidade(connection);
                    }
                }
                catch { }

                _inicializado = true;
            }
        }

        private static void GarantirColunaModalidade(SqliteConnection connection)
        {
            bool existe = false;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Auditoria);";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.FieldCount > 1 && !reader.IsDBNull(1) &&
                            string.Equals(reader.GetString(1), "Modalidade", StringComparison.OrdinalIgnoreCase))
                        {
                            existe = true;
                            break;
                        }
                    }
                }
            }

            if (!existe)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "ALTER TABLE Auditoria ADD COLUMN Modalidade TEXT;";
                    cmd.ExecuteNonQuery();
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE Auditoria
                    SET Modalidade = CASE
                        WHEN UPPER(Acao) IN ('CADASTRAR','CADASTRO','ATUALIZAR_MAC','ATUALIZAR_PATRIMONIO','ATUALIZAR_DISPOSITIVO','EXCLUIR') THEN 'Cadastro'
                        WHEN UPPER(Acao) LIKE '%BUILD%' THEN 'Build'
                        WHEN UPPER(Acao) LIKE '%OTA%' THEN 'OTA'
                        WHEN UPPER(Acao) LIKE 'EXEC%' OR UPPER(Acao) LIKE 'F1%' OR UPPER(Acao) LIKE 'F2%'
                          OR UPPER(Acao) LIKE 'F3%' OR UPPER(Acao) LIKE 'F4%' OR UPPER(Acao) LIKE 'F5%'
                          OR UPPER(Acao) LIKE 'F6%' OR UPPER(Acao) LIKE 'F7%' OR UPPER(Acao) LIKE 'F8%' THEN 'Execução'
                        ELSE 'Sistema'
                    END
                    WHERE Modalidade IS NULL OR TRIM(Modalidade) = '';";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Auditoria_Modalidade ON Auditoria(Modalidade);";
                cmd.ExecuteNonQuery();
            }
        }

        public static string InferirModalidade(string? acao, string? tabela)
        {
            string a = (acao ?? string.Empty).Trim().ToUpperInvariant();
            string t = (tabela ?? string.Empty).Trim().ToUpperInvariant();

            if (a.Contains("BUILD") || t.Contains("BUILD")) return "Build";
            if (a.Contains("OTA") || t.Contains("OTA")) return "OTA";
            if (a.StartsWith("EXEC") || a.StartsWith("F1") || a.StartsWith("F2") ||
                a.StartsWith("F3") || a.StartsWith("F4") || a.StartsWith("F5") ||
                a.StartsWith("F6") || a.StartsWith("F7") || a.StartsWith("F8"))
                return "Execução";
            if (t == "DISPOSITIVOS" || a == "CADASTRAR" || a == "CADASTRO" ||
                a == "ATUALIZAR_MAC" || a == "ATUALIZAR_PATRIMONIO" ||
                a == "ATUALIZAR_DISPOSITIVO" || a == "EXCLUIR")
                return "Cadastro";

            return "Sistema";
        }

        public static void Gravar(string acao, string tabela, string registroId, string detalhes, string? modalidade = null)
        {
            try
            {
                Inicializar();

                string ip = ObterIpMaquina();
                string usuario = string.IsNullOrWhiteSpace(UsuarioAtual) ? "Operador" : UsuarioAtual;
                string dataHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string modalidadeFinal = string.IsNullOrWhiteSpace(modalidade)
                    ? InferirModalidade(acao, tabela)
                    : modalidade.Trim();

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO Auditoria (DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina, Modalidade)
                            VALUES ($data, $usuario, $acao, $tabela, $registroId, $detalhes, $ip, $modalidade)";
                        cmd.Parameters.AddWithValue("$data", dataHora);
                        cmd.Parameters.AddWithValue("$usuario", usuario);
                        cmd.Parameters.AddWithValue("$acao", acao);
                        cmd.Parameters.AddWithValue("$tabela", tabela);
                        cmd.Parameters.AddWithValue("$registroId", string.IsNullOrEmpty(registroId) ? (object)DBNull.Value : registroId);
                        cmd.Parameters.AddWithValue("$detalhes", string.IsNullOrEmpty(detalhes) ? (object)DBNull.Value : detalhes);
                        cmd.Parameters.AddWithValue("$ip", string.IsNullOrEmpty(ip) ? (object)DBNull.Value : ip);
                        cmd.Parameters.AddWithValue("$modalidade", modalidadeFinal);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public static void GravarDispositivo(string acao, string serial, string patrimonio, string mac, string detalhesAdicionais = "")
        {
            string tabela = "Dispositivos";
            string registroId = string.IsNullOrEmpty(serial) ? "N/A" : serial;
            string detalhes = $"Serial={serial}; Patrimonio={patrimonio}; MAC={mac}";
            if (!string.IsNullOrEmpty(detalhesAdicionais))
                detalhes += "; " + detalhesAdicionais;

            Gravar(acao, tabela, registroId, detalhes, "Cadastro");
        }

        public static List<AuditoriaRegistro> LerTodos(int limite = 500)
        {
            var lista = new List<AuditoriaRegistro>();
            try
            {
                Inicializar();

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina, Modalidade
                            FROM Auditoria
                            ORDER BY ID DESC
                            LIMIT $limite";
                        cmd.Parameters.AddWithValue("$limite", limite);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new AuditoriaRegistro
                                {
                                    ID = reader.GetInt32(0),
                                    DataHora = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    Usuario = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Acao = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Tabela = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    RegistroId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    Detalhes = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                    Modalidade = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        public static List<AuditoriaRegistro> LerFiltrado(
            DateTime? inicio,
            DateTime? fim,
            string? modalidade,
            string? busca,
            int limite = 5000)
        {
            var lista = new List<AuditoriaRegistro>();
            try
            {
                Inicializar();

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        var filtros = new List<string>();

                        if (inicio.HasValue)
                        {
                            filtros.Add("DataHora >= $inicio");
                            cmd.Parameters.AddWithValue("$inicio", inicio.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        if (fim.HasValue)
                        {
                            filtros.Add("DataHora <= $fim");
                            cmd.Parameters.AddWithValue("$fim", fim.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        if (!string.IsNullOrWhiteSpace(modalidade) &&
                            !modalidade.Equals("Todas", StringComparison.OrdinalIgnoreCase))
                        {
                            filtros.Add("COALESCE(NULLIF(TRIM(Modalidade),''), 'Sistema') = $modalidade");
                            cmd.Parameters.AddWithValue("$modalidade", modalidade.Trim());
                        }

                        if (!string.IsNullOrWhiteSpace(busca))
                        {
                            filtros.Add("(Usuario LIKE $busca OR Acao LIKE $busca OR Tabela LIKE $busca OR RegistroId LIKE $busca OR Detalhes LIKE $busca OR COALESCE(Modalidade,'') LIKE $busca)");
                            cmd.Parameters.AddWithValue("$busca", $"%{busca.Trim()}%");
                        }

                        string where = filtros.Count > 0 ? " WHERE " + string.Join(" AND ", filtros) : string.Empty;
                        cmd.CommandText = $@"
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina, Modalidade
                            FROM Auditoria{where}
                            ORDER BY ID DESC
                            LIMIT $limite";
                        cmd.Parameters.AddWithValue("$limite", limite);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new AuditoriaRegistro
                                {
                                    ID = reader.GetInt32(0),
                                    DataHora = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    Usuario = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Acao = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Tabela = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    RegistroId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    Detalhes = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                    Modalidade = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        public static List<AuditoriaRegistro> LerPorUsuario(string usuario, int limite = 200)
        {
            var lista = new List<AuditoriaRegistro>();
            try
            {
                Inicializar();

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina, Modalidade
                            FROM Auditoria
                            WHERE Usuario = $usuario
                            ORDER BY ID DESC
                            LIMIT $limite";
                        cmd.Parameters.AddWithValue("$usuario", usuario);
                        cmd.Parameters.AddWithValue("$limite", limite);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new AuditoriaRegistro
                                {
                                    ID = reader.GetInt32(0),
                                    DataHora = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    Usuario = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Acao = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Tabela = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    RegistroId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    Detalhes = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                    Modalidade = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        public static List<AuditoriaRegistro> LerPorSerial(string serial, int limite = 200)
        {
            var lista = new List<AuditoriaRegistro>();
            try
            {
                Inicializar();

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina, Modalidade
                            FROM Auditoria
                            WHERE Tabela = 'Dispositivos' AND RegistroId = $serial
                            ORDER BY ID DESC
                            LIMIT $limite";
                        cmd.Parameters.AddWithValue("$serial", serial);
                        cmd.Parameters.AddWithValue("$limite", limite);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new AuditoriaRegistro
                                {
                                    ID = reader.GetInt32(0),
                                    DataHora = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    Usuario = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Acao = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Tabela = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    RegistroId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    Detalhes = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                    Modalidade = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        public static AuditoriaResumo ObterResumo()
        {
            var resumo = new AuditoriaResumo();
            try
            {
                Inicializar();

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*),
                                   SUM(CASE WHEN DataHora >= $corte7 THEN 1 ELSE 0 END),
                                   SUM(CASE WHEN DataHora >= $corte30 THEN 1 ELSE 0 END)
                            FROM Auditoria";
                        cmd.Parameters.AddWithValue("$corte7", DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("$corte30", DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss"));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                resumo.Total = reader.IsDBNull(0) ? 0 : (int)reader.GetInt64(0);
                                resumo.Ultimos7Dias = reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1);
                                resumo.Ultimos30Dias = reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2);
                            }
                        }
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT DataHora, Acao FROM Auditoria ORDER BY ID DESC LIMIT 1;";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                resumo.UltimaDataHora = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                                resumo.UltimaAcao = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            }
                        }
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Acao, COUNT(*) AS Total
                            FROM Auditoria
                            GROUP BY Acao
                            ORDER BY Total DESC
                            LIMIT 1;";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                resumo.AcaoPrincipal = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                                resumo.AcaoPrincipalQuantidade = reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1);
                            }
                        }
                    }
                }
            }
            catch { }

            return resumo;
        }

        private static string ObterIpMaquina()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var addr in host.AddressList)
                {
                    if (addr.AddressFamily.ToString().Contains("InterNetwork"))
                        return addr.ToString();
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
