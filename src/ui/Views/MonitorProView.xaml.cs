using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InnovPlayUI.Core;

namespace InnovPlayUI.Views
{
    public class DeviceProModel : INotifyPropertyChanged
    {
        private string _slotIndex = "#01";
        public string SlotIndex { get => _slotIndex; set { _slotIndex = value; OnPropertyChanged(nameof(SlotIndex)); } }

        private string _serial = string.Empty;
        public string Serial { get => _serial; set { _serial = value; OnPropertyChanged(nameof(Serial)); } }

        private string _model = string.Empty;
        public string Model { get => _model; set { _model = value; OnPropertyChanged(nameof(Model)); } }

        private string _androidVersion = string.Empty;
        public string AndroidVersion { get => _androidVersion; set { _androidVersion = value; OnPropertyChanged(nameof(AndroidVersion)); } }

        private string _mac = string.Empty;
        public string MAC { get => _mac; set { _mac = value; OnPropertyChanged(nameof(MAC)); } }

        private string _battery = string.Empty;
        public string Battery
        {
            get => _battery;
            set
            {
                _battery = value;
                int parsed = 0;
                if (!string.IsNullOrEmpty(value))
                {
                    var clean = value.Replace("%", "").Trim();
                    int.TryParse(clean, out parsed);
                }
                BatteryValue = parsed;
                OnPropertyChanged(nameof(Battery));
            }
        }

        private int _batteryValue = 0;
        public int BatteryValue { get => _batteryValue; set { _batteryValue = value; OnPropertyChanged(nameof(BatteryValue)); } }

        private string _status = "Pronto";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }

