using System;
using System.IO;

namespace InnovPlayUI.Core
{
    public static class PathHelper
    {
        public static string GetRootDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Check if we are in Release Mode (Plug & Play) where 'config' or 'tools' is next to the executable
            if (Directory.Exists(Path.Combine(baseDir, "config")) && Directory.Exists(Path.Combine(baseDir, "tools")))
            {
                return baseDir;
            }
            
            // Fallback to Development Mode (IDE)
            return Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
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
    }
}
