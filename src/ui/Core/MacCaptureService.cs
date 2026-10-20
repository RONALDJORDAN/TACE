using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BotRpaUI.Core
{
    /// <summary>
    /// Servico de captura automatica do endereco MAC Wi-Fi dos tablets.
    ///
    /// O MAC so e legivel com a interface wlan0 ativa. Por isso o motor C
    /// (adb_get_mac) reproduz o mesmo ciclo da ferramenta de captura:
    ///   1) Tenta a leitura direta (cache do supplicant / wlan0 ja ativa);
    ///   2) Se falhar, LIGA o Wi-Fi, espera a interface subir, extrai o
    ///      endereco e RESTAURA o estado original (Wi-Fi desligado, como
    ///      o pipeline F1/F2/F5 exige para o tunel Gnirehtet funcionar).
    ///
    /// O servico registra o MAC no banco de dados (tabela Dispositivos)
    /// assim que o aparelho e detectado, sem acao manual do operador.
    /// </summary>
    public static class MacCaptureService
    {
        // StringBuilder (LPStr) e obrigatorio aqui: com char[] o CLR nao copia
        // o buffer nativo de volta para o managed, e o MAC chegaria vazio.
        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int Engine_CapturarMAC(string serial, StringBuilder out_mac, int max_len);

        /// <summary>Serials cuja captura ja foi tentada + quando (para retry espacado).</summary>
        private static readonly Dictionary<string, DateTime> _ultimaTentativa = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>Intervalo minimo entre duas tentativas de captura do mesmo aparelho.</summary>
        private static readonly TimeSpan _intervaloRetry = TimeSpan.FromMinutes(5);

        /// <summary>MAC invalido: vazio, "N/A", "---" ou o placeholder do Android 10+.</summary>
        public static bool MacValido(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return false;
            if (mac == "N/A" || mac == "---" || mac == "N/A (Seguranca)") return false;
            if (mac == "02:00:00:00:00:00") return false;

            // Formato XX:XX:XX:XX:XX:XX
            if (mac.Length != 17) return false;
            for (int i = 0; i < mac.Length; i++)
            {
                bool ehSeparador = (i % 3) == 2;
                if (ehSeparador)
                {
                    if (mac[i] != ':' && mac[i] != '-') return false;
                }
                else
                {
                    char c = mac[i];
                    bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                    if (!hex) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Extrai o MAC de um aparelho via motor C (ciclo com toggle de Wi-Fi).
        /// Retorna string vazia se nao foi possivel obter.
        /// </summary>
        public static string CapturarMac(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return string.Empty;

            try
            {
                var buffer = new StringBuilder(64);
                int ok = Engine_CapturarMAC(serial, buffer, buffer.Capacity);
                if (ok != 1) return string.Empty;

                string mac = buffer.ToString().Trim();
                return MacValido(mac) ? mac : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Garante que o aparelho tenha MAC registrado no banco.
        /// Se ja existir MAC valido, nao faz nada (custo zero).
        /// Caso contrario, extrai via motor C (ciclo com toggle de Wi-Fi) e grava.
        /// </summary>
        public static void GarantirMacRegistrado(string serial, string modelo, string? macConhecido = null)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            if (serial == "unknown" || serial.StartsWith("?") || serial == "Pendente") return;

            // MAC ja veio no refresh (batched do motor) -> nada a fazer.
            if (MacValido(macConhecido)) return;

            // Espaco as tentativas: o ciclo de Wi-Fi e caro (~6s) e nao deve
            // repetir a cada refresh de 5 segundos.
            lock (_lock)
            {
                if (_ultimaTentativa.TryGetValue(serial, out DateTime ultima) &&
                    (DateTime.Now - ultima) < _intervaloRetry)
                {
                    return;
                }
                _ultimaTentativa[serial] = DateTime.Now;
            }

            try
            {
                string mac = CapturarMac(serial);
                if (!MacValido(mac)) return;

                SQLiteDatabaseManager.RegistrarOuAtualizarMAC(serial, modelo, mac);
            }
            catch { }
        }

        /// <summary>
        /// Varre os aparelhos conectados e garante o MAC de cada um.
        /// Executado em background a cada ciclo do monitor.
        /// </summary>
        public static void ProcessarConectados(IEnumerable<(string Serial, string Modelo, string Mac)> dispositivos)
        {
            if (dispositivos == null) return;

            foreach (var (serial, modelo, mac) in dispositivos)
            {
                if (string.IsNullOrWhiteSpace(serial)) continue;

                // Ja temos MAC valido (veio no batched do motor): nada a fazer.
                if (MacValido(mac)) continue;

                GarantirMacRegistrado(serial, modelo, mac);
            }
        }

        /// <summary>Limpa o cache de seriais processados (usado ao recarregar a interface).</summary>
        public static void ResetarCache()
        {
            lock (_lock)
            {
                _ultimaTentativa.Clear();
            }
        }
    }
}
