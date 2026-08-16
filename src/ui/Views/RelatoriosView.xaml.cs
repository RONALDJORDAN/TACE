using System;
using System.Collections.ObjectModel;
using System.IO;
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
        
        // Carrinho de selecao
        private ObservableCollection<DispositivoSelecionavel> _listaCompletaSelecionaveis;
        private ObservableCollection<DispositivoSelecionavel> _listaSelecionados;

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

            ConfigurarObservadorDB();
            AtualizarDashboardMetrics();
        }

        public void AtualizarDashboardMetrics()
        {
            try
            {
                var todosDispositivos = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                int total = todosDispositivos.Count;
                int totalV7 = System.Linq.Enumerable.Count(todosDispositivos, d => 
                    (d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Vision", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("v7_", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (d.Serial != null && (d.Serial.IndexOf("T30764", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T030764", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T307G", StringComparison.OrdinalIgnoreCase) >= 0 || d.Serial.IndexOf("T0307G", StringComparison.OrdinalIgnoreCase) >= 0))
                );
                int totalM7 = System.Linq.Enumerable.Count(todosDispositivos, d => d.MarcaModelo != null && (d.MarcaModelo.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("f7_", StringComparison.OrdinalIgnoreCase) >= 0 || d.MarcaModelo.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0));
                
                if (DashTotalDispositivos != null) DashTotalDispositivos.Text = total.ToString("N0");
                if (DashTotalVision7 != null) DashTotalVision7.Text = totalV7.ToString("N0");
                if (DashTotalMultiM7 != null) DashTotalMultiM7.Text = totalM7.ToString("N0");

                string rootPath = System.IO.Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "registros");
                int totalLogs = 0;
                if (Directory.Exists(rootPath))
                {
                    totalLogs = Directory.GetDirectories(rootPath).Length;
                }
                if (DashTotalRegistros != null) DashTotalRegistros.Text = totalLogs.ToString("N0");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RelatoriosView] Erro ao atualizar dashboard: {ex.Message}");
            }
        }

        private void ConfigurarObservadorDB()
        {
            try
            {
                string dbPath = InnovPlayUI.Core.SQLiteDatabaseManager.GetDatabasePath();
                string? dbDir = System.IO.Path.GetDirectoryName(dbPath);
                string dbFile = System.IO.Path.GetFileName(dbPath);

                if (System.IO.Directory.Exists(dbDir))
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

            await System.Threading.Tasks.Task.Delay(500); // Aguarda a liberação do arquivo pelo SQLite

            Dispatcher.Invoke(() =>
            {
                AtualizarDashboardMetrics();
                if (PanelBancoDados.Visibility == Visibility.Visible)
                {
                    LoadBancoDadosDedicado();
                }
                else if (PanelRelatorios.Visibility == Visibility.Visible)
                {
                    LoadRelatorioDados();
                }
            });

            _isRefreshingDb = false;
        }

        // =====================================================================
        // NAVEGACAO ENTRE SUBMODULOS
        // =====================================================================
        private void ResetSubMenuStyles()
        {
            var brushNormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
            var bgNormal = new SolidColorBrush(Colors.Transparent);

            BtnSubRelatorios.Foreground = brushNormal;
            BtnSubRelatorios.Background = bgNormal;
            BtnSubBancoDados.Foreground = brushNormal;
            BtnSubBancoDados.Background = bgNormal;
            BtnSubRegistros.Foreground = brushNormal;
            BtnSubRegistros.Background = bgNormal;

            PanelRelatorios.Visibility = Visibility.Collapsed;
            PanelBancoDados.Visibility = Visibility.Collapsed;
            PanelRegistros.Visibility = Visibility.Collapsed;
        }

        private void SetActiveMenu(Button btn, Border panel)
        {
            ResetSubMenuStyles();
            var brushActive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EDEA3"));
            var bgActive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A40"));
            btn.Foreground = brushActive;
            btn.Background = bgActive;
            panel.Visibility = Visibility.Visible;
        }

        private void BtnSubRelatorios_Click(object sender, RoutedEventArgs e)
        {
            SetActiveMenu(BtnSubRelatorios, PanelRelatorios);
            LoadRelatorioDados();
        }

        private void BtnSubBancoDados_Click(object sender, RoutedEventArgs e)
        {
            SetActiveMenu(BtnSubBancoDados, PanelBancoDados);
            LoadBancoDadosDedicado();
        }

        private void BtnSubRegistros_Click(object sender, RoutedEventArgs e)
        {
            SetActiveMenu(BtnSubRegistros, PanelRegistros);
            LoadRegistrosLocais();
        }

        // =====================================================================
        // RELATORIOS DE OPERACAO — carrega stats e popula filtro
        // =====================================================================
        private void LoadRelatorioDados()
        {
            try
            {
                _fullDbList = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();

                _listaCompletaSelecionaveis.Clear();
                foreach (var d in _fullDbList)
                {
                    var ds = new DispositivoSelecionavel { Dispositivo = d, IsSelected = false };
                    ds.PropertyChanged += Ds_PropertyChanged;
                    _listaCompletaSelecionaveis.Add(ds);
                }

                ListBuscaAparelhos.ItemsSource = _listaCompletaSelecionaveis;
                
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(ListBuscaAparelhos.ItemsSource);
                if (view != null)
                {
                    view.Filter = item =>
                    {
                        if (string.IsNullOrEmpty(TxtBuscaAparelho.Text)) return true;
                        var ds = item as DispositivoSelecionavel;
                        if (ds == null) return true;
                        
                        string txt = TxtBuscaAparelho.Text.ToLower();
                        return (ds.Dispositivo.Serial != null && ds.Dispositivo.Serial.ToLower().Contains(txt)) ||
                               (ds.Dispositivo.MAC != null && ds.Dispositivo.MAC.ToLower().Contains(txt));
                    };
                }

                ListSelecionados.ItemsSource = _listaSelecionados;
                AtualizarCountSelecionados();

                PanelMenuCards.Visibility = Visibility.Visible;
                PanelEmissaoDados.Visibility = Visibility.Collapsed;
                PanelEmissaoQRCode.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private void TxtBuscaAparelho_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(ListBuscaAparelhos.ItemsSource);
            view?.Refresh();
        }

        private void Ds_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected")
            {
                var ds = sender as DispositivoSelecionavel;
                if (ds != null)
                {
                    if (ds.IsSelected && !_listaSelecionados.Contains(ds))
                        _listaSelecionados.Add(ds);
                    else if (!ds.IsSelected && _listaSelecionados.Contains(ds))
                        _listaSelecionados.Remove(ds);
                        
                    AtualizarCountSelecionados();
                }
            }
        }

        private void AtualizarCountSelecionados()
        {
            if (LblQtdSelecionados != null)
                LblQtdSelecionados.Text = $"{_listaSelecionados.Count} aparelhos selecionados";
        }

        private void ChkSelecionarTodos_Click(object sender, RoutedEventArgs e)
        {
            var chk = sender as CheckBox;
            bool check = chk?.IsChecked == true;
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(ListBuscaAparelhos.ItemsSource);
            if (view != null)
            {
                foreach (DispositivoSelecionavel item in view)
                {
                    item.IsSelected = check;
                }
            }
        }

        private void ChkItem_Click(object sender, RoutedEventArgs e)
        {
            // Triggers property changed via binding
        }

        private void BtnRemoverSelecionado_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var ds = btn?.DataContext as DispositivoSelecionavel;
            if (ds != null)
            {
                ds.IsSelected = false; // Isso ja aciona o PropertyChanged e remove da lista de selecionados
            }
        }

        // =====================================================================
        // NAVEGAÇÃO ENTRE CARDS E PAINÉIS DE EMISSÃO
        // =====================================================================
        private void BtnCardRelDados_Click(object sender, RoutedEventArgs e)
        {
            PanelMenuCards.Visibility = Visibility.Collapsed;
            PanelEmissaoDados.Visibility = Visibility.Visible;
            PanelEmissaoQRCode.Visibility = Visibility.Collapsed;
        }

        private void BtnCardRelQRCode_Click(object sender, RoutedEventArgs e)
        {
            PanelMenuCards.Visibility = Visibility.Collapsed;
            PanelEmissaoDados.Visibility = Visibility.Collapsed;
            PanelEmissaoQRCode.Visibility = Visibility.Visible;
        }

        private void BtnVoltarMenuCards_Click(object sender, RoutedEventArgs e)
        {
            PanelMenuCards.Visibility = Visibility.Visible;
            PanelEmissaoDados.Visibility = Visibility.Collapsed;
            PanelEmissaoQRCode.Visibility = Visibility.Collapsed;
        }

        // =====================================================================
        // EMITIR PDF DO RELATORIO DE DADOS
        // =====================================================================
        private void BtnEmitirRelDadosPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
                
                if (PanelBancoDados.Visibility == Visibility.Visible)
                {
                    if (GridBancoDados.SelectedItems.Count > 0)
                    {
                        foreach (InnovPlayUI.Core.DispositivoDB ds in GridBancoDados.SelectedItems)
                            lista.Add(ds);
                    }
                }
                else if (PanelRelatorios.Visibility == Visibility.Visible)
                {
                    if (_listaSelecionados.Count > 0)
                    {
                        foreach (var ds in _listaSelecionados)
                            lista.Add(ds.Dispositivo);
                    }
                }
                else
                {
                    // Fallback para toda a base
                    var res = MessageBox.Show("Nenhum aparelho selecionado. Deseja emitir o relatorio com todos os registros da base?", "Confirmação", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    
                    lista = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                }

                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum dado para emitir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() != true) return;

                System.Windows.Documents.FlowDocument doc = new System.Windows.Documents.FlowDocument();
                doc.PagePadding = new Thickness(60, 50, 60, 50);
                doc.ColumnWidth = pd.PrintableAreaWidth - 120;
                doc.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

                // Titulo
                var title = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run("Relatorio de Dispositivos - Plataforma InnovTech"));
                title.FontSize = 20; title.FontWeight = FontWeights.Bold;
                title.TextAlignment = TextAlignment.Center;
                title.Margin = new Thickness(0, 0, 0, 6);
                doc.Blocks.Add(title);

                // Subtitulo
                string strFiltro = _listaSelecionados.Count > 0 ? "Multiplos Selecionados" : "Todos";
                var sub = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run(
                        $"Filtro: {strFiltro} | Total: {lista.Count} dispositivo(s) | Emitido em: {DateTime.Now:dd/MM/yyyy HH:mm}"));
                sub.FontSize = 11; sub.Foreground = System.Windows.Media.Brushes.Gray;
                sub.TextAlignment = TextAlignment.Center;
                sub.Margin = new Thickness(0, 0, 0, 20);
                doc.Blocks.Add(sub);

                // Tabela
                var table = new System.Windows.Documents.Table();
                double[] widths = { 40, 160, 150, 150, 110 };
                foreach (var w in widths)
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(w) });

                var rg = new System.Windows.Documents.TableRowGroup();
                table.RowGroups.Add(rg);

                // Header
                string[] headers = { "ID", "Marca / Modelo", "N/S (Serial)", "MAC Address", "Data Registro" };
                var hr = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.DimGray };
                foreach (var h in headers)
                {
                    var hcell = new System.Windows.Documents.TableCell(
                        new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontWeight = FontWeights.Bold, FontSize = 11,
                            Foreground = System.Windows.Media.Brushes.White,
                            Padding = new Thickness(6, 4, 6, 4)
                        });
                    hr.Cells.Add(hcell);
                }
                rg.Rows.Add(hr);

                // Linhas de dados
                bool alt = false;
                foreach (var d in lista)
                {
                    var row = new System.Windows.Documents.TableRow
                    {
                        Background = alt
                            ? System.Windows.Media.Brushes.WhiteSmoke
                            : System.Windows.Media.Brushes.White
                    };
                    alt = !alt;
                    string[] vals = { d.ID.ToString(), d.MarcaModelo, d.Serial, d.MAC, d.DataRegistro };
                    foreach (var v in vals)
                    {
                        var dcell = new System.Windows.Documents.TableCell(
                            new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(v ?? "-"))
                            { FontSize = 10, Padding = new Thickness(6, 3, 6, 3) });
                        row.Cells.Add(dcell);
                    }
                    rg.Rows.Add(row);
                }
                doc.Blocks.Add(table);

                // Rodape
                var footer = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run(
                        $"InnovTech \u2014 Gerado automaticamente em {DateTime.Now:dd/MM/yyyy HH:mm:ss}"));
                footer.FontSize = 9;
                footer.Foreground = System.Windows.Media.Brushes.Gray;
                footer.TextAlignment = TextAlignment.Right;
                footer.Margin = new Thickness(0, 20, 0, 0);
                doc.Blocks.Add(footer);

                pd.PrintDocument(
                    ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator,
                    "Relatorio de Dados - InnovTech");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao emitir PDF: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        // EXPORTAR EXCEL (CSV)
        // =====================================================================
        private async void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
                
                if (PanelBancoDados.Visibility == Visibility.Visible)
                {
                    if (GridBancoDados.SelectedItems.Count > 0)
                    {
                        foreach (InnovPlayUI.Core.DispositivoDB ds in GridBancoDados.SelectedItems)
                            lista.Add(ds);
                    }
                }
                else if (PanelRelatorios.Visibility == Visibility.Visible)
                {
                    if (_listaSelecionados.Count > 0)
                    {
                        foreach (var ds in _listaSelecionados)
                            lista.Add(ds.Dispositivo);
                    }
                }
                else
                {
                    // Fallback para toda a base
                    var res = MessageBox.Show("Nenhum aparelho selecionado. Deseja exportar todos os registros da base?", "Confirmação", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    
                    lista = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                }

                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum dado para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "Relatorio_Dispositivos",
                    DefaultExt = ".csv",
                    Filter = "Planilha Excel (CSV)|*.csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    string localPath = dlg.FileName;
                    using (StreamWriter sw = new StreamWriter(localPath, false, System.Text.Encoding.UTF8))
                    {
                        sw.WriteLine("ID;Modelo/Marca;Numero de Serie;MAC Address;Data de Registro");
                        foreach (var d in lista)
                            sw.WriteLine($"{d.ID};{d.MarcaModelo};{d.Serial};{d.MAC};{d.DataRegistro}");
                    }

                    // Push via ADB assincronamente (evita travamento da interface)
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var p = new System.Diagnostics.Process();
                        p.StartInfo.FileName = "adb";
                        string fileName = System.IO.Path.GetFileName(localPath);
                        p.StartInfo.Arguments = $"push \"{localPath}\" /sdcard/Download/{fileName}";
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.RedirectStandardError = true;
                        p.Start();
                        p.WaitForExit();

                        Dispatcher.Invoke(() =>
                        {
                            if (p.ExitCode == 0)
                            {
                                MessageBox.Show("Arquivo CSV salvo no computador e transferido com sucesso para o armazenamento interno do dispositivo (pasta Download)!", "Exportacao Concluida", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                string err = p.StandardError.ReadToEnd();
                                if (string.IsNullOrWhiteSpace(err)) err = "Nenhum dispositivo detectado.";
                                MessageBox.Show($"O arquivo foi salvo em: {localPath}\n\nPorem nao foi possivel salvar no dispositivo via ADB:\n{err}", "Aviso ADB", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao exportar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        // SELECIONAR MODELO PARA TAGS — redireciona para Banco de Dados
        // =====================================================================
        private void BtnFiltrarParaTags_Click(object sender, RoutedEventArgs e)
        {
            SetActiveMenu(BtnSubBancoDados, PanelBancoDados);
            LoadBancoDadosDedicado();
            MessageBox.Show(
                "Filtre por modelo e selecione os dispositivos desejados.\nEntao volte a Relatorios de Operacao e clique em \"Imprimir Tags QR\".",
                "Selecionar Dispositivos para Tags", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =====================================================================
        // GERAR ETIQUETAS QR CODE — formato HORIZONTAL 6cm x 3cm
        // QR Code no lado ESQUERDO | Informacoes no lado DIREITO
        // =====================================================================
        private async void BtnGerarEtiquetas_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lista = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
                
                if (PanelBancoDados.Visibility == Visibility.Visible)
                {
                    if (GridBancoDados.SelectedItems.Count > 0)
                    {
                        foreach (InnovPlayUI.Core.DispositivoDB ds in GridBancoDados.SelectedItems)
                            lista.Add(ds);
                    }
                }
                else if (PanelRelatorios.Visibility == Visibility.Visible)
                {
                    if (_listaSelecionados.Count > 0)
                    {
                        foreach (var ds in _listaSelecionados)
                            lista.Add(ds.Dispositivo);
                    }
                }
                else
                {
                    var res = MessageBox.Show("Nenhum aparelho selecionado. Deseja gerar etiquetas para todos os registros da base?", "Confirmação", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    
                    lista = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();
                }

                if (lista.Count == 0)
                {
                    MessageBox.Show("Nenhum dispositivo para gerar etiquetas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Usando PdfSharpCore para gerar PDF
                PdfSharpCore.Pdf.PdfDocument document = new PdfSharpCore.Pdf.PdfDocument();
                document.Info.Title = "Etiquetas Patrimoniais QR Code";

                PdfSharpCore.Pdf.PdfPage page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                PdfSharpCore.Drawing.XGraphics gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);

                PdfSharpCore.Drawing.XFont fontTitle = new PdfSharpCore.Drawing.XFont("Segoe UI", 16, PdfSharpCore.Drawing.XFontStyle.Bold);
                PdfSharpCore.Drawing.XFont fontSub = new PdfSharpCore.Drawing.XFont("Segoe UI", 10, PdfSharpCore.Drawing.XFontStyle.Regular);
                
                PdfSharpCore.Drawing.XFont fontTagTitle = new PdfSharpCore.Drawing.XFont("Segoe UI", 10, PdfSharpCore.Drawing.XFontStyle.Bold);
                PdfSharpCore.Drawing.XFont fontTagLabel = new PdfSharpCore.Drawing.XFont("Segoe UI", 8, PdfSharpCore.Drawing.XFontStyle.Bold);
                PdfSharpCore.Drawing.XFont fontTagValue = new PdfSharpCore.Drawing.XFont("Consolas", 8, PdfSharpCore.Drawing.XFontStyle.Regular);
                PdfSharpCore.Drawing.XFont fontTagSmall = new PdfSharpCore.Drawing.XFont("Segoe UI", 7, PdfSharpCore.Drawing.XFontStyle.Regular);
                PdfSharpCore.Drawing.XFont fontBadge = new PdfSharpCore.Drawing.XFont("Segoe UI", 7, PdfSharpCore.Drawing.XFontStyle.Bold);

                gfx.DrawString("Etiquetas Patrimoniais - InnovTech", fontTitle, PdfSharpCore.Drawing.XBrushes.Black, new PdfSharpCore.Drawing.XRect(0, 40, page.Width, 20), PdfSharpCore.Drawing.XStringFormats.Center);
                
                string strFiltro = _listaSelecionados.Count > 0 ? "Multiplos Selecionados" : "Todos";
                gfx.DrawString($"{lista.Count} etiqueta(s) | Filtro: {strFiltro} | Emitido em: {DateTime.Now:dd/MM/yyyy HH:mm}", fontSub, PdfSharpCore.Drawing.XBrushes.Gray, new PdfSharpCore.Drawing.XRect(0, 60, page.Width, 15), PdfSharpCore.Drawing.XStringFormats.Center);

                double tagW = PdfSharpCore.Drawing.XUnit.FromCentimeter(6).Point;
                double tagH = PdfSharpCore.Drawing.XUnit.FromCentimeter(3).Point;

                double marginX = 40;
                double marginY = 100;
                double x = marginX;
                double y = marginY;

                QRCoder.QRCodeGenerator qrGen = new QRCoder.QRCodeGenerator();

                foreach (var d in lista)
                {
                    if (x + tagW > page.Width - marginX)
                    {
                        x = marginX;
                        y += tagH + 15;
                    }
                    if (y + tagH > page.Height - marginX)
                    {
                        page = document.AddPage();
                        page.Size = PdfSharpCore.PageSize.A4;
                        gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                        x = marginX;
                        y = 40;
                    }

                    // Borda da etiqueta
                    gfx.DrawRectangle(PdfSharpCore.Drawing.XPens.Black, PdfSharpCore.Drawing.XBrushes.White, x, y, tagW, tagH);

                    string payload = $"ID:{d.ID}|MOD:{d.MarcaModelo}|SER:{d.Serial}|MAC:{d.MAC}";
                    QRCoder.QRCodeData qrData = qrGen.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.M);
                    QRCoder.PngByteQRCode qrCode = new QRCoder.PngByteQRCode(qrData);
                    byte[] qrBytes = qrCode.GetGraphic(4);

                    using (var ms = new System.IO.MemoryStream(qrBytes))
                    {
                        PdfSharpCore.Drawing.XImage qrImg = PdfSharpCore.Drawing.XImage.FromStream(() => ms);
                        double qrSize = tagH - 10;
                        gfx.DrawImage(qrImg, x + 5, y + 5, qrSize, qrSize);
                        
                        double divX = x + qrSize + 10;
                        gfx.DrawLine(PdfSharpCore.Drawing.XPens.LightGray, divX, y + 5, divX, y + tagH - 5);
                        
                        double textX = divX + 5;
                        double textY = y + 15;
                        
                        string marca = (d.MarcaModelo ?? "-").ToUpper();
                        if (marca.Length > 20) marca = marca.Substring(0, 20);
                        
                        gfx.DrawString(marca, fontTagTitle, PdfSharpCore.Drawing.XBrushes.Black, textX, textY); textY += 12;
                        
                        gfx.DrawString("N/S: ", fontTagLabel, PdfSharpCore.Drawing.XBrushes.Black, textX, textY);
                        gfx.DrawString(d.Serial ?? "-", fontTagValue, PdfSharpCore.Drawing.XBrushes.Black, textX + 22, textY); textY += 12;
                        
                        gfx.DrawString("MAC: ", fontTagLabel, PdfSharpCore.Drawing.XBrushes.Black, textX, textY);
                        gfx.DrawString(d.MAC ?? "-", fontTagValue, PdfSharpCore.Drawing.XBrushes.Black, textX + 22, textY); textY += 12;
                        
                        gfx.DrawString($"Reg: {d.DataRegistro}", fontTagSmall, PdfSharpCore.Drawing.XBrushes.Gray, textX, textY); textY += 12;
                        
                        // Badge InnovTech
                        PdfSharpCore.Drawing.XColor badgeColor = PdfSharpCore.Drawing.XColor.FromArgb(81, 59, 166);
                        gfx.DrawRectangle(new PdfSharpCore.Drawing.XSolidBrush(badgeColor), textX, textY - 8, 45, 10);
                        gfx.DrawString("InnovTech", fontBadge, PdfSharpCore.Drawing.XBrushes.White, textX + 3, textY);
                    }

                    x += tagW + 15;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "Etiquetas_QRCode",
                    DefaultExt = ".pdf",
                    Filter = "Documento PDF (.pdf)|*.pdf"
                };

                if (dlg.ShowDialog() == true)
                {
                    string localPath = dlg.FileName;
                    document.Save(localPath);

                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var p = new System.Diagnostics.Process();
                        p.StartInfo.FileName = "adb";
                        string fileName = System.IO.Path.GetFileName(localPath);
                        p.StartInfo.Arguments = $"push \"{localPath}\" /sdcard/Download/{fileName}";
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.RedirectStandardError = true;
                        p.Start();
                        p.WaitForExit();

                        Dispatcher.Invoke(() =>
                        {
                            if (p.ExitCode == 0)
                            {
                                MessageBox.Show($"As Etiquetas em PDF foram salvas no computador e tambem no armazenamento interno do dispositivo (pasta Download)!", "Geracao Concluida", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                string err = p.StandardError.ReadToEnd();
                                if (string.IsNullOrWhiteSpace(err)) err = "Nenhum dispositivo conectado para receber o arquivo via ADB.";
                                MessageBox.Show($"O PDF foi gerado em: {localPath}\n\nPorem falhou ao enviar via ADB para o celular:\n{err}", "Aviso ADB", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao gerar as etiquetas: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        // BANCO DE DADOS DIRETO (ListDispositivosDB — legado, mantido para compatibilidade)
        // =====================================================================
        private void BtnSalvarBD_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var source = GridBancoDados.ItemsSource as System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>
                             ?? (_fullDbList);
                InnovPlayUI.Core.SQLiteDatabaseManager.SalvarTodos(source);
                InnovPlayUI.Core.SQLiteDatabaseManager.InvalidarCache();
                MessageBox.Show("Informacoes atualizadas e salvas com sucesso!", "Registros Atualizados", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadBancoDadosDedicado();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int _editingDeviceId = 0;

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
                AtualizarDashboardMetrics();
                MessageBox.Show("Dispositivo salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var res2 = MessageBox.Show("Esta ação é irreversível e removerá o(s) aparelho(s) permanentemente. Deseja realmente prosseguir?", "Exclusão Definitiva", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res2 == MessageBoxResult.Yes)
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
                        AtualizarDashboardMetrics();
                        MessageBox.Show($"{count} dispositivo(s) excluído(s) com sucesso.", "Exclusão Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Erro ao excluir: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // =====================================================================
        // BANCO DE DADOS DEDICADO
        // =====================================================================
        private System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB> _bancoDadosCache
            = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
        private string _buscaAtualBD = string.Empty;
        private bool _buscaPlaceholderAtivo = true;

        private void LoadBancoDadosDedicado()
        {
            try
            {
                _bancoDadosCache = InnovPlayUI.Core.SQLiteDatabaseManager.LerTodos();

                var modelos = new System.Collections.Generic.List<string> { "Todos os Modelos" };
                foreach (var d in _bancoDadosCache)
                    if (!modelos.Contains(d.MarcaModelo) && !string.IsNullOrWhiteSpace(d.MarcaModelo))
                        modelos.Add(d.MarcaModelo);

                ComboBancoDadosFiltro.ItemsSource = modelos;
                ComboBancoDadosFiltro.SelectedIndex = 0;

                AplicarFiltroBD();
            }
            catch { }
        }

        private void AplicarFiltroBD()
        {
            var resultado = new System.Collections.Generic.List<InnovPlayUI.Core.DispositivoDB>();
            string modeloFiltro = ComboBancoDadosFiltro.SelectedItem?.ToString() ?? "Todos os Modelos";
            string busca = _buscaPlaceholderAtivo ? string.Empty : (_buscaAtualBD ?? string.Empty).ToLower();

            foreach (var d in _bancoDadosCache)
            {
                bool passaModelo = modeloFiltro == "Todos os Modelos" || d.MarcaModelo == modeloFiltro;
                bool passaBusca = string.IsNullOrEmpty(busca)
                    || (d.Serial?.ToLower().Contains(busca) == true)
                    || (d.MarcaModelo?.ToLower().Contains(busca) == true)
                    || (d.MAC?.ToLower().Contains(busca) == true);

                if (passaModelo && passaBusca)
                    resultado.Add(d);
            }

            GridBancoDados.ItemsSource = resultado;
            TxtContadorBD.Text = $"{resultado.Count} registro{(resultado.Count != 1 ? "s" : "")}"
                                 + (_bancoDadosCache.Count != resultado.Count ? $" de {_bancoDadosCache.Count}" : "");
            TxtInfoSelecionado.Text = "Nenhum registro selecionado";
        }

        private void ComboBancoDadosFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltroBD();
        }

        private void TxtBuscaBD_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_buscaPlaceholderAtivo)
            {
                TxtBuscaBD.Text = string.Empty;
                TxtBuscaBD.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3E0F2"));
                _buscaPlaceholderAtivo = false;
            }
        }

        private void TxtBuscaBD_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtBuscaBD.Text))
            {
                _buscaPlaceholderAtivo = true;
                TxtBuscaBD.Text = (string)TxtBuscaBD.Tag;
                TxtBuscaBD.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404C4F"));
            }
        }

        private void TxtBuscaBD_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_buscaPlaceholderAtivo) return;
            _buscaAtualBD = TxtBuscaBD.Text;
            AplicarFiltroBD();
        }

        private void GridBancoDados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = GridBancoDados.SelectedItem as InnovPlayUI.Core.DispositivoDB;
            if (item == null)
            {
                TxtInfoSelecionado.Text = "Nenhum registro selecionado";
                return;
            }
            TxtInfoSelecionado.Text =
                $"Selecionado: #{item.ID} \u2014 {item.MarcaModelo} | N/S: {item.Serial} | MAC: {item.MAC} | Registrado em: {item.DataRegistro}";
        }

        // =====================================================================
        // REGISTROS DE AUDITORIA
        // =====================================================================
        private void LoadRegistrosLocais()
        {
            try
            {
                _registrosList.Clear();
                string rootPath = System.IO.Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "registros");

                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                var dirs = Directory.GetDirectories(rootPath);
                foreach (var d in dirs)
                {
                    var info = new System.IO.DirectoryInfo(d);
                    _registrosList.Add(new RegistroModel
                    {
                        Data = info.CreationTime.ToString("yyyy-MM-dd"),
                        Nome = info.Name,
                        Tipo = "Pasta de Auditoria"
                    });
                    foreach (var sub in Directory.GetDirectories(d))
                    {
                        var subInfo = new System.IO.DirectoryInfo(sub);
                        _registrosList.Add(new RegistroModel
                        {
                            Data = subInfo.CreationTime.ToString("yyyy-MM-dd HH:mm"),
                            Nome = "  \u21aa " + subInfo.Name,
                            Tipo = "Registro Físico"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _registrosList.Add(new RegistroModel { Nome = "Erro ao carregar registros: " + ex.Message });
            }
        }

        // Mantido para compatibilidade de XAML legado
        private void ListDispositivosDB_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }
}
