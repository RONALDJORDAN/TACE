using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace InnovPlayUI.Views
{
    public class DeviceModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _serial = string.Empty;
        public string Serial { get => _serial; set { _serial = value; OnPropertyChanged(nameof(Serial)); } }

        private string _model = string.Empty;
        public string Model { get => _model; set { _model = value; OnPropertyChanged(nameof(Model)); } }

        private string _androidVersion = string.Empty;
        public string AndroidVersion { get => _androidVersion; set { _androidVersion = value; OnPropertyChanged(nameof(AndroidVersion)); } }

        private string _mac = string.Empty;
        public string MAC { get => _mac; set { _mac = value; OnPropertyChanged(nameof(MAC)); } }

        private string _battery = string.Empty;
        public string Battery { 
            get => _battery; 
            set { 
                _battery = value; 
                int parsed = 0;
                if (!string.IsNullOrEmpty(value)) {
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

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct MunicipioModel
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Nome;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Login;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Senha;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string WallpaperPath;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Nome) ? "Município sem Nome" : Nome;
        }
    }

    public partial class MonitorView : UserControl
    {
        private int currentTerminalMode = 0; // 3 para F3, 4 para F4

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback([MarshalAs(UnmanagedType.LPUTF8Str)] string message);



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


        public void BtnF1_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando F1 em {targetDevices.Count} aparelho(s)...";
                
                string apkPath = "";
                string pacotePath = "";
                string packageName = "";
                string login = "";
                string senha = "";
                string municipio = "";
                string wpPath = "";
                
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    
                    // O executável roda em data/output/, mas os caminhos do CSV são data/input/
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                    {
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));
                    }

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                    {
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));
                    }

                    string wpRaw = m.WallpaperPath;
                    if (!string.IsNullOrEmpty(wpRaw))
                    {
                        if (!System.IO.Path.IsPathRooted(wpRaw))
                            wpPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, wpRaw));
                        else
                            wpPath = wpRaw;
                    }

                    login = m.Login;
                    senha = m.Senha;
                    municipio = m.Nome;
                    packageName = "br.com.yantec.innovava"; 
                }
                
                if (string.IsNullOrEmpty(apkPath))
                {
                    MessageBox.Show("Nenhum APK configurado para este município!", "Erro de Instalação", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalWpPath = wpPath;
                    string finalPackageName = packageName;
                    string finalLogin = login;
                    string finalSenha = senha;
                    string finalMunicipio = municipio;

                    LogCallback callback = new LogCallback((msg) =>
                    {
                        Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                    });

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        bool isNokia = device != null && device.Model != null && (device.Model.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || device.Model.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isNokia)
                        {
                            Engine_ExecuteF1_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        }
                        else
                        {
                            Engine_ExecuteF1_Positivo(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        }
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android autorizado conectado via cabo USB!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BtnNokiaF2_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando F2 (Nokia T20 - Preserva Apps) em {targetDevices.Count} aparelho(s)...";
                
                string apkPath = "";
                string pacotePath = "";
                string packageName = "";
                string login = "";
                string senha = "";
                string municipio = "";
                string wpPath = "";
                
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                    {
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));
                    }

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                    {
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));
                    }

                    string wpRaw = m.WallpaperPath;
                    if (!string.IsNullOrEmpty(wpRaw))
                    {
                        if (!System.IO.Path.IsPathRooted(wpRaw))
                            wpPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, wpRaw));
                        else
                            wpPath = wpRaw;
                    }

                    login = m.Login;
                    senha = m.Senha;
                    municipio = m.Nome;
                    packageName = "br.com.yantec.innovava"; 
                }
                
                if (string.IsNullOrEmpty(apkPath))
                {
                    MessageBox.Show("Nenhum APK configurado para este município!", "Erro de Instalação", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalWpPath = wpPath;
                    string finalPackageName = packageName;
                    string finalLogin = login;
                    string finalSenha = senha;
                    string finalMunicipio = municipio;

                    LogCallback callback = new LogCallback((msg) =>
                    {
                        Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                    });

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF2_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Nokia T20 autorizado conectado via cabo USB!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BtnNokiaF3_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando F3 (Nokia T20 - Atualização de Pacote) em {targetDevices.Count} aparelho(s)...";
                
                string apkPath = "";
                string pacotePath = "";
                string packageName = "";
                
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                    {
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));
                    }

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                    {
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));
                    }

                    packageName = "br.com.yantec.innovava"; 
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalPackageName = packageName;

                    LogCallback callback = new LogCallback((msg) =>
                    {
                        Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                    });

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF3_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalPackageName, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Nokia T20 autorizado conectado via cabo USB!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunAutoInstallForDevice(DeviceModel device)
        {
            string apkPath = "";
            string pacotePath = "";
            string packageName = "";
            string login = "";
            string senha = "";
            string municipio = "";
            string wpPath = "";
            
            int idx = -1;
            idx = ComboMunicipios.SelectedIndex;
            
            if (idx >= 0 && idx < _comboList.Count)
            {
                var m = _municipiosArray[idx];
                string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                
                apkPath = GetDynamicApkPath(m.Nome);
                if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                    apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));

                pacotePath = GetDynamicPacotePath(m.Nome);
                if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                    pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));

                login = m.Login;
                senha = m.Senha;
                municipio = m.Nome;
                packageName = "br.com.yantec.innovava"; 

                string wpRaw = m.WallpaperPath;
                if (!string.IsNullOrEmpty(wpRaw))
                {
                    if (!System.IO.Path.IsPathRooted(wpRaw))
                        wpPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, wpRaw));
                    else
                        wpPath = wpRaw;
                }
            }
            
            int actionIdx = ComboAutoAction.SelectedIndex;
            
            if ((actionIdx == 0 || actionIdx == 1) && string.IsNullOrEmpty(apkPath))
            {
                MessageBox.Show("Nenhum APK configurado para este município (Automação cancelada).", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string finalSerial = device.Serial;
            string finalApkPath = apkPath;
            string finalPacotePath = pacotePath;
            string finalWpPath = wpPath;
            string finalPackageName = packageName;
            string finalLogin = login;
            string finalSenha = senha;
            string finalMunicipio = municipio;

            LogCallback callback = new LogCallback((msg) =>
            {
                Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
            });

            bool isMulti = GridMultilaserActions.Visibility == Visibility.Visible;
            bool isNokiaGrid = GridNokiaActions != null && GridNokiaActions.Visibility == Visibility.Visible;

            if (isMulti)
            {
                if (actionIdx == 0)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando processo F1 (Tab-Multi) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF7_TabMulti(finalSerial, finalApkPath, finalPacotePath, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 1)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando Atualização M7 automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF2_TabMulti_Atualizar(finalSerial, finalApkPath, finalPacotePath, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 2)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando Desligamento M7 automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF4_TabMulti_Desligar(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 3)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando F5 (Tab-Multi: Login 30s + Shutdown) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF5_TabMulti(finalSerial, finalApkPath, finalPacotePath, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else if (isNokiaGrid)
            {
                if (actionIdx == 0)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando processo F1 (Nokia T20) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF1_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 1)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando processo F2 (Nokia T20 - Preserva Apps) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF2_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 2)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando Atualização de Pacote F3 (Nokia T20) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF3_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalPackageName, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }

            else
            {
                if (actionIdx == 0)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando processo F1 automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF1_Positivo(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 1)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando processo F2 (Limpeza e Instalação) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF2_Reinstall(finalSerial, finalApkPath, finalPacotePath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 2)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando Atualização de Pacote (F3) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        bool isNokia = device != null && device.Model != null && (device.Model.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || device.Model.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0);
                        bool isM7 = device != null && device.Model != null && (device.Model.IndexOf("Multilaser", StringComparison.OrdinalIgnoreCase) >= 0 || device.Model.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0 || device.Model.IndexOf("TabMulti", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isNokia)
                            Engine_ExecuteF3_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalPackageName, callback);
                        else if (isM7)
                            Engine_ExecuteF2_TabMulti_Atualizar(finalSerial, finalApkPath, finalPacotePath, callback);
                        else
                            Engine_ExecuteF3_UpdatePackage(finalSerial, finalApkPath, finalPacotePath, finalPackageName, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 3)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando desligamento (F5) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF5_Shutdown(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 4)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando Sincronização Avançada (F6) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF6_Sincronizacao(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
                else if (actionIdx == 5)
                {
                    LogMessage($"[{finalSerial}] Automação ativada! Iniciando Conexão Ativa (F8 - Gnirehtet) automaticamente...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF8_ActiveConnection(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct AdbDevice
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string serial;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string model;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string manufacturer;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string android_version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string api_level;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string battery;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string imei;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string mac;
            public int authorized;
        }

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF2_Reinstall(string serial, string apk_path, string pacote_path, string package_name, string login, string senha, string municipio, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF7_TabMulti(string serial, string apk_path, string pacote_path, string login, string senha, string municipio, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void Engine_RunAdbShell(string serial, string command, StringBuilder out_log, int max_len);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF2_TabMulti_Atualizar(string serial, string apk_path, string pacote_path, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF3_TabMulti_Terminal(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF4_TabMulti_Desligar(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF5_TabMulti(string serial, string apk_path, string pacote_path, string login, string senha, string municipio, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetDeviceCount();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_CancelAll();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_SetPaused(int paused);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_ResetCancel();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_GetDevice(int index, ref AdbDevice out_device);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_RefreshDevices();

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetMunicipios([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] MunicipioModel[] out_lista, int max_items);

        private DispatcherTimer _timer;
        private DateTime _lastEscTime = DateTime.MinValue;
#pragma warning disable CS0414
        private bool _isPaused = false;
#pragma warning restore CS0414
        private bool _isRefreshing = false;        // anti-overlap flag
        private const int LOG_MAX_LINES = 500;     // buffer circular de log

        private ObservableCollection<DeviceModel> _devicesList;
        public ObservableCollection<string> _comboList;
        public MunicipioModel[] _municipiosArray;
        // Cache de seriais já exibidos para diff inteligente
        private System.Collections.Generic.HashSet<string> _knownSerials = new System.Collections.Generic.HashSet<string>();
        
        // Gerenciamento de Abas dinâmicas
        private System.Collections.Generic.Dictionary<string, TextBox> _deviceLogBoxes = new System.Collections.Generic.Dictionary<string, TextBox>();
        private System.Collections.Generic.Dictionary<string, int> _deviceLogCounts = new System.Collections.Generic.Dictionary<string, int>();
        private System.Collections.Generic.Dictionary<string, DateTime> _disconnectTimes = new System.Collections.Generic.Dictionary<string, DateTime>();
        
        // Gerenciamento de ProgressBars
        private System.Collections.Generic.Dictionary<string, ProgressBar> _deviceProgressBars = new System.Collections.Generic.Dictionary<string, ProgressBar>();
        private System.Collections.Generic.Dictionary<string, TextBlock> _deviceProgressLabels = new System.Collections.Generic.Dictionary<string, TextBlock>();
        private System.Collections.Generic.Dictionary<string, TextBlock> _deviceProgressSerialLabels = new System.Collections.Generic.Dictionary<string, TextBlock>();
        private System.Collections.Generic.Dictionary<string, Grid> _deviceProgressContainers = new System.Collections.Generic.Dictionary<string, Grid>();

        public MonitorView()
        {
            this.Loaded += (s, e) => {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null) {
                    window.PreviewKeyDown += Window_PreviewKeyDown;
                }
            };
            InitializeComponent();
            _devicesList = new ObservableCollection<DeviceModel>();
            ListDevices.ItemsSource = _devicesList;

            _comboList = new ObservableCollection<string>();
            ComboMunicipios.ItemsSource = _comboList;
            _municipiosArray = new MunicipioModel[256];

            LoadMunicipios();

            LogMessage("Motor Inno RPA inicializado. Aguardando conexão de aparelhos ADB...");
            
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5); // aumentado de 2s para 5s
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public void LoadMunicipios()
        {
            try
            {
                string? currentSelection = null;
                if (ComboMunicipios.SelectedIndex >= 0 && ComboMunicipios.SelectedIndex < _comboList.Count)
                {
                    currentSelection = _comboList[ComboMunicipios.SelectedIndex];
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
                        if (idx >= 0)
                        {
                            newIndex = idx;
                        }
                    }
                    ComboMunicipios.SelectedIndex = newIndex;
                    UpdateMunicipiosFileDisplay();
                }
            }
            catch { }
        }

        private void ComboMunicipios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMunicipiosFileDisplay();
        }

        private void UpdateMunicipiosFileDisplay()
        {
            int idx = ComboMunicipios.SelectedIndex;
            if (idx >= 0 && idx < _comboList.Count)
            {
                var m = _municipiosArray[idx];

                // APK File
                string fullApk = GetDynamicApkPath(m.Nome);
                if (!string.IsNullOrEmpty(fullApk) && System.IO.File.Exists(fullApk))
                {
                    TxtApkName.Text = System.IO.Path.GetFileName(fullApk);
                    TxtApkName.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                    IconApkCheck.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtApkName.Text = "Nenhum APK Encontrado";
                    TxtApkName.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7A7A8A"));
                    IconApkCheck.Visibility = Visibility.Collapsed;
                }

                // Pacote ZIP File
                string fullPacote = GetDynamicPacotePath(m.Nome);
                if (!string.IsNullOrEmpty(fullPacote) && System.IO.File.Exists(fullPacote))
                {
                    TxtPacoteName.Text = System.IO.Path.GetFileName(fullPacote);
                    TxtPacoteName.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                    IconPacoteCheck.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtPacoteName.Text = "Não definido";
                    TxtPacoteName.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7A7A8A"));
                    IconPacoteCheck.Visibility = Visibility.Collapsed;
                }

                // Wallpaper File
                string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                string wpRaw = m.WallpaperPath;
                string fullWp = "";
                if (!string.IsNullOrEmpty(wpRaw))
                {
                    fullWp = System.IO.Path.IsPathRooted(wpRaw) ? wpRaw : System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, wpRaw));
                }

                if (!string.IsNullOrEmpty(fullWp) && System.IO.File.Exists(fullWp))
                {
                    TxtWallpaperName.Text = System.IO.Path.GetFileName(fullWp);
                    TxtWallpaperName.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                    IconWpCheck.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtWallpaperName.Text = "Padrão";
                    TxtWallpaperName.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7A7A8A"));
                    IconWpCheck.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnGerenciarMunicipios_Click(object sender, RoutedEventArgs e)
        {
            MunicipiosWindow win = new MunicipiosWindow();
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
            // Após fechar o popup, recarrega a lista
            LoadMunicipios();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Anti-overlap: não dispara nova consulta enquanto a anterior ainda está rodando
            if (_isRefreshing) return;
            _isRefreshing = true;

            // Executa o ADB em background para não travar a UI
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    int activeCount = Engine_RefreshDevices();
                    int currentListCount = Engine_GetDeviceCount();

                    var newDevices = new System.Collections.Generic.List<DeviceModel>();
                    for (int i = 0; i < currentListCount; i++)
                    {
                        AdbDevice dev = new AdbDevice();
                        Engine_GetDevice(i, ref dev);

                        if (dev.authorized == 1 || dev.authorized == 0)
                        {
                            string serial = dev.serial;
                            string model = dev.authorized == 1 ? dev.model : "Pendente (Verifique a Tela)";
                            string mac = dev.authorized == 1 ? dev.mac : "N/A";
                            string androidVer = (dev.authorized == 1 && !string.IsNullOrEmpty(dev.android_version)) ? $"Android {dev.android_version}" : "---";

                            newDevices.Add(new DeviceModel {
                                Serial = serial,
                                Model = model,
                                MAC = mac,
                                Battery = dev.battery,
                                AndroidVersion = androidVer,
                                Status = dev.authorized == 1 ? "Pronto" : "Pendente",
                                StatusColor = dev.authorized == 1 ? "#8570E6" : "#FFD700"
                            });

                            // Registra no banco apenas se for novo e autorizado
                            if (dev.authorized == 1)
                                InnovPlayUI.Core.SQLiteDatabaseManager.RegistrarSeNovo(serial, dev.manufacturer, model, mac);
                        }
                    }


                    // Retorna para a UI thread para aplicar diff inteligente
                    Dispatcher.BeginInvoke(() =>
                    {
                        // Diff inteligente: remove apenas os que saíram
                        var currentSerials = new System.Collections.Generic.HashSet<string>();
                        foreach (var d in newDevices) currentSerials.Add(d.Serial);

                        for (int i = _devicesList.Count - 1; i >= 0; i--)
                        {
                            if (!currentSerials.Contains(_devicesList[i].Serial))
                                _devicesList.RemoveAt(i);
                        }

                        // Adiciona apenas os que são novos
                        foreach (var nd in newDevices)
                        {
                            if (!_knownSerials.Contains(nd.Serial))
                            {
                                _devicesList.Add(nd);
                                _knownSerials.Add(nd.Serial);

                                if (nd.Status == "Pronto" && ChkAutoInstall.IsChecked == true)
                                {
                                    RunAutoInstallForDevice(nd);
                                }
                            }
                            else
                            {
                                // Atualiza status de existentes
                                foreach (var existing in _devicesList)
                                {
                                    if (existing.Serial == nd.Serial)
                                    {
                                        if (existing.Status == "Pendente" && nd.Status == "Pronto" && ChkAutoInstall.IsChecked == true)
                                        {
                                            RunAutoInstallForDevice(nd);
                                        }
                                        existing.Model = nd.Model;
                                        existing.Battery = nd.Battery;
                                        existing.AndroidVersion = nd.AndroidVersion;
                                        if (existing.Status == "Pronto" || existing.Status == "Pendente" || string.IsNullOrEmpty(existing.Status))
                                        {
                                            existing.Status = nd.Status;
                                            existing.StatusColor = nd.StatusColor;
                                        }
                                        break;
                                    }
                                }
                            }

                            // Atualiza a porcentagem de bateria no GlobalProgressPanel se a label existir
                            if (_deviceProgressSerialLabels.ContainsKey(nd.Serial))
                            {
                                string battInfo = string.IsNullOrEmpty(nd.Battery) || nd.Battery == "---" ? "" : $" 🔋 {nd.Battery}";
                                _deviceProgressSerialLabels[nd.Serial].Text = $"[{nd.Serial}]{battInfo}";
                            }
                        }

                        // Limpa seriais que não estão mais presentes e remove as abas de log
                        var disconnectedSerials = new System.Collections.Generic.List<string>();
                        foreach (var s in _knownSerials)
                        {
                            if (!currentSerials.Contains(s))
                                disconnectedSerials.Add(s);
                        }
                        
                        // Controle de remoção atrasada (1 minuto) para abas
                        foreach (var s in disconnectedSerials)
                        {
                            if (!_disconnectTimes.ContainsKey(s))
                                _disconnectTimes[s] = DateTime.Now;
                        }

                        // Remove seriais reconectados do dicionário de desconexão
                        foreach (var s in currentSerials)
                        {
                            if (_disconnectTimes.ContainsKey(s))
                                _disconnectTimes.Remove(s);
                        }

                        var expiredSerials = new System.Collections.Generic.List<string>();
                        foreach (var kvp in _disconnectTimes)
                        {
                            if ((DateTime.Now - kvp.Value).TotalSeconds >= 60)
                            {
                                expiredSerials.Add(kvp.Key);
                            }
                        }

                        foreach (var s in expiredSerials)
                        {
                            if (_deviceLogBoxes.ContainsKey(s))
                            {
                                var tb = _deviceLogBoxes[s];
                                if (tb.Parent is TabItem tab)
                                {
                                    LogTabs.Items.Remove(tab);
                                }
                                _deviceLogBoxes.Remove(s);
                                _deviceLogCounts.Remove(s);
                            }
                            
                            if (_deviceProgressContainers.ContainsKey(s))
                            {
                                GlobalProgressPanel.Children.Remove(_deviceProgressContainers[s]);
                                _deviceProgressContainers.Remove(s);
                                _deviceProgressBars.Remove(s);
                                _deviceProgressLabels.Remove(s);
                                _deviceProgressSerialLabels.Remove(s);
                            }
                            
                            _disconnectTimes.Remove(s);
                        }

                        _knownSerials.RemoveWhere(s => !currentSerials.Contains(s));

                        if (activeCount > 0)
                            LblStatus.Text = $"Status: {activeCount} aparelho(s) conectado(s).";
                        else
                            LblStatus.Text = "Status: Nenhum aparelho conectado.";

                        _isRefreshing = false;
                    });
                }
                catch
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        LblStatus.Text = "Status: Erro de comunicação com a DLL do motor.";
                        _isRefreshing = false;
                    });
                }
            });
        }

        private void GetOrCreateDeviceProgress(string serial, out ProgressBar pBar, out TextBlock pLabel)
        {
            if (_deviceProgressBars.ContainsKey(serial))
            {
                pBar = _deviceProgressBars[serial];
                pLabel = _deviceProgressLabels[serial];
                return;
            }

            Grid container = new Grid();
            container.Margin = new Thickness(0, 5, 0, 5);
            container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(120) });
            container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });

            TextBlock lblSerial = new TextBlock();
            
            // Tenta achar o device na lista pra já colocar a bateria inicial
            string batteryText = "";
            foreach (var d in _devicesList) {
                if (d.Serial == serial) {
                    batteryText = $" 🔋 {d.Battery}";
                    break;
                }
            }
            lblSerial.Text = $"[{serial}]{batteryText}";
            lblSerial.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D2BBFF"));
            lblSerial.FontWeight = FontWeights.Bold;
            lblSerial.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(lblSerial, 0);

            ProgressBar newBar = new ProgressBar();
            newBar.Height = 8;
            newBar.Margin = new Thickness(10, 0, 10, 0);
            newBar.Minimum = 0;
            newBar.Maximum = 100;
            newBar.Value = 0;
            newBar.Style = (Style)FindResource("RoundedProgressBar");
            Grid.SetColumn(newBar, 1);

            TextBlock lblPct = new TextBlock();
            lblPct.Text = "0%";
            lblPct.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EDEA3"));
            lblPct.FontWeight = FontWeights.Bold;
            lblPct.VerticalAlignment = VerticalAlignment.Center;
            lblPct.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(lblPct, 2);

            container.Children.Add(lblSerial);
            container.Children.Add(newBar);
            container.Children.Add(lblPct);

            GlobalProgressPanel.Children.Add(container);

            _deviceProgressBars[serial] = newBar;
            _deviceProgressLabels[serial] = lblPct;
            _deviceProgressSerialLabels[serial] = lblSerial;
            _deviceProgressContainers[serial] = container;

            pBar = newBar;
            pLabel = lblPct;
        }

        private void UpdateProgress(string msg, string? serial)
        {
            double targetValue = -1;
            if (msg.Contains("Iniciando processo") || msg.Contains("[F5] Iniciando") || msg.Contains("INICIANDO ROTINA TAB-MULTI") || msg.Contains("[M7] INICIANDO")) { targetValue = 5; }
            else if (msg.Contains("[F1 - Etapa 1]") || msg.Contains("Instalando e ativando tunel de internet")) { targetValue = 10; }
            else if (msg.Contains("[F1 - Etapa 1.5]")) { targetValue = 20; }
            else if (msg.Contains("[F1 - Etapa 1.6]")) { targetValue = 30; }
            else if (msg.Contains("[F1 - Etapa 2]") || msg.Contains("Lendo a tela e inserindo credenciais")) { targetValue = 45; }
            else if (msg.Contains("[F1 - Etapa 3]") || msg.Contains("Formatando [") || msg.Contains("Criando pasta 'InnovAva'")) { targetValue = 55; }
            else if (msg.Contains("[F1 - Etapa 4]") || msg.Contains("Enviando APK") || msg.Contains("Transmitindo e instalando APK")) { targetValue = 65; }
            else if (msg.Contains("[F1 - Etapa 5]") || msg.Contains("Executando o aplicativo instalado") || msg.Contains("Procurando a opcao 'Importar ZIP' no menu")) { targetValue = 75; }
            else if (msg.Contains("[F1 - Etapa 6]") || msg.Contains("Sistema chegou na tela de selecao de arquivo")) { targetValue = 85; }
            else if (msg.Contains("Módulo F3") || msg.Contains("[F1 - Etapa 7]") || msg.Contains("Desligando tunelamento Gnirehtet")) { targetValue = 95; }
            else if (msg.Contains("[F3 - Etapa 1]")) { targetValue = 20; }
            else if (msg.Contains("[F3 - Etapa 2]")) { targetValue = 60; }
            else if (msg.Contains("[F5] Iniciando")) { targetValue = 5; }
            else if (msg.Contains("[F5 - Etapa 1]")) { targetValue = 25; }
            else if (msg.Contains("[F5 - Etapa 2]")) { targetValue = 50; }
            else if (msg.Contains("[F5 - Etapa 3]")) { targetValue = 75; }
            else if (msg.Contains("[F5 - Etapa 4]")) { targetValue = 90; }
            else if (msg.Contains("CONCLUIDO COM SUCESSO")) { targetValue = 100; }

            if (targetValue >= 0)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation(targetValue, TimeSpan.FromMilliseconds(500));
                animation.EasingFunction = new System.Windows.Media.Animation.CubicEase() { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
                
                if (!string.IsNullOrEmpty(serial))
                {
                    GetOrCreateDeviceProgress(serial, out ProgressBar pBar, out TextBlock pLabel);
                    pBar.BeginAnimation(ProgressBar.ValueProperty, animation);
                    pLabel.Text = $"{targetValue}%";

                    // Atualiza a label com a bateria atual do aparelho
                    foreach (var d in _devicesList) {
                        if (d.Serial == serial) {
                            if (_deviceProgressSerialLabels.ContainsKey(serial)) {
                                _deviceProgressSerialLabels[serial].Text = $"[{serial}] 🔋 {d.Battery}";
                            }
                            break;
                        }
                    }
                }

                ExecutionProgress.BeginAnimation(ProgressBar.ValueProperty, animation);
                LblProgress.Text = $"{targetValue}%";
            }
        }

        private TextBox GetOrCreateDeviceTab(string serial)
        {
            if (_deviceLogBoxes.ContainsKey(serial))
                return _deviceLogBoxes[serial];

            TabItem newTab = new TabItem();
            newTab.Header = $"[{serial}]";

            TextBox newBox = new TextBox();
            newBox.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0A0A0F"));
            newBox.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF")); // Branco (ocioso)
            newBox.BorderThickness = new Thickness(0);
            newBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            newBox.FontSize = 16;
            newBox.TextWrapping = TextWrapping.Wrap;
            newBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            newBox.IsReadOnly = true;
            newBox.Padding = new Thickness(5);

            newTab.Content = newBox;
            LogTabs.Items.Add(newTab);

            _deviceLogBoxes[serial] = newBox;
            return newBox;
        }

        private void UpdateDeviceStatus(string serial, string status, string colorHex)
        {
            if (string.IsNullOrEmpty(serial)) return;
            foreach (var dev in _devicesList)
            {
                if (dev.Serial == serial)
                {
                    dev.Status = status;
                    dev.StatusColor = colorHex;
                    break;
                }
            }
        }

        private void LogMessage(string msg)
        {
            string? targetSerial = null;
            TextBox targetBox = TxtLogGlobal;
            string displayMsg = msg;

            // Extrai o serial (ex: [12345ABC] ou [080274112345])
            var match = System.Text.RegularExpressions.Regex.Match(msg, @"^\[([a-zA-Z0-9]+)\]\s*(.*)");
            if (match.Success)
            {
                targetSerial = match.Groups[1].Value;
                // Exibe só a mensagem sem o prefixo repetitivo, pois a aba já é do aparelho
                displayMsg = match.Groups[2].Value; 
                targetBox = GetOrCreateDeviceTab(targetSerial);
            }

            if (targetBox == null) targetBox = TxtLogGlobal;

            if (targetBox != null && targetBox != TxtLogGlobal)
            {
                if (msg.Contains("Iniciando process") || msg.Contains("[F5] Iniciando") || msg.Contains("[F1 - ") || msg.Contains("[F2 - ") || msg.Contains("[F5 - Etapa") || msg.Contains("INICIANDO ROTINA TAB-MULTI") || msg.Contains("[M7] INICIANDO") || msg.Contains("Iniciando"))
                {
                    targetBox.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF")); // Branca
                    if (!string.IsNullOrEmpty(targetSerial)) UpdateDeviceStatus(targetSerial, "Executando", "#FFD700");
                }
                else if (msg.Contains("CONCLUIDO") || msg.Contains("sucesso") || msg.Contains("finalizada"))
                {
                    targetBox.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EDEA3")); // Verde
                    if (!string.IsNullOrEmpty(targetSerial)) UpdateDeviceStatus(targetSerial, "Concluído", "#4EDEA3");
                }
                else if (msg.Contains("[ERRO]") || msg.Contains("[ERROR]") || msg.Contains("interrompida") || msg.Contains("cancelada"))
                {
                    targetBox.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF4D4D")); // Vermelho
                    if (!string.IsNullOrEmpty(targetSerial)) UpdateDeviceStatus(targetSerial, "Erro", "#FF4D4D");
                }
            }

            string key = targetSerial ?? "GLOBAL";
            if (!_deviceLogCounts.ContainsKey(key))
                _deviceLogCounts[key] = 0;

            if (targetBox == null) return;

            if (_deviceLogCounts[key] >= LOG_MAX_LINES)
            {
                string text = targetBox.Text ?? string.Empty;
                int cutIdx = 0;
                int removed = 0;
                while (removed < 100 && cutIdx < text.Length)
                {
                    int nl = text.IndexOf('\n', cutIdx);
                    if (nl < 0) break;
                    cutIdx = nl + 1;
                    removed++;
                }
                targetBox.Text = text.Substring(cutIdx);
                _deviceLogCounts[key] -= removed;
            }

            targetBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {displayMsg}\n");
            targetBox.ScrollToEnd();
            _deviceLogCounts[key]++;

            try
            {
                if (targetSerial != null)
                {
                    string dir = System.IO.Path.Combine(InnovPlayUI.Core.PathHelper.GetRootDirectory(), "registros", DateTime.Now.ToString("yyyy-MM-dd"));
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                    string logFile = System.IO.Path.Combine(dir, $"{targetSerial}_execucao.log");
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {displayMsg}\n");
                }
            }
            catch { }
            
            UpdateProgress(msg, targetSerial);
        }

        private System.Collections.Generic.List<DeviceModel> GetTargetDevices()
        {
            var targets = new System.Collections.Generic.List<DeviceModel>();
            foreach (var dev in _devicesList)
            {
                if (dev.Status != "Pendente" && !string.IsNullOrEmpty(dev.Serial))
                {
                    targets.Add(dev);
                }
            }
            return targets;
        }


        public void BtnF2_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Limpeza e Instalação (F2) em {targetDevices.Count} aparelho(s)...";

                string apkPath = "";
                string pacotePath = "";
                string packageName = "";
                string login = "";
                string senha = "";
                string municipio = "";
                
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                    {
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));
                    }

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                    {
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));
                    }

                    login = m.Login;
                    senha = m.Senha;
                    municipio = m.Nome;
                    packageName = "br.com.yantec.innovava"; 
                }

                if (string.IsNullOrEmpty(apkPath))
                {
                    MessageBox.Show("Nenhum APK configurado para este município!", "Erro de Instalação", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalPackageName = packageName;
                    string finalLogin = login;
                    string finalSenha = senha;
                    string finalMunicipio = municipio;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        int result = Engine_ExecuteF2_Reinstall(finalSerial, finalApkPath, finalPacotePath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        
                        if (result != 0)
                        {
                             Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] [ERRO] Reinstalação interrompida ou falhou."));
                        }
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android autorizado conectado via cabo USB!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF3_UpdatePackage(string serial, string apk_path, string pacote_path, string package_name, LogCallback cb);

        public void BtnF3_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Atualização de Pacote (F3) em {targetDevices.Count} aparelho(s)...";

                string apkPath = "";
                string pacotePath = "";
                string packageName = "br.com.yantec.innovava";

                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                    {
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));
                    }

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                    {
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));
                    }
                }

                if (string.IsNullOrEmpty(pacotePath))
                {
                    MessageBox.Show("Nenhum Pacote configurado para este município!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalPackageName = packageName;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        int result = Engine_ExecuteF3_UpdatePackage(finalSerial, finalApkPath, finalPacotePath, finalPackageName, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android conectado!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BtnF4_Click(object sender, RoutedEventArgs e)
        {
            currentTerminalMode = 4;
            TxtInput.Visibility = Visibility.Visible;
            LogMessage("\n=== [F4] MODO TERMINAL INTERATIVO (KIOSK) ===");
            LogMessage("Opções disponíveis para aparelhos ADB:");
            LogMessage("1 📷 - Suspender Câmera");
            LogMessage("2 ⚙️ - Suspender Configurações");
            LogMessage("3 🔓 - Liberar Câmera");
            LogMessage("4 🔓 - Liberar Configurações");
            LogMessage("5 🛑 - Suspender Ambos (Kiosk Máximo)");
            LogMessage("6 🟢 - Liberar Ambos");
            LogMessage("7 🖼️ - Definir Wallpaper (do Município selecionado)");
            LogMessage("8 🌐 - Injetar Internet via Gnirehtet (LIGAR)");
            LogMessage("9 🔌 - Desligar Gnirehtet (PARAR)");
            LogMessage("10 🧹 - Remover Barra de Pesquisa Google (Visual/UIAutomator)");
            LogMessage("👉 Digite os números separados por vírgula (Ex: 1,8,10) e pressione ENTER:\n");
            TxtInput.Focus();
        }

        private void TxtInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string cmd = TxtInput.Text.Trim();
                TxtInput.Clear();
                
                if (string.IsNullOrEmpty(cmd)) return;

                LogMessage($"[Você] Comando digitado: {cmd}");
                
                if (currentTerminalMode == 4)
                    ExecuteF4Command(cmd);
                else
                    LogMessage("[Erro] Nenhum modo de terminal ativo.");
            }
        }
        
        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF4_Ferramenta(string serial, int acao, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF4_Wallpaper(string serial, string wp_path, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF5_Shutdown(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF6_Sincronizacao(string serial, LogCallback cb);

        [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ExecuteF8_ActiveConnection(string serial, LogCallback cb);

        private void ExecuteF4Command(string cmd)
        {
            string[] parts = cmd.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            System.Collections.Generic.List<int> acoes = new System.Collections.Generic.List<int>();

            foreach (string part in parts)
            {
                if (!int.TryParse(part.Trim(), out int acao) || acao < 1 || acao > 10)
                {
                    LogMessage($"[Erro] Comando inválido: '{part}'. Digite números de 1 a 10 (ex: 1, 8, 10).");
                    return;
                }
                acoes.Add(acao);
            }

            var targetDevices = GetTargetDevices();
            if (targetDevices.Count == 0)
            {
                LogMessage("[Erro] Nenhum aparelho conectado para receber o comando.");
                return;
            }

            string wpPath = "";
            if (acoes.Contains(7))
            {
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _municipiosArray.Length)
                {
                    wpPath = _municipiosArray[idx].WallpaperPath;
                    if (string.IsNullOrEmpty(wpPath))
                    {
                        LogMessage("[Erro] O município selecionado não possui um Wallpaper configurado.");
                        return;
                    }
                    
                    // Converte para caminho absoluto
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    if (!System.IO.Path.IsPathRooted(wpPath))
                    {
                        wpPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, wpPath));
                    }
                    
                    if (!System.IO.File.Exists(wpPath))
                    {
                        LogMessage($"[Erro] Arquivo de Wallpaper não encontrado fisicamente em: {wpPath}");
                        return;
                    }
                }
                else
                {
                    LogMessage("[Erro] Selecione um município no menu superior antes de definir o Wallpaper.");
                    return;
                }
            }

            foreach (var device in targetDevices)
            {
                string finalSerial = device.Serial;
                
                LogCallback callback = new LogCallback((msg) =>
                {
                    Dispatcher.Invoke(() => LogMessage($"{msg}"));
                });

                System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (int a in acoes)
                    {
                        if (a == 7)
                        {
                            Engine_ExecuteF4_Wallpaper(finalSerial, wpPath, callback);
                        }
                        else
                        {
                            Engine_ExecuteF4_Ferramenta(finalSerial, a, callback);
                        }
                        // Dá um breve tempo entre um comando e outro para que logs e processos ADB não se encavalem
                        System.Threading.Thread.Sleep(800);
                    }
                    GC.KeepAlive(callback);
                });
            }
        }

        public void BtnF5_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Desligamento Total (F5) em {targetDevices.Count} aparelho(s)...";

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        Engine_ExecuteF5_Shutdown(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android conectado para desligamento!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BtnF6_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Sincronização Avançada (F6) em {targetDevices.Count} aparelho(s)...";

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        Engine_ExecuteF6_Sincronizacao(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android conectado para sincronização!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BtnF8_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Ativando Conexão Ativa (Gnirehtet F8) em {targetDevices.Count} aparelho(s)...";

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        Engine_ExecuteF8_ActiveConnection(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android conectado para ativar a conexão!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BtnF7_Click(object sender, RoutedEventArgs e)
        {
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Tab-MULTI (F7) em {targetDevices.Count} aparelho(s)...";
                
                string apkPath = "";
                string pacotePath = "";
                string login = "";
                string senha = "";
                string municipio = "";
                
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));

                    login = m.Login;
                    senha = m.Senha;
                    municipio = m.Nome;
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalLogin = login;
                    string finalSenha = senha;
                    string finalMunicipio = municipio;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        Engine_ExecuteF7_TabMulti(finalSerial, finalApkPath, finalPacotePath, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android conectado para executar Tab-MULTI!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("[PARADO] Automação interrompida pelo usuário.");
            LblStatus.Text = "Status: Parado.";
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLogGlobal.Clear();
            _deviceLogCounts.Clear();
            foreach (var kvp in _deviceLogBoxes)
            {
                kvp.Value.Clear();
            }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("[*] Forçando atualização da lista de dispositivos...");
            Timer_Tick(null, EventArgs.Empty);
        }

        public void BtnM7_F2_Click(object sender, RoutedEventArgs e)
        {
            Engine_ResetCancel();
            _isPaused = false;
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Atualizacao (M7) em {targetDevices.Count} aparelho(s)...";

                string apkPath = "";
                string pacotePath = "";
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    LogCallback callback = new LogCallback((msg) =>
                    {
                        Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                    });

                    LogMessage($"[{finalSerial}] Iniciando Atualizacao M7...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF2_TabMulti_Atualizar(finalSerial, finalApkPath, finalPacotePath, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
        }

        public void BtnM7_F3_Click(object sender, RoutedEventArgs e)
        {
            Engine_ResetCancel();
            _isPaused = false;
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Terminal M7 em {targetDevices.Count} aparelho(s)...";

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    LogCallback callback = new LogCallback((msg) =>
                    {
                        Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                    });

                    LogMessage($"[{finalSerial}] Iniciando Terminal M7...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF3_TabMulti_Terminal(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
        }

        public void BtnM7_F4_Click(object sender, RoutedEventArgs e)
        {
            Engine_ResetCancel();
            _isPaused = false;
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando Desligamento M7 em {targetDevices.Count} aparelho(s)...";

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    LogCallback callback = new LogCallback((msg) =>
                    {
                        Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                    });

                    LogMessage($"[{finalSerial}] Iniciando Desligamento M7...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Engine_ExecuteF4_TabMulti_Desligar(finalSerial, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
        }

        public void BtnM7_F5_Click(object sender, RoutedEventArgs e)
        {
            Engine_ResetCancel();
            _isPaused = false;
            var targetDevices = GetTargetDevices();

            if (targetDevices.Count > 0)
            {
                LblStatus.Text = $"Status: Executando F5 Tab-MULTI em {targetDevices.Count} aparelho(s)...";

                string apkPath = "";
                string pacotePath = "";
                string login = "";
                string senha = "";
                string municipio = "";

                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = InnovPlayUI.Core.PathHelper.GetRootDirectory();
                    
                    apkPath = GetDynamicApkPath(m.Nome);
                    if (!string.IsNullOrEmpty(apkPath) && !System.IO.Path.IsPathRooted(apkPath))
                        apkPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, apkPath));

                    pacotePath = GetDynamicPacotePath(m.Nome);
                    if (!string.IsNullOrEmpty(pacotePath) && !System.IO.Path.IsPathRooted(pacotePath))
                        pacotePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, pacotePath));

                    login = m.Login;
                    senha = m.Senha;
                    municipio = m.Nome;
                }

                foreach (var device in targetDevices)
                {
                    string finalSerial = device.Serial;
                    string finalApkPath = apkPath;
                    string finalPacotePath = pacotePath;
                    string finalLogin = login;
                    string finalSenha = senha;
                    string finalMunicipio = municipio;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        Engine_ExecuteF5_TabMulti(finalSerial, finalApkPath, finalPacotePath, finalLogin, finalSenha, finalMunicipio, callback);
                        GC.KeepAlive(callback);
                    });
                }
            }
            else
            {
                MessageBox.Show("Nenhum aparelho Android conectado para executar F5 Tab-MULTI!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCategoryMultilaser_Click(object sender, RoutedEventArgs e)
        {
            GridPositivoActions.Visibility = Visibility.Collapsed;
            GridMultilaserActions.Visibility = Visibility.Visible;
            if (GridNokiaActions != null) GridNokiaActions.Visibility = Visibility.Collapsed;

            BtnCategoryMultilaser.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#513BA6"));
            BtnCategoryMultilaser.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
            BtnCategoryMultilaser.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8570E6"));

            BtnCategoryPositivo.Background = System.Windows.Media.Brushes.Transparent;
            BtnCategoryPositivo.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
            BtnCategoryPositivo.BorderBrush = System.Windows.Media.Brushes.Transparent;

            if (BtnCategoryNokia != null)
            {
                BtnCategoryNokia.Background = System.Windows.Media.Brushes.Transparent;
                BtnCategoryNokia.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
                BtnCategoryNokia.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }

            ComboAutoAction.Items.Clear();
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F1] Instalacao + Prep (Tab-MULTI)" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F2] Atualizar Pacote" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F4] Desligar Aparelhos" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F5] Login + Sinc 30s + Shutdown" });
            ComboAutoAction.SelectedIndex = 0;
        }

        private void BtnCategoryPositivo_Click(object sender, RoutedEventArgs e)
        {
            GridPositivoActions.Visibility = Visibility.Visible;
            GridMultilaserActions.Visibility = Visibility.Collapsed;
            if (GridNokiaActions != null) GridNokiaActions.Visibility = Visibility.Collapsed;

            BtnCategoryPositivo.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#513BA6"));
            BtnCategoryPositivo.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
            BtnCategoryPositivo.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8570E6"));

            BtnCategoryMultilaser.Background = System.Windows.Media.Brushes.Transparent;
            BtnCategoryMultilaser.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
            BtnCategoryMultilaser.BorderBrush = System.Windows.Media.Brushes.Transparent;

            if (BtnCategoryNokia != null)
            {
                BtnCategoryNokia.Background = System.Windows.Media.Brushes.Transparent;
                BtnCategoryNokia.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
                BtnCategoryNokia.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }

            ComboAutoAction.Items.Clear();
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F1] Sincronizacao + Instalacao" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F2] Limpeza e Instalacao" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F3] Atualizar Pacote" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F5] Desligamento Remoto" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F6] Sincronizacao Avancada" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F8] Conexao Ativa (Gnirehtet)" });
            ComboAutoAction.SelectedIndex = 0;
        }

        private void BtnCategoryNokia_Click(object sender, RoutedEventArgs e)
        {
            GridPositivoActions.Visibility = Visibility.Collapsed;
            GridMultilaserActions.Visibility = Visibility.Collapsed;
            if (GridNokiaActions != null) GridNokiaActions.Visibility = Visibility.Visible;

            if (BtnCategoryNokia != null)
            {
                BtnCategoryNokia.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#513BA6"));
                BtnCategoryNokia.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                BtnCategoryNokia.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8570E6"));
            }

            BtnCategoryPositivo.Background = System.Windows.Media.Brushes.Transparent;
            BtnCategoryPositivo.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
            BtnCategoryPositivo.BorderBrush = System.Windows.Media.Brushes.Transparent;

            BtnCategoryMultilaser.Background = System.Windows.Media.Brushes.Transparent;
            BtnCategoryMultilaser.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3A3C2"));
            BtnCategoryMultilaser.BorderBrush = System.Windows.Media.Brushes.Transparent;

            ComboAutoAction.Items.Clear();
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F1] Sincronizacao + Instalacao (Nokia T20)" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F2] Reinstalação - Preserva Apps (Nokia T20)" });
            ComboAutoAction.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "[F3] Atualizar Pacote (Nokia T20)" });
            ComboAutoAction.SelectedIndex = 0;
        }

        public void HandleEscPress()
        {
            if (DateTime.Now.Subtract(_lastEscTime).TotalSeconds < 1.0)
            {
                Engine_CancelAll();
                LogMessage("[ALERTA] PARADA GERAL ENVIADA (Duplo ESC)");
                _isPaused = true;
            }
            else
            {
                LogMessage("[AVISO] Pressione ESC novamente para PARADA GERAL");
            }
            _lastEscTime = DateTime.Now;
        }
    
        private string GetDynamicApkPath(string municipio)
        {
            return InnovPlayUI.Core.PathHelper.GetDynamicApkPath(municipio);
        }

        private string GetDynamicPacotePath(string municipio)
        {
            return InnovPlayUI.Core.PathHelper.GetDynamicPacotePath(municipio);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space && !TxtInput.IsFocused && !TxtLogGlobal.IsFocused)
            {
                e.Handled = true;
                foreach (var kvp in _deviceProgressBars)
                {
                    string serial = kvp.Key;
                    System.Windows.Controls.ProgressBar pBar = kvp.Value;
                    if (pBar.Value >= 100.0)
                    {
                        LogMessage($"[{serial}] [SPACE] Iniciando desligamento manual para dispositivo concluido...");
                        System.Threading.Tasks.Task.Run(() => {
                            StringBuilder sb = new StringBuilder(1024);
                            Engine_RunAdbShell(serial, "am force-stop com.genymobile.gnirehtet", sb, 1024);
                            Engine_RunAdbShell(serial, "svc wifi disable", sb, 1024);
                            Engine_RunAdbShell(serial, "svc bluetooth disable", sb, 1024);
                            Engine_RunAdbShell(serial, "settings put secure location_mode 0", sb, 1024);
                            Engine_RunAdbShell(serial, "reboot -p", sb, 1024);
                            
                            Application.Current.Dispatcher.Invoke(() => {
                                LogMessage($"[{serial}] Aparelho desligado com sucesso via atalho SPACE.");
                                pBar.Value = 0; // Reseta para no mandar de novo se apertar espao novamente
                            });
                        });
                    }
                }
            }
        }
    }
}
