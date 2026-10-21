using System;
using System.IO;

namespace BotRpaUI.Core
{
    public static class PathHelper
    {
        /// <summary>
        /// Variavel de ambiente que aponta a pasta de DADOS do usuario.
        /// Definida pelo instalador (machine-wide) quando o programa e
        /// instalado em "C:\Program Files" (sem permissao de escrita).
        /// </summary>
        public const string EnvDataDir = "BOT_RPA_DATA_DIR";

        private static string? _dataDirCache;

        /// <summary>
        /// Pasta onde ficam os PROGRAMAS (binarios, tools, scripts).
        /// Sempre a pasta de instalacao (ex.: C:\Program Files\Bot_RPA).
        /// </summary>
        public static string GetInstallDirectory()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? dir = new DirectoryInfo(current);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "tools")) && Directory.Exists(Path.Combine(dir.FullName, "config")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Pasta onde ficam os DADOS do usuario (banco de dispositivos, cofre de
        /// credenciais, pacotes dos municipios, logs e registros).
        ///
        /// Resolucao:
        ///   1) Variavel de ambiente BOT_RPA_DATA_DIR (definida pelo instalador);
        ///   2) A propria pasta de instalacao, se for gravavel (modo portatil);
        ///   3) %ProgramData%\Bot_RPA (instalacao em Program Files).
        /// </summary>
        public static string GetRootDirectory()
        {
            if (!string.IsNullOrEmpty(_dataDirCache)) return _dataDirCache;

            string resolved;

            // 1) Variavel de ambiente (instalador)
            string? env = Environment.GetEnvironmentVariable(EnvDataDir);
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            {
                resolved = env;
            }
            else
            {
                string install = GetInstallDirectory();
                // 2) Modo portatil: a propria pasta de instalacao, se gravavel
                if (IsDirectoryWritable(install))
                {
                    resolved = install;
                }
                else
                {
                    // 3) Instalacao em Program Files: dados em area machine-wide
                    string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    if (string.IsNullOrWhiteSpace(programData))
                        programData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    resolved = Path.Combine(programData, "Bot_RPA");
                }
            }

            try { Directory.CreateDirectory(resolved); } catch { }
            _dataDirCache = resolved;
            return resolved;
        }

        /// <summary>Testa se e possivel gravar um arquivo na pasta informada.</summary>
        private static bool IsDirectoryWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".botrpa_write_test.tmp");
                using (var fs = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.WriteByte(0);
                }
                try { File.Delete(probe); } catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static string RemoveAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
            string normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(System.Text.NormalizationForm.FormC);
        }

        public static string SanitizeFolderName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return string.Join("_", text.Split(Path.GetInvalidFileNameChars()));
        }

        public static string SanitizeFolderNameAscii(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string noAccents = RemoveAccents(text);
            return string.Join("_", noAccents.Split(Path.GetInvalidFileNameChars()));
        }

        public static string GetDynamicApkPath(string municipio)
        {
            if (string.IsNullOrWhiteSpace(municipio)) return "";
            string root = GetRootDirectory();
            string clean = SanitizeFolderName(municipio);
            string cleanAscii = SanitizeFolderNameAscii(municipio);

            string[] candidateDirs = new string[] {
                Path.Combine(root, "data", "output", "arquivos", "apks", clean),
                Path.Combine(root, "data", "output", "arquivos", "apks", cleanAscii),
                Path.Combine(root, "Arquivos", municipio),
                Path.Combine(root, "Arquivos", clean),
                Path.Combine(root, "Arquivos", cleanAscii),
                Path.Combine(root, "data", "input", "municipios", municipio),
                Path.Combine(root, "data", "input", "municipios", clean),
                Path.Combine(root, "data", "input", "municipios", cleanAscii),
                Path.Combine(root, "data", "input", "apps")
            };

            foreach (var apkDir in candidateDirs)
            {
                if (Directory.Exists(apkDir))
                {
                    var files = Directory.GetFiles(apkDir, "*.apk");
                    if (files.Length > 0) return files[0];
                }
            }
            return "";
        }

        public static string GetDynamicPacotePath(string municipio)
        {
            if (string.IsNullOrWhiteSpace(municipio)) return "";
            string root = GetRootDirectory();
            string clean = SanitizeFolderName(municipio);
            string cleanAscii = SanitizeFolderNameAscii(municipio);

            string[] candidateDirs = new string[] {
                Path.Combine(root, "data", "output", "arquivos", "pacotes", clean),
                Path.Combine(root, "data", "output", "arquivos", "pacotes", cleanAscii),
                Path.Combine(root, "Arquivos", municipio),
                Path.Combine(root, "Arquivos", clean),
                Path.Combine(root, "Arquivos", cleanAscii),
                Path.Combine(root, "data", "input", "municipios", municipio),
                Path.Combine(root, "data", "input", "municipios", clean),
                Path.Combine(root, "data", "input", "municipios", cleanAscii)
            };

            foreach (var pacoteDir in candidateDirs)
            {
                if (Directory.Exists(pacoteDir))
                {
                    var files = Directory.GetFiles(pacoteDir, "*.zip");
                    if (files.Length > 0) return files[0];
                }
            }
            return "";
        }

        public static void DeleteMunicipioFolders(string municipio)
        {
            if (string.IsNullOrWhiteSpace(municipio)) return;
            string root = GetRootDirectory();
            string clean = SanitizeFolderName(municipio);
            string cleanAscii = SanitizeFolderNameAscii(municipio);

            string[] candidateDirs = new string[] {
                Path.Combine(root, "data", "output", "arquivos", "apks", clean),
                Path.Combine(root, "data", "output", "arquivos", "apks", cleanAscii),
                Path.Combine(root, "data", "output", "arquivos", "pacotes", clean),
                Path.Combine(root, "data", "output", "arquivos", "pacotes", cleanAscii),
                Path.Combine(root, "Arquivos", municipio),
                Path.Combine(root, "Arquivos", clean),
                Path.Combine(root, "Arquivos", cleanAscii),
                Path.Combine(root, "data", "input", "municipios", municipio),
                Path.Combine(root, "data", "input", "municipios", clean),
                Path.Combine(root, "data", "input", "municipios", cleanAscii)
            };

            foreach (var dir in candidateDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch { }
            }
        }
    }
}
