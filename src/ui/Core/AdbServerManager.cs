using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TACEUI.Core
{
    /// <summary>
    /// Encerra o servidor ADB iniciado pelo aplicativo.
    /// O binario fica na pasta de INSTALACAO (tools/platform-tools), que pode ser
    /// diferente da pasta de DADOS (TACE_DATA_DIR) em instalacoes no Program Files.
    /// </summary>
    public static class AdbServerManager
    {
        private static readonly object _lock = new object();
        private static bool _encerrado;

        public static void Encerrar()
        {
            lock (_lock)
            {
                if (_encerrado) return;
                _encerrado = true;
            }

            var candidatos = new List<string>(ObterCandidatos());

            foreach (string adbExe in candidatos)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = adbExe,
                        Arguments = "kill-server",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        if (process == null) continue;
                        if (!process.WaitForExit(5000))
                        {
                            try { process.Kill(true); } catch { }
                        }
                    }
                }
                catch { }
            }

            System.Threading.Thread.Sleep(300);
            EncerrarProcessosResiduais(candidatos);
        }

        private static void EncerrarProcessosResiduais(List<string> candidatos)
        {
            var caminhos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidato in candidatos)
            {
                try
                {
                    if (File.Exists(candidato))
                        caminhos.Add(Path.GetFullPath(candidato));
                }
                catch { }
            }

            if (caminhos.Count == 0) return;

            foreach (Process processo in Process.GetProcessesByName("adb"))
            {
                try
                {
                    string? caminho = processo.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(caminho) && caminhos.Contains(Path.GetFullPath(caminho)))
                        processo.Kill(true);
                }
                catch { }
                finally
                {
                    processo.Dispose();
                }
            }
        }

        private static IEnumerable<string> ObterCandidatos()
        {
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in new[]
            {
                PathHelper.GetInstallDirectory(),
                PathHelper.GetRootDirectory(),
                AppDomain.CurrentDomain.BaseDirectory
            })
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;

                string adbExe = Path.Combine(dir, "tools", "platform-tools", "adb.exe");
                if (File.Exists(adbExe) && vistos.Add(Path.GetFullPath(adbExe)))
                    yield return adbExe;
            }

            // Modo desenvolvimento: exe em data\output; sobe ate a raiz do repositorio.
            DirectoryInfo? atual = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 4 && atual != null; i++, atual = atual.Parent)
            {
                string adbExe = Path.Combine(atual.FullName, "tools", "platform-tools", "adb.exe");
                if (File.Exists(adbExe) && vistos.Add(Path.GetFullPath(adbExe)))
                    yield return adbExe;
            }

            if (vistos.Count == 0)
                yield return "adb.exe";
        }
    }
}
