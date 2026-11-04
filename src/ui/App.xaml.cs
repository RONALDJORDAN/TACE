using System.Configuration;
using System.Data;
using System.Windows;
using TACEUI.Views;

namespace TACEUI;


/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Garante o encerramento do ADB tambem em logoff/shutdown e saidas nao tratadas
        AppDomain.CurrentDomain.ProcessExit += (sender, args) => TACEUI.Core.AdbServerManager.Encerrar();
        SessionEnding += (sender, args) => TACEUI.Core.AdbServerManager.Encerrar();

        // Inicializa o Sistema Anti-Crash Guard (Triplo Handler + Watchdog)
        TACEUI.Core.AntiCrashGuard.Initialize();

        // Roda a varredura offline antes de abrir a interface
        TACEUI.Core.PreflightChecker.CheckAndRepair();

        // Modernização: Forçar execução apenas no Windows 11 ou superior
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            MessageBox.Show(
                "O TACE RPA foi modernizado e agora requer o Windows 11 ou superior para rodar com toda a sua performance e recursos nativos.", 
                "Requisito de Sistema Incompatível", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            
            Current.Shutdown();
            return;
        }

        // Verifica se é a primeira execução
        string baseDir = TACEUI.Core.PathHelper.GetRootDirectory();
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
        TACEUI.Core.AdbServerManager.Encerrar();
        base.OnExit(e);
    }
}
