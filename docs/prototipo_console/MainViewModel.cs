using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;

namespace PrototipoConsole;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal static class Palette
{
    public static readonly Brush Accent = Create(0xD0, 0x8B, 0x45);
    public static readonly Brush Warning = Create(0xD9, 0xA4, 0x41);
    public static readonly Brush Success = Create(0x5A, 0xB8, 0x8A);
    public static readonly Brush Danger = Create(0xD9, 0x6A, 0x6A);
    public static readonly Brush TextSecondary = Create(0x98, 0xA1, 0xAF);
    public static readonly Brush TextTertiary = Create(0x6B, 0x74, 0x82);
    public static readonly Brush SurfaceRaised = Create(0x16, 0x1A, 0x20);
    public static readonly Brush SurfaceHover = Create(0x1B, 0x20, 0x27);
    public static readonly Brush Border = Create(0x23, 0x28, 0x2F);
    public static readonly Brush BorderStrong = Create(0x30, 0x37, 0x41);
    public static readonly Brush SuccessSoft = Create(0x14, 0x23, 0x1B);
    public static readonly Brush SuccessBorder = Create(0x2A, 0x4A, 0x39);
    public static readonly Brush WarningSoft = Create(0x24, 0x1D, 0x11);
    public static readonly Brush WarningBorder = Create(0x4B, 0x3A, 0x1F);
    public static readonly Brush DangerSoft = Create(0x25, 0x16, 0x16);
    public static readonly Brush DangerBorder = Create(0x4A, 0x2C, 0x2C);

    private static Brush Create(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}

public sealed class StepItem
{
    public string Number { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Duration { get; init; } = "—";
    public string Status { get; init; } = "Pending";
    public bool IsLast { get; init; }
}

public sealed class LogItem
{
    public string Time { get; init; } = string.Empty;
    public string Level { get; init; } = "INFO";
    public string Message { get; init; } = string.Empty;
    public string Category { get; init; } = "Execução";

    public Brush LevelBrush => Level switch
    {
        "OK" => Palette.Success,
        "WARN" => Palette.Warning,
        "ERR" => Palette.Danger,
        _ => Palette.TextSecondary
    };

    public Brush LevelBackground => Level switch
    {
        "OK" => Palette.SuccessSoft,
        "WARN" => Palette.WarningSoft,
        "ERR" => Palette.DangerSoft,
        _ => Palette.SurfaceRaised
    };

    public Brush LevelBorder => Level switch
    {
        "OK" => Palette.SuccessBorder,
        "WARN" => Palette.WarningBorder,
        "ERR" => Palette.DangerBorder,
        _ => Palette.Border
    };
}

public sealed class DeviceItem : ObservableObject
{
    private string _status = "Pronto";
    private double _progress;
    private string _stepLabel = string.Empty;
    private string _currentStep = string.Empty;
    private string _pipeline = string.Empty;
    private string _elapsed = "—";
    private string _eta = "—";
    private string _nextAction = string.Empty;
    private bool _isPaused;

    public string Model { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public string Mac { get; init; } = string.Empty;
    public ObservableCollection<StepItem> Steps { get; } = new();

    public string Status
    {
        get => _status;
        set
        {
            if (!Set(ref _status, value))
                return;

            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(StatusBorder));
            OnPropertyChanged(nameof(ShowProgress));
            OnPropertyChanged(nameof(ProgressBrush));
        }
    }

    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    public string StepLabel
    {
        get => _stepLabel;
        set => Set(ref _stepLabel, value);
    }

    public string CurrentStep
    {
        get => _currentStep;
        set => Set(ref _currentStep, value);
    }

    public string Pipeline
    {
        get => _pipeline;
        set => Set(ref _pipeline, value);
    }

    public string Elapsed
    {
        get => _elapsed;
        set => Set(ref _elapsed, value);
    }

    public string Eta
    {
        get => _eta;
        set => Set(ref _eta, value);
    }

    public string NextAction
    {
        get => _nextAction;
        set => Set(ref _nextAction, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (!Set(ref _isPaused, value))
                return;

            OnPropertyChanged(nameof(PauseButtonText));
            OnPropertyChanged(nameof(PauseGlyph));
        }
    }

