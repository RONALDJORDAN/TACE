using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BotRpaUI.Core
{
    public class VersaoRemota
    {
        public string Versao { get; set; } = string.Empty;
        public string UrlDownload { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
        public bool Obrigatoria { get; set; } = false;
        public DateTime DataPublicacao { get; set; }
    }

    public class ResultadoAtualizacao
    {
        public bool Disponivel { get; set; }
        public string VersaoAtual { get; set; } = string.Empty;
        public string VersaoNova { get; set; } = string.Empty;
        public string UrlDownload { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
        public bool Obrigatoria { get; set; }
        public string Erro { get; set; } = string.Empty;
    }

    public static class AutoUpdateManager
    {
        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static string UrlVersaoRemota { get; set; } = "";
        public static string PastaDownloads { get; set; } = "";

        static AutoUpdateManager()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BotRPA-AutoUpdate/1.0");
        }

        public static void Configurar(string urlVersaoRemota, string pastaDownloads)
        {
            UrlVersaoRemota = urlVersaoRemota ?? string.Empty;
            PastaDownloads = string.IsNullOrWhiteSpace(pastaDownloads)
                ? Path.Combine(PathHelper.GetRootDirectory(), "downloads")
                : pastaDownloads;
            try { if (!Directory.Exists(PastaDownloads)) Directory.CreateDirectory(PastaDownloads); } catch { }
        }

        public static string ObterVersaoAtual()
        {
            try
            {
                string versionPath = Path.Combine(PathHelper.GetRootDirectory(), "config", "version.txt");
                if (File.Exists(versionPath))
                {
                    var linhas = File.ReadAllLines(versionPath);
                    if (linhas.Length > 0 && !string.IsNullOrWhiteSpace(linhas[0]))
                        return linhas[0].Trim();
                }
            }
            catch { }

            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var nome = asm.GetName().Version;
                if (nome != null) return $"{nome.Major}.{nome.Minor}.{nome.Build}.{nome.Revision}";
            }
            catch { }

            return "0.0.0.0";
        }

        public static async Task<ResultadoAtualizacao> VerificarAtualizacaoAsync()
        {
            var resultado = new ResultadoAtualizacao
            {
                VersaoAtual = ObterVersaoAtual(),
                Disponivel = false
            };

            try
            {
                if (string.IsNullOrWhiteSpace(UrlVersaoRemota))
                {
                    resultado.Erro = "URL de versao remota nao configurada.";
                    return resultado;
                }

                var json = await _httpClient.GetStringAsync(UrlVersaoRemota);
                var remota = JsonSerializer.Deserialize<VersaoRemota>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (remota == null)
                {
                    resultado.Erro = "Resposta remota invalida.";
                    return resultado;
                }

                resultado.VersaoNova = remota.Versao ?? string.Empty;
                resultado.UrlDownload = remota.UrlDownload ?? string.Empty;
                resultado.Notas = remota.Notas ?? string.Empty;
                resultado.Obrigatoria = remota.Obrigatoria;

                if (VersaoMaiorQue(resultado.VersaoNova, resultado.VersaoAtual))
                {
                    resultado.Disponivel = true;
                }
            }
            catch (Exception ex)
            {
                resultado.Erro = ex.Message;
            }

            return resultado;
        }

        public static async Task<string> BaixarInstaladorAsync(string urlDownload, IProgress<double>? progresso = null)
        {
            try
            {
                if (!Directory.Exists(PastaDownloads))
                    Directory.CreateDirectory(PastaDownloads);

                string nomeArquivo = Path.GetFileName(urlDownload);
                if (string.IsNullOrWhiteSpace(nomeArquivo))
                    nomeArquivo = $"BotRPA_Instalador_{ObterVersaoAtual()}.exe";

                string destino = Path.Combine(PastaDownloads, nomeArquivo);

                using (var response = await _httpClient.GetAsync(urlDownload, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long total = response.Content.Headers.ContentLength ?? 0;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[81920];
                        long lido = 0;
                        int bytesLidos;
                        while ((bytesLidos = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesLidos);
                            lido += bytesLidos;
                            if (total > 0 && progresso != null)
                                progresso.Report((double)lido / total * 100.0);
                        }
                    }
                }

                return destino;
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha ao baixar instalador: {ex.Message}", ex);
            }
        }

        private static bool VersaoMaiorQue(string nova, string atual)
        {
            try
            {
                var partesNova = nova.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                var partesAtual = atual.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                int maxLen = Math.Max(partesNova.Length, partesAtual.Length);
                for (int i = 0; i < maxLen; i++)
                {
                    int n = i < partesNova.Length ? int.Parse(partesNova[i]) : 0;
                    int a = i < partesAtual.Length ? int.Parse(partesAtual[i]) : 0;
                    if (n > a) return true;
                    if (n < a) return false;
                }
                return false;
            }
            catch
            {
                return string.Compare(nova, atual, StringComparison.Ordinal) > 0;
            }
        }
    }
}
