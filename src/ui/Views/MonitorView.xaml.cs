using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BotRpaUI.Views
{
    public class DeviceModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _serial = string.Empty;
        public string Serial { get => _serial; set { _serial = value; OnPropertyChanged(nameof(Serial)); } }

        private string _model = string.Empty;
        public string Model { get => _model; set { _model = value; OnPropertyChanged(nameof(Model)); OnPropertyChanged(nameof(Marca)); OnPropertyChanged(nameof(ModeloExibicao)); } }

        /// <summary>
        /// Modelo com nome comercial legivel. O ADB devolve codigos internos
        /// ("T307G", "m7_MULTI", "Nokia T20"), traduzidos aqui para o nome
        /// do produto. Se nao houver correspondencia, mantem o valor original.
        /// </summary>
        public string ModeloExibicao
        {
            get
            {
                string mdl = (_model ?? "").Trim();
                if (string.IsNullOrEmpty(mdl) || mdl == "---" || mdl == "Desconhecido") return "---";
                if (mdl.StartsWith("Pendente")) return "Pendente";

                if (mdl.IndexOf("Vision", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("T307", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("T0307", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Tab Vision 7";

                if (mdl.IndexOf("TabMulti", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("ML01", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("NB36", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "TabMulti M7";

                if (mdl.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mdl.IndexOf("TA-", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Nokia T20";

                return mdl;
            }
        }

        private string _manufacturer = string.Empty;
        public string Manufacturer { get => _manufacturer; set { _manufacturer = value; OnPropertyChanged(nameof(Manufacturer)); OnPropertyChanged(nameof(Marca)); } }

        public string Marca
        {
            get
            {
                string mfr = (_manufacturer ?? "").Trim();
                string mdl = (_model ?? "").Trim();

                if (!string.IsNullOrEmpty(mfr) && mfr != "---" && mfr != "Desconhecido")
                {
                    if (mfr.IndexOf("Positivo", StringComparison.OrdinalIgnoreCase) >= 0) return "Positivo";
                    if (mfr.IndexOf("Multilaser", StringComparison.OrdinalIgnoreCase) >= 0) return "Multilaser";
                    if (mfr.IndexOf("HMD", StringComparison.OrdinalIgnoreCase) >= 0 || mfr.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0) return "Nokia";
                    return mfr;
                }

                if (!string.IsNullOrEmpty(mdl) && mdl != "---" && mdl != "Desconhecido" && !mdl.StartsWith("Pendente"))
                {
                    if (mdl.IndexOf("Vision", StringComparison.OrdinalIgnoreCase) >= 0 || mdl.IndexOf("T307", StringComparison.OrdinalIgnoreCase) >= 0) return "Positivo";
                    if (mdl.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0 || mdl.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0) return "Multilaser";
                    if (mdl.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0 || mdl.IndexOf("TA-", StringComparison.OrdinalIgnoreCase) >= 0) return "Nokia";
                    return mdl;
                }

                return "Android";
            }
        }

        private string _androidVersion = string.Empty;
        public string AndroidVersion { get => _androidVersion; set { _androidVersion = value; OnPropertyChanged(nameof(AndroidVersion)); } }

        private string _mac = string.Empty;
        public string MAC { get => _mac; set { _mac = value; OnPropertyChanged(nameof(MAC)); OnPropertyChanged(nameof(MACExibicao)); } }

        /// <summary>
        /// MAC pronto para exibicao: vazio/placeholder viram "Aguardando" e o
        /// endereco valido aparece em maiusculas (padrao de inventario).
        /// </summary>
        public string MACExibicao
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_mac)) return "Aguardando";
                if (_mac == "N/A" || _mac == "---" || _mac == "02:00:00:00:00:00") return "Aguardando";
                return _mac.ToUpperInvariant();
            }
        }

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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string Login;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
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



        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
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

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
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

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
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

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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

        /// <summary>
        /// Indica se existe alguma automacao "ao conectar" habilitada.
        /// Quando o painel de Gestao do Aparelho (F4) esta aberto, vale o card
        /// "Automacao F4"; caso contrario, vale o card geral de Automacao ao Conectar.
        /// </summary>
        private bool AutomacaoAoConectarAtiva
        {
            get
            {
                if (CardAutoF4 != null && CardAutoF4.Visibility == Visibility.Visible)
                    return ChkAutoF4 != null && ChkAutoF4.IsChecked == true;
                return ChkAutoInstall != null && ChkAutoInstall.IsChecked == true;
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
                string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                
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

            // Se o painel de Gestao do Aparelho (F4) estiver aberto, a automacao ao
            // conectar executa a subfuncao F4 selecionada no card "Automacao F4".
            if (CardAutoF4 != null && CardAutoF4.Visibility == Visibility.Visible)
            {
                RunAutoF4ForDevice(device);
                return;
            }

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

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF2_Reinstall(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string package_name, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string login, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string senha, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string municipio, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF7_TabMulti(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string login, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string senha, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string municipio, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_RunAdbShell(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string command, 
            StringBuilder out_log, 
            int max_len);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF2_TabMulti_Atualizar(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF3_TabMulti_Terminal(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF4_TabMulti_Desligar(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF5_TabMulti(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string login, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string senha, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string municipio, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_GetDeviceCount();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_CancelAll();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_SetPaused(int paused);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_ResetCancel();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_GetDevice(int index, ref AdbDevice out_device);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_RefreshDevices();

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
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

            LogMessage("Motor Bot_RPA inicializado. Aguardando conexão de aparelhos ADB...");
            UpdateCategoryVisibility();
            
            // Subscrição para reagir imediatamente quando o usuário altera chaves em Configurações
            BotRpaUI.Core.SettingsManager.OnSettingsChanged += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateCategoryVisibility();
                    for (int i = _devicesList.Count - 1; i >= 0; i--)
                    {
                        var d = _devicesList[i];
                        if (!BotRpaUI.Core.DeviceFilterHelper.IsDeviceModelAllowed(d.Serial, d.Model, ""))
                        {
                            _devicesList.RemoveAt(i);
                            _knownSerials.Remove(d.Serial);
                        }
                    }
                });
            };

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5); // aumentado de 2s para 5s
            _timer.Tick += Timer_Tick;
            _timer.Start();

            BotRpaUI.Core.SQLiteDatabaseManager.OnCloudPullCompleted += OnCloudPullCompleted_Monitor;
        }

        private void OnCloudPullCompleted_Monitor(int importados, int atualizados)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        LogMessage($"[INFO] Nuvem InnovStock: {importados} novo(s), {atualizados} atualizado(s) — lista local atualizada.");
                        if (!_isRefreshing)
                        {
                            Timer_Tick(null, EventArgs.Empty);
                        }
                    }
                    catch { }
                });
            }
            catch { }
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
                string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
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
                            if ((mac == "N/A" || mac == "---" || string.IsNullOrEmpty(mac)) && !string.IsNullOrEmpty(serial))
                            {
                                var existing = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorSerial(serial);
                                if (existing != null && !string.IsNullOrEmpty(existing.MAC) && existing.MAC != "N/A" && existing.MAC != "---")
                                {
                                    mac = existing.MAC;
                                }
                            }
                            string androidVer = (dev.authorized == 1 && !string.IsNullOrEmpty(dev.android_version)) ? $"Android {dev.android_version}" : "---";

                            // Registra no banco SQLite para manter permanentemente para o futuro
                            if (dev.authorized == 1)
                            {
                                BotRpaUI.Core.SQLiteDatabaseManager.RegistrarSeNovo(serial, dev.manufacturer, model, mac);

                                // Garante o MAC no cadastro: o motor C ja devolve o endereco no
                                // refresh (batched); se faltar, o servico extrai com o ciclo
                                // de Wi-Fi (liga -> extrai -> restaura) e grava no banco.
                                BotRpaUI.Core.MacCaptureService.GarantirMacRegistrado(serial, model, mac);

                                // A captura pode ter gravado um MAC novo: relê do banco para
                                // exibir na lista ja neste ciclo.
                                if (!BotRpaUI.Core.MacCaptureService.MacValido(mac))
                                {
                                    var gravado = BotRpaUI.Core.SQLiteDatabaseManager.BuscarPorSerial(serial);
                                    if (gravado != null && BotRpaUI.Core.MacCaptureService.MacValido(gravado.MAC))
                                    {
                                        mac = gravado.MAC;
                                    }
                                }
                            }

                            // Se o modelo deste aparelho estiver DESATIVADO em Configurações, oculta do monitor ADB
                            if (!BotRpaUI.Core.DeviceFilterHelper.IsDeviceModelAllowed(serial, model, dev.manufacturer))
                            {
                                continue;
                            }

                            newDevices.Add(new DeviceModel {
                                Serial = serial,
                                Model = model,
                                Manufacturer = dev.manufacturer,
                                MAC = mac,
                                Battery = dev.battery,
                                AndroidVersion = androidVer,
                                Status = dev.authorized == 1 ? "Pronto" : "Pendente",
                                StatusColor = dev.authorized == 1 ? "#8570E6" : "#FFD700"
                            });
                        }
                    }

                    // Se a opção de exibir dispositivos desconectados / histórico estiver ATIVA:
                    if (BotRpaUI.Core.SettingsManager.ShowDisconnectedDevices)
                    {
                        var connectedSerials = new System.Collections.Generic.HashSet<string>();
                        foreach (var d in newDevices) connectedSerials.Add(d.Serial);

                        var historico = BotRpaUI.Core.SQLiteDatabaseManager.LerTodos();
                        foreach (var h in historico)
                        {
                            if (!string.IsNullOrEmpty(h.Serial) && !connectedSerials.Contains(h.Serial))
                            {
                                if (!BotRpaUI.Core.DeviceFilterHelper.IsDeviceModelAllowed(h.Serial, h.Modelo, h.Fabricante))
                                {
                                    continue;
                                }

                                string modeloStr = string.IsNullOrEmpty(h.Modelo) || h.Modelo == "N/A" ? (string.IsNullOrEmpty(h.Fabricante) ? "Android Device" : h.Fabricante) : h.Modelo;
                                string macStr = string.IsNullOrEmpty(h.MAC) || h.MAC == "N/A" ? "---" : h.MAC;
                                string verStr = string.IsNullOrEmpty(h.Versao_Android) || h.Versao_Android == "N/A" ? "---" : h.Versao_Android;

                                newDevices.Add(new DeviceModel {
                                    Serial = h.Serial,
                                    Model = modeloStr,
                                    Manufacturer = h.Fabricante,
                                    MAC = macStr,
                                    Battery = "---",
                                    AndroidVersion = verStr,
                                    Status = "Desconectado (Histórico)",
                                    StatusColor = "#6A6A8E"
                                });
                            }
                        }
                    }

                    // Retorna para a UI thread para aplicar diff inteligente
                    Dispatcher.BeginInvoke(() =>
                    {
                        // Diff inteligente: remove apenas os que saíram ou cujo modelo foi desativado em Configurações
                        var currentSerials = new System.Collections.Generic.HashSet<string>();
                        foreach (var d in newDevices) currentSerials.Add(d.Serial);

                        for (int i = _devicesList.Count - 1; i >= 0; i--)
                        {
                            var currentItem = _devicesList[i];
                            if (!currentSerials.Contains(currentItem.Serial) || !BotRpaUI.Core.DeviceFilterHelper.IsDeviceModelAllowed(currentItem.Serial, currentItem.Model, ""))
                            {
                                _devicesList.RemoveAt(i);
                                _knownSerials.Remove(currentItem.Serial);
                            }
                        }

                        // Adiciona novos ou atualiza existentes
                        foreach (var nd in newDevices)
                        {
                            var existing = System.Linq.Enumerable.FirstOrDefault(_devicesList, d => d.Serial == nd.Serial);
                            if (existing == null)
                            {
                                _devicesList.Add(nd);
                                if (!_knownSerials.Contains(nd.Serial))
                                {
                                    _knownSerials.Add(nd.Serial);

                                    if (nd.Status == "Pronto" && AutomacaoAoConectarAtiva)
                                    {
                                        RunAutoInstallForDevice(nd);
                                    }
                                }
                            }
                            else
                            {
                                if (existing.Status == "Pendente" && nd.Status == "Pronto" && AutomacaoAoConectarAtiva)
                                {
                                    RunAutoInstallForDevice(nd);
                                }
                                existing.Model = nd.Model;
                                existing.Manufacturer = nd.Manufacturer;
                                existing.Battery = nd.Battery;
                                existing.AndroidVersion = nd.AndroidVersion;
                                if (!string.IsNullOrEmpty(nd.MAC) && nd.MAC != "N/A" && nd.MAC != "---" && nd.MAC != "02:00:00:00:00:00")
                                {
                                    existing.MAC = nd.MAC;
                                }
                                if (existing.Status == "Pronto" || existing.Status == "Pendente" || string.IsNullOrEmpty(existing.Status))
                                {
                                    existing.Status = nd.Status;
                                    existing.StatusColor = nd.StatusColor;
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
            if (msg.Contains("Iniciando processo") || msg.Contains("[F5] Iniciando") || msg.Contains("INICIANDO ROTINA TAB-MULTI") || msg.Contains("[M7] INICIANDO") || msg.Contains("INICIANDO FLUXO OBJETIVO F2")) { targetValue = 5; }
            else if (msg.Contains("[F1 - Etapa 1]") || msg.Contains("Instalando e ativando tunel de internet")) { targetValue = 10; }
            else if (msg.Contains("[F1 - Etapa 1.2]")) { targetValue = 15; }
            else if (msg.Contains("[F1 - Etapa 1.5]")) { targetValue = 20; }
            else if (msg.Contains("[F1 - Etapa 1.6]")) { targetValue = 30; }
            else if (msg.Contains("[F1 - Etapa 1.7]")) { targetValue = 35; }
            else if (msg.Contains("[F1 - Etapa 2]") || msg.Contains("Lendo a tela e inserindo credenciais")) { targetValue = 45; }
            else if (msg.Contains("[F1 - Etapa 2.1]")) { targetValue = 48; }
            else if (msg.Contains("[F1 - Etapa 2.5]")) { targetValue = 51; }
            else if (msg.Contains("[F1 - Etapa 3]") || msg.Contains("Formatando [") || msg.Contains("Criando pasta 'InnovAva'")) { targetValue = 55; }
            else if (msg.Contains("[F1 - Etapa 4]") || msg.Contains("Enviando APK") || msg.Contains("Transmitindo e instalando APK")) { targetValue = 65; }
            else if (msg.Contains("[F1 - Etapa 4.5]")) { targetValue = 70; }
            else if (msg.Contains("[F1 - Etapa 5]") || msg.Contains("Executando o aplicativo instalado") || msg.Contains("Procurando a opcao 'Importar ZIP' no menu")) { targetValue = 75; }
            else if (msg.Contains("[F1 - Etapa 6]") || msg.Contains("Sistema chegou na tela de selecao de arquivo")) { targetValue = 85; }
            else if (msg.Contains("[F2 - Etapa 7]")) { targetValue = 92; }
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

            // Extrai o serial (ex: [12345ABC] ou [080274112345]).
            // Palavras reservadas de sistema NUNCA viram aba (evita abas [PARADO]/[LIMPEZA]).
            var match = System.Text.RegularExpressions.Regex.Match(msg, @"^\[([a-zA-Z0-9]+)\]\s*(.*)");
            if (match.Success)
            {
                string cand = match.Groups[1].Value;
                if (cand.Equals("PRO", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("INFO", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("ALERTA", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("AVISO", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("PARADO", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("LIMPEZA", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("ERRO", StringComparison.OrdinalIgnoreCase)
                    || cand.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    targetSerial = null;
                    targetBox = TxtLogGlobal;
                    displayMsg = msg;
                }
                else
                {
                    targetSerial = cand;
                    // Exibe só a mensagem sem o prefixo repetitivo, pois a aba já é do aparelho
                    displayMsg = match.Groups[2].Value;
                    targetBox = GetOrCreateDeviceTab(targetSerial);
                }
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
                    string dir = System.IO.Path.Combine(BotRpaUI.Core.PathHelper.GetRootDirectory(), "registros", DateTime.Now.ToString("yyyy-MM-dd"));
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
                string wpPath = "";
                
                int idx = ComboMunicipios.SelectedIndex;
                if (idx >= 0 && idx < _comboList.Count)
                {
                    var m = _municipiosArray[idx];
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        LogCallback callback = msg =>
                        {
                            Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
                        };

                        bool isNokia = device != null && device.Model != null && (device.Model.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 || device.Model.IndexOf("T20", StringComparison.OrdinalIgnoreCase) >= 0);
                        bool isM7 = device != null && device.Model != null && (device.Model.IndexOf("Multilaser", StringComparison.OrdinalIgnoreCase) >= 0 || device.Model.IndexOf("M7", StringComparison.OrdinalIgnoreCase) >= 0);

                        int result;
                        if (isNokia)
                        {
                            result = Engine_ExecuteF2_NokiaT20(finalSerial, finalApkPath, finalPacotePath, finalWpPath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        }
                        else if (isM7)
                        {
                            result = Engine_ExecuteF2_TabMulti_Atualizar(finalSerial, finalApkPath, finalPacotePath, callback);
                        }
                        else
                        {
                            result = Engine_ExecuteF2_Reinstall(finalSerial, finalApkPath, finalPacotePath, finalPackageName, finalLogin, finalSenha, finalMunicipio, callback);
                        }
                        
                        if (result == 0)
                        {
                             Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] [ERRO] Reinstalação interrompida ou falhou."));
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


        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF3_UpdatePackage(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string apk_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pacote_path, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string package_name, 
            LogCallback cb);

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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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
            PanelHistoricoLogs.Visibility = Visibility.Collapsed;
            PanelGestaoAparelho.Visibility = Visibility.Visible;

            // Enquanto o painel de Gestao do Aparelho estiver aberto, o card geral
            // "Automacao ao Conectar" e substituido pelo card de subfuncao do F4.
            if (CardAutoConectar != null) CardAutoConectar.Visibility = Visibility.Collapsed;
            if (CardAutoF4 != null) CardAutoF4.Visibility = Visibility.Visible;

            if (TxtGestaoStatus != null)
                TxtGestaoStatus.Text = "Pronto. Selecione uma opção acima ou informe comandos em lote.";
            LogMessage("\n[📱 Submódulo de Gestão do Aparelho Ativado]");
        }

        private void BtnSairGestao_Click(object sender, RoutedEventArgs e)
        {
            PanelGestaoAparelho.Visibility = Visibility.Collapsed;
            PanelHistoricoLogs.Visibility = Visibility.Visible;
            TxtInput.Visibility = Visibility.Collapsed;
            currentTerminalMode = 0;

            // Restaura o card geral de Automacao ao Conectar.
            if (CardAutoConectar != null) CardAutoConectar.Visibility = Visibility.Visible;
            if (CardAutoF4 != null) CardAutoF4.Visibility = Visibility.Collapsed;

            LogMessage("[📱 Submódulo de Gestão do Aparelho Fechado - Histórico restaurado]");
        }

        private void BtnGestaoAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string tag = btn.Tag?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(tag))
                {
                    if (TxtGestaoStatus != null)
                        TxtGestaoStatus.Text = $"⚡ Disparando ação [{tag}] para os dispositivos...";
                    ExecuteF4Command(tag);
                }
            }
        }

        private void BtnGestaoBatchRun_Click(object sender, RoutedEventArgs e)
        {
            string cmd = TxtGestaoBatchInput.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;

            if (TxtGestaoStatus != null)
                TxtGestaoStatus.Text = $"⚡ Executando lote [{cmd}]...";
            ExecuteF4Command(cmd);
            TxtGestaoBatchInput.Clear();
        }

        private void TxtGestaoBatchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnGestaoBatchRun_Click(sender, e);
            }
        }

        private void BtnGestaoProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string seq && !string.IsNullOrWhiteSpace(seq))
            {
                if (TxtGestaoStatus != null)
                    TxtGestaoStatus.Text = $"⚡ Aplicando perfil [{seq}]...";
                ExecuteF4Command(seq);
            }
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
        
        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF4_Ferramenta(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            int acao, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF4_Wallpaper(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string wp_path, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF5_Shutdown(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF6_Sincronizacao(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            LogCallback cb);

        [DllImport("BotEngine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ExecuteF8_ActiveConnection(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string serial, 
            LogCallback cb);

        private void ExecuteF4Command(string cmd)
        {
            string[] parts = cmd.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            System.Collections.Generic.List<int> acoes = new System.Collections.Generic.List<int>();

            foreach (string part in parts)
            {
                if (!int.TryParse(part.Trim(), out int acao) || acao < 1 || acao > 12)
                {
                    LogMessage($"[Erro] Comando inválido: '{part}'. Digite números de 1 a 12 (ex: 11, 12, ou 5,7).");
                    if (TxtGestaoStatus != null) TxtGestaoStatus.Text = $"⚠️ Comando inválido: '{part}'. Use números de 1 a 12.";
                    return;
                }
                acoes.Add(acao);
            }

            var targetDevices = GetTargetDevices();
            if (targetDevices.Count == 0)
            {
                LogMessage("[Erro] Nenhum aparelho conectado para receber o comando.");
                if (TxtGestaoStatus != null) TxtGestaoStatus.Text = "⚠️ Nenhum aparelho conectado para receber o comando.";
                return;
            }

            string wpPath = "";
            if (acoes.Contains(7))
            {
                if (!ResolverWallpaperF4(out wpPath)) return;
            }

            if (TxtGestaoStatus != null)
                TxtGestaoStatus.Text = $"✔ Disparando comando [{cmd}] para {targetDevices.Count} aparelho(s)...";

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

        /// <summary>
        /// Resolve o caminho absoluto do wallpaper do município selecionado.
        /// Retorna false (já registrando o erro) se o município não tiver wallpaper
        /// configurado ou se o arquivo não existir fisicamente.
        /// </summary>
        private bool ResolverWallpaperF4(out string wpPath)
        {
            wpPath = "";
            int idx = ComboMunicipios.SelectedIndex;

            if (idx < 0 || idx >= _municipiosArray.Length)
            {
                LogMessage("[Erro] Selecione um município no menu superior antes de definir o Wallpaper.");
                if (TxtGestaoStatus != null) TxtGestaoStatus.Text = "⚠️ Selecione um município no menu superior antes de definir Wallpaper.";
                return false;
            }

            wpPath = _municipiosArray[idx].WallpaperPath ?? "";
            if (string.IsNullOrEmpty(wpPath))
            {
                LogMessage("[Erro] O município selecionado não possui um Wallpaper configurado.");
                if (TxtGestaoStatus != null) TxtGestaoStatus.Text = "⚠️ O município selecionado não possui Wallpaper configurado.";
                return false;
            }

            string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
            if (!System.IO.Path.IsPathRooted(wpPath))
            {
                wpPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, wpPath));
            }

            if (!System.IO.File.Exists(wpPath))
            {
                LogMessage($"[Erro] Arquivo de Wallpaper não encontrado fisicamente em: {wpPath}");
                if (TxtGestaoStatus != null) TxtGestaoStatus.Text = $"⚠️ Arquivo de Wallpaper não encontrado: {wpPath}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Comandos F4 na MESMA ordem dos itens do ComboBox ComboAutoF4.
        /// Usado pela automação "F4 ao conectar".
        /// </summary>
        private static readonly string[] F4_COMANDOS_AUTOMACAO =
        {
            "11,1,2,5,10", // Blindar Tudo (perfil)
            "12,3,4,6",   // Restaurar Tudo (perfil)
            "1",           // Bloquear Câmera
            "3",           // Liberar Câmera
            "2",           // Restringir Configurações
            "4",           // Liberar Configurações
            "5",           // Ativar Modo Restrito
            "6",           // Desativar Modo Restrito
            "7",           // Aplicar Papel de Parede
            "8",           // Ligar Internet USB
            "9",           // Desligar Internet USB
            "10",          // Ocultar Barra de Pesquisa
            "11",          // Aplicar Blindagem de Apps
            "12"           // Reativar Apps (Restauração)
        };

        /// <summary>
        /// Executa automaticamente a subfunção F4 selecionada no card
        /// "Automação F4 ao Conectar" quando um aparelho é conectado.
        /// </summary>
        private void RunAutoF4ForDevice(DeviceModel device)
        {
            if (device == null || string.IsNullOrEmpty(device.Serial)) return;
            if (ComboAutoF4 == null) return;

            int idx = ComboAutoF4.SelectedIndex;
            if (idx < 0 || idx >= F4_COMANDOS_AUTOMACAO.Length) return;

            string cmd = F4_COMANDOS_AUTOMACAO[idx];

            // Valida e converte o comando em lista de ações numéricas.
            var acoes = new System.Collections.Generic.List<int>();
            foreach (string part in cmd.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int a) && a >= 1 && a <= 12)
                    acoes.Add(a);
            }
            if (acoes.Count == 0) return;

            // A ação 7 (Papel de Parede) exige o wallpaper do município.
            string wpPath = "";
            if (acoes.Contains(7))
            {
                if (!ResolverWallpaperF4(out wpPath)) return;
            }

            string finalSerial = device.Serial;
            LogCallback callback = new LogCallback((msg) =>
            {
                Dispatcher.Invoke(() => LogMessage($"[{finalSerial}] {msg}"));
            });

            LogMessage($"[{finalSerial}] Automação F4 ativada! Executando subfunção [{cmd}] automaticamente...");
            if (TxtGestaoStatus != null)
                TxtGestaoStatus.Text = $"✔ Automação F4 [{cmd}] disparada para {finalSerial}.";

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
                    System.Threading.Thread.Sleep(800);
                }
                GC.KeepAlive(callback);
            });
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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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
            try { Engine_CancelAll(); } catch { }
            try { Engine_SetPaused(0); } catch { }
            _isPaused = false;
            LblStatus.Text = "Status: Parado pelo usuário — encerrando processos...";
            LogMessage("[PARADO] Parada geral acionada — sinal de cancelamento enviado ao motor; encerrando apps nos aparelhos...");
            var serials = new System.Collections.Generic.List<string>();
            foreach (var dev in _devicesList)
            {
                if (!string.IsNullOrEmpty(dev.Serial) && !serials.Contains(dev.Serial))
                    serials.Add(dev.Serial);
            }
            foreach (var k in _deviceProgressBars.Keys)
            {
                if (!serials.Contains(k))
                    serials.Add(k);
            }
            if (serials.Count == 0)
            {
                LogMessage("[PARADO] Automação interrompida pelo usuário.");
                return;
            }
            foreach (var serial in serials)
            {
                string s2 = serial;
                UpdateDeviceStatus(s2, "Parado", "#FF4D4D");
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder(512);
                        Engine_RunAdbShell(s2, "am force-stop br.com.yantec.innovava && am force-stop com.google.android.documentsui && am force-stop com.android.documentsui && am kill-all", sb, 512);
                    }
                    catch { }
                    Dispatcher.Invoke(() => LogMessage($"[{s2}] Processos encerrados (Parar)."));
                });
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            int removidos = 0;
            var seriais = new System.Collections.Generic.List<string>(_deviceLogBoxes.Keys);
            foreach (var s in seriais)
            {
                var tb = _deviceLogBoxes[s];
                if (tb.Parent is TabItem tab)
                {
                    LogTabs.Items.Remove(tab);
                }
                removidos++;
            }
            _deviceLogBoxes.Clear();
            _deviceLogCounts.Clear();
            _deviceLogCounts["GLOBAL"] = 0;
            GlobalProgressPanel.Children.Clear();
            _deviceProgressContainers.Clear();
            _deviceProgressBars.Clear();
            _deviceProgressLabels.Clear();
            _deviceProgressSerialLabels.Clear();
            if (TxtLogGlobal != null)
                TxtLogGlobal.Clear();
            if (ExecutionProgress != null)
                ExecutionProgress.Value = 0;
            if (LblProgress != null)
                LblProgress.Text = "0%";
            // Varredura final: fecha qualquer aba órfã restante (ex: [LIMPEZA], [PARADO]
            // ou dispositivo desconectado) preservando apenas a aba Global (Sistema).
            for (int i = LogTabs.Items.Count - 1; i >= 0; i--)
            {
                if (LogTabs.Items[i] is TabItem resto)
                {
                    string hdr = resto.Header?.ToString() ?? string.Empty;
                    if (!hdr.Contains("Global"))
                    {
                        LogTabs.Items.RemoveAt(i);
                        removidos++;
                    }
                }
            }
            LogMessage($"[LIMPEZA] {removidos} histórico(s) removido(s). Limpeza total (geral + todos dispositivos).");
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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
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
                    string baseDir = BotRpaUI.Core.PathHelper.GetRootDirectory();
                    
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

        public void UpdateCategoryVisibility()
        {
            bool v7 = BotRpaUI.Core.SettingsManager.EnableDeviceVision7;
            bool m7 = BotRpaUI.Core.SettingsManager.EnableDeviceTabMulti;
            bool t20 = BotRpaUI.Core.SettingsManager.EnableDeviceNokiaT20;

            if (BtnCategoryPositivo != null) BtnCategoryPositivo.Visibility = v7 ? Visibility.Visible : Visibility.Collapsed;
            if (BtnCategoryMultilaser != null) BtnCategoryMultilaser.Visibility = m7 ? Visibility.Visible : Visibility.Collapsed;
            if (BtnCategoryNokia != null) BtnCategoryNokia.Visibility = t20 ? Visibility.Visible : Visibility.Collapsed;

            // Se a categoria atualmente aberta foi desativada, alterna para uma que esteja ativa
            if (GridPositivoActions.Visibility == Visibility.Visible && !v7)
            {
                if (m7 && BtnCategoryMultilaser != null) BtnCategoryMultilaser_Click(BtnCategoryMultilaser, new RoutedEventArgs());
                else if (t20 && BtnCategoryNokia != null) BtnCategoryNokia_Click(BtnCategoryNokia, new RoutedEventArgs());
                else
                {
                    GridPositivoActions.Visibility = Visibility.Collapsed;
                    GridMultilaserActions.Visibility = Visibility.Collapsed;
                    if (GridNokiaActions != null) GridNokiaActions.Visibility = Visibility.Collapsed;
                }
            }
            else if (GridMultilaserActions.Visibility == Visibility.Visible && !m7)
            {
                if (v7 && BtnCategoryPositivo != null) BtnCategoryPositivo_Click(BtnCategoryPositivo, new RoutedEventArgs());
                else if (t20 && BtnCategoryNokia != null) BtnCategoryNokia_Click(BtnCategoryNokia, new RoutedEventArgs());
                else
                {
                    GridPositivoActions.Visibility = Visibility.Collapsed;
                    GridMultilaserActions.Visibility = Visibility.Collapsed;
                    if (GridNokiaActions != null) GridNokiaActions.Visibility = Visibility.Collapsed;
                }
            }
            else if (GridNokiaActions != null && GridNokiaActions.Visibility == Visibility.Visible && !t20)
            {
                if (v7 && BtnCategoryPositivo != null) BtnCategoryPositivo_Click(BtnCategoryPositivo, new RoutedEventArgs());
                else if (m7 && BtnCategoryMultilaser != null) BtnCategoryMultilaser_Click(BtnCategoryMultilaser, new RoutedEventArgs());
                else
                {
                    GridPositivoActions.Visibility = Visibility.Collapsed;
                    GridMultilaserActions.Visibility = Visibility.Collapsed;
                    if (GridNokiaActions != null) GridNokiaActions.Visibility = Visibility.Collapsed;
                }
            }
            else if (GridPositivoActions.Visibility != Visibility.Visible && GridMultilaserActions.Visibility != Visibility.Visible && (GridNokiaActions == null || GridNokiaActions.Visibility != Visibility.Visible))
            {
                // Se tudo estava fechado e alguma chave foi ativada
                if (v7 && BtnCategoryPositivo != null) BtnCategoryPositivo_Click(BtnCategoryPositivo, new RoutedEventArgs());
                else if (m7 && BtnCategoryMultilaser != null) BtnCategoryMultilaser_Click(BtnCategoryMultilaser, new RoutedEventArgs());
                else if (t20 && BtnCategoryNokia != null) BtnCategoryNokia_Click(BtnCategoryNokia, new RoutedEventArgs());
            }
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
            return BotRpaUI.Core.PathHelper.GetDynamicApkPath(municipio);
        }

        private string GetDynamicPacotePath(string municipio)
        {
            return BotRpaUI.Core.PathHelper.GetDynamicPacotePath(municipio);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && PanelGestaoAparelho != null && PanelGestaoAparelho.Visibility == Visibility.Visible)
            {
                BtnSairGestao_Click(sender, e);
                e.Handled = true;
                return;
            }

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