    public string PauseButtonText => IsPaused ? "Retomar" : "Pausar";
    public string PauseGlyph => IsPaused ? "\uE768" : "\uE769";

    public bool ShowProgress => Status is "Executando" or "Pausado" or "Falha";

    public Brush ProgressBrush => Status switch
    {
        "Pausado" => Palette.Warning,
        "Falha" => Palette.Danger,
        "Cancelado" => Palette.TextTertiary,
        _ => Palette.Accent
    };

    public Brush StatusBrush => Status switch
    {
        "Executando" => Palette.Accent,
        "Pausado" => Palette.Warning,
        "Pronto" => Palette.Success,
        "Falha" => Palette.Danger,
        "Cancelado" => Palette.TextTertiary,
        _ => Palette.TextSecondary
    };

    public Brush StatusBackground => Status switch
    {
        "Executando" => Palette.WarningSoft,
        "Pausado" => Palette.WarningSoft,
        "Pronto" => Palette.SuccessSoft,
        "Falha" => Palette.DangerSoft,
        _ => Palette.SurfaceRaised
    };

    public Brush StatusBorder => Status switch
    {
        "Executando" => Palette.WarningBorder,
        "Pausado" => Palette.WarningBorder,
        "Pronto" => Palette.SuccessBorder,
        "Falha" => Palette.DangerBorder,
        _ => Palette.Border
    };
}

public sealed class MainViewModel : ObservableObject
{
    private string _searchText = string.Empty;
    private string _logCategory = "Execução";
    private DeviceItem? _selectedDevice;

    public ObservableCollection<DeviceItem> Devices { get; }
    public ObservableCollection<LogItem> Logs { get; }
    public ICollectionView DevicesView { get; }
    public ICollectionView LogsView { get; }

    public string SessionName => "Sessão 024";
    public string SessionLocation => "São Paulo · 20 tablets";
    public int Completed { get; } = 12;
    public int Total { get; } = 20;
    public double SessionProgress => Completed * 100.0 / Total;
    public string SessionProgressLabel => $"{Completed} de {Total} concluídos";
    public string ConnectedLabel => "4 conectados";
    public string RunningLabel => "1 em execução";
    public string QueuedLabel => "1 na fila";

    public DeviceItem? SelectedDevice
    {
        get => _selectedDevice;
        set => Set(ref _selectedDevice, value);
    }

    public MainViewModel()
    {
        Devices = new ObservableCollection<DeviceItem>(BuildDevices());
        Logs = new ObservableCollection<LogItem>(BuildLogs());

        DevicesView = CollectionViewSource.GetDefaultView(Devices);
        DevicesView.Filter = FilterDevice;

        LogsView = CollectionViewSource.GetDefaultView(Logs);
        LogsView.Filter = FilterLog;

        SelectedDevice = Devices[0];
    }

    public void SetSearch(string text)
    {
        _searchText = text ?? string.Empty;
        DevicesView.Refresh();
    }

    public void SetLogCategory(string category)
    {
        _logCategory = category;
        LogsView.Refresh();
    }

    public void PauseSelected()
    {
        if (SelectedDevice is null || SelectedDevice.Status is not ("Executando" or "Pausado"))
            return;

        SelectedDevice.IsPaused = !SelectedDevice.IsPaused;
        SelectedDevice.Status = SelectedDevice.IsPaused ? "Pausado" : "Executando";
        AddLog(
            SelectedDevice.IsPaused ? "WARN" : "OK",
            $"{SelectedDevice.Serial} · pipeline {(SelectedDevice.IsPaused ? "pausada" : "retomada")} pelo operador");
    }

    public void CancelSelected()
    {
        if (SelectedDevice is null || SelectedDevice.Status is not ("Executando" or "Pausado"))
            return;

        SelectedDevice.IsPaused = false;
        SelectedDevice.Status = "Cancelado";
        SelectedDevice.Progress = 0;
        SelectedDevice.StepLabel = "Cancelado";
        SelectedDevice.CurrentStep = "Execução cancelada pelo operador";
        AddLog("WARN", $"{SelectedDevice.Serial} · execução cancelada pelo operador");
    }

