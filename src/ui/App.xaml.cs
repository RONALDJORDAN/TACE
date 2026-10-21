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

        // Garante o encerramento do ADB tambem em logoff/shutdown e saidas nao tratadas
        AppDomain.CurrentDomain.ProcessExit += (sender, args) => BotRpaUI.Core.AdbServerManager.Encerrar();
        SessionEnding += (sender, args) => BotRpaUI.Core.AdbServerManager.Encerrar();

        // Inicializa o Sistema Anti-Crash Guard (Triplo Handler + Watchdog)
        BotRpaUI.Core.AntiCrashGuard.Initialize();

        // Roda a varredura offline antes de abrir a interface
        BotRpaUI.Core.PreflightChecker.CheckAndRepair();

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
        BotRpaUI.Core.AdbServerManager.Encerrar();
        base.OnExit(e);
    }
}
