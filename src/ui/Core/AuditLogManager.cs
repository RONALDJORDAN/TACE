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
                string dir = Path.GetDirectoryName(dbPath);
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
                    }
                }
                catch { }

                _inicializado = true;
            }
        }

        public static void Gravar(string acao, string tabela, string registroId, string detalhes)
        {
            try
            {
                Inicializar();

                string ip = ObterIpMaquina();
                string usuario = string.IsNullOrWhiteSpace(UsuarioAtual) ? "Operador" : UsuarioAtual;
                string dataHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO Auditoria (DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina)
                            VALUES ($data, $usuario, $acao, $tabela, $registroId, $detalhes, $ip)";
                        cmd.Parameters.AddWithValue("$data", dataHora);
                        cmd.Parameters.AddWithValue("$usuario", usuario);
                        cmd.Parameters.AddWithValue("$acao", acao);
                        cmd.Parameters.AddWithValue("$tabela", tabela);
                        cmd.Parameters.AddWithValue("$registroId", string.IsNullOrEmpty(registroId) ? (object)DBNull.Value : registroId);
                        cmd.Parameters.AddWithValue("$detalhes", string.IsNullOrEmpty(detalhes) ? (object)DBNull.Value : detalhes);
                        cmd.Parameters.AddWithValue("$ip", string.IsNullOrEmpty(ip) ? (object)DBNull.Value : ip);
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

            Gravar(acao, tabela, registroId, detalhes);
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
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina
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
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
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
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina
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
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
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
                            SELECT ID, DataHora, Usuario, Acao, Tabela, RegistroId, Detalhes, IpMaquina
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
                                    IpMaquina = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return lista;
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