    public void StartSession()
    {
        AddLog("INFO", "Nova sessão preparada · aguardando seleção de lote", "Sistema");
    }

    public void RefreshDevices()
    {
        AddLog("OK", "Varredura ADB concluída · 4 dispositivos conectados", "Sistema");
    }

    public void AddLog(string level, string message, string category = "Execução")
    {
        Logs.Insert(0, new LogItem
        {
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Level = level,
            Message = message,
            Category = category
        });

        LogsView.Refresh();
    }

    private bool FilterDevice(object item)
    {
        if (item is not DeviceItem device)
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        string query = _searchText.Trim();
        return device.Model.Contains(query, StringComparison.OrdinalIgnoreCase)
            || device.Serial.Contains(query, StringComparison.OrdinalIgnoreCase)
            || device.Mac.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterLog(object item)
    {
        return item is LogItem log && log.Category == _logCategory;
    }

    private static IEnumerable<DeviceItem> BuildDevices()
    {
        var running = new DeviceItem
        {
            Model = "Vision 7",
            Serial = "T30764G2507010672",
            Mac = "A4:2B:8C:1D:7E:02",
            Status = "Executando",
            Pipeline = "F1 · Instalação completa",
            StepLabel = "7/9 · Login InnoAI",
            CurrentStep = "Etapa 7 de 9 · preenchendo credenciais do município",
            Progress = 64,
            Elapsed = "04:12",
            Eta = "02:30",
            NextAction = "F5 · Desligamento"
        };

        running.Steps.Add(Step("0", "Preparação", "00:18", "Done"));
        running.Steps.Add(Step("1.1", "Trava portrait", "00:04", "Done"));
        running.Steps.Add(Step("1.2", "Captura MAC", "00:11", "Done"));
        running.Steps.Add(Step("1.5", "Debloat", "01:42", "Done"));
        running.Steps.Add(Step("1.6", "Push pacote", "00:37", "Done"));
        running.Steps.Add(Step("1.7", "Permissões", "00:08", "Done"));
        running.Steps.Add(Step("2.0", "Login InnoAI", "em andamento", "Current"));
        running.Steps.Add(Step("2.1", "Importar ZIP", "—", "Pending"));
        running.Steps.Add(Step("7.0", "Desligar", "—", "Pending", true));

        var ready = new DeviceItem
        {
            Model = "Nokia T20",
            Serial = "N20A22190488",
            Mac = "8C:AA:12:44:9D:10",
            Status = "Pronto",
            Pipeline = "Nenhuma execução ativa",
            StepLabel = "Sem execução",
            CurrentStep = "Selecione uma ação para iniciar",
            NextAction = "F1 · Instalação completa"
        };

        ready.Steps.Add(Step("0", "Preparação", "—", "Pending"));
        ready.Steps.Add(Step("1.1", "Trava portrait", "—", "Pending"));
        ready.Steps.Add(Step("1.2", "Captura MAC", "—", "Pending"));
        ready.Steps.Add(Step("1.5", "Debloat", "—", "Pending"));
        ready.Steps.Add(Step("1.6", "Push pacote", "—", "Pending"));
        ready.Steps.Add(Step("1.7", "Permissões", "—", "Pending"));
        ready.Steps.Add(Step("2.0", "Login InnoAI", "—", "Pending"));
        ready.Steps.Add(Step("2.1", "Importar ZIP", "—", "Pending"));
        ready.Steps.Add(Step("7.0", "Desligar", "—", "Pending", true));

        var queued = new DeviceItem
        {
            Model = "TabMulti M7",
            Serial = "M7X99887721",
            Mac = "B0:23:45:6F:11:8A",
            Status = "Na fila",
            Pipeline = "F1 · Instalação completa",
            StepLabel = "Aguardando vez",
            CurrentStep = "Na fila · posição 2",
            NextAction = "F1 · Instalação completa"
        };

        queued.Steps.Add(Step("0", "Preparação", "—", "Pending"));
        queued.Steps.Add(Step("1.1", "Trava portrait", "—", "Pending"));
        queued.Steps.Add(Step("1.2", "Captura MAC", "—", "Pending"));
        queued.Steps.Add(Step("1.5", "Debloat", "—", "Pending"));
        queued.Steps.Add(Step("1.6", "Push pacote", "—", "Pending"));
        queued.Steps.Add(Step("1.7", "Permissões", "—", "Pending"));
        queued.Steps.Add(Step("2.0", "Login InnoAI", "—", "Pending"));
        queued.Steps.Add(Step("2.1", "Importar ZIP", "—", "Pending"));
        queued.Steps.Add(Step("7.0", "Desligar", "—", "Pending", true));

        var failed = new DeviceItem
        {
            Model = "Vision 7",
            Serial = "T30764G2601006469",
            Mac = "A4:2B:8C:1D:80:6C",
            Status = "Falha",
            Pipeline = "F1 · Instalação completa",
            StepLabel = "1.6 · Push pacote",
            CurrentStep = "Falha na etapa 1.6 · dispositivo desconectado",
            Progress = 36,
            Elapsed = "02:05",
            Eta = "—",
            NextAction = "Repetir etapa 1.6"
        };

        failed.Steps.Add(Step("0", "Preparação", "00:18", "Done"));
        failed.Steps.Add(Step("1.1", "Trava portrait", "00:04", "Done"));
        failed.Steps.Add(Step("1.2", "Captura MAC", "00:11", "Done"));
        failed.Steps.Add(Step("1.5", "Debloat", "01:42", "Done"));
        failed.Steps.Add(Step("1.6", "Push pacote", "falhou", "Error"));
        failed.Steps.Add(Step("1.7", "Permissões", "—", "Pending"));
        failed.Steps.Add(Step("2.0", "Login InnoAI", "—", "Pending"));
        failed.Steps.Add(Step("2.1", "Importar ZIP", "—", "Pending"));
        failed.Steps.Add(Step("7.0", "Desligar", "—", "Pending", true));

        return new[] { running, ready, queued, failed };
    }

    private static IEnumerable<LogItem> BuildLogs()
    {
        return new[]
        {
            Log("10:42:18", "INFO", "T30764G… · Login InnoAI: preenchendo credenciais do município"),
            Log("10:42:11", "OK", "T30764G… · Etapa 1.7 concluída · permissões aplicadas"),
            Log("10:42:03", "INFO", "T30764G… · Push de pacote concluído (28,4 MB)"),
            Log("10:41:26", "WARN", "N20A221… · Bateria em 42% · recomenda-se carregar antes do F1"),
            Log("10:40:58", "OK", "M7X9988… · ADB autorizado e dispositivo pronto"),
            Log("10:39:44", "ERR", "T30764G…6069 · Falha no push: dispositivo desconectado"),
            Log("10:39:12", "INFO", "Varredura ADB concluída · 4 dispositivos encontrados", "Sistema"),
            Log("10:38:50", "INFO", "T30764G… · Pipeline F1 iniciado · lote 024"),
            Log("10:38:20", "OK", "Cofre DPAPI carregado · credenciais do município liberadas", "Sistema"),
            Log("10:37:55", "INFO", "Sessão 024 aberta · São Paulo · 20 tablets", "Sistema"),
            Log("10:37:41", "OK", "Servidor ADB iniciado · platform-tools 35.0.2", "Sistema"),
            Log("10:37:38", "INFO", "TACE v21.10.2026.1728 · bancada 03", "Sistema")
        };
    }

    private static StepItem Step(string number, string name, string duration, string status, bool last = false)
    {
        return new StepItem
        {
            Number = number,
            Name = name,
            Duration = duration,
            Status = status,
            IsLast = last
        };
    }

    private static LogItem Log(string time, string level, string message, string category = "Execução")
    {
        return new LogItem
        {
            Time = time,
            Level = level,
            Message = message,
            Category = category
        };
    }
}
