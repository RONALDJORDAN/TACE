using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Collections.ObjectModel;

namespace InnovPlayUI.Views
{
    public partial class ConfigView : UserControl
    {
        private bool _isInitializingSettings = true;

        public ConfigView()
        {
            InitializeComponent();
            InitializeSpeedTestUI();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                _isInitializingSettings = true;
                string configPath = Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "config", "settings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    if (json.Contains("\"motor_c_enabled\": false") || json.Contains("\"motor_c_enabled\":false"))
                    {
                        ToggleMotor.IsChecked = false;
                    }
                    else
                    {
                        ToggleMotor.IsChecked = true;
                    }

                    if (json.Contains("\"reverse_tethering_enabled\": false") || json.Contains("\"reverse_tethering_enabled\":false"))
                    {
                        ToggleTethering.IsChecked = false;
                    }
                    else
                    {
                        ToggleTethering.IsChecked = true;
                    }
                }
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

        private void SaveSettings()
        {
            if (_isInitializingSettings) return;

            try
            {
                string configPath = Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "config", "settings.json");
                string configDir = Path.GetDirectoryName(configPath) ?? "";
                if (!Directory.Exists(configDir) && !string.IsNullOrEmpty(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                bool motorEnabled = ToggleMotor.IsChecked == true;
                bool tetheringEnabled = ToggleTethering.IsChecked == true;

                string json = "{\n" +
                              $"  \"font_ui_size\": 14,\n" +
                              $"  \"font_title_size\": 20,\n" +
                              $"  \"font_mono_size\": 13,\n" +
                              $"  \"motor_c_enabled\": {(motorEnabled ? "true" : "false")},\n" +
                              $"  \"reverse_tethering_enabled\": {(tetheringEnabled ? "true" : "false")}\n" +
                              "}";

                File.WriteAllText(configPath, json);
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

        private void BtnSubLogs_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubLogs, PanelLogs);
        }

        private void BtnSubDados_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubDados, PanelDados);
        }

        private void BtnSubBibliotecas_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubBibliotecas, PanelBibliotecas);
        }

        private void SwitchSubmodule(System.Windows.Controls.Button activeButton, System.Windows.Controls.StackPanel activePanel)
        {
            var brushNormal = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A1A1AA"));
            var bgNormal = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
            var brushActive = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EDEA3"));
            var bgActive = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A40"));

            // Resetar botoes
            BtnSubGeral.Foreground = brushNormal;
            BtnSubGeral.Background = bgNormal;
            BtnSubRede.Foreground = brushNormal;
            BtnSubRede.Background = bgNormal;
            BtnSubAparelhos.Foreground = brushNormal;
            BtnSubAparelhos.Background = bgNormal;
            BtnSubLogs.Foreground = brushNormal;
            BtnSubLogs.Background = bgNormal;
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
            PanelLogs.Visibility = Visibility.Collapsed;
            PanelDados.Visibility = Visibility.Collapsed;
            PanelBibliotecas.Visibility = Visibility.Collapsed;

            // Mostrar o painel ativo
            activePanel.Visibility = Visibility.Visible;
        }

        private void ToggleMotor_Checked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleMotor_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleTethering_Checked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ToggleTethering_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private async void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Inno Backup (*.zip)|*.zip";
                sfd.FileName = "Backup_InnoRPA_" + DateTime.Now.ToString("yyyy-MM-dd");
                
                if (sfd.ShowDialog() == true)
                {
                    BtnExportar.IsEnabled = false;
                    BtnImportar.IsEnabled = false;
                    PanelProgresso.Visibility = Visibility.Visible;
                    TxtStatusProgresso.Text = "Aguarde, calculando arquivos...";
                    BarraProgresso.Value = 0;
                    
                    string rootPath = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    string targetFile = sfd.FileName;
                    
                    await Task.Run(() =>
                    {
                        if (File.Exists(targetFile))
                            File.Delete(targetFile);
                             
                        using (ZipArchive archive = ZipFile.Open(targetFile, ZipArchiveMode.Create))
                        {
                            string[] folders = { 
                                "registros", 
                                "registros_de_dispositivos", 
                                @"data\input\municipios",
                                @"data\input\apps"
                            };
                            
                            string[] filesToBackup = {
                                @"data\inno_database.db",
                                @"data\inno_learn.json"
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
                                        archive.CreateEntryFromFile(file, relativePath);
                                        
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
                                    archive.CreateEntryFromFile(fullFilePath, relFile);
                                    
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
                    
                    string rootPath = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    string sourceFile = ofd.FileName;
                    
                    await Task.Run(() =>
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(sourceFile))
                        {
                            int totalFiles = archive.Entries.Count;
                            if (totalFiles == 0) totalFiles = 1;
                            int processed = 0;
                            
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destinationPath = Path.Combine(rootPath, entry.FullName);
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
                        }
                    });
                    
                    TxtStatusProgresso.Text = "Importação concluída com sucesso!";
                    BarraProgresso.Value = 100;
                    MessageBox.Show("Backup importado com sucesso! Os dados locais foram atualizados.", "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
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
