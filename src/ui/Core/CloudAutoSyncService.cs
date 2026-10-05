using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace BotRpaUI.Core
{
    /// <summary>
    /// Mantém o banco local de aparelhos sincronizado com a nuvem InnovStock
    /// sempre que houver internet: pull no startup (se ativado), timer periódico
    /// e disparo ao reconectar a rede. Tudo em background, sem travar a UI e sem
    /// sobrepor execuções. Após o pull, devolve à nuvem (PUT individual) apenas
    /// as linhas tocadas — sem clobber em massa e sem loop.
    /// </summary>
    public static class CloudAutoSyncService
    {
        private static Timer? _timer;
        private static int _running = 0;
        private static DateTime _lastRun = DateTime.MinValue;
        private static readonly object _lock = new object();

        public static void Start()
        {
            lock (_lock)
            {
                try { NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged; }
                catch { }
                RestartTimer();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (SettingsManager.CloudPullAuto) await SyncOnceAsync();
                }
                catch { }
            });
        }

        public static void RestartTimer()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                if (!SettingsManager.CloudAutoSync) return;
                int min = SettingsManager.CloudSyncMinutes;
                if (min < 1) min = 1;
                if (min > 1440) min = 1440;
                _timer = new Timer(_ => { _ = SyncOnceAsync(); },
                    null, TimeSpan.FromMinutes(min), TimeSpan.FromMinutes(min));
            }
        }

        private static void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            try
            {
                if (!e.IsAvailable) return;
                if (!SettingsManager.CloudAutoSync) return;
                if ((DateTime.UtcNow - _lastRun).TotalMinutes < 1) return;
                _ = SyncOnceAsync();
            }
            catch { }
        }

        public static async Task<(int importados, int atualizados)> SyncOnceAsync()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return (0, 0);
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable()) return (0, 0);
                var (imp, atu, afetados) = await SQLiteDatabaseManager.BaixarDoInnovStockAsync();
                _lastRun = DateTime.UtcNow;
                foreach (var serial in afetados)
                {
                    try
                    {
                        var d = SQLiteDatabaseManager.BuscarPorSerial(serial);
                        if (d != null) await SQLiteDatabaseManager.SincronizarComInnovStockAsync(d);
                    }
                    catch { }
                }
                try { await SQLiteDatabaseManager.EnviarLocaisFaltantesParaNuvemAsync(); }
                catch { }
                return (imp, atu);
            }
            catch { return (0, 0); }
            finally { Interlocked.Exchange(ref _running, 0); }
        }
    }
}
