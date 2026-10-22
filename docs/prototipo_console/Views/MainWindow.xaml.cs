using System.Windows;
using System.Windows.Controls;

namespace PrototipoConsole;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageBench is null || PagePlaceholder is null)
            return;

        if (sender is not RadioButton radio || radio.Tag is not string module)
            return;

        bool isBench = module == "Bancada";
        PageBench.Visibility = isBench ? Visibility.Visible : Visibility.Collapsed;
        PagePlaceholder.Visibility = isBench ? Visibility.Collapsed : Visibility.Visible;

        if (isBench)
            return;

        TxtPlaceholderTitle.Text = module;
        TxtPlaceholderBody.Text = "Este módulo ainda não faz parte do protótipo de bancada. O foco desta etapa é o console de operação.";
        TxtPlaceholderIcon.Text = module switch
        {
            "Lote" => "\uEA37",
            "Dispositivos" => "\uE772",
            "Registros" => "\uE81C",
            "Configurações" => "\uE713",
            _ => "\uE80F"
        };
    }

    private void BtnBackToBench_Click(object sender, RoutedEventArgs e)
    {
        NavBench.IsChecked = true;
    }

    private void LogTab_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            sender is RadioButton radio &&
            radio.Tag is string category)
        {
            viewModel.SetLogCategory(category);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.SetSearch(TxtSearch.Text);
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.PauseSelected();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.CancelSelected();
    }

    private void BtnNewSession_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.StartSession();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.RefreshDevices();
    }

    private void BtnTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.AddLog("INFO", "Terminal do dispositivo aberto · protótipo", "Sistema");
    }

    private void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedDevice is null)
            return;

        DeviceItem device = viewModel.SelectedDevice;
        if (device.Status != "Falha")
            return;

        device.Status = "Executando";
        device.StepLabel = "1.6 · Push pacote";
        device.CurrentStep = "Etapa 5 de 9 · repetindo push do pacote";
        viewModel.AddLog("INFO", $"{device.Serial} · repetindo etapa 1.6");
    }
}
