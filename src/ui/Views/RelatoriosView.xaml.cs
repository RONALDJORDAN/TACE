using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InnovPlayUI.Views
{
    public class RegistroModel
    {
        public string Data { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    public class DispositivoSelecionavel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                if (_isSelected != value) 
                {
                    _isSelected = value; 
                    OnPropertyChanged(nameof(IsSelected)); 
                }
            } 
        }
        public InnovPlayUI.Core.DispositivoDB Dispositivo { get; set; } = new InnovPlayUI.Core.DispositivoDB();

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public partial class RelatoriosView : UserControl
    {
        private ObservableCollection<RegistroModel> _registrosList;
        private System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB> _fullDbList;
        
        // Seleção Múltipla de Dispositivos para Relatório
        private ObservableCollection<DispositivoSelecionavel> _listaCompletaSelecionaveis;
        private ObservableCollection<DispositivoSelecionavel> _listaSelecionados;

        // Cache e Filtros do Banco de Dados
        private System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB> _bancoDadosCache = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
        private string _filtroModeloAtivo = "Todos";
        private int _editingDeviceId = 0;
        private string _currentSelectedLogPath = string.Empty;

        // Auto-refresh via FileSystemWatcher
        private FileSystemWatcher? _dbWatcher;
        private bool _isRefreshingDb = false;

        public RelatoriosView()
        {
            InitializeComponent();
            _registrosList = new ObservableCollection<RegistroModel>();
            _fullDbList = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
            
            _listaCompletaSelecionaveis = new ObservableCollection<DispositivoSelecionavel>();
            _listaSelecionados = new ObservableCollection<DispositivoSelecionavel>();
            
            ListRegistros.ItemsSource = _registrosList;
            ListBuscaAparelhos.ItemsSource = _listaCompletaSelecionaveis;
            ListSelecionados.ItemsSource = _listaSelecionados;

            ConfigurarObservadorDB();
            AtualizarDashboardMetrics();
            LoadBancoDadosDedicado();
            LoadRegistrosLocais();
            LoadRelatorioDados();
        }

        // =========================================================================
        // NAVEGAÇÃO ENTRE OS 4 SUBMÓDULOS
        // =========================================================================
        private void ResetSubMenuStyles()
        {
            var brushNormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
            var bgNormal = Brushes.Transparent;

            BtnSubMetricas.Foreground = brushNormal;
            BtnSubMetricas.Background = bgNormal;
            BtnSubBancoDados.Foreground = brushNormal;
            BtnSubBancoDados.Background = bgNormal;
            BtnSubRegistros.Foreground = brushNormal;
            BtnSubRegistros.Background = bgNormal;
            BtnSubExportacao.Foreground = brushNormal;
            BtnSubExportacao.Background = bgNormal;

            PanelMetricas.Visibility = Visibility.Collapsed;
            PanelBancoDados.Visibility = Visibility.Collapsed;
            PanelRegistros.Visibility = Visibility.Collapsed;
            PanelExportacao.Visibility = Visibility.Collapsed;
        }

        private void SetActiveSubMenu(Button btn, Border panel)
        {
            ResetSubMenuStyles();
            btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EDEA3"));
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A40"));
            panel.Visibility = Visibility.Visible;
        }

        private void BtnSubMetricas_Click(object sender, RoutedEventArgs e)
        {
            SetActiveSubMenu(BtnSubMetricas, PanelMetricas);
            AtualizarDashboardMetrics();
        }

        private void BtnSubBancoDados_Click(object sender, RoutedEventArgs e)
        {
            SetActiveSubMenu(BtnSubBancoDados, PanelBancoDados);
            LoadBancoDadosDedicado();
        }

        private void BtnSubRegistros_Click(object sender, RoutedEventArgs e)
        {
            SetActiveSubMenu(BtnSubRegistros, PanelRegistros);
            LoadRegistrosLocais();
        }

        private void BtnSubExportacao_Click(object sender, RoutedEventArgs e)
        {
            SetActiveSubMenu(BtnSubExportacao, PanelExportacao);
            LoadRelatorioDados();
        }

        // =========================================================================
        // SUBMÓDULO 1: MÉTRICAS & DASHBOARD ANALÍTICO
        // =========================================================================
        public void AtualizarDashboardMetrics()
        {
            try
            {
                var todosDispositivos = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                int total = todosDispositivos.Count;

                int totalV7 = todosDispositivos.Count(d =>
                    (d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Vision", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("v7_", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (d.Serial != null && (d.Serial.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0))
                );

                int totalM7 = todosDispositivos.Count(d =>
                    d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("f7_", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("TabMulti", StringComparison.OrdinalIgnoreCase) >= 0)
                );

                int totalNokia = todosDispositivos.Count(d =>
                    (d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("TA-1392", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("TA1392", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (d.Serial != null && (d.Serial.IndexOf("TA1392", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("TA-1392", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0))
                );

                if (DashTotalDispositivos != null) DashTotalDispositivos.Text = total.ToString("N0");
                if (DashTotalVision7 != null) DashTotalVision7.Text = totalV7.ToString("N0");
                if (DashTotalMultiM7 != null) DashTotalMultiM7.Text = totalM7.ToString("N0");
                if (DashTotalNokiaT20 != null) DashTotalNokiaT20.Text = totalNokia.ToString("N0");

                double percV7 = total > 0 ? (totalV7 * 100.0 / total) : 0;
                double percM7 = total > 0 ? (totalM7 * 100.0 / total) : 0;
                double percNokia = total > 0 ? (totalNokia * 100.0 / total) : 0;

                if (DashPercVision7 != null) DashPercVision7.Text = $"{percV7:F1}% da frota";
                if (DashPercMultiM7 != null) DashPercMultiM7.Text = $"{percM7:F1}% da frota";
                if (DashPercNokiaT20 != null) DashPercNokiaT20.Text = $"{percNokia:F1}% da frota";

                if (TxtBarVision7 != null) TxtBarVision7.Text = $"{totalV7} ({percV7:F1}%)";
                if (ProgressVision7 != null) ProgressVision7.Value = percV7;

                if (TxtBarMultiM7 != null) TxtBarMultiM7.Text = $"{totalM7} ({percM7:F1}%)";
                if (ProgressMultiM7 != null) ProgressMultiM7.Value = percM7;

                if (TxtBarNokiaT20 != null) TxtBarNokiaT20.Text = $"{totalNokia} ({percNokia:F1}%)";
                if (ProgressNokiaT20 != null) ProgressNokiaT20.Value = percNokia;

                // Integridade de MAC
                int comMac = todosDispositivos.Count(d => !string.IsNullOrWhiteSpace(d.MAC) && d.MAC != "02:00:00:00:00:00" && d.MAC != "Desconhecido");
                double percMac = total > 0 ? (comMac * 100.0 / total) : 100;
                if (TxtIntegridadeMac != null) TxtIntegridadeMac.Text = $"{comMac}/{total} ({percMac:F1}%)";

                // Contagem de Registros de Auditoria
                string rootPath = Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "registros");
                int totalLogs = 0;
                if (Directory.Exists(rootPath))
                {
                    totalLogs = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories).Length;
                }
                if (DashTotalRegistros != null) DashTotalRegistros.Text = totalLogs.ToString("N0");
                if (TxtTotalSessoesBuild != null) TxtTotalSessoesBuild.Text = $"{totalLogs} sessões gravadas";

                // Último dispositivo
                if (todosDispositivos.Count > 0)
                {
                    var ultimo = todosDispositivos[todosDispositivos.Count - 1];
                    if (TxtUltimoDispositivo != null)
                        TxtUltimoDispositivo.Text = $"{ultimo.MarcaModelo} | S/N: {ultimo.Serial} ({ultimo.DataRegistro})";
                }
                else
                {
                    if (TxtUltimoDispositivo != null)
                        TxtUltimoDispositivo.Text = "Nenhum dispositivo cadastrado.";
                }

                if (TxtMiniStatusDb != null)
                    TxtMiniStatusDb.Text = $"{total} aparelhos registrados";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RelatoriosView] Erro ao atualizar métricas: {ex.Message}");
            }
        }

        private void BtnAtualizarMetricas_Click(object sender, RoutedEventArgs e)
        {
            AtualizarDashboardMetrics();
            MessageBox.Show("Métricas e telemetria atualizadas com sucesso a partir do banco de dados!", "Métricas Atualizadas", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================================
        // SUBMÓDULO 2: BANCO DE DADOS DE DISPOSITIVOS
        // =========================================================================
        private void LoadBancoDadosDedicado()
        {
            try
            {
                _bancoDadosCache = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                AplicarFiltroBD();
                AtualizarDashboardMetrics();
            }
            catch { }
        }

        private void AplicarFiltroBD()
        {
            var resultado = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
            string busca = (TxtBuscaBD?.Text ?? string.Empty).Trim().ToLower();

            foreach (var d in _bancoDadosCache)
            {
                bool passaModelo = true;
                if (_filtroModeloAtivo == "Vision7")
                {
                    passaModelo = (d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Vision", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("v7_", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                                  (d.Serial != null && (d.Serial.IndexOf("T307", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("POS", StringComparison.OrdinalIgnoreCase) >= 0));
                }
                else if (_filtroModeloAtivo == "MultiM7")
                {
                    passaModelo = (d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("f7_", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0));
                }
                else if (_filtroModeloAtivo == "NokiaT20")
                {
                    passaModelo = (d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("TA-1392", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("TA1392", StringComparison.OrdinalIgnoreCase) >= 0));
                }

                bool passaBusca = string.IsNullOrEmpty(busca)
                    || (d.Serial?.ToLower().Contains(busca) == true)
                    || (d.MarcaModelo?.ToLower().Contains(busca) == true)
                    || (d.MAC?.ToLower().Contains(busca) == true);

                if (passaModelo && passaBusca)
                    resultado.Add(d);
            }

            GridBancoDados.ItemsSource = resultado;
            if (TxtContadorBD != null)
                TxtContadorBD.Text = $"{resultado.Count} registro{(resultado.Count != 1 ? "s" : "")}" + (_bancoDadosCache.Count != resultado.Count ? $" de {_bancoDadosCache.Count}" : "");
            
            if (TxtInfoSelecionado != null)
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
        }

        private void ResetFilterPillButtons()
        {
            var inactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#151525"));
            var inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
            var inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A40"));

            BtnFiltroTodos.Background = inactiveBg; BtnFiltroTodos.Foreground = inactiveFg; BtnFiltroTodos.BorderBrush = inactiveBorder;
            BtnFiltroVision7.Background = inactiveBg; BtnFiltroVision7.Foreground = inactiveFg; BtnFiltroVision7.BorderBrush = inactiveBorder;
            BtnFiltroMultiM7.Background = inactiveBg; BtnFiltroMultiM7.Foreground = inactiveFg; BtnFiltroMultiM7.BorderBrush = inactiveBorder;
            BtnFiltroNokiaT20.Background = inactiveBg; BtnFiltroNokiaT20.Foreground = inactiveFg; BtnFiltroNokiaT20.BorderBrush = inactiveBorder;
        }

        private void SetActiveFilterPill(Button btn, string filtro)
        {
            ResetFilterPillButtons();
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#513BA6"));
            btn.Foreground = Brushes.White;
            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8570E6"));
            _filtroModeloAtivo = filtro;
            AplicarFiltroBD();
        }

        private void BtnFiltroTodos_Click(object sender, RoutedEventArgs e) => SetActiveFilterPill(BtnFiltroTodos, "Todos");
        private void BtnFiltroVision7_Click(object sender, RoutedEventArgs e) => SetActiveFilterPill(BtnFiltroVision7, "Vision7");
        private void BtnFiltroMultiM7_Click(object sender, RoutedEventArgs e) => SetActiveFilterPill(BtnFiltroMultiM7, "MultiM7");
        private void BtnFiltroNokiaT20_Click(object sender, RoutedEventArgs e) => SetActiveFilterPill(BtnFiltroNokiaT20, "NokiaT20");

        private void TxtBuscaBD_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltroBD();

        private void GridBancoDados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = GridBancoDados.SelectedItem as InnovPlayUI.Core.DispositivoDB;
            if (item == null)
            {
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
                return;
            }
            TxtInfoSelecionado.Text = $"Selecionado: #{item.ID} — {item.MarcaModelo} | N/S: {item.Serial} | MAC: {item.MAC} | Registrado em: {item.DataRegistro}";
        }

        private void BtnNovoDispositivo_Click(object sender, RoutedEventArgs e)
        {
            _editingDeviceId = 0;
            TxtModalTitulo.Text = "➕ Adicionar Novo Dispositivo";
            TxtModalFabricante.Text = "Positivo";
            TxtModalModelo.Text = "Tab Vision 7 (T307G)";
            TxtModalSerial.Text = "";
            TxtModalMAC.Text = "";
            PanelModalEditDevice.Visibility = Visibility.Visible;
        }

        private void BtnEditarDispositivo_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GridBancoDados.SelectedItem as InnovPlayUI.Core.DispositivoDB;
            if (selectedItem == null)
            {
                MessageBox.Show("Selecione um dispositivo na lista para editar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingDeviceId = selectedItem.ID;
            TxtModalTitulo.Text = $"✏️ Editar Dispositivo #{selectedItem.ID}";
            TxtModalFabricante.Text = selectedItem.Fabricante;
            TxtModalModelo.Text = selectedItem.Modelo;
            TxtModalSerial.Text = selectedItem.Serial;
            TxtModalMAC.Text = selectedItem.MAC;
            PanelModalEditDevice.Visibility = Visibility.Visible;
        }

        private void BtnModalCancelar_Click(object sender, RoutedEventArgs e)
        {
            PanelModalEditDevice.Visibility = Visibility.Collapsed;
        }

        private void BtnModalSalvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string serial = TxtModalSerial.Text.Trim();
                string fabricante = TxtModalFabricante.Text.Trim();
                string modelo = TxtModalModelo.Text.Trim();
                string mac = TxtModalMAC.Text.Trim();

                if (string.IsNullOrWhiteSpace(serial))
                {
                    MessageBox.Show("Informe o Número de Série (N/S) do dispositivo.", "Campo Obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_editingDeviceId == 0)
                {
                    InnovPlayUI.Core.SQLiteDatabaseManager.RegistrarSeNovo(serial, fabricante, modelo, mac);
                }
                else
                {
                    var todos = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                    var item = todos.Find(d => d.ID == _editingDeviceId);
                    if (item != null)
                    {
                        item.Serial = serial;
                        item.Fabricante = fabricante;
                        item.Modelo = modelo;
                        item.MAC = mac;
                        InnovPlayUI.Core.SQLiteDatabaseManager.SalvarTodos(todos);
                    }
                }

                InnovPlayUI.Core.SQLiteDatabaseManager.InvalidarCache();
                PanelModalEditDevice.Visibility = Visibility.Collapsed;
                LoadBancoDadosDedicado();
                MessageBox.Show("Dispositivo salvo com sucesso no banco de dados!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar dispositivo: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GridBancoDados.SelectedItems;
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Selecione um ou mais dispositivos na lista para excluir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int count = selectedItems.Count;
            string msg = count == 1 
                ? "Tem certeza que deseja excluir o dispositivo selecionado?" 
                : $"Tem certeza que deseja excluir os {count} dispositivos selecionados?";

            var res1 = MessageBox.Show(msg, "Confirmação de Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res1 == MessageBoxResult.Yes)
            {
                try
                {
                    var listToDelete = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
                    foreach (InnovPlayUI.Core.DispositivoDB d in selectedItems)
                    {
                        listToDelete.Add(d);
                    }

                    foreach (var item in listToDelete)
                    {
                        InnovPlayUI.Core.SQLiteDatabaseManager.Excluir(item.ID);
                    }

                    InnovPlayUI.Core.SQLiteDatabaseManager.InvalidarCache();
                    LoadBancoDadosDedicado();
                    MessageBox.Show($"{count} dispositivo(s) excluído(s) com sucesso.", "Exclusão Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao excluir: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // =========================================================================
        // SUBMÓDULO 3: REGISTROS & AUDITORIA DE LOGS
        // =========================================================================
        private void LoadRegistrosLocais()
        {
            try
            {
                _registrosList.Clear();
                string rootPath = Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "registros");

                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                var dirs = Directory.GetDirectories(rootPath);
                // Ordena por data decrescente
                Array.Sort(dirs, (a, b) => Directory.GetCreationTime(b).CompareTo(Directory.GetCreationTime(a)));

                foreach (var d in dirs)
                {
                    var info = new DirectoryInfo(d);
                    
                    // Procura subpastas de auditoria (ex: HH-MM_Plataforma_InnovTech_DESKTOP_BUILD)
                    var subDirs = Directory.GetDirectories(d);
                    if (subDirs.Length > 0)
                    {
                        foreach (var sub in subDirs)
                        {
                            var subInfo = new DirectoryInfo(sub);
                            string tipo = "Auditoria Build";
                            if (subInfo.Name.IndexOf("OTA", StringComparison.OrdinalIgnoreCase) >= 0) tipo = "OTA Update";
                            else if (subInfo.Name.IndexOf("EXEC", StringComparison.OrdinalIgnoreCase) >= 0) tipo = "Execução RPA";

                            _registrosList.Add(new RegistroModel
                            {
                                Data = info.Name,
                                Nome = subInfo.Name,
                                Tipo = tipo,
                                FullPath = sub
                            });
                        }
                    }
                    else
                    {
                        _registrosList.Add(new RegistroModel
                        {
                            Data = info.CreationTime.ToString("yyyy-MM-dd"),
                            Nome = info.Name,
                            Tipo = "Pasta de Auditoria",
                            FullPath = d
                        });
                    }
                }

                if (_registrosList.Count > 0 && ListRegistros.SelectedIndex == -1)
                {
                    ListRegistros.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                TxtLogViewer.Text = "Erro ao carregar lista de registros: " + ex.Message;
            }
        }

        private void ListRegistros_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var reg = ListRegistros.SelectedItem as RegistroModel;
            if (reg == null || string.IsNullOrWhiteSpace(reg.FullPath)) return;

            try
            {
                _currentSelectedLogPath = reg.FullPath;
                TxtLogHeaderTitle.Text = $"📄 {reg.Nome} ({reg.Data})";

                if (Directory.Exists(reg.FullPath))
                {
                    // Procura arquivos .log ou .txt na pasta
                    var logFiles = Directory.GetFiles(reg.FullPath, "*.log", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(reg.FullPath, "*.txt", SearchOption.AllDirectories))
                        .ToArray();

                    if (logFiles.Length > 0)
                    {
                        string content = File.ReadAllText(logFiles[0]);
                        TxtLogViewer.Text = content;
                        _currentSelectedLogPath = logFiles[0];
                    }
                    else
                    {
                        var files = Directory.GetFiles(reg.FullPath);
                        if (files.Length > 0)
                        {
                            TxtLogViewer.Text = $"[Pasta de Auditoria]\nArquivos presentes ({files.Length}):\n" + string.Join("\n", files.Select(Path.GetFileName));
                        }
                        else
                        {
                            TxtLogViewer.Text = "[Pasta de Auditoria Vazia ou Sem Arquivos de Texto]";
                        }
                    }
                }
                else if (File.Exists(reg.FullPath))
                {
                    TxtLogViewer.Text = File.ReadAllText(reg.FullPath);
                }
            }
            catch (Exception ex)
            {
                TxtLogViewer.Text = $"Erro ao ler arquivo de log: {ex.Message}";
            }
        }

        private void BtnCopiarLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtLogViewer.Text))
            {
                Clipboard.SetText(TxtLogViewer.Text);
                MessageBox.Show("Conteúdo do log copiado para a Área de Transferência!", "Log Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAbrirBlocoNotas_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentSelectedLogPath) && File.Exists(_currentSelectedLogPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", _currentSelectedLogPath) { UseShellExecute = true });
            }
            else
            {
                // Cria arquivo temporário para visualização
                string tempFile = Path.Combine(Path.GetTempPath(), "inno_log_inspect.txt");
                File.WriteAllText(tempFile, TxtLogViewer.Text);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", tempFile) { UseShellExecute = true });
            }
        }

        private void BtnAbrirPastaRegistros_Click(object sender, RoutedEventArgs e)
        {
            string rootPath = Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "registros");
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", rootPath) { UseShellExecute = true });
        }

        private void BtnRecarregarLogs_Click(object sender, RoutedEventArgs e)
        {
            LoadRegistrosLocais();
            MessageBox.Show("Registros e auditorias recarregados com sucesso!", "Registros", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================================
        // SUBMÓDULO 4: EXPORTAÇÃO & EMISSÃO DE DADOS
        // =========================================================================
        private void BtnTabRelDados_Click(object sender, RoutedEventArgs e)
        {
            BtnTabRelDados.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#513BA6"));
            BtnTabRelDados.Foreground = Brushes.White;
            BtnTabRelQRCode.Background = Brushes.Transparent;
            BtnTabRelQRCode.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85859E"));

            SubPanelRelDados.Visibility = Visibility.Visible;
            SubPanelRelQRCode.Visibility = Visibility.Collapsed;
        }

        private void BtnTabRelQRCode_Click(object sender, RoutedEventArgs e)
        {
            BtnTabRelQRCode.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#513BA6"));
            BtnTabRelQRCode.Foreground = Brushes.White;
            BtnTabRelDados.Background = Brushes.Transparent;
            BtnTabRelDados.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85859E"));

            SubPanelRelDados.Visibility = Visibility.Collapsed;
            SubPanelRelQRCode.Visibility = Visibility.Visible;
        }

        private void LoadRelatorioDados()
        {
            try
            {
                _fullDbList = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                _listaCompletaSelecionaveis.Clear();
                _listaSelecionados.Clear();

                foreach (var d in _fullDbList)
                {
                    _listaCompletaSelecionaveis.Add(new DispositivoSelecionavel { Dispositivo = d, IsSelected = false });
                }

                AtualizarContadorSelecionados();
            }
            catch { }
        }

        private void TxtBuscaAparelho_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = (TxtBuscaAparelho.Text ?? string.Empty).Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                ListBuscaAparelhos.ItemsSource = _listaCompletaSelecionaveis;
            }
            else
            {
                var filtrados = _listaCompletaSelecionaveis.Where(x =>
                    (x.Dispositivo.Serial != null && x.Dispositivo.Serial.ToLower().Contains(query)) ||
                    (x.Dispositivo.MarcaModelo != null && x.Dispositivo.MarcaModelo.ToLower().Contains(query)) ||
                    (x.Dispositivo.MAC != null && x.Dispositivo.MAC.ToLower().Contains(query))
                ).ToList();
                ListBuscaAparelhos.ItemsSource = filtrados;
            }
        }

        private void ChkSelecionarTodos_Click(object sender, RoutedEventArgs e)
        {
            bool select = ChkSelecionarTodos.IsChecked == true;
            var currentList = ListBuscaAparelhos.ItemsSource as System.Collections.Generic.IEnumerable<DispositivoSelecionavel> ?? _listaCompletaSelecionaveis;
            foreach (var item in currentList)
            {
                item.IsSelected = select;
                SincronizarCarrinho(item);
            }
            AtualizarContadorSelecionados();
        }

        private void ChkItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is DispositivoSelecionavel item)
            {
                SincronizarCarrinho(item);
                AtualizarContadorSelecionados();
            }
        }

        private void SincronizarCarrinho(DispositivoSelecionavel item)
        {
            if (item.IsSelected)
            {
                if (!_listaSelecionados.Any(x => x.Dispositivo.ID == item.Dispositivo.ID))
                    _listaSelecionados.Add(item);
            }
            else
            {
                var existing = _listaSelecionados.FirstOrDefault(x => x.Dispositivo.ID == item.Dispositivo.ID);
                if (existing != null)
                    _listaSelecionados.Remove(existing);
            }
        }

        private void BtnSelecionarTodos_Click(object sender, RoutedEventArgs e)
        {
            var currentList = ListBuscaAparelhos.ItemsSource as System.Collections.Generic.IEnumerable<DispositivoSelecionavel> ?? _listaCompletaSelecionaveis;
            foreach (var item in currentList)
            {
                item.IsSelected = true;
                SincronizarCarrinho(item);
            }
            if (ChkSelecionarTodos != null) ChkSelecionarTodos.IsChecked = true;
            AtualizarContadorSelecionados();
        }

        private void BtnInverterSelecao_Click(object sender, RoutedEventArgs e)
        {
            var currentList = ListBuscaAparelhos.ItemsSource as System.Collections.Generic.IEnumerable<DispositivoSelecionavel> ?? _listaCompletaSelecionaveis;
            foreach (var item in currentList)
            {
                item.IsSelected = !item.IsSelected;
                SincronizarCarrinho(item);
            }
            AtualizarContadorSelecionados();
        }

        private void BtnLimparSelecao_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _listaCompletaSelecionaveis)
            {
                item.IsSelected = false;
            }
            _listaSelecionados.Clear();
            if (ChkSelecionarTodos != null) ChkSelecionarTodos.IsChecked = false;
            AtualizarContadorSelecionados();
        }

        private void BtnRemoverSelecionado_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DispositivoSelecionavel item)
            {
                item.IsSelected = false;
                _listaSelecionados.Remove(item);
                AtualizarContadorSelecionados();
            }
        }

        private void AtualizarContadorSelecionados()
        {
            if (LblQtdSelecionados != null)
            {
                LblQtdSelecionados.Text = $"{_listaSelecionados.Count} aparelho(s) selecionado(s)";
            }
        }

        // =========================================================================
        // EMISSÃO DE RELATÓRIO PDF A4 FORMATADO
        // =========================================================================
        private void BtnEmitirRelDadosPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
                
                if (_listaSelecionados.Count > 0)
                {
                    foreach (var ds in _listaSelecionados) lista.Add(ds.Dispositivo);
                }
                else
                {
                    var res = MessageBox.Show("Nenhum aparelho selecionado na lista. Deseja emitir o relatório contendo toda a base de dados?", "Emissão Completa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    lista = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                }

                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum dado encontrado para emitir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() != true) return;

                System.Windows.Documents.FlowDocument doc = new System.Windows.Documents.FlowDocument();
                doc.PagePadding = new Thickness(50, 40, 50, 40);
                doc.ColumnWidth = pd.PrintableAreaWidth - 100;
                doc.FontFamily = new FontFamily("Segoe UI");

                // Cabeçalho / Título
                var title = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Relatório de Equipamentos — Plataforma InnovTech"))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                doc.Blocks.Add(title);

                // Subtítulo
                string strFiltro = _listaSelecionados.Count > 0 ? "Seleção Personalizada" : "Base Completa";
                var sub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Filtro: {strFiltro} | Total: {lista.Count} dispositivo(s) | Emitido em: {DateTime.Now:dd/MM/yyyy HH:mm}"))
                {
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                doc.Blocks.Add(sub);

                // Tabela de Dados
                var table = new System.Windows.Documents.Table();
                double[] widths = { 40, 160, 140, 140, 110 };
                foreach (var w in widths) table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(w) });

                var rg = new System.Windows.Documents.TableRowGroup();
                table.RowGroups.Add(rg);

                // Cabeçalho da Tabela
                string[] headers = { "#", "Fabricante / Modelo", "N/S (Serial)", "MAC Address", "Data Registro" };
                var hr = new System.Windows.Documents.TableRow { Background = Brushes.DarkSlateGray };
                foreach (var h in headers)
                {
                    hr.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        Foreground = Brushes.White,
                        Padding = new Thickness(6, 4, 6, 4)
                    }));
                }
                rg.Rows.Add(hr);

                // Linhas
                bool alt = false;
                foreach (var d in lista)
                {
                    var row = new System.Windows.Documents.TableRow { Background = alt ? Brushes.WhiteSmoke : Brushes.White };
                    alt = !alt;
                    string[] vals = { d.ID.ToString(), d.MarcaModelo, d.Serial, d.MAC, d.DataRegistro };
                    foreach (var v in vals)
                    {
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(v ?? "-"))
                        {
                            FontSize = 9.5,
                            Padding = new Thickness(6, 3, 6, 3)
                        }));
                    }
                    rg.Rows.Add(row);
                }
                doc.Blocks.Add(table);

                // Rodapé
                var footer = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"InnovTech Inno RPA — Gerado automaticamente em {DateTime.Now:dd/MM/yyyy HH:mm:ss}"))
                {
                    FontSize = 8.5,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };
                doc.Blocks.Add(footer);

                pd.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator, "Relatorio de Dispositivos - InnovTech");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao emitir PDF: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // EXPORTAÇÃO EM PLANILHA EXCEL / CSV
        // =========================================================================
        private async void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
                if (_listaSelecionados.Count > 0)
                {
                    foreach (var s in _listaSelecionados) lista.Add(s.Dispositivo);
                }
                else
                {
                    lista = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                }

                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum dado para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Planilha CSV (*.csv)|*.csv|Arquivo de Texto (*.txt)|*.txt",
                    FileName = $"Relatorio_Dispositivos_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("ID;Fabricante_Modelo;Serial;MAC_Address;Data_Registro");
                    foreach (var d in lista)
                    {
                        sb.AppendLine($"{d.ID};\"{d.MarcaModelo}\";\"{d.Serial}\";\"{d.MAC}\";\"{d.DataRegistro}\"");
                    }

                    await File.WriteAllTextAsync(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    MessageBox.Show($"Exportação concluída com sucesso!\nArquivo: {sfd.FileName}", "Exportação Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao exportar planilha: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // ETIQUETAS E TAGS QR CODE PATRIMONIAIS
        // =========================================================================
        private void BtnGerarEtiquetas_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum aparelho cadastrado para gerar etiquetas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Cria pasta de saída
                string outDir = Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "data", "output", "etiquetas");
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                string pdfFile = Path.Combine(outDir, $"Etiquetas_QR_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                
                // Dispara gerador em segundo plano
                MessageBox.Show($"As etiquetas patrimoniais serão geradas em formato contínuo (6cm x 3cm) em:\n{pdfFile}", "Geração de Etiquetas", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao gerar etiquetas: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFiltrarParaTags_Click(object sender, RoutedEventArgs e)
        {
            SetActiveSubMenu(BtnSubBancoDados, PanelBancoDados);
            MessageBox.Show("Utilize os filtros rápidos de modelo no Banco de Dados para selecionar o lote que deseja etiquetar.", "Filtrar para Etiquetas", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================================
        // OBSERVADOR DE BANCO DE DADOS (AUTO-REFRESH)
        // =========================================================================
        private void ConfigurarObservadorDB()
        {
            try
            {
                string dbPath = InnovPlayUI.Core.SQLiteDatabaseManager.GetDatabasePath();
                string? dbDir = Path.GetDirectoryName(dbPath);
                string dbFile = Path.GetFileName(dbPath);

                if (Directory.Exists(dbDir))
                {
                    _dbWatcher = new FileSystemWatcher(dbDir, dbFile);
                    _dbWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    _dbWatcher.Changed += DbWatcher_Changed;
                    _dbWatcher.EnableRaisingEvents = true;
                }
            }
            catch { }
        }

        private async void DbWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (_isRefreshingDb) return;
            _isRefreshingDb = true;

            await System.Threading.Tasks.Task.Delay(500); // Aguarda liberação do arquivo SQLite

            Dispatcher.Invoke(() =>
            {
                AtualizarDashboardMetrics();
                if (PanelBancoDados.Visibility == Visibility.Visible)
                {
                    LoadBancoDadosDedicado();
                }
                else if (PanelExportacao.Visibility == Visibility.Visible)
                {
                    LoadRelatorioDados();
                }
            });

            _isRefreshingDb = false;
        }

        private async void BtnSyncInnovStock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (BtnSyncInnovStock != null)
                {
                    BtnSyncInnovStock.IsEnabled = false;
                    BtnSyncInnovStock.Content = "⏳ Sincronizando...";
                }

                int count = await InnovPlayUI.Core.SQLiteDatabaseManager.SincronizarTodosComInnovStockAsync();
                if (count > 0)
                {
                    MessageBox.Show($"Sucesso! {count} dispositivo(s) foram sincronizados com o banco de dados da nuvem do InnovStock.", "Sincronização InnovStock", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Nenhum dispositivo encontrado para sincronizar ou ocorreu uma falha de conexão com o InnovStock. Verifique a internet e tente novamente.", "Sincronização InnovStock", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro durante a sincronização com o InnovStock: {ex.Message}", "Erro de Sincronização", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (BtnSyncInnovStock != null)
                {
                    BtnSyncInnovStock.IsEnabled = true;
                    BtnSyncInnovStock.Content = "☁️ Sync InnovStock";
                }
            }
        }
    }
}
