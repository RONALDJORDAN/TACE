using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BotRpaUI.Core
{
    /// <summary>
    /// Sistema Anti-Crash Guard — Proteção Ativa Contra Fechamentos Inesperados e Falhas de Memória
    /// Autor: Jordan Araujo | BotRpa
    /// </summary>
    public static class AntiCrashGuard
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();
        private static DispatcherTimer? _memoryWatcherTimer;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                // 1. Intercepta Exceções de Threads de UI (Dispatcher)
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

                // 2. Intercepta Exceções Não Tratadas no AppDomain (Threads em Background)
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // 3. Intercepta Exceções Não Observadas em Tasks Assíncronas (async/await)
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // 4. Inicia Purgador Automático de Memória RAM (a cada 3 minutos)
                StartMemoryWatcher();

                _initialized = true;
                LogSystemMessage("[AntiCrashGuard] Sistema de proteção contra quedas ativado com sucesso!");
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Impede que o aplicativo feche sozinho
            e.Handled = true;
            LogCrash("UI_DISPATCHER", e.Exception);
            ShowAntiCrashToast("Aviso do Sistema Anti-Crash", "Uma falha de interface foi bloqueada e o sistema continuará rodando normalmente.");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash("APPDOMAIN", ex);
            }
            else
            {
                LogSystemMessage($"[AntiCrashGuard] Exceção não tratada capturada: {e.ExceptionObject}");
            }
            ShowAntiCrashToast("Proteção Anti-Crash", "Um erro interno foi isolado e registrado em log.");
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Marca a exceção de task assíncrona como observada para evitar fechamento do processo
            e.SetObserved();
            if (e.Exception != null)
            {
                LogCrash("ASYNC_TASK", e.Exception);
            }
        }

        /// <summary>
        /// Executa invocações C P/Invoke de forma totalmente segura contra falhas de memória
        /// </summary>
        public static bool ExecuteSafe(Action action, string contextName = "P/Invoke")
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                LogCrash($"SAFE_PINVOKE_{contextName}", ex);
                ShowAntiCrashToast("Proteção de DLL", $"Falha isolada na execução de {contextName}. O sistema permanece estável.");
                return false;
            }
        }

        /// <summary>
        /// Executa invocações C P/Invoke de retorno de forma segura
        /// </summary>
        public static T? ExecuteSafe<T>(Func<T> func, T? fallbackValue = default, string contextName = "P/Invoke")

        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                LogCrash($"SAFE_PINVOKE_{contextName}", ex);
                ShowAntiCrashToast("Proteção de DLL", $"Falha isolada na consulta de {contextName}.");
                return fallbackValue;
            }
        }

        private static void StartMemoryWatcher()
        {
            _memoryWatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(3)
            };
            _memoryWatcherTimer.Tick += (s, e) => PurgeMemory();
            _memoryWatcherTimer.Start();
        }

        public static void PurgeMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch {}
        }

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                string baseDir = PathHelper.GetRootDirectory();
                string logDir = Path.Combine(baseDir, "logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                string crashFile = Path.Combine(logDir, "crash.txt");
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                
                File.AppendAllText(crashFile, logContent);
            }
            catch {}
        }

        private static void LogSystemMessage(string msg)
        {
            try
            {
                string baseDir = PathHelper.GetRootDirectory();
                string logDir = Path.Combine(baseDir, "logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                string logFile = Path.Combine(logDir, "anti_crash.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
            }
            catch {}
        }

        private static void ShowAntiCrashToast(string title, string message)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch {}
        }
    }
}
