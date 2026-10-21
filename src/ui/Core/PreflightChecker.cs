using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Windows;

namespace BotRpaUI.Core
{
    public static class PreflightChecker
    {
        public static void CheckAndRepair()
        {
            try
            {
                // Caminho da pasta de PROGRAMAS (tools/ fica junto dos binarios)
                string baseDir = BotRpaUI.Core.PathHelper.GetInstallDirectory();
                
                string toolsDir = Path.Combine(baseDir, "tools", "reverse_tethering");
                string gnirehtetExe = Path.Combine(toolsDir, "gnirehtet.exe");
                string gnirehtetApk = Path.Combine(toolsDir, "gnirehtet.apk");

                string adbDir = Path.Combine(baseDir, "tools", "platform-tools");
                string adbExe = Path.Combine(adbDir, "adb.exe");

                if (!File.Exists(adbExe))
                {
                    MessageBox.Show(
                        "O BotRpa RPA detectou que o motor de comunicacao ADB nao esta instalado.\n" +
                        "Iniciando o download e a configuracao automatica do ADB oficial do Google. Por favor, aguarde.",
                        "Configuracao PnP do ADB",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DownloadAndExtractAdb(adbDir);

                    MessageBox.Show(
                        "Motor ADB configurado com sucesso! A comunicacao com os tablets esta pronta.",
                        "Configuracao Concluida",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                if (!File.Exists(gnirehtetExe) || !File.Exists(gnirehtetApk))
                {
                    // Falha de integridade detectada, iniciar auto-reparo offline!
                    MessageBox.Show(
                        "O sistema detectou que arquivos críticos de Reverse Tethering estão faltando.\n" +
                        "Iniciando Módulo de Auto-Reparo Offline. Por favor aguarde.",
                        "Verificação de Integridade",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    Directory.CreateDirectory(toolsDir);

                    // Extraindo o recurso embutido (Zip invisível compilado dentro do próprio .exe)
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = "BotRpaUI.Resources.gnirehtet.zip";

                    using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            string tempZip = Path.Combine(toolsDir, "temp_gnirehtet.zip");
                            using (var fileStream = File.Create(tempZip))
                            {
                                stream.CopyTo(fileStream);
                            }

                            // Extrair o zip
                            string extractDir = Path.Combine(toolsDir, "temp_extract");
                            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                            
                            ZipFile.ExtractToDirectory(tempZip, extractDir);

                            // O zip do gnirehtet tem uma subpasta "gnirehtet-rust-win64"
                            string innerDir = Path.Combine(extractDir, "gnirehtet-rust-win64");
                            if (Directory.Exists(innerDir))
                            {
                                foreach (var file in Directory.GetFiles(innerDir))
                                {
                                    string destFile = Path.Combine(toolsDir, Path.GetFileName(file));
                                    File.Copy(file, destFile, true);
                                }
                            }

                            // Limpeza do auto-reparo
                            File.Delete(tempZip);
                            Directory.Delete(extractDir, true);

                            MessageBox.Show(
                                "Auto-Reparo concluído com sucesso. Todas as dependências offline foram restauradas.",
                                "Integridade Restaurada",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Erro Crítico: Recurso de reparo offline não encontrado dentro do sistema.", "Falha Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro durante a verificação de integridade:\n{ex.Message}", "Erro de Boot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void DownloadAndExtractAdb(string adbDir)
        {
            try
            {
                string adbZipUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
                string tempZip = Path.Combine(adbDir, "platform-tools.zip");
                
                if (!Directory.Exists(adbDir))
                {
                    Directory.CreateDirectory(adbDir);
                }
                
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(2);
                    var responseBytes = client.GetByteArrayAsync(adbZipUrl).GetAwaiter().GetResult();
                    File.WriteAllBytes(tempZip, responseBytes);
                }

                string extractDir = Path.Combine(adbDir, "temp_extract");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                
                ZipFile.ExtractToDirectory(tempZip, extractDir);

                string innerDir = Path.Combine(extractDir, "platform-tools");
                if (Directory.Exists(innerDir))
                {
                    foreach (var file in Directory.GetFiles(innerDir))
                    {
                        string destFile = Path.Combine(adbDir, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }

                File.Delete(tempZip);
                Directory.Delete(extractDir, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha ao baixar/extrair o ADB do Google: {ex.Message}", ex);
            }
        }
    }
}
