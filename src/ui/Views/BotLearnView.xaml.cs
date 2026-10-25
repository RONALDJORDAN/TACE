using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

// ============================================================
// BotLearnView — Painel de Estatísticas de Aprendizado
// Exibe scores, acertos e falhas de cada estratégia de UI,
// além do controle de reset com confirmação dupla.
// Autor: Jordan Araujo | BotRpa
// ============================================================

using System.Windows.Data;

namespace BotRpaUI.Views
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

    // Legenda unificada com o Mapa Neural: >50 verde, 1-50 amarelo, 0 neutro, <0 vermelho
    public class ScoreToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double or int)
            {
                double score = System.Convert.ToDouble(value);
                if (score > 50) return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EDEA3"));
                if (score > 0) return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5C542"));
                if (score == 0) return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8E8EA0"));
                return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6B6B"));
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
                if (Score == 0 && Hits == 0 && Misses == 0) return "Não testada";
                if (Score >= 80) return "Excelente";
                if (Score > 0)   return "Boa";
                if (Score <= -80) return "Não utilizar";
                return "Ruim";
            }
        }

        public string HistoryRaw { get; set; } = string.Empty;

        public string HistoryVisual
        {
            get
            {
                if (string.IsNullOrEmpty(HistoryRaw)) return "-----";
                string res = "";
                foreach (char c in HistoryRaw)
                {
                    if (c == '1') res += "1";
                    else if (c == '0') res += "0";
                }
                return res.TrimEnd();
            }
        }
    }

    public partial class BotLearnView : UserControl
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

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetTargetCount();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategyCount();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetStrategy(int index, out LearnStrategyExport outStrategy);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_ResetDB();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_Learn_GetActiveMode();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_RefreshDevices();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Engine_GetRegisteredTotal();

        // --- Estado interno de confirmação dupla ---
        private bool _resetPrimarioConfirmado = false;

        // --- Filtro por Modelo de Hardware ---
        private string _currentModelFilter = "ALL";

        // --- Coleção de dados master e da tabela ---
        private System.Collections.Generic.List<LearnStrategyItem> _allStrategies = new();
        private ObservableCollection<LearnStrategyItem> _strategies = new();

        public BotLearnView()
        {
            InitializeComponent();
            BotRpaUI.Core.BotLearnBackupManager.ExecutarRotinaDiaria();
            CarregarDados();

            UpdateIaModeBadge();
        }

        private void UpdateIaModeBadge()
        {
            try
            {
                int mode = 0;
                try { mode = Engine_Learn_GetActiveMode(); }
                catch { mode = BotRpaUI.Core.SettingsManager.AiLearnActive ? 1 : 0; }
                if (TxtIaSubtitle != null)
                    TxtIaSubtitle.Text = mode == 1
                        ? "Mecanismo de IA adaptativa com Centroid Matching, resolução multi-fallback e proteção contra drift de tela."
                        : "Seletores determinísticos versionados — aprendizado desligado, sem persistência (recomendado produção).";
                if (BtnResetDB != null)
                {
                    BtnResetDB.IsEnabled = mode == 1;
                    BtnResetDB.ToolTip = mode == 1
                        ? "Apaga o histórico de aprendizado persistido e volta ao seed."
                        : "Modo Estático: sem histórico persistido — nada a resetar. Ative o modo ATIVO nas Configurações para aprender.";
                }
            }
            catch { }
        }

        private void CarregarBackups()
        {
            var backups = BotRpaUI.Core.BotLearnBackupManager.ObterBackupsDisponiveis();
            ListBackups.ItemsSource = backups;
        }

        private string _searchTerm = "";

        public void TxtSearchStrategy_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _searchTerm = tb.Text.Trim();
                if (TxtSearchWatermark != null)
                    TxtSearchWatermark.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;
                AplicarFiltroEAgrupamento();
            }
        }

        public void BtnExpandAllGroups_Click(object sender, RoutedEventArgs e)
        {
            SetAllGroupsExpanded(true);
        }

        public void BtnCollapseAllGroups_Click(object sender, RoutedEventArgs e)
        {
            SetAllGroupsExpanded(false);
        }

        private void SetAllGroupsExpanded(bool expanded)
        {
            if (ListStrategies == null) return;
            foreach (var expander in FindVisualChildren<Expander>(ListStrategies))
                expander.IsExpanded = expanded;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T : DependencyObject
        {
            if (dep == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(dep, i);
                if (child is T typed) yield return typed;
                foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
            }
        }

        // A roda do mouse sobre a matriz era engolida pelo ScrollViewer interno
        // do ListView (barra desabilitada) e nunca chegava ao ScrollViewer da
        // página. Repassa o evento ao ScrollViewer externo mais próximo.
        private void ListStrategies_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is not ListView lv) return;
            e.Handled = true;
            DependencyObject? parent = lv;
            while (parent != null && parent is not ScrollViewer)
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            if (parent is ScrollViewer scroller)
            {
                var args = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = lv
                };
                scroller.RaiseEvent(args);
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
        // Carrega os dados do banco BotLearn via DLL e atualiza a UI
        // ============================================================
        private void CarregarDados()
        {
            _allStrategies.Clear();
            _strategies.Clear();

            int totalStrategies = 0;

            try
            {
                totalStrategies = Engine_Learn_GetStrategyCount();

                for (int i = 0; i < totalStrategies; i++)
                {
                    int found = Engine_Learn_GetStrategy(i, out LearnStrategyExport raw);
                    if (found == 0) break;

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

                // Atualizar seletor do simulador BotAI 2.0
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
                MessageBox.Show($"Erro ao ler banco BotLearn:\n{ex.Message}",
                    "BotLearn — Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Cancela qualquer confirmação dupla pendente ao recarregar
            _resetPrimarioConfirmado = false;
            BtnResetDB.Content = "RESETAR BANCO BotLearn";

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
        }

        // Formata o ID técnico do elemento para exibição amigável
        private static string FormatarElementoId(string id)
        {
            return id switch
            {
                "campo_email"             => "E-mail do Aplicador",
                "campo_senha"             => "Senha do Aplicador",
                "botao_entrar"            => "Botão Entrar",
                "spinner_municipio"       => "Gaveta de Município",
                "botao_importar_zip"      => "Opção Importar ZIP",
                "f1_btn_procurar_arquivo" => "Botão Procurar Arquivo ZIP",
                "f1_btn_avaliacoes"        => "Pasta Avaliações",
                "f7_spinner_municipio"    => "Gaveta de Município (M7)",
                "f7_campo_email"          => "E-mail do Aplicador (M7)",
                "f7_campo_senha"          => "Senha do Aplicador (M7)",
                "f7_botao_entrar"         => "Botão Entrar (M7)",
                "f7_btn_procurar_arquivo" => "Botão Procurar Arquivo ZIP (M7)",
                "v7_spinner_municipio"    => "Gaveta de Município (V7)",
                "v7_campo_email"          => "E-mail do Aplicador (V7)",
                "v7_campo_senha"          => "Senha do Aplicador (V7)",
                "v7_botao_entrar"         => "Botão Entrar (V7)",
                "v7_botao_importar_zip"   => "Opção Importar ZIP (V7)",
                "v7_btn_procurar_arquivo" => "Botão Procurar Arquivo ZIP (V7)",
                "v7_btn_sincronizar_agora" => "Botão Sincronizar Agora (V7)",
                "t20_spinner_municipio"   => "Gaveta de Município (T20)",
                "t20_campo_email"         => "E-mail do Aplicador (T20)",
                "t20_campo_senha"         => "Senha do Aplicador (T20)",
                "t20_botao_entrar"        => "Botão Entrar (T20)",
                "f6_btn_sincronizar_agora" => "Botão Sincronizar Agora",
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
        // Clique 1 -> muda o botão para "TEM CERTEZA?"
        // Clique 2 -> executa o reset e recarrega
        // Qualquer outro clique na UI cancela a confirmação pendente
        // ============================================================
        private void BtnResetDB_Click(object sender, RoutedEventArgs e)
        {
            if (!_resetPrimarioConfirmado)
            {
                // === PRIMEIRA CONFIRMAÇÃO ===
                _resetPrimarioConfirmado = true;
                BtnResetDB.Content = "CONFIRMAR RESET? (clique novamente)";

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
                        BtnResetDB.Content = "RESETAR BANCO BotLearn";
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
                    "BotLearn — Confirmação Final de Reset",
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
                            "Banco BotLearn resetado com sucesso.\n" +
                            "O sistema voltou ao estado inicial (seed).",
                            "BotLearn — Reset Concluído",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao resetar o banco:\n{ex.Message}",
                            "BotLearn — Erro Crítico",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Cancelou na segunda confirmação
                    _resetPrimarioConfirmado = false;
                    BtnResetDB.Content = "RESETAR BANCO BotLearn";
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
                    "Restaurar Backup BotLearn", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    bool sucesso = BotRpaUI.Core.BotLearnBackupManager.RestaurarBackup(bkpPath);
                    if (sucesso)
                    {
                        MessageBox.Show(
                            "Backup restaurado com sucesso na pasta de dados.\n\n" +
                            "IMPORTANTE: Para que o motor nativo (BotEngine.dll) recarregue " +
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
