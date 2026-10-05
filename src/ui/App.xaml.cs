using System.Configuration;
using System.Data;
using System.Windows;
using BotRpaUI.Views;

namespace BotRpaUI;


/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Inicializa o Sistema Anti-Crash Guard (Triplo Handler + Watchdog)
        BotRpaUI.Core.AntiCrashGuard.Initialize();

        // Envia o ping de telemetria OTA para inventário no Firebase
        // (respeita o opt-out em Configurações → Telemetria Automática).
        BotRpaUI.Core.SettingsManager.Load();
        if (BotRpaUI.Core.SettingsManager.TelemetryEnabled)
        {
            _ = new BotRpaUI.Core.OtaUpdateManager().SendTelemetryPingAsync();
        }

        // Roda a varredura offline antes de abrir a interface
        BotRpaUI.Core.PreflightChecker.CheckAndRepair();

        // Sincronizacao automatica com a nuvem (pull no startup + periodico + ao reconectar)
        BotRpaUI.Core.SettingsManager.Load();
        BotRpaUI.Core.CloudAutoSyncService.Start();

        // Modernização: Forçar execução apenas no Windows 11 ou superior
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            MessageBox.Show(
                "O BotRpa RPA foi modernizado e agora requer o Windows 11 ou superior para rodar com toda a sua performance e recursos nativos.", 
                "Requisito de Sistema Incompatível", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            
            Current.Shutdown();
            return;
        }

        // Verifica se é a primeira execução
        string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
        string configDir = System.IO.Path.Combine(baseDir, "config");
        if (!System.IO.Directory.Exists(configDir))
        {
            System.IO.Directory.CreateDirectory(configDir);
        }

        string firstRunFile = System.IO.Path.Combine(configDir, "first_run.json");
        
        if (!System.IO.File.Exists(firstRunFile))
        {
            // Primeiro boot, mostra o Welcome Window
            var welcome = new WelcomeWindow();
            welcome.Show();
        }
        else
        {
            // Pula direto pro sistema
            var main = new MainWindow();
            main.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        KillAdbServer();
        base.OnExit(e);
    }

    private void KillAdbServer()
    {
        try
        {
            string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
            string adbExe = System.IO.Path.Combine(baseDir, "tools", "platform-tools", "adb.exe");
            
            if (System.IO.File.Exists(adbExe))
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbExe,
                    Arguments = "kill-server",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                
                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    process?.WaitForExit(5000);
                }
            }
        }
        catch {}
    }
}