        private string _statusColor = "#8570E6";
        public string StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); } }

        private string _currentStep = "Aguardando";
        public string CurrentStep { get => _currentStep; set { _currentStep = value; OnPropertyChanged(nameof(CurrentStep)); } }

        private double _progressValue = 0;
        public double ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(nameof(ProgressValue)); } }

        private string _elapsedTimeStr = "--:--";
        public string ElapsedTimeStr { get => _elapsedTimeStr; set { _elapsedTimeStr = value; OnPropertyChanged(nameof(ElapsedTimeStr)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MonitorProView : UserControl
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback([MarshalAs(UnmanagedType.LPUTF8Str)] string message);

        // =========================================================================
        // DLL IMPORTS (InnoEngine.dll)
        // =========================================================================
        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF1_Positivo(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string wp_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string package_name, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string login, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string senha, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string municipio, 
            LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF1_NokiaT20(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string wp_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string package_name, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string login, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string senha, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string municipio, 
            LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF2_NokiaT20(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string wp_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string package_name, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string login, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string senha, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string municipio, 
            LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF3_NokiaT20(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string package_name, 
            LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF2_Reinstall(string serial, string apk_path, string pacote_path, string package_name, string login, string senha, string municipio, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF3_UpdatePackage(string serial, string apk_path, string pacote_path, string package_name, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF4_Terminal(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF5_Shutdown(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF6_Sincronizacao(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF7_TabMulti(string serial, string apk_path, string pacote_path, string login, string senha, string municipio, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF8_ActiveConnection(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF2_TabMulti_Atualizar(string serial, string apk_path, string pacote_path, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF3_TabMulti_Terminal(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF4_TabMulti_Desligar(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetDeviceCount();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_CancelAll();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_ResetCancel();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_GetDevice(int index, ref MonitorView.AdbDevice out_device);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_RefreshDevices();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetMunicipios([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] MunicipioModel[] out_lista, int max_items);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void Engine_RunAdbShell(string serial, string command, StringBuilder out_log, int max_len);

        // =========================================================================
        // ESTADO E CAMPOS
        // =========================================================================
        private DispatcherTimer _timer;
        private DispatcherTimer _realtimeTimer;
        private DateTime _lastEscTime = DateTime.MinValue;
        private bool _isRefreshing = false;
        private int _metaTotal = 100;
        private int _currentGridColumns = 2;

        private ObservableCollection<DeviceProModel> _devicesList;
        public ObservableCollection<string> _comboList;
        public MunicipioModel[] _municipiosArray;

        private List<string> _allGlobalLogs = new List<string>();
        private Dictionary<string, TextBox> _deviceLogBoxes = new Dictionary<string, TextBox>();
        private Dictionary<string, Stopwatch> _deviceStopwatches = new Dictionary<string, Stopwatch>();
        private List<double> _completedExecutionTimes = new List<double>();
        private int _totalSuccessCount = 0;
        private int _totalFailCount = 0;

        public MonitorProView()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.PreviewKeyDown += Window_PreviewKeyDown;
                }
            };

            _devicesList = new ObservableCollection<DeviceProModel>();
            ListDevicesPro.ItemsSource = _devicesList;
            CardsContainerDevices.ItemsSource = _devicesList;

            _comboList = new ObservableCollection<string>();
            ComboMunicipiosPro.ItemsSource = _comboList;
            _municipiosArray = new MunicipioModel[256];

            LoadMunicipios();
            UpdateViewModeVisuals(true);
            UpdateCategoryVisuals("Positivo");
            UpdateFilterVisuals("Todos");

            LogMessage("[PRO] Inno RPA Studio inicializado. Cockpit e Telemetria em tempo real ativados.");

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(4);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Timer de 1s para telemetria fluida em tempo real
            _realtimeTimer = new DispatcherTimer();
            _realtimeTimer.Interval = TimeSpan.FromSeconds(1);
            _realtimeTimer.Tick += (s, e) =>
            {
                foreach (var dev in _devicesList)
                {
                    if (_deviceStopwatches.TryGetValue(dev.Serial, out var sw) && sw.IsRunning)
                    {
                        var ts = sw.Elapsed;
                        dev.ElapsedTimeStr = ts.TotalMinutes >= 1 
                            ? $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}" 
                            : $"{ts.Seconds:D2}s";
                    }
                }
            };
            _realtimeTimer.Start();
        }

        private static SolidColorBrush HexBrush(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        // =========================================================================
        // GESTÃO DE MUNICÍPIOS & ARQUIVOS
        // =========================================================================
        public void LoadMunicipios()
        {
            try
            {
                string? currentSelection = null;
                if (ComboMunicipiosPro.SelectedIndex >= 0 && ComboMunicipiosPro.SelectedIndex < _comboList.Count)
                {
                    currentSelection = _comboList[ComboMunicipiosPro.SelectedIndex];
                }

                int count = Engine_GetMunicipios(_municipiosArray, 256);
                _comboList.Clear();
                for (int i = 0; i < count; i++)
                {
                    _comboList.Add(_municipiosArray[i].Nome);
                }

                if (_comboList.Count > 0)
                {
                    int newIndex = 0;
                    if (currentSelection != null)
                    {
                        int idx = _comboList.IndexOf(currentSelection);
                        if (idx >= 0) newIndex = idx;
                    }
                    ComboMunicipiosPro.SelectedIndex = newIndex;
                    UpdateMunicipiosFileDisplay();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadMunicipios Error] {ex.Message}");
            }
        }

        private void ComboMunicipiosPro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMunicipiosFileDisplay();
        }

        private void UpdateMunicipiosFileDisplay()
        {
            int idx = ComboMunicipiosPro.SelectedIndex;
            if (idx >= 0 && idx < _comboList.Count)
            {
                var m = _municipiosArray[idx];

                // APK
                string fullApk = PathHelper.GetDynamicApkPath(m.Nome);
                if (!string.IsNullOrEmpty(fullApk) && File.Exists(fullApk))
                {
                    TxtApkNamePro.Text = Path.GetFileName(fullApk);
                    TxtApkNamePro.Foreground = Brushes.White;
                    IconApkPro.Text = "✔";
                    IconApkPro.Foreground = HexBrush("#4EDEA3");
                }
                else
                {
                    TxtApkNamePro.Text = "Ausente";
                    TxtApkNamePro.Foreground = HexBrush("#7A7A8A");
                    IconApkPro.Text = "✖";
                    IconApkPro.Foreground = HexBrush("#FF4D4D");
                }

                // Pacote ZIP
                string fullPacote = PathHelper.GetDynamicPacotePath(m.Nome);
                if (!string.IsNullOrEmpty(fullPacote) && File.Exists(fullPacote))
                {
                    TxtPacoteNamePro.Text = Path.GetFileName(fullPacote);
                    TxtPacoteNamePro.Foreground = Brushes.White;
                    IconPacotePro.Text = "✔";
                    IconPacotePro.Foreground = HexBrush("#4EDEA3");
                }
                else
                {
                    TxtPacoteNamePro.Text = "Não definido";
                    TxtPacoteNamePro.Foreground = HexBrush("#7A7A8A");
                    IconPacotePro.Text = "✖";
                    IconPacotePro.Foreground = HexBrush("#FF4D4D");
                }

                // Wallpaper
                string baseDir = PathHelper.GetRootDirectory();
                string wpRaw = m.WallpaperPath;
                string fullWp = "";
                if (!string.IsNullOrEmpty(wpRaw))
                {
                    fullWp = Path.IsPathRooted(wpRaw) ? wpRaw : Path.GetFullPath(Path.Combine(baseDir, wpRaw));
                }

                if (!string.IsNullOrEmpty(fullWp) && File.Exists(fullWp))
                {
                    TxtWpNamePro.Text = Path.GetFileName(fullWp);
                    TxtWpNamePro.Foreground = Brushes.White;
                    IconWpPro.Text = "✔";
                    IconWpPro.Foreground = HexBrush("#4EDEA3");
                }
                else
                {
                    TxtWpNamePro.Text = "Padrão";
                    TxtWpNamePro.Foreground = HexBrush("#7A7A8A");
                    IconWpPro.Text = "✔";
                    IconWpPro.Foreground = HexBrush("#4EDEA3");
                }
            }
        }

        private void BtnGerenciarMunicipios_Click(object sender, RoutedEventArgs e)
        {
            MunicipiosWindow win = new MunicipiosWindow();
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
            LoadMunicipios();
        }

        // =========================================================================
        // ALTERNADOR DE VISUALIZAÇÃO & DENSIDADE DE GRID (2C, 3C, 4C)
        // =========================================================================
        private void BtnViewList_Click(object sender, RoutedEventArgs e)
        {
            UpdateViewModeVisuals(true);
        }

        private void BtnViewGrid_Click(object sender, RoutedEventArgs e)
        {
            UpdateViewModeVisuals(false);
        }

        private void UpdateViewModeVisuals(bool isList)
        {
            var activeBg = HexBrush("#513BA6");
            var activeFg = Brushes.White;
            var activeBorder = HexBrush("#8570E6");

            var inactiveBg = Brushes.Transparent;
            var inactiveFg = HexBrush("#8A8A9E");
            var inactiveBorder = Brushes.Transparent;

            if (isList)
            {
                ListDevicesPro.Visibility = Visibility.Visible;
                ScrollGridDevices.Visibility = Visibility.Collapsed;
                DensityPanel.Visibility = Visibility.Collapsed;

                BtnViewList.Background = activeBg;
                BtnViewList.Foreground = activeFg;
                BtnViewList.BorderBrush = activeBorder;
                BtnViewList.BorderThickness = new Thickness(1);

                BtnViewGrid.Background = inactiveBg;
                BtnViewGrid.Foreground = inactiveFg;
                BtnViewGrid.BorderBrush = inactiveBorder;
                BtnViewGrid.BorderThickness = new Thickness(0);
            }
            else
            {
                ListDevicesPro.Visibility = Visibility.Collapsed;
                ScrollGridDevices.Visibility = Visibility.Visible;
                DensityPanel.Visibility = Visibility.Visible;

                BtnViewGrid.Background = activeBg;
                BtnViewGrid.Foreground = activeFg;
                BtnViewGrid.BorderBrush = activeBorder;
                BtnViewGrid.BorderThickness = new Thickness(1);

                BtnViewList.Background = inactiveBg;
                BtnViewList.Foreground = inactiveFg;
                BtnViewList.BorderBrush = inactiveBorder;
                BtnViewList.BorderThickness = new Thickness(0);

                SetGridColumns(_currentGridColumns);
            }
        }

        private void BtnDensity2_Click(object sender, RoutedEventArgs e) => SetGridColumns(2);
        private void BtnDensity3_Click(object sender, RoutedEventArgs e) => SetGridColumns(3);
        private void BtnDensity4_Click(object sender, RoutedEventArgs e) => SetGridColumns(4);

        private void SetGridColumns(int cols)
        {
            _currentGridColumns = cols;
            var panel = FindVisualChild<UniformGrid>(CardsContainerDevices);
            if (panel != null)
            {
                panel.Columns = cols;
            }

            var activeBg = HexBrush("#513BA6");
            var activeFg = Brushes.White;
            var inactiveBg = Brushes.Transparent;
            var inactiveFg = HexBrush("#8A8A9E");

            BtnDensity2.Background = cols == 2 ? activeBg : inactiveBg;
            BtnDensity2.Foreground = cols == 2 ? activeFg : inactiveFg;
            BtnDensity3.Background = cols == 3 ? activeBg : inactiveBg;
            BtnDensity3.Foreground = cols == 3 ? activeFg : inactiveFg;
            BtnDensity4.Background = cols == 4 ? activeBg : inactiveBg;
            BtnDensity4.Foreground = cols == 4 ? activeFg : inactiveFg;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        // =========================================================================
        // SELEÇÃO DE CATEGORIA DE HARDWARE
        // =========================================================================
        private void BtnCategoryPositivoPro_Click(object sender, RoutedEventArgs e) => UpdateCategoryVisuals("Positivo");
        private void BtnCategoryMultilaserPro_Click(object sender, RoutedEventArgs e) => UpdateCategoryVisuals("Multilaser");
        private void BtnCategoryNokiaPro_Click(object sender, RoutedEventArgs e) => UpdateCategoryVisuals("Nokia");

        private void UpdateCategoryVisuals(string category)
        {
            var activeBg = HexBrush("#513BA6");
            var activeFg = Brushes.White;
            var activeBorder = HexBrush("#8570E6");

            var inactiveBg = Brushes.Transparent;
            var inactiveFg = HexBrush("#8A8A9E");
            var inactiveBorder = Brushes.Transparent;

            BtnCategoryPositivoPro.Background = category == "Positivo" ? activeBg : inactiveBg;
            BtnCategoryPositivoPro.Foreground = category == "Positivo" ? activeFg : inactiveFg;
            BtnCategoryPositivoPro.BorderBrush = category == "Positivo" ? activeBorder : inactiveBorder;
            BtnCategoryPositivoPro.BorderThickness = new Thickness(category == "Positivo" ? 1 : 0);

            BtnCategoryMultilaserPro.Background = category == "Multilaser" ? activeBg : inactiveBg;
            BtnCategoryMultilaserPro.Foreground = category == "Multilaser" ? activeFg : inactiveFg;
            BtnCategoryMultilaserPro.BorderBrush = category == "Multilaser" ? activeBorder : inactiveBorder;
            BtnCategoryMultilaserPro.BorderThickness = new Thickness(category == "Multilaser" ? 1 : 0);

            BtnCategoryNokiaPro.Background = category == "Nokia" ? activeBg : inactiveBg;
            BtnCategoryNokiaPro.Foreground = category == "Nokia" ? activeFg : inactiveFg;
            BtnCategoryNokiaPro.BorderBrush = category == "Nokia" ? activeBorder : inactiveBorder;
            BtnCategoryNokiaPro.BorderThickness = new Thickness(category == "Nokia" ? 1 : 0);

            GridPositivoActionsPro.Visibility = category == "Positivo" ? Visibility.Visible : Visibility.Collapsed;
            GridMultilaserActionsPro.Visibility = category == "Multilaser" ? Visibility.Visible : Visibility.Collapsed;
            GridNokiaActionsPro.Visibility = category == "Nokia" ? Visibility.Visible : Visibility.Collapsed;

            ComboAutoActionPro.Items.Clear();
            if (category == "Positivo")
            {
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F1] Sincronização + Instalação" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F2] Limpeza e Instalação" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F3] Atualização de Pacote" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F5] Desligamento Remoto" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F6] Sincronização Avançada" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F8] Conexão Ativa (Gnirehtet)" });
            }
            else if (category == "Multilaser")
            {
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F1] Instalação + Prep (Tab-MULTI)" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F2] Atualizar Pacote" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F4] Desligar Aparelhos" });
            }
            else
            {
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F1] Sincronização + Instalação (Nokia T20)" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F2] Reinstalação - Preserva Apps" });
                ComboAutoActionPro.Items.Add(new ComboBoxItem { Content = "[F3] Atualizar Pacote (Nokia T20)" });
            }
            ComboAutoActionPro.SelectedIndex = 0;
        }

        // =========================================================================
        // POLLING ADB E TELEMETRIA
        // =========================================================================
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            Task.Run(() =>
            {
                try
                {
                    int activeCount = Engine_RefreshDevices();
                    int currentListCount = Engine_GetDeviceCount();

                    var tempList = new List<DeviceProModel>();
                    for (int i = 0; i < currentListCount; i++)
                    {
                        var dev = new MonitorView.AdbDevice();
                        Engine_GetDevice(i, ref dev);

                        if (dev.authorized == 1 && !string.IsNullOrEmpty(dev.serial))
                        {
                            string batStr = string.IsNullOrEmpty(dev.battery) ? "0%" : dev.battery;
                            if (!batStr.EndsWith("%")) batStr += "%";

                            tempList.Add(new DeviceProModel
                            {
                                SlotIndex = $"#{i + 1:D2}",
                                Serial = dev.serial,
                                Model = string.IsNullOrEmpty(dev.model) ? "Android Device" : dev.model,
                                AndroidVersion = dev.android_version ?? "11",
                                MAC = dev.mac ?? "",
                                Battery = batStr,
                                Status = "Pronto",
                                StatusColor = "#4EDEA3",
                                CurrentStep = "Pronto",
                                ProgressValue = 0
                            });
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        foreach (var newDev in tempList)
                        {
                            var existing = FindDevice(newDev.Serial);
                            if (existing == null)
                            {
                                _devicesList.Add(newDev);
                                EnsureDeviceTab(newDev.Serial);

                                if (ChkAutoInstallPro.IsChecked == true)
                                {
                                    RunAutoInstallForDevice(newDev);
                                }
                            }
                            else
                            {
                                existing.Battery = newDev.Battery;
                                existing.Model = newDev.Model;
                                existing.AndroidVersion = newDev.AndroidVersion;
                                existing.SlotIndex = newDev.SlotIndex;
                            }
                        }

                        for (int i = _devicesList.Count - 1; i >= 0; i--)
                        {
                            if (!tempList.Exists(d => d.Serial == _devicesList[i].Serial))
                            {
                                _devicesList.RemoveAt(i);
                            }
                        }

                        UpdateKpiMetrics();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Timer_Tick Pro Error] {ex.Message}");
                }
                finally
                {
                    _isRefreshing = false;
                }
            });
        }

        private DeviceProModel? FindDevice(string serial)
        {
            foreach (var d in _devicesList)
            {
                if (d.Serial == serial) return d;
            }
            return null;
        }

        private List<DeviceProModel> GetTargetDevices()
        {
            var list = new List<DeviceProModel>();
            if (ListDevicesPro.SelectedItems.Count > 0)
            {
                foreach (var item in ListDevicesPro.SelectedItems)
                {
                    if (item is DeviceProModel dev) list.Add(dev);
                }
            }
            else
            {
                foreach (var dev in _devicesList) list.Add(dev);
            }
            return list;
        }

        private void UpdateKpiMetrics()
        {
            int count = _devicesList.Count;
            TxtKpiActiveDevices.Text = count.ToString();
            TxtDeviceCountBadge.Text = $"[{count}]";

            if (_completedExecutionTimes.Count > 0)
            {
                double sum = 0;
                foreach (var t in _completedExecutionTimes) sum += t;
                double avg = sum / _completedExecutionTimes.Count;
                
                if (avg < 60)
                {
                    TxtKpiAvgTime.Text = $"{avg:F0}s";
                }
                else
                {
                    int mins = (int)(avg / 60);
                    int secs = (int)(avg % 60);
                    TxtKpiAvgTime.Text = $"{mins}m {secs:D2}s";
                }

                if (avg > 0)
                {
                    double throughput = (3600.0 / avg) * Math.Max(1, count);
                    TxtKpiThroughput.Text = $"{throughput:F0}";
                }
            }
            else
            {
                TxtKpiAvgTime.Text = "--s";
                TxtKpiThroughput.Text = "0";
            }

            TxtKpiCompletedCount.Text = _totalSuccessCount.ToString();
            int totalSessions = _totalSuccessCount + _totalFailCount;
            if (totalSessions > 0)
            {
                double rate = (_totalSuccessCount / (double)totalSessions) * 100.0;
                TxtKpiSuccessRate.Text = $" ({rate:F0}% OK)";
            }
            else
            {
                TxtKpiSuccessRate.Text = " (100% OK)";
            }

            // Meta de Produção
            double metaPct = Math.Min(100.0, (_totalSuccessCount / (double)_metaTotal) * 100.0);
            ProgressMetaLote.Value = metaPct;
            TxtMetaLabel.Text = $"{_totalSuccessCount} / {_metaTotal} ({metaPct:F0}%)";
        }

        // =========================================================================
        // CONSOLE DE LOGS COM FILTROS & EXPORTAÇÃO
        // =========================================================================
        public void LogMessage(string message)
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            string formatted = $"[{timeStamp}] {message}";

            _allGlobalLogs.Add(formatted);

            TxtLogGlobalPro.AppendText(formatted + Environment.NewLine);
            TxtLogGlobalPro.ScrollToEnd();

            string? targetSerial = null;
            if (message.StartsWith("["))
            {
                int closeIdx = message.IndexOf(']');
                if (closeIdx > 1)
                {
                    string candidate = message.Substring(1, closeIdx - 1).Trim();
                    if (!candidate.Equals("PRO", StringComparison.OrdinalIgnoreCase) &&
                        !candidate.Equals("INFO", StringComparison.OrdinalIgnoreCase) &&
                        !candidate.Equals("ALERTA", StringComparison.OrdinalIgnoreCase) &&
                        !candidate.Equals("AVISO", StringComparison.OrdinalIgnoreCase))
                    {
                        targetSerial = candidate;
                    }
                }
            }

            if (targetSerial != null)
            {
                EnsureDeviceTab(targetSerial);
                if (_deviceLogBoxes.TryGetValue(targetSerial, out var box))
                {
                    box.AppendText(formatted + Environment.NewLine);
                    box.ScrollToEnd();
                }

                UpdateDeviceStatusFromLog(targetSerial, message);
            }
        }

        private void EnsureDeviceTab(string serial)
        {
            if (_deviceLogBoxes.ContainsKey(serial)) return;

            var tab = new TabItem { Header = $"📱 {serial}" };
            var box = new TextBox
            {
                Background = HexBrush("#0A0A0F"),
                Foreground = HexBrush("#4EDEA3"),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsReadOnly = true,
                Padding = new Thickness(8, 8, 8, 8),
                Margin = new Thickness(0, 2, 0, 0)
            };

            tab.Content = box;
            LogTabsPro.Items.Add(tab);
            _deviceLogBoxes[serial] = box;
        }

        private void UpdateDeviceStatusFromLog(string serial, string message)
        {
            var dev = FindDevice(serial);
            if (dev == null) return;

            string lower = message.ToLowerInvariant();

            if (lower.Contains("concluído") || lower.Contains("concluido") || lower.Contains("sucesso") || lower.Contains("finalizado"))
            {
                dev.Status = "Concluído";
                dev.StatusColor = "#4EDEA3";
                dev.CurrentStep = "Concluído ✔";
                dev.ProgressValue = 100;
                ExecutionProgressPro.Value = 100;
                LblProgressPro.Text = "100%";

                if (_deviceStopwatches.TryGetValue(serial, out var sw))
                {
                    sw.Stop();
                    double totalSecs = sw.Elapsed.TotalSeconds;
                    _completedExecutionTimes.Add(totalSecs);

                    int mins = (int)sw.Elapsed.TotalMinutes;
                    int secs = sw.Elapsed.Seconds;
                    dev.ElapsedTimeStr = mins >= 1 ? $"{mins}m {secs:D2}s" : $"{secs}s";

                    TxtLogGlobalPro.AppendText($"[{DateTime.Now:HH:mm:ss}] [TELEMETRIA] [{serial}] Aparelho finalizado com sucesso em {dev.ElapsedTimeStr}." + Environment.NewLine);
                    _deviceStopwatches.Remove(serial);
                }
                _totalSuccessCount++;
                UpdateKpiMetrics();
            }
            else if (lower.Contains("erro") || lower.Contains("falha") || lower.Contains("abortado") || lower.Contains("cancelado"))
            {
                dev.Status = "Erro";
                dev.StatusColor = "#FF4D4D";
                dev.CurrentStep = "Falha ✖";

                if (_deviceStopwatches.TryGetValue(serial, out var sw))
                {
                    sw.Stop();
                    _deviceStopwatches.Remove(serial);
                }
                _totalFailCount++;
                UpdateKpiMetrics();
            }
            else if (lower.Contains("iniciando") || lower.Contains("etapa") || lower.Contains("executando"))
            {
                dev.Status = "Executando";
                dev.StatusColor = "#00E5FF";

                if (!_deviceStopwatches.ContainsKey(serial))
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    _deviceStopwatches[serial] = sw;
                }

                if (lower.Contains("etapa 1")) { dev.CurrentStep = "Etapa 1/7: Hardware"; dev.ProgressValue = 15; ExecutionProgressPro.Value = 15; LblProgressPro.Text = "15%"; }
                else if (lower.Contains("etapa 2")) { dev.CurrentStep = "Etapa 2/7: Município"; dev.ProgressValue = 30; ExecutionProgressPro.Value = 30; LblProgressPro.Text = "30%"; }
                else if (lower.Contains("etapa 3")) { dev.CurrentStep = "Etapa 3/7: E-mail"; dev.ProgressValue = 50; ExecutionProgressPro.Value = 50; LblProgressPro.Text = "50%"; }
                else if (lower.Contains("etapa 4")) { dev.CurrentStep = "Etapa 4/7: Senha & Login"; dev.ProgressValue = 70; ExecutionProgressPro.Value = 70; LblProgressPro.Text = "70%"; }
                else if (lower.Contains("etapa 5")) { dev.CurrentStep = "Etapa 5/7: Menu ZIP"; dev.ProgressValue = 85; ExecutionProgressPro.Value = 85; LblProgressPro.Text = "85%"; }
                else if (lower.Contains("etapa 6")) { dev.CurrentStep = "Etapa 6/7: Importando"; dev.ProgressValue = 95; ExecutionProgressPro.Value = 95; LblProgressPro.Text = "95%"; }
                else if (lower.Contains("etapa 7")) { dev.CurrentStep = "Etapa 7/7: Conclusão"; dev.ProgressValue = 98; ExecutionProgressPro.Value = 98; LblProgressPro.Text = "98%"; }
            }
        }

        // =========================================================================
        // FILTROS RÁPIDOS DO LOG
        // =========================================================================
        private void BtnFilterAll_Click(object sender, RoutedEventArgs e) => ApplyLogFilter("Todos");
        private void BtnFilterSuccess_Click(object sender, RoutedEventArgs e) => ApplyLogFilter("Sucessos");
        private void BtnFilterError_Click(object sender, RoutedEventArgs e) => ApplyLogFilter("Erros");
        private void BtnFilterSteps_Click(object sender, RoutedEventArgs e) => ApplyLogFilter("Etapas");

        private void ApplyLogFilter(string mode)
        {
            UpdateFilterVisuals(mode);
            TxtLogGlobalPro.Clear();

            foreach (var line in _allGlobalLogs)
            {
                string lower = line.ToLowerInvariant();
                bool include = mode switch
                {
                    "Sucessos" => lower.Contains("sucesso") || lower.Contains("concluído") || lower.Contains("concluido"),
                    "Erros" => lower.Contains("erro") || lower.Contains("falha") || lower.Contains("abortado"),
                    "Etapas" => lower.Contains("etapa") || lower.Contains("iniciando"),
                    _ => true
                };

                if (include)
                {
                    TxtLogGlobalPro.AppendText(line + Environment.NewLine);
                }
            }
            TxtLogGlobalPro.ScrollToEnd();
        }

        private void UpdateFilterVisuals(string mode)
        {
            var activeBg = HexBrush("#513BA6");
            var activeFg = Brushes.White;
            var inactiveBg = Brushes.Transparent;
            var inactiveFg = HexBrush("#8A8A9E");

            BtnFilterAll.Background = mode == "Todos" ? activeBg : inactiveBg;
            BtnFilterAll.Foreground = mode == "Todos" ? activeFg : inactiveFg;

            BtnFilterSuccess.Background = mode == "Sucessos" ? activeBg : inactiveBg;
            BtnFilterSuccess.Foreground = mode == "Sucessos" ? activeFg : inactiveFg;

            BtnFilterError.Background = mode == "Erros" ? activeBg : inactiveBg;
            BtnFilterError.Foreground = mode == "Erros" ? activeFg : inactiveFg;

            BtnFilterSteps.Background = mode == "Etapas" ? activeBg : inactiveBg;
            BtnFilterSteps.Foreground = mode == "Etapas" ? activeFg : inactiveFg;
        }

        private void TxtLogFilterPro_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = TxtLogFilterPro.Text.Trim();
            if (!string.IsNullOrEmpty(filter))
            {
                string content = TxtLogGlobalPro.Text;
                int idx = content.LastIndexOf(filter, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    TxtLogGlobalPro.Select(idx, filter.Length);
                }
            }
        }

        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LogTabsPro.SelectedItem is TabItem tab && tab.Content is TextBox box)
                {
                    if (!string.IsNullOrEmpty(box.Text))
                    {
                        Clipboard.SetText(box.Text);
                        LogMessage("[PRO] Conteúdo do log copiado para a área de transferência.");
                    }
                }
                else if (!string.IsNullOrEmpty(TxtLogGlobalPro.Text))
                {
                    Clipboard.SetText(TxtLogGlobalPro.Text);
                    LogMessage("[PRO] Conteúdo do log global copiado para a área de transferência.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BtnCopyLog Error] {ex.Message}");
            }
        }

        private void BtnExportLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = PathHelper.GetRootDirectory();
                string logsDir = Path.Combine(baseDir, "logs");
                Directory.CreateDirectory(logsDir);

                string fileName = $"InnoRPA_Studio_Session_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string fullPath = Path.Combine(logsDir, fileName);

                File.WriteAllLines(fullPath, _allGlobalLogs);
                MessageBox.Show($"Log da sessão exportado com sucesso!\n\nArquivo: {fullPath}", "Exportação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao exportar log: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearLogPro_Click(object sender, RoutedEventArgs e)
        {
            _allGlobalLogs.Clear();
            TxtLogGlobalPro.Clear();
            foreach (var box in _deviceLogBoxes.Values) box.Clear();
            ExecutionProgressPro.Value = 0;
            LblProgressPro.Text = "0%";
            LogMessage("[PRO] Logs reinicializados.");
        }

        // =========================================================================
        // DISPARO EM LOTE (BATCH) & DESLIGAMENTO DE CONCLUÍDOS
        // =========================================================================
        private void BtnRunAll_Click(object sender, RoutedEventArgs e)
        {
            if (_devicesList.Count == 0)
            {
                MessageBox.Show("Nenhum aparelho conectado para executar em lote!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogMessage($"[BATCH] Disparo em lote acionado para {_devicesList.Count} aparelho(s)...");
            BtnF1_Click(sender, e);
        }

        private void BtnShutdownCompleted_Click(object sender, RoutedEventArgs e)
        {
            var completedDevices = new List<DeviceProModel>();
            foreach (var d in _devicesList)
            {
                if (d.Status == "Concluído" || d.ProgressValue >= 100)
                {
                    completedDevices.Add(d);
                }
            }

            if (completedDevices.Count == 0)
            {
                MessageBox.Show("Nenhum aparelho concluído (100%) encontrado para desligamento!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LogMessage($"[BATCH] Desligando {completedDevices.Count} aparelho(s) concluído(s)...");
            foreach (var dev in completedDevices)
            {
                string serial = dev.Serial;
                Task.Run(() =>
                {
                    StringBuilder sb = new StringBuilder(512);
                    Engine_RunAdbShell(serial, "svc wifi disable && svc bluetooth disable && settings put secure location_mode 0 && reboot -p", sb, 512);
                    Dispatcher.Invoke(() => LogMessage($"[{serial}] Aparelho concluído desligado com sucesso."));
                });
            }
        }

        // =========================================================================
        // MINI TOOLBAR DE CADA CARD INDIVIDUAL
        // =========================================================================
        private void BtnCardRun_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serial)
            {
                var dev = FindDevice(serial);
                if (dev != null)
                {
                    RunAutoInstallForDevice(dev);
                }
            }
        }

        private void BtnCardStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serial)
            {
                Task.Run(() =>
                {
                    StringBuilder sb = new StringBuilder(512);
                    Engine_RunAdbShell(serial, "am force-stop br.com.yantec.innovava && am force-stop com.google.android.documentsui", sb, 512);
                    Dispatcher.Invoke(() => LogMessage($"[{serial}] Processos encerrados pelo botão Parar individual."));
                });
            }
        }

        private void BtnCardShutdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serial)
            {
                Task.Run(() =>
                {
                    StringBuilder sb = new StringBuilder(512);
                    Engine_RunAdbShell(serial, "svc wifi disable && svc bluetooth disable && reboot -p", sb, 512);
                    Dispatcher.Invoke(() => LogMessage($"[{serial}] Comando de desligamento enviado."));
                });
            }
        }

        // =========================================================================
        // EXECUÇÃO DE FUNÇÕES F1 A F8
        // =========================================================================
        private (string apkPath, string pacotePath, string wpPath, string login, string senha, string municipio) GetSelectedMunicipioData()
        {
            string apkPath = "", pacotePath = "", wpPath = "", login = "", senha = "", municipio = "";
            int idx = ComboMunicipiosPro.SelectedIndex;
            if (idx >= 0 && idx < _comboList.Count)
            {
                var m = _municipiosArray[idx];
                string baseDir = PathHelper.GetRootDirectory();

                apkPath = PathHelper.GetDynamicApkPath(m.Nome);
                if (!string.IsNullOrEmpty(apkPath) && !Path.IsPathRooted(apkPath))
                    apkPath = Path.GetFullPath(Path.Combine(baseDir, apkPath));

                pacotePath = PathHelper.GetDynamicPacotePath(m.Nome);
                if (!string.IsNullOrEmpty(pacotePath) && !Path.IsPathRooted(pacotePath))
                    pacotePath = Path.GetFullPath(Path.Combine(baseDir, pacotePath));

                string wpRaw = m.WallpaperPath;
                if (!string.IsNullOrEmpty(wpRaw))
                    wpPath = Path.IsPathRooted(wpRaw) ? wpRaw : Path.GetFullPath(Path.Combine(baseDir, wpRaw));

                login = m.Login;
                senha = m.Senha;
                municipio = m.Nome;
            }
            return (apkPath, pacotePath, wpPath, login, senha, municipio);
        }

        public void BtnF1_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0)
            {
                MessageBox.Show("Nenhum aparelho Android autorizado conectado via USB!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var data = GetSelectedMunicipioData();
            if (string.IsNullOrEmpty(data.apkPath))
            {
                MessageBox.Show("Nenhum APK configurado para este município!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));

                Task.Run(() =>
                {
                    bool isNokia = dev.Model.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || dev.Model.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isM7 = dev.Model.IndexOf("Multilaser", StringComparison.OrdinalIgnoreCase) >= 0 || dev.Model.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isNokia)
                        Engine_ExecuteF1_NokiaT20(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    else if (isM7)
                        Engine_ExecuteF7_TabMulti(serial, data.apkPath, data.pacotePath, data.login, data.senha, data.municipio, cb);
                    else
                        Engine_ExecuteF1_Positivo(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnF2_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF2_Reinstall(serial, data.apkPath, data.pacotePath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnF3_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF3_UpdatePackage(serial, data.apkPath, data.pacotePath, "br.com.yantec.innovava", cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnF4_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF4_Terminal(serial, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnF5_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF5_Shutdown(serial, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnF6_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF6_Sincronizacao(serial, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnF8_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF8_ActiveConnection(serial, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnM7_F1_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF7_TabMulti(serial, data.apkPath, data.pacotePath, data.login, data.senha, data.municipio, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnM7_F2_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF2_TabMulti_Atualizar(serial, data.apkPath, data.pacotePath, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnM7_F3_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF3_TabMulti_Terminal(serial, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnM7_F4_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF4_TabMulti_Desligar(serial, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnNokiaF1_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF1_NokiaT20(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnNokiaF2_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF2_NokiaT20(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnNokiaF3_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetTargetDevices();
            if (targets.Count == 0) return;

            var data = GetSelectedMunicipioData();
            Engine_ResetCancel();
            foreach (var dev in targets)
            {
                string serial = dev.Serial;
                LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));
                Task.Run(() =>
                {
                    Engine_ExecuteF3_NokiaT20(serial, data.apkPath, data.pacotePath, "br.com.yantec.innovava", cb);
                    GC.KeepAlive(cb);
                });
            }
        }

        public void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            Engine_CancelAll();
            LogMessage("[ALERTA] PARADA GERAL ACIONADA (Botão Parar).");
        }

        private void RunAutoInstallForDevice(DeviceProModel dev)
        {
            var data = GetSelectedMunicipioData();
            if (string.IsNullOrEmpty(data.apkPath)) return;

            int action = ComboAutoActionPro.SelectedIndex;
            string serial = dev.Serial;
            LogCallback cb = msg => Dispatcher.Invoke(() => LogMessage($"[{serial}] {msg}"));

            LogMessage($"[{serial}] [PnP] Automação Plug & Play disparada...");
            Task.Run(() =>
            {
                bool isNokia = dev.Model.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || dev.Model.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isM7 = dev.Model.IndexOf("Multilaser", StringComparison.OrdinalIgnoreCase) >= 0 || dev.Model.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isNokia)
                {
                    if (action == 0) Engine_ExecuteF1_NokiaT20(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    else if (action == 1) Engine_ExecuteF2_NokiaT20(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    else if (action == 2) Engine_ExecuteF3_NokiaT20(serial, data.apkPath, data.pacotePath, "br.com.yantec.innovava", cb);
                }
                else if (isM7)
                {
                    if (action == 0) Engine_ExecuteF7_TabMulti(serial, data.apkPath, data.pacotePath, data.login, data.senha, data.municipio, cb);
                    else if (action == 1) Engine_ExecuteF2_TabMulti_Atualizar(serial, data.apkPath, data.pacotePath, cb);
                    else if (action == 2) Engine_ExecuteF4_TabMulti_Desligar(serial, cb);
                }
                else
                {
                    if (action == 0) Engine_ExecuteF1_Positivo(serial, data.apkPath, data.pacotePath, data.wpPath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    else if (action == 1) Engine_ExecuteF2_Reinstall(serial, data.apkPath, data.pacotePath, "br.com.yantec.innovava", data.login, data.senha, data.municipio, cb);
                    else if (action == 2) Engine_ExecuteF3_UpdatePackage(serial, data.apkPath, data.pacotePath, "br.com.yantec.innovava", cb);
                    else if (action == 3) Engine_ExecuteF5_Shutdown(serial, cb);
                    else if (action == 4) Engine_ExecuteF6_Sincronizacao(serial, cb);
                    else if (action == 5) Engine_ExecuteF8_ActiveConnection(serial, cb);
                }
                GC.KeepAlive(cb);
            });
        }

        public void HandleEscPress()
        {
            if (DateTime.Now.Subtract(_lastEscTime).TotalSeconds < 1.0)
            {
                Engine_CancelAll();
                LogMessage("[ALERTA] PARADA GERAL ENVIADA (Duplo ESC)");
            }
            else
            {
                LogMessage("[AVISO] Pressione ESC novamente para PARADA GERAL");
            }
            _lastEscTime = DateTime.Now;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HandleEscPress();
            }
        }
    }
}
