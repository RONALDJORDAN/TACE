using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TACEUI.Core;

namespace TACEUI.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
            Loaded += (_, _) => AtualizarDados();
        }

        public void AtualizarDados()
        {
            try
            {
                var dispositivos = SQLiteDatabaseManager.LerTodos();
                int total = dispositivos.Count;
                int comPatrimonio = dispositivos.Count(d => !string.IsNullOrWhiteSpace(d.Patrimonio));
                int comMac = dispositivos.Count(d => MacValido(d.MAC));
                int completos = dispositivos.Count(d =>
                    !string.IsNullOrWhiteSpace(d.Patrimonio) &&
                    SerialValido(d.Serial) &&
                    MacValido(d.MAC) &&
                    ModeloIdentificado(d.Modelo));
                int modelos = dispositivos
                    .Where(d => ModeloIdentificado(d.Modelo))
                    .Select(d => d.Modelo.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                int pendencias = Math.Max(0, total - completos);

                double percPatrimonio = total > 0 ? comPatrimonio * 100.0 / total : 0;
                double percMac = total > 0 ? comMac * 100.0 / total : 0;
                double percCompleto = total > 0 ? completos * 100.0 / total : 0;

                string versao = AppInfo.CurrentVersion;
                string windows = Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";
                string dotnet = Environment.Version.ToString(3);
                string ambiente = $"{windows} · .NET {dotnet}";

                string registrosDir = PathHelper.GetRegistrosDirectory();
                int totalArquivos = 0;
                try
                {
                    if (Directory.Exists(registrosDir))
                        totalArquivos = Directory.EnumerateFiles(registrosDir, "*", SearchOption.AllDirectories).Count();
                }
                catch { }

                var resumo = AuditLogManager.ObterResumo();

                if (TxtSobreVersao != null) TxtSobreVersao.Text = $"v{versao}";
                if (TxtSobreVersaoInstalada != null) TxtSobreVersaoInstalada.Text = versao;
                if (TxtSobreVersaoMenu != null) TxtSobreVersaoMenu.Text = $"v{versao}";
                if (TxtSobreAmbiente != null) TxtSobreAmbiente.Text = ambiente;
                if (TxtSobreAmbienteMenu != null) TxtSobreAmbienteMenu.Text = ambiente;
                if (TxtSobreStatus != null) TxtSobreStatus.Text = "Operacional";
                if (TxtSobreStatusMenu != null) TxtSobreStatusMenu.Text = "Operacional";
                if (TxtSobreAtualizado != null) TxtSobreAtualizado.Text = $"Atualizado em {DateTime.Now:dd/MM/yyyy HH:mm}";

                if (TxtSobreEmpresa != null) TxtSobreEmpresa.Text = "InnovEdu";
                if (TxtSobreSetor != null) TxtSobreSetor.Text = "Operacional";
                if (TxtSobreDispositivos != null) TxtSobreDispositivos.Text = total.ToString("N0");
                if (TxtSobrePatrimonios != null) TxtSobrePatrimonios.Text = comPatrimonio.ToString("N0");
                if (TxtSobreMacs != null) TxtSobreMacs.Text = comMac.ToString("N0");
                if (TxtSobreAuditoria != null) TxtSobreAuditoria.Text = resumo.Total.ToString("N0");
                if (TxtSobreCobertura != null) TxtSobreCobertura.Text = $"{percCompleto:F1}%";
                if (TxtSobreModelos != null) TxtSobreModelos.Text = modelos.ToString("N0");
                if (TxtSobrePendencias != null) TxtSobrePendencias.Text = pendencias.ToString("N0");
                if (TxtSobreLogs != null) TxtSobreLogs.Text = $"{totalArquivos:N0} arquivo(s)";
                if (TxtSobreBanco != null) TxtSobreBanco.Text = "data/bot_database.db";
                if (TxtSobreRegistros != null) TxtSobreRegistros.Text = "docs/registros";
                if (TxtSobreRuntime != null) TxtSobreRuntime.Text = $"{windows} · build {Environment.OSVersion.Version.Build} · .NET {dotnet}";

                if (TxtSobrePatrimonioPct != null) TxtSobrePatrimonioPct.Text = $"{percPatrimonio:F1}%";
                if (TxtSobreMacPct != null) TxtSobreMacPct.Text = $"{percMac:F1}%";
                if (TxtSobreCompletoPct != null) TxtSobreCompletoPct.Text = $"{percCompleto:F1}%";
                if (BarSobrePatrimonio != null) BarSobrePatrimonio.Value = percPatrimonio;
                if (BarSobreMac != null) BarSobreMac.Value = percMac;
                if (BarSobreCompleto != null) BarSobreCompleto.Value = percCompleto;

                if (TxtSobreUltimoEvento != null)
                {
                    if (string.IsNullOrWhiteSpace(resumo.UltimaAcao))
                    {
                        TxtSobreUltimoEvento.Text = "Nenhum evento registrado.";
                    }
                    else
                    {
                        string data = "data indisponível";
                        if (DateTime.TryParseExact(resumo.UltimaDataHora, "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime ultimaData))
                        {
                            data = ultimaData.ToString("dd/MM/yyyy HH:mm");
                        }

                        TxtSobreUltimoEvento.Text = $"Última ação: {resumo.UltimaAcao} · {data} · {resumo.Total:N0} evento(s) registrados.";
                    }
                }

                if (TxtSobreCaminhoDados != null) TxtSobreCaminhoDados.Text = SQLiteDatabaseManager.GetDatabasePath();
                if (TxtSobreCaminhoRegistros != null) TxtSobreCaminhoRegistros.Text = registrosDir;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AboutView] Erro ao atualizar dados: {ex.Message}");
                if (TxtSobreStatus != null) TxtSobreStatus.Text = "Verificar";
                if (TxtSobreStatusMenu != null) TxtSobreStatusMenu.Text = "Verificar";
            }
        }

        private static bool SerialValido(string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            string valor = serial.Trim();
            return !valor.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("pendente", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("n/a", StringComparison.OrdinalIgnoreCase)
                && !valor.StartsWith("?", StringComparison.Ordinal);
        }

        private static bool MacValido(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return false;
            string valor = mac.Trim();
            if (valor.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase) ||
                valor == "---" || valor == "02:00:00:00:00:00")
            {
                return false;
            }

            return Regex.IsMatch(valor, @"^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$")
                || Regex.IsMatch(valor, @"^[0-9A-Fa-f]{12}$");
        }

        private static bool ModeloIdentificado(string? modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo)) return false;
            string valor = modelo.Trim();
            return !valor.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase)
                && !valor.Equals("N/A", StringComparison.OrdinalIgnoreCase);
        }

        private void BtnSubGuia_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubGuia, PanelGuia);
        }

        private void BtnSubFaq_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubFaq, PanelFaq);
        }

        private void BtnSubArquitetura_Click(object sender, RoutedEventArgs e)
        {
            SwitchSubmodule(BtnSubArquitetura, PanelArquitetura);
        }

        private void SwitchSubmodule(Button activeButton, FrameworkElement activePanel)
        {
            var brushNormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
            var bgNormal = Brushes.Transparent;
            var brushActive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EDEA3"));
            var bgActive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A40"));

            BtnSubGuia.Foreground = brushNormal;
            BtnSubGuia.Background = bgNormal;
            BtnSubFaq.Foreground = brushNormal;
            BtnSubFaq.Background = bgNormal;
            BtnSubArquitetura.Foreground = brushNormal;
            BtnSubArquitetura.Background = bgNormal;

            activeButton.Foreground = brushActive;
            activeButton.Background = bgActive;

            PanelGuia.Visibility = Visibility.Collapsed;
            PanelFaq.Visibility = Visibility.Collapsed;
            PanelArquitetura.Visibility = Visibility.Collapsed;

            activePanel.Visibility = Visibility.Visible;
        }
    }
}
