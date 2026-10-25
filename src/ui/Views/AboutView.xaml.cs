using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BotRpaUI.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
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

            // Resetar botões do menu lateral
            BtnSubGuia.Foreground = brushNormal;
            BtnSubGuia.Background = bgNormal;
            BtnSubFaq.Foreground = brushNormal;
            BtnSubFaq.Background = bgNormal;
            BtnSubArquitetura.Foreground = brushNormal;
            BtnSubArquitetura.Background = bgNormal;

            // Ativar botão clicado
            activeButton.Foreground = brushActive;
            activeButton.Background = bgActive;

            // Ocultar todos os painéis
            PanelGuia.Visibility = Visibility.Collapsed;
            PanelFaq.Visibility = Visibility.Collapsed;
            PanelArquitetura.Visibility = Visibility.Collapsed;

            // Mostrar o painel ativo
            activePanel.Visibility = Visibility.Visible;
        }
    }
}
