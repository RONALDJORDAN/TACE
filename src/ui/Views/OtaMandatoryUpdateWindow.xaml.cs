using System;
using System.Threading.Tasks;
using System.Windows;
using BotRpaUI.Core;

namespace BotRpaUI.Views
{
    public partial class OtaMandatoryUpdateWindow : Window
    {
        private readonly OtaManifest _manifest;
        private readonly OtaUpdateManager _otaManager = new OtaUpdateManager();

        public OtaMandatoryUpdateWindow(OtaManifest manifest)
        {
            InitializeComponent();
            _manifest = manifest;

            TxtNewVersionBadge.Text = $"v{_manifest.Version}";
            TxtChangelog.Text = string.IsNullOrWhiteSpace(_manifest.Changelog) 
                ? "• Atualização de segurança, correções de bugs e otimizações gerais de esteira." 
                : _manifest.Changelog;

            Loaded += OtaMandatoryUpdateWindow_Loaded;
        }

        private async void OtaMandatoryUpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await StartAutoUpdateSequenceAsync();
        }

        private async Task StartAutoUpdateSequenceAsync()
        {
            try
            {
                TxtProgressStatus.Text = "⚡ Conectando ao Firebase Storage...";
                ProgressBarUpdate.Value = 10;

                // 1. Atualiza telemetria para status 'UPDATING'
                _ = _otaManager.SendTelemetryPingAsync(_manifest.Version, "UPDATING");

                // 2. Download do pacote de atualização com callback de progresso
                var progress = new Progress<double>(percent =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        int pInt = (int)Math.Round(percent);
                        ProgressBarUpdate.Value = pInt;
                        TxtProgressPercent.Text = $"{pInt}%";
                        TxtProgressStatus.Text = pInt < 100 
                            ? $"🚀 Baixando arquivo .ZIP ({pInt}%)..." 
                            : "✅ Validador SHA-256 processando...";
                    });
                });

                var downloadResult = await _otaManager.DownloadAndVerifyUpdateAsync(_manifest, progress);
                bool success = downloadResult.Success;
                string? filePath = downloadResult.FilePath;
                string? errorMessage = downloadResult.ErrorMessage;

                if (!success || string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.Show(
                        $"Falha ao baixar a atualização obrigatória: {errorMessage ?? "Erro desconhecido"}.\nO sistema iniciará offline.",
                        "Erro de Atualização OTA",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    Close();
                    return;
                }

                TxtProgressStatus.Text = "⚡ Aplicando atualização e reiniciando sistema...";
                ProgressBarUpdate.Value = 100;
                TxtProgressPercent.Text = "100%";

                await Task.Delay(1000);

                // 3. Aplica a atualização (fecha a UI, extrai o ZIP sobre a pasta e reinicia)
                _otaManager.ApplyUpdate(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro durante a atualização automática: {ex.Message}", "Erro Inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
    }
}
