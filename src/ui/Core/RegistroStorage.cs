using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace BotRpaUI.Core
{
    /// <summary>
    /// Organiza os registros em disco por data, hora e modalidade.
    ///
    /// Layout novo:
    ///   docs/registros/AAAA-MM-DD/EXECUCAO/HH-mm_&lt;serial&gt;_execucao.log
    ///   docs/registros/AAAA-MM-DD/SISTEMA/HH-mm_sistema.log
    ///
    /// Builds continuam no caminho canonico exigido pelo AGENTS:
    ///   docs/registros/AAAA-MM-DD/HH-MM_Plataforma_BotRPA_DESKTOP_BUILD
    /// </summary>
    public static class RegistroStorage
    {
        public const string ModalidadeBuild = "Build";
        public const string ModalidadeExecucao = "Execução";
        public const string ModalidadeOta = "OTA";
        public const string ModalidadeSistema = "Sistema";

        private static readonly ConcurrentDictionary<string, string> Sessoes =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object EscritaLock = new object();

        public static string ObterPastaDia(DateTime data)
        {
            string dir = Path.Combine(PathHelper.GetRegistrosDirectory(), data.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string ObterPastaModalidade(DateTime data, string modalidade)
        {
            string dir = Path.Combine(ObterPastaDia(data), NormalizarPastaModalidade(modalidade));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string NormalizarPastaModalidade(string? modalidade)
        {
            if (string.IsNullOrWhiteSpace(modalidade)) return "SISTEMA";

            string valor = PathHelper.SanitizeFolderNameAscii(modalidade).Trim().ToUpperInvariant();
            if (valor.Contains("BUILD")) return "BUILD";
            if (valor.Contains("EXEC")) return "EXECUCAO";
            if (valor.Contains("OTA")) return "OTA";
            if (valor.Contains("SISTEMA")) return "SISTEMA";
            return valor.Length == 0 ? "SISTEMA" : valor;
        }

        public static string ObterCaminhoLogExecucao(string serial, DateTime? data = null)
        {
            if (string.IsNullOrWhiteSpace(serial)) throw new ArgumentException("Serial obrigatorio.", nameof(serial));

            DateTime agora = data ?? DateTime.Now;
            string chave = $"EXEC|{serial.Trim()}|{agora:yyyy-MM-dd}";

            return Sessoes.GetOrAdd(chave, _ =>
            {
                string dir = ObterPastaModalidade(agora, ModalidadeExecucao);
                string serialSeguro = PathHelper.SanitizeFolderNameAscii(serial.Trim());
                if (string.IsNullOrWhiteSpace(serialSeguro)) serialSeguro = "SERIAL";

                string caminho = Path.Combine(dir, $"{agora:HH-mm}_{serialSeguro}_execucao.log");
                int sufixo = 2;
                while (File.Exists(caminho))
                {
                    caminho = Path.Combine(dir, $"{agora:HH-mm}_{serialSeguro}_execucao_{sufixo}.log");
                    sufixo++;
                }

                return caminho;
            });
        }

        public static void RegistrarExecucao(string serial, string mensagem, DateTime? data = null)
        {
            if (string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(mensagem)) return;

            try
            {
                DateTime agora = data ?? DateTime.Now;
                string caminho = ObterCaminhoLogExecucao(serial, agora);

                lock (EscritaLock)
                {
                    if (!File.Exists(caminho))
                    {
                        File.AppendAllText(caminho, MontarCabecalho(serial, agora, ModalidadeExecucao), Encoding.UTF8);
                    }

                    File.AppendAllText(caminho, $"[{agora:yyyy-MM-dd HH:mm:ss}] {mensagem}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void RegistrarSistema(string mensagem, DateTime? data = null)
        {
            if (string.IsNullOrWhiteSpace(mensagem)) return;

            try
            {
                DateTime agora = data ?? DateTime.Now;
                string chave = $"SISTEMA|{agora:yyyy-MM-dd}";
                string caminho = Sessoes.GetOrAdd(chave, _ =>
                {
                    string dir = ObterPastaModalidade(agora, ModalidadeSistema);
                    string arquivo = Path.Combine(dir, $"{agora:HH-mm}_sistema.log");
                    int sufixo = 2;
                    while (File.Exists(arquivo))
                    {
                        arquivo = Path.Combine(dir, $"{agora:HH-mm}_sistema_{sufixo}.log");
                        sufixo++;
                    }
                    return arquivo;
                });

                lock (EscritaLock)
                {
                    if (!File.Exists(caminho))
                    {
                        File.AppendAllText(caminho, MontarCabecalho("SISTEMA", agora, ModalidadeSistema), Encoding.UTF8);
                    }

                    File.AppendAllText(caminho, $"[{agora:yyyy-MM-dd HH:mm:ss}] {mensagem}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch { }
        }

        private static string MontarCabecalho(string identificador, DateTime data, string modalidade)
        {
            var sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine("Bot_RPA - Registro de Execucao");
            sb.AppendLine($"Data/Hora : {data:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Modalidade: {modalidade}");
            sb.AppendLine($"Referencia: {identificador}");
            sb.AppendLine("============================================================");
            return sb.ToString();
        }
    }
}
