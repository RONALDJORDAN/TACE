using System.Windows;
using InnovPlayUI.Views;

namespace InnovPlayUI
{
    public partial class MainWindow : Window
    {
        private MonitorView _monitorView;
        private RelatoriosView _relatoriosView;
        private ConfigView _configView;
        private InnoLearnView _learnView;
        private AboutView _aboutView;

        public MainWindow()
        {
            InitializeComponent();
            
            string versionStr = InnovPlayUI.Core.OtaUpdateManager.CurrentVersion;
            Title = $"Inno RPA (Plataforma InnovTech) - v{versionStr}";
            TxtHeaderVersion.Text = $"v{versionStr}";

            // Instanciar views uma única vez
            _monitorView = new MonitorView();
            _relatoriosView = new RelatoriosView();
            _configView = new ConfigView();
            _learnView = new InnoLearnView();
            _aboutView = new AboutView();
            
            // Abre o Monitor USB como página inicial padrão
            NavigateToMonitor();

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckForOtaUpdatesOnStartupAsync();
        }

        private async System.Threading.Tasks.Task CheckForOtaUpdatesOnStartupAsync()
        {
            try
            {
                var otaManager = new InnovPlayUI.Core.OtaUpdateManager();
                var manifest = await otaManager.CheckForUpdatesAsync();

                if (manifest != null && otaManager.IsUpdateAvailable(manifest))
                {
                    // OBRIGATÓRIO: Nova versão encontrada na nuvem! Abre a janela modal de atualização obrigatória.
                    var mandatoryWindow = new OtaMandatoryUpdateWindow(manifest);
                    mandatoryWindow.Owner = this;
                    mandatoryWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup OTA Check Error] {ex.Message}");
            }
        }

        public void NavigateToMonitor()
        {
            MainContent.Content = _monitorView;
            UpdateMenuStyles(BtnMenuMonitor);
        }

        private void MenuMonitor_Click(object sender, RoutedEventArgs e)
        {
            NavigateToMonitor();
        }

        private void MenuLearn_Click(object sender, RoutedEventArgs e)
        {
            NavigateToLearn();
        }

        public void NavigateToLearn()
        {
            _learnView = new InnoLearnView(); // Recarrega dados ao navegar
            MainContent.Content = _learnView;
            UpdateMenuStyles(BtnMenuLearn);
        }

        private void MenuRelatorios_Click(object sender, RoutedEventArgs e)
        {
            NavigateToRelatorios();
        }

        public void NavigateToRelatorios()
        {
            MainContent.Content = _relatoriosView;
            UpdateMenuStyles(BtnMenuRelatorios);
        }

        public void NavigateToConfig()
        {
            MainContent.Content = _configView;
            UpdateMenuStyles(BtnMenuConfig);
        }

        private void UpdateMenuStyles(System.Windows.Controls.Button activeButton)
        {
            var brushNormalFg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
            var brushNormalBg = System.Windows.Media.Brushes.Transparent;
            var brushNormalBorder = System.Windows.Media.Brushes.Transparent;

            var brushActiveFg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EDEA3"));
            var brushActiveBg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D1F5E"));
            var brushActiveBorder = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#513BA6"));

            System.Windows.Controls.Button[] buttons = { BtnMenuMonitor, BtnMenuLearn, BtnMenuRelatorios, BtnMenuConfig, BtnMenuSobre };

            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                if (btn == activeButton)
                {
                    btn.Foreground = brushActiveFg;
                    btn.Background = brushActiveBg;
                    btn.BorderBrush = brushActiveBorder;
                    btn.BorderThickness = new Thickness(1);
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    btn.Foreground = brushNormalFg;
                    btn.Background = brushNormalBg;
                    btn.BorderBrush = brushNormalBorder;
                    btn.BorderThickness = new Thickness(0);
                    btn.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private void MenuConfig_Click(object sender, RoutedEventArgs e)
        {
            NavigateToConfig();
        }

        public void NavigateToSobre()
        {
            MainContent.Content = _aboutView;
            UpdateMenuStyles(BtnMenuSobre);
        }

        private void MenuSobre_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSobre();
        }

        public void ReloadAllViewsData()
        {
            _monitorView?.LoadMunicipios();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (MainContent.Content is MonitorView monitor)
            {
                if (e.Key == System.Windows.Input.Key.F1)
                {
                    monitor.BtnF1_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F2)
                {
                    monitor.BtnF2_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F3)
                {
                    monitor.BtnF3_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F4)
                {
                    monitor.BtnF4_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F5)
                {
                    monitor.BtnF5_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F6)
                {
                    monitor.BtnF6_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F7)
                {
                    monitor.BtnF7_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F8)
                {
                    monitor.BtnF8_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    monitor.HandleEscPress();
                    e.Handled = true;
                }
            }
        }
    }
}