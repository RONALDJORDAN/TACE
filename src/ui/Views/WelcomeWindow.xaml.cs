using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BotRpaUI
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void BtnIniciar_Click(object sender, RoutedEventArgs e)
        {
            BtnIniciar.Visibility = Visibility.Collapsed;
            PanelLoading.Visibility = Visibility.Visible;

            // Simula o tempo de checagem para efeito visual de "trabalhando"
            LblStatus.Text = "Verificando dependências locais...";
            await Task.Delay(1500);

            try
            {
                // Roda a checagem que valida extração de arquivos embutidos (ex: gnirehtet)
                BotRpaUI.Core.PreflightChecker.CheckAndRepair();

                LblStatus.Text = "Verificando Engine de Banco de Dados...";
                await Task.Delay(1000);

                // Inicializa o BD (Isso cria o arquivo .db se não existir)
                BotRpaUI.Core.SQLiteDatabaseManager.InitializeDatabase();

                LblStatus.Text = "Tudo pronto! Carregando Sistema...";
                await Task.Delay(1000);

                // Grava a flag de primeiro boot
                string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                string configDir = Path.Combine(baseDir, "config");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string firstRunFile = Path.Combine(configDir, "first_run.json");
                File.WriteAllText(firstRunFile, "{ \"configured\": true, \"date\": \"" + DateTime.Now.ToString("O") + "\" }");

                // Abre a janela principal
                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro durante a configuração inicial:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnIniciar.Visibility = Visibility.Visible;
                PanelLoading.Visibility = Visibility.Collapsed;
            }
        }
    }
}
