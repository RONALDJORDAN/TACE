using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

// ============================================================
// BotLearnView — Painel do motor de estratégias adaptativas.
// Exibe scores, acertos, falhas e o ranking heurístico de
// seletores, além do controle de reset com confirmação dupla.
// Autor: Jordan Araujo | TACE
// ============================================================

using System.Windows.Data;

namespace TACEUI.Views
{
    // --- Conversores para o score médio dos grupos ---
    public class GroupScoreStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<object> items && items.Count > 0)
            {
                double total = 0;
                int count = 0;
                foreach (var entry in items)
                {
                    if (entry is LearnStrategyItem item)
                    {
                        total += item.Score;
                        count++;
                    }
                }
                if (count > 0) return $"{total / count:+0;-0;0}";
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    public class GroupScoreBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<object> items && items.Count > 0)
            {
                double total = 0;
                int count = 0;
                foreach (var entry in items)
                {
                    if (entry is LearnStrategyItem item)
                    {
                        total += item.Score;
                        count++;
                    }
                }
                if (count > 0)
                    return new ScoreToBrushConverter().Convert(total / count, targetType, parameter, culture);
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    // Legenda unificada: >50 verde, 1-50 amarelo, 0 neutro, <0 vermelho.
    public class ScoreToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double or int)
            {
                double score = System.Convert.ToDouble(value);
                string color = score > 50 ? "#3FB950" : score > 0 ? "#D29922" : score == 0 ? "#8B98A7" : "#F85149";
                return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    // --- Modelo de linha do ranking ---
    public class LearnStrategyItem
    {
        public string RawTargetId { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string ElementoId { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public int Score { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public int Attempts => Hits + Misses;

        public string Precisao
        {
            get
            {
                if (Attempts == 0) return "—";
                double rate = (double)Hits / Attempts * 100.0;
                return $"{rate:F1}%";
            }
        }

        public string StatusStr
        {
            get
            {
                if (Attempts == 0) return "Sem testes";
                if (Score >= 80) return "Excelente";
                if (Score > 50) return "Alta confiança";
                if (Score > 0) return "Em observação";
                if (Score == 0) return "Neutra";
                if (Score <= -50) return "Penalizada";
                return "Atenção";
            }
        }

        public string HistoryRaw { get; set; } = string.Empty;

        public string HistoryVisual
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HistoryRaw)) return "—";
                var chars = HistoryRaw.Where(c => c == '1' || c == '0').Take(10).ToArray();
                return chars.Length == 0 ? "—" : string.Join(" ", chars);
            }
        }
    }

    public partial class BotLearnView : UserControl
    {
        // ============================================================
        // P/Invoke — Estrutura espelho do LearnStrategyExport do C.
        // A ordem e os tamanhos devem ser idênticos ao struct de api.c.
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

        private bool _resetPrimarioConfirmado = false;
        private string _currentModelFilter = "ALL";
        private string _searchTerm = "";
        private System.Collections.Generic.List<LearnStrategyItem> _allStrategies = new();
        private ObservableCollection<LearnStrategyItem> _strategies = new();

        public BotLearnView()
        {
            InitializeComponent();
            TACEUI.Core.BotLearnBackupManager.ExecutarRotinaDiaria();
            CarregarDados();
            AtualizarModo();
        }

        private void AtualizarModo()
        {
            try
            {
                int mode = 0;
                try { mode = Engine_Learn_GetActiveMode(); }
                catch { mode = TACEUI.Core.SettingsManager.AiLearnActive ? 1 : 0; }

                bool adaptive = mode == 1;
                if (TxtModeBadge != null)
                {
                    TxtModeBadge.Text = adaptive ? "ADAPTATIVO" : "DETERMINÍSTICO";
                    TxtModeBadge.Foreground = BrushFromHex(adaptive ? "#3FB950" : "#58A6FF");
                }
                if (BadgeModo != null)
                {
                    BadgeModo.Background = BrushFromHex(adaptive ? "#0F2A1A" : "#0D1B2A");
                    BadgeModo.BorderBrush = BrushFromHex(adaptive ? "#3FB950" : "#1F6FEB");
                }
                if (TxtModeDescription != null)
                {
                    TxtModeDescription.Text = adaptive
                        ? "Aprende com acertos e falhas registrados e persiste o ranking em bot_learn.json."
                        : "Usa somente o seed compilado; scores não são persistidos.";
                }
                if (BtnResetDB != null)
                {
                    BtnResetDB.IsEnabled = adaptive;
                    BtnResetDB.ToolTip = adaptive
                        ? "Apaga o histórico adaptativo persistido e retorna ao seed compilado."
                        : "Disponível apenas no modo adaptativo. Ative o modo nas Configurações.";
                }
            }
            catch { }
        }

        private void CarregarBackups()
        {
            var backups = TACEUI.Core.BotLearnBackupManager.ObterBackupsDisponiveis();
            ListBackups.ItemsSource = backups;

            if (TxtBackupsEmpty != null)
                TxtBackupsEmpty.Visibility = backups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (TxtBackupCount != null)
                TxtBackupCount.Text = backups.Count == 1 ? "1 cópia" : $"{backups.Count} cópias";

            if (TxtBackupPath != null)
            {
                try { TxtBackupPath.Text = $"Pasta de backups: {TACEUI.Core.BotLearnBackupManager.GetBackupDirectory()}"; }
                catch { TxtBackupPath.Text = "Pasta de backups: —"; }
            }
        }

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

        // Repassa a roda do mouse ao ScrollViewer da página, pois o ScrollViewer
        // interno do ListView está desabilitado.
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
            if (TxtSimTargetId == null || TxtSimPrimary == null || TxtSimFallback1 == null)
                return;

            var items = _allStrategies.Where(s => s.ElementoId == friendlyName).OrderByDescending(s => s.Score).ToList();
            if (items.Count == 0)
            {
                LimparDiagnostico();
                return;
            }

            var best = items[0];
            TxtSimTargetId.Text = $"ALVO: {best.ElementoId} · {best.RawTargetId}";
            TxtSimPrimary.Text = string.IsNullOrWhiteSpace(best.Query) ? "—" : best.Query;
            TxtSimFallback1.Text = items.Count > 1 ? items[1].Query : "Sem alternativa registrada";
            TxtSimScore.Text = $"{best.Score:+0;-0;0}";
            TxtSimPrecision.Text = best.Precisao;
            TxtSimAttempts.Text = best.Attempts == 0
                ? "0 registros"
                : $"{best.Attempts} ({best.Hits} acertos / {best.Misses} falhas)";
            TxtSimStatus.Text = best.StatusStr;

            var brush = BrushParaScore(best.Score);
            if (TxtSimScore != null) TxtSimScore.Foreground = brush;
            if (TxtSimStatus != null) TxtSimStatus.Foreground = brush;
        }

        private void LimparDiagnostico()
        {
            if (TxtSimTargetId != null) TxtSimTargetId.Text = "ALVO: nenhuma estratégia selecionada";
            if (TxtSimPrimary != null) TxtSimPrimary.Text = "Aguardando seleção...";
            if (TxtSimFallback1 != null) TxtSimFallback1.Text = "Aguardando seleção...";
            if (TxtSimScore != null) TxtSimScore.Text = "—";
            if (TxtSimPrecision != null) TxtSimPrecision.Text = "—";
            if (TxtSimAttempts != null) TxtSimAttempts.Text = "0 registros";
            if (TxtSimStatus != null) TxtSimStatus.Text = "Sem testes";
        }

        private void FilterModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string filterTag)
            {
                _currentModelFilter = filterTag;
                AplicarFiltroEAgrupamento();
            }
        }

        private void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        {
            CarregarDados();
            AtualizarModo();
        }

        // ============================================================
        // Carrega os dados do motor nativo e atualiza indicadores e ranking.
        // ============================================================
        private void CarregarDados()
        {
            _allStrategies.Clear();
            _strategies.Clear();

            try
            {
                int totalStrategies = Engine_Learn_GetStrategyCount();

                for (int i = 0; i < totalStrategies; i++)
                {
                    int found = Engine_Learn_GetStrategy(i, out LearnStrategyExport raw);
                    if (found == 0) break;

                    string tid = raw.TargetId ?? "";
                    _allStrategies.Add(new LearnStrategyItem
                    {
                        RawTargetId = tid,
                        Categoria = ObterCategoria(tid),
                        ElementoId = FormatarElementoId(tid),
                        Query = raw.Query ?? "",
                        Score = raw.Score,
                        Hits = raw.Hits,
                        Misses = raw.Misses,
                        HistoryRaw = raw.History ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao ler os dados do BotLearn:\n{ex.Message}",
                    "BotLearn — Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            AtualizarKpis();
            AplicarFiltroEAgrupamento();

            if (ComboSimulador != null)
            {
                var distinctElements = _allStrategies.Select(s => s.ElementoId).Distinct().OrderBy(x => x).ToList();
                ComboSimulador.ItemsSource = distinctElements;
                if (distinctElements.Count > 0)
                    ComboSimulador.SelectedIndex = 0;
                else
                    LimparDiagnostico();
            }

            _resetPrimarioConfirmado = false;
            if (BtnResetDB != null)
                BtnResetDB.Content = "Redefinir dados adaptativos";

            CarregarBackups();
        }

        private void AtualizarKpis()
        {
            int totalStrategies = _allStrategies.Count;
            int targets = 0;
            try { targets = Engine_Learn_GetTargetCount(); } catch { }
            if (targets <= 0)
                targets = _allStrategies.Select(s => s.RawTargetId).Distinct().Count();

            int hits = _allStrategies.Sum(s => s.Hits);
            int misses = _allStrategies.Sum(s => s.Misses);
            int runs = hits + misses;
            double avgScore = totalStrategies > 0 ? _allStrategies.Average(s => s.Score) : 0;

            if (TxtKpiStrategies != null) TxtKpiStrategies.Text = totalStrategies.ToString();
            if (TxtKpiTargets != null) TxtKpiTargets.Text = targets.ToString();
            if (TxtKpiRuns != null) TxtKpiRuns.Text = runs.ToString();

            if (TxtKpiAccuracy != null)
            {
                if (runs > 0)
                {
                    TxtKpiAccuracy.Text = $"{(double)hits / runs * 100.0:F1}%";
                    TxtKpiAccuracy.Foreground = BrushFromHex("#58A6FF");
                }
                else
                {
                    TxtKpiAccuracy.Text = "—";
                    TxtKpiAccuracy.Foreground = BrushFromHex("#8B98A7");
                }
            }

            if (TxtKpiScore != null)
            {
                if (totalStrategies > 0)
                {
                    TxtKpiScore.Text = $"{avgScore:+0.0;-0.0;0.0}";
                    TxtKpiScore.Foreground = BrushParaScore(avgScore);
                }
                else
                {
                    TxtKpiScore.Text = "—";
                    TxtKpiScore.Foreground = BrushFromHex("#8B98A7");
                }
            }
        }

        private void AplicarFiltroEAgrupamento()
        {
            var filtered = new System.Collections.Generic.List<LearnStrategyItem>();

            foreach (var item in _allStrategies)
            {
                bool matches = _currentModelFilter switch
                {
                    "V7" => item.Categoria.Contains("Vision 7") || item.RawTargetId.StartsWith("v7_") || item.RawTargetId.Contains("vision7"),
                    "M7" => item.Categoria.Contains("TabMulti M7") || item.RawTargetId.StartsWith("f7_") || item.RawTargetId.Contains("m7"),
                    "T20" => item.Categoria.Contains("Nokia T20") || item.RawTargetId.StartsWith("t20_") || item.RawTargetId.Contains("t20"),
                    "F1" => item.Categoria.Contains("Geral") || item.RawTargetId.StartsWith("f1_") || (!item.RawTargetId.StartsWith("f7_") && !item.RawTargetId.StartsWith("v7_") && !item.RawTargetId.StartsWith("t20_")),
                    _ => true
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

            AtualizarContadoresRanking(filtered.Count);
        }

        private void AtualizarContadoresRanking(int filteredCount)
        {
            if (TxtRankingCount != null)
                TxtRankingCount.Text = filteredCount == 1 ? "1 seletor" : $"{filteredCount} seletores";

            if (TxtRankingEmpty != null)
            {
                bool empty = filteredCount == 0;
                TxtRankingEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                if (empty)
                {
                    TxtRankingEmpty.Text = _allStrategies.Count == 0
                        ? "Nenhuma estratégia carregada pelo motor."
                        : "Nenhum seletor corresponde aos filtros atuais.";
                }
            }
        }

        private static System.Windows.Media.Brush BrushFromHex(string hex)
        {
            return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }

        private static System.Windows.Media.Brush BrushParaScore(double score)
        {
            string color = score > 50 ? "#3FB950" : score > 0 ? "#D29922" : score == 0 ? "#8B98A7" : "#F85149";
            return BrushFromHex(color);
        }

        // Formata o ID técnico do elemento para exibição amigável.
        private static string FormatarElementoId(string id)
        {
            return id switch
            {
                "campo_email" => "E-mail do Aplicador",
                "campo_senha" => "Senha do Aplicador",
                "botao_entrar" => "Botão Entrar",
                "spinner_municipio" => "Gaveta de Município",
                "botao_importar_zip" => "Opção Importar ZIP",
                "f1_btn_procurar_arquivo" => "Botão Procurar Arquivo ZIP",
                "f1_btn_avaliacoes" => "Pasta Avaliações",
                "f7_spinner_municipio" => "Gaveta de Município (M7)",
                "f7_campo_email" => "E-mail do Aplicador (M7)",
                "f7_campo_senha" => "Senha do Aplicador (M7)",
                "f7_botao_entrar" => "Botão Entrar (M7)",
                "f7_btn_procurar_arquivo" => "Botão Procurar Arquivo ZIP (M7)",
                "v7_spinner_municipio" => "Gaveta de Município (V7)",
                "v7_campo_email" => "E-mail do Aplicador (V7)",
                "v7_campo_senha" => "Senha do Aplicador (V7)",
                "v7_botao_entrar" => "Botão Entrar (V7)",
                "v7_botao_importar_zip" => "Opção Importar ZIP (V7)",
                "v7_btn_procurar_arquivo" => "Botão Procurar Arquivo ZIP (V7)",
                "v7_btn_sincronizar_agora" => "Botão Sincronizar Agora (V7)",
                "t20_spinner_municipio" => "Gaveta de Município (T20)",
                "t20_campo_email" => "E-mail do Aplicador (T20)",
                "t20_campo_senha" => "Senha do Aplicador (T20)",
                "t20_botao_entrar" => "Botão Entrar (T20)",
                "f6_btn_sincronizar_agora" => "Botão Sincronizar Agora",
                _ => id
            };
        }

        // Define a etapa e a função do elemento para agrupamento.
        private static string ObterCategoria(string id)
        {
            return id switch
            {
                "t20_spinner_municipio" => "Nokia T20 — Etapa 2: Seleção de Município",
                "t20_campo_email" => "Nokia T20 — Etapa 3: Injeção de E-mail do Aplicador",
                "t20_campo_senha" => "Nokia T20 — Etapa 4a: Injeção de Senha do Aplicador",
                "t20_botao_entrar" => "Nokia T20 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "t20_botao_importar_zip" => "Nokia T20 — Etapa 5a: Menu de Importação do Pacote ZIP",
                "t20_btn_procurar_arquivo" => "Nokia T20 — Etapa 5b: Botão Procurar Arquivo .ZIP",

                "v7_spinner_municipio" => "V7 — Etapa 2: Seleção de Município",
                "v7_campo_email" => "V7 — Etapa 3: Injeção de E-mail do Aplicador",
                "v7_campo_senha" => "V7 — Etapa 4a: Injeção de Senha do Aplicador",
                "v7_botao_entrar" => "V7 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "v7_botao_importar_zip" => "V7 — Etapa 5: Menu de Importação de Pacote ZIP",
                "v7_btn_procurar_arquivo" => "V7 — Etapa 6: Seleção de Arquivo no Storage",
                "v7_btn_sincronizar_agora" => "V7 — Etapa F6: Sincronização Direta",

                "f7_spinner_municipio" => "M7 — Etapa 2: Seleção de Município",
                "f7_campo_email" => "M7 — Etapa 3: Injeção de E-mail do Aplicador",
                "f7_campo_senha" => "M7 — Etapa 4a: Injeção de Senha do Aplicador",
                "f7_botao_entrar" => "M7 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "f7_btn_procurar_arquivo" => "M7 — Etapa 6: Seleção de Arquivo no Storage",

                "spinner_municipio" => "F1 — Etapa 2: Seleção de Município",
                "campo_email" => "F1 — Etapa 3: Injeção de E-mail do Aplicador",
                "campo_senha" => "F1 — Etapa 4a: Injeção de Senha do Aplicador",
                "botao_entrar" => "F1 — Etapa 4b: Confirmação de Acesso (Botão Entrar)",
                "botao_importar_zip" => "F1 — Etapa 5: Menu de Importação de Pacote ZIP",
                "f1_btn_procurar_arquivo" => "F1 — Etapa 6: Seleção de Arquivo no Storage",
                "f1_btn_avaliacoes" => "F1 — Etapa 6.5: Pasta de Avaliações",

                "f2_btn_enviar" => "F2 — Etapa F2: Envio de Dados de Alunos",
                "f3_btn_atualizar" => "F3 — Etapa F3: Atualização Expressa de Pacote",
                "f5_btn_desligar" => "F5 — Etapa F5: Desligamento Remoto",
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
        // Reset com confirmação dupla.
        // Clique 1 muda o botão para confirmação; clique 2 executa.
        // ============================================================
        private void BtnResetDB_Click(object sender, RoutedEventArgs e)
        {
            if (!_resetPrimarioConfirmado)
            {
                _resetPrimarioConfirmado = true;
                BtnResetDB.Content = "Confirmar redefinição";

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (_resetPrimarioConfirmado)
                    {
                        _resetPrimarioConfirmado = false;
                        BtnResetDB.Content = "Redefinir dados adaptativos";
                    }
                };
                timer.Start();
            }
            else
            {
                var resultado = MessageBox.Show(
                    "Esta ação é irreversível.\n\n" +
                    "Todo o histórico adaptativo (scores, acertos, falhas e histórico)\n" +
                    "será apagado e o motor voltará ao seed compilado.\n\n" +
                    "Deseja realmente continuar?",
                    "BotLearn — Confirmar redefinição",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (resultado == MessageBoxResult.Yes)
                {
                    try
                    {
                        Engine_Learn_ResetDB();
                        CarregarDados();
                        AtualizarModo();
                        MessageBox.Show(
                            "Dados adaptativos redefinidos com sucesso.\n" +
                            "O motor voltou ao seed compilado.",
                            "BotLearn — Redefinição concluída",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao redefinir os dados adaptativos:\n{ex.Message}",
                            "BotLearn — Erro crítico",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    _resetPrimarioConfirmado = false;
                    BtnResetDB.Content = "Redefinir dados adaptativos";
                }
            }
        }

        // ============================================================
        // Restauração de backup específico.
        // ============================================================
        private void BtnRestaurarBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string bkpPath)
            {
                var result = MessageBox.Show(
                    "Tem certeza que deseja restaurar este backup?\n" +
                    "O estado atual do arquivo de dados será sobrescrito.",
                    "Restaurar backup do BotLearn", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    bool sucesso = TACEUI.Core.BotLearnBackupManager.RestaurarBackup(bkpPath);
                    if (sucesso)
                    {
                        MessageBox.Show(
                            "Backup restaurado com sucesso na pasta de dados.\n\n" +
                            "Para que o motor nativo recarregue as informações em memória, reinicie o aplicativo.",
                            "Restauração concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Não foi possível restaurar o backup. Verifique se o arquivo não está em uso.",
                            "Erro de restauração", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
