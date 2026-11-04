using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TACEUI.Views
{
    public class RegistroModel
    {
        public DateTime DataHora { get; set; }
        public string Data => DataHora.ToString("dd/MM/yyyy");
        public string Hora => DataHora.ToString("HH:mm");
        public string Nome { get; set; } = string.Empty;
        public string Modalidade { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Detalhe { get; set; } = string.Empty;
        public string Tamanho { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
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
        public TACEUI.Core.DispositivoDB Dispositivo { get; set; } = new TACEUI.Core.DispositivoDB();

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class RegistroAparelhoItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _mac = string.Empty;
        private string _status = "Aguardando";
        private string _patrimonio = string.Empty;
        private string _portaUsb = string.Empty;
        private bool _registrado;
        private bool _selecionado;

        public string Serial { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;

        public bool Selecionado
        {
            get => _selecionado;
            set
            {
                if (_selecionado == value) return;
                _selecionado = value;
                OnPropertyChanged(nameof(Selecionado));
            }
        }

        public string PortaUsb
        {
            get => _portaUsb;
            set
            {
                if (_portaUsb == value) return;
                _portaUsb = value;
                OnPropertyChanged(nameof(PortaUsb));
                OnPropertyChanged(nameof(PortaUsbExibicao));
            }
        }

        public string PortaUsbExibicao => TACEUI.Core.UsbPortNameService.ObterExibicao(_portaUsb);

        public void NotificarPortaUsb()
        {
            OnPropertyChanged(nameof(PortaUsbExibicao));
        }

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
        private System.Collections.Generic.List<TACEUI.Core.DispositivoDB> _fullDbList;
        
        // Seleção Múltipla de Dispositivos para Relatório
        private ObservableCollection<DispositivoSelecionavel> _listaCompletaSelecionaveis;
        private ObservableCollection<DispositivoSelecionavel> _listaSelecionados;

        // Cache e Filtros do Banco de Dados
        private System.Collections.Generic.List<TACEUI.Core.DispositivoDB> _bancoDadosCache = new System.Collections.Generic.List<TACEUI.Core.DispositivoDB>();
        private string _filtroModeloAtivo = "Todos";
        private string _colunaOrdenacaoBD = "Patrimonio";
        private System.ComponentModel.ListSortDirection _direcaoOrdenacaoBD = System.ComponentModel.ListSortDirection.Ascending;
        private int _editingDeviceId = 0;
        private string _currentSelectedLogPath = string.Empty;

        // Registros & Auditoria
        private readonly System.Collections.Generic.List<RegistroModel> _todosRegistros = new System.Collections.Generic.List<RegistroModel>();
        private bool _auditoriaCarregada;
        private bool _atualizandoArquivosRegistro;
        private bool _atualizandoFiltrosRegistros;
        private bool _atualizandoFiltrosAuditoria;

        // Auto-refresh via FileSystemWatcher
        private FileSystemWatcher? _dbWatcher;
        private bool _isRefreshingDb = false;

        private readonly ObservableCollection<RegistroAparelhoItem> _registroAparelhos = new ObservableCollection<RegistroAparelhoItem>();
        private readonly System.Collections.Generic.Dictionary<string, DateTime> _registroUltimaTentativaMac = new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _registroPulados = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _registroDesligamentosSolicitados = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private System.Windows.Threading.DispatcherTimer? _registroTimer;
        private System.Threading.CancellationTokenSource? _registroCts;
        private RegistroAparelhoItem? _registroAtual;
        private bool _registroProcessando;
        private int _registroUltimoTotalConectados = -1;

        public RelatoriosView()
        {
            InitializeComponent();
            _registrosList = new ObservableCollection<RegistroModel>();
            _fullDbList = new System.Collections.Generic.List<TACEUI.Core.DispositivoDB>();
            
            _listaCompletaSelecionaveis = new ObservableCollection<DispositivoSelecionavel>();
            _listaSelecionados = new ObservableCollection<DispositivoSelecionavel>();
            
            ListRegistros.ItemsSource = _registrosList;
            ListBuscaAparelhos.ItemsSource = _listaCompletaSelecionaveis;
            ListSelecionados.ItemsSource = _listaSelecionados;
            GridRegistroSerie.ItemsSource = _registroAparelhos;

            _atualizandoFiltrosRegistros = true;
            if (CmbModalidadeRegistros != null) CmbModalidadeRegistros.SelectedIndex = 0;
            if (CmbPeriodoRegistros != null) CmbPeriodoRegistros.SelectedIndex = 0;
            _atualizandoFiltrosRegistros = false;

            _atualizandoFiltrosAuditoria = true;
            if (CmbModalidadeAuditoria != null) CmbModalidadeAuditoria.SelectedIndex = 0;
            if (CmbPeriodoAuditoria != null) CmbPeriodoAuditoria.SelectedIndex = 0;
            _atualizandoFiltrosAuditoria = false;

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

            if (SubPanelAuditoriaBase != null && SubPanelAuditoriaBase.Visibility == Visibility.Visible)
            {
                LoadAuditoria();
            }
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
                var todosDispositivos = TACEUI.Core.SQLiteDatabaseManager.LerTodos();
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

                // Integridade de MAC (mesma regra usada nos indicadores de cobertura)
                int comMac = todosDispositivos.Count(d => MacValido(d.MAC));
                double percMac = total > 0 ? (comMac * 100.0 / total) : 100;
                if (TxtIntegridadeMac != null) TxtIntegridadeMac.Text = $"{comMac}/{total} ({percMac:F1}%)";

                // Contagem de Registros de Auditoria (pastas de build/log)
                string rootPath = TACEUI.Core.PathHelper.GetRegistrosDirectory();
                int totalLogs = 0;
                if (Directory.Exists(rootPath))
                {
                    totalLogs = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories).Length;
                }
                if (DashTotalRegistros != null) DashTotalRegistros.Text = totalLogs.ToString("N0");
                if (TxtTotalSessoesBuild != null) TxtTotalSessoesBuild.Text = $"{totalLogs} sessões gravadas";

                // Cobertura e qualidade do cadastro
                int comPatrimonio = todosDispositivos.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio));
                int comSerial = todosDispositivos.Count(d => SerialValido(d.Serial));
                int comModelo = todosDispositivos.Count(d => ModeloIdentificado(d.Modelo));
                int cadastrosCompletos = todosDispositivos.Count(d =>
                    !string.IsNullOrWhiteSpace(d.Patrimonio) &&
                    SerialValido(d.Serial) &&
                    MacValido(d.MAC) &&
                    ModeloIdentificado(d.Modelo));

                AtualizarCoberturaDashboard(TxtCoberturaPatrimonio, TxtCoberturaPatrimonioPerc, ProgressCoberturaPatrimonio, comPatrimonio, total);
                AtualizarCoberturaDashboard(TxtCoberturaSerial, TxtCoberturaSerialPerc, ProgressCoberturaSerial, comSerial, total);
                AtualizarCoberturaDashboard(TxtCoberturaMac, TxtCoberturaMacPerc, ProgressCoberturaMac, comMac, total);
                AtualizarCoberturaDashboard(TxtCoberturaModelo, TxtCoberturaModeloPerc, ProgressCoberturaModelo, comModelo, total);

                double percCompleta = total > 0 ? (cadastrosCompletos * 100.0 / total) : 0;
                if (TxtBaseCompleta != null) TxtBaseCompleta.Text = $"{cadastrosCompletos}/{total} ({percCompleta:F1}%)";

                // Ranking de modelos
                var topModelos = todosDispositivos
                    .Select(d => ModeloIdentificado(d.Modelo) ? d.Modelo.Trim() : "Não identificado")
                    .GroupBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Nome = g.Key, Quantidade = g.Count() })
                    .OrderByDescending(x => x.Quantidade)
                    .ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
                    .Take(5)
                    .ToList();

                var paineisTopModelos = new[] { PanelTopModelo1, PanelTopModelo2, PanelTopModelo3, PanelTopModelo4, PanelTopModelo5 };
                var nomesTopModelos = new[] { TxtTopModelo1Nome, TxtTopModelo2Nome, TxtTopModelo3Nome, TxtTopModelo4Nome, TxtTopModelo5Nome };
                var valoresTopModelos = new[] { TxtTopModelo1Valor, TxtTopModelo2Valor, TxtTopModelo3Valor, TxtTopModelo4Valor, TxtTopModelo5Valor };
                var barrasTopModelos = new[] { ProgressTopModelo1, ProgressTopModelo2, ProgressTopModelo3, ProgressTopModelo4, ProgressTopModelo5 };

                for (int i = 0; i < paineisTopModelos.Length; i++)
                {
                    if (i < topModelos.Count)
                    {
                        double percModelo = total > 0 ? (topModelos[i].Quantidade * 100.0 / total) : 0;
                        paineisTopModelos[i].Visibility = Visibility.Visible;
                        nomesTopModelos[i].Text = topModelos[i].Nome;
                        valoresTopModelos[i].Text = $"{topModelos[i].Quantidade} ({percModelo:F1}%)";
                        barrasTopModelos[i].Value = percModelo;
                    }
                    else
                    {
                        paineisTopModelos[i].Visibility = Visibility.Collapsed;
                    }
                }

                // Alertas de cadastro
                int semPatrimonio = total - comPatrimonio;
                var patrimoniosDuplicados = todosDispositivos
                    .Where(d => !string.IsNullOrWhiteSpace(d.Patrimonio))
                    .GroupBy(d => d.Patrimonio.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .ToList();
                int codigosDuplicados = patrimoniosDuplicados.Count;
                int registrosDuplicados = patrimoniosDuplicados.Sum(g => g.Count());
                int macInvalido = total - comMac;
                int modeloNaoIdentificado = total - comModelo;
                int cadastrosIncompletos = total - cadastrosCompletos;

                if (TxtAlertaSemPatrimonio != null) TxtAlertaSemPatrimonio.Text = semPatrimonio.ToString("N0");
                if (TxtAlertaPatrimonioDuplicado != null) TxtAlertaPatrimonioDuplicado.Text = codigosDuplicados.ToString("N0");
                if (TxtAlertaMac != null) TxtAlertaMac.Text = macInvalido.ToString("N0");
                if (TxtAlertaModelo != null) TxtAlertaModelo.Text = modeloNaoIdentificado.ToString("N0");

                if (TxtResumoAlertas != null)
                {
                    if (cadastrosIncompletos == 0)
                    {
                        TxtResumoAlertas.Text = "Nenhuma pendência encontrada. Base 100% completa.";
                        TxtResumoAlertas.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EDEA3"));
                    }
                    else
                    {
                        string duplicados = registrosDuplicados > 0 ? $" | {registrosDuplicados} registro(s) com patrimônio duplicado" : string.Empty;
                        TxtResumoAlertas.Text = $"{cadastrosIncompletos} cadastro(s) com pendência(s) | base {percCompleta:F1}% completa{duplicados}";
                        TxtResumoAlertas.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB703"));
                    }
                }

                // Atividade recente registrada na auditoria
                var resumoAuditoria = TACEUI.Core.AuditLogManager.ObterResumo();
                if (TxtAuditoriaTotal != null) TxtAuditoriaTotal.Text = resumoAuditoria.Total.ToString("N0");
                if (TxtAuditoria7Dias != null) TxtAuditoria7Dias.Text = resumoAuditoria.Ultimos7Dias.ToString("N0");
                if (TxtAuditoria30Dias != null) TxtAuditoria30Dias.Text = resumoAuditoria.Ultimos30Dias.ToString("N0");

                if (TxtAuditoriaUltimoEvento != null)
                {
                    if (DateTime.TryParseExact(resumoAuditoria.UltimaDataHora, "yyyy-MM-dd HH:mm:ss",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime ultimaData))
                    {
                        TxtAuditoriaUltimoEvento.Text = ultimaData.ToString("dd/MM/yyyy HH:mm");
                    }
                    else
                    {
                        TxtAuditoriaUltimoEvento.Text = "—";
                    }
                }

                if (TxtAuditoriaUltimoDetalhe != null)
                    TxtAuditoriaUltimoDetalhe.Text = string.IsNullOrWhiteSpace(resumoAuditoria.UltimaAcao) ? "sem eventos" : resumoAuditoria.UltimaAcao;

                if (TxtAuditoriaAcaoPrincipal != null)
                {
                    TxtAuditoriaAcaoPrincipal.Text = string.IsNullOrWhiteSpace(resumoAuditoria.AcaoPrincipal)
                        ? "Ação mais frequente: —"
                        : $"Ação mais frequente: {resumoAuditoria.AcaoPrincipal} ({resumoAuditoria.AcaoPrincipalQuantidade:N0} eventos)";
                }

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

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RelatoriosView] Erro ao atualizar métricas: {ex.Message}");
            }
        }

        private static bool SerialValido(string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;

            string valor = serial.Trim();
            return !valor.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("pendente", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("n/a", StringComparison.OrdinalIgnoreCase)
                && !valor.StartsWith("?", StringComparison.Ordinal);
        }

        private static bool MacValido(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return false;

            string valor = mac.Trim();
            return !valor.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase)
                && valor != "---"
                && valor != "02:00:00:00:00:00";
        }

        private static bool ModeloIdentificado(string? modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo)) return false;

            string valor = modelo.Trim();
            return !valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("N/A", StringComparison.OrdinalIgnoreCase);
        }

        private static void AtualizarCoberturaDashboard(TextBlock? texto, TextBlock? percentual, ProgressBar? barra, int quantidade, int total)
        {
            double perc = total > 0 ? (quantidade * 100.0 / total) : 0;
            if (texto != null) texto.Text = $"{quantidade}/{total}";
            if (percentual != null) percentual.Text = $"{perc:F1}%";
            if (barra != null) barra.Value = perc;
        }

        // =========================================================================
        // SUBMÓDULO 2: BANCO DE DADOS DE DISPOSITIVOS
        // =========================================================================
        private void LoadBancoDadosDedicado()
        {
            try
            {
                _bancoDadosCache = TACEUI.Core.SQLiteDatabaseManager.LerTodos();
                AplicarFiltroBD();
                AtualizarDashboardMetrics();
            }
            catch { }
        }

        private void AplicarFiltroBD()
        {
            var resultado = new System.Collections.Generic.List<TACEUI.Core.DispositivoDB>();
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

            resultado = OrdenarResultadoBD(resultado);

            GridBancoDados.ItemsSource = resultado;
            AtualizarIndicadoresOrdenacaoBD();
            if (TxtContadorBD != null)
                TxtContadorBD.Text = $"{resultado.Count} registro{(resultado.Count != 1 ? "s" : "")}" + (_bancoDadosCache.Count != resultado.Count ? $" de {_bancoDadosCache.Count}" : "");

            if (TxtBuscaBDPlaceholder != null)
                TxtBuscaBDPlaceholder.Visibility = string.IsNullOrWhiteSpace(TxtBuscaBD?.Text) ? Visibility.Visible : Visibility.Collapsed;

            if (TxtInfoSelecionado != null)
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
        }

        private void GridBancoDados_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void GridBancoDados_Sorting(object sender, DataGridSortingEventArgs e)
        {
            string? coluna = e.Column?.SortMemberPath;
            if (string.IsNullOrEmpty(coluna)) return;

            if (string.Equals(_colunaOrdenacaoBD, coluna, StringComparison.Ordinal))
            {
                _direcaoOrdenacaoBD = _direcaoOrdenacaoBD == System.ComponentModel.ListSortDirection.Ascending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending;
            }
            else
            {
                _colunaOrdenacaoBD = coluna;
                _direcaoOrdenacaoBD = System.ComponentModel.ListSortDirection.Ascending;
            }

            e.Handled = true;
            AplicarFiltroBD();
        }

        private void AtualizarIndicadoresOrdenacaoBD()
        {
            if (GridBancoDados == null) return;

            foreach (var coluna in GridBancoDados.Columns)
            {
                coluna.SortDirection = string.Equals(coluna.SortMemberPath, _colunaOrdenacaoBD, StringComparison.Ordinal)
                    ? _direcaoOrdenacaoBD
                    : null;
            }
        }

        private System.Collections.Generic.List<TACEUI.Core.DispositivoDB> OrdenarResultadoBD(
            System.Collections.Generic.List<TACEUI.Core.DispositivoDB> itens)
        {
            bool desc = _direcaoOrdenacaoBD == System.ComponentModel.ListSortDirection.Descending;

            switch (_colunaOrdenacaoBD)
            {
                case "MarcaModelo":
                    return OrdenarPorTextoBD(itens, d => d.MarcaModelo, desc);
                case "Serial":
                    return OrdenarPorTextoBD(itens, d => d.Serial, desc);
                case "MAC":
                    return OrdenarPorTextoBD(itens, d => d.MAC, desc);
                case "Patrimonio":
                default:
                    var porPatrimonio = itens.OrderBy(d => string.IsNullOrWhiteSpace(d.Patrimonio));
                    var ordenado = desc
                        ? porPatrimonio.ThenByDescending(d => TACEUI.Core.PatrimonioHelper.ExtrairNumero(d.Patrimonio))
                        : porPatrimonio.ThenBy(d => TACEUI.Core.PatrimonioHelper.ExtrairNumero(d.Patrimonio));

                    return ordenado
                        .ThenBy(d => d.Patrimonio ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(d => d.ID)
                        .ToList();
            }
        }

        private static System.Collections.Generic.List<TACEUI.Core.DispositivoDB> OrdenarPorTextoBD(
            System.Collections.Generic.List<TACEUI.Core.DispositivoDB> itens,
            Func<TACEUI.Core.DispositivoDB, string?> seletor,
            bool desc)
        {
            var semVazios = itens.OrderBy(d => string.IsNullOrWhiteSpace(seletor(d)));
            var ordenado = desc
                ? semVazios.ThenByDescending(d => seletor(d) ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                : semVazios.ThenBy(d => seletor(d) ?? string.Empty, StringComparer.CurrentCultureIgnoreCase);

            return ordenado
                .ThenBy(d => TACEUI.Core.PatrimonioHelper.ExtrairNumero(d.Patrimonio))
                .ThenBy(d => d.ID)
                .ToList();
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
            var item = GridBancoDados.SelectedItem as TACEUI.Core.DispositivoDB;
            if (item == null)
            {
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
                return;
            }
            TxtInfoSelecionado.Text = $"Selecionado: Patrimônio {(string.IsNullOrWhiteSpace(item.Patrimonio) ? "—" : item.Patrimonio)} — {item.MarcaModelo} | N/S: {item.Serial} | MAC: {item.MAC}";
        }

        private void BtnNovoDispositivo_Click(object sender, RoutedEventArgs e)
        {
            _editingDeviceId = 0;
            TxtModalTitulo.Text = "Adicionar Novo Dispositivo";
            TxtModalPatrimonio.Text = "";
            TxtModalFabricante.Text = "Positivo";
            TxtModalModelo.Text = "Tab Vision 7 (T307G)";
            TxtModalSerial.Text = "";
            TxtModalMAC.Text = "";
            PanelModalEditDevice.Visibility = Visibility.Visible;
        }

        private void BtnEditarDispositivo_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GridBancoDados.SelectedItem as TACEUI.Core.DispositivoDB;
            if (selectedItem == null)
            {
                MessageBox.Show("Selecione um dispositivo na lista para editar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingDeviceId = selectedItem.ID;
            TxtModalTitulo.Text = string.IsNullOrWhiteSpace(selectedItem.Patrimonio)
                ? "Editar Dispositivo"
                : $"Editar Dispositivo — {selectedItem.Patrimonio}";
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
            if (e.Row.Item is not TACEUI.Core.DispositivoDB item) return;
            if (e.Column?.Header?.ToString() != "Nº Patrimônio") return;

            Dispatcher.BeginInvoke(new Action(() => FinalizarEdicaoPatrimonio(item)));
        }

        private void FinalizarEdicaoPatrimonio(TACEUI.Core.DispositivoDB item)
        {
            string digitado = (item.Patrimonio ?? string.Empty).Trim();

            if (!TACEUI.Core.PatrimonioHelper.TryNormalizar(digitado, out string normalizado, out string erro))
            {
                MessageBox.Show(erro, "Patrimônio inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadBancoDadosDedicado();
                return;
            }

            if (!TACEUI.Core.SQLiteDatabaseManager.AtualizarPatrimonio(item.ID, normalizado))
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

                if (!TACEUI.Core.PatrimonioHelper.TryNormalizar(TxtModalPatrimonio.Text, out string patrimonio, out string erroPatrimonio))
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
                    TACEUI.Core.SQLiteDatabaseManager.RegistrarSeNovo(serial, fabricante, modelo, mac);
                    var novo = TACEUI.Core.SQLiteDatabaseManager.BuscarPorSerial(serial);
                    if (novo != null && !string.IsNullOrWhiteSpace(patrimonio))
                        TACEUI.Core.SQLiteDatabaseManager.AtualizarPatrimonio(novo.ID, patrimonio);
                }
                else
                {
                    bool atualizado = TACEUI.Core.SQLiteDatabaseManager.AtualizarDispositivo(
                        _editingDeviceId, serial, fabricante, modelo, mac, patrimonio);
                    if (!atualizado)
                    {
                        MessageBox.Show("Não foi possível salvar o dispositivo. Verifique se o patrimônio, serial ou MAC já pertencem a outro registro.",
                            "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                TACEUI.Core.SQLiteDatabaseManager.InvalidarCache();
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
                    var listToDelete = new System.Collections.Generic.List<TACEUI.Core.DispositivoDB>();
                    foreach (TACEUI.Core.DispositivoDB d in selectedItems)
                    {
                        listToDelete.Add(d);
                    }

                    foreach (var item in listToDelete)
                    {
                        TACEUI.Core.SQLiteDatabaseManager.Excluir(item.ID);
                    }

                    TACEUI.Core.SQLiteDatabaseManager.InvalidarCache();
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
            _registroDesligamentosSolicitados.Clear();
            _registroUltimaTentativaMac.Clear();
            _registroAtual = null;
            _registroUltimoTotalConectados = -1;
            TxtLogRegistroSerie.Clear();
            TxtRegistroPatrimonio.Clear();
            TxtRegistroPatrimonio.IsEnabled = false;
            BtnRegistrarPatrimonio.IsEnabled = false;
            BtnPularRegistro.IsEnabled = false;
            TxtRegistroSerial.Text = "-";
            LimparReferenciaPortaUsb();
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
            _registroDesligamentosSolicitados.Clear();
            try { PanelModalRegistroSerie.Visibility = Visibility.Collapsed; } catch { }
        }

        private async void RegistroTimer_Tick(object? sender, EventArgs e)
        {
            var cts = _registroCts;
            if (_registroProcessando || cts == null || cts.IsCancellationRequested) return;

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
                var registroSerial = TACEUI.Core.SQLiteDatabaseManager.BuscarPorSerial(dev.serial);

                if (!TACEUI.Core.MacCaptureService.MacValido(mac) && registroSerial != null && TACEUI.Core.MacCaptureService.MacValido(registroSerial.MAC))
                    mac = registroSerial.MAC;

                string portaUsb = dev.usb_port ?? string.Empty;
                if (string.IsNullOrWhiteSpace(portaUsb))
                    portaUsb = TACEUI.Core.UsbPortResolver.ObterPorta(dev.serial);

                string patrimonio = string.Empty;
                if (registroSerial != null && !string.IsNullOrWhiteSpace(registroSerial.Patrimonio))
                    patrimonio = registroSerial.Patrimonio;

                if (string.IsNullOrWhiteSpace(patrimonio) && TACEUI.Core.MacCaptureService.MacValido(mac))
                {
                    var registroMac = TACEUI.Core.SQLiteDatabaseManager.BuscarPorMAC(mac);
                    if (registroMac != null && !string.IsNullOrWhiteSpace(registroMac.Patrimonio))
                        patrimonio = registroMac.Patrimonio;
                }

                lista.Add(new DispositivoConectadoRegistro
                {
                    Serial = dev.serial.Trim(),
                    Modelo = dev.model ?? string.Empty,
                    Fabricante = dev.manufacturer ?? string.Empty,
                    MAC = mac,
                    PortaUsb = portaUsb,
                    PatrimonioExistente = patrimonio
                });
            }

            return lista;
        }

        private void AtualizarGridRegistro(System.Collections.Generic.List<DispositivoConectadoRegistro> conectados)
        {
            var conectadosAgora = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var conectado in conectados) conectadosAgora.Add(conectado.Serial);
            _registroDesligamentosSolicitados.RemoveWhere(s => !conectadosAgora.Contains(s));

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
                        PortaUsb = conectado.PortaUsb,
                        Status = TACEUI.Core.MacCaptureService.MacValido(conectado.MAC) ? "Aguardando" : "Aguardando MAC"
                    };
                    _registroAparelhos.Add(item);
                    RegistroLog($"CONECTADO | S/N: {conectado.Serial} | USB: {TACEUI.Core.UsbPortNameService.ObterExibicaoComPorta(conectado.PortaUsb)} | MAC: {(TACEUI.Core.MacCaptureService.MacValido(conectado.MAC) ? conectado.MAC : "pendente")}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(conectado.Modelo)) item.Modelo = conectado.Modelo;
                    if (!string.IsNullOrWhiteSpace(conectado.Fabricante)) item.Fabricante = conectado.Fabricante;
                    if (!string.IsNullOrWhiteSpace(conectado.PortaUsb)) item.PortaUsb = conectado.PortaUsb;
                    if (ReferenceEquals(item, _registroAtual)) AtualizarReferenciaPortaUsb(item);

                    if (TACEUI.Core.MacCaptureService.MacValido(conectado.MAC) && !string.Equals(item.MAC, conectado.MAC, StringComparison.OrdinalIgnoreCase))
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
                    SolicitarDesligamento(item);
                }
            }

            foreach (var item in _registroAparelhos)
            {
                if (!item.Registrado) continue;
                if (item.Status != "Desligado" && item.Status != "Falha ao desligar") continue;
                if (conectadosAgora.Contains(item.Serial)) SolicitarDesligamento(item);
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

                if (TACEUI.Core.MacCaptureService.MacValido(item.MAC))
                {
                    var alvo = TACEUI.Core.SQLiteDatabaseManager.BuscarPorMAC(item.MAC)
                        ?? TACEUI.Core.SQLiteDatabaseManager.BuscarPorSerial(item.Serial);

                    if (alvo != null && !string.IsNullOrWhiteSpace(alvo.Patrimonio))
                    {
                        item.Registrado = true;
                        item.Patrimonio = alvo.Patrimonio;
                        item.Status = "Ja cadastrado";
                        SolicitarDesligamento(item);
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

                string mac = await System.Threading.Tasks.Task.Run(() => TACEUI.Core.MacCaptureService.CapturarMac(item.Serial));
                if (!SessaoRegistroAtiva(cts)) return;

                if (!TACEUI.Core.MacCaptureService.MacValido(mac))
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
                    var porMac = TACEUI.Core.SQLiteDatabaseManager.BuscarPorMAC(mac);
                    if (porMac != null && !string.IsNullOrWhiteSpace(porMac.Patrimonio)) return porMac;

                    var porSerial = TACEUI.Core.SQLiteDatabaseManager.BuscarPorSerial(item.Serial);
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
                    SolicitarDesligamento(item);
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

        private void SolicitarDesligamento(RegistroAparelhoItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Serial)) return;

            string serial = item.Serial.Trim();

            if (ToggleDesligarAoRegistrar.IsChecked != true)
            {
                RegistroLog($"DESLIGAMENTO AUTOMATICO DESATIVADO | S/N: {serial}");
                return;
            }

            if (!_registroDesligamentosSolicitados.Add(serial)) return;

            item.Status = "Desligando...";
            RegistroLog($"DESLIGANDO | S/N: {serial} | USB: {TACEUI.Core.UsbPortNameService.ObterExibicaoComPorta(item.PortaUsb)}");

            _ = System.Threading.Tasks.Task.Run(() =>
            {
                bool sucesso = false;
                string resposta = string.Empty;
                try
                {
                    MonitorView.Engine_ResetCancel();
                    int retorno = MonitorView.Engine_ShutdownDeviceClean(serial, RegistroLog);
                    sucesso = retorno == 1;
                    resposta = sucesso ? "rotina de desligamento concluida" : "rotina de desligamento retornou falha";
                }
                catch (Exception ex)
                {
                    resposta = "ERRO: " + ex.Message;
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    item.Status = sucesso ? "Desligado" : "Falha ao desligar";
                    RegistroLog($"DESLIGAMENTO | S/N: {serial} | {resposta}");
                }));
            });
        }

        private void AtualizarReferenciaPortaUsb(RegistroAparelhoItem item)
        {
            string porta = item?.PortaUsb ?? string.Empty;
            bool identificada = !string.IsNullOrWhiteSpace(porta) && porta != "N/A";

            TxtRegistroPortaUsb.Text = identificada
                ? TACEUI.Core.UsbPortNameService.ObterExibicaoComPorta(porta)
                : "---";
            TxtRegistroPortaUsb.ToolTip = identificada
                ? $"Porta USB fisica: {porta}"
                : "Porta USB nao identificada para o aparelho atual";
        }

        private void LimparReferenciaPortaUsb()
        {
            TxtRegistroPortaUsb.Text = "---";
            TxtRegistroPortaUsb.ToolTip = "Referencia fisica da porta USB do tablet atual";
        }

        private void SelecionarAparelhoParaPatrimonio(RegistroAparelhoItem item)
        {
            _registroAtual = item;
            item.Status = "Informe o patrimonio";
            GridRegistroSerie.SelectedItem = item;
            TxtRegistroSerial.Text = item.Serial;
            AtualizarReferenciaPortaUsb(item);
            TxtRegistroMac.Text = TACEUI.Core.MacCaptureService.MacValido(item.MAC) ? item.MAC : "nao capturado";
            TxtRegistroPatrimonio.Text = string.Empty;
            TxtRegistroPatrimonio.IsEnabled = true;
            BtnRegistrarPatrimonio.IsEnabled = true;
            BtnPularRegistro.IsEnabled = true;
            TxtStatusRegistroSerie.Text = "Informe o numero de patrimonio do aparelho atual.";
            RegistroLog($"AGUARDANDO PATRIMONIO | S/N: {item.Serial} | USB: {TACEUI.Core.UsbPortNameService.ObterExibicaoComPorta(item.PortaUsb)} | MAC: {(TACEUI.Core.MacCaptureService.MacValido(item.MAC) ? item.MAC : "nao capturado")}");
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

            if (!TACEUI.Core.PatrimonioHelper.TryNormalizar(entrada, out string patrimonio, out string erro))
            {
                MessageBox.Show(erro, "Patrimonio invalido", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtRegistroPatrimonio.Focus();
                return;
            }

            var item = _registroAtual;
            bool salvo = TACEUI.Core.SQLiteDatabaseManager.RegistrarComPatrimonio(item.Serial, item.Fabricante, item.Modelo, item.MAC, patrimonio);
            if (!salvo)
            {
                MessageBox.Show("Nao foi possivel registrar o aparelho no banco de dados.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            item.Patrimonio = patrimonio;
            item.Registrado = true;
            item.Status = "Registrado";
            RegistroLog($"REGISTRADO | S/N: {item.Serial} | MAC: {item.MAC} | Patrimonio: {patrimonio}");
            SolicitarDesligamento(item);

            _registroAtual = null;
            TxtRegistroPatrimonio.Clear();
            TxtRegistroPatrimonio.IsEnabled = false;
            BtnRegistrarPatrimonio.IsEnabled = false;
            BtnPularRegistro.IsEnabled = false;
            TxtRegistroSerial.Text = "-";
            LimparReferenciaPortaUsb();
            TxtRegistroMac.Text = "-";
            TxtStatusRegistroSerie.Text = "Aparelho registrado. Conecte o proximo tablet...";
            AtualizarContadorRegistro();

            TACEUI.Core.SQLiteDatabaseManager.InvalidarCache();
            LoadBancoDadosDedicado();
            Dispatcher.BeginInvoke(new Action(() => RegistroTimer_Tick(null, EventArgs.Empty)));
        }

        private void BtnNomearPortas_Click(object sender, RoutedEventArgs e)
        {
            var portas = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _registroAparelhos)
            {
                if (string.IsNullOrWhiteSpace(item.PortaUsb) || item.PortaUsb == "N/A") continue;
                string detalhe = string.IsNullOrWhiteSpace(item.Modelo)
                    ? $"S/N {item.Serial}"
                    : $"S/N {item.Serial} - {item.Modelo}";
                portas[item.PortaUsb] = detalhe;
            }

            var janela = new UsbPortNamingWindow(portas);
            var owner = Window.GetWindow(this);
            if (owner != null) janela.Owner = owner;

            if (janela.ShowDialog() != true) return;

            foreach (var item in _registroAparelhos) item.NotificarPortaUsb();
            if (_registroAtual != null) AtualizarReferenciaPortaUsb(_registroAtual);
            RegistroLog("Nomes das portas USB atualizados.");
        }

        private void BtnRegistrarSelecionados_Click(object sender, RoutedEventArgs e)
        {
            var selecionados = _registroAparelhos.Where(x => x.Selecionado && !x.Registrado).ToList();
            if (selecionados.Count == 0)
            {
                MessageBox.Show("Marque ao menos um aparelho na coluna Sel. e informe o patrimonio.", "Registro em lote", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var normalizados = new System.Collections.Generic.Dictionary<RegistroAparelhoItem, string>();
            var usados = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in selecionados)
            {
                string entrada = (item.Patrimonio ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(entrada))
                {
                    MessageBox.Show($"Informe o patrimonio do aparelho S/N {item.Serial}.", "Patrimonio obrigatorio", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TACEUI.Core.PatrimonioHelper.TryNormalizar(entrada, out string patrimonio, out string erro))
                {
                    MessageBox.Show($"{erro} (S/N {item.Serial})", "Patrimonio invalido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!usados.Add(patrimonio))
                {
                    MessageBox.Show($"O patrimonio {patrimonio} foi informado para mais de um aparelho.", "Patrimonio repetido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                normalizados[item] = patrimonio;
            }

            int registrados = 0;
            var falhas = new System.Collections.Generic.List<string>();

            foreach (var item in selecionados)
            {
                string patrimonio = normalizados[item];
                bool salvo = TACEUI.Core.SQLiteDatabaseManager.RegistrarComPatrimonio(
                    item.Serial, item.Fabricante, item.Modelo, item.MAC, patrimonio);

                if (!salvo)
                {
                    item.Status = "Erro ao registrar";
                    falhas.Add($"{item.Serial} ({patrimonio})");
                    RegistroLog($"ERRO REGISTRO | S/N: {item.Serial} | USB: {TACEUI.Core.UsbPortNameService.ObterExibicaoComPorta(item.PortaUsb)} | Patrimonio: {patrimonio}");
                    continue;
                }

                item.Patrimonio = patrimonio;
                item.Registrado = true;
                item.Status = "Registrado";
                item.Selecionado = false;
                registrados++;
                RegistroLog($"REGISTRADO EM LOTE | S/N: {item.Serial} | USB: {TACEUI.Core.UsbPortNameService.ObterExibicaoComPorta(item.PortaUsb)} | MAC: {(TACEUI.Core.MacCaptureService.MacValido(item.MAC) ? item.MAC : "nao capturado")} | Patrimonio: {patrimonio}");
                SolicitarDesligamento(item);

                if (ReferenceEquals(_registroAtual, item))
                    _registroAtual = null;
            }

            if (_registroAtual == null)
            {
                TxtRegistroPatrimonio.Clear();
                TxtRegistroPatrimonio.IsEnabled = false;
                BtnRegistrarPatrimonio.IsEnabled = false;
                BtnPularRegistro.IsEnabled = false;
                TxtRegistroSerial.Text = "-";
                LimparReferenciaPortaUsb();
                TxtRegistroMac.Text = "-";
            }

            AtualizarContadorRegistro();
            TxtStatusRegistroSerie.Text = $"{registrados} aparelho(s) registrado(s) nesta operacao. {falhas.Count} falha(s).";
            TACEUI.Core.SQLiteDatabaseManager.InvalidarCache();
            LoadBancoDadosDedicado();

            if (falhas.Count == 0)
                MessageBox.Show($"{registrados} aparelho(s) registrado(s) com sucesso.", "Registro em lote", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"{registrados} registrado(s) e {falhas.Count} falha(s):{Environment.NewLine}{string.Join(Environment.NewLine, falhas)}", "Registro em lote", MessageBoxButton.OK, MessageBoxImage.Warning);

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
            LimparReferenciaPortaUsb();
            TxtRegistroMac.Text = "-";
            TxtStatusRegistroSerie.Text = "Aparelho pulado. Aguardando o proximo...";
            Dispatcher.BeginInvoke(new Action(() => RegistroTimer_Tick(null, EventArgs.Empty)));
        }

        private void BtnReiniciarCapturaRegistro_Click(object sender, RoutedEventArgs e)
        {
            _registroPulados.Clear();
            _registroDesligamentosSolicitados.Clear();
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
            LimparReferenciaPortaUsb();
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
            public string PortaUsb { get; set; } = string.Empty;
            public string PatrimonioExistente { get; set; } = string.Empty;
        }

        // =========================================================================
        // SUBMODULO 3: REGISTROS & AUDITORIA
        // =========================================================================
        private void LoadRegistrosLocais()
        {
            try
            {
                _todosRegistros.Clear();
                string rootPath = TACEUI.Core.PathHelper.GetRegistrosDirectory();

                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                // Arquivos soltos na raiz de registros (ex.: erros_sqlite.txt)
                foreach (var file in Directory.GetFiles(rootPath))
                {
                    if (!EhArquivoTexto(file)) continue;
                    _todosRegistros.Add(CriarRegistroArquivo(new FileInfo(file), DateTime.Today, DetectarModalidadeArquivo(Path.GetFileName(file))));
                }

                var dirs = Directory.GetDirectories(rootPath);
                Array.Sort(dirs, (a, b) => string.Compare(Path.GetFileName(b), Path.GetFileName(a), StringComparison.Ordinal));

                foreach (var d in dirs)
                {
                    var info = new DirectoryInfo(d);
                    DateTime dataPasta = DateTime.TryParseExact(info.Name, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime dataConvertida)
                        ? dataConvertida
                        : info.CreationTime.Date;

                    EscanearRegistros(d, dataPasta, null);
                }

                _todosRegistros.Sort((a, b) => b.DataHora.CompareTo(a.DataHora));
                AplicarFiltroRegistros();
            }
            catch (Exception ex)
            {
                TxtLogViewer.Text = "Erro ao carregar lista de registros: " + ex.Message;
            }
        }

        private void EscanearRegistros(string diretorio, DateTime dataPasta, string? modalidadePai)
        {
            try
            {
                var info = new DirectoryInfo(diretorio);
                string modalidade = string.IsNullOrWhiteSpace(modalidadePai)
                    ? DetectarModalidadeArquivo(info.Name)
                    : modalidadePai;

                if (EhPastaSessaoBuild(info.Name, modalidadePai))
                {
                    _todosRegistros.Add(CriarRegistroPasta(info, dataPasta, modalidade));
                    return;
                }

                foreach (var file in Directory.GetFiles(diretorio))
                {
                    if (!EhArquivoTexto(file)) continue;
                    string modalidadeArquivo = modalidadePai == null
                        ? DetectarModalidadeArquivo(Path.GetFileName(file))
                        : modalidade;
                    _todosRegistros.Add(CriarRegistroArquivo(new FileInfo(file), dataPasta, modalidadeArquivo));
                }

                foreach (var sub in Directory.GetDirectories(diretorio))
                {
                    var subInfo = new DirectoryInfo(sub);
                    string modalidadeSub = modalidadePai != null
                        ? modalidade
                        : DetectarModalidadeArquivo(subInfo.Name);

                    EscanearRegistros(sub, dataPasta, modalidadeSub);
                }
            }
            catch { }
        }

        private static bool EhPastaSessaoBuild(string nome, string? modalidadePai)
        {
            string limpo = nome.Trim();
            if (limpo.Equals("BUILD", StringComparison.OrdinalIgnoreCase) ||
                limpo.Equals("OTA", StringComparison.OrdinalIgnoreCase) ||
                limpo.Equals("EXECUCAO", StringComparison.OrdinalIgnoreCase) ||
                limpo.Equals("SISTEMA", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool temMarcador = limpo.IndexOf("BUILD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               limpo.IndexOf("OTA", StringComparison.OrdinalIgnoreCase) >= 0;
            bool temHora = TentarHoraDoNome(limpo, out _);
            return temMarcador || (temHora && string.Equals(modalidadePai, TACEUI.Core.RegistroStorage.ModalidadeBuild, StringComparison.OrdinalIgnoreCase));
        }

        private static RegistroModel CriarRegistroPasta(DirectoryInfo info, DateTime dataPasta, string modalidade)
        {
            DateTime dataHora = TentarHoraDoNome(info.Name, out TimeSpan hora)
                ? dataPasta.Date.Add(hora)
                : info.CreationTime;
            long tamanho = 0;
            int arquivos = 0;

            try
            {
                foreach (var file in info.GetFiles("*", SearchOption.AllDirectories))
                {
                    tamanho += file.Length;
                    arquivos++;
                }
            }
            catch { }

            return new RegistroModel
            {
                DataHora = dataHora,
                Nome = info.Name,
                Modalidade = modalidade,
                Tipo = modalidade,
                Detalhe = LerDetalheBuild(info.FullName, arquivos),
                Tamanho = FormatarTamanho(tamanho),
                FullPath = info.FullName,
                IsFolder = true
            };
        }

        private static RegistroModel CriarRegistroArquivo(FileInfo info, DateTime dataPasta, string modalidade)
        {
            DateTime dataHora = info.LastWriteTime;
            if (TentarHoraDoNome(info.Name, out TimeSpan hora))
            {
                dataHora = dataPasta.Date.Add(hora);
            }

            string serial = ExtrairSerialDoLog(info.Name);
            return new RegistroModel
            {
                DataHora = dataHora,
                Nome = info.Name,
                Modalidade = modalidade,
                Tipo = modalidade,
                Detalhe = string.IsNullOrWhiteSpace(serial) ? info.Extension.TrimStart('.').ToUpperInvariant() : $"Serial {serial}",
                Tamanho = FormatarTamanho(info.Length),
                FullPath = info.FullName,
                IsFolder = false
            };
        }

        private static string DetectarModalidadeArquivo(string nome)
        {
            string valor = (nome ?? string.Empty).ToUpperInvariant();
            if (valor.Contains("OTA")) return TACEUI.Core.RegistroStorage.ModalidadeOta;
            if (valor.Contains("BUILD")) return TACEUI.Core.RegistroStorage.ModalidadeBuild;
            if (valor.Contains("EXEC")) return TACEUI.Core.RegistroStorage.ModalidadeExecucao;
            return TACEUI.Core.RegistroStorage.ModalidadeSistema;
        }

        private static bool TentarHoraDoNome(string nome, out TimeSpan hora)
        {
            hora = TimeSpan.Zero;
            var match = System.Text.RegularExpressions.Regex.Match(nome ?? string.Empty, @"^(\d{2})[-_](\d{2})(?:[-_](\d{2}))?");
            if (!match.Success) return false;

            if (!int.TryParse(match.Groups[1].Value, out int h) ||
                !int.TryParse(match.Groups[2].Value, out int m))
            {
                return false;
            }

            int s = match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out int seg) ? seg : 0;
            if (h > 23 || m > 59 || s > 59) return false;

            hora = new TimeSpan(h, m, s);
            return true;
        }

        private static string ExtrairSerialDoLog(string nomeArquivo)
        {
            string nome = Path.GetFileNameWithoutExtension(nomeArquivo ?? string.Empty);
            int idx = nome.IndexOf("_execucao", StringComparison.OrdinalIgnoreCase);
            if (idx > 0) nome = nome.Substring(0, idx);
            return nome.Trim();
        }

        private static string LerDetalheBuild(string pasta, int arquivos)
        {
            try
            {
                string log = Path.Combine(pasta, "build.log");
                if (File.Exists(log))
                {
                    string? versao = null;
                    string? modo = null;
                    foreach (var linha in File.ReadLines(log).Take(8))
                    {
                        if (linha.StartsWith("Versao:", StringComparison.OrdinalIgnoreCase))
                            versao = linha.Substring("Versao:".Length).Trim();
                        else if (linha.StartsWith("Modo:", StringComparison.OrdinalIgnoreCase))
                            modo = linha.Substring("Modo:".Length).Trim();
                    }

                    string detalhe = string.Join(" · ", new[] { versao, modo }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (!string.IsNullOrWhiteSpace(detalhe)) return $"{detalhe} · {arquivos} arquivo(s)";
                }
            }
            catch { }

            return $"{arquivos} arquivo(s)";
        }

        private static bool EhArquivoTexto(string caminho)
        {
            string ext = Path.GetExtension(caminho).ToLowerInvariant();
            return ext == ".log" || ext == ".txt" || ext == ".json" || ext == ".csv" ||
                   ext == ".md" || ext == ".xml" || ext == ".ini" || ext == ".config" || ext == ".html";
        }

        private static string FormatarTamanho(long bytes)
        {
            if (bytes >= 1024L * 1024L) return $"{bytes / 1024.0 / 1024.0:F1} MB";
            if (bytes >= 1024L) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        private void AplicarFiltroRegistros()
        {
            try
            {
                string busca = (TxtBuscaRegistros?.Text ?? string.Empty).Trim();
                string modalidade = ObterTextoCombo(CmbModalidadeRegistros, "Todas");
                string periodo = ObterTextoCombo(CmbPeriodoRegistros, "Tudo");
                DateTime? limite = ObterLimitePeriodo(periodo);

                var filtrados = _todosRegistros
                    .Where(r => modalidade == "Todas" || string.Equals(r.Modalidade, modalidade, StringComparison.OrdinalIgnoreCase))
                    .Where(r => !limite.HasValue || r.DataHora >= limite.Value)
                    .Where(r => string.IsNullOrWhiteSpace(busca) ||
                                r.Nome.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Detalhe.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Modalidade.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Data.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Hora.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(r => r.DataHora)
                    .ToList();

                ListRegistros.SelectedItem = null;
                _registrosList.Clear();
                foreach (var registro in filtrados) _registrosList.Add(registro);

                AtualizarResumoRegistros();
                AtualizarContadorAba();

                if (_registrosList.Count > 0)
                {
                    ListRegistros.SelectedIndex = 0;
                }
                else
                {
                    TxtLogHeaderTitle.Text = "Nenhum registro encontrado";
                    TxtLogMeta.Text = "Ajuste os filtros ou gere um novo build/execução.";
                    TxtLogViewer.Text = "[Nenhum registro corresponde aos filtros atuais...]";
                    CmbArquivosRegistro.Items.Clear();
                }
            }
            catch (Exception ex)
            {
                TxtLogViewer.Text = "Erro ao filtrar registros: " + ex.Message;
            }
        }

        private void AtualizarResumoRegistros()
        {
            if (TxtResumoRegistrosTotal != null) TxtResumoRegistrosTotal.Text = _todosRegistros.Count.ToString("N0");
            if (TxtResumoRegistrosHoje != null) TxtResumoRegistrosHoje.Text = _todosRegistros.Count(r => r.DataHora.Date == DateTime.Today).ToString("N0");
            if (TxtResumoBuilds != null) TxtResumoBuilds.Text = _todosRegistros.Count(r => r.Modalidade == TACEUI.Core.RegistroStorage.ModalidadeBuild).ToString("N0");
            if (TxtResumoExecucoes != null) TxtResumoExecucoes.Text = _todosRegistros.Count(r => r.Modalidade == TACEUI.Core.RegistroStorage.ModalidadeExecucao).ToString("N0");
        }

        private static string ObterTextoCombo(ComboBox? combo, string padrao)
        {
            if (combo?.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                return item.Content.ToString() ?? padrao;
            }
            return padrao;
        }

        private static DateTime? ObterLimitePeriodo(string periodo)
        {
            switch (periodo)
            {
                case "Hoje": return DateTime.Today;
                case "7 dias": return DateTime.Today.AddDays(-7);
                case "30 dias": return DateTime.Today.AddDays(-30);
                default: return null;
            }
        }

        private void FiltroRegistros_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizandoFiltrosRegistros || !IsLoaded) return;
            AplicarFiltroRegistros();
        }

        private void ListRegistros_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var reg = ListRegistros.SelectedItem as RegistroModel;
            if (reg == null || string.IsNullOrWhiteSpace(reg.FullPath)) return;

            TxtLogHeaderTitle.Text = reg.Nome;
            TxtLogMeta.Text = $"{reg.DataHora:dd/MM/yyyy HH:mm} · {reg.Modalidade} · {reg.Tamanho}";
            _currentSelectedLogPath = reg.FullPath;

            _atualizandoArquivosRegistro = true;
            CmbArquivosRegistro.Items.Clear();

            var arquivos = new System.Collections.Generic.List<string>();
            if (reg.IsFolder && Directory.Exists(reg.FullPath))
            {
                arquivos = ObterArquivosTexto(reg.FullPath, true);
                foreach (var arquivo in arquivos)
                {
                    CmbArquivosRegistro.Items.Add(new ComboBoxItem
                    {
                        Content = Path.GetRelativePath(reg.FullPath, arquivo),
                        Tag = arquivo
                    });
                }
            }
            else if (File.Exists(reg.FullPath))
            {
                arquivos.Add(reg.FullPath);
                CmbArquivosRegistro.Items.Add(new ComboBoxItem
                {
                    Content = Path.GetFileName(reg.FullPath),
                    Tag = reg.FullPath
                });
            }

            if (arquivos.Count > 0)
            {
                CmbArquivosRegistro.SelectedIndex = 0;
                _atualizandoArquivosRegistro = false;
                CarregarArquivoRegistro(arquivos[0]);
            }
            else
            {
                _atualizandoArquivosRegistro = false;
                _currentSelectedLogPath = reg.FullPath;
                TxtLogViewer.Text = reg.IsFolder
                    ? "[Pasta de auditoria sem arquivos de texto]"
                    : "[Arquivo binário ou vazio]";
            }
        }

        private static System.Collections.Generic.List<string> ObterArquivosTexto(string pasta, bool recursivo)
        {
            try
            {
                return Directory.GetFiles(pasta, "*", recursivo ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(EhArquivoTexto)
                    .OrderBy(f => Path.GetFileName(f).Equals("build.log", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new System.Collections.Generic.List<string>();
            }
        }

        private void CmbArquivosRegistro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizandoArquivosRegistro) return;
            if (CmbArquivosRegistro.SelectedItem is ComboBoxItem item && item.Tag is string caminho)
            {
                CarregarArquivoRegistro(caminho);
            }
        }

        private void CarregarArquivoRegistro(string caminho)
        {
            try
            {
                _currentSelectedLogPath = caminho;
                var info = new FileInfo(caminho);
                if (!info.Exists)
                {
                    TxtLogViewer.Text = "[Arquivo não encontrado]";
                    return;
                }

                if (info.Length > 2_000_000)
                {
                    using (var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8, true))
                    {
                        char[] buffer = new char[2_000_000];
                        int lidos = sr.ReadBlock(buffer, 0, buffer.Length);
                        TxtLogViewer.Text = new string(buffer, 0, lidos) + "\n\n[...] Visualização truncada em 2 MB. Use 'Abrir Selecionado' para ver o arquivo completo.";
                    }
                }
                else
                {
                    TxtLogViewer.Text = File.ReadAllText(caminho);
                }

                var reg = ListRegistros.SelectedItem as RegistroModel;
                if (reg != null)
                {
                    TxtLogMeta.Text = $"{reg.DataHora:dd/MM/yyyy HH:mm} · {reg.Modalidade} · {reg.Tamanho} · {Path.GetFileName(caminho)}";
                }

                TxtLogViewer.ScrollToHome();
            }
            catch (Exception ex)
            {
                TxtLogViewer.Text = $"Erro ao ler arquivo de registro: {ex.Message}";
            }
        }

        private void BtnTabRegistrosArquivos_Click(object sender, RoutedEventArgs e)
        {
            SelecionarAbaRegistros(false);
        }

        private void BtnTabAuditoriaBase_Click(object sender, RoutedEventArgs e)
        {
            SelecionarAbaRegistros(true);
        }

        private void SelecionarAbaRegistros(bool auditoria)
        {
            var corAtiva = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#513BA6"));
            var corInativa = Brushes.Transparent;
            var textoAtivo = Brushes.White;
            var textoInativo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85859E"));

            BtnTabRegistrosArquivos.Background = auditoria ? corInativa : corAtiva;
            BtnTabRegistrosArquivos.Foreground = auditoria ? textoInativo : textoAtivo;
            BtnTabAuditoriaBase.Background = auditoria ? corAtiva : corInativa;
            BtnTabAuditoriaBase.Foreground = auditoria ? textoAtivo : textoInativo;

            SubPanelRegistrosArquivos.Visibility = auditoria ? Visibility.Collapsed : Visibility.Visible;
            SubPanelAuditoriaBase.Visibility = auditoria ? Visibility.Visible : Visibility.Collapsed;

            if (auditoria)
            {
                if (!_auditoriaCarregada) LoadAuditoria();
                else AplicarFiltroAuditoria();
            }

            AtualizarContadorAba();
        }

        private void AtualizarContadorAba()
        {
            if (TxtContadorRegistros == null) return;

            if (SubPanelAuditoriaBase != null && SubPanelAuditoriaBase.Visibility == Visibility.Visible)
            {
                TxtContadorRegistros.Text = TxtContadorAuditoria?.Text ?? "0 evento(s)";
            }
            else
            {
                TxtContadorRegistros.Text = $"{_registrosList.Count:N0} de {_todosRegistros.Count:N0} registro(s)";
            }
        }

        private void LoadAuditoria()
        {
            _auditoriaCarregada = true;
            AplicarFiltroAuditoria();
        }

        private void AplicarFiltroAuditoria()
        {
            try
            {
                string busca = (TxtBuscaAuditoria?.Text ?? string.Empty).Trim();
                string modalidade = ObterTextoCombo(CmbModalidadeAuditoria, "Todas");
                string periodo = ObterTextoCombo(CmbPeriodoAuditoria, "Tudo");
                DateTime? inicio = null;
                DateTime? fim = null;

                switch (periodo)
                {
                    case "Hoje":
                        inicio = DateTime.Today;
                        fim = DateTime.Today.AddDays(1).AddSeconds(-1);
                        break;
                    case "7 dias":
                        inicio = DateTime.Today.AddDays(-7);
                        break;
                    case "30 dias":
                        inicio = DateTime.Today.AddDays(-30);
                        break;
                }

                var lista = TACEUI.Core.AuditLogManager.LerFiltrado(inicio, fim, modalidade, busca, 5000);
                GridAuditoria.ItemsSource = lista;
                TxtContadorAuditoria.Text = $"{lista.Count:N0} evento(s)";

                if (lista.Count > 0)
                {
                    GridAuditoria.SelectedIndex = 0;
                }
                else
                {
                    TxtAuditoriaDetalhesTitulo.Text = "Nenhum evento encontrado";
                    TxtAuditoriaDetalhes.Text = "Ajuste os filtros de período, modalidade ou busca.";
                }

                AtualizarContadorAba();
            }
            catch (Exception ex)
            {
                TxtAuditoriaDetalhesTitulo.Text = "Erro ao carregar auditoria";
                TxtAuditoriaDetalhes.Text = ex.Message;
            }
        }

        private void FiltroAuditoria_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizandoFiltrosAuditoria || !IsLoaded) return;
            AplicarFiltroAuditoria();
        }

        private void GridAuditoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridAuditoria.SelectedItem is not TACEUI.Core.AuditoriaRegistro reg)
            {
                return;
            }

            TxtAuditoriaDetalhesTitulo.Text = $"{reg.Acao} · {reg.Modalidade}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Data/Hora: {reg.DataHoraFormatada}");
            sb.AppendLine($"Modalidade: {reg.Modalidade}");
            sb.AppendLine($"Ação: {reg.Acao}");
            sb.AppendLine($"Tabela: {reg.Tabela}");
            sb.AppendLine($"Registro: {reg.RegistroId}");
            sb.AppendLine($"Usuário: {reg.Usuario}");
            sb.AppendLine($"IP/Máquina: {reg.IpMaquina}");
            sb.AppendLine();
            sb.AppendLine($"Detalhes: {(string.IsNullOrWhiteSpace(reg.Detalhes) ? "—" : reg.Detalhes)}");
            TxtAuditoriaDetalhes.Text = sb.ToString().TrimEnd();
        }

        private void BtnCopiarLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtLogViewer.Text))
            {
                Clipboard.SetText(TxtLogViewer.Text);
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
                string tempFile = Path.Combine(Path.GetTempPath(), "tace_log_inspect.txt");
                File.WriteAllText(tempFile, TxtLogViewer.Text);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", tempFile) { UseShellExecute = true });
            }
        }

        private void BtnAbrirRegistroSelecionado_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_currentSelectedLogPath) && File.Exists(_currentSelectedLogPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_currentSelectedLogPath) { UseShellExecute = true });
                return;
            }

            if (ListRegistros.SelectedItem is RegistroModel reg && Directory.Exists(reg.FullPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", reg.FullPath) { UseShellExecute = true });
            }
        }

        private void BtnAbrirPastaRegistros_Click(object sender, RoutedEventArgs e)
        {
            string rootPath = TACEUI.Core.PathHelper.GetRegistrosDirectory();
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", rootPath) { UseShellExecute = true });
        }

        private void BtnRecarregarLogs_Click(object sender, RoutedEventArgs e)
        {
            LoadRegistrosLocais();
            if (_auditoriaCarregada) LoadAuditoria();
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

        private void BtnIrParaTags_Click(object sender, RoutedEventArgs e)
        {
            BtnTabRelQRCode_Click(sender, e);
        }

        private void LoadRelatorioDados()
        {
            try
            {
                _fullDbList = TACEUI.Core.SQLiteDatabaseManager.LerTodos();
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
                LblQtdSelecionados.Text = _listaSelecionados.Count == 1
                    ? "1 selecionado"
                    : $"{_listaSelecionados.Count} selecionados";
            }
        }

        private System.Collections.Generic.List<TACEUI.Core.DispositivoDB>? ObterListaExportacao(bool somenteSelecionados, bool avisarVazio)
        {
            var lista = new System.Collections.Generic.List<TACEUI.Core.DispositivoDB>();

            if (somenteSelecionados)
            {
                foreach (var item in _listaSelecionados)
                    lista.Add(item.Dispositivo);
            }
            else
            {
                var fonte = ListBuscaAparelhos.ItemsSource as System.Collections.Generic.IEnumerable<DispositivoSelecionavel>
                            ?? _listaCompletaSelecionaveis;

                foreach (var item in fonte)
                {
                    if (item?.Dispositivo == null) continue;
                    if (lista.Any(d => d.ID == item.Dispositivo.ID)) continue;
                    lista.Add(item.Dispositivo);
                }
            }

            if (lista.Count == 0 && avisarVazio)
            {
                if (somenteSelecionados)
                    MessageBox.Show("Nenhum aparelho selecionado. Marque ao menos um aparelho ou escolha a opção \"Toda a base\".", "Seleção vazia", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Nenhum aparelho encontrado para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return lista;
        }

        private static string Csv(string? valor)
        {
            return "\"" + (valor ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        // =========================================================================
        // EMISSÃO DE RELATÓRIO PDF A4 FORMATADO
        // =========================================================================
        private void BtnEmitirRelDadosPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool somenteSelecionados = RdoEscopoSelecionados?.IsChecked == true;
                var lista = ObterListaExportacao(somenteSelecionados, true);
                if (lista == null) return;

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Documento PDF (*.pdf)|*.pdf|Todos os arquivos (*.*)|*.*",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"Relatorio_Dispositivos_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    Title = "Salvar relatório executivo em PDF"
                };

                if (sfd.ShowDialog() != true) return;

                TACEUI.Core.RelatorioPdfGenerator.Gerar(
                    sfd.FileName,
                    lista,
                    somenteSelecionados,
                    TACEUI.Core.AppInfo.CurrentVersion);

                MessageBox.Show(
                    $"Relatório PDF gerado com sucesso!\n\nArquivo: {sfd.FileName}",
                    "Relatório Executivo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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
                bool somenteSelecionados = RdoEscopoSelecionados?.IsChecked == true;
                var lista = ObterListaExportacao(somenteSelecionados, true);
                if (lista == null) return;

                await ExportarCsvAsync(lista);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao exportar planilha: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnExportarExcelBanco_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = (GridBancoDados.ItemsSource as System.Collections.Generic.IEnumerable<TACEUI.Core.DispositivoDB>)?.ToList()
                            ?? new System.Collections.Generic.List<TACEUI.Core.DispositivoDB>(_bancoDadosCache);

                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum dado para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await ExportarCsvAsync(lista);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao exportar planilha: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task ExportarCsvAsync(System.Collections.Generic.List<TACEUI.Core.DispositivoDB> lista)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Planilha CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = $"Relatorio_Dispositivos_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ID;Patrimonio;Fabricante;Modelo;Serial;MAC");
            foreach (var d in lista)
            {
                sb.AppendLine($"{d.ID};{Csv(d.Patrimonio)};{Csv(d.Fabricante)};{Csv(d.Modelo)};{Csv(d.Serial)};{Csv(d.MAC)}");
            }

            await File.WriteAllTextAsync(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
            MessageBox.Show($"Exportação concluída com sucesso!\nArquivo: {sfd.FileName}", "Exportação Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================================
        // ETIQUETAS E TAGS QR CODE PATRIMONIAIS
        // =========================================================================
        private void BtnGerarEtiquetas_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool somenteSelecionados = RdoTagSelecionados?.IsChecked == true;
                var lista = ObterListaExportacao(somenteSelecionados, true);
                if (lista == null || lista.Count == 0) return;

                string outDir = Path.Combine(
                    TACEUI.Core.PathHelper.GetRootDirectory(),
                    "data", "output", "etiquetas",
                    $"Lote_{DateTime.Now:yyyyMMdd_HHmmss}");

                Directory.CreateDirectory(outDir);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
                sb.AppendLine("<title>Etiquetas QR Code - TACE</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("@page { size: A4; margin: 7mm; }");
                sb.AppendLine("* { box-sizing: border-box; }");
                sb.AppendLine("body { margin: 0; background: #f0f0f0; font-family: 'Segoe UI', Arial, sans-serif; }");
                sb.AppendLine(".sheet { display: grid; grid-template-columns: repeat(3, 60mm); gap: 4mm; padding: 4mm; }");
                sb.AppendLine(".label { width: 60mm; height: 30mm; background: #fff; border: 1px solid #b8b8b8; display: flex; overflow: hidden; page-break-inside: avoid; }");
                sb.AppendLine(".qr { width: 30mm; height: 30mm; padding: 2mm; flex: 0 0 30mm; }");
                sb.AppendLine(".qr img { width: 100%; height: 100%; image-rendering: pixelated; }");
                sb.AppendLine(".info { flex: 1; padding: 2mm 2mm 2mm 0; display: flex; flex-direction: column; justify-content: center; min-width: 0; }");
                sb.AppendLine(".brand { font-size: 6pt; color: #513BA6; font-weight: 700; letter-spacing: .4px; }");
                sb.AppendLine(".pat { font-size: 11pt; font-weight: 700; color: #111; margin: .6mm 0; }");
                sb.AppendLine(".mod { font-size: 6.5pt; font-weight: 600; color: #222; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }");
                sb.AppendLine(".meta { font-size: 5.5pt; color: #444; font-family: Consolas, monospace; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }");
                sb.AppendLine("@media print { body { background: #fff; } .sheet { padding: 0; } .label { border: 1px dashed #c8c8c8; } }");
                sb.AppendLine("</style></head><body><div class=\"sheet\">");

                using (var qrGenerator = new QRCoder.QRCodeGenerator())
                {
                    for (int i = 0; i < lista.Count; i++)
                    {
                        var d = lista[i];
                        string patrimonio = string.IsNullOrWhiteSpace(d.Patrimonio) ? "-" : d.Patrimonio;
                        string modelo = string.IsNullOrWhiteSpace(d.MarcaModelo) ? "-" : d.MarcaModelo;
                        string serial = string.IsNullOrWhiteSpace(d.Serial) ? "-" : d.Serial;
                        string mac = string.IsNullOrWhiteSpace(d.MAC) ? "-" : d.MAC;

                        string payload = $"TACE|ID={d.ID}|PAT={patrimonio}|MOD={modelo}|SN={serial}|MAC={mac}";
                        using (var qrData = qrGenerator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q))
                        {
                            var png = new QRCoder.PngByteQRCode(qrData).GetGraphic(12);
                            string imgName = $"qr_{i + 1:D4}.png";
                            File.WriteAllBytes(Path.Combine(outDir, imgName), png);

                            sb.AppendLine("<div class=\"label\">");
                            sb.AppendLine($"<div class=\"qr\"><img src=\"{imgName}\" alt=\"QR\"></div>");
                            sb.AppendLine("<div class=\"info\">");
                            sb.AppendLine("<div class=\"brand\">TACE · PATRIMÔNIO</div>");
                            sb.AppendLine($"<div class=\"pat\">{System.Net.WebUtility.HtmlEncode(patrimonio)}</div>");
                            sb.AppendLine($"<div class=\"mod\">{System.Net.WebUtility.HtmlEncode(modelo)}</div>");
                            sb.AppendLine($"<div class=\"meta\">S/N: {System.Net.WebUtility.HtmlEncode(serial)}</div>");
                            sb.AppendLine($"<div class=\"meta\">MAC: {System.Net.WebUtility.HtmlEncode(mac)}</div>");
                            sb.AppendLine("</div></div>");
                        }
                    }
                }

                sb.AppendLine("</div></body></html>");

                string htmlPath = Path.Combine(outDir, "etiquetas.html");
                File.WriteAllText(htmlPath, sb.ToString(), new System.Text.UTF8Encoding(true));

                MessageBox.Show(
                    $"Etiquetas geradas para {lista.Count} aparelho(s).\nPasta: {outDir}\n\nA folha será aberta no navegador para impressão.",
                    "Etiquetas QR Code",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = htmlPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao gerar etiquetas: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFiltrarParaTags_Click(object sender, RoutedEventArgs e)
        {
            BtnTabRelDados_Click(sender, e);

            if (TxtBuscaAparelho != null)
            {
                TxtBuscaAparelho.Focus();
                TxtBuscaAparelho.SelectAll();
            }
        }

        // =========================================================================
        // OBSERVADOR DE BANCO DE DADOS (AUTO-REFRESH)
        // =========================================================================
        private void ConfigurarObservadorDB()
        {
            try
            {
                string dbPath = TACEUI.Core.SQLiteDatabaseManager.GetDatabasePath();
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
