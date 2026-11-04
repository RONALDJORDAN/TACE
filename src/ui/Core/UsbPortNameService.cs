using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TACEUI.Core
{
    /// <summary>
    /// Mantem um apelido por porta USB fisica (ex.: "1-1" -> "Mesa 1").
    /// O nome fica salvo em config/portas_usb.json e vale para qualquer
    /// aparelho conectado naquela porta.
    /// </summary>
    public static class UsbPortNameService
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, string>? _nomes;

        private static string Arquivo =>
            Path.Combine(PathHelper.GetRootDirectory(), "config", "portas_usb.json");

        public static string ObterNome(string? porta)
        {
            if (string.IsNullOrWhiteSpace(porta)) return string.Empty;

            lock (_lock)
            {
                string chave = porta.Trim();
                return Carregar().TryGetValue(chave, out string? nome) ? nome : string.Empty;
            }
        }

        /// <summary>Nome amigavel para exibicao em colunas. Sem nome, devolve a propria porta.</summary>
        public static string ObterExibicao(string? porta)
        {
            if (string.IsNullOrWhiteSpace(porta) || porta == "N/A") return "---";
            string chave = porta.Trim();
            string nome = ObterNome(chave);
            return string.IsNullOrWhiteSpace(nome) ? ObterRotuloCurto(chave) : nome;
        }

        /// <summary>Nome e porta fisica, usado em logs e dialogos (ex.: "Mesa 1 (1-1)").</summary>
        public static string ObterExibicaoComPorta(string? porta)
        {
            if (string.IsNullOrWhiteSpace(porta) || porta == "N/A") return "---";
            string chave = porta.Trim();
            string nome = ObterNome(chave);
            string rotulo = ObterRotuloCurto(chave);
            return string.IsNullOrWhiteSpace(nome) ? rotulo : $"{nome} ({rotulo})";
        }

        public static string ObterRotuloCurto(string? porta)
        {
            if (string.IsNullOrWhiteSpace(porta) || porta == "N/A") return "---";
            string chave = porta.Trim();

            const string prefixo = "Port_#";
            const string separador = ".Hub_#";
            if (chave.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                int posHub = chave.IndexOf(separador, StringComparison.OrdinalIgnoreCase);
                if (posHub > prefixo.Length)
                {
                    string portaNum = chave.Substring(prefixo.Length, posHub - prefixo.Length);
                    string hubNum = chave.Substring(posHub + separador.Length);
                    if (int.TryParse(portaNum, out int p) && int.TryParse(hubNum, out int h))
                        return $"H{h}-P{p}";
                }
            }

            return chave;
        }

        public static IReadOnlyDictionary<string, string> ObterTodos()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(Carregar(), StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void SalvarNomes(IEnumerable<KeyValuePair<string, string>> nomes)
        {
            lock (_lock)
            {
                var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in nomes)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    string nome = (kv.Value ?? string.Empty).Trim();
                    if (nome.Length > 32) nome = nome.Substring(0, 32);
                    if (!string.IsNullOrEmpty(nome)) mapa[kv.Key.Trim()] = nome;
                }

                try
                {
                    string? pasta = Path.GetDirectoryName(Arquivo);
                    if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

                    string temporario = Arquivo + ".tmp";
                    var opcoes = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    File.WriteAllText(temporario, JsonSerializer.Serialize(mapa, opcoes));
                    File.Move(temporario, Arquivo, true);
                    _nomes = mapa;
                }
                catch
                {
                    // Falha de disco nao deve impedir o uso do programa.
                }
            }
        }

        private static Dictionary<string, string> Carregar()
        {
            if (_nomes != null) return _nomes;

            var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(Arquivo))
                {
                    string json = File.ReadAllText(Arquivo);
                    var lido = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (lido != null)
                    {
                        foreach (var kv in lido)
                        {
                            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value)) continue;
                            mapa[kv.Key.Trim()] = kv.Value.Trim();
                        }
                    }
                }
            }
            catch
            {
                // JSON invalido ou arquivo bloqueado: segue sem apelidos.
            }

            _nomes = mapa;
            return _nomes;
        }
    }
}
