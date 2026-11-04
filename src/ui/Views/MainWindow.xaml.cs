using System.Windows;
using TACEUI.Views;

namespace TACEUI
{
    public partial class MainWindow : Window
    {
        private MonitorView _monitorView;
        private MonitorProView _monitorProView;
        private RelatoriosView _relatoriosView;
        private ConfigView _configView;
        private BotLearnView _learnView;
        private AboutView _aboutView;

        [System.Runtime.InteropServices.DllImport("BotEngine.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern int Engine_GetDeviceCount();

        private System.Windows.Threading.DispatcherTimer? _headerTelemetryTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            string versionStr = TACEUI.Core.AppInfo.CurrentVersion;
            Title = $"TACE - v{versionStr}";
            TxtHeaderVersion.Text = versionStr;

            // Timer para telemetria contínua de dispositivos conectados no cabeçalho
            _headerTelemetryTimer = new System.Windows.Threading.DispatcherTimer();
            _headerTelemetryTimer.Interval = TimeSpan.FromSeconds(2.5);
            _headerTelemetryTimer.Tick += (s, e) =>
            {
                try
                {
                    int count = Engine_GetDeviceCount();
                    if (TxtHeaderDeviceCount != null)
                    {
                        TxtHeaderDeviceCount.Text = $"{count} {(count == 1 ? "DISPOSITIVO" : "DISPOSITIVOS")}";
                    }
                }
                catch { }
            };
            _headerTelemetryTimer.Start();

            // Instanciar views uma única vez
            _monitorView = new MonitorView();
            _monitorProView = new MonitorProView();
            _relatoriosView = new RelatoriosView();
            _configView = new ConfigView();
            _learnView = new BotLearnView();
            _aboutView = new AboutView();
            
            // Atualiza a visibilidade da barra de navegação para exibir apenas a interface de monitor escolhida
            UpdateMonitorNavigationVisibility();

            // Abre a interface do Monitor USB configurada como página inicial padrão
            NavigateToDefaultMonitor();

            // Escuta mudanças de configuração em tempo de execução
            TACEUI.Core.SettingsManager.OnSettingsChanged += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateMonitorNavigationVisibility();
                    if (MainContent.Content is MonitorView && TACEUI.Core.SettingsManager.UsbMonitorInterface.Equals("Pro", StringComparison.OrdinalIgnoreCase))
                    {
                        NavigateToMonitorPro();
                    }
                    else if (MainContent.Content is MonitorProView && !TACEUI.Core.SettingsManager.UsbMonitorInterface.Equals("Pro", StringComparison.OrdinalIgnoreCase))
                    {
                        NavigateToMonitor();
                    }
                });
            };
        }

        public void UpdateMonitorNavigationVisibility()
        {
            bool isPro = TACEUI.Core.SettingsManager.UsbMonitorInterface.Equals("Pro", StringComparison.OrdinalIgnoreCase);
            if (isPro)
            {
                BtnMenuMonitor.Visibility = Visibility.Collapsed;
                BtnMenuMonitorPro.Visibility = Visibility.Visible;
            }
            else
            {
                BtnMenuMonitor.Visibility = Visibility.Visible;
                BtnMenuMonitorPro.Visibility = Visibility.Collapsed;
            }
        }

        public void NavigateToDefaultMonitor()
        {
            UpdateMonitorNavigationVisibility();
            if (TACEUI.Core.SettingsManager.UsbMonitorInterface.Equals("Pro", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToMonitorPro();
            }
            else
            {
                NavigateToMonitor();
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

        public void NavigateToMonitorPro()
        {
            MainContent.Content = _monitorProView;
            UpdateMenuStyles(BtnMenuMonitorPro);
        }

        private void MenuMonitorPro_Click(object sender, RoutedEventArgs e)
        {
            NavigateToMonitorPro();
        }

        private void MenuLearn_Click(object sender, RoutedEventArgs e)
        {
            NavigateToLearn();
        }

        public void NavigateToLearn()
        {
            _learnView = new BotLearnView(); // Recarrega dados ao navegar
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

            System.Windows.Controls.Button[] buttons = { BtnMenuMonitor, BtnMenuMonitorPro, BtnMenuLearn, BtnMenuRelatorios, BtnMenuConfig, BtnMenuSobre };

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
            _aboutView.AtualizarDados();
            UpdateMenuStyles(BtnMenuSobre);
        }

        private void MenuSobre_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSobre();
        }

        public void ReloadAllViewsData()
        {
            _monitorView?.LoadMunicipios();
            _monitorProView?.LoadMunicipios();
        }

        // =========================================================================
        // MODO TELA CHEIA (FULLSCREEN F11)
        // =========================================================================
        private bool _isFullScreen = false;
        private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
        private WindowState _previousWindowState = WindowState.Maximized;
        private ResizeMode _previousResizeMode = ResizeMode.CanResize;

        public void ToggleFullScreen()
        {
            if (!_isFullScreen)
            {
                // Entra em modo Fullscreen completo
                _previousWindowStyle = this.WindowStyle;
                _previousWindowState = this.WindowState;
                _previousResizeMode = this.ResizeMode;

                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
                _isFullScreen = true;

                if (BtnToggleFullScreen != null) BtnToggleFullScreen.Content = "Restaurar";
            }
            else
            {
                // Restaura o modo janela padrão
                this.WindowStyle = _previousWindowStyle;
                this.ResizeMode = _previousResizeMode;
                this.WindowState = _previousWindowState;
                _isFullScreen = false;

                if (BtnToggleFullScreen != null) BtnToggleFullScreen.Content = "F11";
            }
        }

        private void BtnToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                return;
            }

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
            else if (MainContent.Content is MonitorProView monitorPro)
            {
                if (e.Key == System.Windows.Input.Key.F1)
                {
                    monitorPro.BtnF1_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F2)
                {
                    monitorPro.BtnF2_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F3)
                {
                    monitorPro.BtnF3_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F4)
                {
                    monitorPro.BtnF4_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F5)
                {
                    monitorPro.BtnF5_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F6)
                {
                    monitorPro.BtnF6_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.F8)
                {
                    monitorPro.BtnF8_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    monitorPro.HandleEscPress();
                    e.Handled = true;
                }
            }
        }
    }
}