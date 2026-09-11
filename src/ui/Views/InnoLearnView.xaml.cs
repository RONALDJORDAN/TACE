using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

// ============================================================
// InnoLearnView — Painel de Estatísticas de Aprendizado
// Exibe scores, acertos e falhas de cada estratégia de UI,
// além do controle de reset com confirmação dupla.
// Autor: Jordan Araujo | InnovPlay
// ============================================================

using System.Windows.Data;

namespace InnovPlayUI.Views
{
    // --- Conversores para Progresso de Grupo ---
    public class GroupProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<object> items && items.Count > 0)
            {
                double total = 0;
                foreach (LearnStrategyItem item in items) total += item.Progresso;
                return total / items.Count;
            }
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    public class GroupProgressStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<object> items && items.Count > 0)
            {
                double total = 0;
                foreach (LearnStrategyItem item in items) total += item.Progresso;
                return $"{(total / items.Count):F0}%";
            }
            return "0%";
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    public class GroupProgressBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<object> items && items.Count > 0)
            {
                double total = 0;
                foreach (LearnStrategyItem item in items) total += item.Progresso;
                double avg = total / items.Count;
                var converter = new ScoreToBrushConverter();
                return converter.Convert(avg, targetType, parameter, culture);
            }
            return System.Windows.Media.Brushes.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    public class ScoreToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double or int)
            {
                double score = System.Convert.ToDouble(value);
                if (score == 0) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)); // Branco
                
                if (score > 0)
                {
                    if (score >= 80) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 222, 163)); // #4EDEA3 (Verde principal)
                    if (score >= 40) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 200, 150));
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 220, 180));
                }
                else
                {
                    if (score <= -80) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 76, 76)); // #FF4C4C (Vermelho principal)
                    if (score <= -40) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 120, 76)); // Laranja
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 76)); // Amarelo
                }
            }
            return System.Windows.Media.Brushes.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    // --- Modelo de linha da tabela ---
    public class LearnStrategyItem
    {
        public string RawTargetId { get; set; } = string.Empty;
        public string Categoria   { get; set; } = string.Empty;
        public string ElementoId  { get; set; } = string.Empty;
        public string Query       { get; set; } = string.Empty;
        public int    Score       { get; set; }
        public int    Hits        { get; set; }
        public int    Misses      { get; set; }

        // Taxa de precisão calculada na leitura
        public string Precisao
        {
            get
            {
                int total = Hits + Misses;
                if (total == 0) return "—";
                double rate = (double)Hits / total * 100.0;
                return $"{rate:F1}%";
            }
        }

        // Progresso de Aprendizado (Agora direto de -100 a +100)
        public double Progresso
        {
            get
            {
                return Score;
            }
        }

        // String formatada para exibir no TextBlock
        public string ProgressoStr
        {
            get
            {
                if (Score == 0 && Hits == 0 && Misses == 0) return "0";
                return $"{Score}";
            }
        }

        // Status textual baseado no score
        public string StatusStr
        {
            get
            {
                if (Score == 0 && Hits == 0 && Misses == 0) return "⚪ Não testada";
                if (Score >= 80) return "⬆ Excelente";
                if (Score > 0)   return "⬌ Boa";
                if (Score <= -80) return "⛔ Não utilizar";
                return "⚠ Ruim";
            }
        }

        public string HistoryRaw { get; set; } = string.Empty;

        public string HistoryVisual
        {
            get
            {
                if (string.IsNullOrEmpty(HistoryRaw)) return "⚪⚪⚪⚪⚪";
                string res = "";
                foreach (char c in HistoryRaw)
                {
                    if (c == '1') res += "🟢 ";
                    else if (c == '0') res += "🔴 ";
                }
                return res.TrimEnd();
            }
        }
    }

    public partial class InnoLearnView : UserControl
    {
        // ============================================================
        // P/Invoke — Estrutura espelho do LearnStrategyExport do C
        // ATENÇÃO: A ordem e os tamanhos devem ser idênticos ao struct
        // definido em api.c (target_id[64], query[128], score, hits, misses)
        // ============================================================
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct LearnStrategyExport
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string TargetId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Query;

            public int Score;
            public int Hits;
            public int Misses;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string History;
        }

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetTargetCount();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategyCount();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategy(int index, out LearnStrategyExport outStrategy);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_ResetDB();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_RefreshDevices();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_GetRegisteredTotal();

        // --- Estado interno de confirmação dupla ---
        private bool _resetPrimarioConfirmado = false;

        // --- Filtro por Modelo de Hardware ---
        private string _currentModelFilter = "ALL";

        // --- Timer do Super Dashboard ---
        private DispatcherTimer _timer;
        private bool _isRefreshing = false;

        // --- Coleção de dados master e da tabela ---
        private System.Collections.Generic.List<LearnStrategyItem> _allStrategies = new();
        private ObservableCollection<LearnStrategyItem> _strategies = new();

        public InnoLearnView()
        {
            InitializeComponent();
            InnovPlayUI.Core.InnoLearnBackupManager.ExecutarRotinaDiaria();
            CarregarDados();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            UpdateDashboardStats();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    int activeCount = Engine_RefreshDevices();
                    int totalCount = Engine_GetRegisteredTotal();

                    Dispatcher.BeginInvoke(() =>
                    {
                        LblTotal.Text = totalCount.ToString();
                        LblActive.Text = activeCount.ToString();
                        _isRefreshing = false;
                    });
                }
                catch (Exception)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        LblTotal.Text = "ERRO DLL";
                        _isRefreshing = false;
                    });
                }
            });
        }

        private void UpdateDashboardStats()
        {
            try
            {
                int activeCount = Engine_RefreshDevices();
                int totalCount = Engine_GetRegisteredTotal();
                LblTotal.Text = totalCount.ToString();
                LblActive.Text = activeCount.ToString();
            }
            catch (Exception)
            {
                LblTotal.Text = "ERRO DLL";
            }
        }

        private void CarregarBackups()
        {
            var backups = InnovPlayUI.Core.InnoLearnBackupManager.ObterBackupsDisponiveis();
            ListBackups.ItemsSource = backups;
        }

        private string _searchTerm = "";

        public void TxtSearchStrategy_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _searchTerm = tb.Text.Trim();
                AplicarFiltroEAgrupamento();
            }
        }

        public void ComboSimulador_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboSimulador != null && ComboSimulador.SelectedItem is string selectedFriendly)
            {
                AtualizarSimulador(selectedFriendly);
            }
        }

        private void AtualizarSimulador(string friendlyName)
        {
            if (TxtSimTargetId == null || TxtSimPrimary == null || TxtSimFallback1 == null || TxtSimConfidence == null)
                return;

            var items = _allStrategies.Where(s => s.ElementoId == friendlyName).OrderByDescending(s => s.Score).ToList();
            if (items.Count > 0)
            {
                var best = items[0];
                TxtSimTargetId.Text = $"ALVO: {best.RawTargetId} ({friendlyName})";
                TxtSimPrimary.Text = best.Query;
                TxtSimFallback1.Text = items.Count > 1 ? items[1].Query : "Heurística de Centróide Estático / Relativo (X, Y)";

                double conf = Math.Max(88.0, Math.Min(99.9, 88.0 + (best.Score * 0.12)));
                TxtSimConfidence.Text = $"{conf:F1}% Confiabilidade Heurística (Score: {best.Score})";
            }
        }

        private void FilterModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string filterTag)
            {
                _currentModelFilter = filterTag;
                AplicarFiltroEAgrupamento();
            }
        }

        // ============================================================
        // Carrega os dados do banco InnoLearn via DLL e atualiza a UI
        // ============================================================
        private void CarregarDados()
        {
            _allStrategies.Clear();
            _strategies.Clear();

            int totalTargets    = 0;
            int totalStrategies = 0;
            int totalHits       = 0;
            int totalMisses     = 0;

            try
            {
                totalTargets    = Engine_Learn_GetTargetCount();
                totalStrategies = Engine_Learn_GetStrategyCount();

                for (int i = 0; i < totalStrategies; i++)
                {
                    int found = Engine_Learn_GetStrategy(i, out LearnStrategyExport raw);
                    if (found == 0) break;

                    totalHits   += raw.Hits;
                    totalMisses += raw.Misses;

                    string tid = raw.TargetId ?? "";
                    _allStrategies.Add(new LearnStrategyItem
                    {
                        RawTargetId = tid,
                        Categoria   = ObterCategoria(tid),
                        ElementoId  = FormatarElementoId(tid),
                        Query       = raw.Query ?? "",
                        Score       = raw.Score,
                        Hits        = raw.Hits,
                        Misses      = raw.Misses,
                        HistoryRaw  = raw.History ?? ""
                    });
                }

                AplicarFiltroEAgrupamento();

                // Atualizar seletor do simulador InnoAI 2.0
                if (ComboSimulador != null)
                {
                    var distinctElements = _allStrategies.Select(s => s.ElementoId).Distinct().OrderBy(x => x).ToList();
                    ComboSimulador.ItemsSource = distinctElements;
                    if (distinctElements.Count > 0 && ComboSimulador.SelectedIndex < 0)
                    {
                        ComboSimulador.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao ler banco InnoLearn:\n{ex.Message}",
                    "InnoLearn — Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Atualiza os cards de resumo
            LblTotalTargets.Text   = totalTargets.ToString();
            LblTotalHits.Text      = totalHits.ToString();
            LblTotalMisses.Text    = totalMisses.ToString();

            int grandTotal = totalHits + totalMisses;
            if (grandTotal > 0)
            {
                double rate = (double)totalHits / grandTotal * 100.0;
                LblSuccessRate.Text = $"{rate:F1}%";
            }
            else
            {
                LblSuccessRate.Text = "—";
            }

            // Cancela qualquer confirmação dupla pendente ao recarregar
            _resetPrimarioConfirmado = false;
            BtnResetDB.Content = "🗑  RESETAR BANCO InnoLearn";

            // Atualiza também a lista de backups
            CarregarBackups();
        }

        private void AplicarFiltroEAgrupamento()
        {
            var filtered = new System.Collections.Generic.List<LearnStrategyItem>();

            foreach (var item in _allStrategies)
            {
                bool matches = _currentModelFilter switch
                {
                    "V7"  => item.Categoria.Contains("Vision 7") || item.RawTargetId.StartsWith("v7_") || item.RawTargetId.Contains("vision7"),
                    "M7"  => item.Categoria.Contains("TabMulti M7") || item.RawTargetId.StartsWith("f7_") || item.RawTargetId.Contains("m7"),
                    "T20" => item.Categoria.Contains("Nokia T20") || item.RawTargetId.StartsWith("t20_") || item.RawTargetId.Contains("t20"),
                    "F1"  => item.Categoria.Contains("Geral") || item.RawTargetId.StartsWith("f1_") || (!item.RawTargetId.StartsWith("f7_") && !item.RawTargetId.StartsWith("v7_") && !item.RawTargetId.StartsWith("t20_")),
                    _     => true // ALL
                };

                if (!string.IsNullOrEmpty(_searchTerm))
                {
                    bool searchMatches = item.ElementoId.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Query.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.RawTargetId.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Categoria.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!searchMatches) matches = false;
                }

                if (matches) filtered.Add(item);
            }

            filtered.Sort((a, b) => b.Score.CompareTo(a.Score));

            _strategies.Clear();
            foreach (var item in filtered)
                _strategies.Add(item);

            var cvs = new CollectionViewSource { Source = _strategies };
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("Categoria"));
            ListStrategies.ItemsSource = cvs.View;
            LblTotalStrategies.Text = $"{_strategies.Count} estratégias";
        }

        // Formata o ID técnico do elemento para exibição amigável
        private static string FormatarElementoId(string id)
        {
            return id switch
            {
                "campo_email"             => "📧 E-mail do Aplicador",
                "campo_senha"             => "🔒 Senha do Aplicador",
                "botao_entrar"            => "▶ Botão Entrar",
                "spinner_municipio"       => "📍 Gaveta de Município",
                "botao_importar_zip"      => "📦 Opção Importar ZIP",
                "f1_btn_procurar_arquivo" => "📁 Botão Procurar Arquivo ZIP",
                "f1_btn_avaliacoes"        => "📑 Pasta Avaliações",
                "f7_spinner_municipio"    => "📍 Gaveta de Município (M7)",
                "f7_campo_email"          => "📧 E-mail do Aplicador (M7)",
                "f7_campo_senha"          => "🔒 Senha do Aplicador (M7)",
                "f7_botao_entrar"         => "▶ Botão Entrar (M7)",
                "f7_btn_procurar_arquivo" => "📁 Botão Procurar Arquivo ZIP (M7)",
                "v7_spinner_municipio"    => "📍 Gaveta de Município (V7)",
                "v7_campo_email"          => "📧 E-mail do Aplicador (V7)",
                "v7_campo_senha"          => "🔒 Senha do Aplicador (V7)",
                "v7_botao_entrar"         => "▶ Botão Entrar (V7)",
                "v7_botao_importar_zip"   => "📦 Opção Importar ZIP (V7)",
                "v7_btn_procurar_arquivo" => "📁 Botão Procurar Arquivo ZIP (V7)",
                "v7_btn_sincronizar_agora" => "🔄 Botão Sincronizar Agora (V7)",
                "t20_spinner_municipio"   => "📍 Gaveta de Município (T20)",
                "t20_campo_email"         => "📧 E-mail do Aplicador (T20)",
                "t20_campo_senha"         => "🔒 Senha do Aplicador (T20)",
                "t20_botao_entrar"        => "▶ Botão Entrar (T20)",
                "f6_btn_sincronizar_agora" => "🔄 Botão Sincronizar Agora",
                _                         => id
            };
        }

        // Define a qual Etapa e função pertence o elemento para agrupamento estrito
        private static string ObterCategoria(string id)
        {
            return id switch
            {
                // --- ETAPAS NOKIA T20 (ANDROID 11) ---
                "t20_spinner_municipio"    => "Nokia T20 — Etapa 2: Seleção de Município",
                "t20_campo_email"          => "Nokia T20 — Etapa 3: Injeção de E-mail do Aplicador",
                "t20_campo_senha"          => "Nokia T20 — Etapa 4a: Injeção de Senha do Aplicador",
                "t20_botao_entrar"         => "Nokia T20 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "t20_botao_importar_zip"   => "Nokia T20 — Etapa 5a: Menu de Importação do Pacote ZIP",
                "t20_btn_procurar_arquivo" => "Nokia T20 — Etapa 5b: Botão Procurar Arquivo .ZIP",


                // --- ETAPAS V7 (POSITIVO VISION 7) ---
                "v7_spinner_municipio"     => "V7 — Etapa 2: Seleção de Município",
                "v7_campo_email"           => "V7 — Etapa 3: Injeção de E-mail do Aplicador",
                "v7_campo_senha"           => "V7 — Etapa 4a: Injeção de Senha do Aplicador",
                "v7_botao_entrar"          => "V7 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "v7_botao_importar_zip"    => "V7 — Etapa 5: Menu de Importação de Pacote ZIP",
                "v7_btn_procurar_arquivo"  => "V7 — Etapa 6: Seleção de Arquivo no Storage",
                "v7_btn_sincronizar_agora" => "V7 — Etapa F6: Sincronização Direta",

                // --- ETAPAS M7 (MULTILASER TABMULTI M7) ---
                "f7_spinner_municipio"     => "M7 — Etapa 2: Seleção de Município",
                "f7_campo_email"           => "M7 — Etapa 3: Injeção de E-mail do Aplicador",
                "f7_campo_senha"           => "M7 — Etapa 4a: Injeção de Senha do Aplicador",
                "f7_botao_entrar"          => "M7 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "f7_btn_procurar_arquivo"  => "M7 — Etapa 6: Seleção de Arquivo no Storage",

                // --- ETAPAS F1 (GERAL / MULTILASER PADRÃO) ---
                "spinner_municipio"        => "F1 — Etapa 2: Seleção de Município",
                "campo_email"              => "F1 — Etapa 3: Injeção de E-mail do Aplicador",
                "campo_senha"              => "F1 — Etapa 4a: Injeção de Senha do Aplicador",
                "botao_entrar"             => "F1 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "botao_importar_zip"       => "F1 — Etapa 5: Menu de Importação de Pacote ZIP",
                "f1_btn_procurar_arquivo"  => "F1 — Etapa 6: Seleção de Arquivo no Storage",
                "f1_btn_avaliacoes"         => "F1 — Etapa 6.5: Pasta de Avaliações",

                // --- OUTROS MÓDULOS ---
                "f2_btn_enviar"            => "F2 — Etapa F2: Envio de Dados de Alunos",
                "f3_btn_atualizar"         => "F3 — Etapa F3: Atualização Expressa de Pacote",
                "f5_btn_desligar"          => "F5 — Etapa F5: Desligamento Remoto",
                "f6_btn_sincronizar_agora" => "F6 — Etapa F6: Sincronização Direta de Dados",

                _ => id.StartsWith("v7_") ? "V7 — Etapas Positivo Vision 7" :
                     id.StartsWith("f7_") ? "M7 — Etapas Multilaser TabMulti M7" :
                     id.StartsWith("f2_") ? "F2 — Etapas de Envio" :
                     id.StartsWith("f3_") ? "F3 — Etapas de Atualização" :
                     id.StartsWith("f5_") ? "F5 — Etapas de Desligamento" :
                     "F1 — Etapas Gerais e Acesso"
            };
        }

        // ============================================================
        // Botão Atualizar — recarrega dados da DLL
        // ============================================================
        private void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        {
            CarregarDados();
        }

        private void BtnMapeamento_Click(object sender, RoutedEventArgs e)
        {
            BrainVisualizerWindow visualizer = new BrainVisualizerWindow();
            Window parentWindow = Window.GetWindow(this) ?? Application.Current.MainWindow;
            if (parentWindow != null && parentWindow != visualizer)
            {
                visualizer.Owner = parentWindow;
                visualizer.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            visualizer.ShowDialog();
        }

        // ============================================================
        // Reset com Confirmação DUPLA
        // Clique 1 → muda o botão para "TEM CERTEZA?"
        // Clique 2 → executa o reset e recarrega
        // Qualquer outro clique na UI cancela a confirmação pendente
        // ============================================================
        private void BtnResetDB_Click(object sender, RoutedEventArgs e)
        {
            if (!_resetPrimarioConfirmado)
            {
                // === PRIMEIRA CONFIRMAÇÃO ===
                _resetPrimarioConfirmado = true;
                BtnResetDB.Content = "⚠  CONFIRMAR RESET? (clique novamente)";

                // Timer de 5 segundos: se não confirmar, cancela automaticamente
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (_resetPrimarioConfirmado)
                    {
                        _resetPrimarioConfirmado = false;
                        BtnResetDB.Content = "🗑  RESETAR BANCO InnoLearn";
                    }
                };
                timer.Start();
            }
            else
            {
                // === SEGUNDA CONFIRMAÇÃO (EXECUTA O RESET) ===
                var resultado = MessageBox.Show(
                    "ATENÇÃO: Esta ação é IRREVERSÍVEL.\n\n" +
                    "Todo o histórico de aprendizado (scores, acertos, falhas e\n" +
                    "coordenadas aprendidas) será apagado permanentemente.\n\n" +
                    "O sistema voltará ao estado inicial (seed).\n\n" +
                    "Deseja realmente continuar?",
                    "InnoLearn — Confirmação Final de Reset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (resultado == MessageBoxResult.Yes)
                {
                    try
                    {
                        Engine_Learn_ResetDB();
                        CarregarDados();
                        MessageBox.Show(
                            "Banco InnoLearn resetado com sucesso.\n" +
                            "O sistema voltou ao estado inicial (seed).",
                            "InnoLearn — Reset Concluído",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao resetar o banco:\n{ex.Message}",
                            "InnoLearn — Erro Crítico",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Cancelou na segunda confirmação
                    _resetPrimarioConfirmado = false;
                    BtnResetDB.Content = "🗑  RESETAR BANCO InnoLearn";
                }
            }
        }

        // ============================================================
        // Restaurar Backup Específico
        // ============================================================
        private void BtnRestaurarBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string bkpPath)
            {
                var result = MessageBox.Show(
                    "ATENÇÃO: Tem certeza que deseja restaurar este backup?\n" +
                    "O estado atual do aprendizado será permanentemente sobrescrito pelas configurações dessa data.",
                    "Restaurar Backup InnoLearn", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    bool sucesso = InnovPlayUI.Core.InnoLearnBackupManager.RestaurarBackup(bkpPath);
                    if (sucesso)
                    {
                        MessageBox.Show(
                            "Backup restaurado com sucesso na pasta de dados.\n\n" +
                            "⚠ IMPORTANTE: Para que o motor nativo (InnoEngine.dll) recarregue " +
                            "as informações na memória, por favor, REINICIE o aplicativo.",
                            "Restauração Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Ocorreu uma falha ao tentar restaurar o backup. Verifique se o arquivo não está sendo utilizado.",
                            "Erro de Restauração", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
