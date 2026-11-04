using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TACEUI.Core
{
    public class BotLearnBackup
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public string TamanhoStr { get; set; } = string.Empty;
    }

    public static class BotLearnBackupManager
    {
        private const int MAX_BACKUPS = 3;

        public static string GetDataDirectory()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            // O binário C (BotEngine) e o C# procuram pela pasta 'data' relativa ao executável.
            string dataDir = Path.Combine(basePath, "data");
            
            if (!Directory.Exists(dataDir))
            {
                // Fallback de desenvolvimento (sobe dois níveis a partir do bin/Debug)
                string devDir = Path.Combine(basePath, "..", "..", "data");
                if (Directory.Exists(devDir))
                {
                    dataDir = Path.GetFullPath(devDir);
                }
                else
                {
                    Directory.CreateDirectory(dataDir);
                }
            }
            // Migracao automatica do legado Inno_RPA (uma unica vez por maquina).
            try
            {
                string legacy = Path.Combine(dataDir, "inno_learn.json");
                string current = Path.Combine(dataDir, "bot_learn.json");
                if (!File.Exists(current) && File.Exists(legacy))
                    File.Move(legacy, current);
            }
            catch { }
            return dataDir;
        }

        public static string GetBackupDirectory()
        {
            string bkpDir = Path.Combine(GetDataDirectory(), "backups");
            if (!Directory.Exists(bkpDir))
            {
                Directory.CreateDirectory(bkpDir);
            }
            return bkpDir;
        }

        public static void ExecutarRotinaDiaria()
        {
            try
            {
                string dataDir = GetDataDirectory();
                string sourceFile = Path.Combine(dataDir, "bot_learn.json");
                if (!File.Exists(sourceFile)) return; // Sem DB original, sem backup

                string bkpDir = GetBackupDirectory();
                string dateStr = DateTime.Now.ToString("yyyyMMdd");
                string destFile = Path.Combine(bkpDir, $"bot_learn_bkp_{dateStr}.json");

                // Só faz cópia se não houver um backup para o dia atual
                if (!File.Exists(destFile))
                {
                    File.Copy(sourceFile, destFile, true);
                }

                // Efetua a limpeza de rotatividade
                PruneOldBackups(bkpDir);
            }
            catch (Exception)
            {
                // Processo autônomo, não trava UI em caso de Access Violation
            }
        }

        private static void PruneOldBackups(string bkpDir)
        {
            var files = new DirectoryInfo(bkpDir).GetFiles("bot_learn_bkp_*.json");
            if (files.Length > MAX_BACKUPS)
            {
                // Ordenação decrescente: índice 0, 1 e 2 são os mais novos
                var orderedFiles = files.OrderByDescending(f => f.CreationTime).ToList();
                
                // Remove do índice 3 em diante (mantém max 3)
                for (int i = MAX_BACKUPS; i < orderedFiles.Count; i++)
                {
                    try
                    {
                        orderedFiles[i].Delete();
                    }
                    catch { }
                }
            }
        }

        public static List<BotLearnBackup> ObterBackupsDisponiveis()
        {
            var result = new List<BotLearnBackup>();
            try
            {
                string bkpDir = GetBackupDirectory();
                var files = new DirectoryInfo(bkpDir).GetFiles("bot_learn_bkp_*.json");
                
                foreach (var f in files.OrderByDescending(x => x.CreationTime))
                {
                    double kb = f.Length / 1024.0;
                    result.Add(new BotLearnBackup
                    {
                        FileName = f.Name,
                        FullPath = f.FullName,
                        DataCriacao = f.CreationTime,
                        TamanhoStr = $"{kb:F1} KB"
                    });
                }
            }
            catch { }
            return result;
        }

        public static bool RestaurarBackup(string bkpFullPath)
        {
            try
            {
                if (!File.Exists(bkpFullPath)) return false;
                string destFile = Path.Combine(GetDataDirectory(), "bot_learn.json");
                File.Copy(bkpFullPath, destFile, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
