using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Collections.ObjectModel;
using TACEUI.Core;

namespace TACEUI.Views
{
    public partial class ConfigView : UserControl
    {
        private bool _isInitializingSettings = true;

        public ConfigView()
        {
            InitializeComponent();
            TxtVersaoSistema.Text = $"v{AppInfo.CurrentVersion}";
            InitializeSpeedTestUI();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                _isInitializingSettings = true;
                SettingsManager.Load();
                
                ToggleAiLearn.IsChecked = SettingsManager.AiLearnActive;
                UpdateAiModeBadge(SettingsManager.AiLearnActive);
                ToggleShowDisconnected.IsChecked = SettingsManager.ShowDisconnectedDevices;

                ToggleDeviceVision7.IsChecked = SettingsManager.EnableDeviceVision7;
                ToggleDeviceTabMulti.IsChecked = SettingsManager.EnableDeviceTabMulti;
                ToggleDeviceNokiaT20.IsChecked = SettingsManager.EnableDeviceNokiaT20;

                UpdateInterfaceSelectionUI(SettingsManager.UsbMonitorInterface);

                TxtGoogleEmail.Text = SettingsManager.GoogleAccountEmail;
                PwdGoogleSenha.Clear();
                AtualizarStatusGoogleConta();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigView] Erro ao carregar configurações: {ex.Message}");
            }
            finally
            {
                _isInitializingSettings = false;
            }
        }

        private void AtualizarStatusGoogleConta()
        {
            if (TxtGoogleStatus == null)
                return;

            if (SettingsManager.HasGoogleAccountConfigured)
            {
                DefinirStatusGoogle($"Conta configurada: {SettingsManager.GoogleAccountEmail}", "#4EDEA3");
            }
            else
            {
                DefinirStatusGoogle("Nenhuma conta Google configurada.", "#85859E");
            }
        }

        private void DefinirStatusGoogle(string mensagem, string corHex)
        {
            if (TxtGoogleStatus == null)
                return;

            TxtGoogleStatus.Text = mensagem;
            TxtGoogleStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(corHex));
        }

        private void BtnSalvarGoogle_Click(object sender, RoutedEventArgs e)
        {
            string email = (TxtGoogleEmail.Text ?? "").Trim();
            string senha = PwdGoogleSenha.Password;

            if (email.Length == 0 || email.Length > 200 || !email.Contains("@"))
            {
                DefinirStatusGoogle("Informe um e-mail Google válido.", "#FF8080");
                return;
            }

            if (string.IsNullOrEmpty(senha))
            {
                if (!SettingsManager.HasGoogleAccountConfigured)
                {
                    DefinirStatusGoogle("Informe a senha da conta Google.", "#FF8080");
                    return;
                }

                if (!string.Equals(email, SettingsManager.GoogleAccountEmail, StringComparison.OrdinalIgnoreCase))
                {
                    DefinirStatusGoogle("Informe a senha ao alterar o e-mail.", "#FF8080");
                    return;
                }

                DefinirStatusGoogle("Senha mantida. Conta Google já configurada.", "#4EDEA3");
                return;
            }

            if (senha.Length > 200)
            {
                DefinirStatusGoogle("A senha informada é muito longa.", "#FF8080");
                return;
            }

            if (!SettingsManager.SetGoogleAccount(email, senha))
            {
                DefinirStatusGoogle("Não foi possível salvar a conta Google.", "#FF8080");
                return;
            }

            PwdGoogleSenha.Clear();
            TxtGoogleEmail.Text = SettingsManager.GoogleAccountEmail;
            DefinirStatusGoogle("Conta salva. Senha protegida pelo Windows.", "#4EDEA3");
            senha = string.Empty;
        }

        private void SaveSettings()
        {
            if (_isInitializingSettings) return;

            try
            {
                SettingsManager.AiLearnActive = ToggleAiLearn.IsChecked == true;
                UpdateAiModeBadge(SettingsManager.AiLearnActive);
                try { Engine_Learn_SetActiveMode(SettingsManager.AiLearnActive ? 1 : 0); } catch { }
                SettingsManager.ShowDisconnectedDevices = ToggleShowDisconnected.IsChecked == true;

                SettingsManager.EnableDeviceVision7 = ToggleDeviceVision7.IsChecked == true;
                SettingsManager.EnableDeviceTabMulti = ToggleDeviceTabMulti.IsChecked == true;
                SettingsManager.EnableDeviceNokiaT20 = ToggleDeviceNokiaT20.IsChecked == true;

                SettingsManager.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigView] Erro ao salvar configurações: {ex.Message}");
            }
        }

        private void InitializeSpeedTestUI()
        {
            var items = new ObservableCollection<SpeedScoreItem>();
            for (int i = 0; i < 10; i++)
            {
                items.Add(new SpeedScoreItem { Color = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A40")) });
            }
            SpeedScoreItems.ItemsSource = items;
        }

        private void UpdateInterfaceSelectionUI(string selectedInterface)
        {
            bool isPro = selectedInterface.Equals("Pro", StringComparison.OrdinalIgnoreCase);

            if (isPro)
            {
                CardInterfacePro.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18182A"));
                CardInterfacePro.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF007F"));
                CardInterfacePro.BorderThickness = new Thickness(2);
                BadgeProActive.Visibility = Visibility.Visible;

                CardInterfaceClassic.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E0E18"));
                CardInterfaceClassic.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#23233A"));
                CardInterfaceClassic.BorderThickness = new Thickness(1);
                BadgeClassicActive.Visibility = Visibility.Collapsed;

                TxtInterfaceActiveIndicator.Text = "Modo Atual: Monitor Pro";
                TxtInterfaceActiveIndicator.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF007F"));
            }
            else
            {
                CardInterfaceClassic.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18182A"));
                CardInterfaceClassic.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00E5FF"));
                CardInterfaceClassic.BorderThickness = new Thickness(2);
                BadgeClassicActive.Visibility = Visibility.Visible;

                CardInterfacePro.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E0E18"));
                CardInterfacePro.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#23233A"));
                CardInterfacePro.BorderThickness = new Thickness(1);
                BadgeProActive.Visibility = Visibility.Collapsed;

                TxtInterfaceActiveIndicator.Text = "Modo Atual: Clássico";
                TxtInterfaceActiveIndicator.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00E5FF"));
            }
        }

        private void CardInterfaceClassic_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SettingsManager.UsbMonitorInterface = "Classic";
            UpdateInterfaceSelectionUI("Classic");
        }

        private void CardInterfacePro_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SettingsManager.UsbMonitorInterface = "Pro";
            UpdateInterfaceSelectionUI("Pro");
        }

        private void BtnSubGeral_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubGeral, PanelGeral);
        }

        private void BtnSubRede_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubRede, PanelRede);
        }

        private void BtnSubAparelhos_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubAparelhos, PanelAparelhos);
        }

        private void BtnSubDados_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubDados, PanelDados);
        }

        private void BtnSubBibliotecas_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubBibliotecas, PanelBibliotecas);
        }

        private void SwitchSubmodule(System.Windows.Controls.Button activeButton, FrameworkElement activePanel)
        {
            var brushNormal = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A1A1AA"));
            var bgNormal = System.Windows.Media.Brushes.Transparent;
            var brushActive = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EDEA3"));
            var bgActive = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A40"));

            // Resetar botoes
            BtnSubGeral.Foreground = brushNormal;
            BtnSubGeral.Background = bgNormal;
            BtnSubRede.Foreground = brushNormal;
            BtnSubRede.Background = bgNormal;
            BtnSubAparelhos.Foreground = brushNormal;
            BtnSubAparelhos.Background = bgNormal;
            BtnSubDados.Foreground = brushNormal;
            BtnSubDados.Background = bgNormal;
            BtnSubBibliotecas.Foreground = brushNormal;
            BtnSubBibliotecas.Background = bgNormal;

            // Ativar botao clicado
            activeButton.Foreground = brushActive;
            activeButton.Background = bgActive;

            // Ocultar todos os paineis
            PanelGeral.Visibility = Visibility.Collapsed;
            PanelRede.Visibility = Visibility.Collapsed;
            PanelAparelhos.Visibility = Visibility.Collapsed;
            PanelDados.Visibility = Visibility.Collapsed;
            PanelBibliotecas.Visibility = Visibility.Collapsed;

            // Mostrar o painel ativo
            activePanel.Visibility = Visibility.Visible;
        }

        [System.Runtime.InteropServices.DllImport("BotEngine.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private static extern int Engine_Learn_SetActiveMode(int active);

        private void UpdateAiModeBadge(bool active)
        {
            try
            {
                if (TxtAiMode == null || BadgeAiMode == null) return;
                if (active)
                {
                    TxtAiMode.Text = "ADAPTATIVO";
                    TxtAiMode.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#58A6FF"));
                    BadgeAiMode.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0D1B2A"));
                    BadgeAiMode.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F6FEB"));
                }
                else
                {
                    TxtAiMode.Text = "DETERMINÍSTICO";
                    TxtAiMode.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3FB950"));
                    BadgeAiMode.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F2A1A"));
                    BadgeAiMode.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3FB950"));
                }
            }
            catch { }
        }

        private void ToggleAiLearn_Checked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleAiLearn_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleShowDisconnected_Checked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleShowDisconnected_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleDevice_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializingSettings) return;

            if (ToggleDeviceVision7.IsChecked != true
                && ToggleDeviceTabMulti.IsChecked != true
                && ToggleDeviceNokiaT20.IsChecked != true)
            {
                MessageBox.Show(
                    "Mantenha pelo menos um modelo de tablet ativo. Sem nenhum modelo habilitado, o monitor USB não exibirá dispositivos.",
                    "Modelos suportados", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (sender is CheckBox origem)
                {
                    _isInitializingSettings = true;
                    origem.IsChecked = true;
                    _isInitializingSettings = false;
                }
                return;
            }

            SaveSettings();
        }

        private static void AdicionarArquivoAoZip(ZipArchive archive, string caminhoCompleto, string nomeEntrada)
        {
            ZipArchiveEntry entry = archive.CreateEntry(nomeEntrada, CompressionLevel.Optimal);
            using (var origem = new FileStream(caminhoCompleto, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (Stream destino = entry.Open())
            {
                origem.CopyTo(destino);
            }
        }

        private static void AdicionarArquivoBackup(ZipArchive archive, string caminhoCompleto, string nomeEntrada)
        {
            if (nomeEntrada.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                string snapshot = Path.Combine(Path.GetTempPath(), $"TACE_backup_{Guid.NewGuid():N}.db");
                try
                {
                    if (SQLiteDatabaseManager.CriarSnapshotBackup(snapshot))
                    {
                        AdicionarArquivoAoZip(archive, snapshot, nomeEntrada);
                        return;
                    }
                }
                finally
                {
                    try { if (File.Exists(snapshot)) File.Delete(snapshot); } catch { }
                }
            }

            AdicionarArquivoAoZip(archive, caminhoCompleto, nomeEntrada);
        }

        private async void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Inno Backup (*.zip)|*.zip";
                sfd.FileName = "Backup_TACE_" + DateTime.Now.ToString("yyyy-MM-dd");
                
                if (sfd.ShowDialog() == true)
                {
                    BtnExportar.IsEnabled = false;
                    BtnImportar.IsEnabled = false;
                    PanelProgresso.Visibility = Visibility.Visible;
                    TxtStatusProgresso.Text = "Aguarde, calculando arquivos...";
                    BarraProgresso.Value = 0;
                    
                    string rootPath = TACEUI.Core.PathHelper.GetRootDirectory();
                    string targetFile = sfd.FileName;
                    
                    await Task.Run(() =>
                    {
                        if (File.Exists(targetFile))
                            File.Delete(targetFile);
                             
                        using (ZipArchive archive = ZipFile.Open(targetFile, ZipArchiveMode.Create))
                        {
                            string[] folders = { 
                                @"docs\registros", 
                                "registros_de_dispositivos", 
                                "config",
                                @"data\input\municipios",
                                @"data\input\apps",
                                @"data\output\arquivos"
                            };
                            
                            string[] filesToBackup = {
                                @"data\bot_database.db",
                                @"data\bot_learn.json",
                                @"data\output\data\bot_learn.json",
                                "municipios.csv",
                                @"data\municipios.csv",
                                @"data\output\municipios.csv"
                            };
                            
                            // Primeiro contamos o total de arquivos nas pastas
                            int totalFiles = 0;
                            foreach (string folder in folders)
                            {
                                string sourceDir = Path.Combine(rootPath, folder);
                                if (Directory.Exists(sourceDir))
                                {
                                    totalFiles += Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories).Length;
                                }
                            }
                            
                            // E somamos os arquivos avulsos
                            foreach (string relFile in filesToBackup)
                            {
                                string fullFilePath = Path.Combine(rootPath, relFile);
                                if (File.Exists(fullFilePath))
                                {
                                    totalFiles++;
                                }
                            }
                            
                            if (totalFiles == 0) totalFiles = 1; // Prevenir divisão por zero
                            int processed = 0;
                            
                            // 1. Processa pastas
                            foreach (string folder in folders)
                            {
                                string sourceDir = Path.Combine(rootPath, folder);
                                if (Directory.Exists(sourceDir))
                                {
                                    string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
                                    foreach (string file in files)
                                    {
                                        string relativePath = folder + "\\" + file.Substring(sourceDir.Length + 1);
                                        AdicionarArquivoAoZip(archive, file, relativePath);
                                        
                                        processed++;
                                        int percent = (processed * 100) / totalFiles;
                                        
                                        Dispatcher.Invoke(() =>
                                        {
                                            BarraProgresso.Value = percent;
                                            TxtStatusProgresso.Text = $"Exportando: {percent}% ({processed}/{totalFiles} arquivos)";
                                        });
                                    }
                                }
                            }
                            
                            // 2. Processa arquivos avulsos
                            foreach (string relFile in filesToBackup)
                            {
                                string fullFilePath = Path.Combine(rootPath, relFile);
                                if (File.Exists(fullFilePath))
                                {
                                    AdicionarArquivoBackup(archive, fullFilePath, relFile);
                                    
                                    processed++;
                                    int percent = (processed * 100) / totalFiles;
                                    
                                    Dispatcher.Invoke(() =>
                                    {
                                        BarraProgresso.Value = percent;
                                        TxtStatusProgresso.Text = $"Exportando: {percent}% ({processed}/{totalFiles} arquivos)";
                                    });
                                }
                            }
                        }
                    });
                    
                    TxtStatusProgresso.Text = "Exportação concluída com sucesso!";
                    BarraProgresso.Value = 100;
                    MessageBox.Show("Backup exportado com sucesso!", "Exportação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    PanelProgresso.Visibility = Visibility.Collapsed;
                    BtnExportar.IsEnabled = true;
                    BtnImportar.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                PanelProgresso.Visibility = Visibility.Collapsed;
                BtnExportar.IsEnabled = true;
                BtnImportar.IsEnabled = true;
                MessageBox.Show("Erro ao exportar dados: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnImportar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Inno Backup (*.zip)|*.zip";
                
                if (ofd.ShowDialog() == true)
                {
                    BtnExportar.IsEnabled = false;
                    BtnImportar.IsEnabled = false;
                    PanelProgresso.Visibility = Visibility.Visible;
                    TxtStatusProgresso.Text = "Aguarde, lendo pacote...";
                    BarraProgresso.Value = 0;
                    
                    string rootPath = TACEUI.Core.PathHelper.GetRootDirectory();
                    string sourceFile = ofd.FileName;
                    int entradasIgnoradas = 0;
                    
                    await Task.Run(() =>
                    {
                        string raizSegura = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;

                        using (ZipArchive archive = ZipFile.OpenRead(sourceFile))
                        {
                            int totalFiles = archive.Entries.Count;
                            if (totalFiles == 0) totalFiles = 1;
                            int processed = 0;
                            int ignorados = 0;

                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destinationPath;
                                try
                                {
                                    destinationPath = Path.GetFullPath(Path.Combine(rootPath, entry.FullName));
                                }
                                catch
                                {
                                    processed++;
                                    ignorados++;
                                    continue;
                                }

                                if (!destinationPath.StartsWith(raizSegura, StringComparison.OrdinalIgnoreCase))
                                {
                                    processed++;
                                    ignorados++;
                                    continue;
                                }

                                string? destDir = Path.GetDirectoryName(destinationPath);
                                
                                if (!Directory.Exists(destDir) && !string.IsNullOrEmpty(destDir))
                                {
                                    Directory.CreateDirectory(destDir);
                                }
                                
                                if (!string.IsNullOrEmpty(entry.Name))
                                {
                                    entry.ExtractToFile(destinationPath, overwrite: true);
                                }
                                
                                processed++;
                                int percent = (processed * 100) / totalFiles;
                                
                                Dispatcher.Invoke(() =>
                                {
                                    BarraProgresso.Value = percent;
                                    TxtStatusProgresso.Text = $"Importando: {percent}% ({processed}/{totalFiles} arquivos)";
                                });
                              }

                            entradasIgnoradas = ignorados;
                        }
                    });
                    
                    TxtStatusProgresso.Text = entradasIgnoradas > 0
                        ? $"Importação concluída. {entradasIgnoradas} entrada(s) insegura(s) ignorada(s)."
                        : "Importação concluída com sucesso!";
                    BarraProgresso.Value = 100;
                    MessageBox.Show(
                        entradasIgnoradas > 0
                            ? $"Backup importado, mas {entradasIgnoradas} entrada(s) com caminho inválido foram ignoradas por segurança."
                            : "Backup importado com sucesso! Os dados locais foram atualizados.",
                        "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    PanelProgresso.Visibility = Visibility.Collapsed;
                    BtnExportar.IsEnabled = true;
                    BtnImportar.IsEnabled = true;
                    
                    // Recarrega as views na tela principal para atualizar os municípios e configurações na UI
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.ReloadAllViewsData();
                    }
                }
            }
            catch (Exception ex)
            {
                PanelProgresso.Visibility = Visibility.Collapsed;
                BtnExportar.IsEnabled = true;
                BtnImportar.IsEnabled = true;
                MessageBox.Show("Erro ao importar dados: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnTesteVelocidade_Click(object sender, RoutedEventArgs e)
        {
            BtnTesteVelocidade.IsEnabled = false;
            TxtResultadoTeste.Text = "Testando latência...";
            TxtResultadoTeste.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5EB1FF"));

            InitializeSpeedTestUI();

            int points = 0;
            string status = "";
            string colorHex = "#A1A1AA";

            await Task.Run(() =>
            {
                try
                {
                    Ping ping = new Ping();
                    long totalLatency = 0;
                    int successCount = 0;
                    
                    for (int i = 0; i < 4; i++)
                    {
                        PingReply reply = ping.Send("8.8.8.8", 2000);
                        if (reply.Status == IPStatus.Success)
                        {
                            totalLatency += reply.RoundtripTime;
                            successCount++;
                        }
                    }

                    if (successCount == 0)
                    {
                        points = 0;
                        status = "Sem Conexão";
                        colorHex = "#FF4C4C";
                    }
                    else
                    {
                        long avgLatency = totalLatency / successCount;
                        if (avgLatency > 150)
                        {
                            points = (avgLatency > 300) ? 1 : (avgLatency > 200 ? 2 : 3);
                            status = $"Ruim ({avgLatency}ms)";
                            colorHex = "#FF4C4C";
                        }
                        else if (avgLatency > 80)
                        {
                            points = (avgLatency > 110) ? 4 : (avgLatency > 90 ? 5 : 6);
                            status = $"Regular ({avgLatency}ms)";
                            colorHex = "#FFA500";
                        }
                        else if (avgLatency > 30)
                        {
                            points = (avgLatency > 50) ? 7 : 8;
                            status = $"Boa ({avgLatency}ms)";
                            colorHex = "#4EDEA3";
                        }
                        else
                        {
                            points = (avgLatency > 15) ? 9 : 10;
                            status = $"Excelente ({avgLatency}ms)";
                            colorHex = "#5EB1FF";
                        }
                    }
                }
                catch
                {
                    points = 0;
                    status = "Falha no Teste";
                    colorHex = "#FF4C4C";
                }
            });

            var items = new ObservableCollection<SpeedScoreItem>();
            var brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
            var emptyBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A40"));
            
            for (int i = 0; i < 10; i++)
            {
                items.Add(new SpeedScoreItem { Color = (i < points) ? brush : emptyBrush });
            }
            SpeedScoreItems.ItemsSource = items;

            TxtResultadoTeste.Text = status;
            TxtResultadoTeste.Foreground = brush;
            BtnTesteVelocidade.IsEnabled = true;
        }

    }

    public class SpeedScoreItem
    {
        public System.Windows.Media.SolidColorBrush? Color { get; set; }
    }
}
