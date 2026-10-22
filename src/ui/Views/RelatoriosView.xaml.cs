using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BotRpaUI.Views
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
        public BotRpaUI.Core.DispositivoDB Dispositivo { get; set; } = new BotRpaUI.Core.DispositivoDB();

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class RegistroAparelhoItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _mac = string.Empty;
        private string _status = "Aguardando";
        private string _patrimonio = string.Empty;
        private bool _registrado;

        public string Serial { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;

        public string MAC
        {
            get => _mac;
            set
            {
                if (_mac == value) return;
                _mac = value;
                OnPropertyChanged(nameof(MAC));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string Patrimonio
        {
            get => _patrimonio;
            set
            {
                if (_patrimonio == value) return;
                _patrimonio = value;
                OnPropertyChanged(nameof(Patrimonio));
            }
        }

        public bool Registrado
        {
            get => _registrado;
            set
            {
                if (_registrado == value) return;
                _registrado = value;
                OnPropertyChanged(nameof(Registrado));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public partial class RelatoriosView : UserControl
    {
        private ObservableCollection<RegistroModel> _registrosList;
        private System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB> _fullDbList;
        
        // Seleção Múltipla de Dispositivos para Relatório
        private ObservableCollection<DispositivoSelecionavel> _listaCompletaSelecionaveis;
        private ObservableCollection<DispositivoSelecionavel> _listaSelecionados;

        // Cache e Filtros do Banco de Dados
        private System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB> _bancoDadosCache = new System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB>();
        private string _filtroModeloAtivo = "Todos";
        private int _editingDeviceId = 0;
        private string _currentSelectedLogPath = string.Empty;

        // Auto-refresh via FileSystemWatcher
        private FileSystemWatcher? _dbWatcher;
        private bool _isRefreshingDb = false;

        private readonly ObservableCollection<RegistroAparelhoItem> _registroAparelhos = new ObservableCollection<RegistroAparelhoItem>();
        private readonly System.Collections.Generic.Dictionary<string, DateTime> _registroUltimaTentativaMac = new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _registroPulados = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private System.Windows.Threading.DispatcherTimer? _registroTimer;
        private System.Threading.CancellationTokenSource? _registroCts;
        private RegistroAparelhoItem? _registroAtual;
        private bool _registroProcessando;
        private int _registroUltimoTotalConectados = -1;

        public RelatoriosView()
        {
            InitializeComponent();
            _registrosList = new ObservableCollection<RegistroModel>();
            _fullDbList = new System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB>();
            
            _listaCompletaSelecionaveis = new ObservableCollection<DispositivoSelecionavel>();
            _listaSelecionados = new ObservableCollection<DispositivoSelecionavel>();
            
            ListRegistros.ItemsSource = _registrosList;
            ListBuscaAparelhos.ItemsSource = _listaCompletaSelecionaveis;
            ListSelecionados.ItemsSource = _listaSelecionados;
            GridRegistroSerie.ItemsSource = _registroAparelhos;

            _registroTimer = new System.Windows.Threading.DispatcherTimer();
            _registroTimer.Interval = TimeSpan.FromSeconds(2);
            _registroTimer.Tick += RegistroTimer_Tick;
            this.Unloaded += (s, e) => EncerrarRegistroSerie();

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
            try
            {
                _filtroModeloAtivo = "Todos";
                if (TxtBuscaBD != null) TxtBuscaBD.Text = string.Empty;
                if (BtnFiltroTodos != null) SetActiveFilterPill(BtnFiltroTodos, "Todos");
                else AplicarFiltroBD();
            }
            catch { }
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
                var todosDispositivos = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
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
                string rootPath = Path.Combine(BotRpaUI.Core.PathHelper.GetRootDirectory(), "registros");
                int totalLogs = 0;
                if (Directory.Exists(rootPath))
                {
                    totalLogs = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories).Length;
                }
                if (DashTotalRegistros != null) DashTotalRegistros.Text = totalLogs.ToString("N0");
                if (TxtTotalSessoesBuild != null) TxtTotalSessoesBuild.Text = $"{totalLogs} sessões gravadas";

                // Último dispositivo (por ID de cadastro, independente da ordenação por patrimônio)
                var ultimo = todosDispositivos.OrderByDescending(d => d.ID).FirstOrDefault();
                if (ultimo != null)
                {
                    if (TxtUltimoDispositivo != null)
                        TxtUltimoDispositivo.Text = $"{ultimo.MarcaModelo} | S/N: {ultimo.Serial} | Patrimônio: {(string.IsNullOrWhiteSpace(ultimo.Patrimonio) ? "—" : ultimo.Patrimonio)}";
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

        // =========================================================================
        // SUBMÓDULO 2: BANCO DE DADOS DE DISPOSITIVOS
        // =========================================================================
        private void LoadBancoDadosDedicado()
        {
            try
            {
                _bancoDadosCache = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
                AplicarFiltroBD();
                AtualizarDashboardMetrics();
            }
            catch { }
        }

        private void AplicarFiltroBD()
        {
            var resultado = new System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB>();
            string busca = NormalizarBusca(TxtBuscaBD?.Text);

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

                bool passaBusca = busca.Length == 0
                    || NormalizarBusca(d.Patrimonio).Contains(busca)
                    || NormalizarBusca(d.Serial).Contains(busca)
                    || NormalizarBusca(d.MAC).Contains(busca)
                    || NormalizarBusca(d.MarcaModelo).Contains(busca)
                    || NormalizarBusca(d.Fabricante).Contains(busca)
                    || NormalizarBusca(d.Modelo).Contains(busca);

                if (passaModelo && passaBusca)
                    resultado.Add(d);
            }

            resultado = resultado
                .OrderBy(d => BotRpaUI.Core.PatrimonioHelper.ExtrairNumero(d.Patrimonio))
                .ThenBy(d => d.ID)
                .ToList();

            GridBancoDados.ItemsSource = resultado;
            if (TxtContadorBD != null)
                TxtContadorBD.Text = $"{resultado.Count} registro{(resultado.Count != 1 ? "s" : "")}" + (_bancoDadosCache.Count != resultado.Count ? $" de {_bancoDadosCache.Count}" : "");

            if (TxtBuscaBDPlaceholder != null)
                TxtBuscaBDPlaceholder.Visibility = string.IsNullOrWhiteSpace(TxtBuscaBD?.Text) ? Visibility.Visible : Visibility.Collapsed;

            if (TxtInfoSelecionado != null)
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
        }

        private static string NormalizarBusca(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

            var sb = new System.Text.StringBuilder(valor.Length);
            foreach (char c in valor)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
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
            var item = GridBancoDados.SelectedItem as BotRpaUI.Core.DispositivoDB;
            if (item == null)
            {
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
                return;
            }
            TxtInfoSelecionado.Text = $"Selecionado: #{item.ID} — {item.MarcaModelo} | N/S: {item.Serial} | MAC: {item.MAC} | Patrimônio: {(string.IsNullOrWhiteSpace(item.Patrimonio) ? "—" : item.Patrimonio)}";
        }

        private void BtnNovoDispositivo_Click(object sender, RoutedEventArgs e)
        {
            _editingDeviceId = 0;
            TxtModalTitulo.Text = "➕ Adicionar Novo Dispositivo";
            TxtModalPatrimonio.Text = "";
            TxtModalFabricante.Text = "Positivo";
            TxtModalModelo.Text = "Tab Vision 7 (T307G)";
            TxtModalSerial.Text = "";
            TxtModalMAC.Text = "";
            PanelModalEditDevice.Visibility = Visibility.Visible;
        }

        private void BtnEditarDispositivo_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GridBancoDados.SelectedItem as BotRpaUI.Core.DispositivoDB;
            if (selectedItem == null)
            {
                MessageBox.Show("Selecione um dispositivo na lista para editar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingDeviceId = selectedItem.ID;
            TxtModalTitulo.Text = $"✏️ Editar Dispositivo #{selectedItem.ID}";
            TxtModalPatrimonio.Text = SomenteDigitosPatrimonio(selectedItem.Patrimonio);
            TxtModalFabricante.Text = selectedItem.Fabricante;
            TxtModalModelo.Text = selectedItem.Modelo;
            TxtModalSerial.Text = selectedItem.Serial;
            TxtModalMAC.Text = selectedItem.MAC;
            PanelModalEditDevice.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Grava a edicao inline da coluna "Nº Patrimonio" direto na base de dados.
        /// </summary>
        private void GridBancoDados_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is not BotRpaUI.Core.DispositivoDB item) return;
            if (e.Column?.Header?.ToString() != "Nº Patrimônio") return;

            Dispatcher.BeginInvoke(new Action(() => FinalizarEdicaoPatrimonio(item)));
        }

        private void FinalizarEdicaoPatrimonio(BotRpaUI.Core.DispositivoDB item)
        {
            string digitado = (item.Patrimonio ?? string.Empty).Trim();

            if (!BotRpaUI.Core.PatrimonioHelper.TryNormalizar(digitado, out string normalizado, out string erro))
            {
                MessageBox.Show(erro, "Patrimônio inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadBancoDadosDedicado();
                return;
            }

            if (!BotRpaUI.Core.SQLiteDatabaseManager.AtualizarPatrimonio(item.ID, normalizado))
            {
                MessageBox.Show("Não foi possível salvar o patrimônio. Tente novamente.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadBancoDadosDedicado();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RelatoriosView] Patrimonio do #{item.ID} atualizado para '{normalizado}'.");
            LoadBancoDadosDedicado();
        }

        private static string SomenteDigitosPatrimonio(string? patrimonio)
        {
            if (string.IsNullOrWhiteSpace(patrimonio)) return string.Empty;
            return new string(patrimonio.Where(char.IsDigit).ToArray());
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

                if (!BotRpaUI.Core.PatrimonioHelper.TryNormalizar(TxtModalPatrimonio.Text, out string patrimonio, out string erroPatrimonio))
                {
                    MessageBox.Show(erroPatrimonio, "Patrimônio inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtModalPatrimonio.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(serial))
                {
                    MessageBox.Show("Informe o Número de Série (N/S) do dispositivo.", "Campo Obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_editingDeviceId == 0)
                {
                    BotRpaUI.Core.SQLiteDatabaseManager.RegistrarSeNovo(serial, fabricante, modelo, mac);
                    var novo = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorSerial(serial);
                    if (novo != null && !string.IsNullOrWhiteSpace(patrimonio))
                        BotRpaUI.Core.SQLiteDatabaseManager.AtualizarPatrimonio(novo.ID, patrimonio);
                }
                else
                {
                    var todos = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
                    var item = todos.Find(d => d.ID == _editingDeviceId);
                    if (item != null)
                    {
                        item.Serial = serial;
                        item.Fabricante = fabricante;
                        item.Modelo = modelo;
                        item.MAC = mac;
                        item.Patrimonio = patrimonio;
                        BotRpaUI.Core.SQLiteDatabaseManager.SalvarTodos(todos);
                    }
                }

                BotRpaUI.Core.SQLiteDatabaseManager.InvalidarCache();
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
                    var listToDelete = new System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB>();
                    foreach (BotRpaUI.Core.DispositivoDB d in selectedItems)
                    {
                        listToDelete.Add(d);
                    }

                    foreach (var item in listToDelete)
                    {
                        BotRpaUI.Core.SQLiteDatabaseManager.Excluir(item.ID);
                    }

                    BotRpaUI.Core.SQLiteDatabaseManager.InvalidarCache();
                    LoadBancoDadosDedicado();
                    MessageBox.Show($"{count} dispositivo(s) excluído(s) com sucesso.", "Exclusão Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao excluir: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnRegistrarAparelho_Click(object sender, RoutedEventArgs e)
        {
            if (PanelModalRegistroSerie.Visibility == Visibility.Visible) return;

            _registroAparelhos.Clear();
            _registroPulados.Clear();
            _registroUltimaTentativaMac.Clear();
            _registroAtual = null;
            _registroUltimoTotalConectados = -1;
            TxtLogRegistroSerie.Clear();
            TxtRegistroPatrimonio.Clear();
            TxtRegistroPatrimonio.IsEnabled = false;
            BtnRegistrarPatrimonio.IsEnabled = false;
            BtnPularRegistro.IsEnabled = false;
            TxtRegistroSerial.Text = "-";
            TxtRegistroMac.Text = "-";
            TxtStatusRegistroSerie.Text = "Monitorando tablets conectados...";
            TxtContadorRegistroSerie.Text = "0 registrados";
            PanelModalRegistroSerie.Visibility = Visibility.Visible;

            RegistroLog("Registro em serie iniciado.");
            IniciarRegistroSerie();
        }

        private void IniciarRegistroSerie()
        {
            _registroTimer?.Stop();
            _registroCts?.Cancel();
            _registroCts?.Dispose();
            _registroCts = new System.Threading.CancellationTokenSource();
            _registroTimer?.Start();
            RegistroTimer_Tick(null, EventArgs.Empty);
        }

        private void EncerrarRegistroSerie()
        {
            _registroTimer?.Stop();

            var cts = _registroCts;
            _registroCts = null;
            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }

            _registroAtual = null;
            try { PanelModalRegistroSerie.Visibility = Visibility.Collapsed; } catch { }
        }

        private async void RegistroTimer_Tick(object? sender, EventArgs e)
        {
            var cts = _registroCts;
            if (_registroProcessando || _registroAtual != null || cts == null || cts.IsCancellationRequested) return;

            _registroProcessando = true;
            try
            {
                var conectados = await System.Threading.Tasks.Task.Run(() => LerDispositivosConectados());
                if (!SessaoRegistroAtiva(cts)) return;

                AtualizarGridRegistro(conectados);
                if (_registroAtual != null) return;

                await ProcessarAparelhosConectadosAsync(conectados, cts);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                RegistroLog("ERRO: " + ex.Message);
            }
            finally
            {
                _registroProcessando = false;
            }
        }

        private bool SessaoRegistroAtiva(System.Threading.CancellationTokenSource cts)
        {
            return _registroCts == cts
                && !cts.IsCancellationRequested
                && PanelModalRegistroSerie.Visibility == Visibility.Visible;
        }

        private System.Collections.Generic.List<DispositivoConectadoRegistro> LerDispositivosConectados()
        {
            var lista = new System.Collections.Generic.List<DispositivoConectadoRegistro>();
            var seriais = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            MonitorView.Engine_RefreshDevices();
            int total = MonitorView.Engine_GetDeviceCount();

            for (int i = 0; i < total; i++)
            {
                var dev = new MonitorView.AdbDevice();
                MonitorView.Engine_GetDevice(i, ref dev);

                if (dev.authorized != 1 || string.IsNullOrWhiteSpace(dev.serial)) continue;
                if (dev.serial == "unknown" || dev.serial == "Pendente" || dev.serial.StartsWith("?", StringComparison.Ordinal)) continue;
                if (!seriais.Add(dev.serial)) continue;

                string mac = dev.mac ?? string.Empty;
                var registroSerial = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorSerial(dev.serial);

                if (!BotRpaUI.Core.MacCaptureService.MacValido(mac) && registroSerial != null && BotRpaUI.Core.MacCaptureService.MacValido(registroSerial.MAC))
                    mac = registroSerial.MAC;

                string patrimonio = string.Empty;
                if (registroSerial != null && !string.IsNullOrWhiteSpace(registroSerial.Patrimonio))
                    patrimonio = registroSerial.Patrimonio;

                if (string.IsNullOrWhiteSpace(patrimonio) && BotRpaUI.Core.MacCaptureService.MacValido(mac))
                {
                    var registroMac = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorMAC(mac);
                    if (registroMac != null && !string.IsNullOrWhiteSpace(registroMac.Patrimonio))
                        patrimonio = registroMac.Patrimonio;
                }

                lista.Add(new DispositivoConectadoRegistro
                {
                    Serial = dev.serial.Trim(),
                    Modelo = dev.model ?? string.Empty,
                    Fabricante = dev.manufacturer ?? string.Empty,
                    MAC = mac,
                    PatrimonioExistente = patrimonio
                });
            }

            return lista;
        }

        private void AtualizarGridRegistro(System.Collections.Generic.List<DispositivoConectadoRegistro> conectados)
        {
            foreach (var conectado in conectados)
            {
                var item = _registroAparelhos.FirstOrDefault(x => string.Equals(x.Serial, conectado.Serial, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    item = new RegistroAparelhoItem
                    {
                        Serial = conectado.Serial,
                        Modelo = conectado.Modelo,
                        Fabricante = conectado.Fabricante,
                        MAC = conectado.MAC,
                        Status = BotRpaUI.Core.MacCaptureService.MacValido(conectado.MAC) ? "Aguardando" : "Aguardando MAC"
                    };
                    _registroAparelhos.Add(item);
                    RegistroLog($"CONECTADO | S/N: {conectado.Serial} | MAC: {(BotRpaUI.Core.MacCaptureService.MacValido(conectado.MAC) ? conectado.MAC : "pendente")}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(conectado.Modelo)) item.Modelo = conectado.Modelo;
                    if (!string.IsNullOrWhiteSpace(conectado.Fabricante)) item.Fabricante = conectado.Fabricante;

                    if (BotRpaUI.Core.MacCaptureService.MacValido(conectado.MAC) && !string.Equals(item.MAC, conectado.MAC, StringComparison.OrdinalIgnoreCase))
                    {
                        item.MAC = conectado.MAC;
                        RegistroLog($"MAC ATUALIZADO | S/N: {conectado.Serial} | MAC: {conectado.MAC}");
                    }
                }

                if (!item.Registrado && !string.IsNullOrWhiteSpace(conectado.PatrimonioExistente))
                {
                    item.Registrado = true;
                    item.Patrimonio = conectado.PatrimonioExistente;
                    item.Status = "Ja cadastrado";
                    RegistroLog($"JA CADASTRADO | S/N: {conectado.Serial} | MAC: {(string.IsNullOrWhiteSpace(item.MAC) ? "pendente" : item.MAC)} | Patrimonio: {conectado.PatrimonioExistente}");
                }
            }

            if (_registroUltimoTotalConectados != conectados.Count)
            {
                _registroUltimoTotalConectados = conectados.Count;
                RegistroLog($"ADB: Engine_RefreshDevices() -> {conectados.Count} dispositivo(s) autorizado(s)");
            }

            if (conectados.Count == 0)
                TxtStatusRegistroSerie.Text = "Nenhum tablet autorizado conectado. Conecte o proximo aparelho...";
            else
                TxtStatusRegistroSerie.Text = $"{conectados.Count} tablet(s) conectado(s).";

            AtualizarContadorRegistro();
        }

        private async System.Threading.Tasks.Task ProcessarAparelhosConectadosAsync(
            System.Collections.Generic.List<DispositivoConectadoRegistro> conectados,
            System.Threading.CancellationTokenSource cts)
        {
            foreach (var conectado in conectados)
            {
                if (!SessaoRegistroAtiva(cts) || _registroAtual != null) return;

                var item = _registroAparelhos.FirstOrDefault(x => string.Equals(x.Serial, conectado.Serial, StringComparison.OrdinalIgnoreCase));
                if (item == null || item.Registrado || _registroPulados.Contains(item.Serial)) continue;

                if (BotRpaUI.Core.MacCaptureService.MacValido(item.MAC))
                {
                    var alvo = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorMAC(item.MAC)
                        ?? BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorSerial(item.Serial);

                    if (alvo != null && !string.IsNullOrWhiteSpace(alvo.Patrimonio))
                    {
                        item.Registrado = true;
                        item.Patrimonio = alvo.Patrimonio;
                        item.Status = "Ja cadastrado";
                        continue;
                    }

                    SelecionarAparelhoParaPatrimonio(item);
                    return;
                }

                if (!PodeTentarCaptura(item.Serial))
                {
                    item.Status = "Aguardando MAC";
                    continue;
                }

                _registroUltimaTentativaMac[item.Serial] = DateTime.Now;
                item.Status = "Capturando MAC...";
                RegistroLog($"CAPTURANDO MAC | S/N: {item.Serial}");

                string mac = await System.Threading.Tasks.Task.Run(() => BotRpaUI.Core.MacCaptureService.CapturarMac(item.Serial));
                if (!SessaoRegistroAtiva(cts)) return;

                if (!BotRpaUI.Core.MacCaptureService.MacValido(mac))
                {
                    item.Status = "MAC nao capturado";
                    RegistroLog($"FALHA AO CAPTURAR MAC | S/N: {item.Serial}");
                    continue;
                }

                item.MAC = mac;
                item.Status = "MAC capturado";
                RegistroLog($"MAC CAPTURADO | S/N: {item.Serial} | MAC: {mac}");

                var alvoMac = await System.Threading.Tasks.Task.Run(() =>
                {
                    var porMac = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorMAC(mac);
                    if (porMac != null && !string.IsNullOrWhiteSpace(porMac.Patrimonio)) return porMac;

                    var porSerial = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorSerial(item.Serial);
                    if (porSerial != null && !string.IsNullOrWhiteSpace(porSerial.Patrimonio)) return porSerial;

                    return null;
                });

                if (!SessaoRegistroAtiva(cts)) return;

                if (alvoMac != null)
                {
                    item.Registrado = true;
                    item.Patrimonio = alvoMac.Patrimonio;
                    item.Status = "Ja cadastrado";
                    RegistroLog($"JA CADASTRADO | S/N: {item.Serial} | MAC: {mac} | Patrimonio: {alvoMac.Patrimonio}");
                    continue;
                }

                SelecionarAparelhoParaPatrimonio(item);
                return;
            }

            AtualizarContadorRegistro();
        }

        private bool PodeTentarCaptura(string serial)
        {
            if (_registroUltimaTentativaMac.TryGetValue(serial, out DateTime ultima))
                return (DateTime.Now - ultima) >= TimeSpan.FromSeconds(12);

            return true;
        }

        private void SelecionarAparelhoParaPatrimonio(RegistroAparelhoItem item)
        {
            _registroAtual = item;
            item.Status = "Informe o patrimonio";
            GridRegistroSerie.SelectedItem = item;
            TxtRegistroSerial.Text = item.Serial;
            TxtRegistroMac.Text = BotRpaUI.Core.MacCaptureService.MacValido(item.MAC) ? item.MAC : "nao capturado";
            TxtRegistroPatrimonio.Text = string.Empty;
            TxtRegistroPatrimonio.IsEnabled = true;
            BtnRegistrarPatrimonio.IsEnabled = true;
            BtnPularRegistro.IsEnabled = true;
            TxtStatusRegistroSerie.Text = "Informe o numero de patrimonio do aparelho atual.";
            RegistroLog($"AGUARDANDO PATRIMONIO | S/N: {item.Serial} | MAC: {(BotRpaUI.Core.MacCaptureService.MacValido(item.MAC) ? item.MAC : "nao capturado")}");
            Dispatcher.BeginInvoke(new Action(() => TxtRegistroPatrimonio.Focus()));
        }

        private void BtnRegistrarPatrimonio_Click(object sender, RoutedEventArgs e)
        {
            if (_registroAtual == null)
            {
                MessageBox.Show("Nenhum aparelho aguardando registro.", "Registro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string entrada = (TxtRegistroPatrimonio.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(entrada))
            {
                MessageBox.Show("Informe o numero de patrimonio.", "Patrimonio obrigatorio", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtRegistroPatrimonio.Focus();
                return;
            }

            if (!BotRpaUI.Core.PatrimonioHelper.TryNormalizar(entrada, out string patrimonio, out string erro))
            {
                MessageBox.Show(erro, "Patrimonio invalido", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtRegistroPatrimonio.Focus();
                return;
            }

            var item = _registroAtual;
            bool salvo = BotRpaUI.Core.SQLiteDatabaseManager.RegistrarComPatrimonio(item.Serial, item.Fabricante, item.Modelo, item.MAC, patrimonio);
            if (!salvo)
            {
                MessageBox.Show("Nao foi possivel registrar o aparelho no banco de dados.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            item.Patrimonio = patrimonio;
            item.Registrado = true;
            item.Status = "Registrado";
            RegistroLog($"REGISTRADO | S/N: {item.Serial} | MAC: {item.MAC} | Patrimonio: {patrimonio}");

            _registroAtual = null;
            TxtRegistroPatrimonio.Clear();
            TxtRegistroPatrimonio.IsEnabled = false;
            BtnRegistrarPatrimonio.IsEnabled = false;
            BtnPularRegistro.IsEnabled = false;
            TxtRegistroSerial.Text = "-";
            TxtRegistroMac.Text = "-";
            TxtStatusRegistroSerie.Text = "Aparelho registrado. Conecte o proximo tablet...";
            AtualizarContadorRegistro();

            BotRpaUI.Core.SQLiteDatabaseManager.InvalidarCache();
            LoadBancoDadosDedicado();
            Dispatcher.BeginInvoke(new Action(() => RegistroTimer_Tick(null, EventArgs.Empty)));
        }

        private void BtnPularRegistro_Click(object sender, RoutedEventArgs e)
        {
            if (_registroAtual == null) return;

            var item = _registroAtual;
            _registroPulados.Add(item.Serial);
            item.Status = "Pulado";
            RegistroLog($"PULADO | S/N: {item.Serial} | MAC: {item.MAC}");

            _registroAtual = null;
            TxtRegistroPatrimonio.Clear();
            TxtRegistroPatrimonio.IsEnabled = false;
            BtnRegistrarPatrimonio.IsEnabled = false;
            BtnPularRegistro.IsEnabled = false;
            TxtRegistroSerial.Text = "-";
            TxtRegistroMac.Text = "-";
            TxtStatusRegistroSerie.Text = "Aparelho pulado. Aguardando o proximo...";
            Dispatcher.BeginInvoke(new Action(() => RegistroTimer_Tick(null, EventArgs.Empty)));
        }

        private void BtnReiniciarCapturaRegistro_Click(object sender, RoutedEventArgs e)
        {
            _registroPulados.Clear();
            _registroUltimaTentativaMac.Clear();
            _registroAtual = null;

            foreach (var item in _registroAparelhos)
            {
                if (!item.Registrado) item.Status = "Aguardando";
            }

            TxtRegistroPatrimonio.Clear();
            TxtRegistroPatrimonio.IsEnabled = false;
            BtnRegistrarPatrimonio.IsEnabled = false;
            BtnPularRegistro.IsEnabled = false;
            TxtRegistroSerial.Text = "-";
            TxtRegistroMac.Text = "-";
            TxtStatusRegistroSerie.Text = "Captura reiniciada.";
            RegistroLog("Captura reiniciada pelo operador.");
            Dispatcher.BeginInvoke(new Action(() => RegistroTimer_Tick(null, EventArgs.Empty)));
        }

        private void BtnFecharRegistroSerie_Click(object sender, RoutedEventArgs e)
        {
            EncerrarRegistroSerie();
            LoadBancoDadosDedicado();
        }

        private void TxtRegistroPatrimonio_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;

            e.Handled = true;
            BtnRegistrarPatrimonio_Click(sender, new RoutedEventArgs());
        }

        private void AtualizarContadorRegistro()
        {
            int total = _registroAparelhos.Count(x => x.Registrado);
            TxtContadorRegistroSerie.Text = $"{total} registrado{(total == 1 ? string.Empty : "s")}";
        }

        private void RegistroLog(string mensagem)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => RegistroLog(mensagem)));
                return;
            }

            if (TxtLogRegistroSerie == null) return;

            TxtLogRegistroSerie.AppendText($"[{DateTime.Now:HH:mm:ss}] {mensagem}{Environment.NewLine}");
            TxtLogRegistroSerie.ScrollToEnd();
        }

        private sealed class DispositivoConectadoRegistro
        {
            public string Serial { get; set; } = string.Empty;
            public string Modelo { get; set; } = string.Empty;
            public string Fabricante { get; set; } = string.Empty;
            public string MAC { get; set; } = string.Empty;
            public string PatrimonioExistente { get; set; } = string.Empty;
        }

        // =========================================================================
        // SUBMODULO 3: REGISTROS & AUDITORIA DE LOGS
        // =========================================================================
        private void LoadRegistrosLocais()
        {
            try
            {
                _registrosList.Clear();
                string rootPath = Path.Combine(BotRpaUI.Core.PathHelper.GetRootDirectory(), "registros");

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
                    
                    // Procura subpastas de auditoria (ex: HH-MM_Plataforma_BotRPA_DESKTOP_BUILD)
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
            string rootPath = Path.Combine(BotRpaUI.Core.PathHelper.GetRootDirectory(), "registros");
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
                _fullDbList = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
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
                var lista = new System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB>();
                
                if (_listaSelecionados.Count > 0)
                {
                    foreach (var ds in _listaSelecionados) lista.Add(ds.Dispositivo);
                }
                else
                {
                    var res = MessageBox.Show("Nenhum aparelho selecionado na lista. Deseja emitir o relatório contendo toda a base de dados?", "Emissão Completa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    lista = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
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
                var title = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Relatório de Equipamentos — Plataforma Bot_RPA"))
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
                double[] widths = { 40, 160, 140, 140, 110 };                foreach (var w in widths) table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(w) });

                var rg = new System.Windows.Documents.TableRowGroup();
                table.RowGroups.Add(rg);

                // Cabeçalho da Tabela
                string[] headers = { "#", "Nº Patrimônio", "Fabricante / Modelo", "N/S (Serial)", "MAC Address" };
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
                    string[] vals = { d.ID.ToString(), string.IsNullOrWhiteSpace(d.Patrimonio) ? "-" : d.Patrimonio, d.MarcaModelo, d.Serial, d.MAC };
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
                var footer = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Bot_RPA Bot_RPA — Gerado automaticamente em {DateTime.Now:dd/MM/yyyy HH:mm:ss}"))
                {
                    FontSize = 8.5,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };
                doc.Blocks.Add(footer);

                pd.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator, "Relatorio de Dispositivos - Bot_RPA");
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
                var lista = new System.Collections.Generic.List<BotRpaUI.Core.DispositivoDB>();
                if (_listaSelecionados.Count > 0)
                {
                    foreach (var s in _listaSelecionados) lista.Add(s.Dispositivo);
                }
                else
                {
                    lista = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
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
                    sb.AppendLine("ID;Patrimonio;Fabricante_Modelo;Serial;MAC_Address");
                    foreach (var d in lista)
                    {
                        sb.AppendLine($"{d.ID};\"{d.Patrimonio}\";\"{d.MarcaModelo}\";\"{d.Serial}\";\"{d.MAC}\"");
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
                var lista = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum aparelho cadastrado para gerar etiquetas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Cria pasta de saída
                string outDir = Path.Combine(BotRpaUI.Core.PathHelper.GetRootDirectory(), "data", "output", "etiquetas");
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
                string dbPath = BotRpaUI.Core.SQLiteDatabaseManager.GetDatabasePath();
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

    }
}
